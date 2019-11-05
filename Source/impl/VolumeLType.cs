﻿// Kerbal Inventory System
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;

namespace KIS2.GUIUtils {

/// <summary>
/// Localized message formatting class for a numeric value that represents a <i>volume</i> in
/// <c>liters</c>. The resulted message may have a unit specification.
/// </summary>
/// <remarks>
/// <para>
/// Use it as a generic parameter when creating a <c>KSPDev.GUIUtils.LocalizableMessage</c>
/// descendants.
/// </para>
/// </remarks>
public sealed class VolumeLType {
  /// <summary>Localized suffix for the "liter" untis. Scale x1.</summary>
  public static readonly Message Liter = new Message(
      "#kisLOC_99000", defaultTemplate: " L", description: "Liter unit for a volume value.");

  /// <summary>A wrapped numeric value.</summary>
  /// <remarks>This is the original non-rounded and unscaled value.</remarks>
  public readonly double value;

  /// <summary>Constructs an object from a numeric value.</summary>
  /// <param name="value">The numeric value in liters.</param>
  /// <seealso cref="Format"/>
  public VolumeLType(double value) {
    this.value = value;
  }

  /// <summary>Coverts a numeric value into a type object.</summary>
  /// <param name="value">The numeric value to convert.</param>
  /// <returns>An object.</returns>
  public static implicit operator VolumeLType(double value) {
    return new VolumeLType(value);
  }

  /// <summary>Converts a type object into a numeric value.</summary>
  /// <param name="obj">The object type to convert.</param>
  /// <returns>A numeric value.</returns>
  public static implicit operator double(VolumeLType obj) {
    return obj.value;
  }

  /// <summary>Formats the value into a human friendly string with a unit specification.</summary>
  /// <remarks>
  /// <para>
  /// The method tries to keep the resulted string meaningful and as short as possible. For this
  /// reason the big values may be scaled down and/or rounded.
  /// </para>
  /// <para>
  /// The base volume unit in the game is <i>liter</i>. I.e. value <c>1.0</c> in the game
  /// units is <i>one liter</i>. Keep it in mind when passing the argument.
  /// </para>
  /// </remarks>
  /// <param name="value">The numeric value to format.</param>
  /// <param name="format">
  /// The specific float number format to use. If the format is not specified, then it's choosen
  /// basing on the value.
  /// </param>
  /// <returns>A formatted and localized string</returns>
  public static string Format(double value, string format = null) {
    if (format != null) {
      return value.ToString(format) + Liter;
    }
    return CompactNumberType.Format(value) + Liter;
  }

  /// <summary>Returns a string formatted as a human friendly volume specification.</summary>
  /// <returns>A string representing the value.</returns>
  /// <seealso cref="Format"/>
  public override string ToString() {
    return Format(value);
  }
}

}  // namespace
