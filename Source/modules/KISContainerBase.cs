// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KISAPIv2;
using KSPDev.GUIUtils;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
using System;
using System.Collections;
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
      defaultTemplate: "Cannot store part into itself",
      description: "An error that is presented when the part cannot be added into a KIS container due to the inventory"
      + " is owned by that part.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected static readonly Message CannotAddCrewedPartErrorText = new Message(
      "",
      defaultTemplate: "Part has live crew onboard",
      description: "An error that is presented when the part cannot be added into a KIS container due to there crew"
      + " members sitting in it.");

  // ReSharper enable MemberCanBePrivate.Global
  #endregion

  #region Part's config fields
  /// <summary>Maximum size of the item that can fit the container.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Vector3 maxItemSize;

  /// <summary>Index of the stock inventory module ot bind this KIS inventory to.</summary>
  /// <remarks>Specify it if the part has more than one stock inventory modules.</remarks>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public int stockInventoryModuleIndex;
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
  public IReadOnlyDictionary<string, InventoryItem> inventoryItems => _mutableInventoryItems;
  readonly Dictionary<string, InventoryItem> _mutableInventoryItems = new();

  /// <inheritdoc/>
  public ModuleInventoryPart stockInventoryModule =>
      _stockInventoryModule ??= part.Modules.OfType<ModuleInventoryPart>().Skip(stockInventoryModuleIndex).First();
  ModuleInventoryPart _stockInventoryModule;

  /// <inheritdoc/>
  public Vector3 maxInnerSize => maxItemSize;

  /// <inheritdoc/>
  public double maxVolume => stockInventoryModule.packedVolumeLimit;

  /// <inheritdoc/>
  public double usedVolume => stockInventoryModule.volumeCapacity;

  /// <inheritdoc/>
  public float contentMass => stockInventoryModule.GetModuleMass(part.mass, ModifierStagingSituation.CURRENT);

  /// <inheritdoc/>
  public float contentCost => stockInventoryModule.GetModuleCost(part.partInfo.cost, ModifierStagingSituation.CURRENT);
  #endregion

  #region Check reasons
  // ReSharper disable MemberCanBeProtected.Global

  /// <summary>
  /// The error reason type in the case when the STOCK  inventory cannot accept the item due to its layout setup.
  /// </summary>
  /// <remarks>
  /// This error type relates to the compatibility settings. When all the settings are disabled, this error reason is
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
  /// TODO(ihsoft): Drop this reason once the code is complete.
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
            .FirstOrDefault(x => x != null);
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

  /// <summary>Indicates that the stock update event handlers must not react on the calls.</summary>
  /// <remarks>This flag is set when the internal state is being updated.</remarks>
  bool _skipStockInventoryEvents;
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
    _mutableInventoryItems.Clear();
    foreach (var stockSlot in stockInventoryModule.storedParts.Values) {
      var slotIndex = stockSlot.slotIndex;
      var stockSlotItemIds = stockSlotToItemIdsMap.ContainsKey(slotIndex)
          ? stockSlotToItemIdsMap[slotIndex].ToList()
          : new List<string>();
      // Add the missing IDs. This is not expected, but we catch up.
      while (stockSlotItemIds.Count < stockSlot.quantity) {
        var newId = Guid.NewGuid().ToString();
        if (node.name != "") {
          HostedDebugLog.Warning(
              this, "Stock item doesn't have ID mapping: slotIndex={0}, slotPos={1}, newId={2}", slotIndex,
              stockSlotItemIds.Count, newId);
        }
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
  public InventoryItem MakeItem(string partName) {
    ArgumentGuard.NotNullOrEmpty(partName, nameof(partName), context: this);
    var partInfo = PartLoader.getPartInfoByName(partName);
    Preconditions.NotNull(partInfo, message: "Part name not found: " + partName);
    return InventoryItemImpl.FromSnapshotDetached(KisApi.PartNodeUtils.GetProtoPartSnapshot(partInfo.partPrefab));
  }

  /// <inheritdoc/>
  public virtual List<ErrorReason> CheckCanAddItem(
      InventoryItem item, int stockSlotIndex = -1, bool logErrors = false) {
    ArgumentGuard.NotNull(item, nameof(item), context: this);
    var errors = new List<ErrorReason>();

    // Check if the item is allowing to change the owner when it's the case.
    if (item.inventory != null && !ReferenceEquals(item.inventory, this)) {
      errors.AddRange(item.CheckCanChangeOwnership());
      if (errors.Count > 0) {
        return ReportAndReturnCheckErrors(item, errors, logErrors);
      }
    }

    // Check if the inventory is being added into self. Obviously, a bad idea.
    if (part.craftID == item.snapshot.craftID || part.persistentId == item.snapshot.persistentId) {
      errors.Add(new ErrorReason() {
          errorClass = InventoryConsistencyReason,
          guiString = CannotAddIntoSelfErrorText,
      });
      return ReportAndReturnCheckErrors(item, errors, logErrors);
    }

    // Parts with crew cannot be stored.
    if (item.snapshot.protoModuleCrew.Count > 0) {
      errors.Add(new ErrorReason() {
          errorClass = InventoryConsistencyReason,
          guiString = CannotAddCrewedPartErrorText,
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

    // Finally, verify the volume limit. It must be the last check.
    if (!ReferenceEquals(item.inventory, this) && usedVolume + item.volume > maxVolume) {
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
    var newItem = AddItemToStockSlot(item.snapshot, stockSlotIndex);//FIXME: flatten
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
    var detachedItem = RemoveItemFromStockSlot(item);
    return detachedItem;
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
    _mutableInventoryItems.Add(item.itemId, item);
  }

  /// <summary>Removes the item from the <see cref="inventoryItems"/> collection.</summary>
  /// <remarks>
  /// This method doesn't deal with the stock inventory update and only serves the purpose of the internal KIS state
  /// update. The descendants can use it to react to the changes on the KIS inventory. 
  /// </remarks>
  /// <param name="item">The item to remove.</param>
  protected virtual void RemoveInventoryItem(InventoryItem item) {
    if (!_mutableInventoryItems.Remove(item.itemId)) {
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
    var item = InventoryItemImpl.FromStockSlot(this, stockSlotIndex, itemId);
    AddToStockSlotIndex(item, stockSlotIndex);
    return item;
  }

  /// <summary>Adds the item to the internal index.</summary>
  /// <param name="item">The item to add. It must be a new unique item.</param>
  /// <param name="stockSlotIndex">The stock slot index to update the index for.</param>
  void AddToStockSlotIndex(InventoryItem item, int stockSlotIndex) {
    InitializeStockSlots(stockSlotIndex);
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
  }

  /// <summary>Removes the item from the internal index.</summary>
  /// <param name="item">The item to remove. It must be valid and belonging to the inventory.</param>
  void RemoveFromStockSlotIndex(InventoryItem item) {
    var stockSlotIndex = item.stockSlotIndex;
    if (!_stockSlotToItemsMap.ContainsKey(stockSlotIndex) || !_stockSlotToItemsMap[stockSlotIndex].Remove(item)) {
      HostedDebugLog.Warning(
          this, "Item not found in slot to item index: stockSlot={0}, itemId={1}", stockSlotIndex, item.itemId);
    }
    if (!_itemsToStockSlotMap.Remove(item.itemId)) {
      HostedDebugLog.Warning(
          this, "Item not found in item to slot index: stockSlot={0}, itemId={1}", stockSlotIndex, item.itemId);
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

      // Ensures that parts that have different internal state don't get added into the same stock slot.
      if (!CheckProtoModulesSame(slot.snapshot, item.snapshot)) {
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
          logDetails: "Cannot find a compatible stock slot and cannot create a new one");
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

  /// <summary>Verifies that to proto part have the same module configs.</summary>
  bool CheckProtoModulesSame(ProtoPartSnapshot part1, ProtoPartSnapshot part2) {
    var modules1 = part1.modules;
    var modules2 = part2.modules;
    if (modules1.Count != modules2.Count) {
      return false;
    }
    for (var i = modules1.Count - 1; i >= 0; i--) {
      if (!PartNodeUtils2.CompareNodes(modules1[i].moduleValues, modules2[i].moduleValues, x => x == "MODULE")) {
        return false;
      }
    }
    return true;
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
  InventoryItem AddItemToStockSlot(ProtoPartSnapshot source, int stockSlotIndex) {
    var item = InventoryItemImpl.FromSnapshotAttached(this, source, stockSlotIndex);
    AddToStockSlotIndex(item, stockSlotIndex);
    try {
      _skipStockInventoryEvents = true;
      if (!stockInventoryModule.storedParts.ContainsKey(stockSlotIndex)
          || stockInventoryModule.storedParts[stockSlotIndex].IsEmpty) {
        stockInventoryModule.StoreCargoPartAtSlot(source, stockSlotIndex);
      } else {
        var slot = stockInventoryModule.storedParts[stockSlotIndex];
        using (new StackCapacityScope(this, slot, int.MaxValue)) {
          stockInventoryModule.UpdateStackAmountAtSlot(stockSlotIndex, slot.quantity + 1, slot.variantName);
        }
      }
    } finally {
      // We don't want this flag to stick if any problem happens.
      _skipStockInventoryEvents = false;
    }
    return item;
  }

  /// <summary>Removes the item from a stock inventory slot.</summary>
  /// <param name="item">The item to remove.</param>
  InventoryItem RemoveItemFromStockSlot(InventoryItem item) {
    var stockSlotIndex = item.stockSlotIndex;
    var detachedItem = InventoryItemImpl.FromSnapshotDetached(item.snapshot);
    InitializeStockSlots(stockSlotIndex);  // Ensure the state is good up to this index.
    var slot = stockInventoryModule.storedParts[stockSlotIndex];
    var newStackQuantity = slot.quantity - 1;
    try {
      _skipStockInventoryEvents = true;
      if (newStackQuantity == 0) {
        stockInventoryModule.ClearPartAtSlot(stockSlotIndex);
      } else {
        // Assuming the slot was properly updated to correct the capacity.
        stockInventoryModule.UpdateStackAmountAtSlot(stockSlotIndex, newStackQuantity, slot.variantName);
      }
    } finally {
      // We don't want this flag to stick if any problem happens.
      _skipStockInventoryEvents = false;
    }
    RemoveFromStockSlotIndex(item);
    return detachedItem;
  }

  /// <summary>Reacts on the stock inventory change and updates the KIS inventory accordingly.</summary>
  void OnModuleInventorySlotChangedEvent(ModuleInventoryPart changedStockInventoryModule, int stockSlotIndex) {
    if (_skipStockInventoryEvents || !ReferenceEquals(changedStockInventoryModule, stockInventoryModule)) {
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
        RemoveInventoryItem(item);
        RemoveFromStockSlotIndex(item);
      }
    }
    if (slotQuantity > indexedItems) {
      while (!_stockSlotToItemsMap.ContainsKey(stockSlotIndex)
          || slotQuantity > _stockSlotToItemsMap[stockSlotIndex].Count) {
        var item = MakeItemFromStockSlot(stockSlotIndex);
        AddInventoryItem(item);
        HostedDebugLog.Info(
            this, "Adding an item due to the stock slot change: slot={0}, part={1}, itemId={2}",
            stockSlotIndex, item.avPart.name, item.itemId);
      }
    }
  }

  /// <summary>Ensures that the stock inventory GUI is in sync with the stock inventory slots.</summary>
  /// <remarks>
  /// The stock logic requires that every slot has a GUI handler established, and those handlers are only setup for the
  /// slots up to the maximum number in the part config. This method ensures that any slots created beyond the part
  /// config are properly initialized.
  /// </remarks>
  /// <param name="minSlotIndex">
  /// The slot index up to which the stock logic should be fine in handling the updates.
  /// </param>
  void InitializeStockSlots(int minSlotIndex = -1) { //FIXME: rename
    if (stockInventoryUiAction == null) {
      return;
    }
    var needSlots = Math.Max(
        Math.Max(stockInventoryModule.InventorySlots, stockInventoryModule.storedParts.Count), minSlotIndex + 1);
    if (stockInventoryUiAction.slotPartIcon.Count == needSlots) {
      return;
    }
    stockInventoryUiAction.slotPartIcon.Clear();
    stockInventoryUiAction.slotButton.Clear();
    for (var i = 0; i < needSlots; i++) {
      var newStockSlotUi = MakeStockSlotUiObject(i);
      stockInventoryUiAction.slotPartIcon.Add(newStockSlotUi.GetComponent<EditorPartIcon>());
      stockInventoryUiAction.slotButton.Add(newStockSlotUi.GetComponent<UIPartActionInventorySlot>());
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

/// <summary>
/// Handler that waits till the kerbal part is fully initialed and simulates a load method call for it.
/// </summary>
/// <remarks>
/// When kerbal goes EVA in flight, its part doesn't get <c>OnLoad</c> method called. Instead, the game populates
/// kerbal's inventory bypassing the generic inventory logic. Thus, it's not possible to react on default items
/// addition. This handler resolves it by reacting to EVA event and simulating the <c>OnLoad</c> call on the KIS
/// inventory module as it would happen if the game was loaded with a kerbal EVA.    
/// </remarks>
/// FIXME: not a part of module
[KSPAddon(KSPAddon.Startup.Flight, false /*once*/)]
sealed class SetupEvaHandler : MonoBehaviour {
  void Awake() {
    GameEvents.onCrewOnEva.Add(OnCrewOnEvaEvent);
  }

  /// <summary>Overridden from MonoBehaviour.</summary>
  void OnDestroy() {
    GameEvents.onCrewOnEva.Remove(OnCrewOnEvaEvent);
  }

  /// <summary>Reacts on kerbal leaving spacecraft and becoming EVA.</summary>
  void OnCrewOnEvaEvent(GameEvents.FromToAction<Part, Part> fv) {
    fv.to.FindModulesImplementing<IKisInventory>().ForEach(x => x.ownerVessel.StartCoroutine(WaitAndLoadModule(x)));
  }

  // ReSharper disable once MemberCanBeMadeStatic.Local
  IEnumerator WaitAndLoadModule(IKisInventory inventory) {
    var part = inventory.ownerVessel.rootPart;
    while (true) {
      yield return new WaitForEndOfFrame();
      if (part == null || part.State == PartStates.DEAD) {
        yield break;  // End coroutine.
      }
      if (part.State == PartStates.IDLE
          && inventory.stockInventoryModule != null
          && inventory.stockInventoryModule.storedParts != null) {
        break;  // The part is ready to be loaded.
      }
    }
    var module = inventory as PartModule;
    HostedDebugLog.Info(module.part, "Simulating OnLoad on the EVA kerbal");
    module.OnLoad(new ConfigNode(""));
  }
}

}  // namespace
