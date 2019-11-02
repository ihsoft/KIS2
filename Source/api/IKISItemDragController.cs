// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KIS2;
using System;
using UnityEngine;

namespace KISAPIv2 {

/// <summary>
/// Interface that controlls the inventory items movements between the inventories and the scene.
/// </summary>
/// <remarks>
/// This interface is not made for dealing with actual KIS items movements or instantiating the
/// parts. Instead, it serves as a proxy to connect a provider, the class that offers item(s) for
/// movement, and a target, the class that can consume the item(s). The providers should
/// pro-actively call this interface implementation to offer the item(s) for dragging. The targets
/// must be reacting on their events to verify if they can/should consume anything from the
/// controller, and register themselves as targets if they can.
/// </remarks>
/// <seealso cref="IKISDragTarget"/>
public interface IKISItemDragController {
  /// <summary>Tells if there are items being dragged by the controller.</summary>
  /// <seealso cref="leasedItems"/>
  bool isDragging { get; }

  /// <summary>Items that are currently being dragged.</summary>
  /// <value>The list of items or <c>null</c> if nothing is being dragged.</value>
  /// <seealso cref="isDragging"/>
  /// <seealso cref="LeaseItems"/>
  InventoryItem[] leasedItems { get; }

  /// <summary>Drag icon object for the current drag operation.</summary>
  /// <remarks>
  /// The active state of this object can be adjusted by the third-party scripts, but it must not
  /// be destroyed from the outside.
  /// </remarks>
  /// <value>The object or <c>null</c> if nothing is being dragged.</value>
  /// <seealso cref="isDragging"/>
  UIKISInventorySlotDragIcon dragIconObj { get; }

  /// <summary>Offers items for the dragging.</summary>
  /// <param name="dragIcon">The icon that will be representing this opertion.</param>
  /// <param name="items">
  /// The items being offered. The caller must ensure these items won't change teir state bewteen
  /// the start and the end of the drag operation. if this is not possible, the consume method must
  /// do verification and deny the operation as needed.
  /// </param>
  /// <param name="consumeItemsFn">
  /// The function that will be called before consuming the items by the target. This function can
  /// cancel the operation, but it will be treated as an error by the target.
  /// </param>
  /// <param name="cancelItemsLeaseFn">
  /// The cleanup action that is called when the drag operationis cancelled. This action must never
  /// fail.
  /// </param>
  /// <returns><c>true</c> if dragging has successfully started.</returns>
  /// <seealso cref="leasedItems"/>
  /// <seealso cref="ConsumeItems"/>
  /// <seealso cref="InventoryItem.isLocked"/>
  /// <seealso cref="IKISDragTarget.OnKISDragStart"/>
  bool LeaseItems(Texture dragIcon, InventoryItem[] items,
                  Func<bool> consumeItemsFn, Action cancelItemsLeaseFn);

  /// <summary>Indicates that the target is willing to consume the dragged items.</summary>
  /// <remarks>
  /// By calling this method the caller is stating that it's ready to take ownership to the
  /// <see cref="leasedItems"/>. If this method retuns success, then the dragging mode ends.
  /// </remarks>
  /// <returns>
  /// The items to consume, or <c>null</c> if the provider refused the complete the deal. In the
  /// latter case the dragging operation stays running and unchanged.
  /// </returns>
  /// <seealso cref="leasedItems"/>
  /// <seealso cref="IKISDragTarget.OnKISDragEnd"/>
  InventoryItem[] ConsumeItems();

  /// <summary>Cancels the current dragging operaton.</summary>
  /// <seealso cref="leasedItems"/>
  /// <seealso cref="IKISDragTarget.OnKISDragEnd"/>
  void CancelItemsLease();

  /// <summary>
  /// Registers a drag target that will be notified about the dragged items status.
  /// </summary>
  /// <remarks>
  /// The drag controller will ask all the registered targets about the currently dragged items. If
  /// none of them can accept the drop, then UI will make it clear to the user.
  /// <para>
  /// If a target is registered when the dragging state is ON, then this target will immediately get
  /// <see cref="IKISDragTarget.OnKISDragStart"/>.
  /// </para>
  /// </remarks>
  /// <param name="target">The target to register.</param>
  /// <seealso cref="IKISDragTarget.OnKISDrag"/>
  void RegisterTarget(IKISDragTarget target);

  /// <summary>Unregisters the drag target.</summary>
  /// <remarks>
  /// If a target is unregistered when the dragging state is ON, then this target will immediately
  /// get <see cref="IKISDragTarget.OnKISDragEnd"/>.
  /// </remarks>
  /// <param name="target">The target to unregister.</param>
  void UnregisterTarget(IKISDragTarget target);
}

}  // namespace
