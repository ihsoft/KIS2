﻿// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KISAPIv2;
using System;
using System.Collections.Generic;
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
  public ProtoPartSnapshot snapshot { get; }

  /// <inheritdoc/>
  public PartVariant variant => VariantsUtils2.GetPartVariant(avPart, variantName);

  /// <inheritdoc/>
  public string variantName => snapshot.moduleVariantName ?? "";

  /// <inheritdoc/>
  public double volume => KisApi.PartModelUtils.GetPartVolume(avPart, variantName);
  
  /// <inheritdoc/>
  public Vector3 size => KisApi.PartModelUtils.GetPartBounds(avPart, variantName);

  /// <inheritdoc/>
  public double dryMass => snapshot.mass - snapshot.moduleMass;
  
  /// <inheritdoc/>
  public double dryCost => KisApi.PartNodeUtils.GetPartDryCost(avPart, variant: variant);
  
  /// <inheritdoc/>
  public double fullMass => dryMass + snapshot.moduleMass + resources.Sum(r => r.amount * r.definition.density);
  
  /// <inheritdoc/>
  public double fullCost => dryCost + snapshot.moduleCosts + resources.Sum(r => r.amount * r.definition.unitCost);

  /// <inheritdoc/>
  public ProtoPartResourceSnapshot[] resources => snapshot.resources.ToArray();

  /// <inheritdoc/>
  public ScienceData[] science => snapshot.modules
      .SelectMany(m => KisApi.PartNodeUtils.GetModuleScience(m.moduleValues))
      .ToArray();
  
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
  public void UpdateConfig() {
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
    this.itemId = newItemId;
    UpdateConfig();
  }

  /// <summary>Returns a unique item ID if none was provided.</summary>
  static string NewItemId(string providedId) {
    return providedId ?? Guid.NewGuid().ToString();
  }
  #endregion
}
  
}  // namespace
