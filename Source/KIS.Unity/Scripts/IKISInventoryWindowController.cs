// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using UnityEngine;
using UnityEngine.EventSystems;
using KIS2.UIKISInventorySlot;

namespace KIS2 {

/// <summary>KIS inventory controller interface.</summary>
/// <remarks>This a gateway thru which Unity talks to KIS mod.</remarks>
interface IKISInventoryWindowController {
  /// <summary>Handles mouse button clicks on a slot.</summary>
  /// <param name="host">The Unity class that sent the event.</param>
  /// <param name="slot">The Unity slot class for which this evnt was generated.</param>
  /// <param name="button">The pointer button that was clicked.</param>
  void OnSlotClick(UIKISInventoryWindow host, Slot slot, PointerEventData.InputButton button);

  /// <summary>Handles slot's pointer enter/leave events.</summary>
  /// <param name="host">The Unity class that sent the event.</param>
  /// <param name="slot">The Unity slot class for which this evnt was generated.</param>
  /// <param name="isHover">
  /// <c>true</c> if mouse pointer has entered the control's <c>Rect</c>.
  /// </param>
  void OnSlotHover(UIKISInventoryWindow host, Slot slot, bool isHover);

  /// <summary>Handles actions on a slot.</summary>
  /// <param name="host">The Unity class that sent the event.</param>
  /// <param name="slot">The Unity slot class for which this evnt was generated.</param>
  /// <param name="actionButtonNum">The number of the button on the slot that was clicked.</param>
  /// <param name="button">The pointer button that was clicked.</param>
  void OnSlotAction(UIKISInventoryWindow host, Slot slot,
                    int actionButtonNum, PointerEventData.InputButton button);

  /// <summary>Called when inventory grid size change is requested.</summary>
  /// <remarks>
  /// When the size is extended, the grid will simply be padded with more empty slots. If the grid
  /// size is reduced, then the last slots in the list will be destoryed even if they had some
  /// content. It's controller's responsibility to verify it and adjust the final size.
  /// </remarks>
  /// <param name="host">The Unity class that sent the event.</param>
  /// <param name="oldSize">The size before the change.</param>
  /// <param name="newSize">The new size being applied.</param>
  /// <returns>The size that should actually be applied.</returns>
  Vector2 OnSizeChanged(UIKISInventoryWindow host, Vector2 oldSize, Vector2 newSize);
}

}  // namespace
