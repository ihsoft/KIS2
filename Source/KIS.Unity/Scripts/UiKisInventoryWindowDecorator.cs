// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

// ReSharper disable RedundantDefaultMemberInitializer
// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>
/// Wrapper for <see cref="UiKisInventoryWindow"/> that exposes protected callbacks to Unity.
/// </summary>
sealed class UiKisInventoryWindowDecorator : UiKisInventoryWindow {
  #region Unity only listeners
  public new void OnSizeChanged(UiKisHorizontalSliderControl slider) {
    base.OnSizeChanged(slider);
  }

  public new void OnDialogCloseClicked() {
    base.OnDialogCloseClicked();
  }
  #endregion
}

}  // namespace
