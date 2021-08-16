// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System.Collections.Generic;
using KSP.UI.Screens;
using KSPDev.LogUtils;
using UniLinq;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KSPDev.ModelUtils {

/// <summary>Various tools to deal with shaders.</summary>
public static class Shaders {
  /// <summary>Ensures that all materials use shaders from the "ScreenSpace" namespace.</summary>
  /// <remarks>
  /// There are legacy shaders that are used here and there that are not working as intended in the new KSP anymore.
  /// One example is the part's variant setup that replaces the part's shader to a legacy one. This method replaces
  /// such legacy shaders to the new stock shaders that (presumably) make the same output.
  /// </remarks> 
  /// <param name="materials">The materials to fix the shader in.</param>
  /// <param name="logDifferences">Tells if all adjustments to the shader must be logged.</param>
  /// <seealso cref="CreateMaterialArray"/>
  public static void FixScreenSpaceShaders(IEnumerable<Material> materials, bool logDifferences = false) {
    foreach (var material in materials) {
      var originalShader = material.shader.name;
      if (!originalShader.Contains("ScreenSpaceMask")) {
        if (originalShader == "KSP/Bumped Specular (Mapped)") {
          material.shader = Shader.Find("KSP/ScreenSpaceMaskSpecular");
        } else if (originalShader.Contains("Bumped")) {
          material.shader = Shader.Find("KSP/ScreenSpaceMaskBumped");
        } else if (originalShader.Contains("KSP/Alpha/CutoffBackground")) {
          material.shader = Shader.Find("KSP/ScreenSpaceMaskAlphaCutoffBackground");
        } else if (originalShader == "KSP/Unlit") {
          material.shader = Shader.Find("KSP/ScreenSpaceMaskUnlit");
        } else {
          material.shader = Shader.Find("KSP/ScreenSpaceMask");
        }
      }
      if (originalShader != material.shader.name && logDifferences) {
        DebugEx.Warning("Replacing shader: old={0}, new={1}", originalShader, material.shader.name);
      }
    }
  }

  /// <summary>Returns all materials in the model.</summary>
  /// <remarks>
  /// It's an unconstrained version of the stock <see cref="EditorPartIcon.CreateMaterialArray(GameObject)"/> method.
  /// The latter only picks specific shaders which may not be enough to deal with the legacy part's variant setups.
  /// </remarks>
  /// <param name="model">The model to get materials for.</param>
  /// <param name="includeInactiveRenderers">Tells if the inactive renders should also be considered.</param>
  /// <returns>A n array of materials, used by all renderers imn teh model.</returns>
  /// <see cref="FixScreenSpaceShaders"/>
  public static Material[] CreateMaterialArray(GameObject model, bool includeInactiveRenderers = true) {
    return model.GetComponentsInChildren<Renderer>(includeInactiveRenderers)
        .SelectMany(r => r.materials)
        .ToArray();
  }
}

}  // namespace
