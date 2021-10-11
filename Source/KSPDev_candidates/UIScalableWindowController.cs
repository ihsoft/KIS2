// Kerbal Development tools for Unity scripts.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using KSP.UI;
using KSPDev.LogUtils;
using KSPDev.KSPInterfaces;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KSPDev.PrefabUtils {

/// <summary>Base script that reacts to the game UI scale changes.</summary>
/// <remarks>
/// This component needs to be added to a game object to have effect. The primary targets of this
/// component are the dynamic prefabs. On scale change, the GUI object will be repositioned
/// according to its anchors. A message <c>ControlUpdated</c> will be broadcasted to the components
/// on the parent object to let them updating as needed. 
/// </remarks>
public sealed class UIScalableWindowController2 : MonoBehaviour, IsDestroyable {
  #region Local fields and properties
  /// <summary>The previously known UI scale.</summary>
  /// <seealso cref="OnGameSettingsApplied"/>
  float _previousUiScale;
  #endregion

  #region MonoBehaviour overrides
  /// <summary>Activates the component.</summary>
  public void Awake() {
    _previousUiScale = UIMasterController.Instance.uiScale;
    GameEvents.OnGameSettingsApplied.Add(OnGameSettingsApplied);
  }

  /// <inheritdoc/>
  public void OnDestroy() {
    GameEvents.OnGameSettingsApplied.Remove(OnGameSettingsApplied);
  }
  #endregion

  #region Local utility methods
  /// <summary>Callback that is called when the game settings, like UI scale, are changed.</summary>
  void OnGameSettingsApplied() {
    if (Mathf.Abs(_previousUiScale - UIMasterController.Instance.uiScale) <= float.Epsilon) {
      return; // No changes.
    }
    DebugEx.Fine("Game settings changed: dialog={0}, originalScale={1}, newScale={2}",
                 name, _previousUiScale, UIMasterController.Instance.uiScale);
    _previousUiScale = UIMasterController.Instance.uiScale;
    SendMessage("ControlUpdated", gameObject, SendMessageOptions.DontRequireReceiver);
  }
  #endregion
}

}  // namespace
