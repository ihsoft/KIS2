// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KISAPIv2;
using KIS2.GUIUtils;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
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
      description: "A slot tooltip string that shows the mass of an item. It's used when the slot"
          + " has exactly one item.\n"
          + " The <<1>> argument is the item mass of type MassType.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message2/*"/>
  static readonly Message<MassType, MassType> MassMultipartTooltipText =
      new Message<MassType, MassType>(
          "",
          defaultTemplate: "Mass: <b><<1>></b> (total: <b><<2>></b>)",
          description: "A slot tooltip string that shows both the mass of an item and the total"
              + " slot mass. It's used when the slot has more than one item.\n"
              + " The <<1>> argument is the item mass.\n"
              + " The <<2>> argument is the slot total mass.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<VolumeLType> VolumeTooltipText = new Message<VolumeLType>(
      "",
      defaultTemplate: "Volume: <b><<1>></b>",
      description: "A slot tooltip string that shows the volume of an item. It's used when the slot"
          + " has exactly one item.\n"
          + " The <<1>> argument is the item volume.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message2/*"/>
  static readonly Message<VolumeLType, VolumeLType> VolumeMultipartTooltipText =
      new Message<VolumeLType, VolumeLType>(
          "",
          defaultTemplate: "Volume: <b><<1>></b> (total: <b><<2>></b>)",
          description: "A slot tooltip string that shows both the volume of an item and the total"
              + " slot volume. It's used when the slot has more than one item.\n"
              + " The <<1>> argument is the item volume.\n"
              + " The <<2>> argument is the slot total volume.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<CostType> CostTooltipText = new Message<CostType>(
      "",
      defaultTemplate: "Cost: <b><<1>></b>",
      description: "A slot tooltip string that shows the cost of an item. It's used when the slot"
          + " has exactly one item.\n"
          + " The <<1>> argument is the item cost.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message2/*"/>
  static readonly Message<CostType, CostType> CostMultipartTooltipText =
      new Message<CostType, CostType>(
          "",
          defaultTemplate: "Cost: <b><<1>></b> (total: <b><<2>></b>)",
          description: "A slot tooltip string that shows both the cost of an item and the total"
              + " slot cost. It's used when the slot has more than one item.\n"
          + " The <<1>> argument is the item cost.\n"
          + " The <<2>> argument is the slot total cost.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<string> VariantTooltipText = new Message<string>(
      "",
      defaultTemplate: "Variant: <b><<1>></b>",
      description: "Name of the variant of the items in the slot tooltip. All items in the slot"
          + " have the same variant.\n"
          + " The <<1>> argument is a localized name of the variant.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message3/*"/>
  static readonly Message<ResourceType, CompactNumberType, CompactNumberType>
      NormalResourceValueText = new Message<ResourceType, CompactNumberType, CompactNumberType>(
          "",
          defaultTemplate: "<<1>>: <b><<2>></b> / <b><<3>></b>",
          description: "Resource status string in the slot tooltip when the available amount is at"
              + " the expected level (e.g. 'empty' for the ore tanks or 'full' for the fuel"
              + " ones).\n"
              + " The <<1>> argument is a localized name of the resource.\n"
              + " The <<2>> argument is the current amount of the resource.\n"
              + " The <<3>> argument is the maximum amount of the resource.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message3/*"/>
  static readonly Message<ResourceType, CompactNumberType, CompactNumberType>
      SpecialResourceValueText = new Message<ResourceType, CompactNumberType, CompactNumberType>(
          "",
          defaultTemplate: "<<1>>: <b><color=yellow><<2>></color></b> / <b><<3>></b>",
          description: "Resource status string in the slot tooltip when the available amount is at"
              + " the level that is not normally expected (e.g. 'full' for the ore tanks or"
              + " 'empty' for"
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

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message DifferentResourcesReasonText = new Message(
      "",
      defaultTemplate: "Different resources",
      description: "Error message that is presented when parts cannot be added to the inventory"
      + " slot due to their resources or their amounts are too different between each other or the"
      + " slot's items.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message DifferentResourceAmountsReasonText = new Message(
      "",
      defaultTemplate: "Different resource amounts",
      description: "Error message that is presented when parts cannot be added to the inventory"
      + " slot due to their resource amounts are too different between each other or the slot's"
      + " items.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message4/*"/>
  static readonly Message<ResourceType, CompactNumberType, CompactNumberType, CompactNumberType>
      ResourceMultipartSpecialValueText = new Message<ResourceType, CompactNumberType, CompactNumberType, CompactNumberType>(
          "",
          defaultTemplate:
          "<<1>>: <b><color=yellow>~<<2>></color></b> / <b><<3>></b> (total: <b><<4>></b>)",
          description: "Resource status string in the slot tooltip when there are more than one"
          + " items available in the slot and the resource reserve is varying in the items.\n"
          + " The <<1>> argument is a localized name of the resource.\n"
          + " The <<2>> argument is the estimated amount of the resource per item.\n"
          + " The <<3>> argument is the maximum amount of the resource per item.\n"
          + " The <<4>> argument is the slot total reserve.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message4/*"/>
  static readonly Message<ResourceType, CompactNumberType, CompactNumberType, CompactNumberType>
      ResourceMultipartValueText = new Message<ResourceType, CompactNumberType, CompactNumberType, CompactNumberType>(
          "",
          defaultTemplate: "<<1>>: <b><<2>></b> / <b><<3>></b> (total: <b><<4>></b>)",
          description: "Resource status string in the slot tooltip when there are more than one"
          + " items the available in the slot and the resource reserve in all the items is at its"
          + " min or max value (i.e. it's exact).\n"
          + " The <<1>> argument is a localized name of the resource.\n"
          + " The <<2>> argument is the available amount of the resource per item.\n"
          + " The <<3>> argument is the maximum amount of the resource per item.\n"
          + " The <<4>> argument is the slot total reserve.");
  #endregion

  #region API properties and fields
  /// <summary>
  /// Short name of the checking error for the case when parts with different signature are being
  /// added to the slot.
  /// </summary>
  /// <seealso cref="DifferentPartsReasonText"/>
  // ReSharper disable once MemberCanBePrivate.Global
  public const string DifferentPartReason = "DifferentPart";

  /// <summary>
  /// Short name of the checking error for the case when parts have different resource sets.
  /// </summary>
  /// <seealso cref="DifferentResourcesReasonText"/>
  /// <seealso cref="CheckIfSimilar"/>
  // ReSharper disable once MemberCanBePrivate.Global
  public const string DifferentResourcesReason = "DifferentResources";

  /// <summary>
  /// Short name of the checking error for the case when parts have the same resources, but the
  /// amounts are too to be stored in the same slot.
  /// </summary>
  /// <seealso cref="DifferentResourcesReasonText"/>
  /// <seealso cref="CheckIfSimilar"/>
  // ReSharper disable once MemberCanBePrivate.Global
  public const string DifferentResourceAmountsReason = "DifferentResourcesAmounts";

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

  /// <summary>Rounded similarity values per resource name.</summary>
  /// <seealso cref="CheckIfSimilar"/>
  Dictionary<string, int> _resourceSimilarityValues;  
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
  /// This method doesn't check preconditions. The items will be added even if it breaks the slot's
  /// logic. The caller is responsible to verify if the item(s) can be added via the
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
  /// <param name="checkItems">The items to check. If it's empty, the reply is always "yes".</param>
  /// <param name="logErrors">
  /// If <c>true</c>, then all the found errors will be written to the system log. Callers may use
  /// this option when they don't normally expect any errors.
  /// </param>
  /// <returns>
  /// <c>null</c> if the item can be added to the slot, or a list of human readable errors.
  /// </returns>
  public ErrorReason[] CheckCanAddItems(InventoryItem[] checkItems, bool logErrors = false) {
    if (checkItems.Length == 0) {
      return null;
    }
    var errors = new HashSet<ErrorReason>();
    var slotPartName = isEmpty ? checkItems[0].avPart.name : avPart.name;
    //FIXME: check variants - must be equal.
    if (checkItems.Any(checkItem => checkItem.avPart.name != slotPartName)) {
      errors.Add(new ErrorReason() {
          shortString = DifferentPartReason,
          guiString = DifferentPartsReasonText,
      });
    } else {
      var checkSimilarityValues =
          _resourceSimilarityValues ?? CalculateSimilarityValues(checkItems[0]);
      if (checkItems.Any(x => !CheckIfSameResources(x, checkSimilarityValues))) {
        errors.Add(new ErrorReason() {
            shortString = DifferentResourcesReason,
            guiString = DifferentResourcesReasonText,
        });
      } else if (checkItems.Any(x => !CheckIfSimilar(x, checkSimilarityValues))) {
        errors.Add(new ErrorReason() {
            shortString = DifferentResourceAmountsReason,
            guiString = DifferentResourceAmountsReasonText,
        });
      }
    }
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
    //FIXME: consider reservedItems or better the take one modifier
    if (slotItems.Length == 1) {
      UpdateSingleItemTooltip(tooltip);
    } else {
      UpdateMultipleItemsTooltip(tooltip);
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

    //FIXME: show science
  }

  /// <summary>Fills tooltip with useful information about the items in the slot.</summary>
  void UpdateMultipleItemsTooltip(UIKISInventoryTooltip.Tooltip tooltip) {
    var refItem = slotItems[0];
    tooltip.title = avPart.title;

    // Basic stats.
    var infoLines = new List<string> {
        MassMultipartTooltipText.Format(refItem.fullMass, slotItems.Sum(x => x.fullMass)),
        VolumeMultipartTooltipText.Format(refItem.volume, slotItems.Sum(x => x.volume)),
        CostMultipartTooltipText.Format(refItem.fullCost, slotItems.Sum(x => x.fullCost))
    };
    var variant = VariantsUtils.GetCurrentPartVariant(refItem.avPart, refItem.itemConfig);
    if (variant != null) {
      infoLines.Add(VariantTooltipText.Format(variant.DisplayName));
    }
    tooltip.baseInfo.text = string.Join("\n", infoLines);

    // Available resources stats.
    var resourceInfoLines = new List<string>();
    foreach (var resource in refItem.resources) {
      var amountSlot = _resourceSimilarityValues[resource.resourceName];
      var totalAmount = slotItems.Sum(
          x => x.resources.First(r => r.resourceName == resource.resourceName).amount);
      if (amountSlot == 0 || amountSlot == 100) { // Show exact values with no highlighting.
        resourceInfoLines.Add(
            ResourceMultipartValueText.Format(
                resource.resourceName, resource.amount, resource.maxAmount, totalAmount));
      } else {
        var amountPerItem = resource.maxAmount * amountSlot / 100.0;
        resourceInfoLines.Add(
            ResourceMultipartSpecialValueText.Format(
                resource.resourceName, amountPerItem, resource.maxAmount, totalAmount));
      }
    }
    tooltip.availableResourcesInfo.text = string.Join("\n", resourceInfoLines);

    // Multi part slots don't support science, so skip it.
  }

  /// <summary>Gives an approximate short string for a percent value.</summary>
  /// <remarks>
  /// The boundary values, 100% and 0%, are only shown if this value is not a default for any of the
  /// part resources.
  /// </remarks>
  string GetSlotResourceAmountStatus() {
    if (_resourceSimilarityValues == null) {
      return null;
    }
    var slotPercent = _resourceSimilarityValues.Sum(x => (double) x.Value) / 100.0
        / _resourceSimilarityValues.Count;
    var amountSlot = GetResourceAmountSlot(slotPercent);
    string text;
    if (amountSlot == 0) {
      var defaultIsEmpty = KISAPI.PartNodeUtils.GetResources(avPart.partConfig)
          .Any(r => r.amount < double.Epsilon); 
      text = defaultIsEmpty ? null : "0%";
    } else if (amountSlot == 5) {
      text = "<5%";
    } else if (amountSlot < 95) {
      text = $"~{amountSlot}%";
    } else if (amountSlot != 100) {
      text = ">95%";
    } else {
      var defaultIsFull = KISAPI.PartNodeUtils.GetResources(avPart.partConfig)
          .Any(r => r.amount > double.Epsilon); 
      text = defaultIsFull ? null : "100%";
    }
    return text;
  }

  /// <summary>Updates the slot's GUI to the current KIS slot state.</summary>
  void UpdateUnitySlot() {
    if (_unitySlot == null) {
      return;
    }
    if (isEmpty) {
      _unitySlot.ClearContent();
      return;
    }
    _unitySlot.slotImage = iconImage;
    _unitySlot.stackSize = "x" + (slotItems.Length - reservedItems);
    _unitySlot.resourceStatus = GetSlotResourceAmountStatus();

    // Slot science data.
    // FIXME: implement
  }

  /// <summary>Adds or deletes items to/from the slot.</summary>
  void UpdateItems(InventoryItem[] addItems = null, InventoryItem[] deleteItems = null) {
    if (addItems != null && addItems.Length > 0) {
      if (_itemsSet.Count == 0) {
        _resourceSimilarityValues = CalculateSimilarityValues(addItems[0]);
      }
      _itemsSet.AddAll(addItems);
    }
    if (deleteItems != null && deleteItems.Length > 0) {
      Array.ForEach(deleteItems, x => _itemsSet.Remove(x));
      if (_itemsSet.Count == 0) {
        _resourceSimilarityValues = null;
      }
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

  /// <summary>Allocates the percentile into one of the 13 fixed slots.</summary>
  /// <remarks>
  /// The slots were chosen so that the grouping would make sense for the usual game cases.  
  /// </remarks>
  static int GetResourceAmountSlot(double percent) {
    int res;
    if (percent < double.Epsilon) {
      res = 0; // 0%
    } else if (percent < 0.05) {
      res = 5; // (0%, 5%) 
    } else if (percent < 0.95) {
      // [-5%; +5%) with step 10 starting from 10%. 
      res = (int) (Math.Floor((percent + 0.05) * 10) * 10);
    } else if (percent - 1 > double.Epsilon) {
      res = 95; // [95%, 100%)
    } else {
      res = 100; // 100%
    }
    return res;
  }

  /// <summary>Calculate amount slots from the item's resources reserve.</summary>
  Dictionary<string, int> CalculateSimilarityValues(InventoryItem item) {
    return item.resources.ToDictionary(r => r.resourceName,
                                       r => GetResourceAmountSlot(r.amount / r.maxAmount));
  }

  /// <summary>Checks if items resources are the same and the amounts are somewhat close.</summary>
  bool CheckIfSimilar(InventoryItem checkItem, Dictionary<string, int> similarityValues) {
    var checkSimilarityValues = CalculateSimilarityValues(checkItem);
    return similarityValues.Count == checkSimilarityValues.Count
        && !similarityValues.Except(checkSimilarityValues).Any();
  }

  /// <summary>Checks if items resources are the same, disregarding the amounts.</summary>
  bool CheckIfSameResources(InventoryItem checkItem, Dictionary<string, int> similarityValues) {
    var checkSimilarityValues = CalculateSimilarityValues(checkItem);
    return similarityValues.Count == checkSimilarityValues.Count
        && !similarityValues.Keys.Except(checkSimilarityValues.Keys).Any();
  }
  #endregion
}
  
}  // namespace
