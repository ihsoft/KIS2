// Kerbal Development tools for Unity scripts.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KSPDev.Unity {

/// <summary>Various tools to deal with game object hierarchy.</summary>
public static class HierarchyUtils {
  /// <summary>Destroys the object in a way which is safe for physical callback methods.</summary>
  /// <remarks>
  /// This method removes the object from hierarchy, renames it, deactivates, and then destroys via
  /// <c>Destroy()</c> method. This sequence guarantees that the object won't get involved into any
  /// physics interactions in the next frame. And at the same tme the unsafe <c>DestroyImmediate</c>
  /// method is not called.
  /// </remarks>
  /// <param name="obj">The object to destroy.</param>
  public static void SafeDestroy(Transform obj) {
    if (obj != null) {
      SafeDestroy(obj.gameObject);
    }
  }

  /// <inheritdoc cref="SafeDestroy(Transform)"/>
  public static void SafeDestroy(GameObject obj) {
    if (obj == null) {
      return;
    }
    obj.transform.SetParent(null, worldPositionStays: false);
    obj.name = "$disposed";
    obj.SetActive(false);
    Object.Destroy(obj);
  }

  /// <inheritdoc cref="SafeDestroy(Transform)"/>
  public static void SafeDestroy(Component obj) {
    if (obj != null) {
      SafeDestroy(obj.gameObject);
    }
  }
}

}  // namespace
