// Kerbal Inventory System
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using KSP.Localization;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.LogUtils;

// ReSharper disable once CheckNamespace
namespace KIS2.GUIUtils {

/// <summary>
/// Localized message formatting class for a numeric value that represents a <i>volume</i> in
/// <c>liters</c>. The resulted message may have a unit specification.
/// </summary>
/// <remarks>
/// <p>Use it as a generic parameter when creating a <c>KSPDev.GUIUtils.LocalizableMessage</c> descendants.</p>
/// </remarks>
public sealed class VolumeLType {
  /// <summary>Localized suffix for the "liter" units. Scale x1.</summary>
  static readonly Message<CompactNumberType> DefaultLiterUnitTemplate = new(
      "#kisLOC_99000", defaultTemplate: "<<1>> L",
      description: "Liter unit for a volume value to use when the stock localization cannot be extracted.");

  /// <summary>Localization pattern for the litres unit.</summary>
  /// <value>The litres unit localization message.</value>
  public static Message<CompactNumberType> literUnitTemplate {
    get {
      if (_literUnitTemplate == null || LocalizableMessage.systemLocVersion > _loadedLocVersion) {
        UpdateTemplate();
      }
      return _literUnitTemplate;
    }
  }
  static Message<CompactNumberType> _literUnitTemplate;
  static int _loadedLocVersion;

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
  /// <p>
  /// The method tries to keep the resulted string meaningful and as short as possible. For this
  /// reason the big values may be scaled down and/or rounded.
  /// </p>
  /// <p>
  /// The base volume unit in the game is <i>liter</i>. I.e. value <c>1.0</c> in the game
  /// units is <i>one liter</i>. Keep it in mind when passing the argument.
  /// </p>
  /// </remarks>
  /// <param name="value">The numeric value to format.</param>
  /// <returns>A formatted and localized string</returns>
  public static string Format(double value) {
    return literUnitTemplate.Format(value);
  }

  /// <summary>Returns a string formatted as a human friendly volume specification.</summary>
  /// <returns>A string representing the value.</returns>
  /// <seealso cref="Format"/>
  public override string ToString() {
    return Format(value);
  }

  /// <summary>Get the best litres unit name</summary>
  /// <remarks>
  /// First, it tries to extract the unit name from the stock strings. If failed, then a fallback pattern is used.
  /// </remarks>
  /// <seealso cref="DefaultLiterUnitTemplate"/>
  /// <seealso cref="literUnitTemplate"/>
  static void UpdateTemplate() {
    if (_literUnitTemplate != null && LocalizableMessage.systemLocVersion == _loadedLocVersion) {
      return;
    }
    _loadedLocVersion = LocalizableMessage.systemLocVersion;
    // KSP version dependent code below! The fallback addresses the incompatible changes.
    var sample = Localizer.Format("#autoLOC_8003412", 1111, 2222);
    var totalSizeValuePos = sample.IndexOf("2222", StringComparison.Ordinal);
    if (totalSizeValuePos == -1) {
      _literUnitTemplate = DefaultLiterUnitTemplate; // A very unexpected case, but it's still possible.
      DebugEx.Error("Cannot extract stock string for the litres unit, using default: tag={0}, template={1}",
                    _literUnitTemplate.tag, _literUnitTemplate.defaultTemplate);
      return;
    }
    _literUnitTemplate = new Message<CompactNumberType>(
        DefaultLiterUnitTemplate.tag, defaultTemplate: "<<1>> " + sample.Substring(totalSizeValuePos + 4).Trim());
    DebugEx.Fine("Created a litres unit from stock: tag={0}, template={1}",
                 _literUnitTemplate.tag, _literUnitTemplate.defaultTemplate);
  }
}

}  // namespace
