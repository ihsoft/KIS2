// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using KSPDev.ConfigUtils;
using KSPDev.LogUtils;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {
/// <summary>Various settings to control the compatibility with the stock inventory system.</summary>
[PersistentFieldsDatabase("KIS2/settings2/KISConfig")]
static class StockCompatibilitySettings {
  #region Config properties
  // ReSharper disable FieldCanBeMadeReadOnly.Global
  // ReSharper disable ConvertToConstant.Global
  // ReSharper disable CollectionNeverUpdated.Global

  /// <summary>Indicates if the cargo stacking limit settings must be unconditionally obeyed.</summary>
  /// <remarks>
  /// If this flags set to <c>false</c>, then KIS may increase the stock slot size on its own discretion. This makes the
  /// save state less sparse, but may have bad consequences when the mod is removed.
  /// </remarks>
  [PersistentField("Compatibility/respectStockStackingLogic")]
  public static bool respectStockStackingLogic = true;

  /// <summary>Indicates if number of the slots must not be changed in the stock inventory.</summary>
  /// <remarks>
  /// If this flag set, then no new slots will be added when extra space is needed to accomodate a new item. It gives a
  /// great backwards compatibility, but at the price of significant limiting of the KIS storage functionality.
  /// </remarks>
  [PersistentField("Compatibility/respectStockInventoryLayout")]
  public static bool respectStockInventoryLayout = true;

  /// <summary>Indicates if parts without the cargo module can be handled by the KIS inventory.</summary>
  /// <remarks>
  /// KIS will calculate the part's volume based on the part's model bounds. And the stacking limit will be set to
  /// <c>1</c>. Which make make sense if <see cref="respectStockStackingLogic"/> is enabled.
  /// </remarks>
  [PersistentField("Compatibility/handleNonCargoParts")]
  public static bool handleNonCargoParts = false;

  /// <summary>Indicates if the inventory description should not have the stock related features.</summary>
  [PersistentField("Compatibility/fixInventoryDescriptions")]
  public static bool fixInventoryDescriptions = false;

  /// <summary>Indicates if the stock inventory GUI must be hidden.</summary>
  /// <remarks>
  /// A rule of thumb is to hide the stock GUI if there is <i>any</i> incompatibility with the stock system.
  /// </remarks>
  [PersistentField("Compatibility/hideStockGui")]
  public static bool hideStockGui = false;

  /// <summary>
  /// List of the parts for which the volume must be calculated based on the model size, regardless to the cargo module
  /// settings. 
  /// </summary>
  /// <remarks>
  /// Cargo module settings are not subject to the variant override. And there are pats that may significantly change
  /// their model (and volume) based on the variant selected. For such parts it's recommended to exclude them from the
  /// regular logic and always use KIS geometric based calculation.
  /// </remarks>
  [PersistentField("Compatibility/stockVolumeExceptions", isCollection = true)]
  public static HashSet<string> stockVolumeExceptions = new();

  // ReSharper enable FieldCanBeMadeReadOnly.Global
  // ReSharper enable ConvertToConstant.Global
  // ReSharper enable CollectionNeverUpdated.Global
  #endregion
}

[KSPAddon(KSPAddon.Startup.Instantly, true)]
sealed class StockCompatibilitySettingsRunner : MonoBehaviour {
  void Awake() {
    ConfigAccessor.ReadFieldsInType(typeof(StockCompatibilitySettings), null);
    DebugEx.Warning("Loaded stock compatibility settings");
  }
}

}
