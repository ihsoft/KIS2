// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KISAPIv2;
using KSPDev.GUIUtils;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using KSP.UI.Screens;
using KSPDev.ConfigUtils;
using KSPDev.ProcessingUtils;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>Base module to handle inventory items. It can only hold items, no GUI is offered.</summary>
[PersistentFieldsDatabase("KIS2/settings2/KISConfig")]
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
  protected static readonly Message NonCargoPartErrorText = new Message(
      "",
      defaultTemplate: "Stock inventory limitation\nNon-cargo part",
      description: "An error that is presented when the part cannot be added into a KIS container due to it doesn't"
      + " have cargo part module. It only makes sense in the stock compatibility mode.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected static readonly Message ConstructionOnlyPartErrorText = new Message(
      "",
      defaultTemplate: "Stock inventory limitation\nConstruction Only Part",
      description: "An error that is presented when the part cannot be added into a KIS container due to it's"
      + " disallowed for inventory storing. It only makes sense in the stock compatibility mode.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected static readonly Message DifferentPartInSlotReasonText = new Message(
      "",
      defaultTemplate: "Stock inventory limitation\nDifferent part in the stock slot",
      description: "An error that is presented when the part cannot be added into the stock slot due to it already has"
      + " a different part/variant. It only makes sense in the stock compatibility mode.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected static readonly Message CannotStackInSlotReasonText = new Message(
      "",
      defaultTemplate: "Stock inventory limitation\nCannot stack in the stock slot",
      description: "An error that is presented when the part cannot be added into the stock slot due to it has the"
      + " maximum allowed stacked items. It only makes sense in the stock compatibility mode.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected static readonly Message NoFreeSlotsReasonText = new Message(
      "",
      defaultTemplate: "Stock inventory limitation\nNo free stock slots",
      description: "An error that is presented when the part cannot be added into the stock slot due to there are no "
      + " more empty slots. It only makes sense in the stock compatibility mode.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected static readonly Message NoMoreKisSlotsErrorText = new Message(
      "",
      defaultTemplate: "No free slots",
      description: "An error that is presented when the part cannot be added into a KIS container due to there no more"
      + " compatible visible slots.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected static readonly Message CannotAddIntoSelfErrorText = new Message(
      "",
      defaultTemplate: "You cannot store a part into itself. Seriously?!",
      description: "An error that is presented when the part cannot be added into a KIS container due to the inventory"
      + " is owned by that part.");

  // ReSharper enable MemberCanBePrivate.Global
  #endregion

  #region Part's config fields
  /// <summary>Maximum size of the item that can fit the container.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Vector3 maxItemSize;
  #endregion

  #region Global settings
  /// <summary>The maximum size of the slot in the stock inventory.</summary>
  /// <remarks>
  /// It only makes sense if the <c>respectStockStackingLogic</c> is set to <c>false</c>. If the stock logic is not
  /// honored, it's technically possible to put ANY number of items into one slot. However, putting "any" number of
  /// items is never a good idea. A too large slot can result in to unpredictable consequences in the stock game.
  /// </remarks>
  [PersistentField("Performance/maxStockSlotSize")]
  public int maxStockSlotSize = 99; // The default is based on the 1.12.2 parts set.
  #endregion

  #region IKISInventory properties
  /// <inheritdoc/>
  public Vessel ownerVessel => vessel;

  /// <inheritdoc/>
  public Dictionary<string, InventoryItem> inventoryItems { get; } = new();

  /// <inheritdoc/>
  public Vector3 maxInnerSize => maxItemSize;

  /// <inheritdoc/>
  public double maxVolume => stockInventoryModule.packedVolumeLimit;

  /// <inheritdoc/>
  public double usedVolume => stockInventoryModule.volumeCapacity;

  /// <inheritdoc/>
  public float contentMass =>
      _contentMass ??= stockInventoryModule.GetModuleMass(part.mass, ModifierStagingSituation.CURRENT);
  float? _contentMass;

  /// <inheritdoc/>
  public float contentCost =>
      _contentCost ??= stockInventoryModule.GetModuleCost(part.partInfo.cost, ModifierStagingSituation.CURRENT);
  float? _contentCost;
  #endregion

  #region Check reasons
  // ReSharper disable MemberCanBeProtected.Global

  /// <summary>
  /// The error reason type in the case when the STOCK  inventory cannot accept the item due to its layout setup.
  /// </summary>
  /// <remarks>
  /// THis error type relates to the compatibility settings. When all the settings are disabled, this error reason is
  /// not expected to be seen.
  /// </remarks>
  /// <seealso cref="ErrorReason.errorClass"/>
  /// <seealso cref="NonCargoPartErrorText"/>
  /// <seealso cref="ConstructionOnlyPartErrorText"/>
  public const string StockInventoryLimitReason = "StockInventoryLimit";

  /// <summary>The error reason type of the case when KIS doesn't yet have the proper code.</summary>
  /// <remarks>
  /// KIS must be able to deal with any items, except the compatibility settings enabled. However, KIS may not have the
  /// proper support for the feature yet. This is when this error type may appear.
  /// </remarks>
  /// <seealso cref="ErrorReason.errorClass"/>
  public const string KisNotImplementedReason = "KisNotImplemented";

  /// <summary>The error reason type of the case when the item is too large to be added into the inventory.</summary>
  /// <remarks>
  /// Note, that it's recognized as the "item's problem". The inventory can be large enough to accomodate large items,
  /// but the free volume may be limited. So, if a single item is too big to fit, it's "its own problem".
  /// </remarks>
  /// <seealso cref="ErrorReason.errorClass"/>
  /// <seealso cref="NotEnoughVolumeText"/>
  public const string ItemVolumeTooLargeReason = "ItemVolumeLimit";

  /// <summary>The error reason type of the case when adding the item would break the inventory consistency.</summary>
  /// <remarks>
  /// This error type addresses a set of critical but not that spread set of problems. One example of the problem could
  /// be trying to add an inventory into self. There can be other examples based on the current scene.
  /// </remarks>
  /// <seealso cref="ErrorReason.errorClass"/>
  /// <seealso cref="CannotAddIntoSelfErrorText"/>
  public const string InventoryConsistencyReason = "InventoryConsistency";

  // ReSharper enable MemberCanBeProtected.Global
  #endregion

  #region Inhertitable fields and properties
  /// <summary>Returns the stock inventory module on this part, which KIS is being shadowing.</summary>
  /// <value>The stock module instance.</value>
  /// <exception cref="InvalidOperationException"> if no stock module found.</exception>
  protected ModuleInventoryPart stockInventoryModule =>
      _stockInventoryModule ??= part.Modules.OfType<ModuleInventoryPart>().First();
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

    /// <summary>Temporarily increases the stack capacity and restores it back on the disposal.</summary>
    /// <remarks>
    /// The stack capacity will be set to a high (but limited) value to let the stock logic accommodating more items. On
    /// the scope exit, either the original setting will be restored or a new, bigger value, will be set.
    /// </remarks>
    /// <param name="kisBaseModule">The KIS module on which behalf the action is being performed.</param>
    /// <param name="storedPart">The stock slot to modify.</param>
    /// <param name="maxStackCapacity">The stack limit capacity to set while in the scope.</param>
    public StackCapacityScope(KisContainerBase kisBaseModule, StoredPart storedPart, int maxStackCapacity) {
      _kisBaseModule = kisBaseModule;
      _storedPart = storedPart;
      _originalStackCapacity = storedPart.stackCapacity;
      _originalQuantity = storedPart.quantity;
      storedPart.stackCapacity = maxStackCapacity;
      storedPart.snapshot.moduleCargoStackableQuantity = maxStackCapacity;
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
    ConfigAccessor.ReadFieldsInType(GetType(), this);
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
    if (StockCompatibilitySettings.hideStockGui && inventorySlotControl != null) {
      HostedDebugLog.Fine(this, "Disabling the stock inventory GUI settings");
      inventorySlotControl.guiActive = false;
      inventorySlotControl.guiActiveEditor = false;
    }
    RegisterGameEventListener(GameEvents.onModuleInventorySlotChanged, OnModuleInventorySlotChangedEvent);
  }
  #endregion

  #region IKISInventory implementation
  /// <inheritdoc/>
  public List<ErrorReason> CheckCanAddPart(string partName, int stockSlotIndex = -1, bool logErrors = false) {
    ArgumentGuard.NotNullOrEmpty(partName, nameof(partName), context: this);
    return CheckCanAddItem(InventoryItemImpl.ForPartName(null, partName), stockSlotIndex, logErrors);
  }

  /// <inheritdoc/>
  public virtual List<ErrorReason> CheckCanAddItem(
      InventoryItem item, int stockSlotIndex = -1, bool logErrors = false) {
    ArgumentGuard.NotNull(item, nameof(item), context: this);
    var errors = new List<ErrorReason>();

    // Check if the item is allowing to change the owner when it's the case.
    if (!ReferenceEquals(item.inventory, this)) {
      errors.AddRange(item.CheckCanChangeOwnership());
      if (errors.Count > 0) {
        return ReportAndReturnCheckErrors(item, errors, logErrors);
      }
    }

    // Check if the inventory is being added into self. Obviously, a bad idea.
    if (part.craftID == item.snapshot.craftID
        || part.persistentId == item.snapshot.persistentId) {
      errors.Add(new ErrorReason() {
          errorClass = InventoryConsistencyReason,
          guiString = CannotAddIntoSelfErrorText,
      });
      return ReportAndReturnCheckErrors(item, errors, logErrors);
    }

    // Check if the compatibility settings restrict item adding/moving.
    if (StockCompatibilitySettings.isCompatibilityMode) {
      var cargoModule = item.avPart.partPrefab.GetComponent<ModuleCargoPart>();
      if (cargoModule == null) {
        errors.Add(new ErrorReason() {
            errorClass = StockInventoryLimitReason,
            guiString = NonCargoPartErrorText,
        });
        return ReportAndReturnCheckErrors(item, errors, logErrors);
      }
      if (cargoModule.packedVolume < 0.0f) {
        errors.Add(new ErrorReason() {
            errorClass = StockInventoryLimitReason,
            guiString = ConstructionOnlyPartErrorText,
        });
        return ReportAndReturnCheckErrors(item, errors, logErrors);
      }
    }
    if (FindStockSlotForItem(item, stockSlotIndex, out errors) == -1) {
      return ReportAndReturnCheckErrors(item, errors, logErrors);
    }

    // Finally, verify the volume limit.
    if (usedVolume + item.volume > maxVolume) {
      var freeVolume = maxVolume - Math.Min(usedVolume, maxVolume);
      errors.Add(new ErrorReason() {
          errorClass = ItemVolumeTooLargeReason,
          guiString = NotEnoughVolumeText.Format(item.volume - freeVolume),
      });
      return ReportAndReturnCheckErrors(item, errors, logErrors);
    }

    return errors;  // It must be empty at this point.
  }

  /// <inheritdoc/>
  public InventoryItem AddPart(string partName, int stockSlotIndex = -1) {
    ArgumentGuard.NotNullOrEmpty(partName, nameof(partName), context: this);
    return AddItem(InventoryItemImpl.ForPartName(null, partName));
  }

  /// <inheritdoc/>
  public virtual InventoryItem AddItem(InventoryItem item, int stockSlotIndex = -1) {
    ArgumentGuard.NotNull(item, nameof(item), context: this);
    if (item.inventory != null) {
      throw new InvalidOperationException(Preconditions.MakeContextError(this, "The new item must be detached"));
    }
    stockSlotIndex = FindStockSlotForItem(item, stockSlotIndex, out var errors);
    if (errors.Count > 0) {
      ReportAndReturnCheckErrors(item, errors, true);
      return null;
    }
    var newItem = InventoryItemImpl.FromSnapshot(this, item.snapshot);
    AddItemToStockSlot(newItem, stockSlotIndex);
    AddInventoryItem(newItem);
    return newItem;
  }

  /// <inheritdoc/>
  public virtual InventoryItem DeleteItem(InventoryItem item) {
    ArgumentGuard.NotNull(item, nameof(item), context: this);
    if (!ReferenceEquals(item.inventory, this)) {
      HostedDebugLog.Error(this, "Item doesn't belong to this inventory: name={0}, id={1}, owner={2}",
                           item.avPart.name, item.itemId, item.inventory as PartModule);
      return null;
    }
    if (!inventoryItems.ContainsKey(item.itemId)) {
      HostedDebugLog.Error(this, "Item not found: name={0}, id={1}", item.avPart.name, item.itemId);
      return null;
    }
    if (item.isLocked) {
      HostedDebugLog.Error(this, "Cannot delete locked item(s): name={0}, id={1}", item.avPart.name, item.itemId);
      return null;
    }
    RemoveInventoryItem(item);
    RemoveItemFromStockSlot(item);
    return InventoryItemImpl.FromSnapshot(null, item.snapshot);
  }

  /// <inheritdoc/>
  public virtual void UpdateInventory(ICollection<InventoryItem> changedItems = null) {
    _contentCost = null;
    _contentMass = null;
  }

  /// <inheritdoc/>
  public InventoryItem FindItem(string itemId) {
    ArgumentGuard.NotNullOrEmpty(itemId, nameof(itemId), context: this);
    inventoryItems.TryGetValue(itemId, out var res);
    return res;
  }
  #endregion

  #region API methods
  /// <summary>Returns a stock inventory limitation error reason response.</summary>
  public static List<ErrorReason> ReturnStockInventoryErrorReasons(string reasonText, string logDetails = null) {
    var reason = new ErrorReason() {
        errorClass = StockInventoryLimitReason,
        guiString = reasonText,
        logDetails = logDetails,
    };
    return new List<ErrorReason> { reason };
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
    inventoryItems.Add(item.itemId, item);
  }

  /// <summary>Removes the item from the <see cref="inventoryItems"/> collection.</summary>
  /// <remarks>
  /// This method doesn't deal with the stock inventory update and only serves the purpose of the internal KIS state
  /// update. The descendants can use it to react to the changes on the KIS inventory. 
  /// </remarks>
  /// <param name="item">The item to remove.</param>
  protected virtual void RemoveInventoryItem(InventoryItem item) {
    if (!inventoryItems.Remove(item.itemId)) {
      HostedDebugLog.Error(this, "Cannot delete item, not in the index: itemId={0}", item.itemId);
    }
  }

  /// <summary>Verifies if the item can fit the stock slot.</summary>
  /// <param name="item">The item to check.</param>
  /// <param name="stockSlotIndex">The stock slot to check for.</param>
  /// <param name="quantity">The quantity of the items to try to fit into the slot.</param>
  /// <returns>The list of check errors. It's empty if items can be stored into the slot.</returns>
  protected List<ErrorReason> CheckSlotStockForItem(InventoryItem item, int stockSlotIndex, int quantity = 1) {
    var errors = new List<ErrorReason>();
    if (!stockInventoryModule.storedParts.ContainsKey(stockSlotIndex)) {
      return errors;
    }
    var stockSlot = stockInventoryModule.storedParts[stockSlotIndex];
    var maxSlotStackCapacity = StockCompatibilitySettings.isCompatibilityMode
        ? stockSlot.stackCapacity
        : maxStockSlotSize;
    using (new StackCapacityScope(this, stockSlot, maxSlotStackCapacity)) {
      if (stockSlot.stackCapacity - stockSlot.quantity < quantity) {
        return ReturnStockInventoryErrorReasons(
            CannotStackInSlotReasonText,
            logDetails: string.Format("Stack capacity reached in stock slot: index={0}, quantity={1}, stackLimit={2}",
                                      stockSlotIndex, stockSlot.quantity, stockSlot.stackCapacity));
      }
      if (!stockInventoryModule.CanStackInSlot(item.avPart, item.variantName, stockSlotIndex)) {
        return ReturnStockInventoryErrorReasons(
            DifferentPartInSlotReasonText,
            logDetails: $"Incompatible stock slot: index={stockSlotIndex}, slotPart={stockSlot.partName}");
      }
    }
    return errors;
  }
  #endregion

  #region Local utility methods
  /// <summary>Finds the stock slot index for KIS item.</summary>
  /// <param name="item">The item to get the slot for.</param>
  /// <returns>The sock slot of this inventory or <c>-1</c> if the item is not stored in this inventory.</returns>
  /// <seealso cref="InventoryItem.stockSlotIndex"/>
  internal int GetStockSlotForItem(InventoryItem item) {
    if (_itemsToStockSlotMap.TryGetValue(item.itemId, out var stockSlotIndex)) {
      return stockSlotIndex;
    }
    return -1;
  }

  /// <summary>Creates an item from a non-empty stock slot and updates stock related indexes.</summary>
  /// <remarks>
  /// The new item is not added to <see cref="inventoryItems"/>, but it's expected that it will be added there by the
  /// calling code. A normal way of doing it is calling <see cref="AddItem"/> or <see cref="DeleteItem"/>.
  /// </remarks>
  /// <param name="stockSlotIndex">The slot index in the stock inventory module.</param>
  /// <param name="itemId">
  /// An optional ID to assign to the new item. If omitted, then a new unique ID will be generated.
  /// </param>
  /// <returns>A new item that was created.</returns>
  /// <seealso cref="stockInventoryModule"/>
  InventoryItem MakeItemFromStockSlot(int stockSlotIndex, string itemId = null) {
    var item = InventoryItemImpl.FromSnapshot(
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
  /// <param name="stockSlotIndex">
  /// Optional stock slot index to place the item into. If provided, then the other slots will not be considered to find
  /// a suitable slot. To enable searching set this parameter to <c>-1</c>.
  /// </param>
  /// <param name="errors">The list of errors that were detected during searching the compatible slot.</param>
  /// <returns>
  /// The stock slot index that can accept the item or <c>-1</c> if there are none. If no slot was found, the
  /// <paramref name="errors"/> list will not be empty.
  /// </returns>
  /// <seealso cref="StockCompatibilitySettings"/>
  int FindStockSlotForItem(InventoryItem item, int stockSlotIndex, out List<ErrorReason> errors) {
    errors = new List<ErrorReason>(); 
    var maxSlotIndex = -1;

    // When the stock slot is provided, only run the checks.
    if (stockSlotIndex >= 0) {
      errors = CheckSlotStockForItem(item, stockSlotIndex);
      return errors.Count == 0 ? stockSlotIndex : -1;
    }

    // Try to fit the item into an existing occupied slot to preserve the space.
    foreach (var existingSlotIndex in stockInventoryModule.storedParts.Keys) {
      maxSlotIndex = Math.Max(existingSlotIndex, maxSlotIndex);
      var slot = stockInventoryModule.storedParts[existingSlotIndex];
      if (!slot.partName.Equals(item.avPart.name) || item.resources.Length > 0) {
        // Absolutely no stacking for different parts or parts with resources.
        continue;
      }

      // Verify if the slot cannot be used due to the stock inventory limit.
      if (CheckSlotStockForItem(item, existingSlotIndex).Count > 0) {
        continue;
      }

      // In the stock inventory the part states in the slot must be exactly the same.
      var slotState = KisApi.PartNodeUtils.MakeComparablePartNode(slot.snapshot);
      var itemState = KisApi.PartNodeUtils.MakeComparablePartNode(item.snapshot);
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
    if (StockCompatibilitySettings.isCompatibilityMode) {
      errors = ReturnStockInventoryErrorReasons(
          NoFreeSlotsReasonText,
          logDetails: string.Format("Cannot find a compatible stock slot and cannot create a new one"));
      return -1;
    }

    // Find any unused stock slot index, even if it's beyond the stock logic address space.
    // Note that the index of a slot that does not exist may be returned!
    slotIndex = 0;
    while (stockInventoryModule.storedParts.TryGetValue(slotIndex, out var storedPart) && !storedPart.IsEmpty) {
      slotIndex++;
    }
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
      stockInventoryModule.StoreCargoPartAtSlot(item.snapshot, stockSlotIndex);
    } else {
      var slot = stockInventoryModule.storedParts[stockSlotIndex];
      using (new StackCapacityScope(this, slot, int.MaxValue)) {
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
    if (slotQuantity != indexedItems) {
      UpdateInventory();
      // Make a delayed update to catch the stock properties updates in GUI.
      AsyncCall.CallOnEndOfFrame(this, () => UpdateInventory());
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
      if (StockCompatibilitySettings.isCompatibilityMode) {
        HostedDebugLog.Warning(
            this, "Added extra stock inventory GUI elements in the compatibility mode: elements={0}, refSlot=#{1}",
            elementsAdded, maxSlotIndex);
      } else {
        HostedDebugLog.Fine(
            this, "Added extra stock inventory GUI elements: elements={0}, refSlot=#{1}", elementsAdded, maxSlotIndex);
      }
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

  /// <summary>Reports the check errors if requested.</summary>
  /// <param name="item">The item for which the checks are being made.</param>
  /// <param name="errors">The detected errors. Can be an empty list.</param>
  /// <param name="logErrors">Indicates of the errors must be logged.</param>
  /// <returns>The <paramref name="errors"/> provided in the call.</returns>
  protected List<ErrorReason> ReportAndReturnCheckErrors(InventoryItem item, List<ErrorReason> errors, bool logErrors) {
    if (logErrors && errors.Count > 0) {
      HostedDebugLog.Error(
          this, "Cannot add '{0}' part:\n{1}", item.avPart.name,
          DbgFormatter.C2S(errors, separator: "\n", predicate: x => x.logDetails ?? x.guiString));
    }
    return errors;
  }
  #endregion
}

}  // namespace
