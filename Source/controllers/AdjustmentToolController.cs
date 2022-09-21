// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using EditorGizmos;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using UnityEngine;

namespace KIS2 {

/// <summary>Experimental controller that deals with parts rotation and positioning.</summary>
//[KSPAddon(KSPAddon.Startup.Flight, false /*once*/)]
public class AdjustmentToolController : MonoBehaviour {
  Part _hitPart;
  
  
  void DebugGizmo() {
    if (_gizmoRotate == null && _hitPart != null) {
      _gizmoRotate = GizmoRotate.Attach(
          _hitPart.transform, _hitPart.transform.position, _hitPart.initRotation,
          OnRotateGizmoUpdate, OnRotateGizmoUpdated, Camera.main);
    } else {
      Hierarchy.SafeDestroy(_gizmoRotate);
    }
  }
  GizmoRotate _gizmoRotate;
  
  private void OnRotateGizmoUpdate(Quaternion dRot) {
    DebugEx.Warning("*** rotating: {0}", dRot);
    Transform transform = _hitPart.transform;
    Space coordSpace = _gizmoRotate.CoordSpace;
    if (coordSpace != 0) {
      if (coordSpace == Space.Self) {
        transform.rotation = _gizmoRotate.transform.rotation * _hitPart.initRotation;
      }
    } else {
      transform.rotation = dRot * _gizmoRotate.HostRot0;
    }
    //_hitPart.attRotation0 = _hitPart.transform.localRotation;
    //_hitPart.attRotation = gizmoAttRotate * Quaternion.Inverse(gizmoAttRotate0) * selectedPart.transform.localRotation;
  }
  
  private void OnRotateGizmoUpdated(Quaternion dRot) {
    DebugEx.Warning("*** rotated: {0}", dRot);
    if (_hitPart == null) {
      return;
    }
    if (_hitPart.vessel != null) {
      _hitPart.UpdateOrgPosAndRot(_hitPart.vessel.rootPart);
    }
  }
}
}
