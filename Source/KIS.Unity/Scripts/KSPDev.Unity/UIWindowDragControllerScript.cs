// Kerbal Development tools for Unity scripts.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// ReSharper disable once CheckNamespace
namespace KSPDev.Unity {

/// <summary>
/// Script that allows moving windows by dragging a control. It also makes the dragged window the
/// top most window on left-click.
/// </summary>
/// <remarks>
/// Add this script to the main window object. Optionally, specify the control that will be
/// "draggable" (it's usually a window header).
/// </remarks>
/// <seealso cref="IKspDevUnityControlChanged"/>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class UiWindowDragControllerScript : UiControlBaseScript,
    IKspDevUnityControlChanged,
    IBeginDragHandler, IEndDragHandler, IDragHandler,
    IPointerDownHandler, IEventSystemHandler {

  #region API fields and properties
  /// <summary>Control in children that takes drag and click events.</summary>
  /// <remarks>
  /// If not set, that any portion of the window will start dragging or bringing focus, given the
  /// related event is not consumed by the top-most control in that area.
  /// </remarks>
  [SerializeField]
  GameObject eventsTargetControl = null;

  /// <summary>Minimum offsets from the parent boundary when clamping window position.</summary>
  /// <seealso cref="clampWindowToParent"/>
  public Vector2 windowClampOffset;

  /// <summary>Enables or disables window position clamping.</summary>
  /// <remarks>
  /// When <c>true</c>, it will be impossible to drag any part of the window beyond the parents
  /// boundary.
  /// </remarks>
  /// <seealso cref="windowClampOffset"/>
  public bool clampWindowToParent;

  /// <summary>Tells if the window is being dragged.</summary>
  public virtual bool isDragged { get; private set; }
  #endregion

  #region Inheritable fields and properties
  /// <summary>Game object that captures drag events for the window.</summary>
  /// <remarks>Don NOT optimize this call! Unity won't like it.</remarks>
  protected GameObject eventsTarget =>
      eventsTargetControl != null ? eventsTargetControl : mainRect.gameObject;
  #endregion

  #region IBeginDragHandler implementation
  /// <inheritdoc/>
  public void OnBeginDrag(PointerEventData eventData) {
    if (!eventData.used && eventData.pointerEnter == eventsTarget) {
      eventData.Use();
      isDragged = true;
    }
  }
  #endregion

  #region IEndDragHandler implementation
  /// <inheritdoc/>
  public void OnEndDrag(PointerEventData eventData) {
    if (isDragged) {
      eventData.Use();
      isDragged = false;
    }
  }
  #endregion

  #region IDragHandler implementation
  /// <inheritdoc/>
  public virtual void OnDrag(PointerEventData eventData) {
    if (isDragged && eventData.button == PointerEventData.InputButton.Left) {
      eventData.Use();
      var position = mainRect.position;
      position.x += eventData.delta.x;
      position.y += eventData.delta.y;
      mainRect.position = position;
      SendMessage("ControlUpdated", gameObject, SendMessageOptions.DontRequireReceiver);
    }
  }
  #endregion

  #region IPointerDownHandler implementation
  /// <inheritdoc/>
  public virtual void OnPointerDown(PointerEventData eventData) {
    BringOnTop();
  }
  #endregion

  #region IKSPDevUnityControlChanged implementation
  /// <inheritdoc/>
  public virtual void ControlUpdated() {
    ClampToParentRect();
  }
  #endregion

  #region API methods
  /// <summary>Brings this window over the other children in the window parent.</summary>
  public virtual void BringOnTop() {
    mainRect.transform.SetAsLastSibling();
  }
  #endregion

  #region Local utility methods
  bool ClampToParentRect() {
    if (!clampWindowToParent) {
      return false;  // Nothing to do.
    }
    LayoutRebuilder.ForceRebuildLayoutImmediate(mainRect);
    var parentDragRect = transform.parent.GetComponent<RectTransform>();
    var newPosition = mainRect.localPosition;
    var parentRc = parentDragRect.rect;
    var mainRc = mainRect.rect;
    var minPos = parentRc.min + windowClampOffset - mainRc.min;
    var maxPos = parentRc.max - windowClampOffset - mainRc.max;
    if (minPos.y > maxPos.y) {
      var y = minPos.y;
      minPos.y = maxPos.y;
      maxPos.y = y;
    }
    if (minPos.x > maxPos.x) {
      var x = minPos.x;
      minPos.x = maxPos.x;
      maxPos.x = x;
    }
    var localPosition = mainRect.localPosition;
    newPosition.x = Mathf.Clamp(localPosition.x, minPos.x, maxPos.x);
    newPosition.y = Mathf.Clamp(localPosition.y, minPos.y, maxPos.y);
    var res = localPosition != newPosition;
    mainRect.localPosition = newPosition;
    return res;
  }
  #endregion
}

}  // namespace
