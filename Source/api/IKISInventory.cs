// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using UnityEngine;

// ReSharper disable once CheckNamespace
// ReSharper disable once IdentifierTypo
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
  /// <remarks>
  /// If this method replied "yes", then the <see cref="AddPart"/> method cannot fail. It is an exhaustive check for the
  /// part addition conditions.
  /// </remarks>
  /// <param name="partName">The part name to check.</param>
  /// <param name="node">
  /// The part's persisted state. If <c>null</c>, then a default state will be created from the prefab.
  /// </param>
  /// <param name="logErrors">
  /// If <c>true</c>, then the checking errors will be logged. Use it when calling this method as a precondition.
  /// </param>
  /// <returns>An empty list if the part can be added, or a list of reasons why not.</returns>
  /// <seealso cref="AddPart"/>
  ErrorReason[] CheckCanAddPart(string partName, ConfigNode node = null, bool logErrors = false);

  /// <summary>Adds a new part into the inventory.</summary>
  /// <remarks>
  /// This method does <i>not</i> verify if the part can fit the inventory. Doing this check is responsibility of the
  /// caller. If any compatibility setting is enabled, the add action can fail. The caller must check the output before
  /// assuming that the action has succeeded.
  /// </remarks>
  /// <param name="partName">The part name to add.</param>
  /// <param name="node">
  /// The part persisted state. An entry can be <c>null</c> if the default state from prefab should be used.
  /// </param>
  /// <returns>The newly created item or <c>null</c> if action failed.</returns>
  /// <seealso cref="CheckCanAddPart"/>
  InventoryItem AddPart(string partName, ConfigNode node = null);

  /// <summary>Adds an item from another inventory.</summary>
  /// <remarks>
  /// This method does <i>not</i> verify if the item can fit the inventory. Doing this check is responsibility of the
  /// caller. If any compatibility setting is enabled, the add action can fail. The caller must check the output before
  /// assuming that the action has succeeded.
  /// </remarks>
  /// <param name="item">The item to add.</param>
  /// <returns>
  /// The new item from the inventory or <c>null</c> if the action has failed. The ID of the new item will be different
  /// from the source item.
  /// </returns>
  /// <seealso cref="CheckCanAddPart"/>
  /// <seealso cref="InventoryItem.itemId"/>
  InventoryItem AddItem(InventoryItem item);

  /// <summary>Removes the specified item from the inventory.</summary>
  /// <remarks>
  /// The action can fail if the item is locked, doesn't exist or there are other conditions that prevent the logic to
  /// work.
  /// </remarks>
  /// <param name="item">The item to remove. It must belong to this inventory.</param>
  /// <returns><c>true</c> if removal was successful.</returns>
  /// <seealso cref="InventoryItem.isLocked"/>
  /// <seealso cref="InventoryItem.inventory"/>
  bool DeleteItem(InventoryItem item);

  /// <summary>Forces the container to refresh its state according to the new state of the items.</summary>
  /// <remarks>
  /// Every change to any item in the inventory must result in calling of this method. It relates to <i>any</i> state
  /// change, which can be a config node, or lock state, or whatever. The items do <i>not</i> send updates to the owner
  /// inventory automatically.
  /// <p>
  /// Before updating the own state, the inventory will update every single item config. An exception will be made if
  /// parameter <paramref name="changedItems"/> is provided. In this case only the specified items will be updated.
  /// Callers can use this ability to optimize their calls and save CPU.
  /// </p>
  /// <p>
  /// This method will be called internally when the item number changes. In this case <paramref name="changedItems"/>
  /// is an empty collection. Normally, the external callers don't need to do that, since each implementation should
  /// keep control on the items number change.
  /// </p>
  /// </remarks>
  /// <param name="changedItems">
  /// The items, which changed state was the reason of the inventory update. If <c>null</c>, then all the items in the
  /// inventory will be updated. It can also be an empty array, in which case the inventory will refresh its own state,
  /// but not the items configs.
  /// </param>
  /// <seealso cref="InventoryItem.itemConfig"/>
  void UpdateInventoryStats(InventoryItem[] changedItems);

  /// <summary>Finds an item by its unique ID.</summary>
  /// <remarks>
  /// This method is expected to be efficient. So, it can be called from the performance demanding applications.
  /// </remarks>
  /// <param name="itemId">The item ID to find.</param>
  /// <returns>The item or <c>null</c> if not found.</returns>
  /// <seealso cref="InventoryItem.itemId"/>
  /// <seealso cref="inventoryItems"/>
  InventoryItem FindItem(string itemId);
}

}  // namespace
