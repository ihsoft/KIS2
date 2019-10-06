﻿// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;

namespace KSPDev.GUIUtils.TypeFormatters {

/// <summary>
/// Message formatting class for a numeric value. For the values below <c>1000</c> the resulted
/// message is formatted so that it takes 4 digits or less.
/// </summary>
/// <remarks>
/// <para>
/// Use it as a generic parameter when creating a <see cref="LocalizableMessage"/> descendants.
/// </para>
/// </remarks>
/// <include file="SpecialDocTags.xml" path="Tags/MessageTypeWithArg/*"/>
/// <include file="SpecialDocTags.xml" path="Tags/MessageArgumentType/*"/>
/// <seealso cref="CompactNumberType"/>
public sealed class SmallNumberType {
  /// <summary>A wrapped numeric value.</summary>
  /// <remarks>This is the original non-rounded and unscaled value.</remarks>
  public readonly double value;

  /// <summary>Constructs an object from a numeric value.</summary>
  /// <param name="value">The numeric value in the base units.</param>
  /// <seealso cref="Format"/>
  public SmallNumberType(double value) {
    this.value = value;
  }

  /// <summary>Converts a numeric value into a type object.</summary>
  /// <param name="value">The numeric value to convert.</param>
  /// <returns>An object.</returns>
  public static implicit operator SmallNumberType(double value) {
    return new SmallNumberType(value);
  }

  /// <summary>Converts a type object into a numeric value.</summary>
  /// <param name="obj">The object type to convert.</param>
  /// <returns>A numeric value.</returns>
  public static implicit operator double(SmallNumberType obj) {
    return obj.value;
  }

  /// <summary>Formats the value into a human friendly string.</summary>
  /// <remarks>
  /// When the value is below <c>1000</c>, the method tries to present the result in as smal digits
  /// as possible. If the value is greater, then the whole integer part is shown and the fractionap
  /// part is hidden.
  /// </remarks>
  /// <param name="value">The unscaled numeric value to format.</param>
  /// <param name="format">
  /// The specific float number format to use. If the format is not specified, then it's choosen
  /// basing on the value.
  /// </param>
  /// <returns>A formatted and localized string</returns>
  public static string Format(double value, string format = null) {
    if (format != null) {
      return value.ToString(format);
    }
    var testValue = Math.Abs(value);
    if (testValue < double.Epsilon) {
      return "0";  // Zero is zero.
    }
    if (testValue < 1.0) {
      return value.ToString("0.0##");
    }
    if (testValue < 10.0) {
      return value.ToString("0.###");
    }
    if (testValue < 100.0) {
      return value.ToString("0.##");
    }
    if (testValue < 1000.0) {
      return value.ToString("0.#");
    }
    return value.ToString("#,##0");
  }

  /// <summary>Returns a string formatted as a human friendly distance specification.</summary>
  /// <returns>A string representing the value.</returns>
  /// <seealso cref="Format"/>
  public override string ToString() {
    return Format(value);
  }
}

}  // namespace
