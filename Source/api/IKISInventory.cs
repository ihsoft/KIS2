﻿// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace
// ReSharper disable once IdentifierTypo
namespace KISAPIv2 {

/// <summary>Basic interface that every inventory must implement to work with KIS.</summary>
public interface IKisInventory {
  /// <summary>The vessel that holds this inventory.</summary>
  Vessel ownerVessel { get; }

  /// <summary>The stock inventory module to which this inventory is attached.</summary>
  /// <value>The stock module instance.</value>
  /// <seealso cref="GetStockSlotIndex"/>
  ModuleInventoryPart stockInventoryModule { get; }

  /// <summary>Items in the inventory.</summary>
  /// <value>A dictionary of itemId=>item.</value>
  /// <seealso cref="InventoryItem.itemId"/>
  /// <seealso cref="AddItem"/>
  /// <seealso cref="DeleteItem"/>
  IReadOnlyDictionary<string, InventoryItem> inventoryItems { get; }

  /// <summary>Maximum dimensions of the inner inventory space.</summary>
  /// <remarks>Objects that are larger in any of the dimensions cannot fit the inventory.</remarks>
  Vector3 maxInnerSize { get; }

  /// <summary>Maximum volume of all the items that inventory can hold.</summary>
  /// <seealso cref="usedVolume"/>
  double maxVolume { get; }

  /// <summary>Current volume of all the items in the inventory.</summary>
  /// <remarks>
  /// In order to calculate this value, the whole content of the inventory needs to be examined in real-time. It may not
  /// be efficient when calling from the frame update methods. Consider caching mechanisms when the accurate real-time
  /// value is not a real need (e.g. in GUI).
  /// </remarks>
  /// <seealso cref="maxVolume"/>
  double usedVolume { get; }

  /// <summary>Total mass of all the items in the inventory.</summary>
  /// <remarks>
  /// In order to calculate this value, the whole content of the inventory needs to be examined in real-time. It may not
  /// be efficient when calling from the frame update methods. Consider caching mechanisms when the accurate real-time
  /// value is not a real need (e.g. in GUI).
  /// </remarks>
  /// <value>The total mass of the inventory content.</value>
  float contentMass { get; }

  /// <summary>Total cost of all the items in the inventory.</summary>
  /// <remarks>
  /// In order to calculate this value, the whole content of the inventory needs to be examined in real-time. It may not
  /// be efficient when calling from the frame update methods. Consider caching mechanisms when the accurate real-time
  /// value is not a real need (e.g. in GUI).
  /// </remarks>
  /// <value>The total cost of the inventory content.</value>
  float contentCost { get; }

  /// <summary>Verifies if the part can be added into the inventory.</summary>
  /// <remarks>
  /// If this method replied "yes", then the <see cref="AddPart"/> method cannot fail. It is an exhaustive check for the
  /// part addition conditions.
  /// </remarks>
  /// <param name="partName">The part name to check.</param>
  /// <param name="stockSlotIndex">
  /// Optional stock slot index. If provided, then the check will only be done for this specific stock slot. If the slot
  /// is full or incompatible with the item, the check will fail. Use this feature only when it's really important to
  /// store a new item into the specified stock slot.
  /// </param>
  /// <param name="logErrors">
  /// If <c>true</c>, then the checking errors will be logged. Use it when calling this method as a precondition.
  /// </param>
  /// <returns>An empty list if the part can be added, or a list of reasons why not.</returns>
  /// <seealso cref="AddPart"/>
  List<ErrorReason> CheckCanAddPart(string partName, int stockSlotIndex = -1, bool logErrors = false);

  /// <summary>Verifies if the item can be added into the inventory.</summary>
  /// <remarks>
  /// If this method replied "yes", then the <see cref="AddItem"/> method cannot fail. It is an exhaustive check for the
  /// part addition conditions.
  /// </remarks>
  /// <param name="item">The item to check.</param>
  /// <param name="stockSlotIndex">
  /// Optional stock slot index. If provided, then the check will only be done for this specific stock slot. If the slot
  /// is full or incompatible with the item, the check will fail. Use this feature only when it's really important to
  /// store a new item into the specified stock slot.
  /// </param>
  /// <param name="logErrors">
  /// If <c>true</c>, then the checking errors will be logged. Use it when calling this method as a precondition.
  /// </param>
  /// <returns>An empty list if the part can be added, or a list of reasons why not.</returns>
  /// <seealso cref="AddItem"/>
  List<ErrorReason> CheckCanAddItem(InventoryItem item, int stockSlotIndex = -1, bool logErrors = false);

  /// <summary>Adds a new part into the inventory.</summary>
  /// <remarks>
  /// This method does <i>not</i> verify if the part can fit the inventory. Doing this check is responsibility of the
  /// caller. If any compatibility setting is enabled, the add action can fail. The caller must check the output before
  /// assuming that the action has succeeded.
  /// </remarks>
  /// <param name="partName">The part name to add.</param>
  /// <param name="stockSlotIndex">
  /// Optional stock slot index. If not specified, the item will be added to any available stock slot. Otherwise,
  /// the specific slot will be examined to be compatible with the item, and if doesn't fit the action will fail.
  /// </param>
  /// <returns>The newly created item or <c>null</c> if action failed.</returns>
  /// <exception cref="InvalidOperationException">If part name is unknown.</exception>
  /// <seealso cref="CheckCanAddPart"/>
  InventoryItem AddPart(string partName, int stockSlotIndex = -1);

  /// <summary>Adds an item from another inventory.</summary>
  /// <remarks>
  /// This method does <i>not</i> verify if the item can fit the inventory. Doing this check is responsibility of the
  /// caller. If any compatibility setting is enabled, the add action can fail. The caller must check the output before
  /// assuming that the action has succeeded.
  /// </remarks>
  /// <param name="item">
  /// The item to add. It must be a detached item that doesn't belong to any other inventory. This item must not be used
  /// or re-used after it was successfully added to the inventory since it may affect the inventory.
  /// </param>
  /// <param name="stockSlotIndex">
  /// Optional stock slot index. If not specified, the item will be added to any available stock slot. Otherwise,
  /// the specific slot will be examined to be compatible with the item, and if it's not, then the action will fail.
  /// </param>
  /// <returns>
  /// The new item from the inventory or <c>null</c> if the action has failed. The ID of the new item will be different
  /// from the source item.
  /// </returns>
  /// <exception cref="InvalidOperationException">If the item already belongs to some inventory.</exception>
  /// <seealso cref="CheckCanAddItem"/>
  /// <seealso cref="InventoryItem.itemId"/>
  /// <seealso cref="InventoryItem.inventory"/>
  InventoryItem AddItem(InventoryItem item, int stockSlotIndex = -1);

  /// <summary>Removes the specified item from the inventory.</summary>
  /// <remarks>The action can fail if the item is locked or doesn't exist.</remarks>
  /// <param name="item">
  /// The item to remove. It must belong to this inventory. This item must not be used once it was removed from the
  /// inventory since it may affect the inventory.
  /// </param>
  /// <returns>The detached item if removal was successful, or NULL otherwise.</returns>
  /// <seealso cref="InventoryItem.isLocked"/>
  /// <seealso cref="InventoryItem.inventory"/>
  InventoryItem DeleteItem(InventoryItem item);

  /// <summary>Finds an item by its unique ID.</summary>
  /// <remarks>
  /// This method is expected to be efficient. So, it can be called from the performance demanding applications.
  /// </remarks>
  /// <param name="itemId">The item ID to find.</param>
  /// <returns>The item or <c>null</c> if not found.</returns>
  /// <seealso cref="InventoryItem.itemId"/>
  /// <seealso cref="inventoryItems"/>
  InventoryItem FindItem(string itemId);

  /// <summary>Returns the index of the stock slot in which the KIS item is stored.</summary>
  /// <remarks>
  /// Can be handy when performing logic that requires stock inventory interactions. Multiple KIS items can refer the
  /// same stock slot if the slot maximum amount is greater that 1.
  /// </remarks>
  /// <param name="item">The item to get the slot index for.</param>
  /// <returns>The index of the slot or <c>-1</c> if the item is not stored in this inventory.</returns>
  int GetStockSlotIndex(InventoryItem item);
}

}  // namespace
