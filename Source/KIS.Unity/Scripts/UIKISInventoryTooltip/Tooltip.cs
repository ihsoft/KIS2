﻿// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.Unity;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace KIS2.UIKISInventoryTooltip {

/// <summary>
/// Tooltip window class to show key info about a KSP part. It can automatiaclly follow mouse cursor
/// or be statically attached to a control.
/// </summary>
/// <remarks>
/// <para>
/// There is a special basic mode of the tooltip when only title is defined. In this mode tooltip
/// tries to fit the minimum title string size. In the regular mode there is a minimum window size
/// that tooltip maintains.
/// </para>
/// <para>
/// Prefab for this class must be designed so that it doesn't have any raycast target elements.
/// </para>
/// </remarks>
/// <seealso cref="InfoPanel"/>
public sealed class Tooltip : UIPrefabBaseScript {

  #region Unity serialized fields
  [SerializeField]
  Text titleText = null;

  [SerializeField]
  InfoPanel baseInfoPanel = null;

  [SerializeField]
  InfoPanel resourcesInfoPanel = null;

  [SerializeField]
  InfoPanel requiresInfoPanel = null;

  [SerializeField]
  InfoPanel scienceInfoPanel = null;

  [SerializeField]
  Text hintsText = null;
  #endregion

  #region API fields an properties
  /// <summary>Tells if hint should be shown.</summary>
  /// <remarks>It's a global settings that affects all the tooltips in the game.</remarks>
  public static bool showHints = true;

  /// <summary>Pattern that applies style to a hotkey name.</summary>
  /// <remarks>Intended to be used in the hints section.</remarks>
  public const string HintHotkeyPattern = "<b><color=#5a5>[{0}]</color></b>";

  /// <summary>
  /// Pattern that applies style to a value that changes depending on the user's choice.
  /// </summary>
  /// <remarks>Intended to be used in the hints section.</remarks>
  public const string HintActiveValuePattern = "<color=#5a5>[{0}]</color>";

  /// <summary>
  /// Pattern that applies a special style to the values that need extra player's attention.
  /// </summary>
  /// <remarks>Can be used in any info panel.</remarks>
  public const string InfoHighlightPattern = "<color=yellow>{0}</color>";

  /// <summary>Padding when showing hint on the right side of the mouse cursor.</summary>
  public int RightSideMousePadding = 24;

  /// <summary>Padding when showing hint on the left side of the mouse cursor.</summary>
  public int LeftSideMousePadding = 4;

  /// <summary>Tells if tooltip should follow the pointer.</summary>
  /// <remarks>The tooltip will be ajdusted so that it never goes off the screen.</remarks>
  public bool followsPointer {
    get { return _followsPointer; }
    set {
      var needCoroutine = value && _followsPointer != value;
      _followsPointer = value;
      if (needCoroutine) {
        StartCoroutine(TooltipPositionCoroutine());
      }
    }
  }
  bool _followsPointer;

  /// <summary>Main highlighted text.</summary>
  /// <remarks>Can be set to empty string to hide the control.</remarks>
  public string title {
    get { return titleText.text; }
    set {
      titleText.text = value;
      titleText.gameObject.SetActive(!string.IsNullOrEmpty(value));
    }
  }

  /// <summary>GUI hints, applicable to the tooltip's object. Rich text is supported.</summary>
  /// <remarks>Can be set to empty string to hide the control.</remarks>
  /// <seealso cref="showHints"/>
  public string hints {
    get { return hintsText.text; }
    set {
      hintsText.text = value;
      hintsText.gameObject.SetActive(showHints && !string.IsNullOrEmpty(value));
    }
  }

  /// <summary>Basic part's information.</summary>
  public InfoPanel baseInfo {
    get { return baseInfoPanel; }
  }

  /// <summary>Available resources on the part.</summary>
  public InfoPanel availableResourcesInfo {
    get { return resourcesInfoPanel; }
  }

  /// <summary>Resources that are required by the part.</summary>
  public InfoPanel requiredResourcesInfo {
    get { return requiresInfoPanel; }
  }

  /// <summary>Available science in the part.</summary>
  public InfoPanel availableScienceInfo {
    get { return scienceInfoPanel; }
  }

  /// <summary>Width of the client area of the tooltip.</summary>
  /// <remarks>
  /// This is a suggested size. The actual size can be greater if there are inner controls that need
  /// more space.
  /// </remarks>
  /// <value>The size preference in pixels or <c>-1</c> if size is unbound.</value>
  public float preferredContentWidth {
    get {
      var layoutElement = GetComponent<LayoutElement>();
      var layoutPadding = GetComponent<LayoutGroup>().padding;
      return layoutElement.preferredWidth - (layoutPadding.left + layoutPadding.right);
    }
    set {
      var layoutElement = GetComponent<LayoutElement>();
      if (value >= 0) {
        var layoutPadding = GetComponent<LayoutGroup>().padding;
        layoutElement.preferredWidth = value + (layoutPadding.left + layoutPadding.right);
      } else {
        layoutElement.preferredWidth = value;
      }
    }
  }
  #endregion

  #region UIPrefabBaseScript overrides
  /// <inheritdoc/>
  public override bool InitPrefab() {
    if (!base.InitPrefab()) {
      return false;
    }
    title = null;
    baseInfo.text = null;
    availableResourcesInfo.text = null;
    requiredResourcesInfo.text = null;
    availableScienceInfo.text = null;
    hints = null;
    return true;
  }
  #endregion

  #region API methods
  /// <summary>Updates tooltip size to the nwe content</summary>
  /// <remarks>
  /// Should be called after a new content was set, including the case when a new tooltip has just
  /// been created.
  /// </remarks>
  public void UpdateLayout() {
    var prefabWidth = UnityPrefabController.GetPrefab<Tooltip>().preferredContentWidth;
    if (titleText.gameObject.activeSelf
        && !baseInfo.gameObject.activeSelf
        && !availableResourcesInfo.gameObject.activeSelf
        && !requiredResourcesInfo.gameObject.activeSelf
        && !availableScienceInfo.gameObject.activeSelf) {
      FitSizeToTitle(prefabWidth);
    } else {
      preferredContentWidth = prefabWidth;
    }
    LayoutRebuilder.ForceRebuildLayoutImmediate(mainRect);
  }

  /// <summary>Adds color tags to a text to a hotkey hint text.</summary>
  /// <param name="text">The text to wrap.</param>
  /// <returns>The rich text string.</returns>
  public static string MakeHintHotkey(string text) {
    return string.Format(HintHotkeyPattern, text);
  }

  /// <summary>Adds color tags to a text to that represents an interactive hint value.</summary>
  /// <param name="text">The text to wrap.</param>
  /// <returns>The rich text string.</returns>
  public static string MakeHintActiveValue(string text) {
    return string.Format(HintActiveValuePattern, text);
  }

  /// <summary>
  /// Adds color tags to a text to indiacte that it has some special meaning in the contex.
  /// </summary>
  /// <param name="text">The text to wrap.</param>
  /// <returns>The rich text string.</returns>
  public static string MakeHighlightedValue(string text) {
    return string.Format(InfoHighlightPattern, text);
  }
  #endregion

  #region Local utility methods
  /// <summary>Shrinks winod to the maxcimum title string size.</summary>
  /// <remarks>If title is wrapped, then this method finds the longest wrapped line.</remarks>
  /// <param name="maxWidth"></param>
  void FitSizeToTitle(float maxWidth) {
    var textGen = new TextGenerator();
    var generationSettings = titleText.GetGenerationSettings(new Vector2(maxWidth, 10000));
    textGen.Populate(title, generationSettings);
    var lines = textGen.lines;
    float maxTextWidth = 0;
    for (var i = 0; i < lines.Count; ++i) {
      var line = lines[i];
      var lineEnd = i < lines.Count - 1
          ? lines[i + 1].startCharIdx
          : title.Length;
      var lineText = title.Substring(line.startCharIdx, lineEnd - line.startCharIdx);
      maxTextWidth =
          Mathf.Max(maxTextWidth, textGen.GetPreferredWidth(lineText, generationSettings));
    }
    preferredContentWidth = maxTextWidth;
  }

  /// <summary>Tracks mouse position and adjusts the tooltip.</summary>
  IEnumerator TooltipPositionCoroutine() {
    UpdateLayout();
    var canvasRect = GetComponentInParent<Canvas>().transform as RectTransform;
    while (_followsPointer) {
      var pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
      var tooltipSize = mainRect.sizeDelta;
      var xPos = Input.mousePosition.x + RightSideMousePadding;
      if (xPos + tooltipSize.x > canvasRect.sizeDelta.x) {
        xPos = Input.mousePosition.x - LeftSideMousePadding - tooltipSize.x;
      }
      var yPos = Input.mousePosition.y;
      if (yPos < tooltipSize.y) {
        yPos = tooltipSize.y;
      }
      mainRect.position = new Vector3(xPos, yPos, 0);
      yield return null;
    }
  }
  #endregion
}

}  // namespace
