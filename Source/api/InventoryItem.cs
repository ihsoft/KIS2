// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

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

  /// <summary>Real part in the scene if there was one created.</summary>
  /// <remarks>A real part is only defined when the items is equipped in mode "Part".</remarks>
  /// <value>The part or <c>null</c>.</value>
  Part physicalPart { get; }

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
}

}  // namespace
