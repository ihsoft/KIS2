// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using KISAPIv2;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
using KSPDev.ProcessingUtils;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using UnityEngine;
using KIS2.GUIUtils;

namespace KIS2 {

/// <summary>Internal class that holds inventory slot logic.</summary>
/// <remarks>
/// Every slot can only hold items that refer the same part proto. The persisted properties on the
/// items should be "similar". If two parts of the same kind are too different with regard to their
/// internal state, they cannot stack to the same slot.
/// </remarks>
/// <seealso cref="KISContainerWithSlots"/>
sealed class InventorySlotImpl : IKISDragTarget {

  #region Localizable strings
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<MassType> MassTootltipText = new Message<MassType>(
      "",
      defaultTemplate: "Mass: <b><<1>></b>",
      description: "Mass of a single item in the slot tooltip when all items have equal mass.\n"
          + " The <<1>> argument is the mass of type MassType.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<VolumeLType> VolumeTootltipText = new Message<VolumeLType>(
      "",
      defaultTemplate: "Volume: <b><<1>></b>",
      description: "Volume of the item for the slot tooltip. All items in th slot have the same"
          + " volume.n\n"
          + " The <<1>> argument is the volume of type VolumeLType.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<CostType> CostTootltipText = new Message<CostType>(
      "",
      defaultTemplate: "Cost: <b><<1>></b>",
      description: "Cost of a single item for the slot tooltip when all items have equal cost.\n"
          + " The <<1>> argument is the cost of type CostType.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<string> VariantTootltipText = new Message<string>(
      "",
      defaultTemplate: "Variant: <b><<1>></b>",
      description: "Name of the variant of the items in the slot tooltip. All items in the slot"
          + " have the same variant.\n"
          + " The <<1>> argument is a localized name of the variant.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message3/*"/>
  static readonly Message<ResourceType, CompactNumberType, CompactNumberType> NormalResourceValueText =
      new Message<ResourceType, CompactNumberType, CompactNumberType>(
          "",
          defaultTemplate: "<<1>>: <b><<2>></b> / <b><<3>></b>",
          description: "Resource status string in the slot tooltip when the available amount is at"
              + " the expected level (e.g. 'empty' for ore tanks or 'full' for the fuel ones).\n"
              + " The <<1>> argument is a localized name of the resource.\n"
              + " The <<2>> argument is the current amount of the resource.\n"
              + " The <<3>> argument is the maximum amount of the resource.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message3/*"/>
  static readonly Message<ResourceType, CompactNumberType, CompactNumberType> SpecialResourceValueText =
      new Message<ResourceType, CompactNumberType, CompactNumberType>(
          "",
          defaultTemplate: "<<1>>: <b><color=yellow><<2>></color></b> / <b><<3>></b>",
          description: "Resource status string in the slot tooltip when the available amount is at"
              + " the level that is not normally expected (e.g. 'half-full' for ore tanks or"
              + " 'half-empty' for the fuel ones).\n"
              + " The <<1>> argument is a localized name of the resource.\n"
              + " The <<2>> argument is the current amount of the resource.\n"
              + " The <<3>> argument is the maximum amount of the resource.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  readonly static Message DifferentPartsTooltipText = new Message(
      "",
      defaultTemplate: "Different parts",
      description: "Info text that is shown in the inventory slot tooltip. It tells that the"
          + " dragged item(s) cannot be added to the stack due to it already contains a different"
          + " part. All items in the slot are required to be the same part!");
  #endregion

  #region API properties and fields
  /// <summary>Unity object that represents the slot.</summary>
  public readonly UIKISInventorySlot.Slot unitySlot;

  /// <summary>Inventory that owns this slot.</summary>
  public readonly KISContainerWithSlots inventory;

  /// <summary>Part proto of this slot.</summary>
  public AvailablePart avPart {
    get { return !isEmpty ? itemsList[0].avPart : null; }
  }

  /// <summary>Inventory items in the slot.</summary>
  /// TODO(ihsoft): May be cache it if used extensively.
  public InventoryItem[] items {
    get { return itemsList.ToArray(); }
  }
  readonly List<InventoryItem> itemsList = new List<InventoryItem>();

  /// <summary>Generalized icon of the slot.</summary>
  public Texture iconImage {
    get {
      // FIXME: dynamically pick the best icon resolution. 
      return !isEmpty ? KISAPI.PartIconUtils.MakeDefaultIcon(avPart, 256, null) : null;
    }
  }

  /// <summary>Tells if this slot has any part item.</summary>
  public bool isEmpty {
    get { return itemsList.Count == 0; }
  }

  public bool isLocked {
    get { return _isLocked; }
    set {
      _isLocked = value;
      unitySlot.isLocked = value;
      //itemsList.ForEach(i => i.isLocked = value);
    }
  }
  bool _isLocked;
  #endregion

  #region IKISDragTarget implementation
  /// <inheritdoc/>
  public void OnKISDragStart() {
    UpdateTooltip();
  }

  /// <inheritdoc/>
  public void OnKISDragEnd(bool isCancelled) {
    UpdateTooltip();
  }

  /// <inheritdoc/>
  public bool OnKISDrag(bool pointerMoved) {
    UpdateTooltip();
    if (isEmpty) {
      return true;
    }
    var res = CheckCanAdd(KISAPI.ItemDragController.leasedItems);
    //FIXME: display some kinds of errors?
    return res.Length == 0;
  }
  #endregion

  /// <summary>Makes an inventory slot, bound to its Unity counterpart.</summary>
  public InventorySlotImpl(KISContainerWithSlots inventory, UIKISInventorySlot.Slot unitySlot) {
    this.inventory = inventory;
    this.unitySlot = unitySlot;
  }

  #region API methods
  /// <summary>Adds an item to the slot.</summary>
  /// <remarks>
  /// It's an error for this method if the item cannot be added. The callers must verify if it can
  /// be added via <see cref="CheckCanAdd"/>.
  /// </remarks>
  /// <param name="items">The items to add.</param>
  /// <returns><c>true</c> if the items were added to the slot.</returns>
  /// <seealso cref="UpdateTooltip"/>
  public bool AddItems(InventoryItem[] items) {
    if (CheckCanAdd(items, logErrors: true).Length > 0) {
      return false;
    }
    itemsList.AddRange(items);
    UpdateUnitySlot();
    if (inventory.unityWindow.hoveredSlot == unitySlot) {
      UpdateTooltip();
    }
    return true;
  }

  public bool DeleteItem(InventoryItem item) {
    if (!itemsList.Remove(item)) {
      //FIXME: deal with slot items so we always know the slot 
      DebugEx.Error("Cannot remove item from slot");
      return false;
    }
    UpdateUnitySlot();
    if (inventory.unityWindow.hoveredSlot == unitySlot) {
      UpdateTooltip();
    }
    return true;
  }

  /// <summary>Verifies if the item can be added to the slot.</summary>
  /// <remarks>
  /// The item must be "similar" to the other items in teh slot. At the very least, it must be the
  /// same part. The part's state similarity is implementation dependent and the callers must not be
  /// guessing about it.
  /// </remarks>
  /// <param name="items">The items to check. It must not be empty.</param>
  /// <param name="logErrors">
  /// If <c>true</c>, then all the found errors will be written to the system log. Callers may use
  /// this option when they don't normally expect any errors.
  /// </param>
  /// <returns>
  /// An empty array if the item can be added to the slot, or a list of human readable errors.
  /// </returns>
  public ErrorReason[] CheckCanAdd(InventoryItem[] items, bool logErrors = false) {
    Preconditions.MinElements(items, 1);
    var res = new HashSet<ErrorReason>();
    var slotPartName = isEmpty ? items[0].avPart.name : avPart.name;
    foreach (var item in items) {
      if (item.avPart.name != slotPartName) {
        if (logErrors) {
          DebugEx.Error(
              "Mixed parts in the batch: expected={0}, got={1}", slotPartName, item.avPart.name);
        }
        return new[] {
            new ErrorReason() {
                shortString = "DifferentPart",
                guiString = DifferentPartsTooltipText,
            },
        };
      }
      //FIXME: implement similarity check.
    }
    if (logErrors && res.Count > 0) {
      DebugEx.Error("Cannot add part to slot:\n{0}", DbgFormatter.C2S(res, separator: "\n"));
    }
    return res.ToArray();
  }

  /// <summary>Fills tooltip with the slot info.</summary>
  /// <remarks>
  /// If the slot is empty, then no update is made. The tooltip visibility is never affected.
  /// </remarks>
  public void UpdateTooltip() {
    var tooltip = inventory.unityWindow.currentTooltip;
    if (isEmpty) {
      return;  // Nothing to do.
    }
    if (itemsList.Count == 1) {
      UpdateSingleItemTooltip(tooltip, itemsList[0]);
    } else {
      //FIXME: handle multuple item slots
      tooltip.baseInfo.text = "*** multiple items";
    }
    tooltip.UpdateLayout();
  }
  #endregion

  #region Local utility methods
  /// <summary>Fills tooltip with useful information about the items in the slot.</summary>
  void UpdateSingleItemTooltip(UIKISInventoryTooltip.Tooltip tooltip, InventoryItem item) {
    tooltip.title = avPart.title;
    var infoLines = new List<string>();

    // Basic stats.
    infoLines.Add(MassTootltipText.Format(item.fullMass));
    infoLines.Add(VolumeTootltipText.Format(item.volume));
    infoLines.Add(CostTootltipText.Format(item.fullCost));
    var variant = VariantsUtils.GetCurrentPartVariant(item.avPart, item.itemConfig);
    if (variant != null) {
      infoLines.Add(VariantTootltipText.Format(variant.DisplayName));
    }
    tooltip.baseInfo.text = string.Join("\n", infoLines.ToArray());

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
      tooltip.availableResourcesInfo.text = string.Join("\n", resItems.ToArray());
    } else {
      tooltip.availableResourcesInfo.text = null;
    }
  }

  /// <summary>Gives an approximate short string for a percent value.</summary>
  string GetResourceAmountStatus(double percent) {
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
    if (isEmpty) {
      unitySlot.ClearContent();
      return;
    }
    unitySlot.slotImage = iconImage;
    unitySlot.stackSize = "x" + itemsList.Count();
    //FIXME: show hotkey if enabled.

    // Slot resources info.
    if (itemsList[0].resources.Length > 0) {
      var cumAvgPct = 0.0;
      foreach (var item in itemsList) {
        if (item.resources.Length > 0) {
          cumAvgPct += item.resources.Sum(r => r.amount / r.maxAmount) / item.resources.Length;
        }
      }
      unitySlot.resourceStatus = GetResourceAmountStatus(cumAvgPct / itemsList.Count);
    } else {
      unitySlot.resourceStatus = null;
    }
    // Slot science data.
    // FIXME: implement
  }
  #endregion
}
  
}  // namespace

