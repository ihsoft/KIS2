// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.Unity;
using UnityEngine;
using UnityEngine.UI;

// ReSharper disable once CheckNamespace
// ReSharper disable once IdentifierTypo
namespace KIS2.UIKISInventoryTooltip {

/// <summary>
/// Tooltip window class to show key info about a KSP part. It can automatically follow mouse cursor
/// or be statically attached to a control.
/// </summary>
/// <remarks>
/// <para>
/// There is a special basic mode of the tooltip when only title is defined. In this mode tooltip
/// tries to fit the minimum title string size. In the regular mode there is a minimum window size
/// that tooltip maintains.
/// </para>
/// <para>
/// All elements in the prefab will become non-raycast targets on init.
/// </para>
/// </remarks>
/// <seealso cref="InfoPanel"/>
public sealed class Tooltip : UiPrefabBaseScript {

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

  /// <summary>Main highlighted text.</summary>
  /// <remarks>Can be set to empty string to hide the control.</remarks>
  public string title {
    get => titleText.text;
    set {
      titleText.text = value;
      titleText.gameObject.SetActive(!string.IsNullOrEmpty(value));
    }
  }

  /// <summary>GUI hints, applicable to the tooltip object. Rich text is supported.</summary>
  /// <remarks>Can be set to empty string to hide the control.</remarks>
  /// <seealso cref="showHints"/>
  public string hints {
    get => hintsText.text;
    set {
      hintsText.text = value;
      hintsText.gameObject.SetActive(showHints && !string.IsNullOrEmpty(value));
    }
  }

  /// <summary>Basic part's information.</summary>
  public InfoPanel baseInfo => baseInfoPanel;

  /// <summary>Available resources on the part.</summary>
  public InfoPanel availableResourcesInfo => resourcesInfoPanel;

  /// <summary>Resources that are required by the part.</summary>
  public InfoPanel requiredResourcesInfo => requiresInfoPanel;

  /// <summary>Available science in the part.</summary>
  public InfoPanel availableScienceInfo => scienceInfoPanel;

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
    ClearInfoFields();
    hints = null;
    return true;
  }
  #endregion

  #region API methods
  /// <summary>Clears all fields except the hints one.</summary>
  public void ClearInfoFields() {
    title = null;
    baseInfo.text = null;
    availableResourcesInfo.text = null;
    requiredResourcesInfo.text = null;
    availableScienceInfo.text = null;
  }
  
  /// <summary>
  /// Updates tooltip preferred size to make the best fit, given the current content.
  /// </summary>
  /// <remarks>
  /// There is a special case when tooltip only has a title. In this case the window size is reduced
  /// so that it takes the minimum possible rect. In the other cases the window is resized to the
  /// preferred size defined in prefab.
  /// </remarks>
  /// <seealso cref="preferredContentWidth"/>
  public void UpdateLayout() {
    var prefabWidth = UnityPrefabController.GetPrefab<Tooltip>().preferredContentWidth;
    if (titleText.gameObject.activeSelf
        && !baseInfo.gameObject.activeSelf
        && !availableResourcesInfo.gameObject.activeSelf
        && !requiredResourcesInfo.gameObject.activeSelf
        && !availableScienceInfo.gameObject.activeSelf
        && !hintsText.gameObject.activeSelf) {
      FitSizeToTitle(prefabWidth);
    } else {
      preferredContentWidth = prefabWidth;
    }
    LayoutRebuilder.ForceRebuildLayoutImmediate(mainRect);
  }
  #endregion

  #region Local utility methods
  /// <summary>
  /// Sets window's preferred size so that the window wraps the title text and takes as small rect
  /// as possible.
  /// </summary>
  /// <remarks>If title is wrapped, then this method finds the longest wrapped line.</remarks>
  /// <param name="maxWidth">The width of the rect to fit title into.</param>
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
  #endregion
}

}  // namespace
