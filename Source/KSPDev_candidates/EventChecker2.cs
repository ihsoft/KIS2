// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using UnityEngine;
using UnityEngine.EventSystems;

// ReSharper disable once CheckNamespace
namespace KSPDev.InputUtils {

/// <summary>A helper to verify various event handling conditions.</summary>
public static class EventChecker2 {
  /// <summary>Checks if the mouse click event has happen during the frame.</summary>
  /// <remarks>
  /// This check treats "left" and "right" modifiers equally. And it doesn't consider any of the
  /// state modifiers (CAPS, NUM, SCROLL, etc.).
  /// </remarks>
  /// <param name="ev">The event to match for.</param>
  /// <param name="onlyCheckModifiers">
  /// Tells if only the modifiers in teh event need to be checked. This is how a "precondition" can
  /// be verified when the GUI is interactive and depends on the pressed keys.
  /// </param>
  /// <param name="inputButton">
  /// The inputButton from the Unity <c>EventSystem</c> which is known to be pressed in this frame. The
  /// <c>MonoBehaviour.Update</c> logic is out of sync with the event system, so the event system
  /// handlers should provide the pressed inputButton explicitly. Callers from the <c>Update</c> method
  /// don't need to do so. 
  /// </param>
  /// <returns>
  /// <c>true</c> if the requested combination has matched the current frame state.
  /// </returns>
  /// <seealso cref="CheckAnySymmetricalModifiers"/>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Input.GetKeyDown.html">
  /// Input.GetKeyDown
  /// </seealso>
  public static bool CheckClickEvent(
      Event ev, bool onlyCheckModifiers = false, PointerEventData.InputButton? inputButton = null) {
    var modifiersCheck = EventChecker.CheckAnySymmetricalModifiers(ev);
    if (!modifiersCheck || onlyCheckModifiers) {
      return modifiersCheck;
    }
    if (inputButton.HasValue) {
      return inputButton == EventChecker.GetInputButtonFromEvent(ev);
    }
    return Input.GetKeyDown(ev.keyCode);
  }
}

} // namespace
