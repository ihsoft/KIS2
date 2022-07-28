// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KISAPIv2;
using System;
using System.Collections.Generic;
using System.Linq;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
using KSPDev.ProcessingUtils;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>Implementation for the inventory item.</summary>
sealed class InventoryItemImpl : InventoryItem {
  #region InventoryItem properties
  /// <inheritdoc/>
  public IKisInventory inventory { get; }

  /// <inheritdoc/>
  public string itemId { get; }

  /// <inheritdoc/>
  public Part materialPart { get; set; }

  /// <inheritdoc/>
  public AvailablePart avPart => snapshot.partInfo;

  /// <inheritdoc/>
  public Texture iconImage {
    get {
      UpdateVariant();
      return _iconImage ??= KisApi.PartIconUtils.MakeDefaultIcon(avPart, variantName);
    }
  }
  Texture _iconImage;

  /// <inheritdoc/>
  public ProtoPartSnapshot snapshot => stockSlot?.snapshot ?? _cachedSnapshot;
  ProtoPartSnapshot _cachedSnapshot;

  /// <inheritdoc/>
  public int stockSlotIndex { get; }

  /// <inheritdoc/>
  public StoredPart stockSlot => _stockSlot != null || inventory?.FindItem(itemId) != null
      ? _stockSlot ??= inventory.stockInventoryModule.storedParts[stockSlotIndex]
      : null;
  StoredPart _stockSlot;

  /// <inheritdoc/>
  public ProtoPartSnapshot mutableSnapshot {
    get {
      if (inventory != null) {
        throw new InvalidOperationException("Cannot modify item while it belongs to an inventory");
      }
      if (_mutableSnapshot == null) {
        _cachedSnapshot = KisApi.PartNodeUtils.FullProtoPartCopy(_cachedSnapshot);
        _mutableSnapshot = _cachedSnapshot;
      }
      return _mutableSnapshot;
    }
  }
  ProtoPartSnapshot _mutableSnapshot;

  /// <inheritdoc/>
  public PartVariant variant {
    get {
      UpdateVariant();
      return _variant ??= VariantsUtils2.GetPartVariant(avPart, variantName);
    }
  }
  PartVariant _variant;

  /// <inheritdoc/>
  public string variantName => snapshot.moduleVariantName ?? "";
  string _oldVariantName; // Used to track variant changes.

  /// <inheritdoc/>
  public double volume {
    get {
      UpdateVariant();
      return _volume ??= KisApi.PartModelUtils.GetPartVolume(avPart, variantName);
    }
  }
  double? _volume;
  
  /// <inheritdoc/>
  public Vector3 size {
    get {
      UpdateVariant();
      return _size ??= KisApi.PartModelUtils.GetPartBounds(avPart, variantName);
    }
  } 
  Vector3? _size;

  /// <inheritdoc/>
  public double dryMass => snapshot.mass - snapshot.moduleMass;
  
  /// <inheritdoc/>
  public double dryCost {
    get {
      UpdateVariant();
      return _dryCost ??= KisApi.PartPrefabUtils.GetPartDryCost(avPart, variantName);
    }
  }
  double? _dryCost;
  
  /// <inheritdoc/>
  public double fullMass => dryMass + snapshot.moduleMass + resources.Sum(r => r.amount * r.definition.density);
  
  /// <inheritdoc/>
  public double fullCost => dryCost + snapshot.moduleCosts + resources.Sum(r => r.amount * r.definition.unitCost);

  /// <inheritdoc/>
  public ProtoPartResourceSnapshot[] resources => snapshot.resources.ToArray();

  /// <inheritdoc/>
  public ScienceData[] science => _science ??= snapshot.modules
      .SelectMany(m => KisApi.PartNodeUtils.GetModuleScience(m.moduleValues))
      .ToArray();
  ScienceData[] _science;
  
  /// <inheritdoc/>
  public bool isEquipped => false;

  /// <inheritdoc/>
  public bool isLocked { get; private set; }

  /// <inheritdoc/>
  public List<Func<InventoryItem, ErrorReason?>> checkChangeOwnershipPreconditions { get; } = new();
  #endregion

  #region InventoryItem implementation
  /// <inheritdoc/>
  public void SetLocked(bool newState) {
    isLocked = newState;
  }

  /// <inheritdoc/>
  public void SyncToSnapshot() {
    _science = null;
    UpdateVariant();
  }

  /// <inheritdoc/>
  public List<ErrorReason> CheckCanChangeOwnership() {
    return checkChangeOwnershipPreconditions
        .Select(fn => fn(this))
        .Where(x => x.HasValue)
        .Select(x => x.Value)
        .ToList();
  }
  #endregion

  #region API methods
  /// <summary>Creates an attached item from a stock slot.</summary>
  /// <param name="inventory">The inventory to bind the item to. It must not be NULL.</param>
  /// <param name="stockSlotIndex">
  /// The stock slot to get the data from and associate with this item. This slot must not be empty.
  /// </param>
  /// <param name="itemId">The item ID or NULL if a new random value needs to be made.</param>
  /// <returns>A new item.</returns>
  public static InventoryItemImpl FromStockSlot(KisContainerBase inventory, int stockSlotIndex, string itemId = null) {
    return new InventoryItemImpl(
        inventory.stockInventoryModule.storedParts[stockSlotIndex].snapshot, inventory, stockSlotIndex, itemId);
  }

  /// <summary>Creates an attached item from another item.</summary>
  /// <remarks>The new item inherits the source item ID and shares its snapshot.</remarks>
  /// <param name="inventory">The inventory to bind the item to. It must not be NULL.</param>
  /// <param name="item">
  /// The item to copy from. The new instance will have the same item ID as the source, and the source's snapshot will
  /// be shared.
  /// </param>
  /// <param name="stockSlotIndex">The stock slot to associate with this item.</param>
  /// <returns>A new item.</returns>
  public static InventoryItemImpl FromItem(KisContainerBase inventory, InventoryItem item, int stockSlotIndex) {
    return new InventoryItemImpl(item.snapshot, inventory, stockSlotIndex, item.itemId);
  }

  /// <summary>Creates a detached item from a proto part snapshot.</summary>
  /// <param name="snapshot">
  /// The snapshot to make the item from. All items created from the same snapshot will share the instance.
  /// </param>
  /// <returns>A new item.</returns>
  public static InventoryItemImpl FromSnapshot(ProtoPartSnapshot snapshot) {
    return new InventoryItemImpl(snapshot);
  }
  
  /// <summary>Creates a detached item from an active part.</summary>
  /// <remarks>The current part's state will be captured.</remarks>
  /// <param name="part">The part to take a snapshot from.</param>
  /// <returns>A new detached item.</returns>
  public static InventoryItemImpl FromPart(Part part) {
    return new InventoryItemImpl(KisApi.PartNodeUtils.GetProtoPartSnapshot(part));
  }

  /// <summary>Creates a detached item for the given part name.</summary>
  /// <remarks>The part state is captured from the prefab. Use <see cref="snapshot"/> to customize the state.</remarks>
  /// <param name="partName">The name of the part to create.</param>
  /// <returns>A new item.</returns>
  /// <exception cref="InvalidOperationException">If the part name cannot be found.</exception>
  public static InventoryItemImpl ForPartName(string partName) {
    var partInfo = PartLoader.getPartInfoByName(partName);
    Preconditions.NotNull(partInfo, message: "Part name not found: " + partName);
    return new InventoryItemImpl(KisApi.PartNodeUtils.GetProtoPartSnapshot(partInfo.partPrefab));
  }
  #endregion

  #region System overrides
  public override int GetHashCode() {
    return itemId.GetHashCode(); // Allow the item to be properly handled in the sets and dictionaries.
  }
  #endregion

  #region Local utility methods

  /// <summary>Makes a new item from the part snapshot.</summary>
  /// <param name="snapshot">The part's snapshot. It's never NULL.</param>
  /// <param name="inventory">The inventory to bind the item to or NULL if item is detached.</param>
  /// <param name="stockSlotIndex">The index of the stock slot or -1 if item is detached.</param>
  /// <param name="newItemId">The item ID or NULL if a new random value needs to be made.</param>
  InventoryItemImpl(ProtoPartSnapshot snapshot, KisContainerBase inventory = null, int stockSlotIndex = -1,
                    string newItemId = null) {
    this.inventory = inventory;
    this.stockSlotIndex = stockSlotIndex;
    _cachedSnapshot = snapshot;
    _oldVariantName = snapshot.moduleVariantName ?? "";
    itemId = NewItemId(newItemId);
  }

  /// <summary>Verifies if the variant has changed on the item and resets the related caches.</summary>
  /// <remarks>
  /// <p>
  /// Call this method each time a snapshot derived value that may depend on the variant is being read. This method will
  /// reset all the cached values that were extracted from the previous snapshot.
  /// </p>
  /// <p>This method is intended to be fast. Don't put heavy logic into it.</p>
  /// </remarks>
  void UpdateVariant() {
    if (_oldVariantName == snapshot.moduleVariantName) {
      return; // This decision must be done as fast as possible.
    }
    DebugEx.Info("Update variant on the item: itemId={0}, cachedVariant={1}, newVariant={2}",
                 itemId, _oldVariantName, snapshot.moduleVariantName);
    _oldVariantName = snapshot.moduleVariantName;
    _variant = null;
    _volume = null;
    _size = null;
    _dryCost = null;
    _iconImage = null;
  }

  /// <summary>Returns a unique item ID if none was provided.</summary>
  static string NewItemId(string providedId) {
    return providedId ?? Guid.NewGuid().ToString();
  }
  #endregion
}

}  // namespace
