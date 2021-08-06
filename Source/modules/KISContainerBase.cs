// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KISAPIv2;
using KIS2.GUIUtils;
using KSPDev.GUIUtils;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>
/// Base module to handle inventory items. It can only hold items, no GUI is offered.
/// </summary>
public class KisContainerBase : AbstractPartModule,
                                IKisInventory {
  #region Localizable GUI strings
  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<VolumeLType> NotEnoughVolumeText = new Message<VolumeLType>(
      "",
      defaultTemplate: "Not enough volume: <color=#f88>-<<1>></color>",
      description: "Message to present to the user at the main status area when an item being"
      + " placed into the inventory cannot fit it due to not enough free volume.\n"
      + "The <<1>> parameter is the volume delta that would be needed for the item to fit of"
      + " type VolumeLType.");
  #endregion

  #region Part's config fields
  /// <summary>Maximum volume that this container can contain.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public double maxContainerVolume;

  /// <summary>Maximum size of the item that can fit the container.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Vector3 maxItemSize;
  #endregion

  #region IKISInventory properties
  /// <inheritdoc/>
  public InventoryItem[] inventoryItems { get; private set; } = new InventoryItem[0];

  /// <inheritdoc/>
  public Vector3 maxInnerSize => maxItemSize;

  /// <inheritdoc/>
  public double maxVolume => maxContainerVolume;

  /// <inheritdoc/>
  public double usedVolume { get; private set; }

  /// <inheritdoc/>
  public double contentMass { get; private set; }

  /// <inheritdoc/>
  public double contentCost { get; private set; }
  #endregion

  #region Inhertitable fields and properties
  /// <summary>
  /// Reflection of <see cref="inventoryItems"/> in a form of map for quick lookup operations. 
  /// </summary>
  /// <remarks>
  /// Do not modify this map directly! Descendants must use he interface methods or call
  /// <see cref="UpdateItemsCollection"/>.
  /// </remarks>
  /// <seealso cref="UpdateItemsCollection"/>
  /// <seealso cref="InventoryItem.itemId"/>
  protected readonly Dictionary<string, InventoryItem> inventoryItemsMap = new();

  /// <summary>The stock inventory module on this part, which KIS is being shadowing.</summary>
  /// <remarks>
  /// The modules that depend on the stock inventory must verify if this property is <c>null</c>, and if it is, then the
  /// dependent module logic must be completely disabled to avoid the errors down the line.
  /// </remarks>
  /// <value>The stock module or <c>null</c> if none found.</value>
  protected ModuleInventoryPart stockInventoryModule {
    get {
      if (_stockInventoryModule == null) {
        _stockInventoryModule = part.Modules.OfType<ModuleInventoryPart>().FirstOrDefault();
      }
      if (_stockInventoryModule == null) {
        HostedDebugLog.Error(this, "Part doesn't have a stock inventory module");
      }
      return _stockInventoryModule;
    }
  }
  ModuleInventoryPart _stockInventoryModule;
  #endregion

  #region Local fields and properties
  /// <summary>Index to lookup stock slot index to the items it holds.</summary>
  readonly Dictionary<int, HashSet<InventoryItem>> _stockSlotToItemsMap = new();

  /// <summary>Index to lookup item Id to the stock slot index that holds it.</summary>
  readonly Dictionary<string, int> _itemsToStockSlotMap = new();
  #endregion

  #region Persistent node names
  /// <summary>
  /// Name of the config value that holds a mapping between a stock inventory slot and the KIS inventory item ID. 
  /// </summary>
  /// <remarks>The syntax is: &lt;slot-index&gt;-&lt;item-guid&gt;</remarks>
  const string PersistentConfigStockSlotMapping = "itemToStockSlotMapping";
  #endregion

  #region AbstractPartModule overrides
  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);
    if (stockInventoryModule == null || stockInventoryModule.storedParts == null) {
      HostedDebugLog.Error(this, "Cannot load state due to a bad part state");
      return;
    }

    // Restore the items to the stock slot mapping.
    var stockSlotToItemIdsMap = new Dictionary<int, HashSet<string>>();
    var stockSlotMapping = node.GetValues(PersistentConfigStockSlotMapping);
    foreach (var mappingStr in stockSlotMapping) {
      var pair = mappingStr.Split(new[] {'-'}, 2);
      var slotIndex = int.Parse(pair[0]);
      if (!stockSlotToItemIdsMap.ContainsKey(slotIndex)) {
        stockSlotToItemIdsMap[slotIndex] = new HashSet<string>();
      }
      stockSlotToItemIdsMap[slotIndex].Add(pair[1]);
    }

    // Restore the inventory items. Ensure every item has a unique ID and is assigned to a stock slot. 
    var restoredItems = new List<InventoryItem>();
    foreach (var stockSlot in stockInventoryModule.storedParts.Values) {
      var slotIndex = stockSlot.slotIndex;
      var stockSlotItemIds = stockSlotToItemIdsMap.ContainsKey(slotIndex)
          ? stockSlotToItemIdsMap[slotIndex].ToList()
          : new List<string>();
      // Add the missing IDs. This is not expected, but we catch up.
      while (stockSlotItemIds.Count < stockSlot.quantity) {
        var newId = Guid.NewGuid().ToString();
        HostedDebugLog.Warning(this, "Stock item doesn't have ID mapping: slotIndex={0}, slotPos={1}, newId={2}",
                               slotIndex, stockSlotItemIds.Count, newId);
        stockSlotItemIds.Add(newId);
      }
      // Make items and update the stock slot indexes.
      for (var i = 0; i < stockSlot.quantity; i++) {
        restoredItems.Add(MakeItemFromStockSlot(slotIndex, itemId: stockSlotItemIds[i]));
      }
    }
    UpdateItemsCollection(add: restoredItems);
  }

  /// <inheritdoc/>
  public override void OnSave(ConfigNode node) {
    base.OnSave(node);
    foreach (var entry in _stockSlotToItemsMap) {
      foreach (var item in entry.Value) {
        node.AddValue(PersistentConfigStockSlotMapping, entry.Key + "-" + item.itemId);
      }
    }
  }
  #endregion

  #region IKISInventory implementation
  /// <inheritdoc/>
  public virtual ErrorReason[] CheckCanAddParts(
      AvailablePart[] avParts, ConfigNode[] nodes = null, bool logErrors = false) {
    var errors = new List<ErrorReason>();
    nodes ??= new ConfigNode[avParts.Length];
    double partsVolume = 0;
    for (var i = 0; i < avParts.Length; ++i) {
      var avPart = avParts[i];
      var node = nodes[i] ?? KisApi.PartNodeUtils.PartSnapshot(avPart.partPrefab);
      partsVolume += KisApi.PartModelUtils.GetPartVolume(avPart, partNode: node);
      //FIXME: Check part size.
    }
    if (usedVolume + partsVolume > maxVolume) {
      // Normalize the used volume in case of the inventory is already overloaded. 
      var freeVolume = maxVolume - Math.Min(usedVolume, maxVolume);
      errors.Add(new ErrorReason() {
          shortString = "VolumeTooLarge",
          guiString = NotEnoughVolumeText.Format(partsVolume - freeVolume),
      });
    }
    if (logErrors && errors.Count > 0) {
      HostedDebugLog.Error(this, "Cannot add {0} part(s):\n{1}",
                           avParts.Length, DbgFormatter.C2S(errors, separator: "\n"));
    }
    return errors.Count > 0 ? errors.ToArray() : null;
  }

  /// <inheritdoc/>
  public virtual InventoryItem[] AddParts(AvailablePart[] avParts, ConfigNode[] nodes) {
    var items = new InventoryItem[avParts.Length];
    for (var i = 0; i < avParts.Length; i++) {
      items[i] = new InventoryItemImpl(this, avParts[i], nodes[i]);
    }
    return AddItems(items);
  }

  /// <inheritdoc/>
  public virtual InventoryItem[] AddItems(InventoryItem[] items) {
    if (items.Length == 0) {
      return new InventoryItem[0];
    }

    var addedItems = new List<InventoryItem>();
    for (var i = 0; i < items.Length; i++) {
      var item = items[i];
      var stockSlotIndex = FindStockSlotForItem(item);
      if (stockSlotIndex == -1) {
        HostedDebugLog.Error(this, "Cannot add item to inventory: name={0}, itemIndex={1}", item.avPart, i);
        continue;
      }
      var newItem = new InventoryItemImpl(this, item.avPart, item.itemConfig.CreateCopy());
      AddItemToStockSlot(newItem, stockSlotIndex);
      addedItems.Add(newItem);
    }
    UpdateItemsCollection(add: addedItems);
    HostedDebugLog.Fine(this, "Added {0} items of {1}", addedItems.Count, items.Length); 
    return addedItems.ToArray();
  }

  /// <inheritdoc/>
  public virtual bool DeleteItems(InventoryItem[] deleteItems) {
    if (deleteItems.Length == 0) {
      return true;
    }
    foreach (var item in deleteItems) {
      if (item.isLocked) {
        HostedDebugLog.Error(this, "Cannot delete locked item(s): name={0}, id={1}", item.avPart.name, item.itemId);
        return false;
      }
      if (!ReferenceEquals(item.inventory, this)) {
        HostedDebugLog.Error(this, "Item doesn't belong to this inventory: name={0}, id={1}, owner={2}",
                             item.avPart.name, item.itemId, item.inventory as PartModule);
        return false;
      }
      if (!inventoryItemsMap.ContainsKey(item.itemId)) {
        HostedDebugLog.Error(this, "Item not found: name={0}, id={1}", item.avPart.name, item.itemId);
        return false;
      }
    }
    UpdateItemsCollection(remove: deleteItems);
    deleteItems.ToList().ForEach(RemoveItemFromStockSlot);
    HostedDebugLog.Fine(this, "Removed {0} items", deleteItems.Length);
    return true;
  }

  /// <inheritdoc/>
  public virtual void UpdateInventoryStats(InventoryItem[] changedItems) {
    if (changedItems == null) {
      HostedDebugLog.Fine(this, "Updating all items in the inventory...");
      Array.ForEach(inventoryItems, item => item.UpdateConfig());
    } else if (changedItems.Length > 0) {
      HostedDebugLog.Fine(this, "Updating {0} items in the inventory..." , changedItems.Length);
      Array.ForEach(changedItems, item => item.UpdateConfig());
    }
    usedVolume = inventoryItems.Sum(item => item.volume);
    contentMass = inventoryItems.Sum(item => item.fullMass);
    contentCost = inventoryItems.Sum(item => item.fullCost);
  }

  /// <inheritdoc/>
  public InventoryItem FindItem(string itemId) {
    inventoryItemsMap.TryGetValue(itemId, out var res);
    return res;
  }
  #endregion

  #region Inheritable methods
  /// <summary>Modifies <see cref="inventoryItems"/> collection.</summary>
  /// <remarks>
  /// This method doesn't deal with the stock inventory update and only serves the purpose of the internal KIS state
  /// update. The descendants can use it to react to the changes on the KIS inventory. It's highly discouraged to call
  /// this method from the descendants. When possible, all the items updates must be done via the public interface. 
  /// </remarks>
  /// <param name="add">The items to add.</param>
  /// <param name="remove">The items to delete.</param>
  protected virtual void UpdateItemsCollection(
      ICollection<InventoryItem> add = null, ICollection<InventoryItem> remove = null) {
    if (add != null) {
      foreach (var item in add) {
        inventoryItemsMap[item.itemId] = item;
      }
    }
    var removeIds = new HashSet<string>();
    if (remove != null) {
      foreach (var item in remove) {
        if (inventoryItemsMap.Remove(item.itemId)) {
          removeIds.Add(item.itemId);
        } else {
          HostedDebugLog.Error(this, "Cannot delete item, not in the index: itemId={0}", item.itemId);
        } 
      }
    }
    inventoryItems = inventoryItems
        .Where(id => !removeIds.Contains(id.itemId))
        .Concat(add ?? new List<InventoryItem>())
        .ToArray(); 
    UpdateInventoryStats(new InventoryItem[0]);
  } 
  #endregion

  #region Local utility methods
  /// <summary>Creates an item from a non-empty stock slot and updates stock related indexes.</summary>
  /// <remarks>
  /// The new item is not added to <see cref="inventoryItems"/>, but it's expected that it will be added there by the
  /// calling code. A normal way of doing it is calling <see cref="UpdateItemsCollection"/>.
  /// </remarks>
  /// <param name="stockSlotIndex">The slot index in teh stock inventory module.</param>
  /// <param name="itemId">
  /// An optional ID to assign to the new item. If omitted, then a new unique ID will be generated.
  /// </param>
  /// <returns></returns>
  /// <seealso cref="_stockInventoryModule"/>
  InventoryItem MakeItemFromStockSlot(int stockSlotIndex, string itemId = null) {
    // var stockSlot = _stockInventoryModule.storedParts[stockSlotIndex];
    // var slotConfig = KisApi.PartNodeUtils.GetConfigNodeFromProtoPartSnapshot(stockSlot.snapshot); 
    // var item = InventoryItemImpl.FromConfigNode(this, slotConfig, itemId: itemId);
    var item = InventoryItemImpl.FromProtoPartSnapshot(
        this, _stockInventoryModule.storedParts[stockSlotIndex].snapshot, itemId: itemId);
    UpdateStockSlotIndex(stockSlotIndex, item);
    return item;
  }

  /// <summary>Single point method to update item-to-slot indexes.</summary>
  /// <param name="stockSlotIndex">The stock slot index to update the index for.</param>
  /// <param name="item">The item to update the index for.</param>
  /// <param name="remove">Tells if it's an add or remove action.</param>
  void UpdateStockSlotIndex(int stockSlotIndex, InventoryItem item, bool remove = false) {
    if (!remove) {
      if (!_stockSlotToItemsMap.ContainsKey(stockSlotIndex)) {
        _stockSlotToItemsMap[stockSlotIndex] = new HashSet<InventoryItem>();
      }
      if (!_stockSlotToItemsMap[stockSlotIndex].Add(item)) {
        HostedDebugLog.Warning(
            this, "Duplicated record in slot to item index: stockSlot={0}, itemId={1}", stockSlotIndex, item.itemId);
      }
      if (!_itemsToStockSlotMap.ContainsKey(item.itemId)) {
        _itemsToStockSlotMap[item.itemId] = stockSlotIndex;
      } else {
        HostedDebugLog.Warning(
            this, "Duplicated record in item to slot index: stockSlot={0}, itemId={1}", stockSlotIndex, item.itemId);
      }
    } else {
      if (!_stockSlotToItemsMap.ContainsKey(stockSlotIndex) || !_stockSlotToItemsMap[stockSlotIndex].Remove(item)) {
        HostedDebugLog.Warning(
            this, "Item not found in slot to item index: stockSlot={0}, itemId={1}", stockSlotIndex, item.itemId);
      }
      if (!_itemsToStockSlotMap.Remove(item.itemId)) {
        HostedDebugLog.Warning(
            this, "Item not found in item to slot index: stockSlot={0}, itemId={1}", stockSlotIndex, item.itemId);
      }
    }
  }

  /// <summary>Gets a stock inventory slot that can accept the given item.</summary>
  /// <remarks>This method may modify the stock module state if an extra slot needs to be added.</remarks>
  /// <param name="item">The item to verify.</param>
  /// <returns>The stock slot index that can accept the item.</returns>
  /// <seealso cref="KisApi.CommonConfig.respectStockStackingLogic"/>
  /// <seealso cref="KisApi.CommonConfig.respectStockInventoryLayout"/>
  int FindStockSlotForItem(InventoryItem item) {
    var itemConfig = item.itemConfig;
    var variant = VariantsUtils.GetCurrentPartVariant(item.avPart, itemConfig);
    var variantName = variant != null ? variant.Name : "";
    var maxSlotIndex = -1;

    // First, try to fit the item into an existing occupied slot to preserve the space.
    foreach (var existingSlotIndex in _stockInventoryModule.storedParts.Keys) {
      maxSlotIndex = Math.Max(existingSlotIndex, maxSlotIndex);
      var slot = _stockInventoryModule.storedParts[existingSlotIndex];
      if (!slot.partName.Equals(item.avPart.name)) {
        continue;
      }
      if (KisApi.CommonConfig.respectStockStackingLogic
          && !_stockInventoryModule.CanStackInSlot(item.avPart, variantName, existingSlotIndex)) {
        continue;
      }
      // Verify that item has no differences in state with the slot.
      var checkNodes = new[] { "MODULE", "RESOURCE", "SCIENCE" };
      var slotState = KisApi.PartNodeUtils.GetConfigNodeFromProtoPartSnapshot(slot.snapshot);
      foreach (var node in slotState.nodes.GetNodes()) {
        if (!checkNodes.Contains(node.name)) {
          slotState.RemoveNodes(node.name);
        }
      }
      var itemState = itemConfig.CreateCopy();
      foreach (var node in itemState.nodes.GetNodes()) {
        if (!checkNodes.Contains(node.name)) {
          itemState.RemoveNodes(node.name);
        }
      }
      if (slotState.ToString() != itemState.ToString()) {
        continue;
      }
      return existingSlotIndex;  // Found a candidate.
    }

    // No suitable stock slot found.
    var slotIndex = stockInventoryModule.FirstEmptySlot();
    if (slotIndex != -1) {
      return slotIndex;
    }
    if (KisApi.CommonConfig.respectStockInventoryLayout) {
      return -1;
    }

    // Create a new slot beyond the stock addressing space. It won't be accessible via the stock UI. 
    slotIndex = maxSlotIndex + 1;
    HostedDebugLog.Info(this, "Creating an out of scope stock slot: slotIndex={0}, itemId={1}", slotIndex, item.itemId);
    _stockInventoryModule.storedParts.Add(slotIndex, new StoredPart(item.avPart.name, slotIndex));
    return slotIndex;
  }

  /// <summary>Adds the item into the specified stock inventory slot.</summary>
  /// <remarks>
  /// <p>
  /// The caller must ensure the item can be added into the slot. It includes (but is not limited to) the check for the
  /// variant or resources on the part.</p>
  /// <p>
  /// This method has a compatibility setting. When set to be compatible with the KSP stock logic, it's (mostly) using
  /// the stock module methods. In the non-compatible mode a completely custom logic is executed. This gives more
  /// flexibility, but the cost is less compatibility.
  /// </p> 
  /// </remarks>
  /// <param name="item">The item to add.</param>
  /// <param name="stockSlotIndex">The stock slot index to add into.</param>
  /// <seealso cref="FindStockSlotForItem"/>
  /// <seealso cref="KisApi.CommonConfig.respectStockStackingLogic"/>
  void AddItemToStockSlot(InventoryItem item, int stockSlotIndex) {
    if (KisApi.CommonConfig.respectStockStackingLogic) {
      AddItemToStockSlot_StockGame(item, stockSlotIndex);
    } else {
      AddItemToStockSlot_Kis(item, stockSlotIndex);
    }
  }

  /// <summary>Updates the stock inventory module using the stock methods.</summary>
  void AddItemToStockSlot_StockGame(InventoryItem item, int stockSlotIndex) {
    UpdateStockSlotIndex(stockSlotIndex, item);
    if (!_stockInventoryModule.storedParts.ContainsKey(stockSlotIndex)
        || _stockInventoryModule.storedParts[stockSlotIndex].IsEmpty) {
      _stockInventoryModule.StoreCargoPartAtSlot(
          KisApi.PartNodeUtils.GetProtoPartSnapshotFromNode(stockInventoryModule, item.itemConfig), stockSlotIndex);
      return;
    }
    var slot = _stockInventoryModule.storedParts[stockSlotIndex];
    _stockInventoryModule.UpdateStackAmountAtSlot(stockSlotIndex, slot.quantity + 1, slot.variantName);
  }

  /// <summary>Updates the stock inventory using custom KIS logic.</summary>
  void AddItemToStockSlot_Kis(InventoryItem item, int stockSlotIndex) {
    //FIXME: IMPLEMENT!
    throw new NotImplementedException("KIS vision is yet to come");
  }

  /// <summary>Removes the item from a stock inventory slot.</summary>
  /// <param name="item">The item to remove.</param>
  void RemoveItemFromStockSlot(InventoryItem item) {
    if (KisApi.CommonConfig.respectStockStackingLogic) {
      RemoveItemFromStockSlot_StockGame(item);
    } else {
      RemoveItemFromStockSlot_Kis(item);
    }
  }

  /// <summary>Updates the stock inventory module using (mostly) the stock methods.</summary>
  /// <remarks>
  /// It falls back to the KIS method when the stock slot have more items than allowed by ste stock logic. It's done to
  /// let the mod working when the compatibility settings set to <c>true</c>. 
  /// </remarks>
  void RemoveItemFromStockSlot_StockGame(InventoryItem item) {
    var stockSlotIndex = _itemsToStockSlotMap[item.itemId];
    UpdateStockSlotIndex(stockSlotIndex, item, remove: true);
    var slot = _stockInventoryModule.storedParts[stockSlotIndex];
    var newStackQuantity = slot.quantity - 1;
    if (newStackQuantity > slot.stackCapacity) {
      HostedDebugLog.Warning(
          this,
          "Stack size is bigger than capacity, use KIS update method: slotIndex={0}, quantity={1}, capacity={2}",
          stockSlotIndex, newStackQuantity, slot.stackCapacity);
      RemoveItemFromStockSlot_Kis(item);
      return;
    }
    if (newStackQuantity == 0) {
      _stockInventoryModule.ClearPartAtSlot(stockSlotIndex);
    } else {
      _stockInventoryModule.UpdateStackAmountAtSlot(stockSlotIndex, newStackQuantity, slot.variantName);
    }
  }

  /// <summary>Updates the stock inventory using custom KIS logic.</summary>
  void RemoveItemFromStockSlot_Kis(InventoryItem item) {
    //FIXME: IMPLEMENT!
    throw new NotImplementedException("KIS vision is yet to come");
  }
  #endregion
}

}  // namespace
