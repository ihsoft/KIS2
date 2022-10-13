// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KISAPIv2;
using System;
using System.Linq;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>Implementation for the inventory item.</summary>
sealed class InventoryItemImpl : InventoryItem {
  #region InventoryItem properties
  /// <inheritdoc/>
  public IKisInventory inventory {
    get {
      if (_inventory != null) {
        AssertAttached();
      }
      return _inventory;
    }
  }
  readonly IKisInventory _inventory;

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
      return _iconImage ??= PartIconUtils.MakeDefaultIcon(avPart, variantName);
    }
  }
  Texture _iconImage;

  /// <inheritdoc/>
  public ProtoPartSnapshot snapshot => _cachedSnapshot ?? stockSlot.snapshot;
  readonly ProtoPartSnapshot _cachedSnapshot; // Only set if NOT attached to inventory.

  /// <inheritdoc/>
  public int stockSlotIndex {
    get {
      if (inventory != null) {
        AssertAttached();
      }
      return _stockSlotIndex;
    }
  }
  readonly int _stockSlotIndex;

  /// <inheritdoc/>
  public StoredPart stockSlot {
    get {
      if (inventory == null) {
        return null;
      }
      AssertAttached();
      return inventory.stockInventoryModule.storedParts[stockSlotIndex];
    }
  }

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
      return _volume ??= PartModelUtils.GetPartVolume(avPart, variantName);
    }
  }
  double? _volume;
  
  /// <inheritdoc/>
  public Vector3 size {
    get {
      UpdateVariant();
      return _size ??= PartModelUtils.GetPartBounds(avPart, variantName);
    }
  } 
  Vector3? _size;

  /// <inheritdoc/>
  public double dryMass => snapshot.mass - snapshot.moduleMass;
  
  /// <inheritdoc/>
  public double dryCost {
    get {
      UpdateVariant();
      return _dryCost ??= PartPrefabUtils.GetPartDryCost(avPart, variantName);
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
      .SelectMany(m => PartNodeUtils.GetModuleScience(m.moduleValues))
      .ToArray();
  ScienceData[] _science;
  
  /// <inheritdoc/>
  public bool isLocked { get; set; }
  #endregion

  #region API methods
  /// <summary>Creates an attached item from a stock slot.</summary>
  /// <param name="inventory">The inventory to bind the item to. It must not be NULL.</param>
  /// <param name="stockSlotIndex">
  /// The stock slot to get the data from and associate with this item. This slot must not be empty.
  /// </param>
  /// <param name="itemId">The item ID or NULL if a new random value needs to be made.</param>
  /// <returns>A new item.</returns>
  public static InventoryItemImpl FromStockSlot(KisContainerBase inventory, int stockSlotIndex, string itemId) {
    return new InventoryItemImpl(
        inventory.stockInventoryModule.storedParts[stockSlotIndex].snapshot, inventory, stockSlotIndex, itemId);
  }

  /// <summary>Creates a detached item from a proto part snapshot.</summary>
  /// <param name="snapshot">
  /// The snapshot to make the item from. All items created from the same snapshot will share the instance.
  /// </param>
  /// <returns>A new item.</returns>
  public static InventoryItemImpl FromSnapshotDetached(ProtoPartSnapshot snapshot) {
    return new InventoryItemImpl(snapshot);
  }

  /// <summary>Creates an item attached to the inventory from a proto part snapshot.</summary>
  /// <param name="inventory">The inventory to attach the item to.</param>
  /// <param name="snapshot">
  /// The snapshot to make the item from. All items created from the same snapshot will share the instance.
  /// </param>
  /// <param name="stockSlotIndex">The stock slot index to associate the item with.</param>
  /// <returns>A new item.</returns>
  public static InventoryItemImpl FromSnapshotAttached(
      KisContainerBase inventory, ProtoPartSnapshot snapshot, int stockSlotIndex) {
    return new InventoryItemImpl(snapshot, inventory: inventory, stockSlotIndex: stockSlotIndex);
  }

  /// <summary>Creates a detached item from an active part.</summary>
  /// <remarks>The current part's state will be captured.</remarks>
  /// <param name="part">The part to take a snapshot from.</param>
  /// <returns>A new detached item.</returns>
  public static InventoryItemImpl FromPart(Part part) {
    return new InventoryItemImpl(PartNodeUtils.GetProtoPartSnapshot(part));
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
    _inventory = inventory;
    _stockSlotIndex = stockSlotIndex;
    _cachedSnapshot = inventory == null ? snapshot : null;
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

  /// <summary>Throws if item is not more owned by the inventory.</summary>
  /// <exception cref="InvalidOperationException">if the owner inventory doesn't have this item.</exception>
  void AssertAttached() {
    if (!_inventory.inventoryItems.ContainsKey(itemId)) {
      throw new InvalidOperationException(
          $"The item is not in inventory: itemId={itemId}, inventory={DebugEx.ObjectToString(_inventory)}");
    }
  }
  #endregion
}

}  // namespace
