// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace KISAPIv2 {

/// <summary>Basic interface that every inventory must implement to work with KIS.</summary>
public interface IKISInventory {
  /// <summary>Items in the inventory.</summary>
  InventoryItem[] items { get; }

  /// <summary>Adds a new item into the inventory.</summary>
  /// <param name="avPart">The part proto.</param>
  /// <param name="node">The part's persisted state.</param>
  /// <param name="variant">
  /// The part's variant. If it's <c>null</c>, then it will be seacrhed in the persisted state.
  /// </param>
  /// <returns>The newly created item.</returns>
  InventoryItem AddItem(AvailablePart avPart, ConfigNode node, PartVariant variant = null);

  /// <summary>Adds a new item into the inventory from a real part in the scene.</summary>
  /// <remarks>
  /// The original part won't be affected. Only its copy will be saved into inventory.
  /// </remarks>
  /// <param name="part">The real part in the secene to capture.</param>
  /// <returns>The newly created item.</returns>
  InventoryItem AddItem(Part part);

  /// <summary>Removes the specified item from inventory.</summary>
  /// <remarks>Locked items cannot be removed.</remarks>
  /// <param name="item">The item to remove.</param>
  /// <returns><c>null</c> if removed successfully, or a list of errors.</returns>
  /// <seealso cref="SetItemLock"/>
  ErrorReason[] DeleteItem(InventoryItem item);

  /// <summary>
  /// Puts a lock state on the item that prevents any updates to it in this inventory.
  /// </summary>
  /// <param name="item">The item to put lock on.</param>
  /// <param name="isLocked">The lock state.</param>
  /// <seealso cref="DeleteItem"/>
  /// <seealso cref="InventoryItem.isLocked"/>
  void SetItemLock(InventoryItem item, bool isLocked);
}

}  // namespace
