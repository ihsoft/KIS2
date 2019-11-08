// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using UnityEngine;

namespace KISAPIv2 {

/// <summary>Basic container for a single inventory item.</summary>
public interface InventoryItem {
  /// <summary>The inventory that owns this item.</summary>
  /// <remarks>
  /// This is the inventory at which the item was intially created. If it was deleted form that
  /// inventory afterwards, the link still can point to the former parent. It's not allowed or
  /// expected that an item changes inventory parent during its lifetime.
  /// </remarks>
  IKisInventory inventory { get; }

  /// <summary>Part poro.</summary>
  AvailablePart avPart { get; }

  /// <summary>Real part in the secene if there was one created.</summary>
  /// <remarks>A real part is only defined when the items is equipped in mode "Part".</remarks>
  /// <value>The part or <c>null</c>.</value>
  Part physicalPart { get; }

  /// <summary>Persisted state of the part.</summary>
  /// <remarks>
  /// This node can be updated by the external callers, but they must letting the item know that the
  /// config has changed vai the <see cref="UpdateConfig"/> call. Otherwise, teh state of the item
  /// and the owning inventory will be inconsistent.
  /// </remarks>
  /// <seealso cref="UpdateConfig"/>
  ConfigNode itemConfig { get; }

  /// <summary>Cached volume that part would take in its current state.</summary>
  /// <remarks>
  /// The persisted state can greatly affect the volume. E.g. most part take several times more
  /// volume when deployed.
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

  /// <summary>Mass of the part with all availabe resources.</summary>
  /// <value>The mass in <c>tons</c>.</value>
  double fullMass { get; }

  /// <summary>Cached cost of the part with all availabe resources.</summary>
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

  /// <summary>Tells if this item must not by affected externally.</summary>
  /// <remarks>
  /// The locked items are in a process of some complex, possibly multi-frame, operation. Only the
  /// executor of this process should deal with this item, the other actors should not interfere.
  /// </remarks>
  /// <seealso cref="SetLocked"/>
  bool isLocked { get; }

  /// <summary>Sets locked state.</summary>
  /// <seealso cref="isLocked"/>
  void SetLocked(bool newState);

  /// <summary>Updates all cached values from the part's config node.</summary>
  /// <remarks>
  /// This method must always be called when the node is changed. Note, that this method must
  /// <i>not</i> notify the owner inventory about the updates. The actor, that changes the item, is
  /// responsible to do that.
  /// </remarks>
  /// <seealso cref="itemConfig"/>
  /// <seealso cref="inventory"/>
  /// <seealso cref="IKisInventory.UpdateInventoryStats"/>
  void UpdateConfig();
}

}  // namespace
