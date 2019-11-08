﻿// Kerbal Inventory System
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

namespace KIS2 {

  //FIXME: separate container and inventory concepts. data vs UI
public sealed class KISContainerWithSlots : KISContainerBase,
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
    var newItems = base.AddParts(avParts, nodes);
    foreach (var item in newItems) {
      var slot = FindSlotForPart(item.avPart, item.itemConfig);
      if (slot == null) {
        HostedDebugLog.Error(
            this, "No slots available. Using last slot: part={0}", item.avPart.name);
        slot = _inventorySlots.Last();
      }
      AddItemsToSlot(new InventoryItem[] { item }, slot);
    }
    return newItems;
  }

  /// <inheritdoc/>
  public override bool DeleteItems(InventoryItem[] deleteItems) {
    if (!base.DeleteItems(deleteItems)) {
      return false;
    }
    foreach (var deleteItem in deleteItems) {
      _itemToSlotMap[deleteItem].DeleteItem(deleteItem);
      _itemToSlotMap.Remove(deleteItem);
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
    if (CheckHasSlots(avParts, nodes ?? new ConfigNode[avParts.Length])) {
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
      return;  // Nothing to do.
    }
    HostedDebugLog.Fine(this, "Creating inventory window");
    unityWindow = UnityPrefabController.CreateInstance<UIKISInventoryWindow>(
        "KISInventoryDialog", UIMasterController.Instance.actionCanvas.transform);
    
    //FIXME: Fix it in the prefab via TMPro.
    DebugEx.Warning("*** Enabling PerfectPixel mode on the root UI canvas!");
    UIMasterController.Instance.actionCanvas.pixelPerfect = true;
    
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

  /// <summary>Check if inventory has enough slots to accomodate the items.</summary>
  bool CheckHasSlots(IReadOnlyList<AvailablePart> avParts, IReadOnlyList<ConfigNode> nodes) {
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

  /// <summary>Tries to find a slot where this item can stack.</summary>
  /// <returns>The available slot or <c>null</c> if none found.</returns>
  InventorySlotImpl FindSlotForPart(AvailablePart avPart, ConfigNode node,
                                    IEnumerable<InventorySlotImpl> preferredSlots = null) {
    InventorySlotImpl slot = null;
    var matchItem = new InventoryItemImpl(this, avPart, node);
    if (preferredSlots != null) {
      slot = preferredSlots.FirstOrDefault(
          x => x.CheckCanAddItems(new InventoryItem[] { matchItem }) == null);
    }
    if (slot == null) {
      slot = _inventorySlots
          .Where(x => !x.isEmpty)
          .FirstOrDefault(x => x.CheckCanAddItems(new InventoryItem[] { matchItem }) == null);
    }
    if (slot == null) {
      slot = _inventorySlots.FirstOrDefault(s => s.isEmpty);
    }
    return slot;
  }

  /// <summary>Add items to the specified slot of the inventory.</summary>
  /// <remarks>
  /// The items must belong to the inventory, but not be owned by it (i.e. not to be in the
  /// <see cref="KISContainerBase.inventoryItems"/>). This method doesn't check any preconditions.
  /// </remarks>
  /// <seealso cref="InventorySlotImpl.CheckCanAddItems"/>
  void AddItemsToSlot(InventoryItem[] addItems, InventorySlotImpl slot) {
    UpdateItems(addItems: addItems);
    slot.AddItems(addItems);
    Array.ForEach(addItems, x => _itemToSlotMap.Add(x, slot));
  }
  #endregion

  #region Unity window callbacks
  void OnSlotHover(Slot slot, bool isHover) {
    var inventorySlot = _inventorySlots[slot.slotIndex];
    if (isHover) {
      unityWindow.StartSlotTooltip();
      unityWindow.currentTooltip.StartCoroutine(UpdateHoveredHints(inventorySlot));
      inventorySlot.UpdateTooltip();
      KISAPI.ItemDragController.RegisterTarget(inventorySlot);
    } else {
      KISAPI.ItemDragController.UnregisterTarget(inventorySlot);
      unityWindow.DestroySlotTooltip();
    }
  }

  /// <summary>Callback for the Unity slot click action.</summary>
  void OnSlotClick(Slot slot, PointerEventData.InputButton button) {
    var inventorySlot = inventorySlots[slot.slotIndex];
    if (KISAPI.ItemDragController.isDragging) {
      HandleDragClickAction(inventorySlot);
    } else if (!inventorySlot.isEmpty) {
      HandleSlotClickAction(inventorySlot);
    }
  }

  void HandleDragClickAction(InventorySlotImpl inventorySlot) {
    if (!inventorySlot.isEmpty) {
      //FIXME
      DebugEx.Warning("*** demo cancel on non-empty slot");
      KISAPI.ItemDragController.CancelItemsLease();
      return;
    } else {
      //FIXME
      DebugEx.Warning("*** demo consume on empty slot");
      var consumedItems = KISAPI.ItemDragController.ConsumeItems();
      if (consumedItems != null) {
        //FIXME
        HostedDebugLog.Warning(this, "Adding {0} items from dragged pack", consumedItems.Length);
        //FIXME: verify if can add!
        inventorySlot.AddItems(consumedItems);
      } else {
        // FIXME: bip wrong!
        HostedDebugLog.Error(this, "The items owner has unexpectably refused the transfer deal");
      }
    }
  }

  void HandleSlotClickAction(InventorySlotImpl inventorySlot) {
    //FIXME
    DebugEx.Warning("*** demo lease on non-empty slot");
    KISAPI.ItemDragController.LeaseItems(
        inventorySlot.iconImage, inventorySlot.items,
        () => ConsumeSlotItems(inventorySlot),
        () => CancelSlotLeasedItems(inventorySlot));
    var dragIconObj = KISAPI.ItemDragController.dragIconObj;
    dragIconObj.hasScience = inventorySlot.unitySlot.hasScience;
    dragIconObj.stackSize = inventorySlot.items.Length;//FIXME: get it from unity when it's fixed to int
    dragIconObj.resourceStatus = inventorySlot.unitySlot.resourceStatus;
    inventorySlot.isLocked = true;
    Array.ForEach(inventorySlot.items, i => i.SetLocked(true));
  }

  bool ConsumeSlotItems(InventorySlotImpl inventorySlot) {
    //FIXME
    DebugEx.Warning("*** items consumed!");
    //FIXME: verify if all items are in the slot!
    foreach (var consumedItem in KISAPI.ItemDragController.leasedItems) {
      consumedItem.SetLocked(false);
      inventorySlot.DeleteItem(consumedItem);
    }
    inventorySlot.isLocked = false;
    //leasedItems.ToList().ForEach(i => i.isLocked = false);
    return true;
  }

  void CancelSlotLeasedItems(InventorySlotImpl inventorySlot) {
    //FIXME
    DebugEx.Warning("*** lease canceled!");
    inventorySlot.isLocked = false;
    KISAPI.ItemDragController.leasedItems.ToList().ForEach(i => i.SetLocked(false));
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
    if (unityWindow.slots.Length != _inventorySlots.Count) {
      HostedDebugLog.Fine(this, "Resizing inventory controller: oldSlots={0}, newSlots={1}",
                          _inventorySlots.Count, unityWindow.slots.Length);
      for (var i = _inventorySlots.Count; i < unityWindow.slots.Length; ++i) {
        _inventorySlots.Add(new InventorySlotImpl(this, unityWindow.slots[i]));
      }
      while (_inventorySlots.Count > unityWindow.slots.Length) {
        var deleteIndex = _inventorySlots.Count - 1;
        var slot = _inventorySlots[deleteIndex];
        foreach (var item in slot.slotItems) {
          HostedDebugLog.Error(
              this, "Dropping item from a non-empty slot: slotIdx={0}, item={1}",
              deleteIndex, item.avPart.name);
          DeleteItems(new[] { item });
        }
        _inventorySlots.RemoveAt(deleteIndex);
      }
      UpdateInventoryStats(null);
    }
  }

  /// <summary>Updates tooltip hints in every frame to catch the keyboard actions.</summary>
  /// <remarks>
  /// This coroutine is expected to be scheduled on the tooltip object. When it dies, so does this
  /// coroutine.
  /// </remarks>
  IEnumerator UpdateHoveredHints(InventorySlotImpl inventorySlot) {
    if (!UIKISInventoryTooltip.Tooltip.showHints) {
      yield break;  // No hints, no tracking.
    }
    while (true) {  // The coroutine will die with the tooltip.
      inventorySlot.UpdateHints();
      yield return null;
    }
  }
  #endregion
}

}  // namespace
