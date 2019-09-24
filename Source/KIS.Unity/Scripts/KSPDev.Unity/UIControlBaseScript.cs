// Kerbal Development tools for Unity scripts.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using UnityEngine;

namespace KSPDev.Unity {

/// <summary>
/// Base class of the Unity UI scripts that offers basic logging functionality and some convenience
/// properties.
/// </summary>
public class UIControlBaseScript : MonoBehaviour {

  /// <summary>UI transform rect of this control.</summary>
  public RectTransform mainRect {
    get { return transform as RectTransform; }
  }

  #region Inheritable utility methods
  /// <summary>Logs an info record with the type information.</summary>
  protected void LogInfo(string msg, params object[] args) {
    Debug.LogFormat("[" + GetType() + "#obj=" + gameObject.name + "] " + msg, args);
  }

  /// <summary>Logs a warning record with the type information.</summary>
  protected void LogWarning(string msg, params object[] args) {
    Debug.LogWarningFormat("[" + GetType() + "#obj=" + gameObject.name + "] " + msg, args);
  }

  /// <summary>Logs an error record with the type information.</summary>
  protected void LogError(string msg, params object[] args) {
    Debug.LogErrorFormat("[" + GetType() + "#obj=" + gameObject.name + "] " + msg, args);
  }
  #endregion
}

}  // namespace
