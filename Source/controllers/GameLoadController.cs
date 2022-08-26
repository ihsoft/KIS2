// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections;
using KSP.Localization;
using KSPDev.GUIUtils;
using KSPDev.LogUtils;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {
/// <summary>Changes all the stock inventories so that they are KIS compatible.</summary>
/// <remarks>
/// This controller takes care of ALL the parts. The incompatible stock parts must be adjusted so that they become
/// compatible. All the edge cases have to be handled (or at least, reported) here too.
/// </remarks>
class KisLoadController: LoadingSystem {
  #region Local values
  const float MaxTimePerCallMs = 0.1f;
  const string InfoJoinStringTag = "#autoLOC_8004190";
  const string PaketVolumeLimitStringTag = "#autoLOC_8003415";

  bool _isReady;
  float _progressIndicator;
  float _roundStartTime;
  #endregion

  #region LoadingSystem overrides
  /// <inheritdoc/>
  public override bool IsReady() => _isReady;

  /// <inheritdoc/>
  public override string ProgressTitle() => "KIS";

  /// <inheritdoc/>
  public override float ProgressFraction() => _progressIndicator;

  /// <inheritdoc/>
  public override void StartLoad() {
    StartCoroutine(AdjustNegativeVolumeParts());
  }
  #endregion

  #region Local utility methods
  /// <summary>Runs a coroutine that fixes all the parts.</summary>
  IEnumerator AdjustNegativeVolumeParts() {
    _roundStartTime = Time.realtimeSinceStartup;
    for (var i = 0; i < PartLoader.LoadedPartsList.Count; i++) {
      _progressIndicator = (float) i / PartLoader.LoadedPartsList.Count;
      if (Time.realtimeSinceStartup - _roundStartTime < MaxTimePerCallMs) {
        PatchPartVolume(PartLoader.LoadedPartsList[i]);
        continue;
      }
      yield return null;
      _roundStartTime = Time.realtimeSinceStartup;
    }
    _isReady = true;
  }

  /// <summary>Fixes cargo module on one part.</summary>
  /// <param name="avPart">The part to fix.</param>
  void PatchPartVolume(AvailablePart avPart) {
    if (avPart.partPrefab.isKerbalEVA() || !StockCompatibilitySettings.fixInventoryDescriptions) {
      return;
    }

    // The inventory module must not be reporting the number of stock slots. Disable it.
    var inventoryModule = avPart.partPrefab.FindModuleImplementing<ModuleInventoryPart>();
    var cargoModule = avPart.partPrefab.FindModuleImplementing<ModuleCargoPart>();
    if (cargoModule != null || inventoryModule != null) {
      RefreshInfo(avPart, cargoModule, inventoryModule);
    }
  }

  /// <summary>Updates the editor's part info to reflect KIS changes.</summary>
  /// <param name="avPart">The part to update info for.</param>
  /// <param name="cargoModule">An optional cargo module in the part. It can be <c>null</c> if there is none.</param>
  /// <param name="inventoryModule">An optional inventory module. It can be <c>null</c> if there is none.</param>
  void RefreshInfo(AvailablePart avPart, ModuleCargoPart cargoModule, ModuleInventoryPart inventoryModule) {
    var oldStackLimit = 1;
    if (cargoModule != null && cargoModule.stackableQuantity > 1) {
      oldStackLimit = cargoModule.stackableQuantity;
      cargoModule.stackableQuantity = 1;  // This disables the limit string in the part info.
    }
    foreach (var moduleInfo in avPart.moduleInfos) {
      if (cargoModule != null && moduleInfo.moduleName == cargoModule.GUIName) {
        moduleInfo.info = cargoModule.GetInfo();
      }
      if (inventoryModule != null && moduleInfo.moduleName == inventoryModule.GUIName) {
        // Copied (and modified) from ModuleInventoryPart.GetInfo()
        if (Localizer.Tags.ContainsKey(InfoJoinStringTag) && Localizer.Tags.ContainsKey(PaketVolumeLimitStringTag)) {
          moduleInfo.info = Localizer.Format(
              InfoJoinStringTag,
              Localizer.Format(PaketVolumeLimitStringTag),
              VolumeLType.Format(inventoryModule.packedVolumeLimit));
        } else {
          DebugEx.Warning("Cannot find inventory info strings: {0}, {1}", InfoJoinStringTag, PaketVolumeLimitStringTag);
        }
      }
    }
    if (cargoModule != null) {
      cargoModule.stackableQuantity = oldStackLimit;
    }
  }
  #endregion
}

/// <summary>Runner for the loader.</summary>
[KSPAddon(KSPAddon.Startup.Instantly, true)]
sealed class KisLoadControllerRunner : MonoBehaviour {
  void Awake() {
    var list = LoadingScreen.Instance.loaders;
    for (var i = 0; i < list.Count; i++) {
      if (list[i] is PartLoader) {
        list.Insert(i + 1, new GameObject().AddComponent<KisLoadController>());
        break;
      }
    }
  }
}

}
