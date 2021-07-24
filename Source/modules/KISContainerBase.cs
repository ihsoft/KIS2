// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KISAPIv2;
using KIS2.GUIUtils;
using KSPDev.GUIUtils;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
using Smooth.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>
/// Base module to handle inventory items. It can only hold items, no GUI is offered.
/// </summary>
public class KisContainerBase : AbstractPartModule,
                                IKisInventory {
  #region Localizable GUI strings
  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<VolumeLType> NotEnoughVolumeText = new Message<VolumeLType>(
      "",
      defaultTemplate: "Not enough volume: <color=#f88>-<<1>></color>",
      description: "Message to present to the user at the main status area when an item being"
      + " placed into the inventory cannot fit it due to not enough free volume.\n"
      + "The <<1>> parameter is the volume delta that would be needed for the item to fit of"
      + " type VolumeLType.");
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
  public InventoryItem[] inventoryItems { get; private set; } = new InventoryItem[0];

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
  /// Reflection of <see cref="inventoryItems"/> in a form of map for quick lookup operations. 
  /// </summary>
  /// <remarks>
  /// Do not modify this map directly! Descendants must call <see cref="UpdateItems"/> to add or
  /// remove items from the collection.
  /// </remarks>
  /// <seealso cref="UpdateItems"/>
  /// <seealso cref="InventoryItem.itemId"/>
  protected readonly Dictionary<string, InventoryItem> inventoryItemsMap =
      new Dictionary<string, InventoryItem>();
  #endregion

  #region AbstractPartModule overrides
  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);
    var restoredItems = node.GetNodes(InventoryItemImpl.PersistentConfigItemNode)
        .Select(x => InventoryItemImpl.FromConfigNode(this, x))
        .Where(x => x != null)
        .ToArray();
    UpdateItems(addItems: restoredItems);
  }

  /// <inheritdoc/>
  public override void OnSave(ConfigNode node) {
    base.OnSave(node);
    Array.ForEach(inventoryItems, x => node.AddNode(InventoryItemImpl.ToConfigNode(x)));
  }
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
      var node = nodes[i] ?? KisApi.PartNodeUtils.PartSnapshot(avPart.partPrefab);
      partsVolume += KisApi.PartModelUtils.GetPartVolume(avPart, partNode: node);
      //FIXME: Check part size.
    }
    if (usedVolume + partsVolume > maxVolume) {
      // Normalize the used volume in case of the inventory is already overloaded. 
      var freeVolume = maxVolume - Math.Min(usedVolume, maxVolume);
      errors.Add(new ErrorReason() {
          shortString = "VolumeTooLarge",
          guiString = NotEnoughVolumeText.Format(partsVolume - freeVolume),
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
  public virtual InventoryItem[] AddItems(InventoryItem[] items) {
    var ownItems = items
        .Select(x => new InventoryItemImpl(this, x.avPart, x.itemConfig, itemGuid: x.itemId))
        .Cast<InventoryItem>()
        .ToArray();
    UpdateItems(addItems: ownItems);
    return ownItems;
  }

  /// <inheritdoc/>
  public virtual bool DeleteItems(InventoryItem[] deleteItems) {
    if (deleteItems.Any(x => x.isLocked)) {
      HostedDebugLog.Error(this, "Cannot delete locked item(s)");
      return false;
    }
    if (deleteItems.Any(
        x => !ReferenceEquals(x.inventory, this) || !inventoryItemsMap.ContainsKey(x.itemId))) {
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
    if (changedItems == null) {
      HostedDebugLog.Fine(this, "Updating all items in the inventory...");
      Array.ForEach(inventoryItems, i => i.UpdateConfig());
    } else if (changedItems.Length > 0) {
      HostedDebugLog.Fine(this, "Updating {0} items in the inventory..." , changedItems.Length);
      Array.ForEach(changedItems, i => i.UpdateConfig());
    }
    usedVolume = inventoryItems.Sum(i => i.volume);
    contentMass = inventoryItems.Sum(i => i.fullMass);
    contentCost = inventoryItems.Sum(i => i.fullCost);
  }

  /// <inheritdoc/>
  public InventoryItem FindItem(string itemId) {
    InventoryItem res = null;
    inventoryItemsMap.TryGetValue(itemId, out res);
    return res;
  }
  #endregion

  #region Inheritable methods
  /// <summary>Adds or deletes the inventory items.</summary>
  /// <remarks>This method is optimized to update large batches.</remarks>
  protected virtual void UpdateItems(
      InventoryItem[] addItems = null, InventoryItem[] deleteItems = null) {
    if (addItems != null) {
      Array.ForEach(addItems, x => inventoryItemsMap.Add(x.itemId, x));
    }
    if (deleteItems != null) {
      Array.ForEach(deleteItems, x => inventoryItemsMap.Remove(x.itemId));
    }
    // Reconstruct the items array so that the existing items keep their original order, and the new
    // items (if any) are added at the tail.  
    var newItems = inventoryItems.Where(x => inventoryItemsMap.ContainsKey(x.itemId));
    if (addItems != null) {
      newItems = newItems.Concat(addItems);
    }
    inventoryItems = newItems.ToArray();
    UpdateInventoryStats(new InventoryItem[0]);
  }
  #endregion
}

}  // namespace
