// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KISAPIv2;
using KSP.UI;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using KSPDev.ProcessingUtils;
using KSPDev.Unity;
using System;
using System.Linq;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <inheritdoc/>
// ReSharper disable once InconsistentNaming
internal sealed class KisItemDragControllerImpl : IKisItemDragController  {

  #region Localizable strings
  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> CancelDraggingTxt = new Message<KeyboardEventType>(
      "",
      defaultTemplate: "[<<1>>]: Cancel dragging",
      description: "Help string that explains how to cancel the dragging mode. It is shown at the"
          + " top of the screen when a KIS item is being dragged.\n"
          + "The <<1>> argument is the keyboard shortcut that cancels the mode.",
      example: "[Escape]: Cancel dragging");
  #endregion

  #region Helper Unity component to track lock state
  /// <summary>Locks all game's keyboard events and starts updating the drag targets.</summary>
  /// <remarks>
  /// <p>
  /// Once this object is created, all the normal game keys will be locked. Including the key that calls system menu
  /// (ESC). The auto save ability will also be blocked. To exit this mode user either need to press <c>ESC</c> or the
  /// code needs to cancel the drag mode via <see cref="CancelItemsLease"/>.
  /// </p>
  /// <p>
  /// When this object is active, a GUI message will be shown at the top of the screen, explaining how the user can
  /// cancel the mode. It's imperative to not block this ability!
  /// </p>
  /// </remarks>
  /// <seealso cref="LeaseItems"/>
  /// <seealso cref="IKisDragTarget"/>
  class DragModeTracker : MonoBehaviour {
    /// <summary>The controller object to notify.</summary>
    public KisItemDragControllerImpl controller;

    /// <summary>Indicates if user can cancel the dragged operation.</summary>
    /// <remarks>
    /// It affects the offered hints in GUI, as well as handling the appropriate action from the keyboard.
    /// </remarks>
    public bool canCancelInteractively;

    /// <summary>Tells if callbacks are being handling.</summary>
    /// <remarks>Not all actions with the controller are allowed in this mode.</remarks>
    public bool isInCallbackCycle { get; private set; }
  
    #region Local fields
    const string TotalControlLock = "KISDragControllerUberLock";
    readonly ScreenMessage _statusScreenMessage = new("", Mathf.Infinity, ScreenMessageStyle.UPPER_CENTER);
    bool _oldCanAutoSaveState;
    bool _controlsLocked;
    Vector3 _lastPointerPosition = -Vector2.one;
    #endregion

    #region MonoBehaviour callbacks
    void Awake() {
      _statusScreenMessage.message = CancelDraggingTxt.Format(CancelEvent);
      InputLockManager.SetControlLock(DragControlsLockTypes, TotalControlLock);
      _oldCanAutoSaveState = HighLogic.CurrentGame.Parameters.Flight.CanAutoSave;
      HighLogic.CurrentGame.Parameters.Flight.CanAutoSave = false;
      _controlsLocked = true;
      DebugEx.Fine("KIS drag lock acquired");
    }

    void OnDestroy() {
      CancelLock();
    }

    void Update() {
      if (!_controlsLocked) {
        return;
      }
      if (canCancelInteractively) {
        ScreenMessages.PostScreenMessage(_statusScreenMessage);
        // Delay the lock handling to not leak the ESC key release to the game.
        if (Input.GetKeyUp(CancelEvent.keyCode)) {
          AsyncCall.CallOnEndOfFrame(this, CancelLock);
        }
      }

      var pointerMoved = false;
      if (_lastPointerPosition != Input.mousePosition) {
        _lastPointerPosition = Input.mousePosition;
        pointerMoved = true;
      }

      // Update the pointer state.
      var canConsume = false;
      isInCallbackCycle = true;
      foreach (var target in controller._dragTargets) {
        canConsume |= SafeCallbacks.Func(() => target.OnKisDrag(pointerMoved), false);
      }
      isInCallbackCycle = false;
      controller.dragIconObj.showNoGo = !canConsume;
    }
    #endregion

    #region API methods
    /// <summary>Stops any tracking activity and cleanups the object.</summary>
    public void CancelLock() {
      if (!_controlsLocked) {
        return;
      }
      _controlsLocked = false;
      ScreenMessages.RemoveMessage(_statusScreenMessage);
      InputLockManager.RemoveControlLock(TotalControlLock);
      HighLogic.CurrentGame.Parameters.Flight.CanAutoSave = _oldCanAutoSaveState;
      if (controller.isDragging) {
        controller.CancelItemsLease();
      }
      Hierarchy.SafeDestroy(gameObject);  // Ensure the tracking is over.
      DebugEx.Fine("KIS drag lock released");
    }
    #endregion
  }
  #endregion

  #region IKisItemDragController properties
  /// <inheritdoc/>
  public bool isDragging => leasedItems != null;

  /// <inheritdoc/>
  public InventoryItem[] leasedItems { get; private set; }

  /// <inheritdoc/>
  public UiKisInventorySlotDragIcon dragIconObj {
    get => _dragIconObj;
    private set {
      Hierarchy.SafeDestroy(_dragIconObj);
      _dragIconObj = value;
    }
  }
  UiKisInventorySlotDragIcon _dragIconObj;

  /// <inheritdoc/>
  public GameObject focusedTarget { get; private set; }
  #endregion

  #region Local constants, properties and fields
  /// <summary>Keyboard event that cancels the dragging mode.</summary>
  static readonly Event CancelEvent = Event.KeyboardEvent("[esc]");

  /// <summary>Targets that need to get updated when dragging state is changed.</summary>
  IKisDragTarget[] _dragTargets = new IKisDragTarget[0];

  /// <summary>Current lock tracking object.</summary>
  /// <remarks>
  /// This object only exists when there is an active dragging operation. Its main purpose is
  /// tracking the input locking state and notifying the drag targets.
  /// </remarks>
  /// <seealso cref="_dragTargets"/>
  DragModeTracker _dragTracker;

  /// <summary>Action that is called when the dragged items being consumed.</summary>
  /// <remarks>
  /// The callback can return <c>false</c> to indicate that the consumption is not allowed. It will
  /// be treated as an error situation, but the game consistency won't suffer.
  /// </remarks>
  Func<bool> _consumeItemsFn;

  /// <summary>Action that is called when the dragging is cancelled.</summary>
  /// <remarks>It's a cleanup action. It must never fail.</remarks>
  Action _cancelItemsLeaseFn;

  /// <summary>Lock mask for the dragging state.</summary>
  /// <remarks>
  /// The absolute conditions to be met:
  /// <ul type="bullet">
  /// <li>Now quick save/load ability.</li>
  /// <li>No auto-save.</li>
  /// <li>No ship control.</li>
  /// </ul>
  /// </remarks>
  const ControlTypes DragControlsLockTypes = ControlTypes.ALLBUTCAMERAS
      & ~ControlTypes.ACTIONS_SHIP & ~ControlTypes.ACTIONS_EXTERNAL;
  #endregion

  #region KISItemDragController implementation
  /// <inheritdoc/>
  public bool LeaseItems(
      Texture dragIcon, InventoryItem[] items, Func<bool> consumeItemsFn, Action cancelItemsLeaseFn,
      bool allowInteractiveCancel = true) {
    ArgumentGuard.HasElements(items, nameof(items));
    if (isDragging) {
      DebugEx.Error("Cannot start a new dragging: oldLeasedItems={0}, newLeasedItems={1}",
                    leasedItems.Length, items.Length);
      return false;
    }
    ArgumentGuard.NotNull(dragIcon, nameof(dragIcon));
    ArgumentGuard.NotNull(consumeItemsFn, nameof(consumeItemsFn));
    ArgumentGuard.NotNull(cancelItemsLeaseFn, nameof(cancelItemsLeaseFn));
    if (items.Length == 0) {
      DebugEx.Error("Cannot start dragging for an empty items list");
      return false;
    }
    DebugEx.Info("Leasing items: count={0}", items.Length);
    leasedItems = items;
    _consumeItemsFn = consumeItemsFn;
    _cancelItemsLeaseFn = cancelItemsLeaseFn;
    StartDragIcon(dragIcon);
    Array.ForEach(_dragTargets, t => SafeCallbacks.Action(t.OnKisDragStart));
    _dragTracker = new GameObject().AddComponent<DragModeTracker>();
    _dragTracker.controller = this;
    _dragTracker.canCancelInteractively = allowInteractiveCancel;
    return true;
  }

  /// <inheritdoc/>
  public InventoryItem[] ConsumeItems() {
    if (!isDragging) {
      DebugEx.Error("Cannot consume items since nothing is being dragged");
      return null;
    }
    if (_dragTracker.isInCallbackCycle) {
      throw new InvalidOperationException("Cannot consume items from a drag callback!");
    }
    var consumed = _consumeItemsFn();
    if (!consumed) {
      DebugEx.Warning("Items not consumed: provider refused the deal");
      return null;
    }
    var consumedItems = leasedItems;
    DebugEx.Info("Consume dragged items: count={0}", consumedItems.Length);
    ClearLease(isCancelled: false);
    return consumedItems;
  }

  /// <inheritdoc/>
  public void CancelItemsLease() {
    if (!isDragging) {
      DebugEx.Warning("Cannot cancel dragging since nothing is being dragged");
      return;
    }
    if (_dragTracker.isInCallbackCycle) {
      throw new InvalidOperationException("Cannot cancel dragging from a drag callback!");
    }
    SafeCallbacks.Action(_cancelItemsLeaseFn);
    ClearLease(isCancelled: true);
  }

  /// <inheritdoc/>
  public void SetFocusedTarget(GameObject newTarget) {
    if (focusedTarget != newTarget) {
      DebugEx.Fine("Focus target changed: old={0}, new={1}",
                   focusedTarget != null ? focusedTarget.name : null,
                   newTarget != null ? newTarget.name : null);
      focusedTarget = newTarget;
      Array.ForEach(_dragTargets, t => SafeCallbacks.Action(() => t.OnFocusTarget(newTarget)));
    }
  }

  /// <inheritdoc/>
  public void RegisterTarget(IKisDragTarget target) {
    ArgumentGuard.NotNull(target, nameof(target));
    if (!_dragTargets.Contains(target)) {
      _dragTargets = _dragTargets.Concat(new[] { target }).ToArray();
      if (isDragging) {
        SafeCallbacks.Action(target.OnKisDragStart);
      }
    } else {
      DebugEx.Warning("Target is already registered: {0}", target);
    }
  }

  /// <inheritdoc/>
  public void UnregisterTarget(IKisDragTarget target) {
    ArgumentGuard.NotNull(target, nameof(target));
    if (_dragTargets.Contains(target)) {
      if (isDragging) {
        SafeCallbacks.Action(() => target.OnKisDragEnd(isCancelled: true));
      }
      _dragTargets = _dragTargets.Where((t, i) => t != target).ToArray();
    } else {
      DebugEx.Warning("Cannot unregister unknown target: {0}", target);
    }
  }
  #endregion

  #region Local utility methods
  /// <summary>Creates a drag icon object.</summary>
  /// <param name="dragIcon">The icon to show in the object.</param>
  void StartDragIcon(Texture dragIcon) {
    dragIconObj = UnityPrefabController.CreateInstance<UiKisInventorySlotDragIcon>(
        "KISDragIcon", UIMasterController.Instance.actionCanvas.transform);
    dragIconObj.slotImage = dragIcon;
  }

  /// <summary>Cleans up any leased state and unlocks game.</summary>
  void ClearLease(bool isCancelled) {
    _consumeItemsFn = null;
    _cancelItemsLeaseFn = null;
    leasedItems = null;
    dragIconObj = null;
    if (_dragTracker != null) {
      _dragTracker.CancelLock();
      _dragTracker = null;
    }
    Array.ForEach(_dragTargets, t => SafeCallbacks.Action(() => t.OnKisDragEnd(isCancelled)));
  }
  #endregion
}

}  // namespace
