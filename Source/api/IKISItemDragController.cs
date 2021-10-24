// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KIS2;
using System;
using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace
// ReSharper disable once IdentifierTypo
namespace KISAPIv2 {

/// <summary>Interface that controls the inventory items movements between the inventories and the scene.</summary>
/// <remarks>
/// This interface is not made for dealing with actual KIS items movements or instantiating the parts. Instead, it
/// serves as a proxy to connect a provider, the class that offers item(s) for movement, and a target, the class that
/// can consume the item(s). The providers should pro-actively call this interface implementation to offer the item(s)
/// for dragging. The targets must be reacting on their events to verify if they can/should consume anything from the
/// controller, and register themselves as targets if they can.
/// </remarks>
/// <seealso cref="IKisDragTarget"/>
public interface IKisItemDragController {
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
  UiKisInventorySlotDragIcon dragIconObj { get; }

  /// <summary>Target that currently has the pointer focus.</summary>
  /// <value>The object that represents GUI or <c>null</c> of there is none.</value>
  /// <seealso cref="SetFocusedTarget"/>
  GameObject focusedTarget { get; }

  /// <summary>Offers items for the dragging.</summary>
  /// <remarks>Items can belong to different inventories. The items can only be consumed all or none.</remarks>
  /// <param name="dragIcon">The icon that will be representing this operation.</param>
  /// <param name="items">
  /// The items being offered. The caller must ensure these items won't change their state between the start and the end
  /// of the drag operation. If this is not possible, the consume method must do verification and deny the operation if
  /// the change has occured.
  /// </param>
  /// <param name="consumeItemsFn">
  /// The function that will be called before consuming the items by the target. This function can cancel the operation,
  /// but it will be treated as an error by the target.
  /// </param>
  /// <param name="cancelItemsLeaseFn">
  /// The cleanup action that is called when the drag operation is cancelled. It's called before the
  /// <see cref="leasedItems"/> are cleaned up. This action must never fail.
  /// </param>
  /// <param name="allowInteractiveCancel">
  /// Indicates if user can cancel the drag operation from the keyboard. By default, the user MAY do this. If the
  /// provider that leases the items cannot allow it, it has to implement its own interactive approach.
  /// </param>
  /// <returns><c>true</c> if dragging has successfully started.</returns>
  /// <seealso cref="leasedItems"/>
  /// <seealso cref="ConsumeItems"/>
  /// <seealso cref="InventoryItem.isLocked"/>
  /// <seealso cref="IKisDragTarget.OnKisDragStart"/>
  bool LeaseItems(
      Texture dragIcon, ICollection<InventoryItem> items, Func<bool> consumeItemsFn, Action cancelItemsLeaseFn,
      bool allowInteractiveCancel = true);

  /// <summary>Indicates that the target is willing to consume the dragged items.</summary>
  /// <remarks>
  /// By calling this method the caller is stating that it's ready to take ownership to the <see cref="leasedItems"/>.
  /// If this method returns success, then the dragging mode ends.
  /// </remarks>
  /// <returns>
  /// The items to consume, or <c>null</c> if the provider refused the complete the deal. In the latter case the
  /// dragging operation stays running and unchanged.
  /// </returns>
  /// <seealso cref="leasedItems"/>
  /// <seealso cref="IKisDragTarget.OnKisDragEnd"/>
  InventoryItem[] ConsumeItems();

  /// <summary>Cancels the current dragging operation.</summary>
  /// <seealso cref="leasedItems"/>
  /// <seealso cref="IKisDragTarget.OnKisDragEnd"/>
  void CancelItemsLease();

  /// <summary>Sets the target GUI object that is currently owns the dragging focus.</summary>
  /// <remarks>
  /// <p>
  /// Even though any caller can set the value, only the actual UI handlers should be doing it. Exactly one GameObject
  /// can be a focus target at the moment. In a normal case, it's the dialog that has the pointer focus.
  /// </p>
  /// <p>
  /// When the focused control looses focus, it must call this method with <c>null</c> to indicate that the focus has
  /// been released.
  /// </p>
  /// </remarks>
  /// <param name="newTarget">The object that claims ownership on the focus.</param>
  /// <seealso cref="focusedTarget"/>
  void SetFocusedTarget(GameObject newTarget);

  /// <summary>Registers a drag target that will be notified about the dragged items status.</summary>
  /// <remarks>
  /// The drag controller will ask all the registered targets about the currently dragged items. If none of them can
  /// accept the drop, then UI will make it clear to the user.
  /// <p>
  /// If a target is registered when the dragging state is ON, then this target will immediately get
  /// <see cref="IKisDragTarget.OnKisDragStart"/>.
  /// </p>
  /// </remarks>
  /// <param name="target">The target to register.</param>
  /// <seealso cref="IKisDragTarget.OnKisDrag"/>
  void RegisterTarget(IKisDragTarget target);

  /// <summary>Unregisters the drag target.</summary>
  /// <remarks>
  /// If a target is unregistered when the dragging state is ON, then this target will immediately get
  /// <see cref="IKisDragTarget.OnKisDragEnd"/>.
  /// </remarks>
  /// <param name="target">The target to unregister.</param>
  void UnregisterTarget(IKisDragTarget target);
}

}  // namespace
