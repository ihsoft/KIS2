// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSP.UI;
using System.Collections.Generic;
using System;
using System.Linq;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using KSPDev.PrefabUtils;
using KSPDev.Unity;
using KISAPIv2;
using KIS2.UIKISInventorySlot;
using KIS2.GUIUtils;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {

public sealed class KisContainerWithSlots : KisContainerBase,
    IHasGUI, IKisDragTarget {

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

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> TakeStackHint = new Message<KeyboardEventType>(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Grab whole stack",
      description: "Hint text in the inventory slot tooltip that tells what action"
      + " user should do to take the whole slot from the inventory and add it into the currently"
      + " dragged pack.\n"
      + " The <<1>> argument is a user friendly action name.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> TakeOneItemHint = new Message<KeyboardEventType>(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Grab <color=#5a5>1</color> item",
      description: "Hint text in the inventory slot tooltip that tells what action"
      + " user should do to add one item from the inventory slot into the currently dragged pack.\n"
      + " The <<1>> argument is a user friendly action name.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> TakeTenItemsHint = new Message<KeyboardEventType>(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Grab <color=#5a5>10</color> items",
      description: "Hint text in the inventory slot tooltip that tells what action"
      + " user should do to add 10 items from the inventory slot into the currently dragged pack.\n"
      + " The <<1>> argument is a user friendly action name.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> AddOneItemHint = new Message<KeyboardEventType>(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Add <color=#5a5>1</color> item",
      description: "Editor mode. Hint text in the inventory slot tooltip that tells what action"
      + " user should do to add one item to the target slot.\n"
      + " The <<1>> argument is a user friendly action name.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> AddTenItemsHint = new Message<KeyboardEventType>(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Add <color=#5a5>10</color> items",
      description: "Editor mode. Hint text in the inventory slot tooltip that tells what action"
      + " user should do to add 10 items to the target slot.\n"
      + " The <<1>> argument is a user friendly action name.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> RemoveOneItemHint = new Message<KeyboardEventType>(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Remove <color=#5a5>1</color> item",
      description: "Editor mode. Hint text in the inventory slot tooltip that tells what action"
      + " user should do to remove one item from the target slot.\n"
      + " The <<1>> argument is a user friendly action name.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> RemoveTenItemsHint = new Message<KeyboardEventType>(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Remove <color=#5a5>10</color> items",
      description: "Editor mode. Hint text in the inventory slot tooltip that tells what action"
      + " user should do to remove 10 items from the target slot.\n"
      + " The <<1>> argument is a user friendly action name.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> SpawnNewItemHint = new Message<KeyboardEventType>(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Spawn a new item",
      description: "Builder mode. Hint text in the inventory slot tooltip that tells what action"
      + " user should do to open a dialog to spawn new item in the inventory slot.\n"
      + " The <<1>> argument is a user friendly action name.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> SpawnExtraItemHint = new Message<KeyboardEventType>(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Spawn <color=#5a5>1</color> extra item",
      description: "Builder mode. Hint text in the inventory slot tooltip that tells what action"
      + " user should do to add one extra item to the slot that already has an item.\n"
      + " The <<1>> argument is a user friendly action name.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> DropOneItemHint = new Message<KeyboardEventType>(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Drop <color=#5a5>1</color> item",
      description: "Builder mode. Hint text in the inventory slot tooltip that tells what action"
      + " user should do to remove one item from the slot that has some items.\n"
      + " The <<1>> argument is a user friendly action name.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message StoreIntoSlotActionTooltip = new Message(
      "",
      defaultTemplate: "Store items into slot",
      description: "The text to show in the title of the slot tooltip when the dragged items can be"
          + " stored into an empty slot.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message<KeyboardEventType> StoreIntoSlotActionHint = new Message<KeyboardEventType>(
      "",
      defaultTemplate: "<b><color=#5a5><<1>></color></b> to store items into the slot",
      description: "Hint text in the inventory slot tooltip that tells what action"
      + " user should do to add the dragged items into an empty slot.\n"
      + " The <<1>> argument is a user friendly action name.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message<int> StoreIntoSlotCountHint = new Message<int>(
      "",
      defaultTemplate: "Store <color=#5a5><<1>></color> items",
      description: "The text to show in the inventory tooltip that tells how many items will be"
      + " added into an empty slot in case of the action has completed.\n"
      + " The <<1>> argument is the number of items being added.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message AddToStackActionTooltip = new Message(
      "",
      defaultTemplate: "Add items to stack",
      description: "The text to show in the title of the slot tooltip when the dragged items can be"
          + " added into a non-empty slot.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message<KeyboardEventType> AddToStackActionHint = new Message<KeyboardEventType>(
      "",
      defaultTemplate: "<b><color=#5a5><<1>></color></b> to add items to stack",
      description: "Hint text in the inventory slot tooltip that tells what action"
          + " user should do to add the dragged items into a non-empty slot.\n"
          + " The <<1>> argument is a user friendly action name.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message<int> AddToStackCountHint = new Message<int>(
      "",
      defaultTemplate: "Add <color=#5a5><<1>></color> items",
      description: "The text to show in the inventory tooltip that tells how many items will be"
      + " added into a non-empty slot in case of the action has completed.\n"
      + " The <<1>> argument is the number of items being added.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message CannotStoreIntoSlotTooltipText = new Message(
      "",
      defaultTemplate: "Cannot store items into slot",
      description: "The text to show in the title of the slot tooltip when the dragged items can"
      + " NOT be added into the slot. Only shown when the target slot is empty.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message CannotAddToStackTooltipText = new Message(
      "",
      defaultTemplate: "Cannot add items to stack",
      description: "The title to show in the slot tooltip when the dragged items ca NOT be added into the slot. Only"
      + " shown when the target slot is not empty.");
  #endregion

  #region Part's config fields
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Vector2 minGridSize = new Vector2(3, 1);
  
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Vector2 maxGridSize = new Vector2(16, 9);
  #endregion

  #region Part's persistant fields
  /// <summary>Current width of the slots grid.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField(isPersistant = true)]
  public int slotGridWidth;

  /// <summary>Current height of the slots grid.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField(isPersistant = true)]
  public int slotGridHeight;
  #endregion

  #region GUI menu action handlers
  [KSPEvent(guiActive = true, guiActiveUnfocused = true, guiActiveUncommand = true, guiActiveEditor = true)]
  [LocalizableItem(
      tag = "#01",
      defaultTemplate = "KISv2: Inventory",
      description = "A context menu event that opens GUI for the inventory.")]
  public void ShowInventory() {
    if (!isGuiOpen) {
      OpenInventoryWindow();
    } else {
      CloseInventoryWindow();
    }
  }
  #endregion

  #region Event static configs
  static readonly Event TakeSlotEvent = Event.KeyboardEvent("&mouse0");
  static readonly Event TakeOneItemEvent = Event.KeyboardEvent("mouse0");
  static readonly Event TakeTenItemsEvent = Event.KeyboardEvent("#mouse0");
  static readonly Event AddToStackEvent = Event.KeyboardEvent("mouse0");
  static readonly Event StoreIntoSlotEvent = Event.KeyboardEvent("mouse0");
  static readonly Event AddOneItemEvent = Event.KeyboardEvent("^mouse0");
  static readonly Event AddTenItemsEvent = Event.KeyboardEvent("#mouse0");
  static readonly Event RemoveOneItemEvent = Event.KeyboardEvent("^mouse1");
  static readonly Event RemoveTenItemsEvent = Event.KeyboardEvent("#mouse1");
  static readonly Event SpawnNewItemEvent = Event.KeyboardEvent("mouse1");
  static readonly Event SpawnExtraItemEvent = Event.KeyboardEvent("mouse1");
  static readonly Event DropOneItemEvent = Event.KeyboardEvent("&mouse1");
  #endregion

  #region Persistent node names
  /// <summary>
  /// Name of the config value that holds a mapping between a slot and the KIS inventory item ID. 
  /// </summary>
  /// <remarks>The syntax is: &lt;slot-index&gt;-&lt;item-guid&gt;</remarks>
  const string PersistentConfigKisStockSlotMapping = "itemToKisSlotMapping";
  #endregion

  #region APi fileds and properties
  /// <summary>Tells if there is a GUI opened for this inventory.</summary>
  /// <value><c>true</c> if the dialog is opened.</value>
  public bool isGuiOpen => _unityWindow != null;
  #endregion

  /// <summary>Action states for the pointer, hovering over an inventory slot.</summary>
  /// <remarks>Used in the action state machine to simplify actions/hints handling.</remarks>
  /// <seealso cref="_slotEventsHandler"/>
  enum SlotActionMode {
    /// <summary>No slots focused.</summary>
    NothingFocused,

    /// <summary>
    /// Dragging mode is in action and the pointer hovers over an empty, and it's not the slot that
    /// started the dragging action.
    /// </summary>
    DraggingOverEmptyTargetSlot,

    /// <summary>
    /// Dragging mode is in action and the pointer hovers over a slot with items, and it's not the
    /// slot that started the dragging action.
    /// </summary>
    DraggingOverItemsTargetSlot,

    /// <summary>
    /// Dragging mode is in action and the pointer hovers over the slot that started the dragging
    /// action (not empty).
    /// </summary>
    DraggingOverSourceSlot,

    /// <summary>Pointer hovers over an empty slot.</summary>
    HoveringOverEmptySlot,

    /// <summary>Pointer hovers over a slot with items.</summary>
    HoveringOverItemsSlot,
  }

  /// <summary>
  /// State machine that controls what input actions are active with regard to the slot drag/drop
  /// operations.
  /// </summary>
  readonly EventsHandlerStateMachine<SlotActionMode> _slotEventsHandler = new();
      
  #region Local fields, constants, and properties.
  /// <summary>Inventory window that is opened at the moment.</summary>
  UiKisInventoryWindow _unityWindow;

  /// <summary>Inventory slots.</summary>
  /// <remarks>Some or all slots may not be represented in the UI.</remarks>
  /// <seealso cref="InventorySlotImpl.isVisible"/>
  readonly List<InventorySlotImpl> _inventorySlots = new();

  /// <summary>Index that resolves item to the slot that contains it.</summary>
  readonly Dictionary<string, InventorySlotImpl> _itemToSlotMap = new();

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

  /// <summary>Last known position at the dialog close. It will be restored at open.</summary>
  Vector3? _screenPosition;
  #endregion

  #region IKisDragTarget implementation
  /// <inheritdoc/>
  public void OnKisDragStart() {
    UpdateTooltip();
  }

  /// <inheritdoc/>
  public void OnKisDragEnd(bool isCancelled) {
    UpdateTooltip();
  }

  /// <inheritdoc/>
  public bool OnKisDrag(bool pointerMoved) {
    return _canAcceptDraggedItems;
  }

  /// <inheritdoc/>
  public void OnFocusTarget(GameObject newTarget) {
  }
  #endregion

  #region Unity events
  void Update() {
    if (isGuiOpen && Input.anyKeyDown && Time.timeScale > float.Epsilon) {
      _slotEventsHandler.HandleActions();
    }
  }
  #endregion

  #region AbstractPartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) {
      return; // Nothing to do.
    }

    useGUILayout = false;

    // SlotActionMode.HoveringOverEmptySlot
    if (HighLogic.LoadedSceneIsFlight) {
      _slotEventsHandler.DefineAction(
          SlotActionMode.HoveringOverEmptySlot,
          SpawnNewItemHint, SpawnNewItemEvent, SpawnNewItemInFocusedSlot,
          checkIfAvailable: () => KisApi.CommonConfig.builderModeEnabled);
    }

    // SlotActionMode.HoveringOverItemsSlot
    // The order of actions definition defines how the hints will be ordered.
    // FIXME: Add a sorting parameter to workaround it.
    _slotEventsHandler.DefineAction(
        SlotActionMode.HoveringOverItemsSlot,
        TakeOneItemHint, TakeOneItemEvent, () => TakeItemsFromFocusedSlot(1));
    _slotEventsHandler.DefineAction(
        SlotActionMode.HoveringOverItemsSlot,
        TakeStackHint, TakeSlotEvent, () => TakeItemsFromFocusedSlot(int.MaxValue));
    if (HighLogic.LoadedSceneIsFlight) {
      _slotEventsHandler.DefineAction(
          SlotActionMode.HoveringOverItemsSlot,
          TakeTenItemsHint, TakeTenItemsEvent, () => TakeItemsFromFocusedSlot(10));
      _slotEventsHandler.DefineAction(
          SlotActionMode.HoveringOverItemsSlot,
          SpawnExtraItemHint, SpawnExtraItemEvent, () => UpdateItemsCountInFocusedSlot(1),
          checkIfAvailable: () => KisApi.CommonConfig.builderModeEnabled);
      _slotEventsHandler.DefineAction(
          SlotActionMode.HoveringOverItemsSlot,
          DropOneItemHint, DropOneItemEvent, () => UpdateItemsCountInFocusedSlot(-1),
          checkIfAvailable: () => KisApi.CommonConfig.builderModeEnabled);
    } else if (HighLogic.LoadedSceneIsEditor) {
      _slotEventsHandler.DefineAction(
          SlotActionMode.HoveringOverItemsSlot,
          AddOneItemHint, AddOneItemEvent, () => UpdateItemsCountInFocusedSlot(1));
      _slotEventsHandler.DefineAction(
          SlotActionMode.HoveringOverItemsSlot,
          AddTenItemsHint, AddTenItemsEvent, () => UpdateItemsCountInFocusedSlot(10));
      _slotEventsHandler.DefineAction(
          SlotActionMode.HoveringOverItemsSlot,
          RemoveOneItemHint, RemoveOneItemEvent, () => UpdateItemsCountInFocusedSlot(-1));
      _slotEventsHandler.DefineAction(
          SlotActionMode.HoveringOverItemsSlot,
          RemoveTenItemsHint, RemoveTenItemsEvent, () => UpdateItemsCountInFocusedSlot(-10));
    }

    // SlotActionMode.DraggingOverEmptyTargetSlot
    _slotEventsHandler.DefineAction(
        SlotActionMode.DraggingOverEmptyTargetSlot,
        StoreIntoSlotActionHint, StoreIntoSlotEvent, AddDraggedItemsToFocusedSlot,
        checkIfAvailable: () => _canAcceptDraggedItems);

    // SlotActionMode.DraggingOverItemsTargetSlot
    _slotEventsHandler.DefineAction(
        SlotActionMode.DraggingOverItemsTargetSlot,
        AddToStackActionHint, AddToStackEvent, AddDraggedItemsToFocusedSlot,
        checkIfAvailable: () => _canAcceptDraggedItems);

    // Dragging mode is in action and pointer hovers over a slot with items, and it's the slot
    // that started the dragging action.
    _slotEventsHandler.DefineAction(
        SlotActionMode.DraggingOverSourceSlot,
        TakeStackHint, TakeSlotEvent, () => TakeItemsFromFocusedSlot(int.MaxValue));
    _slotEventsHandler.DefineAction(
        SlotActionMode.DraggingOverSourceSlot,
        TakeOneItemHint, TakeOneItemEvent, () => TakeItemsFromFocusedSlot(1));
    _slotEventsHandler.DefineAction(
        SlotActionMode.DraggingOverSourceSlot,
        TakeTenItemsHint, TakeTenItemsEvent, () => TakeItemsFromFocusedSlot(10));
  }

  /// <inheritdoc/>
  public override void OnDestroy() {
    CloseInventoryWindow();
    base.OnDestroy();
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    // First, make all the slots to allow the default allocation logic to work smoothly.
    for (var i = 0; i < slotGridWidth * slotGridHeight; i++) {
      _inventorySlots.Add(new InventorySlotImpl(null));
    }
    var slotMapping = new Dictionary<string, int>();
    var savedMappings = node.GetValues(PersistentConfigKisStockSlotMapping);
    foreach (var savedMapping in savedMappings) {
      var pair = savedMapping.Split(new[] {'-'}, 2);
      var slotIndex = int.Parse(pair[0]);
      slotMapping[pair[1]] = slotIndex;
      if (slotIndex >= _inventorySlots.Count) {
        HostedDebugLog.Warning(this, "Found slot beyond capacity: {0}", slotIndex);
        while (_inventorySlots.Count <= slotIndex) {
          _inventorySlots.Add(new InventorySlotImpl(null));
        }
      }
    }
    base.OnLoad(node);

    // Reorganize slots allocation of all the items based on the save state.
    RemoveItemsFromSlots(inventoryItems);
    foreach (var mapping in slotMapping) {
      var item = FindItem(mapping.Key);
      if (item != null) {
        AddSlotItem(_inventorySlots[mapping.Value], item);
      }
    }

    // Handle out of sync items to ensure every item is assigned to a slot.
    var itemsWithNoSlot = inventoryItems.Where(x => !_itemToSlotMap.ContainsKey(x.itemId));
    foreach (var item in itemsWithNoSlot) {
      HostedDebugLog.Warning(this, "Loading non-slot item: {0}", item.itemId);
      AddSlotItem(FindSlotForItem(item, addInvisibleSlot: true), item);
    }
  }

  /// <inheritdoc/>
  public override void OnSave(ConfigNode node) {
    base.OnSave(node);
    for (var i = 0; i < _inventorySlots.Count; i++) {
      Array.ForEach(
          _inventorySlots[i].slotItems, x => node.AddValue(PersistentConfigKisStockSlotMapping, i + "-" + x.itemId));
    }
  }
  #endregion

  #region DEBUG: IHasGUI implementation
  public void OnGUI() {
    // TODO(ihsoft): Drop this debug code.
    if (!isGuiOpen || !KisApi.CommonConfig.alphaFlagEnableSamples) {
      return;
    }
    if (Event.current.Equals(Event.KeyboardEvent("&1"))) {
      AddFuelParts(1, 1, num: 3);
    }
    if (Event.current.Equals(Event.KeyboardEvent("&2"))) {
      AddFuelParts(0.1f, 0.5f);
    }
    if (Event.current.Equals(Event.KeyboardEvent("&3"))) {
      AddFuelParts(0.5f, 0.5f);
    }
    if (Event.current.Equals(Event.KeyboardEvent("&4"))) {
      AddFuelParts(1, 1);
    }
    if (Event.current.Equals(Event.KeyboardEvent("&5"))) {
      AddFuelParts(0.1f, 0.5f, num: 3, same: true);
    }
  }
  #endregion

  #region KISContainerBase overrides
  /// <inheritdoc/>
  public override List<ErrorReason> CheckCanAddPart(string partName, ConfigNode node = null, bool logErrors = false) {
    var errors = base.CheckCanAddPart(partName, node, logErrors);
    if (errors.Count> 0) {
      return errors;  // Don't go deeper, it's already failed.
    }
    var slot = FindSlotForItem(InventoryItemImpl.ForPartName(this, partName, node));
    if (slot == null) {
      errors.Add(new ErrorReason() {
          shortString = StockInventoryLimitReason,
          guiString = StockContainerLimitReachedErrorText,
      });
    }
    if (logErrors && errors.Count > 0) {
      HostedDebugLog.Error(this, "Cannot add '{0}' part.\nERRORS:{1}\nPART NODE:\n{2}",
                           partName, DbgFormatter.C2S(errors, separator: "\n"), node);
    }
    return errors;
  }
  
  /// <inheritdoc/>
  public override void UpdateInventoryStats(InventoryItem[] changedItems) {
    base.UpdateInventoryStats(changedItems);
    UpdateInventoryWindow();
  }

  /// <inheritdoc/>
  protected override void AddInventoryItem(InventoryItem item) {
    base.AddInventoryItem(item);
    if (!_itemToSlotMap.ContainsKey(item.itemId)) {
      AddSlotItem(FindSlotForItem(item, addInvisibleSlot: true), item);
    } else {
      HostedDebugLog.Warning(this, "Skip update for existing mapping: itemId={0}", item.itemId);
    }
  }

  /// <inheritdoc/>
  protected override void RemoveInventoryItem(InventoryItem removeItem) {
    base.RemoveInventoryItem(removeItem);
    RemoveSlotItem(_itemToSlotMap[removeItem.itemId], removeItem);
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
    AvailablePart avPart = null;
    for (var i = 0; i < num; ++i) {
      if (avPart == null || !same) {
        var avPartIndex = (int) (UnityEngine.Random.value * _fuelParts.Length);
        avPart = PartLoader.getPartInfoByName(_fuelParts[avPartIndex]);
      }
      var node = KisApi.PartNodeUtils.PartSnapshot(avPart.partPrefab);
      foreach (var res in KisApi.PartNodeUtils.GetResources(node)) {
        var amount = UnityEngine.Random.Range(minPct, maxPct) * res.maxAmount;
        KisApi.PartNodeUtils.UpdateResource(node, res.resourceName, amount);
      }
      //FIXME: check volume and size
      if (AddPart(avPart.name, node) == null) {
        HostedDebugLog.Warning(this, "DEBUG: cannot add part '{0}':\n:{1}, ", avPart.name, node);
      }
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

  /// <summary>Callback that is called when the slots grid is trying to resize.</summary>
  Vector2 OnNewGridSize(Vector2 newSize) {
    newSize = new Vector3(
        Mathf.Min(Mathf.Max(newSize.x, minGridSize.x), maxGridSize.x),
        Mathf.Min(Mathf.Max(newSize.y, minGridSize.y), maxGridSize.y),
        0);
    var minSlotsNeeded = _inventorySlots.Count(x => !x.isEmpty);
    if (minSlotsNeeded > newSize.x * newSize.y) {
      newSize = new Vector2(slotGridWidth, slotGridHeight);
    }
    return newSize;
  }

  /// <summary>Callback when the slots grid size has changed.</summary>
  void OnGridSizeChanged() {
    ArrangeSlots();  // Trigger compaction if there are invisible items.
    UIDialogsGridController.ArrangeDialogs();
  }

  /// <summary>Callback when pointers enters or leaves the dialog.</summary>
  void OnEditorDialogHover(bool isHovered) {
    KisApi.ItemDragController.SetFocusedTarget(isHovered ? _unityWindow.gameObject : null);
  }
  #endregion

  #region Local utility methods
  /// <summary>Opens the inventory window.</summary>
  void OpenInventoryWindow() {
    if (isGuiOpen) {
      return; // Nothing to do.
    }
    HostedDebugLog.Fine(this, "Creating inventory window");
    _unityWindow = UnityPrefabController.CreateInstance<UiKisInventoryWindow>(
        "KISInventoryDialog", UIMasterController.Instance.actionCanvas.transform);

    // TODO(ihsoft): Fix it in the prefab via TMPro.
    if (!UIMasterController.Instance.actionCanvas.pixelPerfect) {
      DebugEx.Warning("WORKAROUND: Enabling PerfectPixel mode on the root UI canvas");
      UIMasterController.Instance.actionCanvas.pixelPerfect = true;
    }

    _unityWindow.gameObject.AddComponent<UIScalableWindowController>();
    _unityWindow.onSlotHover.Add(OnSlotHover);
    _unityWindow.onNewGridSize.Add(OnNewGridSize);
    _unityWindow.onGridSizeChanged.Add(OnGridSizeChanged);
    _unityWindow.onDialogClose.Add(CloseInventoryWindow);
    _unityWindow.onDialogHover.Add(OnEditorDialogHover);

    _unityWindow.title = DialogTitle.Format(part.partInfo.title);
    _unityWindow.minSize = minGridSize;
    _unityWindow.maxSize = maxGridSize;
    _unityWindow.SetGridSize(new Vector3(slotGridWidth, slotGridHeight, 0));
    ArrangeSlots(); // Ensure all slots have UI binding.
    UpdateInventoryWindow();

    UIDialogsGridController.AddDialog(_unityWindow);
    if (_screenPosition == null) {
      HostedDebugLog.Fine(this, "Set calculated window position: {0}", _unityWindow.mainRect.position);
    } else {
      HostedDebugLog.Fine(this, "Restore window position: {0}", _screenPosition);
      _unityWindow.mainRect.localPosition = _screenPosition.Value;
      // TODO(ihsoft): Listen for "ControlUpdated" message to detect state change in flights/editor.
      var dragWindow = _unityWindow.gameObject.GetComponent<UiWindowDragControllerScript>();
      if (dragWindow != null) {
        dragWindow.positionChanged = true;
      }
    }
    _unityWindow.SendMessage(
        nameof(IKspDevUnityControlChanged.ControlUpdated), _unityWindow.gameObject,
        SendMessageOptions.DontRequireReceiver);
  }

  /// <summary>Destroys the inventory window.</summary>
  void CloseInventoryWindow() {
    if (!isGuiOpen) {
      return;
    }
    HostedDebugLog.Fine(this, "Destroying inventory window");
    var dragWindow = _unityWindow.gameObject.GetComponent<UiWindowDragControllerScript>();
    if (dragWindow == null || dragWindow.positionChanged) {
      _screenPosition = _unityWindow.mainRect.localPosition;
    }
    if (_dragSourceSlot != null) {
      // TODO(ihsoft): Better, disable the close button when there are items in the dragging state.
      HostedDebugLog.Fine(this, "Cancel dragging items");
      KisApi.ItemDragController.CancelItemsLease();
    }
    UIDialogsGridController.RemoveDialog(_unityWindow);

    Hierarchy.SafeDestroy(_unityWindow);
    _unityWindow = null;
    // Immediately make all slots invisible. Don't rely on Unity cleanup routines.  
    _inventorySlots.ForEach(x => x.BindTo(null));
  }

  /// <summary>Updates stats in the open inventory window.</summary>
  /// <remarks>It's safe to call it when the inventory window is not open.</remarks>
  void UpdateInventoryWindow() {
    if (!isGuiOpen) {
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
      slot = preferredSlots.FirstOrDefault(x => x.CheckCanAddItems(matchItems).Count == 0);
      if (slot != null) {
        return slot;
      }
    }
    // Then, try to store the item into an existing slot to save slots space.
    slot = _inventorySlots.FirstOrDefault(x => !x.isEmpty && x.CheckCanAddItems(matchItems).Count == 0);
    if (slot != null) {
      return slot;
    }
    // Then, pick a first empty slot.
    slot = _inventorySlots.FirstOrDefault(s => s.isEmpty);
    if (slot != null) {
      return slot;
    }
    if (!addInvisibleSlot) {
      return null;
    }
    // Finally, create a new invisible slot to fit the items.
    HostedDebugLog.Warning(this, "Adding an invisible slot: slotIdx={0}", _inventorySlots.Count);
    slot = new InventorySlotImpl(null);
    _inventorySlots.Add(slot);
    return slot;
  }

  /// <summary>Triggers when items from the slot are consumed by the target.</summary>
  /// <remarks>
  /// This method may detect a failing condition. If it happens, the state must stay unchanged.
  /// </remarks>
  /// <seealso cref="_dragSourceSlot"/>
  bool ConsumeSlotItems() {
    var leasedItems = KisApi.ItemDragController.leasedItems;
    // Here we don't expect failures. The caller has to ensure the items can be consumed.
    foreach (var item in leasedItems) {
      item.SetLocked(false);
      if (!DeleteItem(item)) {
        HostedDebugLog.Error(this, "Cannot consume item: part={0}, itemId={1}", item.avPart.name, item.itemId);
        item.SetLocked(true); // Just to restore its original state.
      }
    }
    SetDraggedSlot(null);
    return true;
  }

  /// <summary>Triggers when dragging items from this slot has been canceled.</summary>
  /// <remarks>This method never fails.</remarks>
  void CancelSlotLeasedItems() {
    Array.ForEach(KisApi.ItemDragController.leasedItems, i => i.SetLocked(false));
    SetDraggedSlot(null);
  }

  /// <summary>
  /// Ensures that each UI slot has a corresponded inventory slot. Also, updates and optimizes the
  /// inventory slots that are not currently present in UI. 
  /// </summary>
  /// <remarks>
  /// This method must be called each time the inventory or unity slots number is changed. It
  /// implements a tricky logic that tries to adapt to the inventory GUI size change, and adjusts
  /// the  slots so that the least number of the visible slots change their positions.
  /// </remarks>
  void ArrangeSlots() {
    if (!isGuiOpen) {
      return; // This method is UI bound. It needs a dialog instance.
    }
    var newSlotGridWidth = (int) _unityWindow.gridSize.x;
    var newSlotGridHeight = (int) _unityWindow.gridSize.y;

    // Visible slots order may change, it would invalidate the tooltip.
    if (_slotWithPointerFocus != null) {
      UnregisterSlotHoverCallback();
    }

    // Make a grid from the flat slots array.
    // Expand with empty slots at the right and bottom as needed.
    var slotGrid = new InventorySlotImpl[
        Math.Max(newSlotGridWidth, slotGridWidth),
        Math.Max(newSlotGridHeight, slotGridHeight)];
    for (var i = 0; i < slotGrid.GetLength(0); i++) {
      for (var j = 0; j < slotGrid.GetLength(1); j++) {
        if (i >= slotGridWidth || j >= slotGridHeight || j * slotGridWidth + i >= _inventorySlots.Count) {
          slotGrid[i, j] = new InventorySlotImpl(null);
        } else {
          slotGrid[i, j] = _inventorySlots[j * slotGridWidth + i];
        }
      }
    }
    var invisibleSlots = _inventorySlots
        .Skip(slotGridWidth * slotGridHeight)
        .ToList();

    // Make sure all the slot references are detached from the GUI. The code below may completely
    // change the slots layout.
    _inventorySlots.ForEach(x => x.BindTo(null));

    // Remove lines in a user friendly manner. The items from the bottom will be pulled up.
    if (newSlotGridHeight < slotGridHeight) {
      var slotsToDelete = slotGridHeight - newSlotGridHeight;
      for (var col = 0; col < slotGrid.GetLength(0); col++) {
        var slotsDeleted = 0;
        for (var row = slotGrid.GetLength(1) - 1;
             row >= 0 && slotsDeleted < slotsToDelete;
             row--) {
          var shiftRow = row;
          while (shiftRow >= 0 && !slotGrid[col, shiftRow].isEmpty) {
            shiftRow--;
          }
          if (shiftRow >= 0) {
            for (var i = shiftRow; i < row; i++) {
              slotGrid[col, i] = slotGrid[col, i + 1];
            }
          } else {
            invisibleSlots.Add(slotGrid[col, row]);
          }
          slotGrid[col, row] = null;
          slotsDeleted++;
        }
      }
    }

    // Remove columns in a user friendly manner. The items from the right will be shifted to the
    // left.
    if (newSlotGridWidth < slotGridWidth) {
      var slotsToDelete = slotGridWidth - newSlotGridWidth;
      for (var row = 0; row < slotGrid.GetLength(1); row++) {
        var slotsDeleted = 0;
        for (var col = slotGrid.GetLength(0) - 1;
             col >= 0 && slotsDeleted < slotsToDelete;
             col--) {
          var shiftCol = col;
          while (shiftCol >= 0 && !slotGrid[shiftCol, row].isEmpty) {
            shiftCol--;
          }
          if (shiftCol >= 0) {
            for (var i = shiftCol; i < col; i++) {
              slotGrid[i, row] = slotGrid[i + 1, row];
            }
          } else {
            invisibleSlots.Add(slotGrid[col, row]);
          }
          slotGrid[col, row] = null;
          slotsDeleted++;
        }
      }
    }

    // Flatten the grid. Total number of non-null slots must be exactly the new size.
    _inventorySlots.Clear();
    for (var row = 0; row < slotGrid.GetLength(1); row++) {
      for (var col = 0; col < slotGrid.GetLength(0); col++) {
        if (slotGrid[col, row] != null) {
          _inventorySlots.Add(slotGrid[col, row]);
        }
      }
    }
    slotGridWidth = newSlotGridWidth;
    slotGridHeight = newSlotGridHeight;
    if (_inventorySlots.Count != slotGridWidth * slotGridHeight) {
      HostedDebugLog.Error(this, "Arrange slots logic is broken: hasSlots{0}, needSlots={1}",
                           _inventorySlots.Count, slotGridWidth * slotGridHeight);
    }

    // Try to fit the invisible slots in the existing empty slots (if any).
    for (var i = 0; i < _inventorySlots.Count && invisibleSlots.Count > 0; i++) {
      if (_inventorySlots[i].isEmpty) {
        _inventorySlots[i] = invisibleSlots[invisibleSlots.Count - 1];
        invisibleSlots.RemoveAt(invisibleSlots.Count - 1);
      }
    }

    // Anything that rest, adds at the tail. It's a bad condition.
    foreach (var invisibleSlot in invisibleSlots) {
      HostedDebugLog.Warning(this, "Hidden slot in inventory: part={0}, count={1}",
                             invisibleSlot.slotItems[0].avPart.name, invisibleSlot.slotItems.Length);
      _inventorySlots.Add(invisibleSlot);
    }

    // Bind inventory and Unity slots so that a one-to-one relation is maintained.
    for (var i = 0; i < _unityWindow.slots.Length; i++) {
      _inventorySlots[i].BindTo(_unityWindow.slots[i]);
    }

    // Restore the tooltip callbacks if needed.
    if (_unityWindow.hoveredSlot != null) {
      RegisterSlotHoverCallback();
    }
  }

  /// <summary>Destroys tooltip and stops any active logic on the UI slot.</summary>
  void UnregisterSlotHoverCallback() {
    KisApi.ItemDragController.UnregisterTarget(this);
    _unityWindow.DestroySlotTooltip();
    _slotWithPointerFocus = null;
    _slotEventsHandler.SetState(SlotActionMode.NothingFocused);
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
    KisApi.ItemDragController.RegisterTarget(this);
    var isDragging = KisApi.ItemDragController.isDragging;
    var isSourceSlotFocused = _dragSourceSlot != null && _dragSourceSlot == _slotWithPointerFocus;
    var isEmptySlotFocused = _slotWithPointerFocus.slotItems.Length == 0;
    if (isDragging && isSourceSlotFocused) {
      _slotEventsHandler.SetState(SlotActionMode.DraggingOverSourceSlot);
    } else if (isDragging) {
      _slotEventsHandler.SetState(
          isEmptySlotFocused
              ? SlotActionMode.DraggingOverEmptyTargetSlot
              : SlotActionMode.DraggingOverItemsTargetSlot);
    } else {
      _slotEventsHandler.SetState(
          isEmptySlotFocused
              ? SlotActionMode.HoveringOverEmptySlot
              : SlotActionMode.HoveringOverItemsSlot);
    }
    UpdateTooltip();
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
    CheckCanAcceptDrops();
    if (_slotWithPointerFocus == null || currentTooltip == null) {
      return;
    }
    currentTooltip.ClearInfoFields();
    var cannotAddReasonText = "";
    if (_canAcceptDraggedItemsCheckResult != null) {
      cannotAddReasonText = string.Join(
          "\n",
          _canAcceptDraggedItemsCheckResult
              .Where(r => r.guiString != null)
              .Select(r => r.guiString));
    }
    switch (_slotEventsHandler.currentState) {
      case SlotActionMode.HoveringOverItemsSlot:
        _slotWithPointerFocus.UpdateTooltip(_unityWindow.currentTooltip);
        break;
      case SlotActionMode.DraggingOverEmptyTargetSlot:
        if (_canAcceptDraggedItems) {
          currentTooltip.title = StoreIntoSlotActionTooltip;
          currentTooltip.baseInfo.text = StoreIntoSlotCountHint.Format(KisApi.ItemDragController.leasedItems.Length);
        } else {
          currentTooltip.title = CannotStoreIntoSlotTooltipText;
          currentTooltip.baseInfo.text = cannotAddReasonText;
        }
        break;
      case SlotActionMode.DraggingOverItemsTargetSlot:
        if (_canAcceptDraggedItems) {
          currentTooltip.title = AddToStackActionTooltip;
          currentTooltip.baseInfo.text = AddToStackCountHint.Format(KisApi.ItemDragController.leasedItems.Length);
        } else {
          currentTooltip.title = CannotAddToStackTooltipText;
          currentTooltip.baseInfo.text = cannotAddReasonText;
        }
        break;
    }
    currentTooltip.hints = _slotEventsHandler.GetHints();
    //FIXME: move to unity
    currentTooltip.gameObject.SetActive(
        !string.IsNullOrEmpty(currentTooltip.hints)
        || !string.IsNullOrEmpty(currentTooltip.title) 
        || !string.IsNullOrEmpty(currentTooltip.baseInfo.text));
  }

  /// <summary>Verifies if the currently dragged items can be stored into the hovered slot.</summary>
  /// <seealso cref="_canAcceptDraggedItemsCheckResult"/>
  /// <seealso cref="_canAcceptDraggedItems"/>
  void CheckCanAcceptDrops() {
    if (KisApi.ItemDragController.isDragging && _slotWithPointerFocus != null) {
      var allItems = KisApi.ItemDragController.leasedItems;
      var checkResult = _slotWithPointerFocus.CheckCanAddItems(allItems);
      if (checkResult.Count == 0) {
        // For the items from other inventories also check the basic constraints.
        foreach (var item in allItems) {
          if (!ReferenceEquals(item.inventory, this)) {
            checkResult.AddRange(CheckCanAddPart(item.avPart.name, node: item.itemConfig));
          } 
        }
      }

      // De-dup the errors by the short string to not spam on multiple items.
      _canAcceptDraggedItemsCheckResult = checkResult.Count > 0
          ? checkResult
              .GroupBy(p => p.shortString, StringComparer.OrdinalIgnoreCase)
              .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
              .Values
              .ToArray()
          : null;
      _canAcceptDraggedItems = _canAcceptDraggedItemsCheckResult == null;
    } else {
      _canAcceptDraggedItemsCheckResult = null;
      _canAcceptDraggedItems = false;
    }
  }

  /// <summary>Adds the item into the slot.</summary>
  /// <param name="slot">The slot to add the item to.</param>
  /// <param name="item">The item to add.</param>
  void AddSlotItem(InventorySlotImpl slot, InventoryItem item) {
    slot.AddItems(new[] { item });
    _itemToSlotMap.Add(item.itemId, slot);
    if (slot == _slotWithPointerFocus) {
      UpdateTooltip();
    }
  }

  /// <summary>Removes the item from slot.</summary>
  /// <param name="slot">The slot to remove the item from.</param>
  /// <param name="item">The item to remove.</param>
  void RemoveSlotItem(InventorySlotImpl slot, InventoryItem item) {
    slot.DeleteItems(new[] { item });
    _itemToSlotMap.Remove(item.itemId);
    ArrangeSlots(); // Make de-frag in case of there are invisible slots. 
    if (slot == _slotWithPointerFocus) {
      UpdateTooltip();
    }
  }

  /// <summary>Moves a set of items to teh specified slot.</summary>
  /// <remarks>
  /// All items must belong to this inventory. There will be no checking done to figure if the items can be placed into
  /// the same slot, the caller is responsible to do this check.
  /// </remarks>
  /// <param name="targetSlot">The slot to put the items into.</param>
  /// <param name="items">The items to put into the slot. They all must be the valid items of this inventory!</param>
  void MoveItemsToSlot(InventorySlotImpl targetSlot, params InventoryItem[] items) {
    RemoveItemsFromSlots(items)
        .ToList()
        .ForEach(x => AddSlotItem(targetSlot, x));
  }

  /// <summary>Invokes a GUI dialog that allows choosing a part to be spawned in the slot.</summary>
  /// <remarks>It's an action, invoked from the slot events state machine.</remarks>
  /// <seealso cref="_slotEventsHandler"/>
  void SpawnNewItemInFocusedSlot() {
    //FIXME
    DebugEx.Warning("*** spawn new item2");
  }

  /// <summary>Adds or removes items to the hovered slot in the edit or constructor mode.</summary>
  /// <remarks>All new items will have the config from the first item in the slot.</remarks>
  /// <param name="delta">
  /// The delta value to add. If it's negative, then the items are removed. It's OK to request removal of more items
  /// than exist in the slot. In this case, all the items will be removed and the slot released.
  /// </param>
  /// <seealso cref="_slotEventsHandler"/>
  void UpdateItemsCountInFocusedSlot(int delta) {
    HostedDebugLog.Fine(this, "Update items count in slot: slot=#{0}, delta={1}",
                        _inventorySlots.IndexOf(_slotWithPointerFocus), delta);
    if (delta > 0) {
      var checkResult = new List<ErrorReason>();
      for (var i = 0; i < delta; i++) {
        var avPart = _slotWithPointerFocus.slotItems[0].avPart;
        var itemConfig = _slotWithPointerFocus.slotItems[0].itemConfig;
        var itemErrors = CheckCanAddPart(avPart.name, itemConfig);
        if (itemErrors.Count > 0) {
          checkResult.AddRange(itemErrors);
          continue;
        }
        var newItem = AddPart(avPart.name, itemConfig); // It will get added to a random slot.
        MoveItemsToSlot(_slotWithPointerFocus, newItem); // Move the item to the the specific slot.
      }
      checkResult = checkResult
          .GroupBy(p => p.guiString, StringComparer.OrdinalIgnoreCase)
          .Select(g => g.First())
          .ToList();
      if (checkResult.Count > 0) {
        UISoundPlayer.instance.Play(KisApi.CommonConfig.sndPathBipWrong);
        var errorMsg = checkResult
            .Select(x => x.guiString)
            .Where(x => !string.IsNullOrEmpty(x))
            .ToArray();
        if (errorMsg.Length > 0) {
          ScreenMessaging.ShowPriorityScreenMessage(
              ScreenMessaging.SetColorToRichText(string.Join("\n", errorMsg), ScreenMessaging.ErrorColor));
        }
      }
    } else {
      for (var i = _slotWithPointerFocus.slotItems.Length - 1; i >= 0 && delta < 0; i--) {
        var item = _slotWithPointerFocus.slotItems[i];
        if (!item.isLocked) {
          if (DeleteItem(item)) {
            ++delta;
          } else {
            HostedDebugLog.Error(this, "Cannot delete item: itemPart={0}, itemId={1}", item.avPart.name, item.itemId);
          }
        }
      }
    }
    UpdateTooltip();
  }

  /// <summary>Takes items from the focused slot and starts dragging operation.</summary>
  /// <param name="num">
  /// The number of items to take from the slot. It's OK to request more items than exist in the
  /// slot, all the items will be taken in this case.
  /// </param>
  /// <remarks>It's an action, invoked from the slot events state machine.</remarks>
  /// <seealso cref="_slotEventsHandler"/>
  void TakeItemsFromFocusedSlot(int num) {
    var itemsToDrag = _slotWithPointerFocus.slotItems
        .Where(i => !i.isLocked)
        .Take(num)
        .ToArray();
    HostedDebugLog.Fine(
        this, "Take items from slot: slot=#{0}, requested={1}, got={2}",
        _inventorySlots.IndexOf(_slotWithPointerFocus), num < int.MaxValue ? num : "<all>", itemsToDrag.Length);
    if (itemsToDrag.Length == 0) {
      return;
    }

    // Either add or set the grabbed items.
    if (KisApi.ItemDragController.isDragging) {
      itemsToDrag = KisApi.ItemDragController.leasedItems.Concat(itemsToDrag).ToArray();
      KisApi.ItemDragController.CancelItemsLease();
    }
    Array.ForEach(itemsToDrag, i => i.SetLocked(true));
    SetDraggedSlot(_slotWithPointerFocus, itemsToDrag.Length);
    KisApi.ItemDragController.LeaseItems(
        _slotWithPointerFocus.iconImage, itemsToDrag, ConsumeSlotItems, CancelSlotLeasedItems);
    var dragIconObj = KisApi.ItemDragController.dragIconObj;
    dragIconObj.hasScience = _slotWithPointerFocus.hasScience;
    dragIconObj.stackSize = KisApi.ItemDragController.leasedItems.Length;
    dragIconObj.resourceStatus = _slotWithPointerFocus.resourceStatus;
    UIPartActionController.Instance.partInventory.PlayPartSelectedSFX();
  }

  /// <summary>Consumes the dragged items and adds them into the hovered slot.</summary>
  /// <remarks>It's an action, invoked from the slot events state machine.</remarks>
  /// <seealso cref="_slotEventsHandler"/>
  void AddDraggedItemsToFocusedSlot() {
    var consumedItems = KisApi.ItemDragController.ConsumeItems();
    if (consumedItems != null) {
      HostedDebugLog.Info(this, "Add items to slot: slot=#{0}, num={1}",
                          _inventorySlots.IndexOf(_slotWithPointerFocus), consumedItems.Length);
      _slotEventsHandler.SetState(SlotActionMode.HoveringOverItemsSlot);
      var newItems = new List<InventoryItem>();
      foreach (var consumedItem in consumedItems) {
        var item = AddItem(consumedItem);
        if (item != null) {
          newItems.Add(item);
        } else {
          HostedDebugLog.Error(
              this, "Cannot add dragged item: part={0}, itemId={1}", consumedItem.avPart.name, consumedItem.itemId);
        }
      }
      MoveItemsToSlot(_slotWithPointerFocus, newItems.ToArray());
      UIPartActionController.Instance.partInventory.PlayPartDroppedSFX();
    } else {
      UISoundPlayer.instance.Play(KisApi.CommonConfig.sndPathBipWrong);
      HostedDebugLog.Error(
          this, "Cannot store/stack dragged items to slot: slot=#{0}, draggedItems={1}",
          _inventorySlots.IndexOf(_slotWithPointerFocus), KisApi.ItemDragController.leasedItems.Length);
    }
  }

  /// <summary>Removes the items form their slots.</summary>
  /// <param name="items">The items to remove.</param>
  /// <returns>The items that were found and actually removed.</returns>
  InventoryItem[] RemoveItemsFromSlots(InventoryItem[] items) {
    var res = new List<InventoryItem>();
    foreach (var item in items) {
      if (!_itemToSlotMap.ContainsKey(item.itemId)) {
        HostedDebugLog.Warning(this, "Cannot find slot for item: itemId={0}", item.itemId);
        continue;
      }
      _itemToSlotMap[item.itemId].DeleteItems(new[] { item });
      _itemToSlotMap.Remove(item.itemId);
      res.Add(item);
    }

    return res.ToArray();
  }
  #endregion
}

}  // namespace
