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
/// <remarks>
/// The items are immutable objects. It means that if there was an action performed on an item, then it's state must be
/// treated as inconsistent. Every updating action on the item is required to return a new instance of it. The original
/// item must be considered INVALID after this call and never used for any purpose.
/// </remarks>
// ReSharper disable once InconsistentNaming
// ReSharper disable once IdentifierTypo
public interface InventoryItem {
  /// <summary>The inventory that owns this item.</summary>
  /// <remarks>
  /// This is the inventory at which the item was initially created or loaded for. It's an immutable property that
  /// doesn't change even if the item was deleted or moved from that inventory afterwards. 
  /// </remarks>
  IKisInventory inventory { get; }

  /// <summary>Unique string ID that identifies the item within the inventory.</summary>
  /// <remarks>Once the item is created, its ID cannot change.</remarks>
  string itemId { get; }

  /// <summary>The actual part object which this item represents.</summary>
  /// <remarks>
  /// By the contract it must never be a part prefab, even though the prefab is technically a "part object".
  /// </remarks>
  /// <value>The part object or <c>null</c> if no material part relates to the item.</value>
  Part materialPart { get; set; }

  /// <summary>Read-only. Part info.</summary>
  /// <value>The part info from prefab. It's never NULL.</value>
  AvailablePart avPart { get; }

  /// <summary>Read-only. Icon that represents this item.</summary>
  /// <remarks>
  /// It's a low resolution icon that is suitable for UI, but may not be good for the bigger elements.
  /// </remarks>
  /// <value>The icon texture. It's never NULL.</value>
  Texture iconImage { get; }

  /// <summary>Read-only. Cached variant applied to this item.</summary>
  /// <remarks>The variant instance is taken from the part prefab.</remarks>
  /// <value>The variant or <c>null</c> if the part doesn't have any.</value>
  /// <seealso cref="SyncToSnapshot"/>
  PartVariant variant { get; }

  /// <summary>The name of the current variant.</summary>
  /// <value>The variant name or empty string if the part doesn't have variants.</value>
  /// <seealso cref="SyncToSnapshot"/>
  string variantName { get; }

  /// <summary>Read-only. The part's snapshot.</summary>
  /// <remarks>
  /// This is a SHARED instance of the snapshot. Multiple items from the different inventories can use it at the same
  /// time. NEVER update it! If the snapshot needs to be changed, use <see cref="mutableSnapshot"/>.
  /// </remarks>
  /// <value>The part snapshot.</value>
  /// <seealso cref="stockSlot"/>
  ProtoPartSnapshot snapshot { get; }

  /// <summary>The part's mutable snapshot.</summary>
  /// <remarks>
  /// This snapshot can be modified to change the item's config. However, it's only available on a detached item,
  /// i.e. the item that doesn't belong to any inventory. If an item in inventory needs to be modified, delete it from
  /// the inventory, update the resulted detached item, and add it back.
  /// </remarks>
  /// <exception cref="InvalidOperationException">If the item is not detached.</exception>
  /// <seealso cref="SyncToSnapshot"/>
  ProtoPartSnapshot mutableSnapshot { get; }

  /// <summary>Index of the stock slot where this item is stored into.</summary>
  /// <value>The index in <see cref="ModuleInventoryPart.storedParts"/> or -1 if the item is detached.</value>
  /// <seealso cref="stockSlot"/>
  public int stockSlotIndex { get; }

  /// <summary>Stock inventory slot where this item is stored.</summary>
  /// <remarks>Multiple items can be stored in the same stock slot.</remarks>
  /// <value>The stock slot or NULL if item is detached.</value>
  /// <seealso cref="stockSlotIndex"/>
  public StoredPart stockSlot { get; }

  /// <summary>Volume that part would take in its current state.</summary>
  /// <remarks>
  /// The snapshot state can greatly affect the volume. E.g. most part take several times more volume when deployed.
  /// </remarks>
  /// <value>The volume in <c>litres</c>.</value>
  /// <seealso cref="SyncToSnapshot"/>
  double volume { get; }

  /// <summary>Boundary size of the current part state.</summary>
  /// <value>The size in metres in each dimension.</value>
  /// <seealso cref="SyncToSnapshot"/>
  Vector3 size { get; }

  /// <summary>Mass of the part without resources.</summary>
  /// <value>The mass in <c>tons</c>.</value>
  /// <seealso cref="SyncToSnapshot"/>
  double dryMass { get; }

  /// <summary>Cost of the part without resources.</summary>
  /// <value>The cost in <c>credits</c>.</value>
  /// <seealso cref="SyncToSnapshot"/>
  double dryCost { get; }

  /// <summary>Mass of the part with all available resources.</summary>
  /// <value>The mass in <c>tons</c>.</value>
  /// <seealso cref="SyncToSnapshot"/>
  double fullMass { get; }

  /// <summary>Cost of the part with all available resources.</summary>
  /// <value>The cost in <c>credits</c>.</value>
  /// <seealso cref="SyncToSnapshot"/>
  double fullCost { get; }

  /// <summary>Cached available resources in the part.</summary>
  /// <remarks>
  /// This property is mutable. The amount of resources can be changed on the item without removing it from the
  /// inventory. However, you still need to notify item/inventory about the change.
  /// </remarks>
  /// <value>The resources from the snapshot.</value>
  /// <seealso cref="snapshot"/>
  /// <seealso cref="SyncToSnapshot"/>
  ProtoPartResourceSnapshot[] resources { get; }

  /// <summary>Read-only. Cached science data in the part.</summary>
  /// <value>The science data from the snapshot.</value>
  /// <seealso cref="snapshot"/>
  /// <seealso cref="SyncToSnapshot"/>
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
  /// <value>The list of checker functions. This list can be modified by the callers in runtime.</value>
  /// <seealso cref="CheckCanChangeOwnership"/>
  List<Func<InventoryItem, ErrorReason?>> checkChangeOwnershipPreconditions { get; }

  /// <summary>Sets locked state.</summary>
  /// <seealso cref="isLocked"/>
  void SetLocked(bool newState);

  /// <summary>Updates all the item's cached values to make them matching the snapshot.</summary>
  /// <remarks>
  /// Call this method if the item's properties need to be accessed after modifying the snapshot. If the modified item
  /// needs to be added into an inventory, the sync is not needed. 
  /// </remarks>
  /// <seealso cref="mutableSnapshot"/>
  void SyncToSnapshot();

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
