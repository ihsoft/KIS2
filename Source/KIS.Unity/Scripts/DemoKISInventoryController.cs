// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KIS2.UIKISInventorySlot;
using KSPDev.Unity;
using UnityEngine;
using UnityEngine.EventSystems;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>Helper class to debug behavior in Unity.</summary>
/// <remarks>
/// This class must be removed before making the final prefabs. However, even if it manages to
/// sneak into the game, it will simply not start there.
/// </remarks>
public sealed class DemoKisInventoryController : UiControlBaseScript {

  #region For Unity customization
  [SerializeField]
  int limitGridHeight = 3;

  [SerializeField]
  int limitGridWidth = 6;

  [SerializeField]
  Vector2 minSize = new Vector2(3, 1);

  [SerializeField]
  Vector2 maxSize = new Vector2(16, 8);

  [SerializeField]
  string hintTitle = "test slot";
  #endregion

  #region Local fields and properties
  UiKisInventoryWindow _unityWindow;
  #endregion

  #region MonoBehaviour overrides
  void Awake() {
    _unityWindow = GetComponent<UiKisInventoryWindow>();
  }

  void Start() {
    if (!Application.isEditor) {
      enabled = false;
      return;
    }
    if (!_unityWindow.isPrefab) {
      _unityWindow.onSlotHover.Add(OnSlotHover);
      _unityWindow.onSlotClick.Add(OnSlotClick);
      _unityWindow.onSlotAction.Add(OnSlotAction);
      _unityWindow.onNewGridSize.Add(OnSizeChanged);
      _unityWindow.minSize = minSize;
      _unityWindow.maxSize = maxSize;
      _unityWindow.SetGridSize(minSize);
    }
  }
  #endregion

  #region Local utlity methods
  void OnSlotHover(Slot slot, bool isHover) {
    LogInfo("Pointer hover: slot={0}, isHover={1}", slot.slotIndex, isHover);
    if (isHover) {
      var tooltip = _unityWindow.StartSlotTooltip();
      tooltip.title = hintTitle;
      tooltip.UpdateLayout();
    }
  }

  void OnSlotClick(Slot slot, PointerEventData.InputButton button) {
    LogInfo("Clicked: slot={0}, button={1}", slot.slotIndex, button);
  }

  void OnSlotAction(Slot slot, int actionButtonNum, PointerEventData.InputButton button) {
    LogInfo("Clicked: slot={0}, action={1}, button={2}", slot.slotIndex, actionButtonNum, button);
  }

  Vector2 OnSizeChanged(Vector2 newSize) {
    return new Vector2(
        newSize.x <= limitGridWidth ? limitGridWidth : newSize.x,
        newSize.y <= limitGridHeight ? limitGridHeight : newSize.y);
  }
  #endregion
}

}  // namespace
