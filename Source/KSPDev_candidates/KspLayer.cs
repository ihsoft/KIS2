﻿// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

namespace KSPDev.ModelUtils {

/// <summary>Defines various layers used in the game.</summary>
/// <remarks>
/// It's not a full set of the layers. More investigation is needed to reveal all of them.
/// </remarks>
/// <seealso cref="KspLayerMask"/>
/// <seealso href="https://wiki.kerbalspaceprogram.com/wiki/API:Layers">KSP API: Layers</seealso>
public enum KspLayer2 {
  /// <summary>The layer for a regular part.</summary>
  Part = 0,

  /// <summary>The layer to set bounds of a celestial body.</summary>
  /// <remarks>
  /// It's a very rough boundary of a planet, moon or asteroid. Used for macro objects detection.
  /// </remarks>
  Service = 10, 

  /// <summary>The "zero" level collider of a static structure on the surface.</summary>
  /// <remarks>E.g. a launchpad.</remarks>
  SurfaceCollider = 15,

  /// <summary>The layer for the kerbonaut models.</summary>
  Kerbal = 17,

  /// <summary>The layer for the various interaction colliders.</summary>
  /// <remarks>
  /// The meshes on this layer are not rendered. They are only used to trigger collision events when
  /// a kerbal model is in range.
  /// </remarks>
  TriggerCollider = 21,

  /// <summary>The layer to use when a non-scene object needs to be rendered in flight.</summary>
  /// <remarks>For example, this layer can be used to make icons of the offline parts/vessels.</remarks>
  DragRender = 29,

  /// <summary>The layer for FX objects.</summary>
  /// <remarks>E.g. <c>PadFXReceiver</c> on the Kerbin's VAB launchpad.</remarks>
  Fx = 30,
}

}  // namespace
