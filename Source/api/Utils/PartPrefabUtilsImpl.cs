// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;

// ReSharper disable once CheckNamespace
// ReSharper disable once IdentifierTypo
namespace KISAPIv2 {

/// <summary>Various methods to deal with the part prefabs.</summary>
public class PartPrefabUtilsImpl {

  #region API implementation

  /// <summary>Returns the cargo module from the part.</summary>
  /// <param name="avPart">The available part to get info from.</param>
  /// <param name="onlyForStockInventory">
  /// Tells if the returned cargo module must be compatible with the stock cargo inventory module. I.e. it can be stored
  /// in the stock inventory.
  /// </param>
  /// <returns>The cargo module or <c>null</c>.</returns>
  public ModuleCargoPart GetCargoModule(AvailablePart avPart, bool onlyForStockInventory = true) {
    return avPart.partPrefab.Modules
        .OfType<ModuleCargoPart>()
        .FirstOrDefault(m => !onlyForStockInventory || m.packedVolume > 0);
  }
  #endregion
}

}  // namespace
