﻿// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace KSPDev.GUIUtils.TypeFormatters {

/// <summary>Localized message formatting class for a Unity keyboard event.</summary>
/// <remarks>
/// Use it as a generic parameter when creating a <see cref="LocalizableMessage"/> descendants.
/// Even though the name assumes it's onle relevant to <i>keyboard</i>, it is not! A mouse or
/// joystick action can also be a part of a "keyboard event", e.g. <c>Ctrl + LMB</c>.
/// </remarks>
/// <include file="SpecialDocTags.xml" path="Tags/MessageTypeWithArg/*"/>
/// <include file="SpecialDocTags.xml" path="Tags/MessageArgumentType/*"/>
/// <example><code source="Examples/GUIUtils/TypeFormatters/KeyboardEventType-Examples.cs" region="KeyboardEventTypeDemo1"/></example>
/// <seealso cref="Event.KeyboardEvent"/>
public sealed class KeyboardEventType2 {

  /// <summary>Name overrides for special keys.</summary>
  static readonly Dictionary<KeyCode, string> specialKeys = new Dictionary<KeyCode, string>() {
      { KeyCode.LeftAlt, "L-Alt" },
      { KeyCode.RightAlt, "R-Alt" },
      { KeyCode.LeftControl, "L-Ctrl" },
      { KeyCode.RightControl, "R-Ctrl" },
      { KeyCode.LeftShift, "L-Shift" },
      { KeyCode.RightShift, "R-Shift" },
      { KeyCode.Mouse0, "LMB" },
      { KeyCode.Mouse1, "RMB" },
      { KeyCode.Mouse2, "CMB" },
  };

  /// <summary>A wrapped event value.</summary>
  public readonly Event value;

  /// <summary>Constructs an object from an event.</summary>
  /// <param name="value">The keyboard event.</param>
  /// <seealso cref="Format"/>
  /// <example><code source="Examples/GUIUtils/TypeFormatters/KeyboardEventType-Examples.cs" region="KeyboardEventTypeDemo1"/></example>
  public KeyboardEventType2(Event value) {
    this.value = value;
  }

  /// <summary>Converts a numeric value into a type object.</summary>
  /// <param name="value">The event value to convert.</param>
  /// <returns>An object.</returns>
  public static implicit operator KeyboardEventType2(Event value) {
    return new KeyboardEventType2(value);
  }

  /// <summary>Converts a type object into an event value.</summary>
  /// <param name="obj">The object type to convert.</param>
  /// <returns>A numeric value.</returns>
  public static implicit operator Event(KeyboardEventType2 obj) {
    return obj.value;
  }

  /// <summary>Formats the value into a human friendly string.</summary>
  /// <param name="value">The keyboard event value to format.</param>
  /// <returns>A formatted and localized string</returns>
  /// <example><code source="Examples/GUIUtils/TypeFormatters/KeyboardEventType-Examples.cs" region="KeyboardEventTypeDemo1"/></example>
  public static string Format(Event value) {
    var parts = new List<string>();
    if ((value.modifiers & EventModifiers.Control) != 0) {
      parts.Add("Ctrl");
    }
    if ((value.modifiers & EventModifiers.Shift) != 0) {
      parts.Add("Shift");
    }
    if ((value.modifiers & EventModifiers.Alt) != 0) {
      parts.Add("Alt");
    }
    string specialKeyName;
    if (specialKeys.TryGetValue(value.keyCode, out specialKeyName)) {
      parts.Add(specialKeyName);
    } else {
      parts.Add(value.keyCode.ToString());
    }
    return string.Join("+", parts.ToArray());
  }

  /// <summary>Returns a string formatted as a human friendly key specification.</summary>
  /// <returns>A string representing the value.</returns>
  /// <seealso cref="Format"/>
  public override string ToString() {
    return Format(value);
  }
}

}  // namespace
