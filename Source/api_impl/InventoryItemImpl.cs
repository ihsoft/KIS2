// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KISAPIv2;
using System;
using System.Linq;
using KSPDev.ConfigUtils;
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
  public AvailablePart avPart { get; }

  /// <inheritdoc/>
  public ConfigNode itemConfig { get; }

  /// <inheritdoc/>
  public ProtoPartSnapshot snapshot =>
      _snapshot ??= KisApi.PartNodeUtils.GetProtoPartSnapshotFromNode(
          inventory?.ownerVessel, itemConfig, keepPersistentId: true);
  ProtoPartSnapshot _snapshot;

  /// <inheritdoc/>
  public PartVariant variant { get; private set; }

  /// <inheritdoc/>
  public double volume { get; private set; }
  
  /// <inheritdoc/>
  public Vector3 size { get; private set; }

  /// <inheritdoc/>
  public double dryMass { get; private set; }
  
  /// <inheritdoc/>
  public double dryCost { get; private set; }
  
  /// <inheritdoc/>
  public double fullMass { get; private set; }
  
  /// <inheritdoc/>
  public double fullCost { get; private set; }

  /// <inheritdoc/>
  public ProtoPartResourceSnapshot[] resources { get; private set; }
      = new ProtoPartResourceSnapshot[0];

  /// <inheritdoc/>
  public ScienceData[] science { get; private set; } = new ScienceData[0];
  
  /// <inheritdoc/>
  public bool isEquipped => false;

  /// <inheritdoc/>
  public bool isLocked { get; private set; }
  #endregion

  #region InventoryItem implementation
  /// <inheritdoc/>
  public void SetLocked(bool newState) {
    //FIXME: notify inventory? or let caller doing it?
    isLocked = newState;
  }

  /// <inheritdoc/>
  public void UpdateConfig() {
    _snapshot = null; // Force refresh on access.
    variant = VariantsUtils.GetCurrentPartVariant(avPart, itemConfig);
    volume = KisApi.PartModelUtils.GetPartVolume(avPart, partNode: itemConfig);
    size = KisApi.PartModelUtils.GetPartBounds(avPart, partNode: itemConfig);
    dryMass = KisApi.PartNodeUtils.GetPartDryMass(avPart, partNode: itemConfig);
    dryCost = KisApi.PartNodeUtils.GetPartDryCost(avPart, partNode: itemConfig);
    fullMass = dryMass + resources.Sum(r => r.amount * r.definition.density);
    fullCost = dryCost + resources.Sum(r => r.amount * r.definition.unitCost);
    resources = KisApi.PartNodeUtils.GetResources(itemConfig);
    foreach (var resource in resources) {
      resource.resourceRef = avPart.partPrefab.Resources
          .FirstOrDefault(x => x.resourceName == resource.resourceName);
    }
    science = KisApi.PartNodeUtils.GetScience(itemConfig);
  }

  /// <inheritdoc/>
  public T? GetConfigValue<T>(string path) where T : struct {
    return ConfigAccessor.GetValueByPath<T>(itemConfig, path);
  }

  /// <inheritdoc/>
  public void SetConfigValue<T>(string path, T value) where T : struct {
    ConfigAccessor.SetValueByPath(itemConfig, path, value);
  }
  #endregion

  #region API methods
  /// <summary>Creates an item from a proto part snapshot.</summary>
  /// <param name="inventory">The inventory to bind the item to.</param>
  /// <param name="snapshot">The snapshot to make the item from.</param>
  /// <param name="itemId">An optional item ID. If not set, a new unique value will be generated.</param>
  /// <returns>A new item.</returns>
  public static InventoryItemImpl FromProtoPartSnapshot(
      IKisInventory inventory, ProtoPartSnapshot snapshot, string itemId = null) {
    return new InventoryItemImpl(inventory, snapshot: snapshot, NewItemId(itemId));
  }

  /// <summary>Creates an item from another item.</summary>
  /// <remarks>It may apply some performance tweaks if the items are of the same type.</remarks>
  /// <param name="inventory">
  /// The inventory to bind the item to. It can be <c>null</c> for the intermediate items.
  /// </param>
  /// <param name="item">The item to copy from.</param>
  /// <param name="itemId">An optional item ID. If not set, a new unique value will be generated.</param>
  /// <returns>A new item.</returns>
  public static InventoryItemImpl FromItem(IKisInventory inventory, InventoryItem item, string itemId = null) {
    return new InventoryItemImpl(inventory, item, NewItemId(itemId));
  }

  /// <summary>Creates an item from an active part.</summary>
  /// <remarks>The current part's state will be captured.</remarks>
  /// <param name="inventory">
  /// The inventory to bind the item to. It can be <c>null</c> for the intermediate items.
  /// </param>
  /// <param name="part">The part to take a snapshot from.</param>
  /// <param name="itemId">An optional item ID. If not set, a new unique value will be generated.</param>
  /// <returns>A new item.</returns>
  public static InventoryItemImpl FromPart(IKisInventory inventory, Part part, string itemId = null) {
    return new InventoryItemImpl(
        inventory, KisApi.PartNodeUtils.GetProtoPartSnapshot(part), NewItemId(itemId));
  }

  /// <summary>Creates an item for the given part name.</summary>
  /// <param name="inventory">
  /// The inventory to bind the item to. It can be <c>null</c> for the intermediate items.
  /// </param>
  /// <param name="partName">The name of the part to create.</param>
  /// <param name="itemConfig">
  /// An optional part state node. If not provided, the default state from the prefab will be used.
  /// </param>
  /// <param name="itemId">An optional item ID. If not set, a new unique value will be generated.</param>
  /// <returns>A new item.</returns>
  /// <exception cref="InvalidOperationException">If the part name cannot be found.</exception>
  public static InventoryItemImpl ForPartName(IKisInventory inventory, string partName,
                                              ConfigNode itemConfig = null, string itemId = null) {
    var partInfo = PartLoader.getPartInfoByName(partName);
    Preconditions.NotNull(partInfo, message: "Part name not found: " + partName);
    return new InventoryItemImpl(
        inventory,
        partInfo,
        itemConfig ?? KisApi.PartNodeUtils.GetConfigNode(partInfo.partPrefab),
        NewItemId(itemId));
  }
  #endregion

  #region System overrides
  public override int GetHashCode() {
    return itemId.GetHashCode(); // Allow the item to be properly handled in the sets and dictionaries.
  }
  #endregion

  #region Local utility methods

  /// <summary>Makes a new item from the part info and a saved state node.</summary>
  /// <remarks>This constructor will not populate the <see cref="snapshot"/> property.</remarks>
  /// <param name="inventory">The inventory to bind the item to.</param>
  /// <param name="avPart">The part info of the part.</param>
  /// <param name="itemConfig">The part's state node.</param>
  /// <param name="newItemId">The new item ID.</param>
  /// <seealso cref="snapshot"/>
  InventoryItemImpl(IKisInventory inventory, AvailablePart avPart, ConfigNode itemConfig, string newItemId) {
    this.inventory = inventory;
    this.avPart = avPart;
    this.itemConfig = itemConfig;
    this.itemId = newItemId;
    UpdateConfig();
  }

  /// <summary>Makes a new item from the part snapshot.</summary>
  /// <remarks>
  /// This constructor will capture the <see cref="itemConfig"/> from the <paramref name="snapshot"/>.
  /// </remarks>
  /// <param name="inventory">The inventory to bind the item to.</param>
  /// <param name="snapshot">The part's snapshot.</param>
  /// <param name="newItemId">The new item ID.</param>
  InventoryItemImpl(IKisInventory inventory, ProtoPartSnapshot snapshot, string newItemId) {
    this.inventory = inventory;
    this.avPart = snapshot.partInfo;
    this.itemConfig = new ConfigNode("PART");
    snapshot.Save(this.itemConfig);
    this.itemId = newItemId;
    UpdateConfig();
    _snapshot = snapshot; // Use the cached value.
  }

  /// <summary>Makes a new item from another item.</summary>
  /// <remarks>If the source item is of the same type, then a performance optimization may be mad.e</remarks>
  /// <param name="inventory">
  /// The inventory to bind the item to. It can be <c>null</c>, which means "no inventory". Such items only make sense
  /// in the intermediate state.
  /// </param>
  /// <param name="srcItem">
  /// The item to copy the data from. The deep copy is not guaranteed. Avoid changing the source once the copy is made!
  /// </param>
  /// <param name="newItemId">The new item ID.</param>
  InventoryItemImpl(IKisInventory inventory, InventoryItem srcItem, string newItemId) {
    this.inventory = inventory;
    this.avPart = srcItem.avPart;
    this.itemConfig = srcItem.itemConfig;
    this.itemId = newItemId;
    UpdateConfig();
    // Light optimization for the KIS implemented items.
    if (srcItem is InventoryItemImpl internalItem) {
      this._snapshot = internalItem._snapshot;
    }
  }

  /// <summary>Returns a unique item ID if none was provided.</summary>
  static string NewItemId(string providedId) {
    return providedId ?? Guid.NewGuid().ToString();
  }
  #endregion
}
  
}  // namespace
