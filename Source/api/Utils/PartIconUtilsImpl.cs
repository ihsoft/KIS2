// Kerbal Inventory System API v2
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.LogUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KISAPIv2 {

public sealed class PartIconUtils {
  const int IconCameraLayer = 8;
  const float InflightLightIntensity = 0.4f;
  const float IconCameraZoom = 0.75f;
  const int DefaultHighResolution = 512;
  const int DefaultStdResolution = 256;

  static readonly Dictionary<string, Texture> iconsCache = new Dictionary<string, Texture>();

  /// <summary>
  /// Makes a sprite that represents the part in it's default icon state. This is what is shown in
  /// the editor when the part is not hovered.
  /// </summary>
  /// <param name="avPart">The part to make icon for.</param>
  /// <param name="resolution">The size of the sprite. The part icon is always square.</param>
  /// <param name="variant">
  /// The variant to apply to the part before capturing an icon. It can be null if not varian needs
  /// to be applied.
  /// </param>
  /// <returns>The psrite of the icon.</returns>
  public Texture MakeDefaultIcon(AvailablePart avPart, int resolution, PartVariant variant) {
    var cacheKey = avPart.name + "-" + resolution + (variant == null ? "" : "-" + variant.Name);
    Texture result;
    if (iconsCache.TryGetValue(cacheKey, out result) && result != null) {
      // Note that the cached textures can get destroyed between the scenes.
      return result;
    }
    DebugEx.Fine("Creating a new icon for: part={0}, variant={1}, key={2}",
                 avPart.name, (variant != null ? variant.Name : "N/A"), cacheKey);
    var iconPrefab = KisApi.PartModelUtils.GetIconPrefab(avPart, variant);
    iconPrefab.transform.position = new Vector3(0, 0, 2f);
    iconPrefab.transform.rotation = Quaternion.Euler(-15f, 0.0f, 0.0f);
    iconPrefab.transform.Rotate(0.0f, -30f, 0.0f);
    SetLayerRecursively(iconPrefab, IconCameraLayer);

    // Setup lighiting.
    GameObject lightObj = null;
    if (HighLogic.LoadedSceneIsFlight) {  // Editor has the right lights out of the box.
      lightObj = new GameObject();
      var light = lightObj.AddComponent<Light>();
      light.cullingMask = 1 << IconCameraLayer;
      light.type = LightType.Directional;
      light.intensity = InflightLightIntensity;
      light.shadows = LightShadows.None;
      light.renderMode = LightRenderMode.ForcePixel;
    }

    // Make a camera and make snapshot.
    var cameraObj = new GameObject();
    cameraObj.transform.position = Vector3.zero;
    cameraObj.transform.rotation = Quaternion.identity;
    var camera = cameraObj.AddComponent<Camera>();
    camera.orthographic = true;
    camera.orthographicSize = IconCameraZoom;
    camera.clearFlags = CameraClearFlags.Color;
    camera.enabled = false;  // Yes, it must be disabled!
    camera.cullingMask = 1 << IconCameraLayer;
    camera.ResetAspect();

    var renderTarget = new RenderTexture(resolution, resolution, 16);
    renderTarget.autoGenerateMips = false;
    camera.targetTexture = renderTarget;
    camera.Render();

    // Capture render target into a texture.
    var oldTarget = RenderTexture.active;
    RenderTexture.active = renderTarget;
    var snapshot = new Texture2D(resolution, resolution);
    snapshot.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
    snapshot.Apply();
    RenderTexture.active = oldTarget;
    result = snapshot;

    // Cleanup.
    UnityEngine.Object.DestroyImmediate(iconPrefab);
    UnityEngine.Object.DestroyImmediate(cameraObj);
    UnityEngine.Object.DestroyImmediate(lightObj);
    UnityEngine.Object.DestroyImmediate(renderTarget);
    
    iconsCache.Add(cacheKey, result);
    return result;
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
    return MakeDefaultIcon(
        part.partInfo,
        GetBestIconResolution(resolution), VariantsUtils.GetCurrentPartVariant(part));
  }

  #region Local utility methods
  void SetLayerRecursively(GameObject obj, int newLayer) {
    obj.layer = newLayer;
    for (var i = obj.transform.childCount - 1; i >= 0; --i) {
      SetLayerRecursively(obj.transform.GetChild(i).gameObject, newLayer);
    }
  }

  /// <summary>
  /// Gives the best icon resolution if one is not explicitly set or doesn't make sense.
  /// </summary>
  /// <remarks>
  /// This method have an ultimate power to override the caller's preference. It tries to give a
  /// best resolution given a number of factors. The screen resolution is one of them.
  /// </remarks>
  /// <param name="requested">The caller's idea of the resolution.</param>
  /// <returns></returns>
  static int GetBestIconResolution(int? requested = null) {
    if (Screen.currentResolution.height > 1080) {
      return requested * 2 ?? DefaultHighResolution;
    }
    return requested ?? DefaultStdResolution;
  }
  #endregion
}

}  // namespace
