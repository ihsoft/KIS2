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
  public IKisInventory inventory => _inventory;
  readonly KisContainerBase _inventory;

  /// <inheritdoc/>
  public string itemId { get; }

  /// <inheritdoc/>
  public int stockSlotIndex => _inventory != null ? _inventory.GetStockSlotForItem(this) : -1;

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
  public ProtoPartSnapshot snapshot { get; }

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
  public ProtoPartResourceSnapshot[] resources => _resources ??= snapshot.resources.ToArray();
  ProtoPartResourceSnapshot[] _resources;

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
  public List<Func<InventoryItem, ErrorReason?>> checkChangeOwnershipPreconditions { get; private set; } = new();
  #endregion

  #region InventoryItem implementation
  /// <inheritdoc/>
  public void SetLocked(bool newState) {
    //FIXME: notify inventory? or let caller doing it?
    isLocked = newState;
  }

  /// <inheritdoc/>
  public void UpdateItem() {
    _resources = null;
    _science = null;
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
  /// <summary>Creates an item from a proto part snapshot.</summary>
  /// <param name="inventory">The inventory to bind the item to.</param>
  /// <param name="snapshot">The snapshot to make the item from.</param>
  /// <param name="itemId">An optional item ID. If not set, a new unique value will be generated.</param>
  /// <returns>A new item.</returns>
  public static InventoryItemImpl FromProtoPartSnapshot(
      KisContainerBase inventory, ProtoPartSnapshot snapshot, string itemId = null) {
    return new InventoryItemImpl(inventory, snapshot: snapshot, NewItemId(itemId));
  }

  /// <summary>Creates an item from another item.</summary>
  /// <remarks>This method makes just  a new wrapper around tey existing item.</remarks>
  /// <param name="inventory">
  /// The inventory to bind the item to. It can be <c>null</c> for the intermediate items.
  /// </param>
  /// <param name="item">
  /// The item to copy from. There will be no deep copy made, so the source item must be destroyed in case of the "copy"
  /// is adopted by any inventory.
  /// </param>
  /// <returns>A new item.</returns>
  public static InventoryItemImpl FromItem(KisContainerBase inventory, InventoryItem item) {
    return new InventoryItemImpl(inventory, item.snapshot, NewItemId(null));
  }

  /// <summary>Creates an item from an active part.</summary>
  /// <remarks>The current part's state will be captured.</remarks>
  /// <param name="inventory">
  /// The inventory to bind the item to. It can be <c>null</c> for the intermediate items.
  /// </param>
  /// <param name="part">The part to take a snapshot from.</param>
  /// <returns>A new item.</returns>
  public static InventoryItemImpl FromPart(KisContainerBase inventory, Part part) {
    return new InventoryItemImpl(inventory, KisApi.PartNodeUtils.GetProtoPartSnapshot(part), NewItemId(null));
  }

  /// <summary>Creates an item for the given part name.</summary>
  /// <remarks>The part state is captured from the prefab. Use <see cref="snapshot"/> to customize the state.</remarks>
  /// <param name="inventory">
  /// The inventory to bind the item to. It can be <c>null</c> for the intermediate items.
  /// </param>
  /// <param name="partName">The name of the part to create.</param>
  /// <returns>A new item.</returns>
  /// <exception cref="InvalidOperationException">If the part name cannot be found.</exception>
  public static InventoryItemImpl ForPartName(KisContainerBase inventory, string partName) {
    var partInfo = PartLoader.getPartInfoByName(partName);
    Preconditions.NotNull(partInfo, message: "Part name not found: " + partName);
    return new InventoryItemImpl(
        inventory, KisApi.PartNodeUtils.GetProtoPartSnapshot(partInfo.partPrefab), NewItemId(null));
  }
  #endregion

  #region System overrides
  public override int GetHashCode() {
    return itemId.GetHashCode(); // Allow the item to be properly handled in the sets and dictionaries.
  }
  #endregion

  #region Local utility methods

  /// <summary>Makes a new item from the part snapshot.</summary>
  /// <param name="inventory">The inventory to bind the item to.</param>
  /// <param name="snapshot">The part's snapshot.</param>
  /// <param name="newItemId">The new item ID.</param>
  InventoryItemImpl(KisContainerBase inventory, ProtoPartSnapshot snapshot, string newItemId) {
    this._inventory = inventory;
    this.snapshot = snapshot;
    _oldVariantName = snapshot.moduleVariantName ?? "";
    this.itemId = newItemId;
    UpdateItem();
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
