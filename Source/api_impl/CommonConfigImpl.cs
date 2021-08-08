// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.ConfigUtils;

// ReSharper disable once CheckNamespace
// ReSharper disable once IdentifierTypo
namespace KISAPIv2 {

/// <summary>Container for the various global settings of the mod.</summary>
[PersistentFieldsDatabase("KIS2/settings2/KISConfig")]
public sealed class CommonConfigImpl {
  #region Settings
  /// <summary>Path to the sound clip that plays "NOPE" sound.</summary>
  /// <remarks>Use this sound each time the user action cannot be performed.</remarks>
  public string sndPathBipWrong => _sndPathBipWrong;

  /// <summary>Tells if items in the inventories can be freely adjusted in flight.</summary>
  public bool builderModeEnabled => _builderModeEnabled;

  /// <summary>Tells if all interactions with the stock inventory must follow the stacking rules.</summary>
  /// <remarks>
  /// If this flag set, then all operations on adding new parts into the inventory will use the stock inventory methods.
  /// This also blocks adding non cargo module parts in to the inventory. This setting gives the best level of
  /// compatibility, but significantly limits the KIS abilities.
  /// </remarks>
  public bool respectStockStackingLogic => _respectStockStackingLogic;

  /// <summary>Tells if number of the slots must not be changed in the stock inventory.</summary>
  /// <remarks>
  /// If this flag set, then no new slots will be added when extra space is needed to accomodate a new item. It gives a
  /// great backwards compatibility, but at the price of significant limiting of the KIS storage functionality.
  /// </remarks>
  public bool respectStockInventoryLayout => _respectStockInventoryLayout;

  /// <summary>Tells if the stock inventory GUI must be hidden.</summary>
  public bool hideStockGui => _hideStockGui;
  #endregion

  #region Local fields and properties
  // ReSharper disable FieldCanBeMadeReadOnly.Local
  // ReSharper disable ConvertToConstant.Local

  [PersistentField("Sounds/bipWrong")]
  string _sndPathBipWrong = "";

  [PersistentField("BuilderMode/enabled")]
  bool _builderModeEnabled = false;

  [PersistentField("Compatibility/respectStockStackingLogic")]
  bool _respectStockStackingLogic = true;

  [PersistentField("Compatibility/respectStockInventoryLayout")]
  bool _respectStockInventoryLayout = true;

  [PersistentField("Compatibility/hideStockGui")]
  bool _hideStockGui = false;

  // ReSharper enable FieldCanBeMadeReadOnly.Local
  // ReSharper enable ConvertToConstant.Local
  #endregion

  internal CommonConfigImpl() {
    ConfigAccessor.ReadFieldsInType(GetType(), this);
  }
}
  
}  // namespace
