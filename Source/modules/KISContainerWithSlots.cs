// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.LogUtils;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using KSP.UI;
using System.Reflection;
using System;
using System.Linq;
using KSPDev.GUIUtils;
using KSPDev.ModelUtils;
using KSPDev.PartUtils;
using KSPDev.KSPInterfaces;
using KSPDev.GUIUtils.TypeFormatters;
using KISAPIv2;
using KIS2.UIKISInventorySlot;
using KIS2.GUIUtils;
using KSPDev.Unity;
using UnityEngine.EventSystems;
using KSPDev.PrefabUtils;

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
  
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
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
      unityWindow.SetGridSize(new Vector2(3, 1));
    }
    if (Event.current.Equals(Event.KeyboardEvent("2")) && unityWindow != null) {
      AddFuelPart(0.1f, 0.5f);
    }
    if (Event.current.Equals(Event.KeyboardEvent("3")) && unityWindow != null) {
      AddFuelPart(0.5f, 0.5f);
    }
    if (Event.current.Equals(Event.KeyboardEvent("4")) && unityWindow != null) {
      AddFuelPart(1, 1);
    }
    if (Event.current.Equals(Event.KeyboardEvent("5")) && unityWindow != null) {
      AddPartByName("seatExternalCmd");
    }
  }
  #endregion

  #region KISContainerBase overrides
  /// <inheritdoc/>
  public override InventoryItem AddPart(AvailablePart avPart, ConfigNode node) {
    var slot = FindSlotForPart(avPart, node);
    if (slot == null) {
      HostedDebugLog.Warning(this, "No slots available for part: {0}", avPart.name);
      return null;
    }
    var item = new InventoryItemImpl(this, avPart, node);
    AddItemsToSlot(new InventoryItem[] { item }, slot);
    return item;
  }

  /// <inheritdoc/>
  public override bool DeleteItems(InventoryItem[] deleteItems) {
    if (base.DeleteItems(deleteItems)) {
      foreach (var deleteItem in deleteItems) {
        _itemToSlotMap[deleteItem].DeleteItem(deleteItem);
        _itemToSlotMap.Remove(deleteItem);
      }
      return true;
    }
    return false;
  }


  /// <inheritdoc/>
  public override void UpdateInventoryStats(InventoryItem[] changedItems) {
    base.UpdateInventoryStats(changedItems);
    UpdateInventoryWindow();
  }
  #endregion

  #region DEBUG methods
  string[] fuleParts = new string[] {
      "RadialOreTank",
      "SmallTank",
      "fuelTankSmallFlat",
      "Size3MediumTank",
      "Size3LargeTank",
      "Size3SmallTank",
      "fuelTankSmallFlat",
      "fuelTankSmall",
      "fuelTank",
      "fuelTank_long",
      "RCSTank1-2",
      "externalTankCapsule",
  };

  void AddFuelPart(float minPct, float maxPct) {
    var avPartIndex = (int) (UnityEngine.Random.value * fuleParts.Length);
    var avPart = PartLoader.getPartInfoByName(fuleParts[avPartIndex]);
    if (avPart == null) {
      DebugEx.Error("*** bummer: no part {0}", fuleParts[avPartIndex]);
      return;
    }
    DebugEx.Info("*** adding fuel part: {0}", avPart.name);
    //FIXME: check volume and size
    
    var node = KISAPI.PartNodeUtils.PartSnapshot(avPart.partPrefab);
    foreach (var res in KISAPI.PartNodeUtils.GetResources(node)) {
      var amount = UnityEngine.Random.Range(minPct, maxPct) * res.maxAmount;
      KISAPI.PartNodeUtils.UpdateResource(node, res.resourceName, amount);
    }
    AddPart(avPart, node);
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
    if (unityWindow != null) {
      HostedDebugLog.Fine(this, "Destroying inventory window");
      Hierarchy.SafeDestory(unityWindow);
      unityWindow = null;
    }
  } 

  /// <summary>Updates stats in the open inventory window.</summary>
  /// <remarks>It's safe to call it when the inventory window is not open.</remarks>
  void UpdateInventoryWindow() {
    if (unityWindow != null) {
      var text = new List<string>();
      text.Add(InventoryContentMassTxt.Format(contentMass));
      text.Add(InventoryContentCostTxt.Format(contentCost));
      text.Add(MaxVolumeTxt.Format(maxVolume));
      text.Add(AvailableVolumeTxt.Format(Math.Max(maxVolume - usedVolume, 0)));
      unityWindow.mainStats = string.Join("\n", text.ToArray());
    }
  }

  /// <summary>Returns first empty slot in the invetory.</summary>
  /// <returns>The available slot or <c>null</c> if none found.</returns>
  InventorySlotImpl GetFreeSlot() {
    //FIXME: extend windows size.
    return inventorySlots.FirstOrDefault(s => s.isEmpty);
  }

  /// <summary>Tries to find a slot where this item can stack.</summary>
  /// <returns>The available slot or <c>null</c> if none found.</returns>
  /// <seealso cref="GetFreeSlot"/>
  InventorySlotImpl FindSlotForPart(AvailablePart avAprt, ConfigNode node) {
    //FIXME: look in the existing slots first. 
    return GetFreeSlot();
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
      inventorySlot.UpdateTooltip();
      KISAPI.ItemDragController.RegisterTarget(inventorySlot);
      unityWindow.currentTooltip.StartCoroutine(UpdateHoveredHints(inventorySlot));
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
