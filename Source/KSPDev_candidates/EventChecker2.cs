// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KSPDev.InputUtils {

/// <summary>A helper to verify various event handling conditions.</summary>
public static class EventChecker2 {
  /// <summary>Verifies that the requested key modifiers are pressed.</summary>
  /// <remarks>The check will succeed only if the exact set of modifier keys is pressed. If there
  /// are more or less modifiers pressed the check will fail. E.g. if there are <c>LeftAlt</c> and
  /// <c>LeftShift</c> pressed but the check is executed against
  /// <c>AnyShift</c> then it will fail. Though, checking for <c>AnyShift | AnyAlt</c> will succeed.
  /// <para>In case of checking for <c>None</c> the check will require no modifier keys to be
  /// pressed. If you deal with mouse button events it's a good idea to verify if no modifiers are
  /// pressed even if you don't care about other combinations. It will let other modders to use
  /// mouse buttons and not to interfere with your mod.</para>
  /// </remarks>
  /// <param name="modifiers">A combination of key modifiers to verify.</param>
  /// <returns><c>true</c> when exactly the requested combination is pressed.</returns>
  /// <seealso cref="KeyModifiers"/>
  [ObsoleteAttribute("This method will soon be depreacted. Use CheckAnySymmetricalModifiers().")]
  public static bool IsModifierCombinationPressed(KeyModifiers modifiers) {
    if (Time.timeScale <= float.Epsilon) {
      return false;  // Prevent behavior in the game menu.
    }
    bool shiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    bool altPressed = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
    bool ctrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
    return !((shiftPressed ^ (modifiers & KeyModifiers.AnyShift) == KeyModifiers.AnyShift)
        | (altPressed ^ (modifiers & KeyModifiers.AnyAlt) == KeyModifiers.AnyAlt)
        | (ctrlPressed ^ (modifiers & KeyModifiers.AnyControl) == KeyModifiers.AnyControl));
  }

  /// <summary>
  /// Verifies if the left-side "symmetrical" keyboard modifiers, defined in the event, are
  /// currently in effect (pressed and hold). The <i>exact</i> match is required.
  /// </summary>
  /// <remarks>
  /// The "symmetrical modifiers" are modifier keys that are represented on the both sides of a
  /// standard 101-key keyboard and, thus, have "left" and "right" keys. 
  /// These are: <c>ALT</c>, <c>CTRL</c>, <c>SHIFT</c>, and <c>COMMAND</c>.
  /// </remarks>
  /// <param name="ev">The event to get modifiers from.</param>
  /// <returns><c>true</c> if the the current hold modifier(s) match the event.</returns>
  public static bool CheckLeftSymmetricalModifiers(Event ev) {
    if (Time.timeScale <= float.Epsilon) {
      return false;  // Prevent behavior in the game menu.
    }
    var modifiers = EventModifiers.None;
    if (Input.GetKey(KeyCode.LeftAlt)) {
      modifiers |= EventModifiers.Alt;
    }
    if (Input.GetKey(KeyCode.LeftShift)) {
      modifiers |= EventModifiers.Shift;
    }
    if (Input.GetKey(KeyCode.LeftControl)) {
      modifiers |= EventModifiers.Control;
    }
    if (Input.GetKey(KeyCode.LeftCommand)) {
      modifiers |= EventModifiers.Command;
    }
    return ev.modifiers == modifiers;
  }
  
  /// <summary>
  /// Verifies if the right-side "symmetrical" keyboard modifiers, defined in the event, are
  /// currently in effect (pressed and hold). The <i>exact</i> match is required.
  /// </summary>
  /// <remarks>
  /// The "symmetrical modifiers" are modifier keys that are represented on the both sides of a
  /// standard 101-key keyboard and, thus, have "left" and "right" keys. 
  /// These are: <c>ALT</c>, <c>CTRL</c>, <c>SHIFT</c>, and <c>COMMAND</c>.
  /// </remarks>
  /// <param name="ev">The event to get modifiers from.</param>
  /// <returns><c>true</c> if the the current hold modifier(s) match the event.</returns>
  public static bool CheckRightSymmetricalModifiers(Event ev) {
    if (Time.timeScale <= float.Epsilon) {
      return false;  // Prevent behavior in the game menu.
    }
    var modifiers = EventModifiers.None;
    if (Input.GetKey(KeyCode.RightAlt)) {
      modifiers |= EventModifiers.Alt;
    }
    if (Input.GetKey(KeyCode.RightShift)) {
      modifiers |= EventModifiers.Shift;
    }
    if (Input.GetKey(KeyCode.RightControl)) {
      modifiers |= EventModifiers.Control;
    }
    if (Input.GetKey(KeyCode.RightCommand)) {
      modifiers |= EventModifiers.Command;
    }
    return ev.modifiers == modifiers;
  }

  /// <summary>
  /// Verifies if any of the "symmetrical" keyboard modifiers, defined in the event, are
  /// currently in effect (pressed and hold). The <i>exact</i> match is required.
  /// </summary>
  /// <remarks>
  /// The "symmetrical modifiers" are modifier keys that are represented on the both sides of a
  /// standard 101-key keyboard and, thus, have "left" and "right" keys. 
  /// These are: <c>ALT</c>, <c>CTRL</c>, <c>SHIFT</c>, and <c>COMMAND</c>.
  /// </remarks>
  /// <param name="ev">The event to get modifiers from.</param>
  /// <returns><c>true</c> if the the current hold modifier(s) match the event.</returns>
  public static bool CheckAnySymmetricalModifiers(Event ev) {
    if (Time.timeScale <= float.Epsilon) {
      return false;  // Prevent behavior in the game menu.
    }
    var modifiers = EventModifiers.None;
    if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) {
      modifiers |= EventModifiers.Alt;
    }
    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
      modifiers |= EventModifiers.Shift;
    }
    if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) {
      modifiers |= EventModifiers.Control;
    }
    if (Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand)) {
      modifiers |= EventModifiers.Command;
    }
    return ev.modifiers == modifiers;
  }

  /// <summary>Extracts mouse button from a keyboard event definition.</summary>
  /// <remarks>
  /// Only three basic buttons are supported: <c>Left</c>, <c>Right</c>, and <c>Middle</c>. 
  /// </remarks>
  /// <param name="ev">The event to extract the button from.</param>
  /// <returns>
  /// The button or <c>null</c> if event doesn't have any recognizable mouse button.
  /// </returns>
  public static PointerEventData.InputButton? GetInputButtonFromEvent(Event ev) {
    if (Time.timeScale <= float.Epsilon) {
      return null;  // Prevent behavior in the game menu.
    }
    switch (ev.keyCode) {
      case KeyCode.Mouse0:
        return PointerEventData.InputButton.Left;
      case KeyCode.Mouse1:
        return PointerEventData.InputButton.Right;
      case KeyCode.Mouse2:
        return PointerEventData.InputButton.Middle;
    }
    return null;
  }

  /// <summary>Checks if the keyboard/mouse down event has happen during the frame.</summary>
  /// <remarks>
  /// This check treats "left" and "right" modifiers equally. And it doesn't consider any of the
  /// state modifiers (CAPS, NUM, SCROLL, etc.).
  /// </remarks>
  /// <param name="ev">The event to match for.</param>
  /// <returns>
  /// <c>true</c> if the requested combination has matched the current frame state.
  /// </returns>
  /// <seealso cref="CheckAnySymmetricalModifiers"/>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Input.GetKeyDown.html">
  /// Input.GetKeyDown
  /// </seealso>
  public static bool CheckDownEvent(Event ev) {
    return Time.timeScale > float.Epsilon && CheckAnySymmetricalModifiers(ev) && Input.GetKeyDown(ev.keyCode);
  }

  /// <summary>Checks if the keyboard/mouse event is happening during this frame.</summary>
  /// <remarks>If the event has ended in the current frame, it's assumed it's NOT active during this frame.</remarks>
  /// <param name="ev">The event to match for.</param>
  /// <returns>
  /// <c>true</c> if the requested combination has matched the current frame state.
  /// </returns>
  /// <seealso cref="CheckAnySymmetricalModifiers"/>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Input.GetKeyDown.html">
  /// Input.GetKeyDown
  /// </seealso>
  public static bool CheckEventActive(Event ev) {
    return Time.timeScale > float.Epsilon && (Input.GetKeyUp(ev.keyCode) || Input.GetKey(ev.keyCode));
  }

  /// <summary>Checks if the mouse click event has happen during the frame.</summary>
  /// <remarks>
  /// This check treats "left" and "right" modifiers equally. And it doesn't consider any of the
  /// state modifiers (CAPS, NUM, SCROLL, etc.).
  /// </remarks>
  /// <param name="ev">The event to match for.</param>
  /// <param name="button">
  /// The mouse button from the Unity <c>EventSystem</c> which is known to be pressed in this frame.
  /// The <c>EventSystem</c> logic is not in sync with <c>MonoBehaviour.Update</c>, so the event
  /// system handlers should provide the pressed <c>button</c> explicitly. The callers from the
  /// <c>Update</c> method don't need to do so since the right action button can be extracted from
  /// <c>Input</c>.
  /// </param>
  /// <returns>
  /// <c>true</c> if the requested combination has matched the current frame state.
  /// </returns>
  /// <seealso cref="CheckAnySymmetricalModifiers"/>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Input.GetKeyDown.html">
  /// Input.GetKeyDown
  /// </seealso>
  public static bool CheckClickEvent(Event ev, PointerEventData.InputButton button) {
    return Time.timeScale > float.Epsilon && CheckAnySymmetricalModifiers(ev) && button == GetInputButtonFromEvent(ev);
  }
}

} // namespace
