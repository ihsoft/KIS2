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
using System.Reflection;
using KSPDev.ProcessingUtils;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>
/// Base module to handle inventory items. It can only hold items, no GUI is offered.
/// </summary>
public class KisContainerBase : AbstractPartModule,
                                IKisInventory {
  #region Localizable GUI strings
  // ReSharper disable MemberCanBePrivate.Global

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  protected static readonly Message<VolumeLType> NotEnoughVolumeText = new Message<VolumeLType>(
      "",
      defaultTemplate: "Not enough volume: <color=#f88>-<<1>></color>",
      description: "Message to present to the user at the main status area when an item being placed into the inventory"
      + " cannot fit it due to not enough free volume.\n"
      + "The <<1>> parameter is the volume delta that would be needed for the item to fit of type VolumeLType.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected static readonly Message StockContainerLimitReachedErrorText = new Message(
      "",
      defaultTemplate: "Stock container limit reached",
      description: "An error that is presented when the part cannot be added into a KIS container due to the stock"
      + " container limitations (any). It only makes sense in the stock compatibility mode.");

  // ReSharper enable MemberCanBePrivate.Global
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

  #region Check reasons
  // ReSharper disable MemberCanBeProtected.Global

  /// <summary>Any of the stock storage settings prevent the action.</summary>
  /// <remarks>
  /// It depends on the compatibility settings. When all the settings are disabled, this error reason is not expected to
  /// be seen. The actual error text reason can differ for this type.
  /// </remarks>
  /// <seealso cref="StockContainerLimitReachedErrorText"/>
  public const string StockInventoryLimitReason = "StockInventoryLimit";

  /// <summary>The part is too large to be added into the inventory.</summary>
  public const string VolumeTooLargeReason = "VolumeTooLarge";

  // ReSharper enable MemberCanBeProtected.Global
  #endregion

  #region Inhertitable fields and properties
  /// <summary>
  /// Reflection of <see cref="inventoryItems"/> in a form of map for quick lookup operations. 
  /// </summary>
  /// <remarks>
  /// Do not modify this map directly! Descendants must use the interface methods or call the inherited methods.
  /// </remarks>
  /// <seealso cref="AddItem"/>
  /// <seealso cref="DeleteItem"/>
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
  /// <summary>
  /// Name of the config value that holds a mapping between a stock inventory slot and the KIS inventory item ID. 
  /// </summary>
  /// <remarks>The syntax is: &lt;slot-index&gt;-&lt;item-guid&gt;</remarks>
  const string PersistentConfigStockSlotMapping = "itemToStockSlotMapping";

  /// <summary>Index to lookup stock slot index to the items it holds.</summary>
  readonly Dictionary<int, HashSet<InventoryItem>> _stockSlotToItemsMap = new();

  /// <summary>Index to lookup item Id to the stock slot index that holds it.</summary>
  readonly Dictionary<string, int> _itemsToStockSlotMap = new();
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
        AddInventoryItem(MakeItemFromStockSlot(slotIndex, itemId: stockSlotItemIds[i]));
      }
    }
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

  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) {
      return;
    }
    var inventorySlotControl = stockInventoryModule.Fields[nameof(stockInventoryModule.InventorySlots)];
    if (KisApi.CommonConfig.compatibilitySettings.hideStockGui && inventorySlotControl != null) {
      HostedDebugLog.Fine(this, "Disabling the stock inventory GUI settings");
      if (!KisApi.CommonConfig.compatibilitySettings.respectStockInventoryLayout
          && !KisApi.CommonConfig.compatibilitySettings.respectStockStackingLogic) {
        HostedDebugLog.Warning
            (this, "Some of the compatibility settings are not active! The stock GUI may (and likely will) fail.");
      }
      inventorySlotControl.guiActive = false;
      inventorySlotControl.guiActiveEditor = false;
    }
    RegisterGameEventListener(GameEvents.onModuleInventorySlotChanged, OnModuleInventorySlotChangedEvent);
  }
  #endregion

  #region IKISInventory implementation
  /// <inheritdoc/>
  public virtual List<ErrorReason> CheckCanAddPart(string partName, ConfigNode node = null, bool logErrors = false) {
    ArgumentGuard.NotNullOrEmpty(partName, nameof(partName), context: this);
    var errors = new List<ErrorReason>();
    var item = InventoryItemImpl.ForPartName(this, partName, itemConfig: node);
    if (FindStockSlotForItem(item) == -1) {
      errors.Add(new ErrorReason() {
          shortString = StockInventoryLimitReason,
          guiString = StockContainerLimitReachedErrorText,
      });
    }
    var partVolume = item.volume;
    if (usedVolume + partVolume > maxVolume) {
      var freeVolume = maxVolume - Math.Min(usedVolume, maxVolume);
      errors.Add(new ErrorReason() {
          shortString = VolumeTooLargeReason,
          guiString = NotEnoughVolumeText.Format(partVolume - freeVolume),
      });
    }
    if (logErrors && errors.Count > 0) {
      HostedDebugLog.Error(this, "Cannot add '{0}' part:\n{1}", partName, DbgFormatter.C2S(errors, separator: "\n"));
    }
    return errors;
  }

  /// <inheritdoc/>
  public virtual InventoryItem AddPart(string partName, ConfigNode node = null) {
    ArgumentGuard.NotNullOrEmpty(partName, nameof(partName), context: this);
    return AddItem(InventoryItemImpl.ForPartName(this, partName, node));
  }

  /// <inheritdoc/>
  public virtual InventoryItem AddItem(InventoryItem item) {
    ArgumentGuard.NotNull(item, nameof(item), context: this);
    var stockSlotIndex = FindStockSlotForItem(item, logChecks: true);
    if (stockSlotIndex == -1) {
      HostedDebugLog.Error(this, "Cannot find a stock slot for part: name={0}", item.avPart.name);
      return null;
    }
    var newItem = InventoryItemImpl.ForPartName(this, item.avPart.name, item.itemConfig.CreateCopy());
    AddItemToStockSlot(newItem, stockSlotIndex);
    AddInventoryItem(newItem);
    HostedDebugLog.Fine(
        this, "Added item: part={0}, sourceId={1}, newItemId={2}", item.avPart.name, item.itemId, newItem.itemId);
    return newItem;
  }

  /// <inheritdoc/>
  public virtual bool DeleteItem(InventoryItem item) {
    ArgumentGuard.NotNull(item, nameof(item), context: this);
    if (!ReferenceEquals(item.inventory, this)) {
      HostedDebugLog.Error(this, "Item doesn't belong to this inventory: name={0}, id={1}, owner={2}",
                           item.avPart.name, item.itemId, item.inventory as PartModule);
      return false;
    }
    if (!inventoryItemsMap.ContainsKey(item.itemId)) {
      HostedDebugLog.Error(this, "Item not found: name={0}, id={1}", item.avPart.name, item.itemId);
      return false;
    }
    if (item.isLocked) {
      HostedDebugLog.Error(this, "Cannot delete locked item(s): name={0}, id={1}", item.avPart.name, item.itemId);
      return false;
    }
    RemoveInventoryItem(item);
    RemoveItemFromStockSlot(item);
    HostedDebugLog.Fine(this, "Removed item: part={0}, itemId={1}", item.avPart.name, item.itemId);
    return true;
  }

  /// <inheritdoc/>
  public virtual void UpdateInventoryStats(InventoryItem[] changedItems) {
    ArgumentGuard.NotNull(changedItems, nameof(changedItems), context: this);
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
    ArgumentGuard.NotNullOrEmpty(itemId, nameof(itemId), context: this);
    inventoryItemsMap.TryGetValue(itemId, out var res);
    return res;
  }
  #endregion

  #region Inheritable methods
  /// <summary>Adds the item into <see cref="inventoryItems"/> collection.</summary>
  /// <remarks>
  /// This method doesn't deal with the stock inventory update and only serves the purpose of the internal KIS state
  /// update. The descendants can use it to react to the changes on the KIS inventory.
  /// </remarks>
  /// <param name="item">The item to add.</param>
  protected virtual void AddInventoryItem(InventoryItem item) {
    inventoryItemsMap[item.itemId] = item;
    var itemsList = inventoryItems.ToList();
    itemsList.Add(item);
    inventoryItems = itemsList.ToArray();
    UpdateInventoryStats(new InventoryItem[0]);
  }

  /// <summary>Removes the item from the <see cref="inventoryItems"/> collection.</summary>
  /// <remarks>
  /// This method doesn't deal with the stock inventory update and only serves the purpose of the internal KIS state
  /// update. The descendants can use it to react to the changes on the KIS inventory. 
  /// </remarks>
  /// <param name="item">The item to remove.</param>
  protected virtual void RemoveInventoryItem(InventoryItem item) {
    if (!inventoryItemsMap.Remove(item.itemId)) {
      HostedDebugLog.Error(this, "Cannot delete item, not in the index: itemId={0}", item.itemId);
    } 
    inventoryItems = inventoryItems
        .Where(x => x.itemId != item.itemId)
        .ToArray(); 
    UpdateInventoryStats(new InventoryItem[0]);
  }
  #endregion

  #region Local utility methods
  /// <summary>Creates an item from a non-empty stock slot and updates stock related indexes.</summary>
  /// <remarks>
  /// The new item is not added to <see cref="inventoryItems"/>, but it's expected that it will be added there by the
  /// calling code. A normal way of doing it is calling <see cref="AddItem"/> or <see cref="DeleteItem"/>.
  /// </remarks>
  /// <param name="stockSlotIndex">The slot index in teh stock inventory module.</param>
  /// <param name="itemId">
  /// An optional ID to assign to the new item. If omitted, then a new unique ID will be generated.
  /// </param>
  /// <returns></returns>
  /// <seealso cref="stockInventoryModule"/>
  InventoryItem MakeItemFromStockSlot(int stockSlotIndex, string itemId = null) {
    var item = InventoryItemImpl.FromProtoPartSnapshot(
        this, stockInventoryModule.storedParts[stockSlotIndex].snapshot, itemId: itemId);
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
  /// <param name="logChecks">Tells if the stock compatibility related errors or limitations should be logged.</param>
  /// <returns>The stock slot index that can accept the item or <c>-1</c> if there are none.</returns>
  /// <seealso cref="KisApi.CommonConfig.compatibilitySettings"/>
  int FindStockSlotForItem(InventoryItem item, bool logChecks = false) {
    var itemConfig = item.itemConfig;
    var variant = VariantsUtils.GetCurrentPartVariant(item.avPart, itemConfig);
    var variantName = variant != null ? variant.Name : "";
    var maxSlotIndex = -1;
    var compatibility = KisApi.CommonConfig.compatibilitySettings;

    // Check if the stock compatibility mode is satisfied.
    if (compatibility.addOnlyCargoParts && !KisApi.PartPrefabUtils.GetCargoModule(item.avPart)) {
      if (logChecks) {
        HostedDebugLog.Error(this, "The item is not stock cargo compatible: partName={0}", item.avPart.name);
      }
      return -1;
    }

    // First, try to fit the item into an existing occupied slot to preserve the space.
    var skippedGoodSlots = new List<int>();
    foreach (var existingSlotIndex in stockInventoryModule.storedParts.Keys) {
      maxSlotIndex = Math.Max(existingSlotIndex, maxSlotIndex);
      var slot = stockInventoryModule.storedParts[existingSlotIndex];
      if (!slot.partName.Equals(item.avPart.name)) {
        continue;
      }
      if (compatibility.respectStockStackingLogic
          && !stockInventoryModule.CanStackInSlot(item.avPart, variantName, existingSlotIndex)) {
        skippedGoodSlots.Add(existingSlotIndex);
        continue;
      }
      var slotState =
          KisApi.PartNodeUtils.MakeComparablePartNode(
              KisApi.PartNodeUtils.GetConfigNodeFromProtoPartSnapshot(slot.snapshot));
      var itemState = KisApi.PartNodeUtils.MakeComparablePartNode(itemConfig);
      if (slotState.ToString() != itemState.ToString()) {
        continue;
      }
      return existingSlotIndex;  // Found a candidate.
    }
    if (skippedGoodSlots.Count > 0 && logChecks) {
      HostedDebugLog.Warning(
          this, "Skipping good stock stacks due to the compatibility settings: candidateSlots={0}, partName={1}",
          DbgFormatter.C2S(skippedGoodSlots), item.avPart.name);
    }

    // No suitable stock slot found. Get a first empty slot.
    var slotIndex = stockInventoryModule.FirstEmptySlot();
    if (slotIndex != -1) {
      return slotIndex;
    }
    if (compatibility.respectStockInventoryLayout) {
      if (logChecks) {
        HostedDebugLog.Error(
            this, "Cannot add an extra stock slot in the compatibility mode: partName={0}", item.avPart.name);
      }
      return -1;
    }

    // Find any unused stock slot index, even if it's beyond the stock logic address space.
    slotIndex = 0;
    while (stockInventoryModule.storedParts.TryGetValue(slotIndex, out var storedPart) && !storedPart.IsEmpty) {
      slotIndex++;
    }
    HostedDebugLog.Info(this, "Returning an out of scope stock slot: slotIndex={0}", slotIndex);
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
  /// <seealso cref="KisApi.CommonConfig.compatibilitySettings.respectStockStackingLogic"/>
  void AddItemToStockSlot(InventoryItem item, int stockSlotIndex) {
    if (KisApi.CommonConfig.compatibilitySettings.respectStockStackingLogic) {
      AddItemToStockSlot_StockGame(item, stockSlotIndex);
    } else {
      AddItemToStockSlot_Kis(item, stockSlotIndex);
    }
  }

  /// <summary>Updates the stock inventory module using the stock methods.</summary>
  void AddItemToStockSlot_StockGame(InventoryItem item, int stockSlotIndex) {
    if (stockSlotIndex >= stockInventoryModule.InventorySlots) {
      HostedDebugLog.Warning(
          this, "Extra stock slots cannot be handled via the stock logic: stockSlot=#{0}, part={1}",
          stockSlotIndex, item.avPart.name);
      AddItemToStockSlot_Kis(item, stockSlotIndex);
      return;
    }
    UpdateStockSlotIndex(stockSlotIndex, item);
    if (!stockInventoryModule.storedParts.ContainsKey(stockSlotIndex)
        || stockInventoryModule.storedParts[stockSlotIndex].IsEmpty) {
      stockInventoryModule.StoreCargoPartAtSlot(
          KisApi.PartNodeUtils.GetProtoPartSnapshotFromNode(
              vessel, item.itemConfig, keepPersistentId: true), stockSlotIndex);
    } else {
      var slot = stockInventoryModule.storedParts[stockSlotIndex];
      stockInventoryModule.UpdateStackAmountAtSlot(stockSlotIndex, slot.quantity + 1, slot.variantName);
    }
  }

  /// <summary>Updates the stock inventory using custom KIS logic.</summary>
  /// <remarks>The caller must ensure that the item fits the slot!</remarks>
  void AddItemToStockSlot_Kis(InventoryItem item, int stockSlotIndex) {
    if (!stockInventoryModule.storedParts.ContainsKey(stockSlotIndex)
        || stockInventoryModule.storedParts[stockSlotIndex].IsEmpty) {
      var pPart = KisApi.PartNodeUtils.GetProtoPartSnapshotFromNode(vessel, item.itemConfig, keepPersistentId: true);
      InvokePrivateStockModuleMethod("RefillEVAPropellantOnStoring", pPart); //FIXME: make a constant?
      var storedPart = new StoredPart(pPart.partName, stockSlotIndex) {
          slotIndex = stockSlotIndex,
          snapshot = pPart,
          variantName = pPart.moduleVariantName,
          quantity = 1,
          stackCapacity = pPart.moduleCargoStackableQuantity
      };
      stockInventoryModule.storedParts.Add(stockSlotIndex, storedPart);
      InvokePrivateStockModuleMethod("ResetInventoryPartsByName");
    } else {
      stockInventoryModule.storedParts[stockSlotIndex].quantity += 1;
    }
    UpdateStockSlotIndex(stockSlotIndex, item);

    if (stockSlotIndex < stockInventoryModule.InventorySlots) {
      GameEvents.onModuleInventorySlotChanged.Fire(stockInventoryModule, stockSlotIndex);
    } else {
      HostedDebugLog.Fine(
          this, "Not sending 'onModuleInventorySlotChanged' for the extra slots: stockSlot=#{0}", stockSlotIndex);
    }
    GameEvents.onModuleInventoryChanged.Fire(stockInventoryModule);
  }

  /// <summary>Removes the item from a stock inventory slot.</summary>
  /// <param name="item">The item to remove.</param>
  void RemoveItemFromStockSlot(InventoryItem item) {
    if (KisApi.CommonConfig.compatibilitySettings.respectStockStackingLogic) {
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
    if (stockSlotIndex >= stockInventoryModule.InventorySlots) {
      HostedDebugLog.Warning(
          this, "Extra stock slots cannot be handled via the stock logic: stockSlot=#{0}", stockSlotIndex);
      RemoveItemFromStockSlot_Kis(item);
      return;
    }
    UpdateStockSlotIndex(stockSlotIndex, item, remove: true);
    var slot = stockInventoryModule.storedParts[stockSlotIndex];
    var newStackQuantity = slot.quantity - 1;
    if (newStackQuantity > slot.stackCapacity) {
      // This can happen if the slot was previously made with a different compatibility setting. 
      HostedDebugLog.Warning(
          this,
          "Stack size is bigger than capacity, use KIS update method: slotIndex={0}, quantity={1}, capacity={2}",
          stockSlotIndex, newStackQuantity, slot.stackCapacity);
      RemoveItemFromStockSlot_Kis(item);
      return;
    }
    if (newStackQuantity == 0) {
      stockInventoryModule.ClearPartAtSlot(stockSlotIndex);
    } else {
      stockInventoryModule.UpdateStackAmountAtSlot(stockSlotIndex, newStackQuantity, slot.variantName);
    }
  }

  /// <summary>Updates the stock inventory using custom KIS logic.</summary>
  void RemoveItemFromStockSlot_Kis(InventoryItem item) {
    var stockSlotIndex = _itemsToStockSlotMap[item.itemId];
    var slot = stockInventoryModule.storedParts[stockSlotIndex];
    var newStackQuantity = slot.quantity - 1;
    if (newStackQuantity == 0) {
      stockInventoryModule.storedParts.Remove(stockSlotIndex);
      InvokePrivateStockModuleMethod("ResetInventoryPartsByName");
    } else {
      slot.quantity = newStackQuantity;
    }
    UpdateStockSlotIndex(stockSlotIndex, item, remove: true);

    if (stockSlotIndex < stockInventoryModule.InventorySlots) {
      GameEvents.onModuleInventorySlotChanged.Fire(stockInventoryModule, stockSlotIndex);
    } else {
      HostedDebugLog.Fine(
          this, "Not sending 'onModuleInventorySlotChanged' for the extra slots: stockSlot=#{0}", stockSlotIndex);
    }
    GameEvents.onModuleInventoryChanged.Fire(stockInventoryModule);
  }

  /// <summary>Reacts on the stock inventory change and updates the KIS inventory accordingly.</summary>
  void OnModuleInventorySlotChangedEvent(ModuleInventoryPart changedStockInventoryModule, int stockSlotIndex) {
    if (!ReferenceEquals(changedStockInventoryModule, stockInventoryModule)) {
      return;
    }
    var slotQuantity = stockInventoryModule.storedParts.ContainsKey(stockSlotIndex)
        ? stockInventoryModule.storedParts[stockSlotIndex].quantity
        : 0;
    var indexedItems = _stockSlotToItemsMap.ContainsKey(stockSlotIndex)
        ? _stockSlotToItemsMap[stockSlotIndex].Count
        : 0;
    if (slotQuantity < indexedItems) {
      while (slotQuantity < _stockSlotToItemsMap[stockSlotIndex].Count) {
        var item = _stockSlotToItemsMap[stockSlotIndex].Last();
        HostedDebugLog.Info(
            this, "Removing an item due to the stock slot change: slot={0}, itemId={1}", stockSlotIndex, item.itemId);
        UpdateStockSlotIndex(stockSlotIndex, item, remove: true);
        RemoveInventoryItem(item);
      }
    }
    if (slotQuantity > indexedItems) {
      while (!_stockSlotToItemsMap.ContainsKey(stockSlotIndex)
          || slotQuantity > _stockSlotToItemsMap[stockSlotIndex].Count) {
        var item = MakeItemFromStockSlot(stockSlotIndex);
        HostedDebugLog.Info(
            this, "Adding an item due to the stock slot change: slot={0}, part={1}, itemId={2}",
            stockSlotIndex, item.avPart.name, item.itemId);
        AddInventoryItem(item);
      }
    }
  }

  /// <summary>Calls a protected/private method on the stock cargo inventory module.</summary>
  /// <remarks>
  /// It's a very bad practice to call the non-public members via a reflection. However, copy/pasting the logic from the
  /// stock game is another bad practice due to the implementation can get changed easily. The chances that a
  /// private/protected method gets removed are much less than the implementation logic change. So, until the stock
  /// inventory module is refactored for inheritance, this is the only solution we have.  
  /// </remarks>
  /// <param name="methodName">
  /// The non-public method name to call on the <see cref="stockInventoryModule"/> module.
  /// </param>
  /// <param name="args">Optional arguments to pass.</param>
  /// <returns>
  /// The return result of the method. If the method declares the return result as <c>void</c>, then it won't be
  /// possible to distinguish a <c>null</c> result from the "no result" output.
  /// </returns>
  object InvokePrivateStockModuleMethod(string methodName, params object[] args) {
    var method = stockInventoryModule.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
    if (method == null) {
      HostedDebugLog.Error(this, "Cannot find stock cargo module method: name={0}", methodName);
      return null;
    }
    return method.Invoke(stockInventoryModule, args);
  }
  #endregion
}

}  // namespace
