// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using KSP.UI;
using System;
using System.Collections;
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
using UnityEngine.UI;

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

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message NoSlotsErrorReason = new Message(
      "",
      defaultTemplate: "No enough compatible slots",
      description: "Error message that is presented when parts cannot be added into the inventory"
          + " due to there are not enough compatible/empty slots available.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> TakeStackHint = new Message<KeyboardEventType>(
      "",
      defaultTemplate: "<b><color=#5a5><<1>></color></b> to grab whole stack",
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
  [KSPEvent(guiActive = true, guiActiveUnfocused = true, guiActiveEditor = true)]
  [LocalizableItem(
      tag = "#01",
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
  static readonly Event AddToStackEvent = Event.KeyboardEvent("mouse0");
  static readonly Event StoreIntoSlotEvent = Event.KeyboardEvent("mouse0");
  #endregion

  #region Persistent node names
  const string PersistentConfigSlotNode = "SLOT";
  const string PersistentConfigSlotItem = "item";
  #endregion

  #region Local fields, constants, and properties.
  /// <summary>The left side inventory window offset in the <i>FLIGHT</i> scene.</summary>
  /// <seealso cref="GetDefaultDlgPos"/>
  const float DlgFlightSceneXOffset = 10.0f;

  /// <summary>The top side inventory window offset in the <i>FLIGHT</i> scene.</summary>
  /// <remarks>Must not be overlapping with the flight controls.</remarks>
  /// <seealso cref="GetDefaultDlgPos"/>
  const float DlgFlightSceneYOffset = 40.0f;

  /// <summary>The left side inventory window offset in the <i>EDIT</i> scene.</summary>
  /// <remarks>Must not be overlapping the parts selection side panel.</remarks>
  /// <seealso cref="GetDefaultDlgPos"/>
  const float DlgEditorSceneXOffset = 280.0f;

  /// <summary>The top side inventory window offset in the <i>EDIT</i> scene.</summary>
  /// <remarks>Must not be overlapping the editor controls at the top.</remarks>
  /// <seealso cref="GetDefaultDlgPos"/>
  const float DlgEditorSceneYOffset = 40.0f;

  /// <summary>Distance between auto-arranged windows.</summary>
  /// <seealso cref="ArrangeWindows"/>
  const float WindowsGapSize = 5;

  /// <summary>
  /// Duration for the inventory windows to move on the screen to fit to the new layout. 
  /// </summary>
  /// <see cref="ArrangeWindows"/>
  const float WindowMoveAnimationDuration = 0.2f; // Seconds.

  /// <summary>Inventory window that is opened at the moment.</summary>
  UiKisInventoryWindow _unityWindow;

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

  /// <summary>Last known position at the dialog close. It will be restored at open.</summary>
  Vector3? screenPosition;

  /// <summary>All slots inventory windows opened till now.</summary>
  /// TODO(ihsoft): Move into some global location to support other GUI modules.
  static readonly List<UiKisInventoryWindow> openWindows = new List<UiKisInventoryWindow>();

  /// <summary>Windows that are in a process of aligning.</summary>
  static readonly Dictionary<UiWindowDragControllerScript, Coroutine> movingWindows =
      new Dictionary<UiWindowDragControllerScript, Coroutine>();
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

  #region AbstractPartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    useGUILayout = false;
  }

  /// <inheritdoc/>
  public override void OnDestroy() {
    CloseInventoryWindow();
    base.OnDestroy();
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);
    var slotsNodes = node.GetNodes(PersistentConfigSlotNode);
    foreach (var slotNode in slotsNodes) {
      var slot = new InventorySlotImpl(null);
      _inventorySlots.Add(slot);
      var items = slotNode.GetValues(PersistentConfigSlotItem)
          .Where(x => inventoryItemsMap.ContainsKey(x))
          .Select(x => inventoryItemsMap[x])
          .ToArray();
      UpdateSlotItems(slot, addItems: items);
    }
    // Handle out of sync items to ensure every item is assigned to a slot.
    var itemsWithNoSlot = inventoryItems.Where(x => !_itemToSlotMap.ContainsKey(x));
    foreach (var item in itemsWithNoSlot) {
      HostedDebugLog.Warning(this, "Loading non-slot item: {0}", item.itemId);
      UpdateSlotItems(FindSlotForItem(item, addInvisibleSlot: true), addItems: new[] { item });
    }
  }

  /// <inheritdoc/>
  public override void OnSave(ConfigNode node) {
    base.OnSave(node);
    foreach (var slot in _inventorySlots) {
      var slotsNode = node.AddNode(PersistentConfigSlotNode);
      Array.ForEach(slot.slotItems, x => slotsNode.AddValue(PersistentConfigSlotItem, x.itemId));
    }
  }
  #endregion

  #region DEBUG: IHasGUI implementation
  public void OnGUI() {
    if (_unityWindow == null) {
      return;
    }
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
      UpdateSlotItems(FindSlotForItem(item, addInvisibleSlot: true), addItems: new[] { item });
    }
    return newItems;
  }

  /// <inheritdoc/>
  public override InventoryItem[] AddItems(InventoryItem[] items) {
    var newItems = base.AddItems(items);
    foreach (var item in newItems) {
      UpdateSlotItems(FindSlotForItem(item, addInvisibleSlot: true), addItems: new[] { item });
    }
    return newItems;
  }

  /// <inheritdoc/>
  public override bool DeleteItems(InventoryItem[] deleteItems) {
    if (!base.DeleteItems(deleteItems)) {
      return false;
    }
    foreach (var item in deleteItems) {
      UpdateSlotItems(_itemToSlotMap[item], deleteItems: new[] { item });
    }
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
      var node = KisApi.PartNodeUtils.PartSnapshot(avPart.partPrefab);
      foreach (var res in KisApi.PartNodeUtils.GetResources(node)) {
        var amount = UnityEngine.Random.Range(minPct, maxPct) * res.maxAmount;
        KisApi.PartNodeUtils.UpdateResource(node, res.resourceName, amount);
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
    if (KisApi.ItemDragController.isDragging && _slotWithPointerFocus == _dragSourceSlot
        || !KisApi.ItemDragController.isDragging) {
      MouseClickTakeItems(button); // User wants to start/add items to the dragging action.
    } else if (KisApi.ItemDragController.isDragging) {
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
    ArrangeWindows();
  }

  /// <summary>Callback when pointers enters or leaves the dialog.</summary>
  void OnEditorDialogHover(bool isHovered) {
    KisApi.ItemDragController.SetFocusedTarget(isHovered ? _unityWindow.gameObject : null);
  }
  #endregion

  #region Local utility methods
  /// <summary>Gives a staring position for the inventory dialogs, depending on teh scene.</summary>
  /// <remarks>
  /// The <c>X</c> boundary should be treated as indentation for the multi-row collections.
  /// </remarks>
  static Vector3 GetDefaultDlgPos() {
    if (HighLogic.LoadedSceneIsFlight) {
      return new Vector3(
          -Screen.width / 2.0f + DlgFlightSceneXOffset,
          Screen.height / 2.0f - DlgFlightSceneYOffset,
          0);
    }
    if (HighLogic.LoadedSceneIsEditor) {
      return new Vector3(
          -Screen.width / 2.0f + DlgEditorSceneXOffset,
          Screen.height / 2.0f - DlgEditorSceneYOffset,
          0);
    }
    return Vector3.zero; // Fallback.
  }

  /// <summary>Opens the inventory window.</summary>
  void OpenInventoryWindow() {
    if (_unityWindow != null) {
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
    _unityWindow.onSlotClick.Add(OnSlotClick);
    _unityWindow.onSlotAction.Add(OnSlotAction);
    _unityWindow.onNewGridSize.Add(OnNewGridSize);
    _unityWindow.onGridSizeChanged.Add(OnGridSizeChanged);
    _unityWindow.onDialogClose.Add(CloseInventoryWindow);
    _unityWindow.onDialogHover.Add(OnEditorDialogHover);

    _unityWindow.title = DialogTitle.Format(part.partInfo.title);
    _unityWindow.minSize = minGridSize;
    _unityWindow.maxSize = maxGridSize;
    _unityWindow.SetGridSize(new Vector3(slotGridWidth, slotGridHeight, 0)); 
    UpdateInventoryWindow();

    if (screenPosition == null) {
      var basePos = ArrangeWindows(calculateOnly: true);
      HostedDebugLog.Fine(this, "Set calculated window positon: {0}", basePos);
      _unityWindow.mainRect.localPosition = basePos;
    } else {
      HostedDebugLog.Fine(this, "Restore window position: {0}", screenPosition);
      _unityWindow.mainRect.localPosition = screenPosition.Value;
      var dragWindow = _unityWindow.gameObject.GetComponent<UiWindowDragControllerScript>();
      if (dragWindow != null) {
        dragWindow.positionChanged = true;
      }
    }
    _unityWindow.SendMessage(
        nameof(IKspDevUnityControlChanged.ControlUpdated), _unityWindow.gameObject,
        SendMessageOptions.DontRequireReceiver);
    openWindows.Add(_unityWindow);
  }

  /// <summary>Destroys the inventory window.</summary>
  void CloseInventoryWindow() {
    if (_unityWindow == null) {
      return;
    }
    HostedDebugLog.Fine(this, "Destroying inventory window");
    var dragWindow = _unityWindow.gameObject.GetComponent<UiWindowDragControllerScript>();
    if (dragWindow == null || dragWindow.positionChanged) {
      screenPosition = _unityWindow.mainRect.localPosition;
    }
    if (_dragSourceSlot != null) {
      // TODO(ihsoft): Better, disable the close button when there are items in the dragging state.
      HostedDebugLog.Fine(this, "Cancel dragging items");
      KisApi.ItemDragController.CancelItemsLease();
    }
    openWindows.Remove(_unityWindow);
    ArrangeWindows();
    Hierarchy.SafeDestory(_unityWindow);
    _unityWindow = null;
    // Immediately make all slots invisible. Don't rely on Unity cleanup routines.  
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
      var node = nodes[i] ?? KisApi.PartNodeUtils.PartSnapshot(avPart.partPrefab);
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
    _inventorySlots.Add(new InventorySlotImpl(null));
    slot = _inventorySlots[_inventorySlots.Count - 1];
    return slot;
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
      UISoundPlayer.instance.Play(KisApi.CommonConfig.sndPathBipWrong);
      HostedDebugLog.Error(
          this, "Cannot take items from slot: totalItems={0}, canTakeItems={1}",
          _slotWithPointerFocus.slotItems.Length, newCanTakeItems.Length);
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
    dragIconObj.stackSize = itemsToDrag.Length;
    dragIconObj.resourceStatus = _slotWithPointerFocus.resourceStatus;
    UIPartActionController.Instance.partInventory.PlayPartSelectedSFX();
  }

  /// <summary>Triggers when items from the slot are consumed by the target.</summary>
  /// <remarks>
  /// This method may detect a failing condition. If it happens, the state must stay unchanged.
  /// </remarks>
  /// <seealso cref="_dragSourceSlot"/>
  bool ConsumeSlotItems() {
    var leasedItems = KisApi.ItemDragController.leasedItems;
    Array.ForEach(leasedItems, i => i.SetLocked(false));
    var res = DeleteItems(KisApi.ItemDragController.leasedItems);
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
    Array.ForEach(KisApi.ItemDragController.leasedItems, i => i.SetLocked(false));
    SetDraggedSlot(null);
  }

  /// <summary>
  /// Handles slot clicks when there is a drag operation pending from another slot.
  /// </summary>
  /// <seealso cref="_slotWithPointerFocus"/>
  void MouseClickDropItems(PointerEventData.InputButton button) {
    var storeItems = _slotWithPointerFocus.isEmpty
        && EventChecker.CheckClickEvent(StoreIntoSlotEvent, button);
    var stackItems = !_slotWithPointerFocus.isEmpty
        && EventChecker.CheckClickEvent(AddToStackEvent, button);
    InventoryItem[] consumedItems = null;
    if ((storeItems || stackItems) && _canAcceptDraggedItems) {
      consumedItems = KisApi.ItemDragController.ConsumeItems();
      if (consumedItems != null) {
        var newItems = base.AddItems(consumedItems);
        UpdateSlotItems(_slotWithPointerFocus, addItems: newItems);
        UIPartActionController.Instance.partInventory.PlayPartDroppedSFX();
      }
    }
    if (consumedItems == null) {
      UISoundPlayer.instance.Play(KisApi.CommonConfig.sndPathBipWrong);
      HostedDebugLog.Error(
          this, "Cannot store/stack dragged items to slot: draggedItems={0}",
          KisApi.ItemDragController.leasedItems.Length);
    }
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
        if (i >= slotGridWidth || j >= slotGridHeight) {
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
      HostedDebugLog.Warning(
            this, "Hidden slot in inventory: part={0}, count={1}",
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
    currentTooltip.gameObject.SetActive(false);
    if (KisApi.ItemDragController.isDragging && _slotWithPointerFocus != _dragSourceSlot) {
      currentTooltip.gameObject.SetActive(true);
      if (_canAcceptDraggedItems) {
        if (_slotWithPointerFocus.isEmpty) {
          currentTooltip.title = StoreIntoSlotActionTooltip;
          currentTooltip.baseInfo.text =
              StoreIntoSlotCountHint.Format(KisApi.ItemDragController.leasedItems.Length);
          currentTooltip.hints = StoreIntoSlotActionHint.Format(StoreIntoSlotEvent);
        } else {
          currentTooltip.title = AddToStackActionTooltip;
          currentTooltip.baseInfo.text =
              AddToStackCountHint.Format(KisApi.ItemDragController.leasedItems.Length);
          currentTooltip.hints = AddToStackActionHint.Format(AddToStackEvent);
        }
      } else {
        currentTooltip.title = _slotWithPointerFocus.isEmpty
            ? CannotStoreIntoSlotTooltipText
            : CannotAddToStackTooltipText;
        if (_canAcceptDraggedItemsCheckResult != null) {
          currentTooltip.baseInfo.text = string.Join(
              "\n",
              _canAcceptDraggedItemsCheckResult
                  .Where(r => r.guiString != null)
                  .Select(r => r.guiString));
        }
        currentTooltip.hints = null;
      }
    } else if (!_slotWithPointerFocus.isEmpty) {
      currentTooltip.gameObject.SetActive(true);
      _slotWithPointerFocus.UpdateTooltip(_unityWindow.currentTooltip);
      var hints = new List<string> {
          TakeStackHint.Format(TakeSlotEvent),
          TakeOneItemHint.Format(TakeOneItemEvent),
          TakeTenItemsHint.Format(TakeTenItemsEvent)
      };
      currentTooltip.hints = string.Join("\n", hints);
    }
  }

  /// <summary>
  /// Verifies if the currently dragged items can be stored into the hovered slot.
  /// </summary>
  /// <seealso cref="_canAcceptDraggedItemsCheckResult"/>
  /// <seealso cref="_canAcceptDraggedItems"/>
  void CheckCanAcceptDrops() {
    if (KisApi.ItemDragController.isDragging && _slotWithPointerFocus != null) {
      // For the items from other inventories also check the basic constraints.
      var checkItems = KisApi.ItemDragController.leasedItems;
      var otherInvAvParts = new List<AvailablePart>();
      var otherInvPartConfigs = new List<ConfigNode>();
      foreach (var checkItem in checkItems.Where(x => !ReferenceEquals(x.inventory, this))) {
        otherInvAvParts.Add(checkItem.avPart);
        otherInvPartConfigs.Add(checkItem.itemConfig);
      }
      _canAcceptDraggedItemsCheckResult = null;
      if (otherInvAvParts.Count > 0) {
        _canAcceptDraggedItemsCheckResult =
            base.CheckCanAddParts(otherInvAvParts.ToArray(), otherInvPartConfigs.ToArray());
      }
      if (_canAcceptDraggedItemsCheckResult == null) {
        _canAcceptDraggedItemsCheckResult = _slotWithPointerFocus.CheckCanAddItems(checkItems);
      }
      _canAcceptDraggedItems = _canAcceptDraggedItemsCheckResult == null;
    } else {
      _canAcceptDraggedItemsCheckResult = null;
      _canAcceptDraggedItems = false;
    }
  }

  /// <summary>Updates items list in the slot and updates indexes.</summary>
  void UpdateSlotItems(InventorySlotImpl slot,
                       InventoryItem[] addItems = null, InventoryItem[] deleteItems = null) {
    if (addItems != null) {
      slot.AddItems(addItems);
      Array.ForEach(addItems, x => _itemToSlotMap.Add(x, slot));
    }
    if (deleteItems != null) {
      slot.DeleteItems(deleteItems);
      Array.ForEach(deleteItems, x => _itemToSlotMap.Remove(x));
      ArrangeSlots(); // Make de-frag in case of there are invisible slots. 
    }
    if (slot == _slotWithPointerFocus) {
      UpdateTooltip();
    }
  }

  /// <summary>Arranges the unpinned inventory windows so that they are not overlapping.</summary>
  /// <param name="calculateOnly">
  /// Tells if only the next new window location need to be calculated.
  /// </param>
  /// <returns>The position of the next new window.</returns>
  Vector3 ArrangeWindows(bool calculateOnly = false) {
    var managedWindows = openWindows
        .Select(x => x.gameObject.GetComponent<UiWindowDragControllerScript>())
        .Where(x => !x.positionChanged)
        .ToList();
    var basePos = GetDefaultDlgPos();
    if (managedWindows.Count == 0) {
      return basePos; // Nothing to do.
    }
    if (calculateOnly) {
      var lastWindow = managedWindows[managedWindows.Count - 1];
      return lastWindow.mainRect.position
          + new Vector3(lastWindow.mainRect.sizeDelta.x + WindowsGapSize, 0, 0);
    }
    foreach (var window in managedWindows) {
      LayoutRebuilder.ForceRebuildLayoutImmediate(window.mainRect);
      if (Vector3.SqrMagnitude(window.mainRect.position - basePos) > float.Epsilon) {
        if (movingWindows.ContainsKey(window)) {
          StopCoroutine(movingWindows[window]);
          movingWindows.Remove(window);
        }
        movingWindows.Add(window, StartCoroutine(AnimateMoveWindow(window, basePos)));
      }
      basePos.x += window.mainRect.sizeDelta.x + WindowsGapSize;
    }
    return basePos;
  }

  /// <summary>Animates setting window's position.</summary>
  IEnumerator AnimateMoveWindow(UiWindowDragControllerScript window, Vector3 tgtPos) {
    var srcPos = window.mainRect.position;
    var animationTime = 0.0f;
    while (window.gameObject.activeInHierarchy
        && Vector3.SqrMagnitude(window.mainRect.position - tgtPos) > float.Epsilon) {
      yield return null;
      animationTime += Time.deltaTime;
      window.mainRect.position =
          Vector3.Lerp(srcPos, tgtPos, animationTime / WindowMoveAnimationDuration);
    }
  }
  #endregion
}

}  // namespace
