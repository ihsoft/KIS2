// Kerbal Development tools for Unity scripts.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KSPDev.Unity {

/// <summary>Utility class to manage prefabs in Unity.</summary>
/// <remarks>This class can be accessed from the scripts that run in Unity Editor.</remarks>
/// <seealso cref="UIPrefabBaseScript"/>
public static class UnityPrefabController {
  /// <summary>All the registered prefab instances.</summary>
  static readonly Dictionary<Type, Dictionary<string, Component>> registeredPrefabs =
      new Dictionary<Type, Dictionary<string, Component>>();

  /// <summary>Creates instance from a registered prefab.</summary>
  /// <param name="newInstanceName">The name to assign to the new object.</param>
  /// <param name="parent">The parent of the new object.</param>
  /// <param name="prefabName">
  /// The prefab name. If omitted, then it's expected there is only one prefab for the type. If it's
  /// not the case, an exception is thrown.
  /// </param>
  /// <typeparam name="T">The component class to create prefab for.</typeparam>
  /// <returns>The new instance, created out of the prefab.</returns>
  /// <exception cref="ArgumentException">if the prefab cannot be found.</exception>
  /// <seealso cref="CreateInstance(Type,string,Transform,string)"/>
  /// <seealso cref="RegisterPrefab"/>
  public static T CreateInstance<T>(string newInstanceName, Transform parent,
                                    string prefabName = null) where T : Component {
    return CreateInstance(typeof(T), newInstanceName, parent, prefabName) as T;
  }

  /// <summary>Creates instance from a registered prefab.</summary>
  /// <param name="type">The component class to create prefab for.</param>
  /// <param name="newInstanceName">The name to assign to the new object.</param>
  /// <param name="parent">The parent of the new object.</param>
  /// <param name="prefabName">
  /// The prefab name. If omitted, then it's expected there is only one prefab for the type. If it's
  /// not the case, an exception is thrown.
  /// </param>
  /// <returns>The new instance, created out of the prefab.</returns>
  /// <exception cref="ArgumentException">if the prefab cannot be found.</exception>
  /// <seealso cref="CreateInstance&lt;T&gt;"/>
  /// <seealso cref="RegisterPrefab"/>
  public static Component CreateInstance(Type type, string newInstanceName, Transform parent,
                                         string prefabName = null) {
    var prefab = GetPrefab(type, prefabName: prefabName);
    var res = UnityEngine.Object.Instantiate(prefab, parent, worldPositionStays: false);
    res.name = newInstanceName;
    res.gameObject.SetActive(true);
    return res;
  }

  /// <summary>Adds prefab to the library.</summary>
  /// <param name="prefab">The perfab to add.</param>
  /// <param name="prefabName">The name, under which the prefab will be registered.</param>
  /// <returns>
  /// <c>true</c> if prefab was sucessfully added, or <c>false</c> thos perfab has alerady been
  /// added.
  /// </returns>
  /// <seealso cref="IsPrefabRegistered"/>
  /// <seealso cref="CreateInstance(Type,string,Transform,string)"/>
  /// <seealso cref="CreateInstance&lt;T&gt;"/>
  public static bool RegisterPrefab(Component prefab, string prefabName) {
    var prefabs = GetPrefabsForType(prefab.GetType(), failIfNotFound: false);
    if (prefabs.ContainsKey(prefabName)) {
      Debug.LogWarningFormat(
          "Prefab is already initalized: type={0}, name={1}", prefab.GetType(), prefabName);
      return false;
    }
    prefabs[prefabName] = prefab;
    Debug.LogFormat("Captured a prefab: type={0}, name={1}", prefab.GetType(), prefabName);
    return true;
  }

  /// <summary>Checks if prefab is already added.</summary>
  /// <param name="prefab">The prefab to check.</param>
  /// <param name="prefabName">The name, under which the prefab was registered.</param>
  /// <returns><c>true</c> if there is such prefab in the libarary.</returns>
  /// <seealso cref="RegisterPrefab"/>
  public static bool IsPrefabRegistered(Component prefab, string prefabName) {
    var prefabs = GetPrefabsForType(prefab.GetType(), failIfNotFound: false, makeIfNotFound: false);
    return prefabs != null && prefabs.ContainsKey(prefabName);
  }

  /// <summary>Returns prefab from the library.</summary>
  /// <param name="prefabName">
  /// The prefab name. If omitted, then it's expected there is only one prefab for the type. If it's
  /// not the case, an exception is thrown.
  /// </param>
  /// <typeparam name="T">The component class to create prefab for.</typeparam>
  /// <returns>The prefab instance.</returns>
  /// <exception cref="ArgumentException">if the prefab cannot be found.</exception>
  /// <seealso cref="RegisterPrefab"/>
  public static T GetPrefab<T>(string prefabName = null) where T : Component {
    return GetPrefab(typeof(T), prefabName) as T;
  }

  /// <summary>Returns prefab from the library.</summary>
  /// <param name="type">The component class to create prefab for.</param>
  /// <param name="prefabName">
  /// The prefab name. If omitted, then it's expected there is only one prefab for the type. If it's
  /// not the case, an exception is thrown.
  /// </param>
  /// <typeparam name="T">The component class to create prefab for.</typeparam>
  /// <returns>The prefab instance.</returns>
  /// <exception cref="ArgumentException">if the prefab cannot be found.</exception>
  /// <seealso cref="RegisterPrefab"/>
  public static Component GetPrefab(Type type, string prefabName = null) {
    var prefabs = GetPrefabsForType(type, failIfNotFound: true);
    Component prefab;
    if (prefabName == null) {
      if (prefabs.Count > 1) {
        throw new ArgumentException(string.Format(
          "Multiple prefabs found: type={0}, count={1}", type, prefabs.Keys.Count));
      }
      prefab = prefabs.Values.First();
    } else {
      if (!prefabs.TryGetValue(prefabName, out prefab)) {
        throw new ArgumentException(string.Format(
            "Prefab not found: type={0}, name={1}", type, prefabName));
      }
    }
    return prefab as Component;
  }

  #region Local utility methods
  static Dictionary<string, Component> GetPrefabsForType(
      Type prefabType, bool failIfNotFound = true, bool makeIfNotFound = true) {
    Dictionary<string, Component> prefabs;
    if (!registeredPrefabs.TryGetValue(prefabType, out prefabs)) {
      if (failIfNotFound) {
        throw new ArgumentException(
            string.Format("No prefabs found for type: {0}", prefabType));
      }
      if (!makeIfNotFound) {
        return null;
      }
      prefabs = new Dictionary<string, Component>();
      registeredPrefabs[prefabType] = prefabs;
    }
    return prefabs;
  }
  #endregion
}

}
