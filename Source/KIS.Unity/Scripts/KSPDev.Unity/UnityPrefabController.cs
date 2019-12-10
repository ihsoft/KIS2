// Kerbal Development tools for Unity scripts.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KSPDev.Unity {

/// <summary>Utility class to manage prefabs in Unity.</summary>
/// <remarks>This class can be accessed from the scripts that run in Unity Editor.</remarks>
/// <seealso cref="UiPrefabBaseScript"/>
public static class UnityPrefabController {
  /// <summary>All the registered prefab instances.</summary>
  static readonly Dictionary<Type, Dictionary<string, Component>> RegisteredPrefabs =
      new Dictionary<Type, Dictionary<string, Component>>();

  /// <summary>Creates instance from a registered prefab.</summary>
  /// <param name="newInstanceName">The name to assign to the new object.</param>
  /// <param name="parent">The parent of the new object.</param>
  /// <param name="prefabName">
  /// The prefab name. If omitted, then it's expected there is only one prefab for the type. If it's
  /// not the case, an exception is thrown.
  /// </param>
  /// <typeparam name="T">
  /// The component class to create prefab for. If it's not a public class, then the first public
  /// parent will be picked up from the hierarchy. 
  /// </typeparam>
  /// <returns>The new instance, created out of the prefab.</returns>
  /// <exception cref="ArgumentException">if the prefab cannot be found.</exception>
  /// <seealso cref="CreateInstance(Type,string,Transform,string)"/>
  /// <seealso cref="RegisterPrefab"/>
  public static T CreateInstance<T>(string newInstanceName, Transform parent,
                                    string prefabName = null) where T : Component {
    return CreateInstance(
        GetFirstPublicParentType(typeof(T)), newInstanceName, parent, prefabName) as T;
  }

  /// <summary>Creates instance from a registered prefab.</summary>
  /// <param name="type">
  /// The component class to create prefab for. If it's not a public class, then the first public
  /// parent will be found in the hierarchy.
  /// </param>
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
    var prefab = GetPrefab(GetFirstPublicParentType(type), prefabName: prefabName);
    var res = UnityEngine.Object.Instantiate(prefab, parent, worldPositionStays: false);
    res.name = newInstanceName;
    res.gameObject.SetActive(true);
    return res;
  }

  /// <summary>Adds prefab to the library.</summary>
  /// <remarks>
  /// The class being registered must be <i>public</i>. If it's not, this method tries to find the
  /// first public class in the hierarchy. The one found will be registered. If none was found, an
  /// error will be returned.
  /// </remarks>
  /// <param name="prefab">
  /// The prefab to add. If it's not a public class, then the first public parent will be found in
  /// the hierarchy, and this class will be registered as prefab.
  /// </param>
  /// <param name="prefabName">The name, under which the prefab will be registered.</param>
  /// <returns>
  /// <c>true</c> if prefab was successfully added, or <c>false</c> if prefab has already been
  /// added.
  /// </returns>
  /// <seealso cref="IsPrefabRegistered"/>
  /// <seealso cref="CreateInstance(Type,string,Transform,string)"/>
  /// <seealso cref="CreateInstance&lt;T&gt;"/>
  public static bool RegisterPrefab(Component prefab, string prefabName) {
    var prefabType = prefab.GetType();
    if (!prefabType.IsPublic) {
      var publicType = GetFirstPublicParentType(prefabType);
      Debug.LogFormat("Not registering non-public type: type={0}, publicType={1}",
                      prefabType, publicType);
      prefabType = publicType;
    }
    if (prefabType == null) {
      Debug.LogErrorFormat("None of the prefab parents is a public class: {0}", prefab.GetType());
      return false;
    }
    var prefabs = GetPrefabsForType(prefab.GetType(), failIfNotFound: false);
    if (prefabs.ContainsKey(prefabName)) {
      Debug.LogWarningFormat(
          "Prefab is already initialized: type={0}, name={1}", prefabType, prefabName);
      return false;
    }
    prefabs[prefabName] = prefab;
    Debug.LogFormat("Captured a prefab: type={0}, name={1}", prefabType, prefabName);
    return true;
  }

  /// <summary>Checks if prefab is already added.</summary>
  /// <param name="prefab">
  /// The prefab to check. If it's not a public class, then the first public
  /// parent will be used from the hierarchy.
  /// </param>
  /// <param name="prefabName">The name, under which the prefab was registered.</param>
  /// <returns><c>true</c> if there is such prefab in the library.</returns>
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
  /// <typeparam name="T">
  /// The component class to create prefab for. If it's not a public class, then the first public
  /// parent will be picked from the hierarchy.
  /// </typeparam>
  /// <returns>The prefab instance.</returns>
  /// <exception cref="ArgumentException">if the prefab cannot be found.</exception>
  /// <seealso cref="RegisterPrefab"/>
  public static T GetPrefab<T>(string prefabName = null) where T : Component {
    return GetPrefab(GetFirstPublicParentType(typeof(T)), prefabName) as T;
  }

  /// <summary>Returns prefab from the library.</summary>
  /// <param name="type">
  /// The component class to create prefab for. If it's not a public class, then the first public
  /// parent will be picked from the hierarchy.
  /// </param>
  /// <param name="prefabName">
  /// The prefab name. If omitted, then it's expected there is only one prefab for the type. If it's
  /// not the case, an exception is thrown.
  /// </param>
  /// <returns>The prefab instance.</returns>
  /// <exception cref="ArgumentException">if the prefab cannot be found.</exception>
  /// <seealso cref="RegisterPrefab"/>
  public static Component GetPrefab(Type type, string prefabName = null) {
    var prefabs = GetPrefabsForType(type);
    Component prefab;
    if (prefabName == null) {
      if (prefabs.Count > 1) {
        throw new ArgumentException(
            $"Multiple prefabs found: type={type}, count={prefabs.Keys.Count}");
      }
      prefab = prefabs.Values.First();
    } else {
      if (!prefabs.TryGetValue(prefabName, out prefab)) {
        throw new ArgumentException($"Prefab not found: type={type}, name={prefabName}");
      }
    }
    return prefab;
  }

  #region Local utility methods
  /// <summary>Gets all prefabs known to this type.</summary>
  /// <param name="prefabType">The type to get available prefabs.</param>
  /// <param name="failIfNotFound">If <c>true</c>, then throw on a not found prefab.</param>
  /// <param name="makeIfNotFound"></param>
  /// <returns></returns>
  /// <exception cref="ArgumentException"></exception>
  static Dictionary<string, Component> GetPrefabsForType(
      Type prefabType, bool failIfNotFound = true, bool makeIfNotFound = true) {
    prefabType = GetFirstPublicParentType(prefabType);
    Dictionary<string, Component> prefabs;
    if (!RegisteredPrefabs.TryGetValue(prefabType, out prefabs)) {
      if (failIfNotFound) {
        throw new ArgumentException($"No prefabs found for type: {prefabType}");
      }
      if (!makeIfNotFound) {
        return null;
      }
      prefabs = new Dictionary<string, Component>();
      RegisteredPrefabs[prefabType] = prefabs;
    }
    return prefabs;
  }

  /// <summary>Finds and returns the first public class in the hierarchy.</summary>
  /// <param name="type">The type to start searching from.</param>
  /// <returns>The public parent or <c>null</c> if nothing found.</returns>
  static Type GetFirstPublicParentType(Type type) {
    var res = type;
    if (res.IsPublic) {
      return res;
    }
    while (res != null && !res.IsPublic) {
      res = res.BaseType;
    }
    return res;
  }
  #endregion
}

}
