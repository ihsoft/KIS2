// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KISAPIv2;
using KIS2.GUIUtils;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
using KSPDev.ProcessingUtils;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using Smooth.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>Internal class that holds inventory slot logic.</summary>
/// <remarks>
/// Every slot can only hold items that refer the same part proto. The persisted properties on the
/// items should be "similar". If two parts of the same kind are too different with regard to their
/// internal state, they cannot stack to the same slot.
/// </remarks>
/// <seealso cref="KISContainerWithSlots"/>
internal sealed class InventorySlotImpl {

  #region Localizable strings
  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<MassType> MassTooltipText = new Message<MassType>(
      "",
      defaultTemplate: "Mass: <b><<1>></b>",
      description: "Mass of a single item in the slot tooltip when all items have equal mass.\n"
          + " The <<1>> argument is the mass of type MassType.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<VolumeLType> VolumeTooltipText = new Message<VolumeLType>(
      "",
      defaultTemplate: "Volume: <b><<1>></b>",
      description: "Volume of the item for the slot tooltip. All items in th slot have the same"
          + " volume.n\n"
          + " The <<1>> argument is the volume of type VolumeLType.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<CostType> CostTooltipText = new Message<CostType>(
      "",
      defaultTemplate: "Cost: <b><<1>></b>",
      description: "Cost of a single item for the slot tooltip when all items have equal cost.\n"
          + " The <<1>> argument is the cost of type CostType.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<string> VariantTooltipText = new Message<string>(
      "",
      defaultTemplate: "Variant: <b><<1>></b>",
      description: "Name of the variant of the items in the slot tooltip. All items in the slot"
          + " have the same variant.\n"
          + " The <<1>> argument is a localized name of the variant.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message3/*"/>
  static readonly Message<ResourceType, CompactNumberType, CompactNumberType> NormalResourceValueText =
      new Message<ResourceType, CompactNumberType, CompactNumberType>(
          "",
          defaultTemplate: "<<1>>: <b><<2>></b> / <b><<3>></b>",
          description: "Resource status string in the slot tooltip when the available amount is at"
              + " the expected level (e.g. 'empty' for ore tanks or 'full' for the fuel ones).\n"
              + " The <<1>> argument is a localized name of the resource.\n"
              + " The <<2>> argument is the current amount of the resource.\n"
              + " The <<3>> argument is the maximum amount of the resource.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message3/*"/>
  static readonly Message<ResourceType, CompactNumberType, CompactNumberType> SpecialResourceValueText =
      new Message<ResourceType, CompactNumberType, CompactNumberType>(
          "",
          defaultTemplate: "<<1>>: <b><color=yellow><<2>></color></b> / <b><<3>></b>",
          description: "Resource status string in the slot tooltip when the available amount is at"
              + " the level that is not normally expected (e.g. 'full' for ore tanks or 'empty' for"
              + " the fuel ones).\n"
              + " The <<1>> argument is a localized name of the resource.\n"
              + " The <<2>> argument is the current amount of the resource.\n"
              + " The <<3>> argument is the maximum amount of the resource.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message DifferentPartsReasonText = new Message(
      "",
      defaultTemplate: "Different parts",
      description: "Error message that is presented when parts cannot be added to the inventory"
          + " slot due to some of them don't match to each other or the other items in the slot.");

  #endregion

  #region API properties and fields
  /// <summary>
  /// Short name of the checking error for the case when parts with different signature are being
  /// added to the slot.
  /// </summary>
  /// <seealso cref="DifferentPartsReasonText"/>
  // ReSharper disable once MemberCanBePrivate.Global
  public const string DifferentPartReason = "DifferentPart";

  /// <summary>Tells if this slot is visible in the inventory dialog.</summary>
  /// <remarks>
  /// Slot can be invisible in two cases: the dialog is closed or there are not enough UI elements
  /// to represent all the slots in the dialog. In the latter case, the tail slots become invisible.
  /// </remarks>
  public bool isVisible => _unitySlot != null;

  /// <summary>
  /// Tells if the slot is participating in a multi-frame operation and must not get removed from
  /// the dialog.
  /// </summary>
  /// <remarks>
  /// The content of the locked slot can change as long as the affected items are not locked.
  /// </remarks>
  public bool isLocked {
    // ReSharper disable once UnusedMember.Global
    get {  return _unitySlot.isLocked; }
    set { _unitySlot.isLocked = value; }
  }

  /// <summary>Tells if the item in the slot has science data.</summary>
  /// <remarks>Items that have data are not allowed to stack in the slots.</remarks>
  public bool hasScience => isVisible && _unitySlot.hasScience;

  /// <summary>
  /// Gives percentile string that describes the aggregate state of the items in the slot.
  /// </summary>
  public string resourceStatus => isVisible ? _unitySlot.resourceStatus : null;

  /// <summary>
  /// Number of the items that was claimed by the inventory fro the removal. They must be skipped
  /// from the UI related updates.
  /// </summary>
  /// <remarks>The items are always reserved starting from item 0.</remarks>
  public int reservedItems {
    get { return _reservedItems; }
    set {
      _reservedItems = value;
      UpdateUnitySlot();
    }
  }
  int _reservedItems;

  /// <summary>Inventory items in the slot.</summary>
  public InventoryItem[] slotItems { get; private set; } = new InventoryItem[0];

  /// <summary>Generalized icon of the slot.</summary>
  public Texture iconImage =>
      !isEmpty ? KISAPI.PartIconUtils.MakeDefaultIcon(avPart, 256, null) : null;

  /// <summary>Tells if this slot has any part item.</summary>
  public bool isEmpty => slotItems.Length == 0;
  #endregion

  #region Local fields and properties
  /// <summary>Part proto of this slot.</summary>
  AvailablePart avPart => !isEmpty ? slotItems[0].avPart : null;

  /// <summary>Unity object that represents the slot.</summary>
  /// <remarks>It can be <c>null</c> if the slot is not shown in the inventory window.</remarks>
  UIKISInventorySlot.Slot _unitySlot;

  /// <summary>
  /// Reflection of <see cref="slotItems"/> in a form of hash set for quick lookup operations. 
  /// </summary>
  /// <seealso cref="UpdateItems"/>
  readonly HashSet<InventoryItem> _itemsSet = new HashSet<InventoryItem>();
  #endregion

  /// <summary>Makes an inventory slot, bound to its Unity counterpart.</summary>
  public InventorySlotImpl(UIKISInventorySlot.Slot unitySlot) {
    BindTo(unitySlot);
  }

  #region API methods
  /// <summary>Attaches this slot to a Unity slot object.</summary>
  /// <remarks>
  /// The slots that are not attached to any Unity object are invisible. Invisible slots are fully
  /// functional slots, except the UI related properties and methods are NO-OP. 
  /// </remarks>
  /// <param name="newUnitySlot">The slot or <c>null</c> if slot should become invisible.</param>
  /// <seealso cref="isVisible"/>
  public void BindTo(UIKISInventorySlot.Slot newUnitySlot) {
    var needUpdate = _unitySlot != newUnitySlot;
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

  /// <summary>Adds an item to the slot.</summary>
  /// <remarks>
  /// This method doesn't check preconditions. The items will be added even if it break the slot's
  /// logic. The caller is responsible to verify if the items can be added via the
  /// <see cref="CheckCanAddItems"/> before attempting to add anything.
  /// </remarks>
  /// <param name="items">
  /// The items to add. They are not copied, they are added as the references. These items must be
  /// already added to the inventory.
  /// </param>
  /// <seealso cref="UpdateTooltip"/>
  /// <seealso cref="CheckCanAddItems"/>
  public void AddItems(InventoryItem[] items) {
    UpdateItems(addItems: items);
  }

  /// <summary>Deletes items from the slot.</summary>
  /// <remarks>The items won't be removed from the inventory.</remarks>
  /// <param name="items">The items to delete.</param>
  public void DeleteItems(InventoryItem[] items) {
    UpdateItems(deleteItems: items);
  }

  /// <summary>Verifies if the items can be added to the slot.</summary>
  /// <remarks>
  /// The items must be "similar" to the other items in the slot. At the very least, it must be the
  /// same part. The part's state similarity is implementation dependent and the callers must not be
  /// guessing about it.
  /// </remarks>
  /// <param name="checkItems">The items to check. It must not be empty.</param>
  /// <param name="logErrors">
  /// If <c>true</c>, then all the found errors will be written to the system log. Callers may use
  /// this option when they don't normally expect any errors.
  /// </param>
  /// <returns>
  /// <c>null</c> if the item can be added to the slot, or a list of human readable errors.
  /// </returns>
  public ErrorReason[] CheckCanAddItems(InventoryItem[] checkItems, bool logErrors = false) {
    Preconditions.MinElements(checkItems, 1);
    var errors = new HashSet<ErrorReason>();
    var slotPartName = isEmpty ? checkItems[0].avPart.name : avPart.name;
    if (checkItems.Any(checkItem => checkItem.avPart.name != slotPartName)) {
      errors.Add(new ErrorReason() {
          shortString = DifferentPartReason,
          guiString = DifferentPartsReasonText,
      });
    }
    //FIXME: check for similarity
    if (logErrors && errors.Count > 0) {
      DebugEx.Error("Cannot add items to slot:\n{0}", DbgFormatter.C2S(errors, separator: "\n"));
    }
    return errors.Count > 0 ? errors.ToArray() : null;
  }

  /// <summary>Fills tooltip with the slot info.</summary>
  /// <remarks>If the slot is empty, then all info fields are erased.</remarks>
  public void UpdateTooltip(UIKISInventoryTooltip.Tooltip tooltip) {
    tooltip.ClearInfoFileds();
    if (isEmpty) {
      return;
    }
    //FIXME: consider reservedItems
    if (slotItems.Length == 1) {
      UpdateSingleItemTooltip(tooltip);
    } else {
      //FIXME: handle multiple item slots
      tooltip.baseInfo.text = "*** multiple items";
    }
    tooltip.UpdateLayout();
  }
  #endregion

  #region Local utility methods
  /// <summary>Fills tooltip with useful information about the items in the slot.</summary>
  void UpdateSingleItemTooltip(UIKISInventoryTooltip.Tooltip tooltip) {
    var item = slotItems[0];
    tooltip.title = avPart.title;
    var infoLines = new List<string> {
        MassTooltipText.Format(item.fullMass),
        VolumeTooltipText.Format(item.volume),
        CostTooltipText.Format(item.fullCost)
    };

    // Basic stats.
    var variant = VariantsUtils.GetCurrentPartVariant(item.avPart, item.itemConfig);
    if (variant != null) {
      infoLines.Add(VariantTooltipText.Format(variant.DisplayName));
    }
    tooltip.baseInfo.text = string.Join("\n", infoLines);

    // Available resources stats.
    // Populate the available resources on the part. 
    if (item.resources.Length > 0) {
      var resItems = new List<string>();
      foreach (var resource in item.resources) {
        if (Math.Abs(resource.amount - resource.maxAmount) <= double.Epsilon) {
          resItems.Add(NormalResourceValueText.Format(
              resource.resourceName,
              resource.amount,
              resource.maxAmount));
        } else {
          resItems.Add(SpecialResourceValueText.Format(
              resource.resourceName,
              resource.amount,
              resource.maxAmount));
        }
      }
      tooltip.availableResourcesInfo.text = string.Join("\n", resItems);
    } else {
      tooltip.availableResourcesInfo.text = null;
    }
  }

  /// <summary>Gives an approximate short string for a percent value.</summary>
  static string GetResourceAmountStatus(double percent) {
    string text;
    if (percent < double.Epsilon) {
      text = "0%";
    } else if (percent < 0.05) {
      text = "<5%";
    } else if (percent < 0.15) {
      text = "~10%";
    } else if (percent < 0.25) {
      text = "~20%";
    } else if (percent < 0.35) {
      text = "~30%";
    } else if (percent < 0.45) {
      text = "~40%";
    } else if (percent < 0.55) {
      text = "~50%";
    } else if (percent < 0.65) {
      text = "~60%";
    } else if (percent < 0.75) {
      text = "~70%";
    } else if (percent < 0.85) {
      text = "~80%";
    } else if (percent < 0.95) {
      text = "~90%";
    } else if (percent - 1 > double.Epsilon) {
      text = ">95%";
    } else {
      text = null;  // 100%
    }
    return text;
  }

  /// <summary>Updates the slot's GUI to the current KIS slot state.</summary>
  void UpdateUnitySlot() {
    if (_unitySlot == null) {
      return;
    }
    if (isEmpty || _unitySlot == null) {
      _unitySlot.ClearContent();
      return;
    }
    _unitySlot.slotImage = iconImage;
    _unitySlot.stackSize = "x" + (slotItems.Length - reservedItems);

    // Slot resources info.
    if (slotItems[0].resources.Length > 0) {
      var cumAvgPct = slotItems.Where(item => item.resources.Length > 0)
          .Sum(item => item.resources.Sum(r => r.amount / r.maxAmount) / item.resources.Length);
      _unitySlot.resourceStatus = GetResourceAmountStatus(cumAvgPct / slotItems.Length);
    } else {
      _unitySlot.resourceStatus = null;
    }
    // Slot science data.
    // FIXME: implement
  }

  /// <summary>Adds or deletes items to/from the slot.</summary>
  void UpdateItems(InventoryItem[] addItems = null, InventoryItem[] deleteItems = null) {
    if (addItems != null) {
      _itemsSet.AddAll(addItems);
    }
    if (deleteItems != null) {
      Array.ForEach(deleteItems, x => _itemsSet.Remove(x));
    }
    // Reconstruct the items array so that the existing items keep their original order, and the new
    // items (if any) are added at the tail.
    var newItems = slotItems.Where(x => _itemsSet.Contains(x));
    if (addItems != null) {
      newItems = newItems.Concat(addItems);
    }
    slotItems = newItems.ToArray();
    UpdateUnitySlot();
  }
  #endregion
}
  
}  // namespace
