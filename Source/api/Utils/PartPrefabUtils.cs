// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

// ReSharper disable once CheckNamespace
// ReSharper disable once IdentifierTypo

using KSPDev.PartUtils;

// ReSharper disable once CheckNamespace
// ReSharper disable once IdentifierTypo
namespace KISAPIv2 {

/// <summary>Various methods to deal with the part prefabs.</summary>
public static class PartPrefabUtils {

  #region API implementation
  /// <summary>Returns the cargo module from the part that can be stored into the inventory.</summary>
  /// <remarks>
  /// If the part has the module, but the packed volume is negative, then this method returns <c>null</c>. Such parts
  /// cannot be stored into the stock inventory.
  /// </remarks>
  /// <param name="avPart">The available part to get info from.</param>
  /// <returns>The cargo module or <c>null</c>.</returns>
  public static ModuleCargoPart GetCargoModule(AvailablePart avPart) {
    var module = avPart.partPrefab.FindModuleImplementing<ModuleCargoPart>();
    return module == null || module.packedVolume > 0 ? module : null;
  }

  /// <summary>Calculates part's dry mass given the config and the variant.</summary>
  /// <param name="avPart">The part's proto.</param>
  /// <param name="variantName">
  /// The part's variant. This value is ignored if the part doesn't have variants. Otherwise, it must be a valid variant
  /// name.
  /// </param>
  /// <returns>The dry cost of the part.</returns>
  /// <seealso cref="VariantsUtils2.ExecuteAtPartVariant"/>
  public static double GetPartDryMass(AvailablePart avPart, string variantName) {
    var itemMass = 0.0f;
    VariantsUtils2.ExecuteAtPartVariant(avPart, variantName, p => {
      itemMass = p.mass + p.GetModuleMass(p.mass);
    });
    return itemMass;
  }

  /// <summary>Calculates part's dry cost given the config and the variant.</summary>
  /// <param name="avPart">The part's proto.</param>
  /// <param name="variantName">
  /// The part's variant. This value is ignored if the part doesn't have variants. Otherwise, it must be a valid variant
  /// name.
  /// </param>
  /// <returns>The dry cost of the part.</returns>
  public static double GetPartDryCost(AvailablePart avPart, string variantName) {
    var itemCost = 0.0f;
    VariantsUtils2.ExecuteAtPartVariant(avPart, variantName, p => {
      itemCost = avPart.cost + p.GetModuleCosts(avPart.cost);
    });
    return itemCost;
  }
  #endregion
}

}  // namespace
