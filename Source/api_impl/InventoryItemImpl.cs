// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KISAPIv2;
using System;
using System.Linq;
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
  public Part physicalPart { get; }

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
  #endregion

  #region API methods
  /// <summary>Creates an item from a proto part snapshot.</summary>
  /// <param name="inventory">The inventory to bind the item to.</param>
  /// <param name="snapshot">The snapshot to make the item from.</param>
  /// <param name="itemId">An optional item ID. If not set, a new unique value will be generated.</param>
  /// <returns>A new item.</returns>
  public static InventoryItemImpl FromProtoPartSnapshot(
      IKisInventory inventory, ProtoPartSnapshot snapshot, string itemId = null) {
    var itemConfig = KisApi.PartNodeUtils.GetConfigNodeFromProtoPartSnapshot(snapshot);
    return new(inventory, snapshot.partInfo.name, itemConfig, itemId);
  }

  /// <summary>Creates an item from an active part.</summary>
  /// <remarks>
  /// The current part's state will be captured. The created item will be "unowned", i.e. it won't be claimed by any
  /// inventory. Use <see cref="IKisInventory.AddItem"/> to pass such items to the real owner.
  /// </remarks>
  /// <param name="part">The part to take a snapshot from.</param>
  /// <param name="itemId">An optional item ID. If not set, a new unique value will be generated.</param>
  /// <returns>A new item.</returns>
  public static InventoryItemImpl FromPart(Part part, string itemId = null) {
    return new(null, part.partInfo.name, KisApi.PartNodeUtils.PartSnapshot(part), itemId);
  }

  /// <summary>Creates an item from a proto part snapshot.</summary>
  /// <param name="inventory">The inventory to bind the item to.</param>
  /// <param name="partName">The name of the part to create.</param>
  /// <param name="itemConfig">
  /// An optional part state node. If not provided, the default state from the prefab will be used.
  /// </param>
  /// <param name="itemId">An optional item ID. If not set, a new unique value will be generated.</param>
  /// <returns>A new item.</returns>
  /// <exception cref="InvalidOperationException">If the part name cannot be found.</exception>
  public static InventoryItemImpl ForPartName(
      IKisInventory inventory, string partName, ConfigNode itemConfig, string itemId = null) {
    return new(inventory, partName, itemConfig, itemId);
  }
  #endregion

  #region System overrides
  public override int GetHashCode() {
    return itemId.GetHashCode(); // Allow the item to properly handled in the sets and dictionaries.
  }
  #endregion

  #region Local utility methods

  /// <summary>Makes a new item from the part name.</summary>
  /// <remarks>It must not be called directly. The clients must use the factory methods.</remarks>
  /// <param name="inventory">
  /// The inventory to bind the item to. It can be <c>null</c>, which means "no inventory". Such items only make sense
  /// in the intermediate state.
  /// </param>
  /// <param name="partName">The name of the part to create.</param>
  /// <param name="itemConfig">
  /// An optional part state node. If not provided, the default state from the prefab will be used.
  /// </param>
  /// <param name="itemId">An optional item ID. If not set, a new unique value will be generated.</param>
  /// <seealso cref="FromProtoPartSnapshot"/>
  /// <seealso cref="FromPart"/>
  /// <seealso cref="ForPartName"/>
  /// <exception cref="InvalidOperationException">If the part name cannot be found.</exception>
  InventoryItemImpl(IKisInventory inventory, string partName, ConfigNode itemConfig, string itemId) {
    var partInfo = PartLoader.getPartInfoByName(partName);
    Preconditions.NotNull(partInfo, message: "Part name found: " + partName, context: inventory);
    this.inventory = inventory;
    this.avPart = partInfo;
    this.itemConfig = itemConfig ?? KisApi.PartNodeUtils.PartSnapshot(avPart.partPrefab);
    this.itemId = itemId ?? Guid.NewGuid().ToString();
    UpdateConfig();
  }
  #endregion
}
  
}  // namespace
