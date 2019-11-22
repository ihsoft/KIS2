﻿// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

// ReSharper disable once CheckNamespace
// ReSharper disable once IdentifierTypo
namespace KISAPIv2 {

/// <summary>
/// Interface for the components that need to be aware of the KIS dragging actions.
/// </summary>
/// <seealso cref="IKisItemDragController"/>
public interface IKisDragTarget {
  /// <summary>Notifies when new items are leased for dragging in the controller.</summary>
  /// <remarks>
  /// This method will be called when registering a target, if the dragging state was already
  /// started at the moment.
  /// </remarks>
  /// <seealso cref="IKisItemDragController.LeaseItems"/>
  /// <seealso cref="IKisItemDragController.RegisterTarget"/>
  void OnKisDragStart();

  /// <summary>Notifies when the dragging is over due to consume or cancel action.</summary>
  /// <remarks>
  /// When this method is called, the dragging mode is <i>already</i> cancelled. The only exception
  /// is the case when the target is being unregistered during the active dragging operation. In
  /// this case this method will be called with the <paramref name="isCancelled"/> parameter set to
  /// <c>true</c>, and the dragging state will still be in effect.
  /// </remarks>
  /// <param name="isCancelled">Tells if the drag mode ended due to the dragging cancel.</param>
  /// <seealso cref="IKisItemDragController.CancelItemsLease"/>
  /// <seealso cref="IKisItemDragController.ConsumeItems"/>
  /// <seealso cref="IKisItemDragController.UnregisterTarget"/>
  void OnKisDragEnd(bool isCancelled);

  /// <summary>Asks the target if the items can be consumed by it.</summary>
  /// <remarks>
  /// This callback is called each frame while there are items being dragged. Multiple targets can
  /// reply to this callback, and some (or all) of them can answer positively. It doesn't impound
  /// any obligations on the targets, but it does affect the GUI appearance of the dragged icon. If
  /// no targets can accept the items, the user will notice.
  /// </remarks>
  /// <param name="pointerMoved">Tells if mouse pointer has moved in this frame.</param>
  /// <returns><c>true</c> if the target can accept the currently dragged items.</returns>
  /// <seealso cref="IKisItemDragController.LeaseItems"/>
  bool OnKisDrag(bool pointerMoved);
}

}  // namespace
