// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using KSPDev.LogUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KSPDev.ModelUtils {

/// <summary>Utility class to load Unity assets and bundles.</summary>
public static class PrefabLoader {
  static readonly Dictionary<string, AssetBundle> loadedBundles =
      new Dictionary<string, AssetBundle>();

  /// <summary>Loads an asset from the specified bundle.</summary>
  /// <remarks>
  /// The loaded bundles are cached, so multiple calls for the same bundle won't impact performance
  /// or cause errors.
  /// </remarks>
  /// <typeparam name="T">
  /// The type of component to retrieve from the prefab object. If it's <c>GameObject</c>, then the
  /// prefab itself is returned.
  /// </typeparam>
  /// <param name="bundlePath">The full path to the bundle.</param>
  /// <param name="prefabName">
  /// The prefab name. It can be a <c>GameObject</c> name, or it can be a full Unity path to the
  /// object, which lets avoiding name collisions.
  /// </param>
  /// <param name="initPrefab">
  /// If <c>true</c>, then method <c>InitPrefab</c> will be attempted to invoke on
  /// <i>all the components</i> of the loaded prefab. The method can be instance or static, but it
  /// must be <i>public</i>. If the method is absent, it's not an error. Use this ability if Unity
  /// prefabs need some setup before being exposed to the game.
  /// </param>
  /// <returns>
  /// The component of type <typeparamref name="T"/>. Can be <c>null</c> if the prefab cannot be
  /// found or the bundle cannot be loaded.
  /// </returns>
  /// <seealso cref="GetOrLoadBundle"/>
  public static T LoadAsset<T>(
      string bundlePath, string prefabName, bool initPrefab = false) where T : class {
    var bundle = GetOrLoadBundle(bundlePath);
    if (bundle == null) {
      return null;  // Bummer.
    }
    var obj = bundle.LoadAsset<GameObject>(prefabName);
    if (obj == null) {
      DebugEx.Error("Cannot find prefab in bundle: bundlePath={0}, prefabName={1}",
                    bundlePath, prefabName);
      return null;
    }
    DebugEx.Fine("Loaded prefab: bundlePath={0}, prefabName={1}", bundlePath, prefabName);
    if (initPrefab) {
      var prefabs = obj.GetComponents<Component>()
          .Select(c => new {
              init = c.GetType().GetMethod("InitPrefab", new Type[0]),
              comp = c
          })
          .Where(m => m.init != null)
          .ToArray();
      if (prefabs.Length > 0) {
        DebugEx.Fine("[{0}] Running {1} prefab initializers...", prefabName, prefabs.Length);
        foreach (var prefab in prefabs) {
          DebugEx.Info("Initializing prefab: {0}...", prefab.comp.GetType());
          prefab.init.Invoke(prefab.comp, new object[0]);
        }
        DebugEx.Fine("[{0}] Prefab initializiation complete", prefabName);
      } else {
        DebugEx.Fine("[{0}] No prefab initializers found", prefabName);
      }
    }
    
    if (typeof(T) == typeof(GameObject)) {
      return obj as T;
    }
    var component = obj.GetComponent<T>();
    if (obj == null) {
      DebugEx.Error(
          "Cannot find component on prefab: bundlePath={0}, prefabName={1}, component={2}",
          bundlePath, prefabName, typeof(T));
      return null;
    }
    
    return component;
  }

  /// <summary>Loads and initializes all the assets from the bundle file.</summary>
  /// <remarks>
  /// This method extracts all prefabs from teh asset file and makes them availabe to the game. It
  /// also looks for method <c>InitPrefab()</c> thru all the components of at the root prefab object,
  /// and invokes it if found. This method can be implemented by the components to bring the prefab
  /// into a "default" state.
  /// <para>
  /// Every asset bundle is loaded exactly one time and cached. Repetative calls for the same bundle
  /// will be NO-OP.  
  /// </para>
  /// </remarks>
  /// <returns><c>true</c> if bundle has been successfully loaded or found in cache.</returns>
  /// <param name="bundlePath">The full path to the bundle.</param>
  /// <seealso cref="GetOrLoadBundle"/>
  public static bool LoadAllAssets(string bundlePath) {
    var bundle = GetOrLoadBundle(bundlePath);
    if (bundle == null) {
      return false;  // Bummer. And the errors are already logged.
    }
    var assets = bundle.LoadAllAssets();
    foreach (var asset in assets) {
      DebugEx.Fine("Loaded asset: {0}", asset.name);
      var prefabs = (asset as GameObject).GetComponents<Component>()
          .Select(c => new {
              init = c.GetType().GetMethod("InitPrefab", new Type[0]),
              component = c
          })
          .Where(m => m.init != null);
      foreach (var prefab in prefabs) {
        prefab.init.Invoke(prefab.component, new object[0]);
      }
    }
    return true;
  }

  /// <summary>Returns a bundle either from the cache or loads it.</summary>
  /// <param name="bundlePath">The full path to the bundle.</param>
  /// <returns>The asset bundle or <c>null</c> if cannot be loaded.</returns>
  /// <seealso cref="LoadAsset"/>
  /// <seealso cref="GetCachedBundle"/>
  public static AssetBundle GetOrLoadBundle(string bundlePath) {
    var bundle = GetCachedBundle(bundlePath);
    if (bundle == null) {
      bundle = AssetBundle.LoadFromFile(bundlePath);
      if (bundle != null) {
        DebugEx.Fine("Loaded bundle: {0}", bundlePath);
      } else {
        DebugEx.Error("Cannot load asset bundle from: {0}", bundlePath);
        return null;
      }
      loadedBundles.Add(bundlePath, bundle);
    }
    return bundle;
  }

  /// <summary>Lookups the bundle in the cache.</summary>
  /// <remarks>Only bundles loaded thru KSPDev are considered.</remarks>
  /// <param name="bundlePath">The full path to the bundle.</param>
  /// <returns>The asset bundle or <c>null</c> if none found in the cache.</returns>
  /// <seealso cref="GetOrLoadBundle"/>
  public static AssetBundle GetCachedBundle(string bundlePath) {
    AssetBundle bundle = null;
    loadedBundles.TryGetValue(bundlePath, out bundle);
    return bundle;
  }
}

}  // namespace
