// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.ConfigUtils;

namespace KISAPIv2 {

/// <summary>Container for the various global settings of the mod.</summary>
[PersistentFieldsDatabase("KIS/settings2/KISConfig", "")]
public sealed class CommonConfigImpl {
  #region ICommonConfig implementation
  /// <inheritdoc/>
  public string sndPathBipWrong { get { return _sndPathBipWrong; } }
  #endregion

  [PersistentField("Sounds/bipWrong")]
  string _sndPathBipWrong = "";

  internal CommonConfigImpl() {
    ConfigAccessor.ReadFieldsInType(GetType(), this);
  }
}
  
}  // namespace
