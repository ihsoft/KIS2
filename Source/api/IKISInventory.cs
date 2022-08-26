// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace
// ReSharper disable once IdentifierTypo
namespace KISAPIv2 {

/// <summary>Basic interface that every inventory must implement to work with KIS.</summary>
public interface IKisInventory {
  /// <summary>The part that holds this inventory.</summary>
  /// <remarks>A part can have multiple inventories.</remarks>
  Part ownerPart { get; }

  /// <summary>The stock inventory module to which this inventory is attached.</summary>
  /// <value>The stock module instance.</value>
  ModuleInventoryPart stockInventoryModule { get; }

  /// <summary>Items in the inventory.</summary>
  /// <value>A dictionary of itemId=>item.</value>
  /// <seealso cref="InventoryItem.itemId"/>
  /// <seealso cref="AddPart"/>
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

  /// <summary>Verifies if the part can be added into the inventory without breaking its constraints.</summary>
  /// <remarks>
  /// If this method replied "yes", then the <see cref="AddPart"/> method cannot fail. It is an exhaustive check for the
  /// part addition conditions.
  /// </remarks>
  /// <param name="partSnapshot">The part to check.</param>
  /// <param name="logErrors">
  /// If <c>true</c>, then the checking errors will be logged. Use it when calling this method as a precondition.
  /// </param>
  /// <returns>An empty list if the part can be added, or a list of reasons why not.</returns>
  /// <seealso cref="AddPart"/>
  List<ErrorReason> CheckCanAddPart(ProtoPartSnapshot partSnapshot, bool logErrors = false);

  /// <summary>Adds a part into inventory.</summary>
  /// <remarks>
  /// This method does <i>not</i> verify if the part can fit the inventory. Doing this check is responsibility of the
  /// caller.
  /// </remarks>
  /// <param name="partSnapshot">The part to add.</param>
  /// <returns>The new item from the inventory or <c>null</c> if the action has failed.</returns>
  /// <seealso cref="CheckCanAddPart"/>
 InventoryItem AddPart(ProtoPartSnapshot partSnapshot);

  /// <summary>Removes the specified item from the inventory.</summary>
  /// <remarks>The action can fail if the item is locked or doesn't exist.</remarks>
  /// <param name="itemId">The ID of the item to remove.</param>
  /// <returns>The detached item if removal was successful, or NULL otherwise.</returns>
  /// <seealso cref="InventoryItem.isLocked"/>
  InventoryItem DeleteItem(string itemId);
}

}  // namespace
