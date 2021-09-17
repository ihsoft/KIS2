// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.ModelUtils;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KISAPIv2 {

/// <summary>Various methods to deal with the part model.</summary>
public class PartModelUtilsImpl {

  #region Local fields and properties
  /// <summary>Cached volumes of the parts.</summary>
  /// <remarks>
  /// They are accumulated as the KIS containers are being used. The cache only lives during the game's session and dies
  /// on the game restart.
  /// </remarks>
  readonly Dictionary<string, double> _partsVolumeCache = new();
  #endregion

  #region API methods
  /// <summary>Returns the part's model, used to make the preview icon.</summary>
  /// <remarks>
  /// Note, that this is not the actual part appearance. It's an optimized version, specifically made for the icon
  /// preview. In particular, the model is scaled to fit the icon's constrains.
  /// </remarks>
  /// <param name="avPart">The part proto to get the model from.</param>
  /// <param name="variant">
  /// The part's variant to apply. If <c>null</c>, then variant will be extracted from <paramref name="partNode"/>.
  /// </param>
  /// <param name="partNode">
  /// The part's persistent state. It's used to extract the part's variant. It can be <c>null</c>.
  /// </param>
  /// <returns>The model of the part. Don't forget to destroy it when not needed.</returns>
  public GameObject GetIconPrefab(AvailablePart avPart, PartVariant variant = null, ConfigNode partNode = null) {
    var iconPrefab = UnityEngine.Object.Instantiate(avPart.iconPrefab);
    iconPrefab.SetActive(true);
    if (variant == null && partNode != null) {
      variant = VariantsUtils.GetCurrentPartVariant(avPart, partNode);
    }
    var materials = Shaders.CreateMaterialArray(iconPrefab);
    if (variant != null) {
      DebugEx.Fine("Applying variant to the iconPrefab: part={0}, variant={1}", avPart.name, variant.Name);
      ModulePartVariants.ApplyVariant(null, iconPrefab.transform, variant, materials, skipShader: false);
    }
    Shaders.FixScreenSpaceShaders(materials, logDifferences: true);
    return iconPrefab;
  }

  /// <summary>Returns the part's model.</summary>
  /// <remarks>The returned model is a copy from the part prefab.</remarks>
  /// <param name="avPart">The part proto to get the model from.</param>
  /// <param name="variant">
  /// The part's variant to apply. If <c>null</c>, then variant will be extracted from
  /// <paramref name="partNode"/>.
  /// </param>
  /// <param name="partNode">
  /// The part's persistent state. It's used to extract the external scale modifiers and part's
  /// variant. It can be <c>null</c>.
  /// </param>
  /// <returns>The model of the part. Don't forget to destroy it when not needed.</returns>
  public GameObject GetPartModel(
      AvailablePart avPart,
      PartVariant variant = null, ConfigNode partNode = null) {
    if (variant == null && partNode != null) {
      variant = VariantsUtils.GetCurrentPartVariant(avPart, partNode);
    }
    GameObject modelObj = null;
    VariantsUtils.ExecuteAtPartVariant(avPart, variant, p => {
      var partPrefabModel = Hierarchy.GetPartModelTransform(p).gameObject;
      modelObj = UnityEngine.Object.Instantiate(partPrefabModel);
      modelObj.SetActive(true);
    });

    // Handle TweakScale settings.
    if (partNode != null) {
      var scale = KisApi.PartNodeUtils.GetTweakScaleSizeModifier(partNode);
      if (Math.Abs(1.0 - scale) > double.Epsilon) {
        DebugEx.Fine("Applying TweakScale size modifier: {0}", scale);
        var scaleRoot = new GameObject("TweakScale");
        scaleRoot.transform.localScale = new Vector3((float) scale, (float) scale, (float) scale);
        modelObj.transform.SetParent(scaleRoot.transform, worldPositionStays: false);
        modelObj = scaleRoot;
      }
    }
    
    return modelObj;
  }

  /// <summary>Collects all the models in the part or hierarchy.</summary>
  /// <remarks>
  /// The result of this method only includes meshes and renderers. Any colliders, animations or
  /// effects will be dropped.
  /// <para>
  /// Note, that this method captures the current model state fro the part, which may be affected
  /// by animations or third-party mods. That said, each call for the same part may return different
  /// results.
  /// </para>
  /// </remarks>
  /// <param name="rootPart">The part to start scanning the assembly from.</param>
  /// <param name="goThruChildren">
  /// Tells if the parts down the hierarchy need to be captured too.
  /// </param>
  /// <returns>
  /// The root game object of the new hierarchy. This object must be explicitly disposed when not
  /// needed anymore.
  /// </returns>
  public GameObject GetSceneAssemblyModel(Part rootPart, bool goThruChildren = true) {
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
      if (!(component is Renderer || component is MeshFilter)) {
        UnityEngine.Object.DestroyImmediate(component);
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

    if (goThruChildren) {
      foreach (var childPart in rootPart.children) {
        var childObj = GetSceneAssemblyModel(childPart);
        childObj.transform.parent = modelObj.transform;
        childObj.transform.localRotation =
            rootPart.transform.rotation.Inverse() * childPart.transform.rotation;
        childObj.transform.localPosition =
            rootPart.transform.InverseTransformPoint(childPart.transform.position);
      }
    }
    return modelObj;
  }

  /// <summary>Returns part's volume basing on its geometric.</summary>
  /// <remarks>
  /// The volume is calculated basing on the smallest boundary box that encapsulates all the meshes
  /// in the part. The deployable parts can take much more space in the deployed state.
  /// </remarks>
  /// <param name="part">The actual part, that exists in the scene.</param>
  /// <returns>The volume in liters.</returns>
  /// FIXME: This method should capture real part bounds/volume
  public double GetPartVolume(Part part) {
    var partNode = KisApi.PartNodeUtils.GetConfigNode(part);
    return GetPartVolume(part.partInfo, partNode: partNode);
  }

  /// <summary>Returns a part's volume.</summary>
  /// <remarks>
  /// The volume is either get from the <c>ModuleCargoPart</c> or from the smallest boundary box that encapsulates all
  /// the meshes in the part. See <see cref="KisApi.CommonConfig.compatibilitySettings.stockVolumeExceptions"/>.
  /// </remarks>
  /// <param name="avPart">The part proto to get the models from.</param>
  /// <param name="variant">
  /// The part's variant. If it's <c>null</c>, then the variant will be attempted to read from
  /// <paramref name="partNode"/>. If both the <paramref name="variant"/> and the
  /// <paramref name="partNode"/> are specified, then the variant argument will be considered.  
  /// </param>
  /// <param name="partNode">
  /// The part's persistent config. It will be looked up for the variant and other volume modifiers.
  /// </param>
  /// <returns>The volume in liters.</returns>
  public double GetPartVolume(AvailablePart avPart, PartVariant variant = null, ConfigNode partNode = null) {
    if (variant != null && partNode != null) {
      variant = VariantsUtils.GetCurrentPartVariant(avPart, partNode);
    }
    var cacheKey = avPart.name + (variant != null ? "-" + avPart.Variants.IndexOf(variant) : "");
    if (_partsVolumeCache.TryGetValue(cacheKey, out var cachedVolume)) {
      return cachedVolume;
    }
    if (!KisApi.CommonConfig.compatibilitySettings.stockVolumeExceptions.Contains(avPart.name)) {
      var cargoModule = KisApi.PartPrefabUtils.GetCargoModule(avPart);
      if (cargoModule != null) {
        DebugEx.Info("Put cargo module volume into the cache: partName={0}, volume={1}, cacheKey={2}",
                     avPart.name, cargoModule.packedVolume, cacheKey);
        _partsVolumeCache.Add(cacheKey, cargoModule.packedVolume);
        return cargoModule.packedVolume;
      }
    }

    var boundsSize = GetPartBounds(avPart, variant: variant, partNode: partNode);
    var volume = boundsSize.x * boundsSize.y * boundsSize.z * 1000f;
    DebugEx.Info("No cargo module volume for the part, make a KIS one: partName={0}, kisVolume={1}, cacheKey={2}",
                 avPart.name, volume, cacheKey);
    _partsVolumeCache.Add(cacheKey, volume);
    return volume;
  }

  /// <summary>Returns part's boundary box basing on its geometrics.</summary>
  /// <remarks>The size is calculated from the part prefab model.</remarks>
  /// <param name="avPart">The part proto to get the models from.</param>
  /// <param name="variant">
  /// The part's variant. If it's <c>null</c>, then the variant will be attempted to read from
  /// <paramref name="partNode"/>.
  /// </param>
  /// <param name="partNode">
  /// The part's persistent config. It will be looked up for the variant if it's not specified.
  /// </param>
  /// <returns>The volume in liters.</returns>
  public Vector3 GetPartBounds(AvailablePart avPart, PartVariant variant = null, ConfigNode partNode = null) {
//    var itemModule = avPart.partPrefab.Modules.OfType<KIS.ModuleKISItem>().FirstOrDefault();
//    if (itemModule != null && itemModule.volumeOverride > 0) {
//      return itemModule.volumeOverride  // Ignore geometry.
//          * KisApi.PartNodeUtils.GetTweakScaleSizeModifier(partNode);  // But respect TweakScale.
//    }
    var bounds = default(Bounds);
    if (variant == null && partNode != null) {
      variant = VariantsUtils.GetCurrentPartVariant(avPart, partNode);
    }
    VariantsUtils.ExecuteAtPartVariant(avPart, variant, p => {
      var partModel = GetSceneAssemblyModel(p).transform;
      bounds.Encapsulate(GetMeshBounds(partModel));
      UnityEngine.Object.DestroyImmediate(partModel.gameObject);
    });
    return bounds.size;
  }

  /// <summary>Returns a bounds box from the render models.</summary>
  /// <param name="part">The part to get bounds for.</param>
  /// <returns>The bounds.</returns>
  public Bounds GetPartBounds(Part part) {
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
  public void CollectMeshesFromModel(Transform model,
                                     ICollection<CombineInstance> meshCombines,
                                     Matrix4x4? worldTransform = null,
                                     bool considerInactive = false) {
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
        var localToWorldMatrix = Matrix4x4.TRS(
            skinnedMeshRenderer.transform.position,
            skinnedMeshRenderer.transform.rotation,
            Vector3.one);
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
  Bounds GetMeshBounds(Transform model, bool considerInactive = false) {
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
