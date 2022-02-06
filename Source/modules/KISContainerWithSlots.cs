// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSP.UI;
using System.Collections.Generic;
using System;
using System.Collections;
using System.Linq;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using KSPDev.PrefabUtils;
using KSPDev.Unity;
using KISAPIv2;
using KIS2.UIKISInventorySlot;
using KSPDev.ConfigUtils;
using KSPDev.InputUtils;
using KSPDev.PartUtils;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>
/// A GUI extension above the inventory system that allows grouping parts into slots of (almost) any size.
/// </summary>
/// <remarks>
/// Under the hood all items are kept in the regular KIS inventory, meaning their the state is persisted at the part
/// level. The parts with different but resource amounts can be placed into the same slot if the difference is not too
/// much.
/// </remarks>
[PersistentFieldsDatabase("KIS2/settings2/KISConfig")]
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
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: to store items into the slot",
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
      description: "The title to show in the slot tooltip when the dragged items can NOT be added into the slot. Only"
      + " shown when the target slot is not empty.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<MassType> MassTooltipText = new(
      "",
      defaultTemplate: "Mass: <b><<1>></b>",
      description: "A slot tooltip string that shows the mass of an item. It's used when the slot"
          + " has exactly one item.\n"
          + " The <<1>> argument is the item mass of type MassType.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message2/*"/>
  static readonly Message<MassType, MassType> MassMultipartTooltipText = new(
      "",
      defaultTemplate: "Mass: <b><<1>></b> (total: <b><<2>></b>)",
      description: "A slot tooltip string that shows both the mass of an item and the total"
          + " slot mass. It's used when the slot has more than one item.\n"
          + " The <<1>> argument is the item mass.\n"
          + " The <<2>> argument is the slot total mass.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<VolumeLType> VolumeTooltipText = new(
      "",
      defaultTemplate: "Volume: <b><<1>></b>",
      description: "A slot tooltip string that shows the volume of an item. It's used when the slot"
          + " has exactly one item.\n"
          + " The <<1>> argument is the item volume.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message2/*"/>
  static readonly Message<VolumeLType, VolumeLType> VolumeMultipartTooltipText = new(
      "",
      defaultTemplate: "Volume: <b><<1>></b> (total: <b><<2>></b>)",
      description: "A slot tooltip string that shows both the volume of an item and the total"
          + " slot volume. It's used when the slot has more than one item.\n"
          + " The <<1>> argument is the item volume.\n"
          + " The <<2>> argument is the slot total volume.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<CostType> CostTooltipText = new(
      "",
      defaultTemplate: "Cost: <b><<1>></b>",
      description: "A slot tooltip string that shows the cost of an item. It's used when the slot"
          + " has exactly one item.\n"
          + " The <<1>> argument is the item cost.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message2/*"/>
  static readonly Message<CostType, CostType> CostMultipartTooltipText = new(
      "",
      defaultTemplate: "Cost: <b><<1>></b> (total: <b><<2>></b>)",
      description: "A slot tooltip string that shows both the cost of an item and the total"
          + " slot cost. It's used when the slot has more than one item.\n"
      + " The <<1>> argument is the item cost.\n"
      + " The <<2>> argument is the slot total cost.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<string> VariantTooltipText = new(
      "",
      defaultTemplate: "Variant: <b><<1>></b>",
      description: "Name of the variant of the items in the slot tooltip. All items in the slot"
          + " have the same variant.\n"
          + " The <<1>> argument is a localized name of the variant.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message4/*"/>
  static readonly Message<ResourceType, CompactNumberType, CompactNumberType, CompactNumberType>
      ResourceMultipartSpecialValueText = new(
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
      ResourceMultipartValueText = new(
          "",
          defaultTemplate: "<<1>>: <b><<2>></b> / <b><<3>></b> (total: <b><<4>></b>)",
          description: "Resource status string in the slot tooltip when there are more than one"
          + " items the available in the slot and the resource reserve in all the items is at its"
          + " min or max value (i.e. it's exact).\n"
          + " The <<1>> argument is a localized name of the resource.\n"
          + " The <<2>> argument is the available amount of the resource per item.\n"
          + " The <<3>> argument is the maximum amount of the resource per item.\n"
          + " The <<4>> argument is the slot total reserve.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message3/*"/>
  static readonly Message<ResourceType, CompactNumberType, CompactNumberType> NormalResourceValueText = new(
      "",
      defaultTemplate: "<<1>>: <b><<2>></b> / <b><<3>></b>",
      description: "Resource status string in the slot tooltip when the available amount is at"
      + " the expected level (e.g. 'empty' for the ore tanks or 'full' for the fuel"
      + " ones).\n"
      + " The <<1>> argument is a localized name of the resource.\n"
      + " The <<2>> argument is the current amount of the resource.\n"
      + " The <<3>> argument is the maximum amount of the resource.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message3/*"/>
  static readonly Message<ResourceType, CompactNumberType, CompactNumberType> SpecialResourceValueText = new(
      "",
      defaultTemplate: "<<1>>: <b><color=yellow><<2>></color></b> / <b><<3>></b>",
      description: "Resource status string in the slot tooltip when the available amount is at"
      + " the level that is not normally expected (e.g. 'full' for the ore tanks or"
      + " 'empty' for"
      + " the fuel ones).\n"
      + " The <<1>> argument is a localized name of the resource.\n"
      + " The <<2>> argument is the current amount of the resource.\n"
      + " The <<3>> argument is the maximum amount of the resource.");
  #endregion

  #region Helper classes
  /// <summary>Helper Unity module to detect if the inventory dialog position has been changed by the user.</summary>
  /// <remarks>
  /// By default the dialogs are getting added to the "dialogs grid", which automatically manages the dialog positions.
  /// When user intentionally changes the dialog position, it gets removed from the grid control.  
  /// </remarks>
  sealed class UIWindowMoveTracker : MonoBehaviour, IKspDevUnityControlChanged {
    bool _isInTheGrid = true;
    public void ControlUpdated() {
      if (!_isInTheGrid) {
        return;
      }
      var dragCtrl = gameObject.GetComponent<UiWindowDragControllerScript>();
      if (dragCtrl != null && dragCtrl.positionChanged) {
        UIDialogsGridController.RemoveDialog(gameObject);
        _isInTheGrid = false;
      }
    }
  }
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

  #region Global settings
  /// <summary>The maximum size of the slot in the KIS inventory.</summary>
  /// <remarks>
  /// A high setting may impact the low-end machines: the dragging operation of the large slots may kill the game
  /// performance wise. A low setting will limit the KIS inventory usability: the inventory capacity may get
  /// significantly limited if there are many light wight or small volume items being stored.
  /// </remarks>
  [PersistentField("Performance/maxKisSlotSize")]
  public int maxKisSlotSize = 500; // The default is based on a high-end machine test.
  #endregion

  #region GUI menu action handlers
  // TODO(IgorZ): manage the unfocused range the same way as the old KIS does (with resect to the geometry).
  [KSPEvent(guiActive = true, guiActiveUnfocused = true, guiActiveUncommand = true, guiActiveEditor = true,
            unfocusedRange = 10)]
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
  static readonly ClickEvent TakeSlotEvent = new(Event.KeyboardEvent("&mouse0"));
  static readonly ClickEvent TakeOneItemEvent = new(Event.KeyboardEvent("mouse0"));
  static readonly ClickEvent TakeTenItemsEvent = new(Event.KeyboardEvent("#mouse0"));
  static readonly ClickEvent AddToStackEvent = new(Event.KeyboardEvent("mouse0"));
  static readonly ClickEvent StoreIntoSlotEvent = new(Event.KeyboardEvent("mouse0"));
  static readonly ClickEvent AddOneItemEvent = new(Event.KeyboardEvent("^mouse0"));
  static readonly ClickEvent AddTenItemsEvent = new(Event.KeyboardEvent("#mouse0"));
  static readonly ClickEvent RemoveOneItemEvent = new(Event.KeyboardEvent("^mouse1"));
  static readonly ClickEvent RemoveTenItemsEvent = new(Event.KeyboardEvent("#mouse1"));
  static readonly ClickEvent SpawnNewItemEvent = new(Event.KeyboardEvent("mouse1"));
  static readonly ClickEvent SpawnExtraItemEvent = new(Event.KeyboardEvent("mouse1"));
  static readonly ClickEvent DropOneItemEvent = new(Event.KeyboardEvent("&mouse1"));
  #endregion

  #region Persistent node names
  /// <summary>
  /// Name of the config value that holds a mapping between a KIS slot and the KIS inventory item ID. 
  /// </summary>
  /// <remarks>The syntax is: &lt;slot-index&gt;-&lt;item-guid&gt;</remarks>
  const string PersistentConfigKisSlotMapping = "itemToKisSlotMapping";
  #endregion

  #region API fileds and properties
  /// <summary>Tells if there is a GUI opened for this inventory.</summary>
  /// <value><c>true</c> if the dialog is opened.</value>
  public bool isGuiOpen => _unityWindow != null;
  #endregion

  #region Local fields, constants, and properties.
  /// <summary>Action states for the pointer, hovering over an inventory slot.</summary>
  /// <remarks>Used in the action state machine to simplify actions/hints handling.</remarks>
  /// <seealso cref="_slotEventsHandler"/>
  enum SlotActionMode {
    /// <summary>
    /// Dragging mode is in action and the pointer hovers over an empty, and it's not the slot that started the dragging
    /// action.
    /// </summary>
    DraggingOverEmptyTargetSlot,

    /// <summary>
    /// Dragging mode is in action and the pointer hovers over a slot with items, and it's not the slot that started the
    /// dragging action.
    /// </summary>
    DraggingOverItemsTargetSlot,

    /// <summary>
    /// Dragging mode is in action and the pointer hovers over the slot that started the dragging action (not empty).
    /// </summary>
    DraggingOverSourceSlot,

    /// <summary>Pointer hovers over an empty slot.</summary>
    HoveringOverEmptySlot,

    /// <summary>Pointer hovers over a slot with items.</summary>
    HoveringOverItemsSlot,
  }

  /// <summary>
  /// State machine that controls what input actions are active with regard to the slot drag/drop operations.
  /// </summary>
  readonly EventsHandlerStateMachine<SlotActionMode> _slotEventsHandler = new();

  /// <summary>Inventory window that is opened at the moment.</summary>
  UiKisInventoryWindow _unityWindow;

  /// <summary>Inventory slots.</summary>
  /// <remarks>Some or all slots may not be represented in the UI.</remarks>
  /// <seealso cref="InventorySlotImpl.isVisible"/>
  readonly List<InventorySlotImpl> _inventorySlots = new();

  /// <summary>Index that resolves item to the slot that contains it.</summary>
  readonly Dictionary<string, InventorySlotImpl> _itemToSlotMap = new();

  /// <summary>Slot that has initiated a drag action from this inventory.</summary>
  InventorySlotImpl _dragSourceSlot;

  /// <summary>A slot of this inventory that is currently has pointer focus.</summary>
  /// <remarks>This slot is the target for the pointer actions.</remarks>
  InventorySlotImpl slotWithPointerFocus {
    get => _slotWithPointerFocus;
    set {
      _slotWithPointerFocus = value;
      UpdateEventsHandlerState();
      _canAcceptDraggedItemsCheckResult = null;
      if (value != null) {
        _unityWindow.StartSlotTooltip();
        UpdateTooltip();
      } else {
        _unityWindow.DestroySlotTooltip();
      }
    }
  }
  InventorySlotImpl _slotWithPointerFocus;

  /// <summary>Tells if currently dragging items can fit into the currently hovered slot of this inventory.</summary>
  /// <seealso cref="slotWithPointerFocus"/>
  /// <seealso cref="canAcceptDraggedItemsCheckResult"/>
  bool canAcceptDraggedItems => canAcceptDraggedItemsCheckResult.Length == 0;

  /// <summary>The errors from the last hovered slot check.</summary>
  /// <seealso cref="canAcceptDraggedItems"/>
  ErrorReason[] canAcceptDraggedItemsCheckResult {
    get {
      if (_canAcceptDraggedItemsCheckResult == null) {
        CheckCanAcceptDrops();
      }
      return _canAcceptDraggedItemsCheckResult;
    }
  }
  ErrorReason[] _canAcceptDraggedItemsCheckResult;

  /// <summary>Shortcut to get the current tooltip.</summary>
  UIKISInventoryTooltip.Tooltip currentTooltip => _unityWindow.currentTooltip;
  #endregion

  #region IKisDragTarget implementation
  /// <inheritdoc/>
  public Component unityComponent => this;

  /// <inheritdoc/>
  public void OnKisDragStart() {
    UpdateEventsHandlerState();
  }

  /// <inheritdoc/>
  public void OnKisDragEnd(bool isCancelled) {
    UpdateEventsHandlerState();
  }

  /// <inheritdoc/>
  public bool OnKisDrag(bool pointerMoved) {
    return canAcceptDraggedItems;
  }

  /// <inheritdoc/>
  public void OnFocusTarget(IKisDragTarget newTarget) {
  }
  #endregion

  #region AbstractPartModule overrides
  /// <inheritdoc/>
  public override void OnStart(StartState state) {
    base.OnStart(state);

    useGUILayout = false;
    _slotEventsHandler.ONAfterTransition += (oldState, newState) => {
      UpdateTooltip();
    }; 

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
        checkIfAvailable: () => canAcceptDraggedItems);

    // SlotActionMode.DraggingOverItemsTargetSlot
    _slotEventsHandler.DefineAction(
        SlotActionMode.DraggingOverItemsTargetSlot,
        AddToStackActionHint, AddToStackEvent, AddDraggedItemsToFocusedSlot,
        checkIfAvailable: () => canAcceptDraggedItems);

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
    KisApi.ItemDragController.UnregisterTarget(this);
    base.OnDestroy();
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    // Prepare slots before loading.
    if (StockCompatibilitySettings.isCompatibilityMode) {
      var width = 3;
      var height = (stockInventoryModule.InventorySlots + width - 1)/ width; // Round up.
      if (width != slotGridWidth || height != slotGridHeight) {
        slotGridWidth = width;
        slotGridHeight = height;
        HostedDebugLog.Warning(this, "Rescale container to the stock size: width={0}, height={1}", width, height);
      }
    }
    for (var i = 0; i < slotGridWidth * slotGridHeight; i++) {
      _inventorySlots.Add(new InventorySlotImpl(this, null));
    }

    base.OnLoad(node);

    //FIXME: this can extend inventory slots

    var itemToKisSlotMap = StockCompatibilitySettings.isCompatibilityMode
        ? inventoryItems.Values.ToDictionary(k => k.itemId, v => v.stockSlotIndex)
        : node.GetValues(PersistentConfigKisSlotMapping)
            .AsEnumerable()
            .Select(x => x.Split(new[] { '-' }, 2))
            .ToDictionary(k => k[1], v => int.Parse(v[0]));

    // Drop the default layout and move the loaded items to their designated slots.
    foreach (var slot in _inventorySlots) {
      while (slot.slotItems.Count > 0) {
        RemoveSlotItem(slot, slot.slotItems[0]);
      }
    }
    foreach (var mapping in itemToKisSlotMap) {
      var item = FindItem(mapping.Key);
      var slotIndex = mapping.Value;
      if (item != null) {
        if (slotIndex >= _inventorySlots.Count) {
          HostedDebugLog.Warning(this, "Found slot beyond capacity: index={0}, addExtra={1}",
                                 slotIndex, _inventorySlots.Count - slotIndex);
          while (_inventorySlots.Count <= slotIndex) {
            _inventorySlots.Add(new InventorySlotImpl(this, null));
          }
        }
        AddSlotItem(_inventorySlots[slotIndex], item); // Place item to the proper slot.
      } else {
        HostedDebugLog.Warning(this, "Drop unknown item slot mapping: item={0}, slot={1}", mapping.Key, slotIndex);
      }
    }

    // Handle out of sync items to ensure every item is assigned to a slot.
    var itemsWithNoSlot = inventoryItems.Values.Where(x => !_itemToSlotMap.ContainsKey(x.itemId));
    foreach (var item in itemsWithNoSlot) {
      HostedDebugLog.Warning(this, "Loading non-slot item: {0}", item.itemId);
      AddSlotItem(FindSlotForItem(item, addInvisibleSlot: true), item);
    }
  }

  /// <inheritdoc/>
  public override void OnSave(ConfigNode node) {
    base.OnSave(node);
    for (var i = 0; i < _inventorySlots.Count; i++) {
      _inventorySlots[i].slotItems.ForEach(x => node.AddValue(PersistentConfigKisSlotMapping, i + "-" + x.itemId));
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
  public override List<ErrorReason> CheckCanAddItem(
      InventoryItem item, int stockSlotIndex = -1, bool logErrors = false) {
    var errors = base.CheckCanAddItem(item, stockSlotIndex, logErrors);
    if (errors.Count> 0) {
      return errors;  // Don't go deeper, it's already failed.
    }
    var slot = FindSlotForItem(item);
    if (slot == null) {
      errors.Add(new ErrorReason() {
          errorClass = InventoryConsistencyReason,
          guiString = NoMoreKisSlotsErrorText,
      });
    }
    return ReportAndReturnCheckErrors(item, errors, logErrors);
  }

  /// <inheritdoc/>
  /// FIXME: can be called at high FPS.
  public override void UpdateInventory(ICollection<InventoryItem> changedItems = null) {
    base.UpdateInventory(changedItems);
    ArrangeSlots(); // Optimize the KIS slots usage.
    UpdateInventoryWindow();
  }

  /// <inheritdoc/>
  protected override void AddInventoryItem(InventoryItem item) {
    base.AddInventoryItem(item);
    var slot = StockCompatibilitySettings.isCompatibilityMode
        ? _inventorySlots[item.stockSlotIndex]
        : FindSlotForItem(item, addInvisibleSlot: true);
    AddSlotItem(slot, item);
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
    string partName = null;
    for (var i = 0; i < num; ++i) {
      if (partName == null || !same) {
        var avPartIndex = (int) (UnityEngine.Random.value * _fuelParts.Length);
        partName = _fuelParts[avPartIndex];
      }
      var item = AddPart(partName);
      if (item != null) {
        DeleteItem(item);
        foreach (var resource in item.resources) {
          resource.amount = UnityEngine.Random.Range(minPct, maxPct) * resource.maxAmount;
        }
        if (item.variant != null) {
          var variantIndex = (int) (UnityEngine.Random.value * item.avPart.Variants.Count);
          item.snapshot.moduleVariantName = item.avPart.Variants[variantIndex].Name;
        }
        item.UpdateItem();
        AddItem(item);
        HostedDebugLog.Warning(
            this, "DEBUG: added part '{0}': resources={1}, variant={2}", partName,
            DbgFormatter.C2S(item.resources.Select(x => x.amount)), item.variantName);
      } else {
        HostedDebugLog.Warning(this, "DEBUG: cannot add part '{0}'", partName);
      }
    }
    UpdateInventory();
  }
  #endregion

  #region Unity window callbacks
  /// <summary>A callback that is called when pointer enters or leaves a UI slot.</summary>
  /// <param name="hoveredSlot">The Unity slot that is a source of the event.</param>
  /// <param name="isHover">Tells if pointer enters or leaves the UI element.</param>
  void OnSlotHover(Slot hoveredSlot, bool isHover) {
    if (isHover) {
      // The slot can receive the event before the owner dialog had. So, ensure the target is properly set.
      KisApi.ItemDragController.SetFocusedTarget(this);
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
    KisApi.ItemDragController.SetFocusedTarget(isHovered ? this : null);
  }
  #endregion

  #region Static API
  /// <summary>Fills tooltip with the info for the provided items.</summary>
  /// <remarks>If the slot is empty, then all info fields are erased.</remarks>
  public static void UpdateTooltip(UIKISInventoryTooltip.Tooltip tooltip, ICollection<InventoryItem> items) {
    tooltip.ClearInfoFields();
    if (items.Count == 0) {
      return;
    }
    if (items.Count == 1) {
      UpdateSingleItemTooltip(tooltip, items.First());
    } else {
      UpdateMultipleItemsTooltip(tooltip, items);
    }
    tooltip.UpdateLayout();
  }
  #endregion

  #region Shared local methods
  /// <summary>Makes the values that let groping items with different resources reserve.</summary>
  /// <remarks>
  /// <p>
  /// This method allocates the item to a "slot" on every resource which it may have. The slots are used to group the
  /// items. Two items can only be grouped together if their slots on every resource match exactly, even though each
  /// item may have different amount of the resource.
  /// </p>
  /// <p>
  /// Simple example. If two items of the same part have resource reserve of "less than 5%", then they can be grouped as
  /// "items that have less than 5% of the reserve". If any of them has more than 5% of the reserve, then they cannot be
  /// grouped. However, the exact logic of assigning the slots is undetermined.
  /// </p>
  /// </remarks>
  /// <seealso cref="GetResourceAmountSlot"/>
  internal static Dictionary<string, int> CalculateSimilarityValues(InventoryItem item) {
    return item.resources.ToDictionary(r => r.resourceName, r => GetResourceAmountSlot(r.amount / r.maxAmount));
  }

  /// <summary>Allocates the percentile into one of the 5 fixed slots.</summary>
  /// <remarks>The slots were chosen so that the grouping would make sense for the usual game cases.</remarks>
  internal static int GetResourceAmountSlot(double percent) {
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
  #endregion

  #region Local utility methods
  /// <summary>Fills tooltip with useful information about one item.</summary>
  static void UpdateSingleItemTooltip(UIKISInventoryTooltip.Tooltip tooltip, InventoryItem item) {
    tooltip.title = item.avPart.title;
    var infoLines = new List<string> {
        MassTooltipText.Format(item.fullMass),
        VolumeTooltipText.Format(item.volume),
        CostTooltipText.Format(item.fullCost)
    };

    // Basic stats.
    if (item.variant != null) {
      infoLines.Add(VariantTooltipText.Format(item.variant.DisplayName));
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
  static void UpdateMultipleItemsTooltip(UIKISInventoryTooltip.Tooltip tooltip, ICollection<InventoryItem> items,
                                         Dictionary<string, int> similarityValues = null) {
    var refItem = items.First();
    similarityValues ??= CalculateSimilarityValues(refItem);
    tooltip.title = refItem.avPart.title;

    // Basic stats.
    var infoLines = new List<string> {
        MassMultipartTooltipText.Format(refItem.fullMass, items.Sum(x => x.fullMass)),
        VolumeMultipartTooltipText.Format(refItem.volume, items.Sum(x => x.volume)),
        CostMultipartTooltipText.Format(refItem.fullCost, items.Sum(x => x.fullCost))
    };
    if (refItem.variant != null) {
      infoLines.Add(VariantTooltipText.Format(refItem.variant.DisplayName));
    }
    tooltip.baseInfo.text = string.Join("\n", infoLines);

    // Available resources stats.
    var resourceInfoLines = new List<string>();
    foreach (var resource in refItem.resources) {
      var amountSlot = similarityValues[resource.resourceName];
      var totalAmount = items.Sum(
          x => x.resources.First(r => r.resourceName == resource.resourceName).amount);
      if (amountSlot is 0 or 100) { // Show exact values with no highlighting.
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

  /// <summary>Opens the inventory window.</summary>
  void OpenInventoryWindow() {
    if (isGuiOpen) {
      return; // Nothing to do.
    }
    HostedDebugLog.Fine(this, "Creating inventory window");
    _unityWindow = UnityPrefabController.CreateInstance<UiKisInventoryWindow>(
        "KISInventoryDialog-" + part.persistentId, UIMasterController.Instance.actionCanvas.transform);

    _unityWindow.gameObject.AddComponent<UIScalableWindowController2>(); // Respect the game's UI scale settings.
    _unityWindow.gameObject.AddComponent<UIWindowMoveTracker>(); // Remove from the grid if the dialog has moved.
    _unityWindow.onSlotHover.Add(OnSlotHover);
    _unityWindow.onNewGridSize.Add(OnNewGridSize);
    _unityWindow.onDialogClose.Add(CloseInventoryWindow);
    _unityWindow.onDialogHover.Add(OnEditorDialogHover);

    _unityWindow.title = DialogTitle.Format(part.partInfo.title);

    if (!StockCompatibilitySettings.isCompatibilityMode) {
      _unityWindow.onGridSizeChanged.Add(OnGridSizeChanged);
      _unityWindow.minSize = minGridSize;
      _unityWindow.maxSize = maxGridSize;
    } else {
      _unityWindow.minSize = _unityWindow.maxSize = new Vector2(slotGridWidth, slotGridHeight);
      _unityWindow.isResizable = false;
    }
    _unityWindow.SetGridSize(new Vector3(slotGridWidth, slotGridHeight, 0));
    ArrangeSlots(); // Ensure all slots have UI binding.
    UpdateInventoryWindow();

    UIDialogsGridController.AddDialog(_unityWindow.gameObject);
    _unityWindow.SendMessage(
        nameof(IKspDevUnityControlChanged.ControlUpdated), _unityWindow.gameObject,
        SendMessageOptions.DontRequireReceiver);
    _unityWindow.StartCoroutine(GuiActionsHandler());
  }

  /// <summary>Destroys the inventory window.</summary>
  void CloseInventoryWindow() {
    if (!isGuiOpen) {
      return;
    }
    HostedDebugLog.Fine(this, "Destroying inventory window");
    if (_dragSourceSlot != null) {
      // TODO(ihsoft): Better, disable the close button when there are items in the dragging state.
      HostedDebugLog.Fine(this, "Cancel dragging items");
      KisApi.ItemDragController.CancelItemsLease();
    }
    UIDialogsGridController.RemoveDialog(_unityWindow.gameObject);

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
    UpdateEventsHandlerState();
  }

  /// <summary>Returns a slot where the item can be stored.</summary>
  /// <remarks>This method tries to find a best slot, so that the inventory is kept as dense as possible.</remarks>
  /// <param name="item">The item to find a slot for. It may belong to a different inventory.</param>
  /// <param name="preferredSlots">
  /// If set, then these slots will be checked for best fit first. The preferred slots can be invisible.
  /// </param>
  /// <param name="addInvisibleSlot">
  /// If TRUE, then a new invisible slot will be created in the inventory in case of no compatible slot was
  /// found.
  /// </param>
  /// <returns>The available slot or <c>null</c> if none found.</returns>
  /// <seealso cref="_inventorySlots"/>
  InventorySlotImpl FindSlotForItem(
      InventoryItem item, IEnumerable<InventorySlotImpl> preferredSlots = null, bool addInvisibleSlot = false) {
    InventorySlotImpl slot;
    var matchItems = new[] { item };
    // First, try to store the item into one of the preferred slots.
    if (preferredSlots != null) {
      slot = preferredSlots.FirstOrDefault(
          x => x.slotItems.Count < maxKisSlotSize && x.CheckCanAddItems(matchItems).Count == 0);
      if (slot != null) {
        return slot;
      }
    }
    // Then, try to store the item into an existing slot to save slots space.
    slot = _inventorySlots.FirstOrDefault(
        x => !x.isEmpty && x.slotItems.Count < maxKisSlotSize && x.CheckCanAddItems(matchItems).Count == 0);
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
    slot = new InventorySlotImpl(this, null);
    _inventorySlots.Add(slot);
    return slot;
  }

  /// <summary>Triggers when items from the slot are consumed by the target.</summary>
  /// <remarks>This method may detect a failing condition. If it happens, the state must stay unchanged.</remarks>
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
    UpdateInventory();
    return true;
  }

  /// <summary>Triggers when dragging items from this slot has been canceled.</summary>
  /// <remarks>This method never fails.</remarks>
  void CancelSlotLeasedItems() {
    Array.ForEach(KisApi.ItemDragController.leasedItems, i => i.SetLocked(false));
    SetDraggedSlot(null);
  }

  /// <summary>
  /// Ensures that each UI slot has a corresponded inventory slot. Also, updates and optimizes the inventory slots that
  /// are not currently present in UI. 
  /// </summary>
  /// <remarks>
  /// This method must be called each time the inventory or unity slots number is changed. It implements a tricky logic
  /// that tries to adapt to the inventory GUI size change, and adjusts the  slots so that the least number of the
  /// visible slots change their positions.
  /// </remarks>
  void ArrangeSlots() {
    if (!isGuiOpen) {
      return; // This method is UI bound. It needs a dialog instance.
    }
    var newSlotGridWidth = (int) _unityWindow.gridSize.x;
    var newSlotGridHeight = (int) _unityWindow.gridSize.y;

    // Visible slots order may change, it would invalidate the tooltip.
    if (slotWithPointerFocus != null) {
      UnregisterSlotHoverCallback();
    }

    // Make a grid from the flat slots array. Expand with empty slots at the right-bottom as needed.
    var slotGrid = new InventorySlotImpl[
        Math.Max(newSlotGridWidth, slotGridWidth),
        Math.Max(newSlotGridHeight, slotGridHeight)];
    for (var i = 0; i < slotGrid.GetLength(0); i++) {
      for (var j = 0; j < slotGrid.GetLength(1); j++) {
        if (i >= slotGridWidth || j >= slotGridHeight || j * slotGridWidth + i >= _inventorySlots.Count) {
          slotGrid[i, j] = new InventorySlotImpl(this, null);
        } else {
          slotGrid[i, j] = _inventorySlots[j * slotGridWidth + i];
        }
      }
    }
    var invisibleSlots = _inventorySlots
        .Skip(slotGridWidth * slotGridHeight)
        .ToList();

    // Make sure all the slot references are detached from the GUI. The code below may completely change the slots
    // layout.
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

    // Remove columns in a user friendly manner. The items from the right will be shifted to the left.
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
                             invisibleSlot.slotItems[0].avPart.name, invisibleSlot.slotItems.Count);
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
    slotWithPointerFocus = null;
  }

  /// <summary>Establishes a tooltip and starts the active logic on a UI slot.</summary>
  void RegisterSlotHoverCallback() {
    slotWithPointerFocus = _inventorySlots.FirstOrDefault(x => x.IsBoundTo(_unityWindow.hoveredSlot));
    if (slotWithPointerFocus == null) {
      HostedDebugLog.Error(
          this, "Expected to get a KIS hovered slot, but none was found: unitySlot={0}", _unityWindow.hoveredSlot);
      return;
    }
    KisApi.ItemDragController.RegisterTarget(this);
  }

  /// <summary>
  /// Verifies the current conditions and sets the appropriate state of the events handler state machine.
  /// </summary>
  /// <remarks>
  /// <p>
  /// This method only sets the state of the machine, which is a cheap operation if the state was the same. Than being
  /// said, it can be called as frequently as needed. Call it in any case that affects the inventory state.
  /// </p>
  /// <p>This method can be called at any time of the part's life. It has to handle all the cases.</p>
  /// </remarks>
  /// <seealso cref="_slotEventsHandler"/>
  void UpdateEventsHandlerState() {
    if (slotWithPointerFocus == null) {
      _slotEventsHandler.currentState = null;
      return;
    }
    var isDragging = KisApi.ItemDragController.isDragging;
    var isSourceSlotFocused = _dragSourceSlot == slotWithPointerFocus;
    var isEmptySlotFocused = slotWithPointerFocus.slotItems.Count == 0;
    SlotActionMode newState;
    if (isDragging) {
      if (isSourceSlotFocused) {
        newState = SlotActionMode.DraggingOverSourceSlot;
      } else {
        newState = isEmptySlotFocused
            ? SlotActionMode.DraggingOverEmptyTargetSlot
            : SlotActionMode.DraggingOverItemsTargetSlot;
      }
    } else {
      newState = isEmptySlotFocused ? SlotActionMode.HoveringOverEmptySlot : SlotActionMode.HoveringOverItemsSlot;
    }
    _slotEventsHandler.currentState = newState;
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
    UpdateEventsHandlerState();
  }

  /// <summary>Updates the currently focused slot (if any) with the relevant tooltip info.</summary>
  /// <remarks>
  /// This method needs to be called every time the slot content is changed in any way. Including the updates to the
  /// slot item configs. Note, that this methods is expensive and it's not advised to be invoked in every frame update.
  /// </remarks>
  void UpdateTooltip() {
    if (slotWithPointerFocus == null || currentTooltip == null) {
      return;
    }
    currentTooltip.ClearInfoFields();
    switch (_slotEventsHandler.currentState) {
      case SlotActionMode.DraggingOverSourceSlot:
      case SlotActionMode.HoveringOverItemsSlot:
        UpdateTooltip(_unityWindow.currentTooltip, slotWithPointerFocus.slotItems);
        break;
      case SlotActionMode.DraggingOverEmptyTargetSlot:
        if (canAcceptDraggedItems) {
          currentTooltip.title = StoreIntoSlotActionTooltip;
          currentTooltip.baseInfo.text = StoreIntoSlotCountHint.Format(KisApi.ItemDragController.leasedItems.Length);
        } else {
          currentTooltip.title = CannotStoreIntoSlotTooltipText;
          currentTooltip.baseInfo.text = MakeErrorReasonText();
        }
        break;
      case SlotActionMode.DraggingOverItemsTargetSlot:
        if (canAcceptDraggedItems) {
          currentTooltip.title = AddToStackActionTooltip;
          currentTooltip.baseInfo.text = AddToStackCountHint.Format(KisApi.ItemDragController.leasedItems.Length);
        } else {
          currentTooltip.title = CannotAddToStackTooltipText;
          currentTooltip.baseInfo.text = MakeErrorReasonText();
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

  /// <summary>Builds a string that describes why the items cannot be accepted by this inventory.</summary>
  /// <returns>A friendly "\n" separated string, or empty value if items can be accepted.</returns>
  string MakeErrorReasonText() {
    return string.Join(
        "\n",
        canAcceptDraggedItemsCheckResult
            .Where(r => r.guiString != null)
            .Select(r => r.guiString));
  }

  /// <summary>Verifies if the currently dragged items can be stored into the hovered slot.</summary>
  /// <seealso cref="canAcceptDraggedItemsCheckResult"/>
  /// <seealso cref="canAcceptDraggedItems"/>
  void CheckCanAcceptDrops() {
    if (!KisApi.ItemDragController.isDragging || slotWithPointerFocus == null) {
      _canAcceptDraggedItemsCheckResult = new ErrorReason[0];
      return;
    }

    var allItems = KisApi.ItemDragController.leasedItems;
    var checkResult = slotWithPointerFocus.CheckCanAddItems(allItems);

    if (checkResult.Count == 0 && StockCompatibilitySettings.isCompatibilityMode) {
      var stockSlotIndex = _inventorySlots.IndexOf(slotWithPointerFocus);
      checkResult = CheckSlotStockForItem(allItems[0], stockSlotIndex, quantity: allItems.Length);
    }

    if (checkResult.Count == 0) {
      // For the items from other inventories also check the basic constraints.
      var extraVolumeNeeded = 0.0;
      var breakCheckLoop = false;
      for (var i = 0; i < allItems.Length && !breakCheckLoop; i++) {
        var item = allItems[i];
        if (!ReferenceEquals(item.inventory, this)) {
          extraVolumeNeeded += item.volume;
        }
        foreach (var itemCheck in CheckCanAddItem(item)) {
          if (itemCheck.errorClass == InventoryConsistencyReason
              || itemCheck.errorClass == StockInventoryLimitReason
              || itemCheck.errorClass == KisNotImplementedReason) {
            // It's a fatal error. Stop all the other logic.
            checkResult.Add(itemCheck);
            breakCheckLoop = true;
            break;
          }
          // Skip a single item volume check since we have our own batch algo.
          if (itemCheck.errorClass != ItemVolumeTooLargeReason) {
            checkResult.Add(itemCheck);
          }
        }
      }
      if (!breakCheckLoop && extraVolumeNeeded > 0 && usedVolume + extraVolumeNeeded > maxVolume) {
        checkResult.Add(new ErrorReason() {
            errorClass = ItemVolumeTooLargeReason,
            guiString = NotEnoughVolumeText.Format(usedVolume + extraVolumeNeeded - maxVolume),
        });
      }
    }

    // De-dup the errors by the error class to not spam on multiple items.
    _canAcceptDraggedItemsCheckResult = checkResult
        .GroupBy(p => p.errorClass, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
        .Values
        .ToArray();
  }

  /// <summary>Adds the item into the slot.</summary>
  /// <param name="slot">The slot to add the item to.</param>
  /// <param name="item">The item to add.</param>
  void AddSlotItem(InventorySlotImpl slot, InventoryItem item) {
    slot.AddItem(item);
    _itemToSlotMap.Add(item.itemId, slot);
  }

  /// <summary>Removes the item from slot.</summary>
  /// <param name="slot">The slot to remove the item from.</param>
  /// <param name="item">The item to remove.</param>
  void RemoveSlotItem(InventorySlotImpl slot, InventoryItem item) {
    slot.DeleteItem(item);
    _itemToSlotMap.Remove(item.itemId);
  }

  /// <summary>Moves a set of items to the specified slot.</summary>
  /// <remarks>
  /// All items must belong to this inventory. There will be no checking done to figure if the items can be placed into
  /// the same slot, the caller is responsible to do this check.
  /// </remarks>
  /// <param name="targetSlot">The slot to put the items into.</param>
  /// <param name="item">The item to put into the slot. It must belong to this inventory!</param>
  void MoveItemsToSlot(InventorySlotImpl targetSlot, InventoryItem item) {
    if (!_itemToSlotMap.TryGetValue(item.itemId, out var slot)) {
      HostedDebugLog.Error(this, "Cannot find slot for item: itemId={0}", item.itemId);
      return;
    }
    RemoveSlotItem(slot, item);
    AddSlotItem(targetSlot, item);
  }

  /// <summary>Invokes a GUI dialog that allows choosing a part to be spawned in the slot.</summary>
  /// <remarks>It's an action, invoked from the slot events state machine.</remarks>
  /// <seealso cref="_slotEventsHandler"/>
  void SpawnNewItemInFocusedSlot() {
    SpawnItemDialogController.ShowDialog(this);
  }

  /// <summary>Adds or removes items to the hovered slot in the editor or constructor mode.</summary>
  /// <remarks>All new items will have the config from the first item in the slot.</remarks>
  /// <param name="requestedDelta">
  /// The delta value to add. If it's negative, then the items are removed. It's OK to request removal of more items
  /// than exist in the slot. In this case, all the items will be removed and the slot released.
  /// </param>
  /// <seealso cref="_slotEventsHandler"/>
  void UpdateItemsCountInFocusedSlot(int requestedDelta) {
    var actualDelta = requestedDelta;
    if (requestedDelta > 0 && slotWithPointerFocus.slotItems.Count + requestedDelta > maxKisSlotSize) {
      actualDelta = Math.Max(maxKisSlotSize - slotWithPointerFocus.slotItems.Count, 0);
    }
    HostedDebugLog.Fine(this, "Update items count in slot: slot=#{0}, requestedDelta={1}, actualDelta={2}",
                        _inventorySlots.IndexOf(slotWithPointerFocus), requestedDelta, actualDelta);
    if (actualDelta > 0) {
      var checkResult = new List<ErrorReason>();
      for (var i = 0; i < actualDelta; i++) {
        var itemErrors = CheckCanAddItem(slotWithPointerFocus.slotItems[0]);
        if (itemErrors.Count > 0) {
          checkResult.AddRange(itemErrors);
          continue;
        }
        var newItem = AddItem(slotWithPointerFocus.slotItems[0]); // It will get added to a random slot.
        MoveItemsToSlot(slotWithPointerFocus, newItem); // Move the item to the the specific slot.
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
    } else if (actualDelta < 0) {
      //FIXME: here we may be more smart and pre-sort the items by their stock slot. The gerater slots must be deleted first.
      for (var i = slotWithPointerFocus.slotItems.Count - 1; i >= 0 && actualDelta < 0; i--) {
        var item = slotWithPointerFocus.slotItems[i];
        if (!item.isLocked) {
          if (DeleteItem(item)) {
            ++actualDelta;
          } else {
            HostedDebugLog.Error(this, "Cannot delete item: itemPart={0}, itemId={1}", item.avPart.name, item.itemId);
          }
        }
      }
    }
    UpdateInventory();
  }

  /// <summary>Takes items from the focused slot and starts dragging operation.</summary>
  /// <param name="num">
  /// The number of items to take from the slot. It's OK to request more items than exist in the slot, all the items
  /// will be taken in this case.
  /// </param>
  /// <remarks>It's an action, invoked from the slot events state machine.</remarks>
  /// <seealso cref="_slotEventsHandler"/>
  void TakeItemsFromFocusedSlot(int num) {
    var itemsToDrag = slotWithPointerFocus.slotItems
        .Where(i => !i.isLocked)
        .Take(num)
        .ToArray();
    HostedDebugLog.Fine(
        this, "Take items from slot: slot=#{0}, requested={1}, got={2}",
        _inventorySlots.IndexOf(slotWithPointerFocus), num < int.MaxValue ? num : "<all>", itemsToDrag.Length);
    if (itemsToDrag.Length == 0) {
      return;
    }

    // Either add or set the grabbed items.
    if (KisApi.ItemDragController.isDragging) {
      itemsToDrag = KisApi.ItemDragController.leasedItems.Concat(itemsToDrag).ToArray();
      KisApi.ItemDragController.CancelItemsLease();
    }
    Array.ForEach(itemsToDrag, i => i.SetLocked(true));
    SetDraggedSlot(slotWithPointerFocus, itemsToDrag.Length);
    KisApi.ItemDragController.LeaseItems(
        slotWithPointerFocus.iconImage, itemsToDrag, ConsumeSlotItems, CancelSlotLeasedItems);
    var dragIconObj = KisApi.ItemDragController.dragIconObj;
    dragIconObj.hasScience = slotWithPointerFocus.hasScience;
    dragIconObj.stackSize = KisApi.ItemDragController.leasedItems.Length;
    dragIconObj.resourceStatus = slotWithPointerFocus.resourceStatus;
    UIPartActionController.Instance.partInventory.PlayPartSelectedSFX();
    UpdateInventory();
  }

  /// <summary>Consumes the dragged items and adds them into the hovered slot.</summary>
  /// <remarks>
  /// It's an action, invoked from the slot events state machine. Keep this method performance efficient since the stack
  /// size can be really big, like 200 or 500 items. At such sizes even a 2ms per item cost will be well noticeable in
  /// GUI. 
  /// </remarks>
  /// <seealso cref="_slotEventsHandler"/>
  void AddDraggedItemsToFocusedSlot() {
    var consumedItems = KisApi.ItemDragController.ConsumeItems();
    var kisSlotIndex = _inventorySlots.IndexOf(slotWithPointerFocus);
    if (consumedItems != null) {
      HostedDebugLog.Info(this, "Add items to slot: slot=#{0}, num={1}", kisSlotIndex, consumedItems.Length);
      var newItems = new List<InventoryItem>(consumedItems.Length);
      foreach (var consumedItem in consumedItems) {
        InventoryItem item;
        if (StockCompatibilitySettings.isCompatibilityMode) {
          item = AddItem(consumedItem, stockSlotIndex: kisSlotIndex);
        } else {
          item = AddItem(consumedItem);
          if (item != null) {
            MoveItemsToSlot(slotWithPointerFocus, item);
          }
        }
        if (item == null) {
          HostedDebugLog.Error(
              this, "Cannot add dragged item: part={0}, itemId={1}", consumedItem.avPart.name, consumedItem.itemId);
        }
      }
      UpdateInventory();
      UIPartActionController.Instance.partInventory.PlayPartDroppedSFX();
    } else {
      UISoundPlayer.instance.Play(KisApi.CommonConfig.sndPathBipWrong);
      HostedDebugLog.Error(
          this, "Cannot store/stack dragged items to slot: slot=#{0}, draggedItems={1}",
          kisSlotIndex, KisApi.ItemDragController.leasedItems.Length);
    }
  }

  /// <summary>Runs on the opened dialog to handle the dialog actions.</summary>
  /// <remarks>It must be attached to the dialog's game object.</remarks>
  IEnumerator GuiActionsHandler() {
    while (true) {
      yield return null; // Call on every frame update.
      if (Time.timeScale > float.Epsilon) {
        _slotEventsHandler.HandleActions(); // This also evaluates the events conditions.
      }
    }
  }
  #endregion
}

}  // namespace
