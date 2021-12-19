// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using KSPDev.LogUtils;
using UnityEngine;

// ReSharper disable once CheckNamespace
// ReSharper disable once IdentifierTypo
namespace KISAPIv2 {

/// <summary>Various methods to deal with the vessels.</summary>
public class VesselUtilsImpl {

  #region API implementation
  /// <summary>Changes vessel's location.</summary>
  /// <remarks>
  /// Moving a vessel in the modern KSP world is not a straightforward action. There is a lot of arcade logic on top of
  /// it. So, a simple change of the vessel position won't make the trick.
  /// </remarks>
  /// <param name="movingVessel">The vessel to re-position.</param>
  /// <param name="position">The new position.</param>
  /// <param name="rotation">The new rotation.</param>
  /// <param name="refPart">An optional part to sync velocities to.</param>
  public void MoveVessel(Vessel movingVessel, Vector3 position, Quaternion rotation, Part refPart) {
    DebugEx.Info("Moving vessel: part={0}, vessel={1}", movingVessel.rootPart, movingVessel.vesselName);
    
    // Reset the RB anchors on the vessels that had contact with the moving vessel to make them refreshing their state.
    var resetParts = movingVessel.parts
        .SelectMany(x => x.currentCollisions.Keys)
        .Select(c => FlightGlobals.GetPartUpwardsCached(c.gameObject))
        .Where(p => p != null);
    foreach (var resetPart in resetParts) {
      DebugEx.Fine("Reset vessel RB anchor: vessel={0}, contactPart={1}", resetPart.vessel.vesselName, resetPart);
      resetPart.vessel.ResetRBAnchor();
    }

    DebugEx.Fine("Reset vessel RB anchor: vessel={0}", movingVessel.vesselName);
    movingVessel.ResetRBAnchor();
    CollisionEnhancer.bypass = true;  // This prevents the surface obstacles to interfere with the move.
    movingVessel.vesselTransform.position = position;
    movingVessel.vesselTransform.rotation = rotation;
    movingVessel.SetRotation(rotation);  // It applies changes to the parts.

    var refVelocity = Vector3.zero;
    var refAngularVelocity = Vector3.zero;
    if (refPart != null) {
      var refVessel = refPart.vessel;
      refVelocity = refVessel.rootPart.Rigidbody.velocity;
      refAngularVelocity = refVessel.rootPart.Rigidbody.angularVelocity;
      DebugEx.Fine("Sync the moved vessel velocity: vessel={0}, syncTarget={1}",
                   movingVessel.vesselName, refVessel.vesselName);
    } else {
      DebugEx.Fine("Nullify the moved vessel velocity: vessel={0}", movingVessel.vesselName);
    }
    foreach (var p in movingVessel.parts.Where(p => p.rb != null)) {
      p.rb.velocity = refVelocity;
      p.rb.angularVelocity = refAngularVelocity;
    }
  }
  #endregion
}

}  // namespace
