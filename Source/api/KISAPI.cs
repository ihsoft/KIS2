﻿// Kerbal Inventory System API v2
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: Public Domain
//
// This module is used to build the API assembly. Once released to the public the version of this
// assembly cannot change. This guarantees that all the dependent mods will be able to load it. In
// case of a new version of API is released it must inherit from the previous version, and become a
// completely new assembly that is supplied together with the old versions.
//
// It's unspecified how many old versions of the API are to be preserved in the distribution. Mod
// developers should migrate to the newest available API version as soon as possible. In a normal
// case, each version is an ancestor of the previous version(s), so the migration
// should be trivial. However, in case of there is some methods/interfaces deprecation, an extra
// work may be required to migrate.

// FIXME: For now it's only a dummy! A lot of refactoring is needed before we get KIS API.

// Name of the namespace denotes the API version.
namespace KISAPIv2 {

/// <summary>KIS API, version 1.</summary>
/// FIXME: This implementation is a FAKE for now. It's not a real API.
public static class KISAPI {
  /// <summary>Tells if API V1 was loaded and ready to use.</summary>
  public static bool isLoaded = true;

  /// <summary>Utils to deal with various aspects of part models.</summary>
  public static readonly PartModelUtilsImpl PartModelUtils = new PartModelUtilsImpl();

  /// <summary>Utils to deal with part config nodes.</summary>
  public static readonly PartNodeUtilsImpl PartNodeUtils = new PartNodeUtilsImpl();

  /// <summary>Utils to deal with part icons.</summary>
  public static readonly PartIconUtils PartIconUtils = new PartIconUtils();

  /// <summary>
  /// Controller that handles moving KIS items between the inventories and the scene.
  /// </summary>
  public static readonly IKISItemDragController ItemDragController =
      new KIS2.KISItemDragControllerImpl();
}

}  // namespace
