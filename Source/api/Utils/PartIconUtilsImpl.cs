// Kerbal Inventory System API v2
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.LogUtils;
using KSPDev.PartUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace KISAPIv2 {

public sealed class PartIconUtils {
  //const int CameraLayer = 22;  // KerbalInstructors
  const int IconCameraLayer = 8;
  const float InflightLightIntensity = 0.4f;
  //FIXME: use level 8 (PartsList_Icons) and setup lights in flight
  //const int CameraLayer = 8;
  const float IconCameraZoom = 0.75f;

  readonly Dictionary<string, Texture> iconsCache = new Dictionary<string, Texture>();

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
    var cacheKey = avPart.name + "-" + resolution + "-" + (variant == null ? "#" : variant.Name);
    Texture result;
    if (iconsCache.TryGetValue(cacheKey, out result) && result != null) {
      // Note that the cached textures can get destroyed between the scenes.
      //FIXME
      DebugEx.Warning("*** getting icon from cache");
      return result;
    }
    //FIXME
    DebugEx.Warning("Creating a new icon for: part={0}, variant={1}",
                    avPart.name, (variant != null ? variant.Name : "N/A"));
    
    var iconPrefab = KISAPI.PartModelUtils.GetIconPrefab(avPart, variant);
    iconPrefab.transform.position = new Vector3(0, 0, 2f);
    iconPrefab.transform.rotation = Quaternion.Euler(-15f, 0.0f, 0.0f);
    iconPrefab.transform.Rotate(0.0f, -30f, 0.0f);
    SetLayerRecursively(iconPrefab, IconCameraLayer);

    // Command Seat Icon Fix (Temporary workaround until squad fix the broken shader)
    // FIXME: reconsider
    var fixShader = Shader.Find("KSP/Alpha/Cutoff Bumped");
    foreach (Renderer r in iconPrefab.GetComponentsInChildren<Renderer>(true)) {
      foreach (Material m in r.materials) {
        if (m.shader.name == "KSP/Alpha/Cutoff") {
          DebugEx.Warning("*** WORKRAROUND!");
          m.shader = fixShader;
        }
      }
    }

    // Setup lighiting.
    GameObject lightObj = null;
    if (HighLogic.LoadedSceneIsFlight) {  // Editor has the light out of the box.
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
    //camera.clearFlags = CameraClearFlags.Depth;//FIXME: was Color
    camera.clearFlags = CameraClearFlags.Color;
    camera.enabled = false;  // Yes, it must be disabled!
    camera.cullingMask = 1 << IconCameraLayer;
    camera.ResetAspect();

    var renderTarget = new RenderTexture(resolution, resolution, 8);
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

  #region Local utility methods
  void SetLayerRecursively(GameObject obj, int newLayer) {
    obj.layer = newLayer;
    for (var i = obj.transform.childCount - 1; i >= 0; --i) {
      SetLayerRecursively(obj.transform.GetChild(i).gameObject, newLayer);
    }
  }
  #endregion
}

}  // namespace
