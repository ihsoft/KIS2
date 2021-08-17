﻿// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.ConfigUtils;
using UnityEngine;

// ReSharper disable once CheckNamespace
// ReSharper disable once IdentifierTypo
namespace KISAPIv2 {

/// <summary>Container for the various global settings of the mod.</summary>
[PersistentFieldsDatabase("KIS2/settings2/KISConfig")]
public sealed class CommonConfigImpl {
  #region Settings
  // ReSharper disable ConvertToAutoProperty

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

  /// <summary>Tells if only the parts with the stock cargo module can be added into the inventory.</summary>
  /// <remarks>
  /// If this flag set, then only parts that have <see cref="ModuleCargoPart"/> module can be added into the inventory.
  /// Additionally, this module must specify a non-zero volume. 
  /// </remarks>
  public bool addOnlyCargoParts => _addOnlyCargoParts;

  /// <summary>Tells if the stock inventory GUI must be hidden.</summary>
  public bool hideStockGui => _hideStockGui;

  // ReSharper disable FieldCanBeMadeReadOnly.Global
  // ReSharper disable ConvertToConstant.Global

  /// <summary>Various settings tha affect how the inventory icons are made.</summary>
  /// <remarks>
  /// All the default values below give a nice look in KSP 1.12. It may not be the case in the future versions.
  /// </remarks>
  /// FIXME: It may not be the right place. Move it into PartIconUtils? 
  public class IconSnapshotSettings {
    /// <summary>
    /// Icon resolution to use when screen resolution is 1080p. It may get upscaled for the greater resolutions. 
    /// </summary>
    [PersistentField("baseIconResolution")]
    public int baseIconResolution = 256;

    [PersistentField("cameraFov")]
    public float cameraFov = 540f; // From the stock inventory: 30 FOV x 18 boost
    [PersistentField("cameraElevation")]
    public float cameraElevation = 15f;
    [PersistentField("cameraAzimuth")]
    public float cameraAzimuth = 25f;
    [PersistentField("cameraPitch")]
    public float cameraPitch = 15f;
    [PersistentField("cameraHeading")]
    public float cameraHeading = 25f;

    [PersistentField("lightRotation")]
    public Vector3 lightRotation = new(45, -90, 0);
    [PersistentField("lightIntensity")]
    public float lightIntensity = 0.4f;

    [PersistentField("ambientLightColor")]
    public Color ambientLightColor = new(0.463f, 0.463f, 0.463f, 1.000f);
    [PersistentField("ambientEquatorColor")]
    public Color ambientEquatorColor = new(0.580f, 0.580f, 0.580f, 1.000f);
    [PersistentField("ambientGroundColor")]
    public Color ambientGroundColor = new(0.345f, 0.345f, 0.345f, 1.000f);
    [PersistentField("ambientSkyColor")]
    public Color ambientSkyColor = new(0.463f, 0.463f, 0.463f, 1.000f);
  }
  // ReSharper enable FieldCanBeMadeReadOnly.Global
  // ReSharper enable ConvertToConstant.Global

  /// <summary>Settings to make inventory part icons.</summary>
  public IconSnapshotSettings iconIconSnapshotSettings => _iconIconSnapshotSettings;

  // ReSharper enable ConvertToAutoProperty
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

  [PersistentField("Compatibility/addOnlyCargoParts")]
  bool _addOnlyCargoParts = true;
  
  [PersistentField("Compatibility/hideStockGui")]
  bool _hideStockGui = false;

  [PersistentField("IconSnapshotSettings")]
  IconSnapshotSettings _iconIconSnapshotSettings = new();

  // ReSharper enable FieldCanBeMadeReadOnly.Local
  // ReSharper enable ConvertToConstant.Local
  #endregion

  internal CommonConfigImpl() {
    ConfigAccessor.ReadFieldsInType(GetType(), this);
  }
}
  
}  // namespace
