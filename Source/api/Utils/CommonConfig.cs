﻿// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.ConfigUtils;
using UnityEngine;

// ReSharper disable once CheckNamespace
// ReSharper disable once IdentifierTypo
namespace KISAPIv2 {

/// <summary>Container for the various global settings of the mod.</summary>
[PersistentFieldsDatabase("KIS2/settings2/KISConfig")]
public static class CommonConfig {
  #region Settings
  // ReSharper disable ConvertToConstant.Global
  // ReSharper disable FieldCanBeMadeReadOnly.Global

  /// <summary>Path to the sound clip that plays "NOPE" sound.</summary>
  /// <remarks>Use this sound each time the user action cannot be performed.</remarks>
  [PersistentField("Sounds/bipWrong")]
  public static string sndPathBipWrong = "KIS2/Sounds/bipwrong";

  /// <summary>Path to the clip that plays "UI click" sound.</summary>
  [PersistentField("Sounds/click")]
  public static string sndClick = "KIS2/Sounds/click";

  /// <summary>Tells if items in the inventories can be freely adjusted in flight.</summary>
  [PersistentField("BuilderMode/enabled")]
  public static bool builderModeEnabled = false;

  /// <summary>Tells if the "sample parts" hotkey should be active.</summary>
  [PersistentField("AlphaFlags/enableInventorySamples")]
  public static bool alphaFlagEnableSamples = false;

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

    [PersistentField("cameraDistance")]
    public float cameraDistance = 100f;
    [PersistentField("cameraOrthoSize")]
    public float cameraOrthoSize = 0.75f;
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

  /// <summary>Settings to make inventory part icons.</summary>
  [PersistentField("IconSnapshotSettings")]
  public static IconSnapshotSettings iconIconSnapshotSettings = new();

  // ReSharper enable FieldCanBeMadeReadOnly.Global
  // ReSharper enable ConvertToConstant.Global
  #endregion

  internal static void Initialize() {
    ConfigAccessor.ReadFieldsInType(typeof(CommonConfig), null);
  }
}

}  // namespace
