// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections;
using UnityEngine;

namespace KIS2.controllers.flight_dragging {

/// <summary>Handles the keyboard/mouse events when the controller is idle.</summary>
/// <remarks>Keep this handler as simple as possible to not consume CPU when KIS logic is not needed.</remarks>
sealed class WaitingForPickupStateHandler : AbstractStateHandler {
  #region AbstractStateHandler implementation
  /// <inheritdoc/>
  public WaitingForPickupStateHandler(FlightItemDragController hostObj) : base(hostObj) {
  }

  /// <inheritdoc/>
  protected override IEnumerator StateTrackingCoroutine() {
    while (isStarted) {
      // Don't handle the keys in the same frame as the coroutine has started in to avoid double actions.
      yield return null;

      if (Input.anyKey && hostObj.pickupModeSwitchEvent.isEventActive) {
        hostObj.ToPickupState();
        break;
      }
    }
    // No logic beyond this point! The coroutine can be explicitly killed.
  }
  #endregion
}
}
