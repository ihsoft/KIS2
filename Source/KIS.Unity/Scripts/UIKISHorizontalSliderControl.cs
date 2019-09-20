// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace KIS2 {

/// <summary>KSP alike range value selection control.</summary>
public sealed class UIKISHorizontalSliderControl : UIControlBaseScript {
  #region Unity serialized fields
  [SerializeField]
  Slider sliderControl = null;

  [SerializeField]
  Text sliderText = null;
  #endregion

  #region API properties
  /// <summary>Text which is shown on top of the scroll bar area.</summary>
  /// <remarks>Can be set to empty string to hide the control.</remarks>
  public string text {
    get { return sliderText.text; }
    set {
      sliderText.text = value;
      sliderText.gameObject.SetActive(!string.IsNullOrEmpty(value));
    }
  }

  /// <summary>Current value of the slider.</summary>
  public float value {
    get { return sliderControl.value; }
    set { sliderControl.value = value; }
  }

  /// <summary>Minimum value of the slider.</summary>
  public float minValue {
    get { return sliderControl.minValue; }
    set { sliderControl.minValue = value; }
  }

  /// <summary>Maximum value of the slider.</summary>
  public float maxValue {
    get { return sliderControl.maxValue; }
    set { sliderControl.maxValue = value; }
  }
  #endregion
}

}  // namespace
