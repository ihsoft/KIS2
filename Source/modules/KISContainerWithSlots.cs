// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections;
using System.Collections.Generic;
using KSP.UI;
using System;
using System.Linq;
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
    IHasGUI {

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

  #region Local utility fields and properties.
  /// <summary>Inventory window that is opened at the moment.</summary>
  internal UIKISInventoryWindow unityWindow;

  /// <summary>Inventory slots. They always exactly match the Unity window slots.</summary>
  readonly List<InventorySlotImpl> _inventorySlots = new List<InventorySlotImpl>();

  /// <summary>Index that resolves item to the slot that contains it.</summary>
  readonly Dictionary<InventoryItem, InventorySlotImpl> _itemToSlotMap =
      new Dictionary<InventoryItem, InventorySlotImpl>();
  #endregion

  #region AbstractPartModule overrides
  public override void OnAwake() {
    base.OnAwake();
    useGUILayout = false;
  }
  #endregion

  #region IHasGUI implementation
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
      if (unityWindow.hoveredSlot != null && unityWindow.hoveredSlot == slot.unitySlot) {
        slot.UpdateTooltip();
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

  #region DEBUG methods
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
    unityWindow.mainStats = string.Join("\n", text.ToArray());
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
        newSlots.Add(new InventorySlotImpl(this, slot.unitySlot));
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
    slot = _inventorySlots.FirstOrDefault(s => s.isEmpty && s.unitySlot != null);
    if (slot != null) {
      return slot;
    }
    if (!addInvisibleSlot) {
      return null;
    }
    // Finally, create a new invisible slot to fit the items.
    HostedDebugLog.Warning(this, "Adding an invisible slot: slotIdx={0}", _inventorySlots.Count);
    _inventorySlots.Add(new InventorySlotImpl(this, null));
    slot = _inventorySlots[_inventorySlots.Count - 1];
    return slot;
  }

  /// <summary>Add items to the specified slot of the inventory.</summary>
  /// <remarks>
  /// The items must belong to the inventory, but not be owned by it (i.e. not to be in the
  /// <see cref="KisContainerBase.inventoryItems"/>). This method doesn't check any preconditions.
  /// </remarks>
  /// <seealso cref="InventorySlotImpl.CheckCanAddItems"/>
  internal void AddItemsToSlot(InventoryItem[] addItems, InventorySlotImpl slot) {
    UpdateItems(addItems: addItems);
    slot.AddItems(addItems);
    Array.ForEach(addItems, x => _itemToSlotMap.Add(x, slot));
  }
  #endregion

  #region Unity window callbacks
  /// <summary>A callback that is called when pointer enters or leaves a UI slot.</summary>
  /// <param name="hoveredSlot">The Unity slot that is a source of the event.</param>
  /// <param name="isHover">Tells if pointer enters or leaves the UI element.</param>
  void OnSlotHover(Slot hoveredSlot, bool isHover) {
    if (isHover) {
      RegisterHoverCallback(hoveredSlot);
    } else {
      UnregisterHoverCallback(hoveredSlot);
    }
  }

  /// <summary>Callback for the Unity slot click action.</summary>
  void OnSlotClick(Slot slot, PointerEventData.InputButton button) {
    var inventorySlot = _inventorySlots[slot.slotIndex];
    inventorySlot.OnSlotClicked(button);
  }

  void OnSlotAction(Slot slot, int actionButtonNum, PointerEventData.InputButton button) {
    //FIXME
    HostedDebugLog.Fine(
        this, "Clicked: slot={0}, action={1}, button={2}", slot.slotIndex, actionButtonNum, button);
  }

  Vector2 OnNewGridSize(Vector2 newSize) {
    //FIXME: check if not below non-empty slots
    return newSize;
  }

  void OnGridSizeChanged() {
    ArrangeSlots();
  }

  /// <summary>
  /// Ensures that each UI slot has a corresponded inventory slot. Also, updates and optimizes the
  /// inventory slots that are not currently present in UI. 
  /// </summary>
  /// <remarks>
  /// This method must be called each time the inventory or unity slots number is changed.
  /// </remarks>
  void ArrangeSlots() {
    // Visible slots order may change, it would make the tooltip irrelevant.
    if (unityWindow.hoveredSlot != null) {
      UnregisterHoverCallback(unityWindow.hoveredSlot);
    }

    // Compact empty slots when there are hidden slots in the inventory. This may let some of the
    // hidden slots to become visible. 
    var visibleSlots = unityWindow.slots.Length;
    for (var i = _inventorySlots.Count - 1; i >= 0 && _inventorySlots.Count > visibleSlots; --i) {
      if (!_inventorySlots[i].isEmpty) {
        continue; 
      }
      if (i >= visibleSlots) {
        // Simple cleanup, do it silently.
        //FIXME
        HostedDebugLog.Warning(this, "**** CLEANUP an empty slot: slotIdx={0}", i);
        _inventorySlots.RemoveAt(i); 
      } else {
        // Cleanup of a visible empty slot. The inventory layout will change.
        HostedDebugLog.Warning(this, "Compact an empty slot: slotIdx={0}", i);
        _inventorySlots.RemoveAt(i);
      }
    }

    // Add up slots to match the current UI.
    for (var i = _inventorySlots.Count; i < unityWindow.slots.Length; ++i) {
      _inventorySlots.Add(new InventorySlotImpl(this, null));
    }

    // Align logical slots with the visible slots in UI.
    for (var i = 0; i < _inventorySlots.Count; ++i) {
      var slot = _inventorySlots[i];
      var newUnitySlot = i < unityWindow.slots.Length
          ? unityWindow.slots[i]
          : null;
      if (!slot.isEmpty) {
        if (slot.unitySlot != null && newUnitySlot == null) {
          HostedDebugLog.Warning(this, "Slot becomes hidden in UI: slotIdx={0}", i);
        } else if (slot.unitySlot == null && newUnitySlot != null) {
          HostedDebugLog.Fine(this, "Hidden slot becomes visible in UI: slotIdx={0}", i);
        }
      }
      slot.unitySlot = newUnitySlot;
    }
    UpdateInventoryStats(new InventoryItem[0]);

    // Restore the tooltip callbacks if needed.
    if (unityWindow.hoveredSlot != null) {
      RegisterHoverCallback(unityWindow.hoveredSlot);
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
      inventorySlot.UpdateHints();
      yield return null;
    }
  }

  /// <summary>Destroys tooltip and stops any active logic on the UI slot.</summary>
  /// <param name="hoveredSlot">The UI element that is a target of the hover event.</param>
  void UnregisterHoverCallback(Slot hoveredSlot) {
    var inventorySlot = _inventorySlots.FirstOrDefault(x => x.unitySlot == hoveredSlot);
    if (inventorySlot != null) {
      KISAPI.ItemDragController.UnregisterTarget(inventorySlot);
      unityWindow.DestroySlotTooltip();
    } else {
      HostedDebugLog.Error(this, "No inventory slot found: unitySlot={0}", hoveredSlot);
    }
  }

  /// <summary>Establishes a tooltip and starts the active logic on a UI slot.</summary>
  /// <param name="hoveredSlot">The UI element that is a target of the hover event.</param>
  void RegisterHoverCallback(Slot hoveredSlot) {
    var inventorySlot = _inventorySlots.FirstOrDefault(x => x.unitySlot == hoveredSlot);
    if (inventorySlot != null) {
      unityWindow.StartSlotTooltip();
      unityWindow.currentTooltip.StartCoroutine(UpdateHoveredHints(inventorySlot));
      inventorySlot.UpdateTooltip();
      KISAPI.ItemDragController.RegisterTarget(inventorySlot);
    } else {
      HostedDebugLog.Error(this, "No inventory slot found: unitySlot={0}", hoveredSlot);
    }
  }
  #endregion
}

}  // namespace
