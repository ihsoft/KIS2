// Kerbal Development tools for Unity scripts.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using UnityEngine;
using UnityEngine.UI;

// ReSharper disable once CheckNamespace
namespace KSPDev.Unity {

/// <summary>Script that tracks mouse position and aligns the control to it.</summary>
/// <remarks>
/// Simply add this script to a control that needs following the pointer (like a hint window) and
/// set the desired offsets and clamping mode.
/// <para>The position is aligned with respect to the owners pivot point.</para>
/// </remarks>
public sealed class UiFollowThePointerScript : UiControlBaseScript {

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
  /// create an interference.
  /// </remarks>
  public bool noRaycastTargets = true;

  /// <summary>
  /// Tells if this control must be be rendered over any other siblings of the parent.
  /// </summary>
  /// <remarks>
  /// Unless the code implements own siblings ordering, this property must be <c>true</c>.  
  /// </remarks>
  public bool alwaysOnTop = true;
  #endregion

  #region Local fields
  Canvas _canvas;
  RectTransform _canvasRect;
  Vector3 _lastPointerPosition = -Vector2.one;
  Vector2 _lastControlSize = -Vector2.one;
  #endregion

  #region MonoBehaviour callbacks
  void Awake() {
    _canvas = GetComponentInParent<Canvas>();
    _canvasRect = _canvas.transform as RectTransform;
  }

  void Start() {
    if (noRaycastTargets) {
      Array.ForEach(gameObject.GetComponentsInChildren<Graphic>(), x => x.raycastTarget = false);
    }
  }

  void LateUpdate() {
    if (_lastPointerPosition != Input.mousePosition || _lastControlSize != mainRect.sizeDelta) {
      _lastPointerPosition = Input.mousePosition;
      _lastControlSize = mainRect.sizeDelta;
      PositionToPointer();
    }
    if (alwaysOnTop) {
      mainRect.transform.SetAsLastSibling();  
    }
  }
  #endregion

  #region Local utility methods
  void PositionToPointer() {
    var tooltipSize = mainRect.sizeDelta;
    var canvasSize = _canvasRect.sizeDelta;
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
        _canvasRect, newPos, _canvas.worldCamera, out pos);
    mainRect.position = _canvas.transform.TransformPoint(pos);
  }
  #endregion
}

}  // namespace
