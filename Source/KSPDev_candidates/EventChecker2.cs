// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using UnityEngine;

namespace KSPDev.InputUtils {

/// <summary>A helper to verify various event handling conditions.</summary>
public static class EventChecker2 {
  /// <summary>
  /// Verifies if the left-side "symmetrical" keyboard modifiers, defined in the event, are
  /// currently in effect (pressed and hold). The <i>exact</i> match is required.
  /// </summary>
  /// <remarks>
  /// The "symmetrical modifiers" are modifier keys that are represented on the both sides of a
  /// standard 101-key keyboard and, thus, have "left" and "right" keys. 
  /// These are: <c>ALT</c>, <c>CTRL</c>, <c>SHIFT</c>, and <c>COMMAND</c>.
  /// </remarks>
  /// <param name="ev"></param>
  /// <returns></returns>
  public static bool CheckLeftSymmetricalModifiers(Event ev) {
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
  /// <param name="ev"></param>
  /// <returns></returns>
  public static bool CheckRightSymmetricalModifiers(Event ev) {
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
  /// <param name="ev"></param>
  /// <returns></returns>
  public static bool CheckAnySymmetricalModifiers(Event ev) {
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
}

} // namespace

