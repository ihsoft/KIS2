// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Linq;
using UnityEngine;

namespace KSPDev.ModelUtils2 {

/// <summary>Various tools to deal with game object hierarchy.</summary>
public static class Hierarchy2 {
  /// <inheritdoc cref="SafeDestory(Transform)"/>
  public static void SafeDestory(GameObject obj) {
    if (obj != null) {
      obj.transform.SetParent(null, worldPositionStays: false);
      obj.name = "$disposed";
      obj.SetActive(false);
      UnityEngine.Object.Destroy(obj);
    }
  }

  /// <inheritdoc cref="SafeDestory(Transform)"/>
  public static void SafeDestory(Component comp) {
    if (comp != null) {
      SafeDestory(comp.gameObject);
    }
  }
}

}  // namespace
