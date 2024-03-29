﻿// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KISAPIv2;
using KSPDev.LogUtils;
using KSPDev.GUIUtils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>Internal class that holds inventory slot logic.</summary>
/// <remarks>
/// Every slot can only hold items that refer the same part proto. The persisted properties on the items should be
/// "similar". If two parts of the same kind are too different with regard to their internal state, they cannot stack to
/// the same slot.
/// </remarks>
/// <seealso cref="KisContainerWithSlots"/>
sealed class InventorySlotImpl {

  #region Localizable strings
  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message DifferentPartsReasonText = new Message(
      "#KIS2-TBD",
      defaultTemplate: "Different parts",
      description: "Error message that is presented when parts cannot be added to the inventory"
          + " slot due to some of them don't match to each other or the other items in the slot.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message DifferentResourcesReasonText = new Message(
      "#KIS2-TBD",
      defaultTemplate: "Different resources",
      description: "Error message that is presented when parts cannot be added to the inventory"
      + " slot due to their resources or their amounts are too different between each other or the"
      + " slot's items.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message DifferentResourceAmountsReasonText = new Message(
      "#KIS2-TBD",
      defaultTemplate: "Different resource amounts",
      description: "Error message that is presented when parts cannot be added to the inventory"
      + " slot due to their resource amounts are too different between each other or the slot's"
      + " items.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message DifferentVariantReasonText = new Message(
      "#KIS2-TBD",
      defaultTemplate: "Different part variant",
      description: "Error message that is presented when parts cannot be added to the inventory"
      + " slot due to their variants are different between each other or with the slot's items.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<int> SlotIsFullReasonText = new(
      "#KIS2-TBD",
      defaultTemplate: "Slot is full, max stack size is <<1>>",
      description: "Error message that is presented when the parts cannot be dropped into teh slot due to it doesn't"
      + " have enough spare space.\n"
      + " The <<1>> argument is the maximum allowed size.\n");
  #endregion

  #region API properties and fields
  // ReSharper disable MemberCanBePrivate.Global

  /// <summary>
  /// Short name of the checking error for the case when parts with different signature are being added to the slot.
  /// </summary>
  /// <seealso cref="DifferentPartsReasonText"/>
  public const string DifferentPartReason = "DifferentPart";

  /// <summary>Short name of the checking error for the case when parts have different resource sets.</summary>
  /// <seealso cref="DifferentResourcesReasonText"/>
  public const string DifferentResourcesReason = "DifferentResources";

  /// <summary>
  /// Short name of the checking error for the case when parts have the same resources, but the amounts are too to be
  /// stored in the same slot.
  /// </summary>
  /// <seealso cref="DifferentResourcesReasonText"/>
  public const string DifferentResourceAmountsReason = "DifferentResourcesAmounts";

  /// <summary>
  /// Short name of the checking error for the case when parts with different variants are being added to the slot.
  /// </summary>
  /// <seealso cref="DifferentVariantReasonText"/>
  public const string DifferentVariantReason = "DifferentVariant";

  /// <summary>The target slot doesn't have space to fit all the items.</summary>
  /// <seealso cref="KisContainerWithSlots.maxKisSlotSize"/>
  public const string SlotIsFullReason = "kisSlotIsFull";

  /// <summary>The inventory that owns this slot.</summary>
  /// <value>The inventory instance. It's never <c>null</c>.</value>
  public readonly KisContainerWithSlots ownerInventory;

  /// <summary>Tells if this slot is visible in the inventory dialog.</summary>
  /// <remarks>
  /// Slot can be invisible in two cases: the dialog is closed or there are not enough UI elements to represent all the
  /// slots in the dialog. In the latter case, the tail slots become invisible.
  /// </remarks>
  public bool isVisible => _unitySlot != null;

  /// <summary>
  /// Tells if the slot is participating in a multi-frame operation and must not get removed or updated.
  /// </summary>
  public bool isLocked {
    // ReSharper disable once UnusedMember.Global
    get => _isLocked;
    set {
      _isLocked = value;
      if (_unitySlot != null) {
        _unitySlot.isLocked = value;
      }
    }
  }
  bool _isLocked;

  /// <summary>Tells if the item in the slot has science data.</summary>
  /// <remarks>Items that have data are not allowed to stack in the slots.</remarks>
  public bool hasScience => isVisible && _unitySlot.hasScience;

  /// <summary>Gives percentile string that describes the aggregate state of the items in the slot.</summary>
  public string resourceStatus => isVisible ? _unitySlot.resourceStatus : null;

  /// <summary>
  /// Number of the items that was claimed by the inventory for the removal. They must be skipped from the UI related
  /// updates.
  /// </summary>
  /// <remarks>The items are always reserved starting from item 0.</remarks>
  public int reservedItems {
    get => _reservedItems;
    set {
      _reservedItems = value;
      UpdateUnitySlot();
    }
  }
  int _reservedItems;

  /// <summary>Inventory items in the slot.</summary>
  /// FIXME: make it hashset, or sorted hashset
  public List<InventoryItem> slotItems { get; private set; } = new();

  /// <summary>Generalized icon of the slot.</summary>
  /// <value>The texture that represents the slot.</value>
  public Texture iconImage => slotRefItem?.iconImage;

  /// <summary>Indicates if this slot has any part item.</summary>
  public bool isEmpty => slotItems.Count == 0;

  /// <summary>Reference part info of this slot.</summary>
  /// <value>The part info or <c>null</c> if the slot is empty.</value>
  public AvailablePart avPart => slotRefItem?.avPart;

  // ReSharper enable MemberCanBePrivate.Global
  #endregion

  #region Local fields and properties
  /// <summary>Unity object that represents the slot.</summary>
  /// <remarks>It can be <c>null</c> if the slot is not shown in the inventory window.</remarks>
  UIKISInventorySlot.Slot _unitySlot;

  /// <summary>Reflection of <see cref="slotItems"/> in a form of hash set for quick lookup operations.</summary>
  /// <seealso cref="AddItem"/>
  /// <seealso cref="DeleteItem"/>
  readonly HashSet<InventoryItem> _itemsSet = new();

  /// <summary>Returns the item which this slot uses as a base for the similarity checks.</summary>
  /// <value>The item or NULL if the slot is empty.</value>
  /// <seealso cref="CheckIfSimilar"/>
  InventoryItem slotRefItem => !isEmpty ? slotItems[0] : null;
  #endregion

  /// <summary>Makes an inventory slot, bound to its Unity counterpart.</summary>
  public InventorySlotImpl(KisContainerWithSlots owner, UIKISInventorySlot.Slot unitySlot) {
    ownerInventory = owner;
    BindTo(unitySlot);
  }

  #region API methods
  /// <summary>Attaches this slot to a Unity slot object.</summary>
  /// <remarks>
  /// The slots that are not attached to any Unity object are invisible. Invisible slots are fully functional slots,
  /// except the UI related properties and methods are NO-OP. 
  /// </remarks>
  /// <param name="newUnitySlot">The slot or <c>null</c> if slot should become invisible.</param>
  /// <seealso cref="isVisible"/>
  public void BindTo(UIKISInventorySlot.Slot newUnitySlot) {
    var needUpdate = newUnitySlot != null && _unitySlot != newUnitySlot;
    _unitySlot = newUnitySlot;
    if (needUpdate) {
      UpdateUnitySlot();
    }
  }

  /// <summary>Checks if this slot is represented by the specified Unity slot object.</summary>
  /// <param name="checkUnitySlot">The Unity slot to check against.</param>
  /// <seealso cref="isVisible"/>
  public bool IsBoundTo(UIKISInventorySlot.Slot checkUnitySlot) {
    return isVisible && _unitySlot == checkUnitySlot;
  }

  /// <summary>Adds an item into the slot.</summary>
  /// <remarks>
  /// This method doesn't check preconditions. The item will be added even if it breaks the slot's logic. The caller is
  /// responsible to verify if the item can be added before attempting to add anything.
  /// </remarks>
  /// <param name="item">
  /// An item to add. The item is not copied, it's added as a reference. It must be already added into the inventory.
  /// </param>
  /// <seealso cref="CheckCanAddParts"/>
  public void AddItem(InventoryItem item) {
    _itemsSet.Add(item);
    slotItems.Add(item);
  }

  /// <summary>Deletes the item from the slot.</summary>
  /// <remarks>The item won't be removed from the inventory.</remarks>
  /// <param name="item">An item to delete.</param>
  public void DeleteItem(InventoryItem item) {
    _itemsSet.Remove(item);
    slotItems.Remove(item); // FIXME: This won't be fast on large sets.
  }

  /// <summary>Updates the slot's GUI to the current KIS slot state.</summary>
  public void UpdateUnitySlot() {
    if (_unitySlot == null) {
      return;
    }
    if (isEmpty) {
      _unitySlot.ClearContent();
      return;
    }
    _unitySlot.slotImage = iconImage;
    _unitySlot.stackSize = "x" + (slotItems.Count - reservedItems);
    _unitySlot.resourceStatus = GetSlotResourceAmountStatus();

    // Slot science data.
    // FIXME: implement
  }

  /// <summary>Verifies if the parts can be added into the slot.</summary>
  /// <remarks>The parts must be "similar enough" to be added into the same slot.</remarks>
  /// <param name="checkSnapshots">The parts to check. If it's empty, the reply is always "yes".</param>
  /// <param name="logErrors">
  /// If <c>true</c>, then all the found errors will be written to the system log. Callers may use this option when
  /// they don't normally expect any errors.
  /// </param>
  /// <returns>
  /// An empty list if the parts can be added to the slot, or a list of human readable errors otherwise.
  /// </returns>
  public List<ErrorReason> CheckCanAddParts(ProtoPartSnapshot[] checkSnapshots, bool logErrors = false) {
    if (checkSnapshots.Length == 0) {
      return new List<ErrorReason>();
    }
    if (slotItems.Count + checkSnapshots.Length > ownerInventory.maxKisSlotSize) {
      return ReturnErrorReasons(
          logErrors, SlotIsFullReason, SlotIsFullReasonText.Format(ownerInventory.maxKisSlotSize));
    }

    // The errors must be logged by priority.
    List<ErrorReason> err1 = null;
    List<ErrorReason> err2 = null;
    List<ErrorReason> err3 = null;
    List<ErrorReason> err4 = null;
    var refSnapshot = slotRefItem?.snapshot ?? checkSnapshots[0];
    foreach (var checkSnapshot in checkSnapshots) {
      if (err1 == null && checkSnapshot.partName != refSnapshot.partName) {
        err1 = ReturnErrorReasons(logErrors, DifferentPartReason, DifferentPartsReasonText);
      } else if (err2 == null && checkSnapshot.moduleVariantName != refSnapshot.moduleVariantName) {
        err2 = ReturnErrorReasons(logErrors, DifferentVariantReason, DifferentVariantReasonText);
      } else if (err3 == null && !CheckIfSameResources(refSnapshot, checkSnapshot)) {
        err3 = ReturnErrorReasons(logErrors, DifferentResourcesReason, DifferentResourcesReasonText);
      } else if (err4 == null && !CheckIfSimilar(refSnapshot, checkSnapshot)) {
        err4 = ReturnErrorReasons(logErrors, DifferentResourceAmountsReason, DifferentResourceAmountsReasonText);
      }
    }
    return err1 ?? err2 ?? err3 ?? err4 ?? new List<ErrorReason>();
  }

  /// <summary>Checks if the item can be stacked into this slot.</summary>
  public bool IsPartFit(ProtoPartSnapshot checkSnapshot) {
    return slotItems.Count <= 1 || CheckIfSimilar(slotRefItem.snapshot, checkSnapshot);
  }
  #endregion

  #region Local utility methods
  /// <summary>Gives an approximate short string for a percent value.</summary>
  /// <remarks>
  /// The boundary values, 100% and 0%, are only shown if this value is not a default for any of the part resources.
  /// </remarks>
  string GetSlotResourceAmountStatus() {
    if (slotRefItem.resources.Length == 0) {
      return null;
    }
    var similarityValues = KisContainerWithSlots.CalculateSimilarityValues(slotRefItem.snapshot); 
    var slotPercent = similarityValues.Sum(x => (double) x.Value) / 100.0
        / similarityValues.Count;
    var amountSlot = KisContainerWithSlots.GetResourceAmountSlot(slotPercent);
    string text;
    if (amountSlot == 0) {
      var defaultIsEmpty = slotRefItem.avPart.partPrefab.Resources.Any(r => r.amount < double.Epsilon); 
      text = defaultIsEmpty ? null : "0%";
    } else if (amountSlot == 5) {
      text = "<5%";
    } else if (amountSlot < 95) {
      text = $"~{amountSlot}%";
    } else if (amountSlot != 100) {
      text = ">95%";
    } else {
      var defaultIsFull = slotRefItem.avPart.partPrefab.Resources.Any(r => r.amount > double.Epsilon); 
      text = defaultIsFull ? null : "100%";
    }
    return text;
  }

  /// <summary>Checks if items resources are the same and the amounts are somewhat close.</summary>
  static bool CheckIfSimilar(ProtoPartSnapshot refSnapshot, ProtoPartSnapshot checkSnapshot) {
    var refSimilarityValues = KisContainerWithSlots.CalculateSimilarityValues(refSnapshot);
    var checkSimilarityValues = KisContainerWithSlots.CalculateSimilarityValues(checkSnapshot);
    return refSimilarityValues.SequenceEqual(checkSimilarityValues); 
  }

  /// <summary>Checks if items resources are the same, disregarding the amounts.</summary>
  static bool CheckIfSameResources(ProtoPartSnapshot refSnapshot, ProtoPartSnapshot checkSnapshot) {
    var refResources = refSnapshot.resources.Select(r => r.resourceName);
    var checkResources = checkSnapshot.resources.Select(r => r.resourceName);
    return refResources.SequenceEqual(checkResources);
  }

  /// <summary>Returns a standard error reason response.</summary>
  static List<ErrorReason> ReturnErrorReasons(bool logErrors, string reasonCode, string reasonText) {
    var reason = new ErrorReason() {
        errorClass = reasonCode,
        guiString = reasonText,
    };
    if (logErrors) {
      DebugEx.Error("Cannot add items to slot:\n{0}", reason);
    }
    return new List<ErrorReason> { reason };
  }
  #endregion
}

}  // namespace
