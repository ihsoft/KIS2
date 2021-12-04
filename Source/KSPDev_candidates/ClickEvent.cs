// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using UnityEngine;

namespace KSPDev.InputUtils {

/// <summary>Class to handle "true clicks" on the events.</summary>
/// <remarks>
/// <p>
/// This class covers a gap in the Unity event system that can only tell if the key was pressed, released or being
/// active in the frame. The "true click event" is when the key (or a combination) was pressed and released withing some
/// reasonable (not too short and not too long) period of time. And a derivative of this event is "double click": the
/// case when a click event happen rapidly two times in a row.
/// </p>
/// <p>
/// The click event is assumed to happen only when the the delay between pressing and releasing the key was within a
/// certain threshold (<see cref="maxClickDelay"/>). The double click event assumes that two click events happen with
/// the same delay between each other.
/// </p>
/// <p>
/// The down event is checked for the full event definition: modifiers+keyCode. The release event is only checked
/// against the keyCode. I.e. releasing the modifiers (if any) won't trigger the click event as long as the key is
/// hold.
/// <p>
/// </p>
/// It's important to call the <see cref="CheckClick"/> and/or <see cref="CheckDoubleClick"/> methods in every frame to
/// let them capturing the starting and the ending events. However, it's OK to prematurely stop updating. The incomplete
/// click sequence will be correctly handled once the update calls resumed.
/// </p>
/// <p>
/// The click and double click events are "consumable". I.e. the caller that got "true" response is assumed to consume
/// it. All the other callers that may request the check in the same frame will get the "false" response.
/// </p>
/// </remarks>
public class ClickEvent {
  #region API fields and properties
  /// <summary>The maximum delay between press and release events to consider it was a "click event".</summary>
  /// <remarks>
  /// This is a global setting that will be applied to any new instance. Use it to override the system wide settings.
  /// </remarks>
  /// <seealso cref="maxClickDelay"/>
  public static float maxClickDelayGlobal = 0.3f; // It relates to 4 FPS. We assume, nobody seriously play at this FPS.

  /// <summary>The wrapped Unity event.</summary>
  public readonly Event unityEvent;

  /// <summary>The maximum delay between press and release events to consider it was a "click event".</summary>
  /// <remarks>Use it to override the behavior of a single instance.</remarks>
  /// <seealso cref="maxClickDelayGlobal"/>
  public float maxClickDelay = maxClickDelayGlobal;
  
  /// <summary>The maximum delay between two click events to trigger a " double click event".</summary>
  /// <remarks>Use it to override the behavior of a single instance.</remarks>
  /// <seealso cref="maxClickDelayGlobal"/>
  public float maxDoubleClickDelay = maxClickDelayGlobal;

  /// <summary>Tells if the event is currently active (the keys are being hold).</summary>
  /// <remarks>The positive answer from this property doesn't indicate that the click event may happen.</remarks>
  /// <value><c>true</c> if the "main keys" of the event definition are being hold.</value>
  /// <seealso cref="canMakeClick"/>
  public bool isEventActive => EventChecker2.CheckEventActive(unityEvent);

  /// <summary>Tells if the click event is still possible.</summary>
  /// <remarks>The result of this property only makes sense when <see cref="isEventActive"/> is <c>true</c>.</remarks>
  /// <value><c>true</c> if the delay between press and release actions has not yet expired.</value>
  /// <seealso cref="maxClickDelay"/>
  public bool canMakeClick => isEventActive && Time.fixedTime - _eventDownTs <= maxClickDelay;
  #endregion

  #region Local utlity fields and properties
  /// <summary>The moment when the event was detected as "pressed".</summary>

  float _eventDownTs;
  /// <summary>The timestamp of the click event that happen before the latest one.</summary>
  /// <seealso cref="_lastClickEventTs"/>
  /// <seealso cref="CheckDoubleClick"/>
  float _prevClickEventTs;

  /// <summary>The very last click event timestamp.</summary>
  /// <seealso cref="CheckDoubleClick"/>
  /// <seealso cref="_prevClickEventTs"/>
  float _lastClickEventTs;
  #endregion

  #region API methods
  /// <summary>Creates the click event instance from, a Unity event.</summary>
  /// <param name="unityEvent">The Unity event to create from.</param>
  public ClickEvent(Event unityEvent) {
    this.unityEvent = unityEvent;
  }

  /// <summary>Checks if the click event happen.</summary>
  /// <remarks>
  /// <p>This method must be called in every frame as long as the client is interested in the output.</p>
  /// </remarks>
  /// <param name="consume">
  /// Indicates if in a case of positive response the state must be consumed. If the state is "consumed", then all the
  /// other callers that would try to check the state in the same frame will get a negative response even though the
  /// event did happen in that frame.
  /// </param>
  /// <returns><c>true</c> if the click event has been detected in this frame.</returns>
  /// <seealso cref="maxClickDelay"/>
  /// <seealso cref="unityEvent"/>
  public bool CheckClick(bool consume = true) {
    if (_eventDownTs > float.Epsilon && !EventChecker2.CheckEventActive(unityEvent)) {
      var res = Time.fixedTime - _eventDownTs <= maxClickDelay;
      if (consume) {
        if (res) {
          _prevClickEventTs = _lastClickEventTs;
          _lastClickEventTs = Time.fixedTime;
        }
        _eventDownTs = 0;
      }
      return res;
    }
    if (EventChecker2.CheckDownEvent(unityEvent)) {
      _eventDownTs = Time.fixedTime;
    }
    return false;
  }

  /// <summary>Checks if the double click event happen.</summary>
  /// <remarks>
  /// <p>This method must be called in every frame as long as the client is interested in the output.</p>
  /// <p>
  /// This method consumes the click event if any! If the caller(s) want to distinguish clicks vs DBL clicks, then they
  /// have to order the calling chain so that the <see cref="CheckClick"/> is called by all of the interested parties
  /// BEFORE calling checks for the DBL clicks.
  /// </p>
  /// </remarks>
  /// <param name="consume">
  /// Indicates if in a case of positive response the state must be consumed. If the state is "consumed", then all the
  /// other callers that would try to check the state in the same frame will get a negative response even though the
  /// event did happen in that frame.
  /// </param>
  /// <returns><c>true</c> if the DBL click event has been detected in this frame.</returns>
  /// <seealso cref="maxClickDelay"/>
  /// <seealso cref="unityEvent"/>
  public bool CheckDoubleClick(bool consume = true) {
    CheckClick();
    if (_prevClickEventTs > float.Epsilon && _lastClickEventTs > float.Epsilon) {
      var res = _lastClickEventTs - _prevClickEventTs < maxDoubleClickDelay;
      if (res && consume) {
        _prevClickEventTs = 0;
        _lastClickEventTs = 0;
      }
      return res;
    }
    return false;
  }
  #endregion
}

} // namespace
