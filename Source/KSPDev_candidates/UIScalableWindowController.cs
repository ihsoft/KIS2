// Kerbal Development tools for Unity scripts.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using KSP.UI;
using KSPDev.LogUtils;
using KSPDev.KSPInterfaces;
using KSPDev.Unity;
using System;
using UnityEngine;

namespace KSPDev.Prefabs {

/// <summary>Base script that reacts to the game UI scale changes.</summary>
/// <remarks>
/// This component needs to be added to a game object to have effect. The primary targets of this
/// component are the dynamic prefabs. 
/// </remarks>
/// <seealso cref="KSPDev.Unity.IKSPDevUnityControlChanged"/>
/// <seealso cref="KSPDev.Unity.UnityPrefabController"/>
public class UIScalableWindowController : UIControlBaseScript, IsDestroyable {
  #region Local fields and properties
  /// <summary>Previously known scale onf UI.</summary>
  /// <seealso cref="OnGameSettingsApplied"/>
  protected float previousUiScale { get; private set; }
  #endregion

  #region MonoBehaviour overrides
  /// <inheritdoc/>
  public virtual void Start() {
  }

  /// <inheritdoc/>
  public virtual void Awake() {
    previousUiScale = UIMasterController.Instance.uiScale;
    GameEvents.OnGameSettingsApplied.Add(OnGameSettingsApplied);
  }

  /// <inheritdoc/>
  public virtual void OnDestroy() {
    GameEvents.OnGameSettingsApplied.Remove(OnGameSettingsApplied);
  }
  #endregion

  #region Local utility methods
  /// <summary>Callback that is called when game settings, like UI scale, are changed.</summary>
  protected virtual void OnGameSettingsApplied() {
    var scaleDelta = UIMasterController.Instance.uiScale - previousUiScale;
    DebugEx.Info("Game settings changed: scaleDelta={0}, window={1}", scaleDelta, gameObject.name);
    previousUiScale = UIMasterController.Instance.uiScale;
    mainRect.anchoredPosition *= 1f - scaleDelta;
    SendMessage("ControlUpdated", gameObject, SendMessageOptions.DontRequireReceiver);
  }
  #endregion
}

}  // namespace
