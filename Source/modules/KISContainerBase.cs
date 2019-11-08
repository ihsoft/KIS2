﻿// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using KISAPIv2;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KIS2.GUIUtils;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
using UnityEngine;

namespace KIS2 {

/// <summary>
/// Base module to handle inventory items. It can only hold items, no GUI is offered.
/// </summary>
public class KISContainerBase : AbstractPartModule,
    IKisInventory {
  //FIXME: Add descriptions to the strings.
  #region Localizable GUI strings
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  readonly Message<VolumeLType> NeedMoreVolume = new Message<VolumeLType>(
      "",
      defaultTemplate: "Not enough volume, need +<<1>> more",
      description: "Message to present to the user at the main status area when an item being"
          + " placed into the inventory cannot fit it due to not enouh free volume.\n"
          + "The <<1>> parameter is the volume delta that would be needed for the item to fit of"
          + " type VolumeLType.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  readonly Message<DistanceType, DistanceType> WidthTooLarge =
      new Message<DistanceType, DistanceType>(
          "",
          defaultTemplate: "Width too large: <<1>> > <<2>>");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  readonly Message<DistanceType, DistanceType> HeightTooLarge =
      new Message<DistanceType, DistanceType>(
          "",
          defaultTemplate: "Height too large: <<1>> > <<2>>");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  readonly Message<DistanceType, DistanceType> LengthTooLarge =
      new Message<DistanceType, DistanceType>(
          "",
          defaultTemplate: "Length too large: <<1>> > <<2>>");
  #endregion

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

  #region Local fields and properties
  readonly HashSet<InventoryItem> itemsSet = new HashSet<InventoryItem>();
  #endregion

  #region IKISInventory properties
  /// <inheritdoc/>
  public InventoryItem[] inventoryItems { get; private set; }

  /// <inheritdoc/>
  public Vector3 maxInnerSize { get { return maxItemSize; } }

  /// <inheritdoc/>
  public double maxVolume { get { return maxContainerVolume; } }

  /// <inheritdoc/>
  public double usedVolume {
    get {
      if (_usedVolume < 0) {
        _usedVolume = itemsSet.Sum(i => i.volume);
      }
      return _usedVolume;
    }
  }
  double _usedVolume = -1;

  /// <inheritdoc/>
  public double contentMass {
    get {
      if (_contentMass < 0) {
        _contentMass = itemsSet.Sum(i => i.fullMass);
      }
      return _contentMass;
    }
  }
  double _contentMass = -1;

  public double contentCost {
    get {
      if (_contentCost < 0) {
        _contentCost = itemsSet.Sum(i => i.fullCost);
      }
      return _contentCost;
    }
  }
  double _contentCost;
  #endregion

  #region IKISInventory implementation
  /// <inheritdoc/>
  public virtual ErrorReason[] CheckCanAddParts(
      AvailablePart[] avParts, ConfigNode[] nodes = null, bool logErrors = false) {
    var errors = new List<ErrorReason>();
    nodes = nodes ?? new ConfigNode[avParts.Length];
    double partsVolume = 0;
    for (var i = 0; i < avParts.Length; ++i) {
      var avPart = avParts[i];
      var node = nodes[i] ?? KISAPI.PartNodeUtils.PartSnapshot(avPart.partPrefab);
      partsVolume += KISAPI.PartModelUtils.GetPartVolume(avPart, partNode: node);
      //FIXME: Check part size.
    }
    if (usedVolume + partsVolume > maxVolume) {
      errors.Add(new ErrorReason() {
                   shortString = "VolumeTooLarge",
                   guiString = NeedMoreVolume.Format(partsVolume - (maxVolume - usedVolume)),
                 });
    }
    if (logErrors && errors.Count > 0) {
      HostedDebugLog.Error(this, "Cannot add {0} part(s):\n{1}",
                           avParts.Length, DbgFormatter.C2S(errors, separator: "\n"));
    }
    return errors.Count > 0 ? errors.ToArray() : null;
  }

  /// <inheritdoc/>
  public virtual InventoryItem[] AddParts(AvailablePart[] avParts, ConfigNode[] nodes) {
    var items = new InventoryItem[avParts.Length];
    for (var i = 0; i < avParts.Length; i++) {
      items[i] = new InventoryItemImpl(this, avParts[i], nodes[i]);
    }
    UpdateItems(addItems: items);
    return items;
  }

  /// <inheritdoc/>
  public virtual bool DeleteItems(InventoryItem[] deleteItems) {
    if (deleteItems.Any(x => x.isLocked)) {
      HostedDebugLog.Error(this, "Cannot delete locked item(s)");
      return false;
    }
    if (deleteItems.Any(x => !ReferenceEquals(x.inventory, this) || !itemsSet.Contains(x))) {
      HostedDebugLog.Error(this, "Cannot delete item(s) that are not owned by the inventory");
      return false;
    }
    UpdateItems(deleteItems: deleteItems);
    HostedDebugLog.Fine(
        this, "Removed items: {0}", DbgFormatter.C2S(deleteItems, predicate: x => x.avPart.name));
    return true;
  }

  /// <inheritdoc/>
  public virtual void UpdateInventoryStats(InventoryItem[] changedItems) {
    if (changedItems != null && changedItems.Length > 0) {
      changedItems.ToList().ForEach(i => i.UpdateConfig());
    } else {
      HostedDebugLog.Fine(this, "Updating all items in the inventory...");
      itemsSet.ToList().ForEach(i => i.UpdateConfig());
    }
    _usedVolume = -1;
    _contentMass = -1;
    _contentCost = -1;
  }
  #endregion

  #region Inheritable methods
  /// <summary>Adds or deletes the inventory items.</summary>
  protected void UpdateItems(InventoryItem[] addItems = null, InventoryItem[] deleteItems = null) {
    if (addItems != null) {
      Array.ForEach(addItems, x => itemsSet.Add(x));
    }
    if (deleteItems != null) {
      Array.ForEach(deleteItems, x => itemsSet.Remove(x));
    }
    inventoryItems = itemsSet.ToArray();
    UpdateInventoryStats(addItems);
  }
  #endregion
}

}  // namespace
