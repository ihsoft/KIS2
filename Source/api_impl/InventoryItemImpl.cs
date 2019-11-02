// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using KISAPIv2;
using UnityEngine;

namespace KIS2 {

/// <summary>Implementation for the inventory item.</summary>
sealed class InventoryItemImpl : InventoryItem {
  #region InventoryItem properties
  /// <inheritdoc/>
  public IKISInventory inventory { get; private set; }

  /// <inheritdoc/>
  public AvailablePart avPart { get; private set; }

  /// <inheritdoc/>
  public Part physicalPart { get; internal set; }
  
  /// <inheritdoc/>
  public ConfigNode itemConfig { get; private set; }
  
  /// <inheritdoc/>
  public double volume {
    get {
      if (_volume < 0) {
        _volume = KISAPI.PartModelUtils.GetPartVolume(avPart, partNode: itemConfig);
      }
      return _volume;
    }
  }
  double _volume = -1;
  
  /// <inheritdoc/>
  public Vector3 size {
    get {
      if (!_size.HasValue) {
        _size = KISAPI.PartModelUtils.GetPartBounds(avPart, partNode: itemConfig);
      }
      return _size.Value;
    }
  }
  Vector3? _size;

  /// <inheritdoc/>
  public double dryMass {
    get {
      if (_dryMass < 0) {
        _dryMass = KISAPI.PartNodeUtils.GetPartDryMass(avPart, partNode: itemConfig);
      }
      return _dryMass;
    }
  }
  double _dryMass = -1;
  
  /// <inheritdoc/>
  public double dryCost {
    get {
      if (_dryCost < 0) {
        _dryCost = KISAPI.PartNodeUtils.GetPartDryCost(avPart, partNode: itemConfig);
      }
      return _dryCost;
    }
  }
  double _dryCost = -1;
  
  /// <inheritdoc/>
  public double fullMass {
    get {
      if (_fullMass < 0) {
        _fullMass = dryMass + resources.Sum(r => r.amount * r.definition.density);
      }
      return _fullMass;
    }
  }
  double _fullMass = -1;
  
  /// <inheritdoc/>
  public double fullCost {
    get {
      if (_fullCost < 0) {
        _fullCost = dryCost + resources.Sum(r => r.amount * r.definition.unitCost);
      }
      return _fullCost;
    }
  }
  double _fullCost = -1;
  
  /// <inheritdoc/>
  public ProtoPartResourceSnapshot[] resources {
    get {
      if (_resources == null) {
        _resources = KISAPI.PartNodeUtils.GetResources(itemConfig);
      }
      return _resources;
    }
  }
  ProtoPartResourceSnapshot[] _resources;
  
  /// <inheritdoc/>
  public ScienceData[] science {
    get {
      if (_science == null) {
        _science = KISAPI.PartNodeUtils.GetScience(itemConfig);
      }
      return _science;
    }
  }
  ScienceData[] _science;
  
  /// <inheritdoc/>
  public bool isEquipped { get; private set; }

  /// <inheritdoc/>
  public bool isLocked { get; private set; }
  #endregion

  #region InventoryItem implementation
  /// <inheritdoc/>
  public void SetLocked(bool newState) {
    isLocked = newState;
  }

  /// <inheritdoc/>
  public void UpdateConfig() {
    _volume = -1;
    _size = null;
    _dryMass = -1;
    _dryCost = -1;
    _fullMass = -1;
    _fullCost = -1;
    _resources = null;
    _science = null;
  }
  #endregion

  #region API methods
  /// <summary>Makes a new item from the part definition.</summary>
  internal InventoryItemImpl(IKISInventory inventory, AvailablePart avPart, ConfigNode node) {
    this.inventory = inventory;
    this.avPart = avPart;
    this.itemConfig = node;
  }
  #endregion
}
  
}  // namespace

