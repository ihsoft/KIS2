﻿// Kerbal Inventory System API v2
// API design and implementation: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.LogUtils;
using KSPDev.PartUtils;
using KSPDev.ModelUtils;
using UnityEngine;
using UnityEngine.Rendering;

// ReSharper disable once CheckNamespace
// ReSharper disable once IdentifierTypo
namespace KISAPIv2 {

public static class PartIconUtils {

  /// <summary>
  /// Makes a sprite that represents the part in it's default icon state. This is what is shown in
  /// the editor when the part is not hovered.
  /// </summary>
  /// <param name="avPart">The part to make icon for.</param>
  /// <param name="variantName">
  /// The name of the variant to apply. It's ignored for the parts without variants, but otherwise it must be a valid
  /// name.
  /// </param>
  /// <returns>The sprite of the icon.</returns>
  public static Texture MakeDefaultIcon(AvailablePart avPart, string variantName) {
    string partIconTexturePath = null;
    VariantsUtils2.ExecuteAtPartVariant(avPart, variantName, part => {
      partIconTexturePath = CraftThumbnail.GetPartIconTexturePath(part, out _);
    });
    if (string.IsNullOrEmpty(partIconTexturePath)) {
      partIconTexturePath = "kisIcon-" + avPart.name + (variantName == "" ? "" : "-" + variantName);
    }
    var gameIcon = GameDatabase.Instance.GetTexture(partIconTexturePath, asNormalMap: false);
    if (gameIcon != null) {
      return gameIcon;
    }
    DebugEx.Fine("Making icon texture for part: path={0}", partIconTexturePath);

    // There is some stock logic that KIS doesn't have a clue about. So, just warn and skip it. 
    if (CraftThumbnail.GetThumbNailSetupIface(avPart) != null) {
      DebugEx.Warning("Part implements IThumbnailSetup, but it's not supported in KIS: name={0}", avPart.name);
    }

    var snapshotSettings = CommonConfig.iconIconSnapshotSettings;
    const int snapshotRenderLayer = (int) KspLayer2.DragRender;
    const int snapshotRenderMask = 1 << snapshotRenderLayer;

    // The camera that will take a snapshot.
    var cameraObj = new GameObject("KisPartSnapshotCamera");
    var camera = cameraObj.AddComponent<Camera>();
    camera.clearFlags = CameraClearFlags.Color;
    camera.backgroundColor = Color.clear;
    camera.cullingMask = snapshotRenderMask;
    camera.enabled = false;
    camera.orthographic = true;
    camera.orthographicSize = snapshotSettings.cameraOrthoSize;
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
    var iconPrefab = PartModelUtils.GetIconPrefab(avPart, variantName);
    iconPrefab.layer = snapshotRenderLayer;
    iconPrefab.SetLayerRecursive(snapshotRenderLayer);
    camera.transform.position =
        Quaternion.AngleAxis(snapshotSettings.cameraAzimuth, Vector3.up)
        * Quaternion.AngleAxis(snapshotSettings.cameraElevation, Vector3.right)
        * (Vector3.back * snapshotSettings.cameraDistance);
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
      thumbTexture = RenderCamera(camera, snapshotSettings.baseIconResolution, snapshotSettings.baseIconResolution, 24);
    }

    // Don't use GameObject.Destroy() method here!
    Hierarchy.SafeDestroy(cameraObj);
    Hierarchy.SafeDestroy(iconPrefab);
    Hierarchy.SafeDestroy(sceneLightObj);

    var texInfo = new GameDatabase.TextureInfo(null, thumbTexture,
                                               isNormalMap: false, isReadable: false, isCompressed: true) {
        name = partIconTexturePath,
    };
    GameDatabase.Instance.databaseTexture.Add(texInfo);

    return thumbTexture;
  }

  /// <summary>
  /// Makes a sprite that represents the part in it's default icon state. This is what is shown in
  /// the editor when the part is not hovered.
  /// </summary>
  /// <param name="part">The part to make the icon for.</param>
  /// <returns>The sprite of the icon.</returns>
  public static Texture MakeDefaultIcon(Part part) {
    return MakeDefaultIcon(part.partInfo, VariantsUtils2.GetCurrentPartVariantName(part));
  }

  #region Local utility methods
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
    var outTexture = new Texture2D(width, height, TextureFormat.ARGB32, mipChain: false);
    outTexture.ReadPixels(new Rect(0.0f, 0.0f, width, height), 0, 0, true);
    outTexture.Compress(highQuality: true);
    outTexture.Apply(updateMipmaps: true, makeNoLongerReadable: true);
    RenderTexture.active = oldActive;
    camera.targetTexture = null;
    renderTexture.Release();
    Object.DestroyImmediate(renderTexture);
    return outTexture;
  }
  #endregion
}

}  // namespace
