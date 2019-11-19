// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections;
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
  static readonly Message<MassType> InventoryContentMassTxt = new Message<MassType>(
      "",
      defaultTemplate: "Content mass: <color=#58F6AE><<1>></color>");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<CostType> InventoryContentCostTxt = new Message<CostType>(
      "",
      defaultTemplate: "Content cost: <color=#58F6AE><<1>></color>");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<VolumeLType> AvailableVolumeTxt = new Message<VolumeLType>(
      "",
      defaultTemplate: "Available volume: <color=yellow><<1>></color>");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<VolumeLType> MaxVolumeTxt = new Message<VolumeLType>(
      "",
      defaultTemplate: "Maximum volume: <color=#58F6AE><<1>></color>");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message NoSlotsReasonText = new Message(
      "",
      defaultTemplate: "No enough compatible slots",
      description: "Error message that is presented when parts cannot be added to the inventory"
          + " due to there are not enough compatible/empty slots available.");

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
    if (unityWindow == null) {
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
  /// <seealso cref="NoSlotsReasonText"/>
  public const string NoSlotsReason = "NoSlots";
  #endregion

  #region Event static configs
  public static readonly Event TakeSlotEvent = Event.KeyboardEvent("mouse0");
  public static readonly Event TakeOneItemEvent = Event.KeyboardEvent("&mouse0");
  public static readonly Event TakeOneItemModifierEvent = Event.KeyboardEvent("LeftAlt");
  public static readonly Event TakeTenItemsEvent = Event.KeyboardEvent("#mouse0");
  public static readonly Event TakeTenItemsModifierEvent = Event.KeyboardEvent("LeftShift");
  public static readonly Event AddItemsIntoStackEvent = Event.KeyboardEvent("mouse0");
  public static readonly Event DropIntoSlotEvent = Event.KeyboardEvent("mouse0");
  #endregion

  #region Local fields and properties.
  /// <summary>Inventory window that is opened at the moment.</summary>
  UIKISInventoryWindow unityWindow;

  /// <summary>Inventory slots.</summary>
  /// <remarks>NSome or all slots may not be represented in the UI.</remarks>
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
  UIKISInventoryTooltip.Tooltip currentTooltip => unityWindow.currentTooltip;
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
    _slotWithPointerFocus.UpdateTooltip(unityWindow.currentTooltip);
  }

  /// <inheritdoc/>
  public bool OnKISDrag(bool pointerMoved) {
    if (_slotWithPointerFocus != _dragSourceSlot) {
      UpdateDraggingStateTooltip();
    } else {
      _slotWithPointerFocus.UpdateTooltip(unityWindow.currentTooltip);  
    }
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
    if (Event.current.Equals(Event.KeyboardEvent("1")) && unityWindow != null) {
      AddFuelParts(1, 1, 3);
    }
    if (Event.current.Equals(Event.KeyboardEvent("2")) && unityWindow != null) {
      AddFuelParts(0.1f, 0.5f);
    }
    if (Event.current.Equals(Event.KeyboardEvent("3")) && unityWindow != null) {
      AddFuelParts(0.5f, 0.5f);
    }
    if (Event.current.Equals(Event.KeyboardEvent("4")) && unityWindow != null) {
      AddFuelParts(1, 1);
    }
    if (Event.current.Equals(Event.KeyboardEvent("5")) && unityWindow != null) {
      AddFuelParts(0.1f, 0.5f, num: 3, same: true);
    }
    if (Event.current.Equals(Event.KeyboardEvent("6")) && unityWindow != null
        && _inventorySlots.Count > 0 && _inventorySlots[0].slotItems.Length > 0) {
      DeleteItems(_inventorySlots[0].slotItems);
    }
  }
  #endregion

  #region KISContainerBase overrides
  /// <inheritdoc/>
  public override InventoryItem[] AddParts(AvailablePart[] avParts, ConfigNode[] nodes) {
    // Ignore the base method implementation.
    // FIXME: get back to the approach of adding to base.
    var newItemsList = new List<InventoryItem>();
    for (var i = 0; i < avParts.Length; i++) {
      var avPart = avParts[i];
      var itemConfig = nodes[i];
      var slot = FindSlotForPart(avPart, itemConfig);
      if (slot == null) {
        ArrangeSlots(); // Make compaction and tray again.
        slot = FindSlotForPart(avPart, itemConfig, addInvisibleSlot: true);
      }
      var item = new InventoryItemImpl(this, avPart, itemConfig);
      AddItemsToSlot(new InventoryItem[] {item}, slot);
      newItemsList.Add(item);
    }
    return newItemsList.ToArray();
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
        slot.UpdateTooltip(unityWindow.currentTooltip);
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
            guiString = NoSlotsReasonText,
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

  void AddPartByName(string partName) {
    var avPart = PartLoader.getPartInfoByName(partName);
    if (avPart == null) {
      DebugEx.Error("*** bummer: no part {0}", partName);
      return;
    }
    AddParts(new[] {avPart}, new ConfigNode[1]);
  }
  #endregion

  #region Local utility methods
  /// <summary>Opens the inventory window.</summary>
  void OpenInventoryWindow() {
    if (unityWindow != null) {
      return; // Nothing to do.
    }
    HostedDebugLog.Fine(this, "Creating inventory window");
    unityWindow = UnityPrefabController.CreateInstance<UIKISInventoryWindow>(
        "KISInventoryDialog", UIMasterController.Instance.actionCanvas.transform);

    // TODO(ihsoft): Fix it in the prefab via TMPro.
    if (!UIMasterController.Instance.actionCanvas.pixelPerfect) {
      DebugEx.Warning("WORKAROUND: Enabling PerfectPixel mode on the root UI canvas");
      UIMasterController.Instance.actionCanvas.pixelPerfect = true;
    }

    unityWindow.gameObject.AddComponent<UIScalableWindowController>();
    unityWindow.onSlotHover.Add(OnSlotHover);
    unityWindow.onSlotClick.Add(OnSlotClick);
    unityWindow.onSlotAction.Add(OnSlotAction);
    unityWindow.onNewGridSize.Add(OnNewGridSize);
    unityWindow.onGridSizeChanged.Add(OnGridSizeChanged);

    var gridSize = persistedGridSize;
    if (gridSize.x < minGridSize.x) {
      gridSize.x = minGridSize.x;
    }
    if (gridSize.y < minGridSize.y) {
      gridSize.y = minGridSize.y;
    }
    persistedGridSize = gridSize;
    unityWindow.title = DialogTitle.Format(part.partInfo.title);
    unityWindow.minSize = minGridSize;
    unityWindow.maxSize = maxGridSize;
    unityWindow.SetGridSize(persistedGridSize);
    UpdateInventoryWindow();
  }

  /// <summary>Destroys the inventory window.</summary>
  void CloseInventoryWindow() {
    if (unityWindow == null) {
      return;
    }
    HostedDebugLog.Fine(this, "Destroying inventory window");
    Hierarchy.SafeDestory(unityWindow);
    unityWindow = null;
    // Immediately make all slots invisible. Don't relay on Unity cleanup routines.  
    _inventorySlots.ForEach(x => x.BindTo(null));
  } 

  /// <summary>Updates stats in the open inventory window.</summary>
  /// <remarks>It's safe to call it when the inventory window is not open.</remarks>
  void UpdateInventoryWindow() {
    if (unityWindow == null) {
      return;
    }
    var text = new List<string> {
        InventoryContentMassTxt.Format(contentMass),
        InventoryContentCostTxt.Format(contentCost),
        MaxVolumeTxt.Format(maxVolume),
        AvailableVolumeTxt.Format(Math.Max(maxVolume - usedVolume, 0))
    };
    unityWindow.mainStats = string.Join("\n", text);
  }

  /// <summary>Check if inventory has enough visible slots to accomodate the items.</summary>
  bool CheckHasVisibleSlots(IReadOnlyList<AvailablePart> avParts, IReadOnlyList<ConfigNode> nodes) {
    var newSlots = new HashSet<InventorySlotImpl>();
    for (var i = 0; i < avParts.Count; ++i) {
      var avPart = avParts[i];
      var node = nodes[i] ?? KISAPI.PartNodeUtils.PartSnapshot(avPart.partPrefab);
      var slot = FindSlotForPart(avPart, node, preferredSlots: newSlots);
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
  /// This method tries to find a best slot, so that the inventory is kept as dense as possible. By
  /// default it only considers the slots that are visible in UI, but it can be overwritten.
  /// </remarks>
  /// <param name="avPart">The part to find a slot for.</param>
  /// <param name="node">The parts config node.</param>
  /// <param name="preferredSlots">
  /// If set, then these slots will be checked for best fit first. The preferred slots can be
  /// invisible.
  /// </param>
  /// <param name="addInvisibleSlot">
  /// If <c>true</c>, then a new invisible slot will be created in the inventory in case of no
  /// compatible visible slot was found.
  /// </param>
  /// <returns>The available slot or <c>null</c> if none found.</returns>
  /// <seealso cref="InventorySlotImpl.unitySlot"/>
  InventorySlotImpl FindSlotForPart(AvailablePart avPart, ConfigNode node,
                                    IEnumerable<InventorySlotImpl> preferredSlots = null,
                                    bool addInvisibleSlot = false) {
    InventorySlotImpl slot;
    var matchItems = new InventoryItem[] {
        new InventoryItemImpl(this, avPart, node)
    };
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
    UpdateItems(addItems: addItems);
    slot.AddItems(addItems);
    Array.ForEach(addItems, x => _itemToSlotMap.Add(x, slot));
  }

  /// <summary>Fills or updates the hints section of the slot tooltip.</summary>
  /// <remarks>
  /// The hints may depend on the current game state (like the keyboard keys pressed), so it's
  /// assumed that this method may be called every frame when the interactive mode is ON.
  /// </remarks>
  /// <seealso cref="currentTooltip"/>
  /// <seealso cref="UIKISInventoryTooltip.Tooltip.showHints"/>
  void UpdateHints() {
    var hints = new List<string>();
    if (KISAPI.ItemDragController.isDragging && _slotWithPointerFocus != _dragSourceSlot) {
      if (_slotWithPointerFocus.isEmpty) {
        hints.Add(StoreItemsHintText.Format(DropIntoSlotEvent));
      } else if (_canAcceptDraggedItems) {
        hints.Add(AddItemsHintText.Format(AddItemsIntoStackEvent));
      }
    } else if (!_slotWithPointerFocus.isEmpty) {
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
    currentTooltip.hints = hints.Count > 0 ? string.Join("\n", hints) : null;
  }

  /// <summary>
  /// Updates tooltip when mouse pointer is hovering over the slot AND the dragging mode is
  /// started.
  /// </summary>
  void UpdateDraggingStateTooltip() {
    currentTooltip.ClearInfoFileds();
    if (_slotWithPointerFocus.isEmpty) {
      currentTooltip.title = StoreItemsTooltipText;
      currentTooltip.baseInfo.text = null;
    } else {
      if (_canAcceptDraggedItems) {
        currentTooltip.title = AddItemsTooltipText;
        currentTooltip.baseInfo.text =
            AddItemsCountHintText.Format(KISAPI.ItemDragController.leasedItems.Length);
      } else {
        currentTooltip.title = CannotAddItemsTooltipText;
        if (_canAcceptDraggedItemsCheckResult != null) {
          currentTooltip.baseInfo.text = string.Join(
              "\n",
              _canAcceptDraggedItemsCheckResult
                  .Where(r => r.guiString != null)
                  .Select(r => r.guiString));
        }
      }
    }
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
        && EventChecker.CheckClickEvent(AddItemsIntoStackEvent, button);
    InventoryItem[] consumedItems = null;
    if (storeItems || stackItems) {
      //FIXME: eligibility has already been checked
      if (_slotWithPointerFocus.CheckCanAddItems(
          KISAPI.ItemDragController.leasedItems, logErrors: true) == null) {
        consumedItems = KISAPI.ItemDragController.ConsumeItems();
      }
      if (consumedItems != null) {
        AddItemsToSlot(consumedItems, _slotWithPointerFocus);
      }
    }
    if (consumedItems == null) {
      UISoundPlayer.instance.Play(KISAPI.CommonConfig.sndPathBipWrong);
      HostedDebugLog.Error(
          this, "Cannot store/stack dragged items to slot: draggedItems={0}",
          KISAPI.ItemDragController.leasedItems.Length);
    }
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
    var visibleSlots = unityWindow.slots.Length;
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
    for (var i = _inventorySlots.Count; i < unityWindow.slots.Length; ++i) {
      _inventorySlots.Add(new InventorySlotImpl(null));
    }

    // Align logical slots with the visible slots in UI.
    for (var i = 0; i < _inventorySlots.Count; ++i) {
      var slot = _inventorySlots[i];
      var newUnitySlot = i < unityWindow.slots.Length
          ? unityWindow.slots[i]
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
    UpdateInventoryStats(new InventoryItem[0]);

    // Restore the tooltip callbacks if needed.
    if (unityWindow.hoveredSlot != null) {
      RegisterSlotHoverCallback();
    }
  }

  /// <summary>Updates tooltip hints in every frame to catch the keyboard actions.</summary>
  /// <remarks>
  /// This coroutine is expected to be scheduled on the tooltip object. When it dies, so does this
  /// coroutine.
  /// </remarks>
  // ReSharper disable once MemberCanBeMadeStatic.Local
  // NOTE: Nope, this method cannot be static.
  IEnumerator UpdateHoveredHints(InventorySlotImpl inventorySlot) {
    if (!UIKISInventoryTooltip.Tooltip.showHints) {
      yield break;  // No hints, no tracking.
    }
    while (true) {  // The coroutine will die with the tooltip.
      UpdateHints();
      yield return null;
    }
  }

  /// <summary>Destroys tooltip and stops any active logic on the UI slot.</summary>
  void UnregisterSlotHoverCallback() {
    KISAPI.ItemDragController.UnregisterTarget(this);
    unityWindow.DestroySlotTooltip();
    _slotWithPointerFocus = null;
  }

  /// <summary>Establishes a tooltip and starts the active logic on a UI slot.</summary>
  void RegisterSlotHoverCallback() {
    _slotWithPointerFocus =
        _inventorySlots.FirstOrDefault(x => x.IsBoundTo(unityWindow.hoveredSlot));
    if (_slotWithPointerFocus == null) {
      HostedDebugLog.Error(this, "Expected to get a hovered slot, but none was found");
      return;
    }
    unityWindow.StartSlotTooltip();
    unityWindow.currentTooltip.StartCoroutine(UpdateHoveredHints(_slotWithPointerFocus));
    _slotWithPointerFocus.UpdateTooltip(unityWindow.currentTooltip);
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
  #endregion
}

}  // namespace
