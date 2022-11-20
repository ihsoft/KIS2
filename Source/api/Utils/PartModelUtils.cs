// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.ModelUtils;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using KIS2;
using UnityEngine;

// ReSharper disable once CheckNamespace
// ReSharper disable once IdentifierTypo
namespace KISAPIv2 {

/// <summary>Various methods to deal with the part model.</summary>
public static class PartModelUtils {

  #region Local fields and properties
  /// <summary>Cached volumes of the parts.</summary>
  /// <remarks>
  /// They are accumulated as the KIS containers are being used. The cache only lives during the game's session and dies
  /// on the game restart.
  /// </remarks>
  static readonly Dictionary<string, double> PartsVolumeCache = new();
  #endregion

  #region API methods
  /// <summary>Returns the part's model, used to make the preview icon.</summary>
  /// <remarks>
  /// Note, that this is not the actual part appearance. It's an optimized version, specifically made for the icon
  /// preview. In particular, the model is scaled to fit the icon's constrains.
  /// </remarks>
  /// <param name="avPart">The part proto to get the model from.</param>
  /// <param name="variantName">
  /// An optional variant name to apply to the model. If it's NULL, then the prefab's variant will be used (if any). If
  /// the part doesn't have any variant, then this parameter is simply ignored.
  /// </param>
  /// <returns>The model of the part. Don't forget to destroy it when not needed.</returns>
  public static GameObject GetIconPrefab(AvailablePart avPart, string variantName = null) {
    var iconPrefab = UnityEngine.Object.Instantiate(avPart.iconPrefab);
    iconPrefab.SetActive(true);
    var materials = Shaders.CreateMaterialArray(iconPrefab);
    variantName ??= VariantsUtils2.GetCurrentPartVariantName(avPart.partPrefab);
    var variant = VariantsUtils2.GetPartVariant(avPart, variantName);
    if (variant != null) {
      DebugEx.Fine("Applying variant to the iconPrefab: part={0}, variant={1}", avPart.name, variantName);
      ModulePartVariants.ApplyVariant(null, iconPrefab.transform, variant, materials, skipShader: false);
    }
    Shaders.FixScreenSpaceShaders(materials, logDifferences: true);
    return iconPrefab;
  }

  /// <summary>Collects all the models in the part or hierarchy.</summary>
  /// <remarks>
  /// The result of this method only includes meshes and renderers. Any colliders, animations or effects will be
  /// dropped.
  /// <para>
  /// Note, that this method captures the current model state fro the part, which may be affected by animations or
  /// third-party mods. That said, each call for the same part may return different results.
  /// </para>
  /// </remarks>
  /// <param name="rootPart">The part to start scanning the assembly from.</param>
  /// <param name="goThroughChildren">Tells if the parts down the hierarchy need to be captured too.</param>
  /// <param name="keepColliders">
  /// Indicates if the colliders should be kept in the model. All the colliders will be turned into triggers. If this
  /// options is "false", then the colliders will be dropped.
  /// </param>
  /// <returns>
  /// The root game object of the new hierarchy. This object must be explicitly disposed when not needed anymore.
  /// </returns>
  public static GameObject GetSceneAssemblyModel(
      Part rootPart, bool goThroughChildren = true, bool keepColliders = false) {
    var modelObj = UnityEngine.Object.Instantiate(Hierarchy.GetPartModelTransform(rootPart).gameObject);
    modelObj.SetActive(true);

    // Drop stuff that is not intended to show up in flight.
    PartLoader.StripComponent<MeshRenderer>(modelObj, "Icon_Hidden", true);
    PartLoader.StripComponent<MeshFilter>(modelObj, "Icon_Hidden", true);
    PartLoader.StripComponent<SkinnedMeshRenderer>(modelObj, "Icon_Hidden", true);

    // Strip anything that is not mesh related.
    var joints = new List<Joint>();
    var rbs = new List<Rigidbody>();
    foreach (var component in modelObj.GetComponentsInChildren(typeof(Component))) {
      if (component is Transform) {
        continue;  // Transforms belong to the GameObject.
      }
      var rb = component as Rigidbody;
      if (rb != null) {
        rbs.Add(rb);
        continue;  // It can be tied with a joint, which must be deleted first.
      }
      var joint = component as Joint;
      if (joint != null) {
        joints.Add(joint);
        continue;  // They must be handled before the connected RBs handled.
      }
      if (component is Renderer or MeshFilter) {
        continue;
      }
      if (component is not Collider collider || !keepColliders) {
        UnityEngine.Object.DestroyImmediate(component);
      } else {
        collider.isTrigger = true;
      }
    }
    // Drop joints before rigidbodies.
    foreach (var joint in joints) {
      UnityEngine.Object.DestroyImmediate(joint);
    }
    // Drop rigidbodies once it's safe to do so.
    foreach (var rb in rbs) {
      UnityEngine.Object.DestroyImmediate(rb);
    }

    if (goThroughChildren) {
      foreach (var childPart in rootPart.children) {
        var childPartTransform = childPart.transform;
        var childModel = GetSceneAssemblyModel(childPart, goThroughChildren: true).transform;
        childModel.SetParent(modelObj.transform, worldPositionStays: false);
        childModel.localRotation = rootPart.transform.rotation.Inverse() * childPartTransform.rotation;
        childModel.localPosition = rootPart.transform.InverseTransformPoint(childPartTransform.position);
      }
    }

    return modelObj;
  }

  /// <summary>Returns a part's volume.</summary>
  /// <remarks>
  /// <p>
  /// The volume is either get from the <c>ModuleCargoPart</c> or from the smallest boundary box that encapsulates all
  /// the meshes in the part. See <see cref="StockCompatibilitySettings.stockVolumeExceptions"/>.
  /// </p>
  /// <p>
  /// The values are cached, so it's OK to call this method at high frequency. However, it also means that once
  /// calculated, the volume value will stay till the end of the game. 
  /// </p>
  /// </remarks>
  /// <param name="avPart">The part info to get the models from.</param>
  /// <param name="variantName">
  /// An optional part variant name. If it's NULL, then the base variant will be applied on the part before obtaining
  /// its volume.  
  /// </param>
  /// <returns>The volume in liters.</returns>
  public static double GetPartVolume(AvailablePart avPart, string variantName = null) {
    var cacheKey = avPart.name + (variantName != null ? "-" + variantName : "");
    if (PartsVolumeCache.TryGetValue(cacheKey, out var cachedVolume)) {
      return cachedVolume;
    }
    if (!StockCompatibilitySettings.stockVolumeExceptions.Contains(avPart.name)) {
      var cargoModule = PartPrefabUtils.GetCargoModule(avPart);
      if (cargoModule != null) {
        DebugEx.Info("Put cargo module volume into the cache: partName={0}, volume={1}, cacheKey={2}",
                     avPart.name, cargoModule.packedVolume, cacheKey);
        PartsVolumeCache.Add(cacheKey, cargoModule.packedVolume);
        return cargoModule.packedVolume;
      }
    }

    var boundsSize = GetPartBounds(avPart, variantName);
    var volume = boundsSize.x * boundsSize.y * boundsSize.z * 1000f;
    DebugEx.Info("No cargo module volume for the part, make a KIS one: partName={0}, kisVolume={1}, cacheKey={2}",
                 avPart.name, volume, cacheKey);
    PartsVolumeCache.Add(cacheKey, volume);
    return volume;
  }

  /// <summary>Returns part's boundary box basing on its geometrics.</summary>
  /// <remarks>The size is calculated from the part prefab model.</remarks>
  /// <param name="avPart">The part proto to get the models from.</param>
  /// <param name="variantName">
  /// An optional part variant name. If it's NULL, then the base variant will be applied on the part before obtaining
  /// its bounds.  
  /// </param>
  /// <returns>The bounds in metres.</returns>
  public static Vector3 GetPartBounds(AvailablePart avPart, string variantName = null) {
    var bounds = default(Bounds);
    VariantsUtils2.ExecuteAtPartVariant(avPart, variantName, p => {
      var partModel = GetSceneAssemblyModel(p).transform;
      bounds.Encapsulate(GetMeshBounds(partModel));
      UnityEngine.Object.DestroyImmediate(partModel.gameObject);
    });
    return bounds.size;
  }

  /// <summary>Returns a bounds box from the render models.</summary>
  /// <param name="part">The part to get bounds for.</param>
  /// <returns>The bounds in metres.</returns>
  public static Bounds GetPartBounds(Part part) {
    var partModel = GetSceneAssemblyModel(part).transform;
    var bounds = GetMeshBounds(partModel);
    UnityEngine.Object.DestroyImmediate(partModel.gameObject);
    return bounds;
  }

  /// <summary>Traverses through the hierarchy and gathers all the meshes from it.</summary>
  /// <param name="model">The root model to start from.</param>
  /// <param name="meshCombines">The collection to accumulate the meshes.</param>
  /// <param name="worldTransform">
  /// The optional world matrix to apply to the mesh. If not set, then the models world's matrix
  /// will be taken.
  /// </param>
  /// <param name="considerInactive">Tells if the inactive objects must be checked as well.</param>
  public static void CollectMeshesFromModel(Transform model, ICollection<CombineInstance> meshCombines,
                                            Matrix4x4? worldTransform = null, bool considerInactive = false) {
    // Always use world transformation from the root.
    var rootWorldTransform = worldTransform ?? model.localToWorldMatrix.inverse;

    // Get all meshes from the part's model.
    var meshFilters = model
        .GetComponentsInChildren<MeshFilter>()
        // Prefab models are always inactive, so ignore the check.
        .Where(mf => considerInactive || mf.gameObject.activeInHierarchy)
        .ToArray();
    Array.ForEach(meshFilters, meshFilter => {
      var combine = new CombineInstance {
          mesh = meshFilter.sharedMesh,
          transform = rootWorldTransform * meshFilter.transform.localToWorldMatrix
      };
      meshCombines.Add(combine);
    });

    // Skinned meshes are baked on every frame before rendering.
    var skinnedMeshRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
    if (skinnedMeshRenderers.Length > 0) {
      foreach (var skinnedMeshRenderer in skinnedMeshRenderers) {
        var combine = new CombineInstance {
            mesh = new Mesh(),
        };
        skinnedMeshRenderer.BakeMesh(combine.mesh);
        // BakeMesh() gives mesh in world scale, so don't apply it twice.
        var transform = skinnedMeshRenderer.transform;
        var localToWorldMatrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        combine.transform = rootWorldTransform * localToWorldMatrix;
        meshCombines.Add(combine);
      }
    }
  }
  #endregion

  #region Local utility methods
  /// <summary>Calculates bounds from the actual meshes of the model.</summary>
  /// <remarks>Note that the result depends on the model orientation.</remarks>
  /// <param name="model">The model to find the bounds for.</param>
  /// <param name="considerInactive">Tells if inactive meshes should be considered.</param>
  /// <returns>The estimated volume of the meshes.</returns>
  static Bounds GetMeshBounds(Transform model, bool considerInactive = false) {
    var combines = new List<CombineInstance>();
    CollectMeshesFromModel(model, combines, considerInactive: considerInactive);
    var bounds = default(Bounds);
    foreach (var combine in combines) {
      var mesh = new Mesh();
      mesh.CombineMeshes(new[] { combine });
      bounds.Encapsulate(mesh.bounds);
    }
    return bounds;
  }
  #endregion
}

}  // namespace
