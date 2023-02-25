// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using KSPDev.LogUtils;
using KSPDev.ProcessingUtils;
using UnityEngine;

namespace KSPDev.GUIUtils {

/// <summary>Blocks the right mouse button click in the flight mode to let other shortcuts to work.</summary>
/// <remarks>
/// In flight, RMB action unconditionally triggers Part Action Window (PAW) appearance. However, in some cases modules
/// want to capture this click and use it for their benefit. This class makes it real. Add this class to any live game
/// object and it will start blocking PAW appearance. Destroy the class when it's no more needed. For better results use
/// <code>DestroyImmediately</code>.
/// </remarks>
public sealed class RightClickBlocker : MonoBehaviour {
  #region MonoBehaviour
  void Awake() {
    if (!HighLogic.LoadedSceneIsFlight) {
      DebugEx.Error("Only applicable to the FLIGHT scenes");
      return;
    }
    GameEvents.onPartActionUIShown.Add(OnPawShow);
  }

  void OnDestroy() {
    GameEvents.onPartActionUIShown.Remove(OnPawShow);
  }
  #endregion

  #region Local utility methods
  void OnPawShow(UIPartActionWindow window, Part part) {
    // If RMB action spawns PAW, then hide it and destroy.
    window.gameObject.SetActive(false);
    AsyncCall.CallOnEndOfFrame(this, () => {
      window.isValid = false;
      UIPartActionController.Instance.UpdateFlight();
    });
  }
  #endregion
}
}
