// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KISAPIv2;
using KIS2.GUIUtils;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KIS2 {

/// <summary>
/// Base module to handle inventory items. It can only hold items, no GUI is offered.
/// </summary>
public class KisContainerBase : AbstractPartModule,
    IKisInventory {
  //FIXME: Add descriptions to the strings.
  #region Localizable GUI strings
  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<VolumeLType> NeedMoreVolume = new Message<VolumeLType>(
      "",
      defaultTemplate: "Not enough volume, need +<<1>> more",
      description: "Message to present to the user at the main status area when an item being"
          + " placed into the inventory cannot fit it due to not enough free volume.\n"
          + "The <<1>> parameter is the volume delta that would be needed for the item to fit of"
          + " type VolumeLType.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<DistanceType, DistanceType> WidthTooLarge =
      new Message<DistanceType, DistanceType>(
          "",
          defaultTemplate: "Width too large: <<1>> > <<2>>");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<DistanceType, DistanceType> HeightTooLarge =
      new Message<DistanceType, DistanceType>(
          "",
          defaultTemplate: "Height too large: <<1>> > <<2>>");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<DistanceType, DistanceType> LengthTooLarge =
      new Message<DistanceType, DistanceType>(
          "",
          defaultTemplate: "Length too large: <<1>> > <<2>>");
  #endregion

  #region Part's config fields
  /// <summary>Maximum volume that this container can contain.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public double maxContainerVolume;

  /// <summary>Maximum size of the item that can fit the container.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Vector3 maxItemSize;
  #endregion

  #region IKISInventory properties
  /// <inheritdoc/>
  public InventoryItem[] inventoryItems { get; private set; }

  /// <inheritdoc/>
  public Vector3 maxInnerSize => maxItemSize;

  /// <inheritdoc/>
  public double maxVolume => maxContainerVolume;

  /// <inheritdoc/>
  public double usedVolume { get; private set; }

  /// <inheritdoc/>
  public double contentMass { get; private set; }

  /// <inheritdoc/>
  public double contentCost { get; private set; }
  #endregion

  #region Inhertitable fields and properties
  /// <summary>
  /// Reflection of <see cref="inventoryItems"/> in a form of hash set for quick lookup operations. 
  /// </summary>
  /// <remarks>
  /// Do not modify this set directly! Descendants must call <see cref="UpdateItems"/> to add or
  /// remove items from the collection.
  /// </remarks>
  protected readonly HashSet<InventoryItem> inventoryItemsSet = new HashSet<InventoryItem>();
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
    if (deleteItems.Any(
        x => !ReferenceEquals(x.inventory, this) || !inventoryItemsSet.Contains(x))) {
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
    if (changedItems != null) {
      Array.ForEach(changedItems, i => i.UpdateConfig());
    } else {
      HostedDebugLog.Fine(this, "Updating all items in the inventory...");
      Array.ForEach(inventoryItems, i => i.UpdateConfig());
    }
    UpdateStatsOnly();
  }
  #endregion

  #region Inheritable methods
  /// <summary>Adds or deletes the inventory items.</summary>
  protected void UpdateItems(InventoryItem[] addItems = null, InventoryItem[] deleteItems = null) {
    if (addItems != null) {
      Array.ForEach(addItems, x => inventoryItemsSet.Add(x));
    }
    if (deleteItems != null) {
      Array.ForEach(deleteItems, x => inventoryItemsSet.Remove(x));
    }
    inventoryItems = inventoryItemsSet.ToArray();
    UpdateStatsOnly();
  }
  #endregion

  #region Local utility methods
  /// <summary>Updates container stats without updating the item configs.</summary>
  void UpdateStatsOnly() {
    usedVolume = inventoryItems.Sum(i => i.volume);
    contentMass = inventoryItems.Sum(i => i.fullMass);
    contentCost = inventoryItems.Sum(i => i.fullCost);
  }
  #endregion
}

}  // namespace
