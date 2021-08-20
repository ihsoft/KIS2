// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
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
  // ReSharper disable FieldCanBeMadeReadOnly.Global
  // ReSharper disable ConvertToConstant.Global
  // ReSharper disable CollectionNeverUpdated.Global
  // ReSharper disable UnassignedField.Global

  /// <summary>Path to the sound clip that plays "NOPE" sound.</summary>
  /// <remarks>Use this sound each time the user action cannot be performed.</remarks>
  public string sndPathBipWrong => _sndPathBipWrong;

  /// <summary>Tells if items in the inventories can be freely adjusted in flight.</summary>
  public bool builderModeEnabled => _builderModeEnabled;

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

  /// <summary>Various settings to control the compatibility with the stock inventory system.</summary>
  public class StockCompatibilitySettings {
    /// <summary>Tells if all interactions with the stock inventory must follow the stacking rules.</summary>
    /// <remarks>
    /// If this flag set, then all operations on adding new parts into the inventory will use the stock inventory methods.
    /// This also blocks adding non cargo module parts in to the inventory. This setting gives the best level of
    /// compatibility, but significantly limits the KIS abilities.
    /// </remarks>
    [PersistentField("respectStockStackingLogic")]
    public bool respectStockStackingLogic;

    /// <summary>Tells if number of the slots must not be changed in the stock inventory.</summary>
    /// <remarks>
    /// If this flag set, then no new slots will be added when extra space is needed to accomodate a new item. It gives a
    /// great backwards compatibility, but at the price of significant limiting of the KIS storage functionality.
    /// </remarks>
    [PersistentField("respectStockInventoryLayout")]
    public bool respectStockInventoryLayout;

    /// <summary>Tells if only the parts with the stock cargo module can be added into the inventory.</summary>
    /// <remarks>
    /// If this flag set, then only parts that have <see cref="ModuleCargoPart"/> module can be added into the inventory.
    /// Additionally, this module must specify a non-zero volume and stack size.
    /// </remarks>
    [PersistentField("addOnlyCargoParts")]
    public bool addOnlyCargoParts = false;

    /// <summary>Tells if the stock inventory GUI must be hidden.</summary>
    /// <remarks>
    /// A rule of thumb is to hide the stock GUI if there is <i>any</i> incompatibility with the stock system.
    /// </remarks>
    public bool hideStockGui;

    /// <summary>
    /// List of parts for which the volume must be calculated based on the model size, regardless to the cargo module
    /// settings. 
    /// </summary>
    /// <remarks>
    /// Cargo module settings are not subject to the variant override. And there are pats that may significantly change
    /// their model (and volume) based on the variant selected. For such parts it's recommended to exclude them from the
    /// regular logic and always use KIS geometric based calculation.
    /// </remarks>
    [PersistentField("stockVolumeExceptions", isCollection = true)]
    public HashSet<string> stockVolumeExceptions = new();
  }

  /// <summary>Settings to make inventory part icons.</summary>
  public IconSnapshotSettings iconIconSnapshotSettings => _iconIconSnapshotSettings;

  /// <summary>Settings to control compatibility aspects with teh stock system.</summary>
  [PersistentField("Compatibility")]
  public StockCompatibilitySettings compatibilitySettings = new();

  // ReSharper enable ConvertToAutoProperty
  // ReSharper enable FieldCanBeMadeReadOnly.Global
  // ReSharper enable ConvertToConstant.Global
  // ReSharper enable CollectionNeverUpdated.Global
  // ReSharper enable UnassignedField.Global
  #endregion

  #region Local fields and properties
  // ReSharper disable FieldCanBeMadeReadOnly.Local
  // ReSharper disable ConvertToConstant.Local

  [PersistentField("Sounds/bipWrong")]
  string _sndPathBipWrong = "";

  [PersistentField("BuilderMode/enabled")]
  bool _builderModeEnabled = false;

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
