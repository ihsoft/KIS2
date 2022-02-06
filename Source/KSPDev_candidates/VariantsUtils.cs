// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Linq;
using KSPDev.LogUtils;

namespace KSPDev.PartUtils {

/// <summary>Various methods to deal with the part variants.</summary>
public static class VariantsUtils2 {
  /// <summary>Gets the part variant of the given name.</summary>
  /// <param name="avPart">The part info.</param>
  /// <param name="variantName">
  /// The variant name. It can be NULL or empty, in which case the base variant will be returned.
  /// </param>
  /// <returns>The part's variant or <c>null</c> if no variant found.</returns>
  public static PartVariant GetPartVariant(AvailablePart avPart, string variantName) {
    if (string.IsNullOrEmpty(variantName)) {
      return avPart.partPrefab.baseVariant?.Name != null ? avPart.partPrefab.baseVariant : null;
    }
    var variant = avPart.partPrefab.variants.variantList
        .FirstOrDefault(x => x.Name == variantName);
    if (variant == null) {
      DebugEx.Error("Cannot find variant on part: variantName={0}, part={1}", variantName, avPart.name);
    }
    return variant;
  }

  /// <summary>Gets the part variant that is currently selected.</summary>
  /// <param name="part">The part to get variant for.</param>
  /// <returns>The part's variant.</returns>
  public static PartVariant GetCurrentPartVariant(Part part) {
    return part.variants != null ? part.variants.SelectedVariant : null;
  }

  /// <summary>Executes an action on a part with an arbitrary variant applied.</summary>
  /// <remarks>
  /// If the part doesn't support variants, then the action is executed for the unchanged prefab.
  /// </remarks>
  /// <param name="avPart">The part proto.</param>
  /// <param name="variantName">
  /// The name of the variant to apply. If it's not found on the part, then an errors is logged and the method is
  /// executed on the prefab with the base variant.
  /// </param>
  /// <param name="fn">
  /// The action to call once the variant is applied. The argument is a prefab part with the variant
  /// applied, so changing it or obtaining any references won't be a good idea. The prefab part's variant will be
  /// reverted before the method return.
  /// </param>
  public static void ExecuteAtPartVariant(AvailablePart avPart, string variantName, Action<Part> fn) {
    var part = avPart.partPrefab;
    var oldPartVariant = GetCurrentPartVariant(part);
    if (oldPartVariant != null) {
      if (!part.variants.GetVariantNames().Contains(variantName)) {
        DebugEx.Error("Variant not found on part: part={0}, variantName={1}, useVariant={2}",
                      avPart.name, variantName, part.baseVariant.Name);
        variantName = part.baseVariant.Name;
      } 
      part.variants.SetVariant(variantName);
      ApplyVariantOnAttachNodes(part);
      fn(part);  // Run on the updated part.
      part.variants.SetVariant(oldPartVariant.Name);
      ApplyVariantOnAttachNodes(part);
    } else {
      fn(part);
    }
  }

  /// <summary>Applies variant settings to the part attach nodes.</summary>
  /// <remarks>
  /// <p>
  /// The stock apply variant method only does it when the active scene is editor. So if there is a
  /// part in the flight scene with a variant, it needs to be updated for the proper KIS behavior.
  /// </p>
  /// <p>Use this method when changing part variant in flight.</p>
  /// </remarks>
  /// <param name="part">The part to apply the changes to.</param>
  /// <param name="updatePartPosition">
  /// Tells if any connected parts at the attach nodes need to be repositioned accordingly. This may
  /// trigger collisions in the scene, so use carefully.
  /// </param>
  public static void ApplyVariantOnAttachNodes(Part part, bool updatePartPosition = false) {
    foreach (var partAttachNode in part.attachNodes) {
      foreach (var variantAttachNode in part.variants.SelectedVariant.AttachNodes) {
        if (partAttachNode.id == variantAttachNode.id) {
          if (updatePartPosition) {
            ModulePartVariants.UpdatePartPosition(partAttachNode, variantAttachNode);
          }
          partAttachNode.originalPosition = variantAttachNode.originalPosition;
          partAttachNode.position = variantAttachNode.position;
          partAttachNode.size = variantAttachNode.size;
        }
      }
    }
  }
}

}  // namespace
