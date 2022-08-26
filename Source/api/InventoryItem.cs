// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using UnityEngine;

// ReSharper disable once CheckNamespace
// ReSharper disable once IdentifierTypo
namespace KISAPIv2 {

/// <summary>Basic container for a single inventory item.</summary>
/// <remarks>
/// The items are immutable objects, but they depend on the inventory they belong to. If an item was added to or removed
/// from inventory, it must not be used for any purpose after that. Every updating action on the item is required to
/// return a new consistent instance of the item.
/// </remarks>
// ReSharper disable once InconsistentNaming
// ReSharper disable once IdentifierTypo
public interface InventoryItem {
  /// <summary>The inventory that owns this item.</summary>
  IKisInventory inventory { get; }

  /// <summary>Unique string ID that identifies the item within the inventory.</summary>
  string itemId { get; }

  /// <summary>A real part which this item represents.</summary>
  /// <remarks>
  /// This relation is not required. It's used when a part from the scene need to be in sync with an item in inventory
  /// or during the drag operation.
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
  PartVariant variant { get; }

  /// <summary>The name of the current variant.</summary>
  /// <value>The variant name or empty string if the part doesn't have variants.</value>
  string variantName { get; }

  /// <summary>Read-only. The part's snapshot.</summary>
  /// <remarks>
  /// This is a SHARED instance of the snapshot. Multiple items from the different inventories can use it at the same
  /// time. NEVER update it!
  /// </remarks>
  /// <value>The part snapshot.</value>
  ProtoPartSnapshot snapshot { get; }

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
  double volume { get; }

  /// <summary>Boundary size of the current part state.</summary>
  /// <value>The size in metres in each dimension.</value>
  Vector3 size { get; }

  /// <summary>Mass of the part without resources.</summary>
  /// <value>The mass in <c>tons</c>.</value>
  double dryMass { get; }

  /// <summary>Cost of the part without resources.</summary>
  /// <value>The cost in <c>credits</c>.</value>
  double dryCost { get; }

  /// <summary>Mass of the part with all available resources.</summary>
  /// <value>The mass in <c>tons</c>.</value>
  double fullMass { get; }

  /// <summary>Cost of the part with all available resources.</summary>
  /// <value>The cost in <c>credits</c>.</value>
  double fullCost { get; }

  /// <summary>Available resources in the part.</summary>
  /// <remarks>
  /// This property is mutable. The amount of resources can be changed on the item without removing it from the
  /// inventory.
  /// </remarks>
  /// <value>The resources from the snapshot.</value>
  /// <seealso cref="snapshot"/>
  ProtoPartResourceSnapshot[] resources { get; }

  /// <summary>Read-only. Science data in the part.</summary>
  /// <value>The science data from the snapshot.</value>
  /// <seealso cref="snapshot"/>
  ScienceData[] science { get; }

  /// <summary>Indicates that this item cannot be removed.</summary>
  /// <remarks>
  /// Locking items allows performing multi frame operations on a set of items. While locked, it's guaranteed that the
  /// item stays in the inventory.
  /// </remarks>
  bool isLocked { get; set;  }
}

}  // namespace
