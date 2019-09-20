// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using KIS2.UIKISInventorySlot;
using KSPDev.Unity;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KIS2 {

/// <summary>Helper class to debug behavior in Unity.</summary>
/// <remarks>
/// This class must be removed before making the final prefabs. However, even if it manages to
/// sneak into the game, it will simply not start there.
/// </remarks>
public sealed class DemoKISInventoryController : UIControlBaseScript {

  #region For Unity customization
  public int limitGridHeight = 3;
  public int limitGridWidth = 6;
  public Vector2 minSize = new Vector2(3, 1);
  public Vector2 maxSize = new Vector2(16, 8);
  public string hintTitle = "test slot";
  #endregion

  #region MonoBehaviour overrides
  void Start() {
    if (!Application.isEditor) {
      enabled = false;
      return;
    }
    var kisWindow = GetComponent<UIKISInventoryWindow>();
    if (!kisWindow.isPrefab) {
      kisWindow.onSlotHover.Add(OnSlotHover);
      kisWindow.onSlotClick.Add(OnSlotClick);
      kisWindow.onSlotAction.Add(OnSlotAction);
      kisWindow.onGridSizeChange.Add(OnSizeChanged);
      kisWindow.minSize = minSize;
      kisWindow.maxSize = maxSize;
      kisWindow.SetGridSize((int) minSize.x, (int) minSize.y);
    }
  }
  #endregion

  #region Local utlity methods
  void OnSlotHover(UIKISInventoryWindow host, Slot slot, bool isHover) {
    LogInfo("Pointer hover: slot={0}, isHover={1}", slot.slotIndex, isHover);
    if (isHover) {
      var tooltip = host.StartSlotTooltip();
      tooltip.title = hintTitle;
      tooltip.UpdateLayout();
    }
  }

  void OnSlotClick(
      UIKISInventoryWindow host, Slot slot, PointerEventData.InputButton button) {
    LogInfo("Clicked: slot={0}, button={1}", slot.slotIndex, button);
  }

  void OnSlotAction(
      UIKISInventoryWindow host, Slot slot, int actionButtonNum,
      PointerEventData.InputButton button) {
    LogInfo("Clicked: slot={0}, action={1}, button={2}", slot.slotIndex, actionButtonNum, button);
  }

  Vector2 OnSizeChanged(UIKISInventoryWindow host, Vector2 oldSize, Vector2 newSize) {
    return new Vector2(
        newSize.x <= limitGridWidth ? newSize.x : limitGridWidth,
        newSize.y <= limitGridHeight ? newSize.y : limitGridHeight);
  }
  #endregion
}

}  // namespace
