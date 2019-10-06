// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using UnityEngine;

namespace KSPDev.GUIUtils {

public static class RichTextFormatter {
  /// <summary>Returns rich text, formatted to the selected criteria.</summary>
  /// <param name="text">The text to format.</param>
  /// <param name="color">An optional color object.</param>
  /// <param name="webColor">
  /// An optional web color string. If it's a hexadecimal color, it must start from <c>#</c>.
  /// Otherwise, it can be one of the predefined colot constants. This valut will be overwritten
  /// if <paramref name="color"/> is specified.
  /// </param>
  /// <param name="isBold">If set, then the text will be wrapped with <c>&lt;b/&gt;</c> tags.</param>
  /// <param name="isItalic">If set, then the text will be wrapped with <c>&lt;i/&gt;</c> tags.</param>
  /// <returns></returns>
  public static string ApplyStyle(
      string text,
      Color? color = null, string webColor = null, bool isBold = false, bool isItalic = false) {
    if (isBold) {
      text = "<b>" + text + "</b>";
    }
    if (isItalic) {
      text = "<i>" + text + "</i>";
    }
    if (color.HasValue) {
        webColor = ColorToWeb(color.Value);
    }
    if (webColor != null) {
      text = "<color=" + webColor + ">" + text + "</color>";
    }
    return text;
  }
  /// <summary>Returns <c>Color</c> as a web color string to use in the rich text strings.</summary>
  /// <param name="color">The color to convert.</param>
  /// <returns>The web color string.</returns>
  public static string ColorToWeb(Color color) {
    return string.Format(
        "#{0:X02}{1:X02}{2:X02}{3:X02}",
        (int) (color.a * 256.0),
        (int) (color.r * 256.0),
        (int) (color.g * 256.0),
        (int) (color.b * 256.0));
  }
}

}  // namespace
