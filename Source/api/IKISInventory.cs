// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using UnityEngine;

namespace KISAPIv2 {

/// <summary>Basic interface that every inventory must implement to work with KIS.</summary>
public interface IKisInventory {
  /// <summary>Items in the inventory.</summary>
  InventoryItem[] inventoryItems { get; }

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
  /// <seealso cref="AddParts"/>
  ErrorReason[] CheckCanAddParts(
      AvailablePart[] avParts, ConfigNode[] nodes = null, bool logErrors = false);

  /// <summary>Adds new parts into the inventory.</summary>
  /// <remarks>
  /// This method does <i>not</i> verify if the item can fit the inventory. Doing this check is
  /// responsibility of the caller.
  /// </remarks>
  /// <param name="avParts">The part protos.</param>
  /// <param name="nodes">
  /// The part persisted states. An entry can be <c>null</c> if default state from prefab should be
  /// used.
  /// </param>
  /// <returns>The newly created items.</returns>
  /// <seealso cref="CheckCanAddParts"/>
  InventoryItem[] AddParts(AvailablePart[] avParts, ConfigNode[] nodes);

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
  /// Forces the container to refresh its state according to the new state of the items.
  /// </summary>
  /// <remarks>
  /// Every change to any item in the inventory must result in calling of this method. The items
  /// will <i>not</i> send updates to the owner inventory automatically.
  /// <para>
  /// Before updating own state, the inventory will update every single item config. An exception
  /// will be made if parameter <paramref name="changedItems"/> is provided. In this case only the
  /// specified items will be updated. Callers can use this ability to optimize their calls and
  /// save CPU.
  /// </para>
  /// <para>
  /// This method will be called internally when the item number changes. In this case
  /// <paramref name="changedItems"/> is an empty collection. Normally, the external callers don't
  /// need to do that, since each implementation should keep control on the items number change.
  /// </para>
  /// </remarks>
  /// <param name="changedItems">
  /// The items, which changed state was the reason of the inventory update. If <c>null</c>, then
  /// all the items in the inventory will be updated. It can also be an empty array, in which case
  /// the inventory will refresh its own state, but not the items configs. 
  /// </param>
  /// <seealso cref="InventoryItem.itemConfig"/>
  void UpdateInventoryStats(InventoryItem[] changedItems);
}

}  // namespace
