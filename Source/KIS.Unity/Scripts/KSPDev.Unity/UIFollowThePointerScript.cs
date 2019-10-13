// Kerbal Development tools for Unity scripts.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using KSPDev.Unity;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace KSPDev.Unity {

/// <summary>Script that tracks mouse position and aligns the control to it.</summary>
/// <remarks>
/// Simply add this script to a control that needs following the pointer (like a hint window) and
/// set the desired offsets and clamping mode.
/// <para>The position is aligned with respect to the owners pivot point.</para>
/// </remarks>
public sealed class UIFollowThePointerScript : UIControlBaseScript {

  #region API fields an properties
  /// <summary>
  /// Offset between the left-top corner of the control and the current pointer position.
  /// </summary>
  /// <seealso cref="clampToScreen"/>
  public Vector2 nonClampedPointerOffset;

  /// <summary>
  /// Offset between the right-top corner of the control and the current pointer position.
  /// </summary>
  /// <remarks>
  /// This settings is only considered when <see cref="clampToScreen"/> mode is enabled and the
  /// control cannot fit to the right side of the pointer.
  /// </remarks>
  /// <seealso cref="clampToScreen"/>
  public Vector2 clampedPointerOffset;

  /// <summary>Tells if the control should always be fully visible.</summary>
  /// <remarks>
  /// If the control cannot fit to the right side of the pointer, then it flips on the left side. If
  /// the control height is too large, then it just "sticks" to the screen bottom.    
  /// </remarks>
  /// <seealso cref="clampedPointerOffset"/>
  public bool clampToScreen;

  /// <summary>Tells if any of the children in this assembly should be a ray cast target.</summary>
  /// <remarks>
  /// Normally, the controls that follow pointer should not be raycast targets, since it could
  /// create an interferrence.
  /// </remarks>
  public bool NoRaycastTargets;
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

  void Start() {
    if (NoRaycastTargets) {
      Array.ForEach(gameObject.GetComponentsInChildren<Graphic>(), x => x.raycastTarget = false);
    }
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
      if (newPos.x + nonClampedPointerOffset.x + tooltipSize.x < Screen.width) {
        newPos.x += nonClampedPointerOffset.x;
      } else {
        newPos.x -= clampedPointerOffset.x + tooltipSize.x;
      }
      if (newPos.y + nonClampedPointerOffset.y < tooltipSize.y) {
        newPos.y = tooltipSize.y;
      }
    } else {
      newPos += new Vector3(nonClampedPointerOffset.x, nonClampedPointerOffset.y, 0);
    }
    Vector2 pos;
    RectTransformUtility.ScreenPointToLocalPointInRectangle(
        canvasRect, newPos, canvas.worldCamera, out pos);
    mainRect.position = canvas.transform.TransformPoint(pos);
  }
  #endregion
}

}  // namespace
