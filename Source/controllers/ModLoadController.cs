// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.LogUtils;
using System;
using UnityEngine;

namespace KIS2 {

/// <summary>Initializes the mod and warns if it's not loaded correctly.</summary>
[KSPAddon(KSPAddon.Startup.Instantly, true /*once*/)]
public sealed class KISModLoadController : MonoBehaviour {
  public static bool modIsInconsistent { get; private set; }

  #region MonoBehaviour overrides
  void Awake() {
    DebugEx.Info("[KISModLoadController] Start loading configuartion...");
    //FIXME: check critical resources location 
    InvokeLoader(UIKISInventoryWindowController.OnGameLoad);
    if (modIsInconsistent) {
      DebugEx.Error("[KISModLoadController] Loaded with errors!");
    } else {
      DebugEx.Info("[KISModLoadController] Successfully loaded!");
    }
    //FIXME: show a dialog on the game load if mod loading failed.
  }
  #endregion

  #region Local utility methods
  void InvokeLoader(Action fn) {
    try {
      fn();
    } catch (Exception ex) {
      modIsInconsistent = true;
      DebugEx.Error("[KISModLoadController]: {0}", ex);
    }
  }
  #endregion
}

}  // namespace
