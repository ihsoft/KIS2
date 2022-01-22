// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

// ReSharper disable once CheckNamespace
// ReSharper disable once IdentifierTypo
namespace KISAPIv2 {

/// <summary>Various methods to deal with the part prefabs.</summary>
public class PartPrefabUtilsImpl {

  #region API implementation
  /// <summary>Returns the cargo module from the part that can be stored into the inventory.</summary>
  /// <remarks>
  /// If the part has the module, but the packed volume is negative, then this method returns <c>null</c>. Such parts
  /// cannot be stored into the stock inventory.
  /// </remarks>
  /// <param name="avPart">The available part to get info from.</param>
  /// <returns>The cargo module or <c>null</c>.</returns>
  public ModuleCargoPart GetCargoModule(AvailablePart avPart) {
    var module = avPart.partPrefab.FindModuleImplementing<ModuleCargoPart>();
    return module == null || module.packedVolume > 0 ? module : null;
  }
  #endregion
}

}  // namespace
