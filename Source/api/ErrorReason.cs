// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

// ReSharper disable once CheckNamespace
// ReSharper disable once IdentifierTypo
namespace KISAPIv2 {

/// <summary>Container for an error result.</summary>
/// <remarks>
/// This type is a generic way to report back the error conditions within KIS API.
/// </remarks>
public struct ErrorReason {
  /// <summary>Indicates the error class.</summary>
  /// <remarks>Multiple different errors can be reported under the same class.</remarks>
  public string errorClass;

  /// <summary>Localized string that can be presented to the user.</summary>
  /// <remarks>It can be <c>null</c> if the error is not supposed to be shown to the user.</remarks>
  public string guiString;
}

}  // namespace
