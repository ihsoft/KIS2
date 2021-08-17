// Kerbal Inventory System API v2
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.LogUtils;
using KSPDev.PartUtils;
using System.Collections.Generic;
using KSPDev.ModelUtils;
using UnityEngine;
using UnityEngine.Rendering;

// ReSharper disable once CheckNamespace
// ReSharper disable once IdentifierTypo
namespace KISAPIv2 {

public sealed class PartIconUtils {

  /// <summary>Local cache for the icons. Valid with the scene only.</summary>
  /// <remarks>
  /// When the scene switches, the cache are is reset but the items in it get destroyed. So, the values must always be
  /// checked for non-null.
  /// </remarks>
  static readonly Dictionary<string, Texture> IconsCache = new();

  /// <summary>
  /// Makes a sprite that represents the part in it's default icon state. This is what is shown in
  /// the editor when the part is not hovered.
  /// </summary>
  /// <param name="avPart">The part to make icon for.</param>
  /// <param name="variant">
  /// The variant to apply to the part before capturing an icon. It can be null if not variant needs
  /// to be applied.
  /// </param>
  /// <param name="resolution">
  /// The size of the sprite. The part icon is always square. If not set, then the best size will be
  /// picked based on the screen resolution.
  /// </param>
  /// <returns>The sprite of the icon.</returns>
  public Texture MakeDefaultIcon(AvailablePart avPart, PartVariant variant, int? resolution = null) {
    var iconSize = GetBestIconResolution(resolution);
    var cacheKey = avPart.name + "-" + iconSize + (variant == null ? "" : "-" + variant.Name);
    if (IconsCache.TryGetValue(cacheKey, out var result) && result != null) {
      return result;
    }
    DebugEx.Fine("Creating a new icon for: part={0}, variant={1}, cacheKey={2}",
                 avPart.name, (variant != null ? variant.Name : "N/A"), cacheKey);

    // There is some stock logic that KIS doesn't have a clue about. So, just warn and skip it. 
    if (CraftThumbnail.GetThumbNailSetupIface(avPart) != null) {
      DebugEx.Warning("Part implements IThumbnailSetup, but it's not supported in KIS: name={0}", avPart.name);
    }

    var snapshotSettings = KisApi.CommonConfig.iconIconSnapshotSettings;
    const int snapshotRenderLayer = (int) KspLayer2.DragRender;
    const int snapshotRenderMask = 1 << snapshotRenderLayer;

    // The camera that will take a snapshot.
    var cameraObj = new GameObject("KisPartSnapshotCamera");
    var camera = cameraObj.AddComponent<Camera>();
    camera.clearFlags = CameraClearFlags.Color;
    camera.backgroundColor = Color.clear;
    camera.fieldOfView = snapshotSettings.cameraFov;
    camera.cullingMask = snapshotRenderMask;
    camera.enabled = false;
    camera.orthographic = true;
    camera.orthographicSize = 0.75f;
    camera.allowHDR = false;

    // The "like in the editor" lighting.
    var sceneLightObj = new GameObject("KisPartSnapshotLight");
    sceneLightObj.transform.SetParent(cameraObj.transform, worldPositionStays: false);
    sceneLightObj.transform.localRotation = Quaternion.Euler(snapshotSettings.lightRotation);
    var sunLight = sceneLightObj.AddComponent<Light>();
    sunLight.cullingMask = snapshotRenderMask;
    sunLight.type = LightType.Directional;
    sunLight.intensity = snapshotSettings.lightIntensity;
    sunLight.shadows = LightShadows.Soft;
    sunLight.renderMode = LightRenderMode.ForcePixel;

    // The model that represents the part's icon.
    var iconPrefab = KisApi.PartModelUtils.GetIconPrefab(avPart, variant: variant);
    iconPrefab.layer = snapshotRenderLayer;
    iconPrefab.SetLayerRecursive(snapshotRenderLayer);
    var iconPrefabSize =
        PartGeometryUtil.MergeBounds(new[] { iconPrefab.GetRendererBounds() }, iconPrefab.transform.root).size;

    // Position the camera so that the whole part is in focus and rotated as in the stock logic. 
    var camDist = KSPCameraUtil.GetDistanceToFit(
        Mathf.Max(Mathf.Max(iconPrefabSize.x, iconPrefabSize.y), iconPrefabSize.z),
        snapshotSettings.cameraFov,
        iconSize);
    camera.transform.position =
        Quaternion.AngleAxis(snapshotSettings.cameraAzimuth, Vector3.up)
        * Quaternion.AngleAxis(snapshotSettings.cameraElevation, Vector3.right)
        * (Vector3.back * camDist);
    camera.transform.rotation =
        Quaternion.AngleAxis(snapshotSettings.cameraHeading, Vector3.up)
        * Quaternion.AngleAxis(snapshotSettings.cameraPitch, Vector3.right);

    // Render the icon using the provided ambient settings.
    Texture2D thumbTexture;
    using (new AmbientSettingsScope(logCapturedValues: true)) {
      RenderSettings.ambientMode = AmbientMode.Trilight;
      RenderSettings.ambientLight = snapshotSettings.ambientLightColor;
      RenderSettings.ambientEquatorColor = snapshotSettings.ambientEquatorColor;
      RenderSettings.ambientGroundColor = snapshotSettings.ambientGroundColor;
      RenderSettings.ambientSkyColor = snapshotSettings.ambientSkyColor;
      Shader.SetGlobalFloat(AmbientSettingsScope.AmbientBoostDiffuse, 0f);
      Shader.SetGlobalFloat(AmbientSettingsScope.AmbientBoostEmissive, GameSettings.AMBIENTLIGHT_BOOSTFACTOR_EDITONLY);
      thumbTexture = RenderCamera(camera, iconSize, iconSize, 24);
    }

    // Don't use GameObject.Destroy() method here!
    Hierarchy.SafeDestroy(cameraObj);
    Hierarchy.SafeDestroy(iconPrefab);
    Hierarchy.SafeDestroy(sceneLightObj);

    IconsCache[cacheKey] = thumbTexture;
    return thumbTexture;
  }

  /// <summary>
  /// Makes a sprite that represents the part in it's default icon state. This is what is shown in
  /// the editor when the part is not hovered.
  /// </summary>
  /// <param name="part">The part to make the icon for.</param>
  /// <param name="resolution">
  /// The size of the sprite. The part icon is always square. If not set, then the best size will be
  /// picked based on the screen resolution.
  /// </param>
  /// <returns>The sprite of the icon.</returns>
  public Texture MakeDefaultIcon(Part part, int? resolution = null) {
    return MakeDefaultIcon(part.partInfo, VariantsUtils.GetCurrentPartVariant(part), resolution: resolution);
  }

  #region Local utility methods
  /// <summary>Gives the best icon resolution if one is not explicitly set or doesn't make sense.</summary>
  /// <remarks>
  /// This method have an ultimate power to override the caller's preference. It tries to give a
  /// best resolution given a number of factors. The screen resolution is one of them.
  /// </remarks>
  /// <param name="requested">
  /// The caller's idea of the resolution. If not set, then KIS will choose the best one based on its settings.
  /// </param>
  /// <returns>The best resolution, given the current screen mode ands the settings.</returns>
  static int GetBestIconResolution(int? requested = null) {
    var res = requested ?? KisApi.CommonConfig.iconIconSnapshotSettings.baseIconResolution;
    return Screen.currentResolution.height <= 1080 ? res : res * 2;
  }

  /// <summary>Renders the camera's output into a texture.</summary>
  /// <param name="camera">The camera to render.</param>
  /// <param name="width">The width of the resulted texture.</param>
  /// <param name="height">The height of the resulted texture.</param>
  /// <param name="depth">The bit-depth of the resulted texture.</param>
  /// <returns>The texture created from the camera's output.</returns>
  static Texture2D RenderCamera(Camera camera, int width, int height, int depth) {
    var renderTexture =
        new RenderTexture(width, height, depth, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
    renderTexture.Create();
    var oldActive = RenderTexture.active;
    RenderTexture.active = renderTexture;
    camera.targetTexture = renderTexture;
    camera.Render();
    var texture = new Texture2D(width, height, TextureFormat.ARGB32, true);
    texture.ReadPixels(new Rect(0.0f, 0.0f, width, height), 0, 0, false);
    texture.Apply();
    RenderTexture.active = oldActive;
    camera.targetTexture = null;
    renderTexture.Release();
    Object.DestroyImmediate(renderTexture);
    return texture;
  }
  #endregion
}

}  // namespace
