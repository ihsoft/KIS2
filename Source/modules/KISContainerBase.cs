// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using KISAPIv2;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
using UnityEngine;

namespace KIS2 {

/// <summary>
/// Base module to handle inventory items. It can only hold items, no GUI is offered.
/// </summary>
public class KISContainerBase : AbstractPartModule,
    IKISInventory {

  #region Part's config fields
  /// <summary>Maximum volume that this container can contain.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public double maxContainerVolume;

  /// <summary>Maximum size of the item that can fit the contianer.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Vector3 maxItemSize;
  #endregion

  #region IKISInventory properties
  /// <inheritdoc/>
  public InventoryItem[] items {
    get { return itemsList.ToArray(); }
  }
  readonly List<InventoryItem> itemsList = new List<InventoryItem>();

  /// <inheritdoc/>
  public Vector3 maxInnerSize { get { return maxItemSize; } }

  /// <inheritdoc/>
  public double maxVolume { get { return maxContainerVolume; } }

  /// <inheritdoc/>
  public double usedVolume {
    get {
      if (_usedVolume < 0) {
        _usedVolume = itemsList.Sum(i => i.volume);
      }
      return _usedVolume;
    }
  }
  double _usedVolume = -1;

  /// <inheritdoc/>
  public double contentMass {
    get {
      if (_contentMass < 0) {
        _contentMass = itemsList.Sum(i => i.fullMass);
      }
      return _contentMass;
    }
  }
  double _contentMass = -1;

  public double contentCost {
    get {
      if (_contentCost < 0) {
        _contentCost = itemsList.Sum(i => i.fullCost);
      }
      return _contentCost;
    }
  }
  double _contentCost;
  #endregion

  #region IKISInventory implementation
  /// <inheritdoc/>
  public virtual ErrorReason[] CheckCanAdd(AvailablePart avPart, ConfigNode node) {
    //FIXME: verify part's volume and size
    return null;
  }

  /// <inheritdoc/>
  public virtual InventoryItem AddItem(AvailablePart avPart, ConfigNode node) {
    var item = new InventoryItemImpl(this, avPart, node);
    itemsList.Add(item);
    UpdateInventoryStats(item);
    return item;
  }

  /// <inheritdoc/>
  public virtual ErrorReason[] DeleteItem(InventoryItem item) {
    throw new NotImplementedException();
  }

  /// <inheritdoc/>
  public virtual void SetItemLock(InventoryItem item, bool isLocked) {
    throw new NotImplementedException();
  }

  /// <inheritdoc/>
  //public virtual void UpdateInventoryStats() {
  public virtual void UpdateInventoryStats(params InventoryItem[] changedItems) {
    if (changedItems.Length > 0) {
      changedItems.ToList().ForEach(i => i.UpdateConfig());
    } else {
      DebugEx.Fine("Updating all items in the inventory...");
      itemsList.ForEach(i => i.UpdateConfig());
    }
    _usedVolume = -1;
    _contentMass = -1;
    _contentCost = -1;
  }
  #endregion
}

}  // namespace
