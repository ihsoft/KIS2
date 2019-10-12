// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace KISAPIv2 {

/// <summary>
/// Interface for the components that need to be aware of the KIS dragging actions.
/// </summary>
/// <seealso cref="KISItemDragController"/>
public interface IKISDragTarget {
  /// <summary>Notifies when new items are leased for dragging in the controller.</summary>
  /// <remarks>
  /// This method will be called when registering a target, if the dragging state was already
  /// started at the moment.
  /// </remarks>
  /// <seealso cref="KISItemDragController.LeaseItems"/>
  /// <seealso cref="KISItemDragController.RegisterTarget"/>
  void OnKISDragStart();

  /// <summary>Notifies when the draggin is over due to consume or cancel action.</summary>
  /// <remarks>
  /// This method will be called when unregistering a target, if the dragging state was already
  /// started at the moment. If that's the case, the <paramref name="isCancelled"/> parameter will
  /// be <c>true</c>.
  /// </remarks>
  /// <param name="isCancelled">Tells if the drag mode ended due to the dragging cancel.</param>
  /// <seealso cref="KISItemDragController.CancelItemsLease"/>
  /// <seealso cref="KISItemDragController.ConsumeItems"/>
  /// <seealso cref="KISItemDragController.UnregisterTarget"/>
  void OnKISDragEnd(bool isCancelled);

  /// <summary>Asks the target if the items can be consumed by it.</summary>
  /// <remarks>
  /// This callback is called each frame while there are items being dragged. Mutliple targets can
  /// reply to this callback, and some (or all) of them can answer positively. It doesn't impound
  /// any obligations on the tragets, but it does affect the GUI appearance of the dragged icon. If
  /// no targets can accept the items, the user will notice.
  /// </remarks>
  /// <param name="pointerMoved">Tells if mouse pointer has moved in this frame.</param>
  /// <returns><c>true</c> if the traget can accept the currently dragged items.</returns>
  /// <seealso cref="KISItemDragController.LeaseItems"/>
  bool OnKISDrag(bool pointerMoved);
}

}  // namespace
