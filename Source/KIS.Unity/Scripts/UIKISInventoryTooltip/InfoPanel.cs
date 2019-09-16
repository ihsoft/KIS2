// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace KIS2.UIKISInventoryTooltip {

/// <summary>Basic element of the tootltip.</summary>
/// <seealso cref="Tooltip"/>
public sealed class InfoPanel : UIControlBaseScript {
  #region Unity serialized fields
  [SerializeField]
  Text captionText = null;

  [SerializeField]
  Text infoText = null;
  #endregion

  #region API properties
  /// <summary>Caption of the panel.</summary>
  /// <value>The caption or <c>null</c> if panel doesn't have caption.</value>
  public string caption {
    get { return captionText != null ? captionText.text : null; }
    set {
      if (captionText != null) {
        captionText.text = value;
      }
    }
  }

  /// <summary>Text to show in the panel. Rich text is supported.</summary>
  /// <remarks>Can be set to empty string to hide the control.</remarks>
  public string text {
    get { return infoText.text; }
    set {
      infoText.text = value;
      gameObject.SetActive(!string.IsNullOrEmpty(value));
    }
  }
  #endregion
}

}  // namespace
