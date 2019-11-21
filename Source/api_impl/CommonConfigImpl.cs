// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.ConfigUtils;

// ReSharper disable once CheckNamespace
// ReSharper disable once IdentifierTypo
namespace KISAPIv2 {

/// <summary>Container for the various global settings of the mod.</summary>
[PersistentFieldsDatabase("KIS/settings2/KISConfig")]
public sealed class CommonConfigImpl {
  /// <summary>Path to the sound clip that plays "NOPE" sound.</summary>
  /// <remarks>Use this sound each time the user action cannot be performed.</remarks>
  public string sndPathBipWrong => _sndPathBipWrong;

  // ReSharper disable once FieldCanBeMadeReadOnly.Local
  // ReSharper disable once ConvertToConstant.Local
  [PersistentField("Sounds/bipWrong")]
  string _sndPathBipWrong = "";

  internal CommonConfigImpl() {
    ConfigAccessor.ReadFieldsInType(GetType(), this);
  }
}
  
}  // namespace
