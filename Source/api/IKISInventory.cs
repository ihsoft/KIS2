// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using UnityEngine;

namespace KISAPIv2 {

/// <summary>Basic interface that every inventory must implement to work with KIS.</summary>
public interface IKISInventory {
  /// <summary>Items in the inventory.</summary>
  InventoryItem[] items { get; }

  /// <summary>Maximum dimensions of the inner inventory space.</summary>
  /// <remarks>Objects that are large in any of the dimensions cannot fit the inventory.</remarks>
  Vector3 maxInnerSize { get; }

  /// <summary>Maximum volume of all the items that inventory can hold.</summary>
  /// <seealso cref="usedVolume"/>
  double maxVolume { get; }

  /// <summary>Current volume of all the items in the inventory.</summary>
  /// <seealso cref="maxVolume"/>
  /// <seealso cref="UpdateInventoryStats"/>
  double usedVolume { get; }

  /// <summary>Total mass of all the items in the inventory.</summary>
  /// <seealso cref="UpdateInventoryStats"/>
  double contentMass { get; }

  /// <summary>Total cost of all the items in the inventory.</summary>
  /// <seealso cref="UpdateInventoryStats"/>
  double contentCost { get; }

  /// <summary>Verifies if the item can be added into the inventory.</summary>
  /// <param name="avPart">The part proto.</param>
  /// <param name="node">The part's persisted state.</param>
  /// <returns><c>null</c> if item can be added, or a list of reasons why not.</returns>
  /// <seealso cref="AddItem"/>
  ErrorReason[] CheckCanAdd(AvailablePart avPart, ConfigNode node);

  /// <summary>Adds a new item into the inventory.</summary>
  /// <remarks>
  /// This method does <i>not</i> verifies if the item can fit the inventory. Doing this check is
  /// responsibility of the caller.
  /// </remarks>
  /// <param name="avPart">The part proto.</param>
  /// <param name="node">The part's persisted state.</param>
  /// <returns>The newly created item or <c>null</c> if add failed.</returns>
  /// <seealso cref="CheckCanAdd"/>
  InventoryItem AddItem(AvailablePart avPart, ConfigNode node);

  /// <summary>Removes the specified item from inventory.</summary>
  /// <remarks>Locked items cannot be removed.</remarks>
  /// <param name="item">The item to remove.</param>
  /// <returns>
  /// <c>true</c> if removal was successful or NO-OP. <c>false</c> if the item exists but cannot be
  /// removed.
  /// </returns>
  /// <seealso cref="SetItemLock"/>
  bool DeleteItem(InventoryItem item);

  /// <summary>
  /// Puts a lock state on the item that prevents any updates to it in this inventory.
  /// </summary>
  /// <remarks>The locked item cannot be removed from the inventory.</remarks>
  /// <param name="item">The item to put lock on.</param>
  /// <param name="isLocked">The lock state.</param>
  /// <seealso cref="DeleteItem"/>
  /// <seealso cref="InventoryItem.isLocked"/>
  void SetItemLock(InventoryItem item, bool isLocked);

  /// <summary>Forces the container to recalculate its mass/cost/volume stats.</summary>
  /// <remarks>
  /// Before updating own state, the inventory will update every single item in it. An exception
  /// will be made if <paramref name="changedItems"/> are provided. In this case only that items
  /// will be updated. Callers can use this ability to optimize their calls and save CPU. 
  /// </remarks>
  /// <param name="changedItems">
  /// The items, which changed state was the reason of the inventory update. If empty, then all the
  /// items in the inventory will be updated.
  /// </param>
  void UpdateInventoryStats(params InventoryItem[] changedItems);
}

}  // namespace
