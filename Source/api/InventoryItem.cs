﻿// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace
// ReSharper disable once IdentifierTypo
namespace KISAPIv2 {

/// <summary>Basic container for a single inventory item.</summary>
// ReSharper disable once InconsistentNaming
// ReSharper disable once IdentifierTypo
public interface InventoryItem {
  /// <summary>The inventory that owns this item.</summary>
  /// <remarks>
  /// This is the inventory at which the item was initially created or loaded for. It's an immutable property that
  /// doesn't change even if the item was deleted or moved from that inventory afterwards. If the owner of the instance
  /// needs to ensure the item still belongs to the inventory, it should verify it by requesting the relevant inventory. 
  /// </remarks>
  IKisInventory inventory { get; }

  /// <summary>Unique string ID that identifies the item within the inventory.</summary>
  /// <remarks>The ID is generated when the part is first time added into inventory. It's an immutable value.</remarks>
  string itemId { get; }

  /// <summary>Part proto.</summary>
  AvailablePart avPart { get; }

  /// <summary>The variant applied to this item.</summary>
  /// <value>The variant or <c>null</c> if the part doesn't have any.</value>
  PartVariant variant { get; }

  /// <summary>Persisted state of the part.</summary>
  /// <remarks>
  /// This node can be updated by the external callers, but they must letting the item know that the config has changed
  /// via the <see cref="UpdateConfig"/> call. Otherwise, the state of the item and the owning inventory will be
  /// inconsistent.
  /// </remarks>
  /// <seealso cref="UpdateConfig"/>
  ConfigNode itemConfig { get; }

  /// <summary>The part's snapshot.</summary>
  /// <remarks>
  /// This instance must NOT be changed. The changes will not be propagated to the item config, but they may affect the
  /// downstream logic. In case of the item state needs to be changed, use <see cref="itemConfig"/>.
  /// </remarks>
  /// <seealso cref="UpdateConfig"/>
  ProtoPartSnapshot snapshot { get; }

  /// <summary>Cached volume that part would take in its current state.</summary>
  /// <remarks>
  /// The persisted state can greatly affect the volume. E.g. most part take several times more volume when deployed.
  /// </remarks>
  /// <value>The volume in <c>litres</c>.</value>
  /// <seealso cref="UpdateConfig"/>
  double volume { get; }

  /// <summary>Cached boundary size of the current part state.</summary>
  /// <value>The size in metres in each dimension.</value>
  Vector3 size { get; }

  /// <summary>Cached mass of the part without resources.</summary>
  /// <value>The mass in <c>tons</c>.</value>
  /// <seealso cref="UpdateConfig"/>
  double dryMass { get; }

  /// <summary>Cached cost of the part without resources.</summary>
  /// <seealso cref="UpdateConfig"/>
  double dryCost { get; }

  /// <summary>Mass of the part with all available resources.</summary>
  /// <value>The mass in <c>tons</c>.</value>
  double fullMass { get; }

  /// <summary>Cached cost of the part with all available resources.</summary>
  /// <seealso cref="UpdateConfig"/>
  double fullCost { get; }

  /// <summary>Cached available resources in the part.</summary>
  /// <seealso cref="UpdateConfig"/>
  ProtoPartResourceSnapshot[] resources { get; }

  /// <summary>Cached science data in the part.</summary>
  /// <seealso cref="UpdateConfig"/>
  ScienceData[] science { get; }

  /// <summary>Tells if this item is currently equipped on the actor.</summary>
  bool isEquipped { get; }

  /// <summary>Tells if this item must not be affected externally.</summary>
  /// <remarks>
  /// The locked items are in a process of some complex, possibly multi-frame, operation. Only the executor of this
  /// process should deal with this item, the other actors should not interfere.
  /// </remarks>
  /// <seealso cref="SetLocked"/>
  bool isLocked { get; }

  /// <summary>The list of callbacks to be called when a change ownership action is attempted on the item.</summary>
  /// <remarks>
  /// The item will only be considered to the ownership change if none of the callbacks has replied with a non-empty
  /// error reason. These callbacks let the external actors to have a control over the inventory item's fate.
  /// </remarks>
  /// <seealso cref="CheckCanChangeOwnership"/>
  List<Func<ErrorReason?>> checkChangeOwnershipPreconditions { get; }

  /// <summary>Sets locked state.</summary>
  /// <remarks>
  /// The inventory may need to know if the item's lock stat has updated. The actor, that changes the state, is
  /// responsible to notify the inventory via the <see cref="IKisInventory.UpdateInventoryStats"/> method.
  /// </remarks>
  /// <seealso cref="isLocked"/>
  /// <seealso cref="IKisInventory.UpdateInventoryStats"/>
  void SetLocked(bool newState);

  /// <summary>Updates all cached values from the part's config node.</summary>
  /// <remarks>
  /// This method only updates a single item. It will not update the inventory. Avoid calling this method directly.
  /// Instead, call the <see cref="IKisInventory.UpdateInventoryStats"/> on the owner inventory to ensure all the
  /// changes are accounted.
  /// </remarks>
  /// <seealso cref="itemConfig"/>
  /// <seealso cref="inventory"/>
  /// <seealso cref="IKisInventory.UpdateInventoryStats"/>
  void UpdateConfig();

  /// <summary>Gets a value from the config by the specified path</summary>
  /// <param name="path">The "/" delimited path to the value.</param>
  /// <typeparam name="T">The type of the value. Only the ordinary values are recognized.</typeparam>
  /// <returns>The requested value or <c>null</c> if the value is not found.</returns>
  /// <exception cref="ArgumentException">If value cannot be parsed as the requested type.</exception>
  T? GetConfigValue<T>(string path) where T : struct;

  /// <summary>Sets a value in the config by the specified path</summary>
  /// <remarks> 
  /// The changed are not automatically picked up by the item. The caller is responsible to either update the item's
  /// state via <see cref="UpdateConfig"/> or by using the <see cref="IKisInventory.UpdateInventoryStats"/> semantic.
  /// </remarks>
  /// <param name="path">The "/" delimited path to the value. If the path doesn't exist, it will be created.</param>
  /// <param name="value">The value to set.</param>
  /// <typeparam name="T">The type of the value. Only the ordinary values are recognized.</typeparam>
  /// <seealso cref="UpdateConfig"/>
  /// <seealso cref="IKisInventory.UpdateInventoryStats"/>
  void SetConfigValue<T>(string path, T value) where T : struct;

  /// <summary>
  /// Verifies the item dynamic conditions that may prevent this item to be moved between the inventories or be added
  /// into a new one.
  /// </summary>
  /// <remarks>
  /// This method guards the item's ownership change. It's the first condition to be checked in
  /// <see cref="IKisInventory.CheckCanAddItem"/>.
  /// </remarks>
  /// <returns>The error that doesn't allow the add/move action. Or an empty value.</returns>
  /// <seealso cref="checkChangeOwnershipPreconditions"/>
  List<ErrorReason> CheckCanChangeOwnership();
}

}  // namespace
