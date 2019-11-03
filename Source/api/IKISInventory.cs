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
  /// <remarks>Objects that are larger in any of the dimensions cannot fit the inventory.</remarks>
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

  /// <summary>Verifies if the part can be added into the inventory.</summary>
  /// <remarks>This method verifies if <i>all</i> the parts can fit the inventory.</remarks>
  /// <param name="avParts">The part protos.</param>
  /// <param name="nodes">
  /// The part's persisted state. If <c>null</c>, then a default state will be created from the
  /// prefab. If an array is provided, then it can have <c>null</c> elements for the parts that have
  /// a default config.
  /// </param>
  /// <param name="logErrors">
  /// If <c>true</c>, then the checking errors will be logged. Use it when calling this method as a
  /// precondition.
  /// </param>
  /// <returns><c>null</c> if the part can be added, or a list of reasons why not.</returns>
  /// <seealso cref="AddPart"/>
  ErrorReason[] CheckCanAddParts(
      AvailablePart[] avParts, ConfigNode[] nodes = null, bool logErrors = false);

  /// <summary>Adds a new part into the inventory.</summary>
  /// <remarks>
  /// This method does <i>not</i> verify if the item can fit the inventory. Doing this check is
  /// responsibility of the caller.
  /// </remarks>
  /// <param name="avPart">The part proto.</param>
  /// <param name="node">The part's persisted state.</param>
  /// <returns>The newly created item or <c>null</c> if add failed.</returns>
  /// <seealso cref="CheckCanAddParts"/>
  InventoryItem AddPart(AvailablePart avPart, ConfigNode node);

  /// <summary>Removes the specified items from inventory.</summary>
  /// <remarks>
  /// The delete action is atomic: it either succeeds in full or fails without changing the
  /// inventory state.
  /// </remarks>
  /// <param name="deleteItems">The items to remove.</param>
  /// <returns>
  /// <c>true</c> if removal was successful or NO-OP. <c>false</c> if any of the items cannot be
  /// removed.
  /// </returns>
  /// <seealso cref="InventoryItem.isLocked"/>
  bool DeleteItems(InventoryItem[] deleteItems);

  /// <summary>
  /// Forces the container to refresh its state according to the new state of an item.
  /// </summary>
  /// <remarks>
  /// Every change to any item in the inventory must result in calling of this method. The items
  /// will <i>not</i> send updates to the owner inventory (they are forbidden to do this).
  /// <para>
  /// Before updating own state, the inventory will update every single item in it. An exception
  /// will be made if <paramref name="changedItems"/> are provided. In this case only that items
  /// will be updated. Callers can use this ability to optimize their calls and save CPU.
  /// </para>
  /// <para>
  /// Do not call this method if the changes were made thru the inventory methods (like adding or
  /// removing items). The inventory is fully aware of such chages and can update accordingly. This
  /// method must only be called when the item itself has changed (e.g. its config was updated). 
  /// </para>
  /// </remarks>
  /// <param name="changedItems">
  /// The items, which changed state was the reason of the inventory update. If <c>null</c>, then\
  /// all the items in the inventory will be updated.
  /// </param>
  void UpdateInventoryStats(InventoryItem[] changedItems);
}

}  // namespace
