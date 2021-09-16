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
using KSP.UI.Screens;
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
  /// <summary>Maximum size of the item that can fit the container.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Vector3 maxItemSize;
  #endregion

  #region IKISInventory properties
  /// <inheritdoc/>
  public Vessel ownerVessel => vessel;

  /// <inheritdoc/>
  public InventoryItem[] inventoryItems { get; private set; } = new InventoryItem[0];

  /// <inheritdoc/>
  public Vector3 maxInnerSize => maxItemSize;

  /// <inheritdoc/>
  public double maxVolume => stockInventoryModule.packedVolumeLimit;

  /// <inheritdoc/>
  public double usedVolume => stockInventoryModule.volumeCapacity;

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

  /// <summary>Returns the stock inventory module on this part, which KIS is being shadowing.</summary>
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

  /// <summary>Returns the stock UI inventory action.</summary>
  /// <remarks>It controls the GUI behavior of the stock inventory PAW.</remarks>
  /// <value>The UI action inventory. It's never <c>null</c>.</value>
  protected UIPartActionInventory stockInventoryUiAction {
    get {
      if (_stockInventoryUiAction == null) {
        _stockInventoryUiAction = stockInventoryModule.Fields
            .OfType<BaseField>()
            .SelectMany(f => new[] { f.uiControlFlight, f.uiControlEditor })
            .OfType<UI_Grid>()
            .Select(x => x.pawInventory)
            .First(x => x != null);
      }
      return _stockInventoryUiAction;
    }
  }
  UIPartActionInventory _stockInventoryUiAction;
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

  #region Helper scope class
  /// <summary>Helper scope class that temporarily increases the stock inventory slot stack capacity.</summary>
  /// <remarks>Use it in the non-compatible modes to pack more items into the stock inventory.</remarks>
  class StackCapacityScope : IDisposable {
    readonly KisContainerBase _kisBaseModule;
    readonly StoredPart _storedPart;
    readonly int _originalStackCapacity;
    readonly int _originalQuantity;

    /// <summary>The maximum size of the stock inventory slot stack.</summary>
    const int MaxStackCapacity = 99; // Keep it two-digits for better UI experience.

    /// <summary>Temporarily increases the stack capacity and restores it back on the disposal.</summary>
    /// <remarks>
    /// The stack capacity will be set to a high (but limited) value to let the stock logic accommodating more items. On
    /// the scope exit, either the original setting will be restored or a new, bigger value, will be set.
    /// </remarks>
    /// <param name="kisBaseModule">The KIS module on which behalf the action is being performed.</param>
    /// <param name="storedPart">The stock slot to modify.</param>
    /// <seealso cref="MaxStackCapacity"/>
    public StackCapacityScope(KisContainerBase kisBaseModule, StoredPart storedPart) {
      _kisBaseModule = kisBaseModule;
      _storedPart = storedPart;
      _originalStackCapacity = storedPart.stackCapacity;
      _originalQuantity = storedPart.quantity;
      storedPart.stackCapacity = MaxStackCapacity;
      storedPart.snapshot.moduleCargoStackableQuantity = MaxStackCapacity;
    }

    /// <summary>Restores the original stack capacity or sets a new one.</summary>
    /// <remarks>
    /// The original capacity is restored if the slot quantity is below or equal to it. Otherwise, the stack capacity is
    /// set to the current quantity.
    /// </remarks>
    public void Dispose() {
      if (_storedPart.quantity == _originalQuantity) {
        _storedPart.stackCapacity = _originalStackCapacity;
        _storedPart.snapshot.moduleCargoStackableQuantity = _originalStackCapacity;
        return; // Nothing has changed, no need to send updates.
      }
      var newStackCapacity = Math.Max(_storedPart.quantity, _originalStackCapacity);
      if (newStackCapacity > _originalStackCapacity) {
        HostedDebugLog.Fine(
            _kisBaseModule,
            "Increasing stock slot stacking capacity: old={0}, new={1}", _originalStackCapacity, newStackCapacity);
        _storedPart.stackCapacity = newStackCapacity;
        _storedPart.snapshot.moduleCargoStackableQuantity = newStackCapacity;
      } else {
        _storedPart.stackCapacity = _originalStackCapacity;
        _storedPart.snapshot.moduleCargoStackableQuantity = _originalStackCapacity;
      }
      // The downstream listeners had a notification with the slot size modified. Let them know it's now restored.
      GameEvents.onModuleInventorySlotChanged.Fire(_kisBaseModule.stockInventoryModule, _storedPart.slotIndex);
      GameEvents.onModuleInventoryChanged.Fire(_kisBaseModule.stockInventoryModule);
    }
  }
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
  public override void OnStart(StartState state) {
    base.OnStart(state);
    var inventorySlotControl = stockInventoryModule.Fields[nameof(stockInventoryModule.InventorySlots)];
    var compatibilitySettings = KisApi.CommonConfig.compatibilitySettings;
    if (compatibilitySettings.hideStockGui && inventorySlotControl != null) {
      HostedDebugLog.Fine(this, "Disabling the stock inventory GUI settings");
      if (!compatibilitySettings.respectStockInventoryLayout || !compatibilitySettings.respectStockStackingLogic) {
        HostedDebugLog.Warning(
            this, "Some of the compatibility settings are not active! The stock GUI may (and likely will) fail.");
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
    } else if (usedVolume + item.volume > maxVolume) {
      var freeVolume = maxVolume - Math.Min(usedVolume, maxVolume);
      errors.Add(new ErrorReason() {
          shortString = VolumeTooLargeReason,
          guiString = NotEnoughVolumeText.Format(item.volume - freeVolume),
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
    foreach (var existingSlotIndex in stockInventoryModule.storedParts.Keys) {
      maxSlotIndex = Math.Max(existingSlotIndex, maxSlotIndex);
      var slot = stockInventoryModule.storedParts[existingSlotIndex];
      if (!slot.partName.Equals(item.avPart.name)) {
        continue;
      }

      // Verify if the item must not be added due to the stock inventory limit.
      if (compatibility.respectStockStackingLogic
          && !stockInventoryModule.CanStackInSlot(item.avPart, variantName, existingSlotIndex)) {
        continue;
      }

      // Verify if we can go above the stock limit.
      using (new StackCapacityScope(this, slot)) {
        if (!stockInventoryModule.CanStackInSlot(item.avPart, variantName, existingSlotIndex)) {
          continue;
        }
      }

      // In the stock inventory the part states in the slot must be exactly the same.
      var slotState =
          KisApi.PartNodeUtils.MakeComparablePartNode(
              KisApi.PartNodeUtils.GetConfigNodeFromProtoPartSnapshot(slot.snapshot));
      var itemState = KisApi.PartNodeUtils.MakeComparablePartNode(itemConfig);
      if (slotState.ToString() != itemState.ToString()) {
        continue;
      }
      return existingSlotIndex;  // Found a candidate.
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
    // Note that the index of a slot that does not exist may be returned!
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
  /// variant or resources on the part.
  /// </p>
  /// <p>
  /// This method <i>always</i> fit the item into the slot. If the slot is not stock compatible for the item, then the
  /// needed adjustments are made to the inventory.
  /// </p> 
  /// </remarks>
  /// <param name="item">The item to add.</param>
  /// <param name="stockSlotIndex">The stock slot index to add into.</param>
  /// <seealso cref="FindStockSlotForItem"/>
  void AddItemToStockSlot(InventoryItem item, int stockSlotIndex) {
    UpdateStockSlotIndex(stockSlotIndex, item); // This must go before the stock inventory change.
    SyncStockSlots(stockSlotIndex);
    if (!stockInventoryModule.storedParts.ContainsKey(stockSlotIndex)
        || stockInventoryModule.storedParts[stockSlotIndex].IsEmpty) {
      stockInventoryModule.StoreCargoPartAtSlot(
          KisApi.PartNodeUtils.GetProtoPartSnapshotFromNode(
              vessel, item.itemConfig, keepPersistentId: true), stockSlotIndex);
    } else {
      var slot = stockInventoryModule.storedParts[stockSlotIndex];
      using (new StackCapacityScope(this, slot)) {
        stockInventoryModule.UpdateStackAmountAtSlot(stockSlotIndex, slot.quantity + 1, slot.variantName);
      }
    }
  }

  /// <summary>Removes the item from a stock inventory slot.</summary>
  /// <param name="item">The item to remove.</param>
  void RemoveItemFromStockSlot(InventoryItem item) {
    var stockSlotIndex = _itemsToStockSlotMap[item.itemId];
    UpdateStockSlotIndex(stockSlotIndex, item, remove: true);
    SyncStockSlots(stockSlotIndex);
    var slot = stockInventoryModule.storedParts[stockSlotIndex];
    var newStackQuantity = slot.quantity - 1;
    if (newStackQuantity == 0) {
      stockInventoryModule.ClearPartAtSlot(stockSlotIndex);
    } else {
      // Assuming the slot was properly updated to correct the capacity.
      stockInventoryModule.UpdateStackAmountAtSlot(stockSlotIndex, newStackQuantity, slot.variantName);
    }
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

  /// <summary>Ensures that the stock inventory GUI is in sync with the extended inventory slots.</summary>
  /// <remarks>
  /// The stock logic only handles the slots within the part settings. This method ensures that the stock logic is ready
  /// to receive updates to the slots that are <i>beyond</i> the part config. Such slots may be created by KIS when it
  /// cannot fit a new item into any of the existing stock slots. They are not visible in the stock GUI.
  /// </remarks>
  /// <param name="maxSlotIndex">
  /// The slot index up to which the stock logic should be fine in handling the updates.
  /// </param>
  /// <seealso cref="FindStockSlotForItem"/>
  /// <seealso cref="AddItemToStockSlot"/>
  void SyncStockSlots(int maxSlotIndex) {
    var elementsAdded = 0;
    while (stockInventoryUiAction.slotPartIcon.Count <= maxSlotIndex) {
      var newStockSlotUi = MakeStockSlotUiObject(stockInventoryUiAction.slotPartIcon.Count);
      stockInventoryUiAction.slotPartIcon.Add(newStockSlotUi.GetComponent<EditorPartIcon>());
      stockInventoryUiAction.slotButton.Add(newStockSlotUi.GetComponent<UIPartActionInventorySlot>());
      elementsAdded++;
    }
    if (elementsAdded > 0) {
      HostedDebugLog.Fine(
          this, "Added extra stock inventory GUI elements: elements={0}, refSlot=#{1}", elementsAdded, maxSlotIndex);
    }
  }

  /// <summary>Creates a fake stock slot UI element.</summary>
  /// <remarks>This element will not be visible anywhere. It's only made to keep the stock logic consistent.</remarks>
  /// <param name="stockSlotIndex"></param>
  /// <returns>An inactive GUI element object.</returns>
  GameObject MakeStockSlotUiObject(int stockSlotIndex) {
    var slotObject = Instantiate(stockInventoryUiAction.slotPrefab, stockInventoryUiAction.contentTransform);
    slotObject.SetActive(false); // It's not a real GUI element!
    var slotUi = slotObject.AddComponent<UIPartActionInventorySlot>();
    slotUi.Setup(stockInventoryUiAction, stockSlotIndex);
    return slotObject;
  }
  #endregion
}

}  // namespace
