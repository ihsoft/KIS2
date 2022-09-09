// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections;
using KSPDev.ConfigUtils;
using KSPDev.InputUtils;
using UnityEngine;

namespace KIS2.controllers.flight_dragging {

/// <summary>Handles the keyboard/mouse events when the controller is idle.</summary>
/// <remarks>Keep this handler as simple as possible to not consume CPU when KIS logic is not needed.</remarks>
[PersistentFieldsDatabase("KIS2/settings2/KISConfig")]
sealed class IdleStateHandler : AbstractStateHandler {
  #region Configuration
  // ReSharper disable FieldCanBeMadeReadOnly.Local
  // ReSharper disable ConvertToConstant.Local

  /// <summary>The key that activates the in-flight pickup mode.</summary>
  /// <remarks>
  /// It's a standard keyboard event definition. Even though it can have modifiers, avoid specifying them since it may
  /// affect the UX experience.
  /// </remarks>
  [PersistentField("PickupMode/actionKey")]
  static string _flightActionKey = "j";

  // ReSharper enable FieldCanBeMadeReadOnly.Local
  // ReSharper enable ConvertToConstant.Local
  #endregion

  #region Event static configs
  static ClickEvent _pickupModeSwitchEvent = new(Event.KeyboardEvent(_flightActionKey));
  #endregion

  #region AbstractStateHandler implementation
  /// <inheritdoc/>
  public override void Init(FlightItemDragController aHostObj) {
    base.Init(aHostObj);
    ConfigAccessor.ReadFieldsInType(GetType(), null); // All config fields are static.
    _pickupModeSwitchEvent = new ClickEvent(Event.KeyboardEvent(_flightActionKey));
  }

  /// <inheritdoc/>
  protected override IEnumerator StateTrackingCoroutine() {
    while (isStarted) {
      // Don't handle the keys in the same frame as the coroutine has started in to avoid double actions.
      yield return null;

      if (Input.anyKey && _pickupModeSwitchEvent.isEventActive) {
        hostObj.ToPickupState();
        break;
      }
    }
    // No logic beyond this point! The coroutine can be explicitly killed.
  }
  #endregion
}
}
