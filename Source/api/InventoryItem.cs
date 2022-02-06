// Kerbal Inventory System
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

  /// <summary>Index of slot in the stock inventory.</summary>
  /// <remarks>
  /// Indicates which stock inventory slot contains this item. If the stock slot contains a stack of items, then
  /// multiple items will be referring the same slot.
  /// </remarks>
  /// <value>The stock slot index or <c>-1</c> if the item doesn't belong to any inventory.</value>
  int stockSlotIndex { get; }

  /// <summary>The actual part object which this item represents.</summary>
  /// <remarks>
  /// By the contract it must never be a part prefab, even though the prefab is technically a "part object".
  /// </remarks>
  /// <value>The part object or <c>null</c> if no material part relates to the item.</value>
  Part materialPart { get; set; }

  /// <summary>Part proto.</summary>
  AvailablePart avPart { get; }

  /// <summary>Icon that represents this item.</summary>
  /// <remarks>
  /// It's a low resolution icon that is suitable for UI, but may not be good for the bigger elements.
  /// </remarks>
  /// <value>The icon texture. It's never NULL.</value>
  Texture iconImage { get; }

  /// <summary>The variant applied to this item.</summary>
  /// <value>The variant or <c>null</c> if the part doesn't have any.</value>
  PartVariant variant { get; }

  /// <summary>The name of the current variant.</summary>
  /// <value>The variant name or empty string if the part doesn't have variants.</value>
  string variantName { get; }

  /// <summary>The part's snapshot.</summary>
  /// <remarks>
  /// This instance is a direct reflection from the stock inventory. Its state can be changed but keep in mind that it
  /// affects the stock logic. If any changes were made to the snapshot, the item must be notified via
  /// <see cref="UpdateItem"/>. Moreover, the owner inventory must also be notified via
  /// <see cref="IKisInventory.UpdateInventory"/>.
  /// </remarks>
  /// <seealso cref="UpdateItem"/>
  /// <seealso cref="IKisInventory.UpdateInventory"/>
  ProtoPartSnapshot snapshot { get; }

  /// <summary>Cached volume that part would take in its current state.</summary>
  /// <remarks>
  /// The snapshot state can greatly affect the volume. E.g. most part take several times more volume when deployed.
  /// </remarks>
  /// <value>The volume in <c>litres</c>.</value>
  /// <seealso cref="UpdateItem"/>
  double volume { get; }

  /// <summary>Cached boundary size of the current part state.</summary>
  /// <value>The size in metres in each dimension.</value>
  /// <seealso cref="UpdateItem"/>
  Vector3 size { get; }

  /// <summary>Cached mass of the part without resources.</summary>
  /// <value>The mass in <c>tons</c>.</value>
  /// <seealso cref="UpdateItem"/>
  double dryMass { get; }

  /// <summary>Cached cost of the part without resources.</summary>
  /// <value>The cost in <c>credits</c>.</value>
  /// <seealso cref="UpdateItem"/>
  double dryCost { get; }

  /// <summary>Cached mass of the part with all available resources.</summary>
  /// <value>The mass in <c>tons</c>.</value>
  /// <seealso cref="UpdateItem"/>
  double fullMass { get; }

  /// <summary>Cached cost of the part with all available resources.</summary>
  /// <value>The cost in <c>credits</c>.</value>
  /// <seealso cref="UpdateItem"/>
  double fullCost { get; }

  /// <summary>Cached available resources in the part.</summary>
  /// <value>The resources from the snapshot.</value>
  /// <seealso cref="UpdateItem"/>
  /// <seealso cref="snapshot"/>
  ProtoPartResourceSnapshot[] resources { get; }

  /// <summary>Cached science data in the part.</summary>
  /// <value>The science data from the snapshot.</value>
  /// <seealso cref="UpdateItem"/>
  /// <seealso cref="snapshot"/>
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
  /// <value>The list of checker functions. This list can be modified by the callers.</value>
  /// <seealso cref="CheckCanChangeOwnership"/>
  List<Func<InventoryItem, ErrorReason?>> checkChangeOwnershipPreconditions { get; }

  /// <summary>Sets locked state.</summary>
  /// <remarks>
  /// The inventory may need to know if the item's lock stat has updated. The actor, that changes the state, is
  /// responsible to notify the inventory via the <see cref="IKisInventory.UpdateInventory"/> method.
  /// </remarks>
  /// <seealso cref="isLocked"/>
  /// <seealso cref="IKisInventory.UpdateInventory"/>
  void SetLocked(bool newState);

  /// <summary>Updates all the cached items values to make them matching the snapshot.</summary>
  /// <remarks>
  /// This method only updates a single item. It will not update the inventory. Avoid calling this method directly.
  /// Instead, call the <see cref="IKisInventory.UpdateInventory"/> on the owner inventory to ensure all the
  /// changes are accounted.
  /// </remarks>
  /// <seealso cref="inventory"/>
  /// <seealso cref="IKisInventory.UpdateInventory"/>
  /// <seealso cref="snapshot"/>
  void UpdateItem();

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
