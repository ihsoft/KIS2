// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KISAPIv2;
using System;
using System.Linq;
using KSPDev.LogUtils;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>Implementation for the inventory item.</summary>
internal sealed class InventoryItemImpl : InventoryItem {
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

  #region Persistent node names
  public const string PersistentConfigKisItemId = "kisItemId";
  public const string PersistentConfigItemNode = "ITEM";
  #endregion

  #region API methods
  /// <summary>Restores inventory item from a saved state.</summary>
  /// <param name="inventory">The inventory to bind the item to.</param>
  /// <param name="savedState">The state to restore from.</param>
  /// <returns>The item or <c>null</c> if item cannot be restored from the given state.</returns>
  public static InventoryItem FromConfigNode(IKisInventory inventory, ConfigNode savedState) {
    var partName = savedState.GetValue("name"); // It's a standard name for the part save node.
    var avPart = PartLoader.getPartInfoByName(partName);
    if (avPart == null) {
      HostedDebugLog.Error(
          inventory as PartModule, "Cannot load part form config: name={0}", partName);
      return null;
    }
    var itemConfig = savedState.CreateCopy();
    itemConfig.name = "PART";
    itemConfig.RemoveValue(PersistentConfigKisItemId);
    return new InventoryItemImpl(
          inventory, avPart, itemConfig, itemGuid: savedState.GetValue(PersistentConfigKisItemId));
  }

  /// <summary>Saves an item into a config node.</summary>
  /// <param name="item">The item to store.</param>
  /// <returns>The config node. It's never <c>null</c>.</returns>
  public static ConfigNode ToConfigNode(InventoryItem item) {
    var node = item.itemConfig.CreateCopy();
    node.SetValue(PersistentConfigKisItemId, item.itemId, createIfNotFound: true);
    node.name = PersistentConfigItemNode;
    return node;
  }

  /// <summary>Makes a new item from the part definition.</summary>
  public InventoryItemImpl(IKisInventory inventory, AvailablePart avPart, ConfigNode itemConfig,
                           string itemGuid = null) {
    this.inventory = inventory;
    this.avPart = avPart;
    this.itemConfig = itemConfig;
    this.itemId = itemGuid ?? Guid.NewGuid().ToString();
    UpdateConfig();
  }

  /// <inheritdoc cref="InventoryItemImpl(IKisInventory,AvailablePart,ConfigNode,string)"/>
  public InventoryItemImpl(IKisInventory inventory, Part part, string itemGuid = null)
      : this(inventory, part.partInfo, KisApi.PartNodeUtils.PartSnapshot(part), itemGuid) {
  }
  #endregion

  #region System overrides
  public override int GetHashCode() {
    return itemId.GetHashCode();
  }
  #endregion
}
  
}  // namespace
