// Kerbal Inventory System
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
    IKISInventory {
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
    var errors = new List<ErrorReason>();
    var partVolume = KISAPI.PartModelUtils.GetPartVolume(avPart, partNode: node);
    if (usedVolume + partVolume > maxVolume) {
      errors.Add(new ErrorReason() {
                   shortString = "VolumeTooLarge",
                   guiString = NeedMoreVolume.Format(partVolume - (maxVolume - usedVolume)),
                 });
    }
    var partBox = KISAPI.PartModelUtils.GetPartBounds(avPart, partNode: node);
    if (partBox.x > maxItemSize.x) {
      errors.Add(new ErrorReason() {
                   shortString = "SizeTooLarge",
                   guiString = WidthTooLarge.Format(partBox.x, maxItemSize.x),
                 });
    }
    if (partBox.y > maxItemSize.y) {
      errors.Add(new ErrorReason() {
                   shortString = "SizeTooLarge",
                   guiString = HeightTooLarge.Format(partBox.y, maxItemSize.y),
                 });
    }
    if (partBox.z > maxItemSize.z) {
      errors.Add(new ErrorReason() {
                   shortString = "SizeTooLarge",
                   guiString = LengthTooLarge.Format(partBox.z, maxItemSize.z),
                 });
    }
    return errors.Count > 0 ? errors.ToArray() : null;
  }

  /// <inheritdoc/>
  public virtual InventoryItem AddItem(AvailablePart avPart, ConfigNode node) {
    var item = new InventoryItemImpl(this, avPart, node);
    itemsList.Add(item);
    UpdateInventoryStats(item);
    return item;
  }

  /// <inheritdoc/>
  public virtual bool DeleteItem(InventoryItem item) {
    if (item.isLocked) {
      HostedDebugLog.Error(this, "Cannot delete locked item: idx={0}", itemsList.IndexOf(item));
      return false;
    }
    if (!itemsList.Remove(item)) {
      if (ReferenceEquals(item.inventory, this)) {
        HostedDebugLog.Warning(this, "Item is already removed: {0}", item.avPart.name);
      } else {
        HostedDebugLog.Error(this, "Item was never owned by the inventory: realOwner={0}",
                             item.inventory as PartModule);
      }
    } else {
      HostedDebugLog.Fine(this, "Remove item: {0}", item.avPart.name);
    }
    return true;
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
