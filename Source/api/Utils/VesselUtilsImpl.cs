// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections;
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
    var movingVesselTransform = movingVessel.vesselTransform;
    if ((movingVesselTransform.position - position).sqrMagnitude < PositionPrecision
        && 1.0f - Math.Abs(Quaternion.Dot(movingVesselTransform.rotation, rotation)) < RotationPrecision) {
      return; // No significant changes.
    }
    
    DebugEx.Info("Moving vessel: part={0}, vessel={1}", movingVessel.rootPart, movingVessel.vesselName);
    
    // Reset the RB anchors on the vessels that had contact with the moving vessel to make them refreshing their state.
    var resetParts = movingVessel.parts
        .SelectMany(x => x.currentCollisions.Keys)
        .Select(c => FlightGlobals.GetPartUpwardsCached(c.gameObject))
        .Where(p => p != null);
    foreach (var resetPart in resetParts) {
      if (!resetPart.vessel.IsAnchored) {
        continue;
      }
      DebugEx.Fine("Reset vessel RB anchor: vessel={0}, contactPart={1}", resetPart.vessel.vesselName, resetPart);
      resetPart.vessel.ResetRBAnchor();
    }

    if (movingVessel.IsAnchored) {
      DebugEx.Fine("Reset vessel RB anchor: vessel={0}", movingVessel.vesselName);
      movingVessel.ResetRBAnchor();
    }
    CollisionEnhancer.bypass = true;  // This prevents the surface obstacles to interfere with the move.
    movingVesselTransform.position = position;
    movingVesselTransform.rotation = rotation;
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
  const float PositionPrecision = 0.001f; // 1mm
  const float RotationPrecision = 0.0000004f; // 1 degree on any axis

  /// <summary>Creates vessel from a part snapshot and waits till it becomes live.</summary>
  /// <remarks>
  /// This method creates a vessel that is activated through the full cycle of a packed vessel. The effect is the same
  /// as an existing vessel came into the physical range of the current scene. Such approach ensures the best
  /// compatibility with the existing an future parts, but the drawback is that the vessel cannot be created immediately
  /// in just one frame. It may take seconds before the vessel becomes active and operational.
  /// </remarks>
  /// <param name="partSnapshot">The [part snapshot to make a vessel from.</param>
  /// <param name="pos">The world position of the vessel.</param>
  /// <param name="rot">The world rotation of the vessel.</param>
  /// <param name="refTransform">
  /// The transform to use as an anchor of the position and rotation. I.e. the reference pos/rot will be used to
  /// calculate the relative values, and these values will be used to align the vessel relative to the transform. If
  /// this transform is <c>null</c>, then <paramref name="pos"/> and <paramref name="rot"/> are absolute.
  /// </param>
  /// <param name="refPart">
  /// The part to copy linear and angular velocities from. The new vessel velocities will be synced to this part. If
  /// this parameter is <c>null</c> then the new vessel will have zero velocities.
  /// </param>
  /// <param name="vesselCreatedFn">An action to execute when the vessel is created and fully initialized.</param>
  /// <param name="waitFn">
  /// An action that is called on every frame while the method awaits for the vessel to wake up. The passed argument can
  /// be <c>null</c> at the early stages of the vessel creation. When the argument is not <c>null</c>, the vessel can
  /// still not be ready, but ity will be known to <c>FlightGlobals</c>.
  /// </param>
  /// <returns>Enumerator for the coroutine scheduling.</returns>
  public static IEnumerator CreateLonePartVesselAndWait(
      ProtoPartSnapshot partSnapshot, Vector3 pos, Quaternion rot, Transform refTransform,
      Part refPart = null, Action<Vessel> vesselCreatedFn = null, Action<Vessel> waitFn = null) {
    DebugEx.Info("Spawning new vessel from the dragged part: part={0}...", partSnapshot.partInfo.name);
    var protoVesselNode =
        KisApi.PartNodeUtils.MakeLonePartVesselNode(FlightGlobals.ActiveVessel, partSnapshot, pos, rot);
    var protoVessel = HighLogic.CurrentGame.AddVessel(protoVesselNode);
    var spawnedVesselId = protoVessel.persistentId;
    var spawnRefTransform = new GameObject().transform;
    spawnRefTransform.position = pos;
    spawnRefTransform.rotation = rot;
    if (refTransform != null) {
      spawnRefTransform.SetParent(refTransform);
    }

    var cancelGroundRepositioningFn = new EventData<Vessel>.OnEvent(
        delegate(Vessel v) {
          if (v.persistentId != spawnedVesselId) {
            return;
          }
          v.skipGroundPositioning = true;
          v.skipGroundPositioningForDroppedPart = true;
          KisApi.VesselUtils.MoveVessel(v, spawnRefTransform.position, spawnRefTransform.rotation, refPart);
          spawnedVesselId = 0;
        });
    GameEvents.onVesselGoOffRails.Add(cancelGroundRepositioningFn);

    Vessel spawnedVessel;
    while (true) {
      spawnedVessel = FlightGlobals.VesselsLoaded.FirstOrDefault(v => v.persistentId == protoVessel.persistentId);
      waitFn?.Invoke(spawnedVessel);
      if (spawnedVessel != null) {
        KisApi.VesselUtils.MoveVessel(spawnedVessel, spawnRefTransform.position, spawnRefTransform.rotation, refPart);
        if (!spawnedVessel.vesselSpawning) {
          break; // The vessel is ready.
        }
      }
      yield return null;
    }
    GameEvents.onVesselGoOffRails.Remove(cancelGroundRepositioningFn);
    UnityEngine.Object.Destroy(spawnRefTransform.gameObject);
    DebugEx.Info("Vessel spawned: type={0}, name={1}", spawnedVessel.vesselType, spawnedVessel.vesselName);
    vesselCreatedFn?.Invoke(spawnedVessel);
  }
  #endregion
}

}  // namespace
