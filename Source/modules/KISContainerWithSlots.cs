﻿// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.LogUtils;
using UnityEngine;
using UnityEngine.UI;
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
using KSPDev.Prefabs;

namespace KIS2 {

  //FIXME: separate container and inventory concepts. data vs UI
public sealed class KISContainerWithSlots : KISContainerBase,
    IHasGUI, IKISDragTarget {

  #region Localizable GUI strings.
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<string> DialogTitle = new Message<string>(
      "",
      defaultTemplate: "Inventory: <<1>>",
      description: "Title of the inventory dialog for this part.\n"
          + " The <<1>> argument is a user friendly name of the onwer part.",
      example: "Inventory: SC-62 Portable Container");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<MassType> InventoryContentMassTxt = new Message<MassType>(
      "",
      defaultTemplate: "Content mass: <color=#58F6AE><<1>></color>");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<CostType> InventoryContentCostTxt = new Message<CostType>(
      "",
      defaultTemplate: "Content cost: <color=#58F6AE><<1>></color>");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<VolumeLType> AvailableVolumeTxt = new Message<VolumeLType>(
      "",
      defaultTemplate: "Available volume: <color=yellow><<1>></color>");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<VolumeLType> MaxVolumeTxt = new Message<VolumeLType>(
      "",
      defaultTemplate: "Maximum volume: <color=#58F6AE><<1>></color>");
  #endregion

  #region Part's config fields
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Vector2 minGridSize = new Vector2(3, 1);
  
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Vector2 maxGridSize = new Vector2(16, 9);

  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
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
  readonly List<InventorySlotImpl> inventorySlots = new List<InventorySlotImpl>();
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

  #region IKISDragTarget implementation
  /// <inheritdoc/>
  public void OnKISDragStart() {
    throw new NotImplementedException();
  }

  /// <inheritdoc/>
  public void OnKISDragEnd(bool isCancelled) {
    throw new NotImplementedException();
  }

  /// <inheritdoc/>
  public bool OnKISDrag(bool pointerMoved) {
    throw new NotImplementedException();
  }
  #endregion

  #region KISContainerBase overrides
  /// <inheritdoc/>
  public override InventoryItem AddItem(AvailablePart avPart, ConfigNode node) {
    var slot = FindSlotForPart(avPart, node);
    if (slot == null) {
      HostedDebugLog.Warning(this, "No slots available for part: {0}", avPart.name);
      return null;
    }
    var item = base.AddItem(avPart, node);
    slot.AddItems(new[] { item });
    return item;
  }

  //public virtual bool DeleteItem(InventoryItem item)

  /// <inheritdoc/>
  public override void UpdateInventoryStats(params InventoryItem[] changedItems) {
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
    AddItem(avPart, node);
  }

  void AddPartByName(string partName) {
    var avPart = PartLoader.getPartInfoByName(partName);
    if (avPart == null) {
      DebugEx.Error("*** bummer: no part {0}", partName);
      return;
    }
    AddItem(avPart, KISAPI.PartNodeUtils.PartSnapshot(avPart.partPrefab));
  }
  #endregion

  #region Local utility methods
  void OpenInventoryWindow() {
    if (unityWindow != null) {
      return;  // Nothing to do.
    }
    HostedDebugLog.Fine(this, "Creating inventory window");
    unityWindow = UnityPrefabController.CreateInstance<UIKISInventoryWindow>(
        "KISInventoryDialog", UIMasterController.Instance.actionCanvas.transform);
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

  void CloseInventoryWindow() {
    if (unityWindow != null) {
      HostedDebugLog.Fine(this, "Destroying inventory window");
      Hierarchy.SafeDestory(gameObject);
      unityWindow = null;
    }
  } 

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
  #endregion

  #region Unity window callbacks
  void OnSlotHover(Slot slot, bool isHover) {
    var inventorySlot = inventorySlots[slot.slotIndex];
    if (isHover) {
      unityWindow.StartSlotTooltip();
      KISAPI.ItemDragController.RegisterTarget(this);
    } else {
      KISAPI.ItemDragController.UnregisterTarget(this);
      unityWindow.StartSlotTooltip();
    }
    if (isHover && !inventorySlot.isEmpty && !KISAPI.ItemDragController.isDragging) {
      inventorySlot.UpdateTooltip(unityWindow.StartSlotTooltip());
    }
  }

  void OnSlotClick(Slot slot, PointerEventData.InputButton button) {
    //FIXME
    HostedDebugLog.Fine(this, "Clicked: slot={0}, button={1}", slot.slotIndex, button);
    var inventorySlot = inventorySlots[slot.slotIndex];
    //FIXME
    if (KISAPI.ItemDragController.isDragging) {
      //FIXME: don't allow for demo!
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
      return;
    }
    //FIXME
    DebugEx.Warning("*** demo lease on non-empty slot");
    KISAPI.ItemDragController.LeaseItems(
        slot.slotImage, inventorySlot.items,
        () => ConsumeSlotItems(inventorySlot),
        () => CancelSlotLeasedItems(inventorySlot));
    var dragIconObj = KISAPI.ItemDragController.dragIconObj;
    dragIconObj.hasScience = slot.hasScience;
    dragIconObj.stackSize = inventorySlot.items.Length;//FIXME: get it from unity when it's fixed to int
    dragIconObj.resourceStatus = slot.resourceStatus;
    inventorySlot.isLocked = true;
    Array.ForEach(inventorySlot.items, i => i.SetLocked(true));
    //FIXME: it's demo only case
    //KISAPI.ItemDragController.SetCanConsumeState(false);
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
    if (unityWindow.slots.Length != inventorySlots.Count) {
      HostedDebugLog.Fine(this, "Resizing inventory controller: oldSlots={0}, newSlots={1}",
                          inventorySlots.Count, unityWindow.slots.Length);
      for (var i = inventorySlots.Count; i < unityWindow.slots.Length; ++i) {
        inventorySlots.Add(new InventorySlotImpl(this, unityWindow.slots[i]));
      }
      while (inventorySlots.Count > unityWindow.slots.Length) {
        var deleteIndex = inventorySlots.Count - 1;
        var slot = inventorySlots[deleteIndex];
        foreach (var item in slot.items) {
          HostedDebugLog.Error(
              this, "Dropping item from a non-empty slot: slotIdx={0}, item={1}",
              deleteIndex, item.avPart.name);
          DeleteItem(item);
        }
        inventorySlots.RemoveAt(deleteIndex);
      }
      UpdateInventoryStats();
    }
  }
  #endregion
}

}  // namespace
