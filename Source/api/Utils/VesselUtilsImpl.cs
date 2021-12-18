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
  /// <param name="movingVessel">The vessel to re-position.</param>
  /// <param name="position">The new position.</param>
  /// <param name="rotation">The new rotation.</param>
  /// <param name="refPart">An optional part to sync velocities to.</param>
  public void MoveVessel(Vessel movingVessel, Vector3 position, Quaternion rotation, Part refPart) {
    DebugEx.Info("Moving dragged vessel: part={0}, vessel={1}", movingVessel.rootPart, movingVessel.vesselName);
    movingVessel.ResetGroundContact();
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
      DebugEx.Fine("Sync the dragged vessel velocity: vessel={0}, syncTarget={1}",
                   movingVessel.vesselName, refVessel.vesselName);
    } else {
      DebugEx.Fine("Nullify the dragged vessel velocity: vessel={0}", movingVessel.vesselName);
    }
    foreach (var p in movingVessel.parts.Where(p => p.rb != null)) {
      p.rb.velocity = refVelocity;
      p.rb.angularVelocity = refAngularVelocity;
    }
  }
  #endregion
}

}  // namespace
