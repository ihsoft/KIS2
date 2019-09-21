﻿// Kerbal Development tools for Unity scripts.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using KSPDev.Unity;
using UnityEngine;

namespace KSPDev.Unity {

/// <summary>Script that tracks mouse position and aligns the control to it.</summary>
/// <remarks>
/// Simply add this script to a control that needs following the pointer (like a hint window) and
/// set the desired offsets and clamping mode.
/// </remarks>
public sealed class UIFollowThePointerScript : UIControlBaseScript {

  #region API fields an properties
  /// <summary>
  /// Distance between the left-top corner of the control and the current pointer position.
  /// </summary>
  /// <seealso cref="clampToScreen"/>
  public int rightSidePointerOffset;

  /// <summary>
  /// Distance between the right-top corner of the control and the current pointer position.
  /// </summary>
  /// <remarks>
  /// This settings is only considered when <see cref="clampToScreen"/> mode is enabled and the
  /// control cannot fit to the right side of the pointer.
  /// </remarks>
  /// <seealso cref="clampToScreen"/>
  public int leftSidePointerOffset;

  /// <summary>Tells if the control should always be fully visible.</summary>
  /// <remarks>
  /// If the control cannot fit to the right side of the pointer, then it flips on the left side. If
  /// the control height is too large, then it just "sticks" to the screen bottom.    
  /// </remarks>
  /// <seealso cref="leftSidePointerOffset"/>
  public bool clampToScreen;
  #endregion

  #region Local fields
  Canvas canvas;
  RectTransform canvasRect;
  Vector3 lastPointerPosition = -Vector2.one;
  Vector2 lastControlSize = -Vector2.one;
  #endregion

  #region MonoBehaviour callbacks
  void Awake() {
    canvas = GetComponentInParent<Canvas>();
    canvasRect = canvas.transform as RectTransform;
  }

  void LateUpdate() {
    if (lastPointerPosition != Input.mousePosition || lastControlSize != mainRect.sizeDelta) {
      lastPointerPosition = Input.mousePosition;
      lastControlSize = mainRect.sizeDelta;
      PositionToPointer();
    }
  }
  #endregion

  #region Local utility methods
  void PositionToPointer() {
    var tooltipSize = mainRect.sizeDelta;
    var canvasSize = canvasRect.sizeDelta;
    var newPos = Input.mousePosition;
    if (clampToScreen) {
      if (newPos.x + rightSidePointerOffset + tooltipSize.x < Screen.width) {
        newPos.x += rightSidePointerOffset;
      } else {
        newPos.x -= leftSidePointerOffset + tooltipSize.x;
      }
      if (newPos.y < tooltipSize.y) {
        newPos.y = tooltipSize.y;
      }
    } else {
      newPos.x += rightSidePointerOffset;
    }

    Vector2 pos;
    RectTransformUtility.ScreenPointToLocalPointInRectangle(
        canvasRect, newPos, canvas.worldCamera, out pos);
    pos = canvas.transform.TransformPoint(pos);
    mainRect.position = new Vector3(pos.x, pos.y, 0);
  }
  #endregion
}

}  // namespace
