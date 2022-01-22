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

  /// <summary>
  /// When this mode is enabled, the KIS mod will be 100% compatible with the stock inventory system if the game's type
  /// is either carrier or science.
  /// </summary>
  /// <remarks>
  /// <p>
  /// Some inventory related features of KIS will be lost, but most of nice KIS features will be available. And the
  /// stock system behavior will be fully honored. This mode makes sense if there is a plan to drop KIS in the future or
  /// if there are other mods that require the strict stock logic. The following abilities will be unavailable:
  /// </p>
  /// <ul>
  /// <li>Unlimited stacking in the slot. The stock limit will be honored.</li>
  /// <li>Stacking of the parts with resources.</li>
  /// <li>Extra slots to store parts. The stock setting will be honored.</li>
  /// <li>KIS inventory dialog resizing. It will permanently have the number of slots, which is setup in the part.</li>
  /// </ul>
  /// </remarks>
  [PersistentField("Compatibility/fullCompatibilityInCarrierGame")]
  public static bool fullCompatibilityInCarrierGame = true;

  /// <summary>
  /// Indicates if the stock inventory compatibility must be maintained in the sandbox games. Similar to
  /// <see cref="fullCompatibilityInCarrierGame"/>.
  /// </summary>
  /// <remarks>
  /// In the normal case there is no good reason to restrict KIS in the sandbox games: they are not supposed to be
  /// compatible anyway. However, it may be useful for the testing.
  /// </remarks>
  [PersistentField("Compatibility/fullCompatibilityInSandboxGame")]
  public static bool fullCompatibilityInSandboxGame = true;

  /// <summary>Indicates if the current game must be running in the full compatibility mode.</summary>
  /// <seealso cref="fullCompatibilityInCarrierGame"/>
  /// <seealso cref="fullCompatibilityInSandboxGame"/>
  public static bool isCompatibilityMode =>
      HighLogic.CurrentGame.Mode != Game.Modes.SANDBOX && fullCompatibilityInCarrierGame
      || HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX && fullCompatibilityInSandboxGame;

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
