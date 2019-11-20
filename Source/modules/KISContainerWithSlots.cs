// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using KSP.UI;
using System;
using System.Linq;
using KSPDev.InputUtils;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using KSPDev.PrefabUtils;
using KISAPIv2;
using KIS2.UIKISInventorySlot;
using KIS2.GUIUtils;
using KSPDev.Unity;
using UnityEngine;
using UnityEngine.EventSystems;

// ReSharper disable once CheckNamespace
namespace KIS2 {

  //FIXME: separate container and inventory concepts. data vs UI
public sealed class KISContainerWithSlots : KisContainerBase,
    IHasGUI, IKISDragTarget {

  #region Localizable GUI strings.
  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<string> DialogTitle = new Message<string>(
      "",
      defaultTemplate: "Inventory: <<1>>",
      description: "Title of the inventory dialog for this part.\n"
          + " The <<1>> argument is a user friendly name of the owner part.",
      example: "Inventory: SC-62 Portable Container");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<MassType> InventoryContentMassStat = new Message<MassType>(
      "",
      defaultTemplate: "Content mass: <color=#58F6AE><<1>></color>");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<CostType> InventoryContentCostStat = new Message<CostType>(
      "",
      defaultTemplate: "Content cost: <color=#58F6AE><<1>></color>");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<VolumeLType> AvailableVolumeStat = new Message<VolumeLType>(
      "",
      defaultTemplate: "Available volume: <color=yellow><<1>></color>");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<VolumeLType> MaxVolumeStat = new Message<VolumeLType>(
      "",
      defaultTemplate: "Maximum volume: <color=#58F6AE><<1>></color>");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message NoSlotsErrorReason = new Message(
      "",
      defaultTemplate: "No enough compatible slots",
      description: "Error message that is presented when parts cannot be added into the inventory"
          + " due to there are not enough compatible/empty slots available.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> TakeSlotHint = new Message<KeyboardEventType>(
      "",
      defaultTemplate: "<b><color=#5a5><<1>></color></b> to grab the stack",
      description: "Hint text in the inventory slot tooltip that tells what action"
      + " user should do to take the whole slot from the inventory and add it into the currently"
      + " dragged pack.\n"
      + " The <<1>> argument is a user friendly action name.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> TakeOneItemHint = new Message<KeyboardEventType>(
      "",
      defaultTemplate: "<b><color=#5a5><<1>></color></b> to grab <color=#5a5>1</color> item",
      description: "Hint text in the inventory slot tooltip that tells what action"
      + " user should do to add one item from the inventory slot into the currently dragged pack.\n"
      + " The <<1>> argument is a user friendly action name.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> TakeTenItemsHint = new Message<KeyboardEventType>(
      "",
      defaultTemplate: "<b><color=#5a5><<1>></color></b> to grab <color=#5a5>10</color> items",
      description: "Hint text in the inventory slot tooltip that tells what action"
      + " user should do to add 10 items from the inventory slot into the currently dragged pack.\n"
      + " The <<1>> argument is a user friendly action name.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message StoreToSlotActionTooltip = new Message(
      "",
      defaultTemplate: "Store items",
      description: "The text to show in the title of the slot tooltip when the dragged items can be"
          + " stored into an empty slot.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message AddToSlotActionTooltip = new Message(
      "",
      defaultTemplate: "Add items to stack",
      description: "The text to show in the title of the slot tooltip when the dragged items can be"
          + " added into an non-empty slot.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message<KeyboardEventType> StoreToSlotActionHint = new Message<KeyboardEventType>(
      "",
      defaultTemplate: "<b><color=#5a5><<1>></color></b> to store items into the slot",
      description: "Hint text in the inventory slot tooltip that tells what action"
          + " user should do to store the dragged items into the hovered slot."
          + " The slot can be empty or have some items already.\n"
          + " The <<1>> argument is a user friendly action name.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message<int> StoreToSlotCountHint = new Message<int>(
      "",
      defaultTemplate: "Add <color=#5a5><<1>></color> items",
      description: "The text to show in the inventory tooltip that tells how many items will be"
      + " added into the stack in case of the action has completed. The slot "
      + " can be empty or have some items already.\n"
      + " The <<1>> argument is the number of items being added.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message CannotAddToSlotTooltipText = new Message(
      "",
      defaultTemplate: "Cannot add items to slot",
      description: "The text to show in the title of the slot tooltip when the dragged items can"
          + " NOT be added into the slot. Only shown when the target slot is not empty.");
  #endregion

  #region Part's config fields
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Vector2 minGridSize = new Vector2(3, 1);
  
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Vector2 maxGridSize = new Vector2(16, 9);

  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField(isPersistant = true)]
  public Vector2 persistedGridSize;
  #endregion

  #region GUI menu action handlers
  [KSPEvent(guiActive = true, guiActiveUnfocused = true)]
  [LocalizableItem(
      tag = "",
      defaultTemplate = "KISv2: Inventory",
      description = "A context menu event that opens GUI for the inventory.")]
  public void ShowInventory() {
    if (_unityWindow == null) {
      OpenInventoryWindow();
    } else {
      CloseInventoryWindow();
    }
  }
  #endregion

  #region API fields and properties
  /// <summary>
  /// Short name of the checking error for the case when parts cannot fit to the existing slots.
  /// </summary>
  /// <seealso cref="NoSlotsErrorReason"/>
  // ReSharper disable once MemberCanBePrivate.Global
  public const string NoSlotsReason = "NoSlots";
  #endregion

  #region Event static configs
  static readonly Event TakeSlotEvent = Event.KeyboardEvent("mouse0");
  static readonly Event TakeOneItemEvent = Event.KeyboardEvent("&mouse0");
  static readonly Event TakeTenItemsEvent = Event.KeyboardEvent("#mouse0");
  static readonly Event AddIntoStackEvent = Event.KeyboardEvent("mouse0");
  static readonly Event DropIntoSlotEvent = Event.KeyboardEvent("mouse0");
  #endregion

  #region Local fields and properties.
  /// <summary>Inventory window that is opened at the moment.</summary>
  UIKISInventoryWindow _unityWindow;

  /// <summary>Inventory slots.</summary>
  /// <remarks>Some or all slots may not be represented in the UI.</remarks>
  /// <seealso cref="InventorySlotImpl.isVisible"/>
  readonly List<InventorySlotImpl> _inventorySlots = new List<InventorySlotImpl>();

  /// <summary>Index that resolves item to the slot that contains it.</summary>
  readonly Dictionary<InventoryItem, InventorySlotImpl> _itemToSlotMap =
      new Dictionary<InventoryItem, InventorySlotImpl>();

  /// <summary>Slot that initiated a drag action from this inventory.</summary>
  InventorySlotImpl _dragSourceSlot;

  /// <summary>A slot of this inventory that is currently has pointer focus.</summary>
  /// <remarks>This slot is the target for the pointer actions.</remarks>
  InventorySlotImpl _slotWithPointerFocus;

  /// <summary>
  /// Tells if currently dragging items can fit into the currently hovered slot of this inventory.
  /// </summary>
  /// <seealso cref="_slotWithPointerFocus"/>
  bool _canAcceptDraggedItems;

  /// <summary>The errors from the last hovered slot check.</summary>
  ErrorReason[] _canAcceptDraggedItemsCheckResult;

  /// <summary>Shortcut to get the current tooltip.</summary>
  UIKISInventoryTooltip.Tooltip currentTooltip => _unityWindow.currentTooltip;
  #endregion

  #region IKISDragTarget implementation
  /// <inheritdoc/>
  public void OnKISDragStart() {
    _canAcceptDraggedItemsCheckResult =
        _slotWithPointerFocus.CheckCanAddItems(KISAPI.ItemDragController.leasedItems);
    _canAcceptDraggedItems = _canAcceptDraggedItemsCheckResult == null;
  }

  /// <inheritdoc/>
  public void OnKISDragEnd(bool isCancelled) {
    _canAcceptDraggedItemsCheckResult = null;
    _canAcceptDraggedItems = false;
    _slotWithPointerFocus.UpdateTooltip(_unityWindow.currentTooltip);
  }

  /// <inheritdoc/>
  public bool OnKISDrag(bool pointerMoved) {
    UpdateTooltip();
    return _canAcceptDraggedItems;
  }
  #endregion

  #region AbstractPartModule overrides
  public override void OnAwake() {
    base.OnAwake();
    useGUILayout = false;
  }

  public override void OnDestroy() {
    CloseInventoryWindow();
    base.OnDestroy();
  }
  #endregion

  #region DEBUG: IHasGUI implementation
  public void OnGUI() {
    // FIXME: drop this debug code.
    if (Event.current.Equals(Event.KeyboardEvent("1")) && _unityWindow != null) {
      AddFuelParts(1, 1, 3);
    }
    if (Event.current.Equals(Event.KeyboardEvent("2")) && _unityWindow != null) {
      AddFuelParts(0.1f, 0.5f);
    }
    if (Event.current.Equals(Event.KeyboardEvent("3")) && _unityWindow != null) {
      AddFuelParts(0.5f, 0.5f);
    }
    if (Event.current.Equals(Event.KeyboardEvent("4")) && _unityWindow != null) {
      AddFuelParts(1, 1);
    }
    if (Event.current.Equals(Event.KeyboardEvent("5")) && _unityWindow != null) {
      AddFuelParts(0.1f, 0.5f, num: 3, same: true);
    }
    if (Event.current.Equals(Event.KeyboardEvent("6")) && _unityWindow != null
        && _inventorySlots.Count > 0 && _inventorySlots[0].slotItems.Length > 0) {
      DeleteItems(_inventorySlots[0].slotItems);
    }
  }
  #endregion

  #region KISContainerBase overrides
  /// <inheritdoc/>
  public override InventoryItem[] AddParts(AvailablePart[] avParts, ConfigNode[] nodes) {
    var newItems = base.AddParts(avParts, nodes);
    foreach (var item in newItems) {
      AddItemsToSlot(new[] { item }, FindSlotForItem(item, addInvisibleSlot: true));
    }
    UpdateTooltip();
    return newItems;
  }

  /// <inheritdoc/>
  public override bool DeleteItems(InventoryItem[] deleteItems) {
    if (!base.DeleteItems(deleteItems)) {
      return false;
    }
    //FIXME: group by slot to effectively delete many items from one slot.
    foreach (var deleteItem in deleteItems) {
      var slot = _itemToSlotMap[deleteItem];
      slot.DeleteItems(new[] {deleteItem});
      _itemToSlotMap.Remove(deleteItem);
      if (slot == _slotWithPointerFocus) {
        slot.UpdateTooltip(_unityWindow.currentTooltip);
      }
    }
    ArrangeSlots();
    return true;
  }

  /// <inheritdoc/>
  public override ErrorReason[] CheckCanAddParts(
      AvailablePart[] avParts, ConfigNode[] nodes = null, bool logErrors = false) {
    var res = base.CheckCanAddParts(avParts, nodes, logErrors);
    if (res != null) {
      return res;  // Don't go deeper when the volume constraints are not satisfied.
    }
    if (CheckHasVisibleSlots(avParts, nodes ?? new ConfigNode[avParts.Length])) {
      return null;
    }
    var errors = new[] {
        new ErrorReason() {
            shortString = NoSlotsReason,
            guiString = NoSlotsErrorReason,
        }
    };
    if (logErrors) {
      HostedDebugLog.Error(this, "Cannot add {0} part(s):\n{1}",
                           avParts.Length, DbgFormatter.C2S(errors, separator: "\n"));
    }
    return errors;
  }
  
  /// <inheritdoc/>
  public override void UpdateInventoryStats(InventoryItem[] changedItems) {
    base.UpdateInventoryStats(changedItems);
    UpdateInventoryWindow();
  }
  #endregion

  #region DEBUG: parts spawning methods
  readonly string[] _fuelParts = new string[] {
      "RadialOreTank",
      "SmallTank",
      "fuelTankSmallFlat",
      "Size3MediumTank",
      "Size3LargeTank",
      "Size3SmallTank",
      "fuelTankSmallFlat",
      "fuelTankSmall",
      "fuelTank",
      "fuelTank.long",
      "RCSTank1-2",
      "externalTankCapsule",
  };

  void AddFuelParts(float minPct, float maxPct, int num = 1, bool same = false) {
    var avParts = new AvailablePart[num];
    var nodes = new ConfigNode[num];
    AvailablePart avPart = null;
    for (var i = 0; i < num; ++i) {
      if (avPart == null || !same) {
        var avPartIndex = (int) (UnityEngine.Random.value * _fuelParts.Length);
        avPart = PartLoader.getPartInfoByName(_fuelParts[avPartIndex]);
      }
      avParts[i] = avPart;
      var node = KISAPI.PartNodeUtils.PartSnapshot(avPart.partPrefab);
      foreach (var res in KISAPI.PartNodeUtils.GetResources(node)) {
        var amount = UnityEngine.Random.Range(minPct, maxPct) * res.maxAmount;
        KISAPI.PartNodeUtils.UpdateResource(node, res.resourceName, amount);
      }
      nodes[i] = node;
    }
    //FIXME: check volume and size
    AddParts(avParts, nodes);
  }

  // ReSharper disable once UnusedMember.Local
  void AddPartByName(string partName) {
    var avPart = PartLoader.getPartInfoByName(partName);
    if (avPart == null) {
      DebugEx.Error("*** bummer: no part {0}", partName);
      return;
    }
    AddParts(new[] {avPart}, new ConfigNode[1]);
  }
  #endregion

  #region Unity window callbacks
  /// <summary>A callback that is called when pointer enters or leaves a UI slot.</summary>
  /// <param name="hoveredSlot">The Unity slot that is a source of the event.</param>
  /// <param name="isHover">Tells if pointer enters or leaves the UI element.</param>
  void OnSlotHover(Slot hoveredSlot, bool isHover) {
    if (isHover) {
      RegisterSlotHoverCallback();
    } else {
      UnregisterSlotHoverCallback();
    }
  }

  /// <summary>Callback for the Unity slot click action.</summary>
  void OnSlotClick(Slot slot, PointerEventData.InputButton button) {
    if (_slotWithPointerFocus == null) {
      HostedDebugLog.Error(this, "Unexpected slot click event");
      return;
    }
    if (KISAPI.ItemDragController.isDragging && _slotWithPointerFocus == _dragSourceSlot
        || !KISAPI.ItemDragController.isDragging) {
      MouseClickTakeItems(button); // User wants to start/add items to the dragging action.
    } else if (KISAPI.ItemDragController.isDragging) {
      MouseClickDropItems(button); // User wants to store items into the slot.
    }
  }

  /// <summary>Callback on the slot's action button click.</summary>
  void OnSlotAction(Slot slot, int actionButtonNum, PointerEventData.InputButton button) {
    //FIXME
    HostedDebugLog.Fine(
        this, "Clicked: slot={0}, action={1}, button={2}", slot.slotIndex, actionButtonNum, button);
  }

  /// <summary>Callback that is called when the slots grid is trying to resize.</summary>
  Vector2 OnNewGridSize(Vector2 newSize) {
    //FIXME: check if not below non-empty slots
    return newSize;
  }

  /// <summary>Callback when the slots grid size has changed.</summary>
  void OnGridSizeChanged() {
    ArrangeSlots();  // Trigger compaction if there are invisible items.
  }
  #endregion

  #region Local utility methods
  /// <summary>Opens the inventory window.</summary>
  void OpenInventoryWindow() {
    if (_unityWindow != null) {
      return; // Nothing to do.
    }
    HostedDebugLog.Fine(this, "Creating inventory window");
    _unityWindow = UnityPrefabController.CreateInstance<UIKISInventoryWindow>(
        "KISInventoryDialog", UIMasterController.Instance.actionCanvas.transform);

    // TODO(ihsoft): Fix it in the prefab via TMPro.
    if (!UIMasterController.Instance.actionCanvas.pixelPerfect) {
      DebugEx.Warning("WORKAROUND: Enabling PerfectPixel mode on the root UI canvas");
      UIMasterController.Instance.actionCanvas.pixelPerfect = true;
    }

    _unityWindow.gameObject.AddComponent<UIScalableWindowController>();
    _unityWindow.onSlotHover.Add(OnSlotHover);
    _unityWindow.onSlotClick.Add(OnSlotClick);
    _unityWindow.onSlotAction.Add(OnSlotAction);
    _unityWindow.onNewGridSize.Add(OnNewGridSize);
    _unityWindow.onGridSizeChanged.Add(OnGridSizeChanged);

    var gridSize = persistedGridSize;
    if (gridSize.x < minGridSize.x) {
      gridSize.x = minGridSize.x;
    }
    if (gridSize.y < minGridSize.y) {
      gridSize.y = minGridSize.y;
    }
    persistedGridSize = gridSize;
    _unityWindow.title = DialogTitle.Format(part.partInfo.title);
    _unityWindow.minSize = minGridSize;
    _unityWindow.maxSize = maxGridSize;
    _unityWindow.SetGridSize(persistedGridSize);
    UpdateInventoryWindow();
  }

  /// <summary>Destroys the inventory window.</summary>
  void CloseInventoryWindow() {
    if (_unityWindow == null) {
      return;
    }
    HostedDebugLog.Fine(this, "Destroying inventory window");
    Hierarchy.SafeDestory(_unityWindow);
    _unityWindow = null;
    // Immediately make all slots invisible. Don't relay on Unity cleanup routines.  
    _inventorySlots.ForEach(x => x.BindTo(null));
  } 

  /// <summary>Updates stats in the open inventory window.</summary>
  /// <remarks>It's safe to call it when the inventory window is not open.</remarks>
  void UpdateInventoryWindow() {
    if (_unityWindow == null) {
      return;
    }
    var text = new List<string> {
        InventoryContentMassStat.Format(contentMass),
        InventoryContentCostStat.Format(contentCost),
        MaxVolumeStat.Format(maxVolume),
        AvailableVolumeStat.Format(Math.Max(maxVolume - usedVolume, 0))
    };
    _unityWindow.mainStats = string.Join("\n", text);
  }

  /// <summary>Check if inventory has enough visible slots to accomodate the items.</summary>
  bool CheckHasVisibleSlots(IReadOnlyList<AvailablePart> avParts, IReadOnlyList<ConfigNode> nodes) {
    var newSlots = new HashSet<InventorySlotImpl>();
    for (var i = 0; i < avParts.Count; ++i) {
      var avPart = avParts[i];
      var node = nodes[i] ?? KISAPI.PartNodeUtils.PartSnapshot(avPart.partPrefab);
      var slot = FindSlotForItem(
          new InventoryItemImpl(this, avPart, node), preferredSlots: newSlots);
      if (slot == null) {
        return false;
      }
      if (slot.isEmpty) {
        newSlots.Add(new InventorySlotImpl(null));
      }
    }
    var emptySlots = _inventorySlots.Count(s => s.isEmpty);
    return emptySlots > newSlots.Count;
  }

  /// <summary>Returns a slot where the item can be stored.</summary>
  /// <remarks>
  /// This method tries to find a best slot, so that the inventory is kept as dense as possible.
  /// </remarks>
  /// <param name="item">
  /// The item to find a slot for. It may belong to a different inventory.
  /// </param>
  /// <param name="preferredSlots">
  /// If set, then these slots will be checked for best fit first. The preferred slots can be
  /// invisible.
  /// </param>
  /// <param name="addInvisibleSlot">
  /// If <c>true</c>, then a new invisible slot will be created in the inventory in case of no
  /// compatible slot was found.
  /// </param>
  /// <returns>The available slot or <c>null</c> if none found.</returns>
  /// <seealso cref="_inventorySlots"/>
  InventorySlotImpl FindSlotForItem(InventoryItem item,
                                    IEnumerable<InventorySlotImpl> preferredSlots = null,
                                    bool addInvisibleSlot = false) {
    InventorySlotImpl slot;
    var matchItems = new[] { item };
    // First, try to store the item into one of the preferred slots.
    if (preferredSlots != null) {
      slot = preferredSlots.FirstOrDefault(x => x.CheckCanAddItems(matchItems) == null);
      if (slot != null) {
        return slot;
      }
    }
    // Then, try to store the item into an existing slot to save slots space.
    slot = _inventorySlots
        .FirstOrDefault(x => !x.isEmpty && x.CheckCanAddItems(matchItems) == null);
    if (slot != null) {
      return slot;
    }
    // Then, pick a first empty slot, given it's visible.
    slot = _inventorySlots.FirstOrDefault(s => s.isEmpty && s.isVisible);
    if (slot != null) {
      return slot;
    }
    if (!addInvisibleSlot) {
      return null;
    }
    // Finally, create a new invisible slot to fit the items.
    HostedDebugLog.Warning(this, "Adding an invisible slot: slotIdx={0}", _inventorySlots.Count);
    _inventorySlots.Add(new InventorySlotImpl(null));
    slot = _inventorySlots[_inventorySlots.Count - 1];
    return slot;
  }

  /// <summary>Add items to the specified slot of the inventory.</summary>
  /// <remarks>
  /// The items must belong to the inventory, but not be owned by it (i.e. not to be in the
  /// <see cref="KisContainerBase.inventoryItems"/>). This method doesn't check any preconditions.
  /// </remarks>
  /// <seealso cref="InventorySlotImpl.CheckCanAddItems"/>
  void AddItemsToSlot(InventoryItem[] addItems, InventorySlotImpl slot) {
    slot.AddItems(addItems);
    Array.ForEach(addItems, x => _itemToSlotMap.Add(x, slot));
  }

  /// <summary>
  /// Handles slot clicks when the drag operation is not started or has started on this same slot.
  /// </summary>
  /// <remarks>This method requires a pointer actions target. It must not be <c>null</c>.</remarks>
  /// <seealso cref="_slotWithPointerFocus"/>
  void MouseClickTakeItems(PointerEventData.InputButton button) {
    var newCanTakeItems = _slotWithPointerFocus.slotItems
        .Where(i => !i.isLocked)
        .ToArray();
    InventoryItem[] itemsToDrag = null;

    // Go thru the known mouse+keyboard events for the action.
    if (EventChecker.CheckClickEvent(TakeSlotEvent, button)) {
      if (newCanTakeItems.Length > 0) {
        itemsToDrag = newCanTakeItems;
      }
    } else if (EventChecker.CheckClickEvent(TakeOneItemEvent, button)) {
      if (newCanTakeItems.Length >= 1) {
        itemsToDrag = newCanTakeItems.Take(1).ToArray();
      }
    } else if (EventChecker.CheckClickEvent(TakeTenItemsEvent, button)) {
      if (newCanTakeItems.Length >= 10) {
        itemsToDrag = newCanTakeItems.Take(10).ToArray();
      }
    }
    if (itemsToDrag == null) {
      UISoundPlayer.instance.Play(KISAPI.CommonConfig.sndPathBipWrong);
      HostedDebugLog.Error(
          this, "Cannot take items from slot: totalItems={0}, canTakeItems={1}",
          _slotWithPointerFocus.slotItems.Length, newCanTakeItems.Length);
      return;
    }

    // Either add or set the grabbed items.
    if (KISAPI.ItemDragController.isDragging) {
      itemsToDrag = KISAPI.ItemDragController.leasedItems.Concat(itemsToDrag).ToArray();
      KISAPI.ItemDragController.CancelItemsLease();
    }
    KISAPI.ItemDragController.LeaseItems(
        _slotWithPointerFocus.iconImage, itemsToDrag, ConsumeSlotItems, CancelSlotLeasedItems);
    Array.ForEach(itemsToDrag, i => i.SetLocked(true));
    SetDraggedSlot(_slotWithPointerFocus, itemsToDrag.Length);
    var dragIconObj = KISAPI.ItemDragController.dragIconObj;
    dragIconObj.hasScience = _slotWithPointerFocus.hasScience;
    dragIconObj.stackSize = itemsToDrag.Length;
    dragIconObj.resourceStatus = _slotWithPointerFocus.resourceStatus;
  }

  /// <summary>Triggers when items from this slot are consumed by the target.</summary>
  /// <remarks>
  /// This method may detect a failing condition. If it happens, the state must stay unchanged.
  /// </remarks>
  bool ConsumeSlotItems() {
    var leasedItems = KISAPI.ItemDragController.leasedItems;
    Array.ForEach(leasedItems, i => i.SetLocked(false));
    var res = DeleteItems(KISAPI.ItemDragController.leasedItems);
    if (!res) {
      // Something went wrong! Rollback.
      Array.ForEach(leasedItems, i => i.SetLocked(true));
      return false;
    }
    SetDraggedSlot(null);
    return true;
  }

  /// <summary>Triggers when dragging items from this slot has been canceled.</summary>
  /// <remarks>This method never fails.</remarks>
  void CancelSlotLeasedItems() {
    Array.ForEach(KISAPI.ItemDragController.leasedItems, i => i.SetLocked(false));
    SetDraggedSlot(null);
  }

  /// <summary>
  /// Handles slot clicks when there is a drag operation pending from another slot.
  /// </summary>
  /// <seealso cref="_slotWithPointerFocus"/>
  void MouseClickDropItems(PointerEventData.InputButton button) {
    var storeItems = _slotWithPointerFocus.isEmpty
        && EventChecker.CheckClickEvent(DropIntoSlotEvent, button);
    var stackItems = !_slotWithPointerFocus.isEmpty
        && EventChecker.CheckClickEvent(AddIntoStackEvent, button);
    InventoryItem[] consumedItems = null;
    if (storeItems || stackItems) {
      //FIXME: eligibility has already been checked
      if (_slotWithPointerFocus.CheckCanAddItems(
          KISAPI.ItemDragController.leasedItems, logErrors: true) == null) {
        consumedItems = KISAPI.ItemDragController.ConsumeItems();
      }
      if (consumedItems != null) {
        var avParts = new AvailablePart[consumedItems.Length];
        var itemConfigs = new ConfigNode[consumedItems.Length];
        for (var i = 0; i < consumedItems.Length; i++) {
          avParts[i] = consumedItems[i].avPart;
          itemConfigs[i] = consumedItems[i].itemConfig;
        } 
        var newItems = base.AddParts(avParts, itemConfigs);
        AddItemsToSlot(newItems, _slotWithPointerFocus);
      }
    }
    if (consumedItems == null) {
      UISoundPlayer.instance.Play(KISAPI.CommonConfig.sndPathBipWrong);
      HostedDebugLog.Error(
          this, "Cannot store/stack dragged items to slot: draggedItems={0}",
          KISAPI.ItemDragController.leasedItems.Length);
    }
  }

  /// <summary>
  /// Ensures that each UI slot has a corresponded inventory slot. Also, updates and optimizes the
  /// inventory slots that are not currently present in UI. 
  /// </summary>
  /// <remarks>
  /// This method must be called each time the inventory or unity slots number is changed.
  /// </remarks>
  void ArrangeSlots() {
    // Visible slots order may change, it would invalidate the tooltip.
    if (_slotWithPointerFocus != null) {
      UnregisterSlotHoverCallback();
    }

    // Compact empty slots when there are hidden slots in the inventory. This may let some of the
    // hidden slots to become visible. 
    var visibleSlots = _unityWindow.slots.Length;
    for (var i = _inventorySlots.Count - 1; i >= 0 && _inventorySlots.Count > visibleSlots; --i) {
      if (!_inventorySlots[i].isEmpty) {
        continue; 
      }
      if (i >= visibleSlots) {
        _inventorySlots.RemoveAt(i); // Simple cleanup, do it silently. 
      } else {
        // Cleanup of a visible empty slot. The inventory layout will change.
        HostedDebugLog.Warning(this, "Compact an empty slot: slotIdx={0}", i);
        _inventorySlots.RemoveAt(i);
      }
    }

    // Add up slots to match the current UI.
    for (var i = _inventorySlots.Count; i < _unityWindow.slots.Length; ++i) {
      _inventorySlots.Add(new InventorySlotImpl(null));
    }

    // Align logical slots with the visible slots in UI.
    for (var i = 0; i < _inventorySlots.Count; ++i) {
      var slot = _inventorySlots[i];
      var newUnitySlot = i < _unityWindow.slots.Length
          ? _unityWindow.slots[i]
          : null;
      if (!slot.isEmpty) {
        if (slot.isVisible && newUnitySlot == null) {
          HostedDebugLog.Warning(this, "Slot becomes hidden in UI: slotIdx={0}", i);
        } else if (!slot.isVisible && newUnitySlot != null) {
          HostedDebugLog.Fine(this, "Hidden slot becomes visible in UI: slotIdx={0}", i);
        }
      }
      slot.BindTo(newUnitySlot);
    }

    // Restore the tooltip callbacks if needed.
    if (_unityWindow.hoveredSlot != null) {
      RegisterSlotHoverCallback();
    }
  }

  /// <summary>Destroys tooltip and stops any active logic on the UI slot.</summary>
  void UnregisterSlotHoverCallback() {
    KISAPI.ItemDragController.UnregisterTarget(this);
    _unityWindow.DestroySlotTooltip();
    _slotWithPointerFocus = null;
  }

  /// <summary>Establishes a tooltip and starts the active logic on a UI slot.</summary>
  void RegisterSlotHoverCallback() {
    _slotWithPointerFocus =
        _inventorySlots.FirstOrDefault(x => x.IsBoundTo(_unityWindow.hoveredSlot));
    if (_slotWithPointerFocus == null) {
      HostedDebugLog.Error(this, "Expected to get a hovered slot, but none was found");
      return;
    }
    _unityWindow.StartSlotTooltip();
    UpdateTooltip();
    KISAPI.ItemDragController.RegisterTarget(this);
  }

  /// <summary>(Re)sets the current drag operation source.</summary>
  void SetDraggedSlot(InventorySlotImpl newDraggedSlot, int draggedItemsNum = 0) {
    if (_dragSourceSlot != null) {
      _dragSourceSlot.isLocked = false;
      _dragSourceSlot.reservedItems = 0;
      _dragSourceSlot = null;
    }
    _dragSourceSlot = newDraggedSlot;
    if (_dragSourceSlot != null) {
      _dragSourceSlot.isLocked = true;
      _dragSourceSlot.reservedItems = draggedItemsNum;
    }
  }

  /// <summary>Updates the currently focused slot (if any) with the relevant tooltip info.</summary>
  /// <remarks>
  /// This method needs to be called every time the slot content is changed in any way. Including
  /// the updates to the slot item configs. Note, that this methods is expensive and it's not
  /// advised to be invoked in every frame update.
  /// </remarks>
  void UpdateTooltip() {
    if (_slotWithPointerFocus == null || currentTooltip == null) {
      return;
    }
    currentTooltip.ClearInfoFileds();
    if (_dragSourceSlot != null && _slotWithPointerFocus != _dragSourceSlot) {
      currentTooltip.baseInfo.text =
          StoreToSlotCountHint.Format(KISAPI.ItemDragController.leasedItems.Length);
      currentTooltip.hints = StoreToSlotActionHint.Format(AddIntoStackEvent);
      if (_canAcceptDraggedItems) {
        currentTooltip.title = _slotWithPointerFocus.isEmpty
            ? StoreToSlotActionTooltip
            : AddToSlotActionTooltip;
      } else {
        currentTooltip.title = CannotAddToSlotTooltipText;
        if (_canAcceptDraggedItemsCheckResult != null) {
          currentTooltip.baseInfo.text = string.Join(
              "\n",
              _canAcceptDraggedItemsCheckResult
                  .Where(r => r.guiString != null)
                  .Select(r => r.guiString));
        }
        currentTooltip.hints = null;
      }
    } else {
      _slotWithPointerFocus.UpdateTooltip(_unityWindow.currentTooltip);
      if (!_slotWithPointerFocus.isEmpty) {
        var hints = new List<string> {
            TakeSlotHint.Format(TakeSlotEvent),
            TakeOneItemHint.Format(TakeOneItemEvent),
            TakeTenItemsHint.Format(TakeTenItemsEvent)
        };
        currentTooltip.hints = string.Join("\n", hints);
      } else {
        currentTooltip.hints = null;
      }
    }
  }
  #endregion
}

}  // namespace
