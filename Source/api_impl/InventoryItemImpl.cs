// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KISAPIv2;
using System.Linq;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>Implementation for the inventory item.</summary>
internal sealed class InventoryItemImpl : InventoryItem {
  #region InventoryItem properties
  /// <inheritdoc/>
  public IKisInventory inventory { get; }

  /// <inheritdoc/>
  public AvailablePart avPart { get; }

  /// <inheritdoc/>
  public ConfigNode itemConfig { get; }
  
  /// <inheritdoc/>
  public Part physicalPart { get; }

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
    volume = KISAPI.PartModelUtils.GetPartVolume(avPart, partNode: itemConfig);
    size = KISAPI.PartModelUtils.GetPartBounds(avPart, partNode: itemConfig);
    dryMass = KISAPI.PartNodeUtils.GetPartDryMass(avPart, partNode: itemConfig);
    dryCost = KISAPI.PartNodeUtils.GetPartDryCost(avPart, partNode: itemConfig);
    fullMass = dryMass + resources.Sum(r => r.amount * r.definition.density);
    fullCost = dryCost + resources.Sum(r => r.amount * r.definition.unitCost);
    resources = KISAPI.PartNodeUtils.GetResources(itemConfig);
    foreach (var resource in resources) {
      resource.resourceRef = avPart.partPrefab.Resources
          .First(x => x.resourceName == resource.resourceName);
    }
    science = KISAPI.PartNodeUtils.GetScience(itemConfig);
  }
  #endregion

  #region API methods
  /// <summary>Makes a new item from the part definition.</summary>
  public InventoryItemImpl(IKisInventory inventory, AvailablePart avPart, ConfigNode itemConfig) {
    this.inventory = inventory;
    this.avPart = avPart;
    this.itemConfig = itemConfig;
    UpdateConfig();
  }
  #endregion
}
  
}  // namespace
