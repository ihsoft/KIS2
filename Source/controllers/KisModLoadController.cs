// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.LogUtils;
using KSPDev.PrefabUtils;
using System;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>Initializes the mod and warns if it's not loaded correctly.</summary>
[KSPAddon(KSPAddon.Startup.Instantly, true /*once*/)]
public sealed class KisModLoadController : MonoBehaviour {
  static bool modIsInconsistent { get; set; }

  #region MonoBehaviour overrides
  void Awake() {
    DebugEx.Info("[KisModLoadController] Start loading configuration...");
    //FIXME: check critical resources location 
    InvokeLoader(() => LoadAsset("ui_prefabs"));
    if (modIsInconsistent) {
      DebugEx.Error("[KisModLoadController] Loaded with errors!");
    } else {
      DebugEx.Info("[KisModLoadController] Successfully loaded!");
    }
    //FIXME: show a dialog on the game load if mod loading failed.
  }
  #endregion

  #region Local utility methods
  static void InvokeLoader(Action fn) {
    try {
      fn();
    } catch (Exception ex) {
      modIsInconsistent = true;
      DebugEx.Error("[KisModLoadController]: {0}", ex);
    }
  }

  static void LoadAsset(string assetFileName) {
    modIsInconsistent |= !PrefabLoader.LoadAllAssets(
        KSPUtil.ApplicationRootPath + "GameData/KIS/Prefabs/" + assetFileName);
  }
  #endregion
}

}  // namespace
