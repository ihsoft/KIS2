// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KISAPIv2;
using KIS2.GUIUtils;
using KSPDev.InputUtils;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
using KSPDev.ProcessingUtils;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>Internal class that holds inventory slot logic.</summary>
/// <remarks>
/// Every slot can only hold items that refer the same part proto. The persisted properties on the
/// items should be "similar". If two parts of the same kind are too different with regard to their
/// internal state, they cannot stack to the same slot.
/// </remarks>
/// <seealso cref="KISContainerWithSlots"/>
internal sealed class InventorySlotImpl : IKISDragTarget {

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
              + " the level that is not normally expected (e.g. 'half-full' for ore tanks or"
              + " 'half-empty' for the fuel ones).\n"
              + " The <<1>> argument is a localized name of the resource.\n"
              + " The <<2>> argument is the current amount of the resource.\n"
              + " The <<3>> argument is the maximum amount of the resource.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> TakeSlotHintText = new Message<KeyboardEventType>(
      "",
      defaultTemplate: "<b><color=#5a5><<1>></color></b> to grab the stack",
      description: "Hint text that is shown in the inventory slot tooltip. It tells what action"
          + " the user should do to start dragging the whole slot form the inventory.\n"
          + " The <<1>> argument is a user friendly action name.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> TakeOneItemHintText = new Message<KeyboardEventType>(
      "",
      defaultTemplate: "<b><color=#5a5><<1>></color></b> to grab <color=#5a5>1</color> item",
      description: "Hint text that is shown in the inventory slot tooltip. It tells what action"
          + " the user should do to start dragging exactly ONE item from the inventory slot.\n"
          + " The <<1>> argument is a user friendly action name.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> TakeTenItemsHintText = new Message<KeyboardEventType>(
      "",
      defaultTemplate: "<b><color=#5a5><<1>></color></b> to grab <color=#5a5>10</color> items",
      description: "Hint text that is shown in the inventory slot tooltip. It tells what action"
          + " the user should do to start dragging 10 items from the inventory.\n"
          + " The <<1>> argument is a user friendly action name.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message DifferentPartsReasonText = new Message(
      "",
      defaultTemplate: "Different parts",
      description: "Error message that is presented when parts cannot be added to the inventory"
          + " slot due to some of them don't match to each other or the other items in the slot.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message StoreItemsTooltipText = new Message(
      "",
      defaultTemplate: "Store items",
      description: "The text to show in the title of the slot tooltip when the dragged items can be"
          + " stored into an empty slot.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> StoreItemsHintText = new Message<KeyboardEventType>(
      "",
      defaultTemplate: "<b><color=#5a5><<1>></color></b> to store items into the slot",
      description: "Hint text that is shown in the inventory slot tooltip. It tells what action"
          + " the user should do to STORE the dragged items into the hovered slot.\n"
          + " The <<1>> argument is a user friendly action name.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message AddItemsTooltipText = new Message(
      "",
      defaultTemplate: "Add items to stack",
      description: "The text to show in the title of the slot tooltip when the dragged items can be"
          + " added into an non-empty slot.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message<KeyboardEventType> AddItemsHintText = new Message<KeyboardEventType>(
      "",
      defaultTemplate: "<b><color=#5a5><<1>></color></b> to add items to the stack",
      description: "Hint text that is shown in the inventory slot tooltip. It tells what action"
          + " the user should do to ADD the dragged items into the hovered slot.\n"
          + " The <<1>> argument is a user friendly action name.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message CannotAddItemsTooltipText = new Message(
      "",
      defaultTemplate: "Cannot add items to stack",
      description: "The text to show in the title of the slot tooltip when the dragged items can"
          + " NOT be added into the slot.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message<int> AddItemsCountHintText = new Message<int>(
      "",
      defaultTemplate: "Add <color=#5a5><<1>></color> items",
      description: "Hint text that is shown in the inventory slot tooltip. It tells how many items"
          + " will be added into the stack in case of the action has completed.\n"
          + " The <<1>> argument is the number of items being added.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> TakeOneModifierHintText =
      new Message<KeyboardEventType>(
          "",
          defaultTemplate: "Press and hold <b><color=#5a5><<1>></color></b>"
              + " to take <color=#5a5>1</color> item",
          description: "Hint text that is shown in the inventory slot tooltip. It tells what key"
              + " modifier to press and hold to enable the mode in which only one item is grabbed"
              + " on the inventory slot click.\n"
              + " The <<1>> argument is the keyboard key that needs to be pressed and held.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> TakeTenModifierHintText =
      new Message<KeyboardEventType>(
          "",
          defaultTemplate: "Press and hold <b><color=#5a5><<1>></color></b>"
              + " to take <color=#5a5>10</color> items",
          description: "Hint text that is shown in the inventory slot tooltip. It tells what key"
              + " modifier to press and hold to enable the mode in which only one item is grabbed"
              + " on the inventory slot click.\n"
              + " The <<1>> argument is the keyboard key that needs to be pressed and held.");
  #endregion

  #region API properties and fields
  /// <summary>
  /// Short name of the checking error for the case when parts with different signature are being
  /// added to the slot.
  /// </summary>
  /// <seealso cref="DifferentPartsReasonText"/>
  public const string DifferentPartReason = "DifferentPart";

  /// <summary>Unity object that represents the slot.</summary>
  /// <remarks>It can be <c>null</c> if the slot is not shown in the inventory window.</remarks>
  public UIKISInventorySlot.Slot unitySlot {
    get { return _unitySlot;  }
    set {
      _unitySlot = value;
      UpdateUnitySlot();
    }
  }
  UIKISInventorySlot.Slot _unitySlot;

  /// <summary>Inventory that owns this slot.</summary>
  public readonly KISContainerWithSlots inventory;

  /// <summary>Part proto of this slot.</summary>
  public AvailablePart avPart => !isEmpty ? slotItems[0].avPart : null;

  /// <summary>Inventory items in the slot.</summary>
  public InventoryItem[] slotItems { get; private set; } = new InventoryItem[0];

  /// <summary>Generalized icon of the slot.</summary>
  public Texture iconImage =>
      !isEmpty ? KISAPI.PartIconUtils.MakeDefaultIcon(avPart, 256, null) : null;

  /// <summary>Tells if this slot has any part item.</summary>
  public bool isEmpty => slotItems.Length == 0;

  public bool isLocked {
    get { return _isLocked; }
    set {
      _isLocked = value;
      if (unitySlot != null) {
        unitySlot.isLocked = value;
      }
    }
  }
  bool _isLocked;
  #endregion

  #region Local constants
  static readonly Event TakeSlotEvent = Event.KeyboardEvent("mouse0");
  static readonly Event TakeOneItemEvent = Event.KeyboardEvent("&mouse0");
  static readonly Event TakeOneItemModifierEvent = Event.KeyboardEvent("LeftAlt");
  static readonly Event TakeTenItemsEvent = Event.KeyboardEvent("#mouse0");
  static readonly Event TakeTenItemsModifierEvent = Event.KeyboardEvent("LeftShift");
  static readonly Event AddItemsIntoStackEvent = Event.KeyboardEvent("mouse0");
  static readonly Event DropIntoSlotEvent = Event.KeyboardEvent("mouse0");
  #endregion

  #region Local fields and properties
  /// <summary>
  /// Reflection of <see cref="slotItems"/> in a form of hash set for quick lookup operations. 
  /// </summary>
  /// <seealso cref="UpdateItems"/>
  readonly HashSet<InventoryItem> _itemsSet = new HashSet<InventoryItem>();
  bool _canAcceptDraggedItems;
  ErrorReason[] _canAcceptDraggedItemsCheckResult;
  UIKISInventoryTooltip.Tooltip currentTooltip => inventory.unityWindow.currentTooltip;
  #endregion

  #region IKISDragTarget implementation
  /// <inheritdoc/>
  public void OnKISDragStart() {
    _canAcceptDraggedItemsCheckResult = CheckCanAddItems(KISAPI.ItemDragController.leasedItems);
    _canAcceptDraggedItems = _canAcceptDraggedItemsCheckResult == null;
    UpdateTooltip();
  }

  /// <inheritdoc/>
  public void OnKISDragEnd(bool isCancelled) {
    _canAcceptDraggedItemsCheckResult = null;
    _canAcceptDraggedItems = false;
    UpdateTooltip();
  }

  /// <inheritdoc/>
  public bool OnKISDrag(bool pointerMoved) {
    return _canAcceptDraggedItems;
  }
  #endregion

  /// <summary>Handles mouse clicks on the Unity slot.</summary>
  public void OnSlotClicked(PointerEventData.InputButton button) {
    if (KISAPI.ItemDragController.isDragging && isLocked
        || !KISAPI.ItemDragController.isDragging) {
      // User wants to take/add items to the dragging action.
      MouseClickTakeItems(button);
    } else if (KISAPI.ItemDragController.isDragging) {
      // User wants to store items into the slot.
      MouseClickDropItems(button);
    } else {
      // NOT expected. When you don't know what to do, play the "BIP WRONG" sound!
      UISoundPlayer.instance.Play(KISAPI.CommonConfig.sndPathBipWrong);
    }
  }

  /// <summary>Makes an inventory slot, bound to its Unity counterpart.</summary>
  public InventorySlotImpl(KISContainerWithSlots inventory, UIKISInventorySlot.Slot unitySlot) {
    this.inventory = inventory;
    this.unitySlot = unitySlot;
  }

  #region API methods
  /// <summary>Adds an item to the slot.</summary>
  /// <remarks>
  /// This method doesn't check preconditions. The items will be added even if it break the slot's
  /// logic. The caller is responsible to verify if the items can be added via the
  /// <see cref="CheckCanAddItems"/> before attempting to add anything.
  /// </remarks>
  /// <param name="items">
  /// The items to add. They are not copied, they are added as the references. These items must
  /// belong to the same inventory as the slot!
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
    if (logErrors && errors.Count > 0) {
      DebugEx.Error("Cannot add items to slot:\n{0}", DbgFormatter.C2S(errors, separator: "\n"));
    }
    return errors.Count > 0 ? errors.ToArray() : null;
  }

  /// <summary>Fills tooltip with the slot info.</summary>
  /// <remarks>
  /// If the slot is empty, then no update is made. The tooltip visibility is never affected.
  /// </remarks>
  /// <seealso cref="currentTooltip"/>
  public void UpdateTooltip() {
    if (KISAPI.ItemDragController.isDragging) {
      UpdateDraggingStateTooltip();
    } else {
      UpdateSimpleHoveringTooltip();
    }
    UpdateHints();
    currentTooltip.UpdateLayout();
  }

  /// <summary>Fills or updates the hints section of the slot tooltip.</summary>
  /// <remarks>
  /// The hints may depend on the current game state (like the keyboard keys pressed), so it's
  /// assumed that this method may be called every frame when the interactive mode is ON.
  /// </remarks>
  /// <seealso cref="currentTooltip"/>
  /// <seealso cref="UIKISInventoryTooltip.Tooltip.showHints"/>
  public void UpdateHints() {
    var hints = new List<string>();
    if (KISAPI.ItemDragController.isDragging) {
      if (isEmpty) {
        hints.Add(StoreItemsHintText.Format(DropIntoSlotEvent));
      } else if (_canAcceptDraggedItems) {
        hints.Add(AddItemsHintText.Format(AddItemsIntoStackEvent));
      }
    } else if (!isEmpty) {
      if (EventChecker.CheckClickEvent(TakeSlotEvent)) {
        hints.Add(TakeSlotHintText.Format(TakeSlotEvent));
        hints.Add(TakeOneModifierHintText.Format(TakeOneItemModifierEvent));
        hints.Add(TakeTenModifierHintText.Format(TakeTenItemsModifierEvent));
      } else if (EventChecker.CheckClickEvent(TakeOneItemEvent)) {
        hints.Add(TakeOneItemHintText.Format(TakeOneItemEvent));
      } else if (EventChecker.CheckClickEvent(TakeTenItemsEvent)) {
        hints.Add(TakeTenItemsHintText.Format(TakeTenItemsEvent));
      }
    }
    currentTooltip.hints = hints.Count > 0 ? string.Join("\n", hints.ToArray()) : null;
  }
  #endregion

  #region Local utility methods
  /// <summary>
  /// Updates tooltip when mouse pointer is hovering over the slot AND the dragging mode was
  /// started.
  /// </summary>
  void UpdateDraggingStateTooltip() {
    currentTooltip.ClearInfoFileds();
    if (isEmpty) {
      currentTooltip.title = StoreItemsTooltipText;
      currentTooltip.baseInfo.text = null;
    } else {
      if (_canAcceptDraggedItems) {
        currentTooltip.title = AddItemsTooltipText;
        currentTooltip.baseInfo.text = KISAPI.ItemDragController.leasedItems.Length > 1
            ? AddItemsCountHintText.Format(KISAPI.ItemDragController.leasedItems.Length)
            : null;
      } else {
        currentTooltip.title = CannotAddItemsTooltipText;
        if (_canAcceptDraggedItemsCheckResult != null) {
          currentTooltip.baseInfo.text = string.Join(
              "\n",
              _canAcceptDraggedItemsCheckResult
                  .Where(r => r.guiString != null)
                  .Select(r => r.guiString)
                  .ToArray());
        }
      }
    }
  }

  /// <summary>
  /// Updates tooltip when mouse pointer is hovering over the slot but the dragging mode was not
  /// started.
  /// </summary>
  void UpdateSimpleHoveringTooltip() {
    currentTooltip.ClearInfoFileds();
    if (isEmpty) {
      return;
    }
    if (slotItems.Length == 1) {
      UpdateSingleItemTooltip(slotItems[0]);
    } else {
      //FIXME: handle multuple item slots
      currentTooltip.baseInfo.text = "*** multiple items";
    }
  }

  /// <summary>Fills tooltip with useful information about the items in the slot.</summary>
  void UpdateSingleItemTooltip(InventoryItem item) {
    currentTooltip.title = avPart.title;
    var infoLines = new List<string>();

    // Basic stats.
    infoLines.Add(MassTooltipText.Format(item.fullMass));
    infoLines.Add(VolumeTooltipText.Format(item.volume));
    infoLines.Add(CostTooltipText.Format(item.fullCost));
    var variant = VariantsUtils.GetCurrentPartVariant(item.avPart, item.itemConfig);
    if (variant != null) {
      infoLines.Add(VariantTooltipText.Format(variant.DisplayName));
    }
    currentTooltip.baseInfo.text = string.Join("\n", infoLines.ToArray());

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
      currentTooltip.availableResourcesInfo.text = string.Join("\n", resItems.ToArray());
    } else {
      currentTooltip.availableResourcesInfo.text = null;
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
    if (unitySlot == null) {
      return;
    }
    if (isEmpty || unitySlot == null) {
      unitySlot.ClearContent();
      return;
    }
    unitySlot.slotImage = iconImage;
    unitySlot.stackSize = "x" + slotItems.Length;

    // Slot resources info.
    if (slotItems[0].resources.Length > 0) {
      var cumAvgPct = slotItems.Where(item => item.resources.Length > 0)
          .Sum(item => item.resources.Sum(r => r.amount / r.maxAmount) / item.resources.Length);
      unitySlot.resourceStatus = GetResourceAmountStatus(cumAvgPct / slotItems.Length);
    } else {
      unitySlot.resourceStatus = null;
    }
    // Slot science data.
    // FIXME: implement
  }

  /// <summary>Triggers when items from this slot are consumed by the target.</summary>
  bool ConsumeSlotItems() {
    isLocked = false;
    Array.ForEach(KISAPI.ItemDragController.leasedItems, i => i.SetLocked(false));
    inventory.DeleteItems(KISAPI.ItemDragController.leasedItems);
    return true;
  }

  /// <summary>Triggers when dragging items from this slot has been canceled.</summary>
  void CancelSlotLeasedItems() {
    isLocked = false;
    Array.ForEach(KISAPI.ItemDragController.leasedItems, i => i.SetLocked(false));
  }

  /// <summary>
  /// Handles slot clicks when the drag operation is not started or has started on this slot.
  /// </summary>
  /// <remarks>Beeps and logs an error if the action cannot be completed.</remarks>
  /// <returns><c>true</c> if action was successful.</returns>
  bool MouseClickTakeItems(PointerEventData.InputButton button) {
    var newCanTakeItems = slotItems.Where(i => !i.isLocked).ToArray();
    InventoryItem[] itemsToDrag = null;
    if (EventChecker.CheckClickEvent(TakeSlotEvent, button)
        && !isLocked && !KISAPI.ItemDragController.isDragging
        && newCanTakeItems.Length > 0) {
      DebugEx.Warning("*** take whole slot");
      itemsToDrag = newCanTakeItems;
    } else if (EventChecker.CheckClickEvent(TakeOneItemEvent, button)
               && newCanTakeItems.Length >= 1) {
      DebugEx.Warning("*** take one item");
      itemsToDrag = newCanTakeItems.Take(1).ToArray();
    } else if (EventChecker.CheckClickEvent(TakeTenItemsEvent, button)
               && newCanTakeItems.Length >= 10) {
      DebugEx.Warning("*** take ten items");
      itemsToDrag = newCanTakeItems.Take(10).ToArray();
    }
    if (itemsToDrag == null) {
      UISoundPlayer.instance.Play(KISAPI.CommonConfig.sndPathBipWrong);
      HostedDebugLog.Error(
          inventory, "Cannot take items from slot: totalItems={0}, canTakeItems={1}",
          slotItems.Length, newCanTakeItems.Length);
      return false;
    }

    if (KISAPI.ItemDragController.isDragging) {
      Array.ForEach(KISAPI.ItemDragController.leasedItems, i => i.SetLocked(false));
      itemsToDrag = itemsToDrag.Concat(KISAPI.ItemDragController.leasedItems).ToArray();
      KISAPI.ItemDragController.CancelItemsLease();
    }
    KISAPI.ItemDragController.LeaseItems(
        iconImage, itemsToDrag, ConsumeSlotItems, CancelSlotLeasedItems);
    var dragIconObj = KISAPI.ItemDragController.dragIconObj;
    dragIconObj.hasScience = unitySlot.hasScience;
    dragIconObj.stackSize = itemsToDrag.Length;//FIXME: get it from unity when it's fixed to int
    dragIconObj.resourceStatus = unitySlot.resourceStatus;
    isLocked = true;
    Array.ForEach(itemsToDrag, i => i.SetLocked(true));
    return true;
  }

  /// <summary>
  /// Handles slot clicks when there is a drag operation pending from another slot.
  /// </summary>
  /// <param name="button"></param>
  void MouseClickDropItems(PointerEventData.InputButton button) {
    //FIXME
    DebugEx.Warning("*** drop items requested");
    
    //FIXME: verify what is clicked. ignore unknown events
    var storeItems = isEmpty && EventChecker.CheckClickEvent(DropIntoSlotEvent, button);
    var stackItems = !isEmpty && EventChecker.CheckClickEvent(AddItemsIntoStackEvent, button);
    if (!storeItems && !stackItems) {
      return;  // Nothing to do.
    }
    //FIXME: implement other branches
  }

  /// <summary>Adds or deletes items to/from the slot.</summary>
  void UpdateItems(InventoryItem[] addItems = null, InventoryItem[] deleteItems = null) {
    if (addItems != null) {
      Array.ForEach(addItems, x => _itemsSet.Add(x));
    }
    if (deleteItems != null) {
      Array.ForEach(deleteItems, x => _itemsSet.Remove(x));
    }
    // Reconstruct the items array so that the existing items keep their original order, and the new
    // items (if any) are added at the tail.  
    if (addItems != null) {
      slotItems = slotItems.Where(x => _itemsSet.Contains(x))
          .Concat(addItems)
          .ToArray();
    } else {
      slotItems = slotItems.Where(x => _itemsSet.Contains(x))
          .ToArray();
    }
    UpdateUnitySlot();
    if (inventory.unityWindow.hoveredSlot != null
        && inventory.unityWindow.hoveredSlot == unitySlot) {
      UpdateTooltip();
    }
  }
  #endregion
}
  
}  // namespace
