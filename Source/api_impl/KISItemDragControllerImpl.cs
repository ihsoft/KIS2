// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KISAPIv2;
using KSP.UI;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.LogUtils;
using KSPDev.ModelUtils2;
using KSPDev.ProcessingUtils;
using KSPDev.Unity;
using System;
using System.Linq;
using UnityEngine;

namespace KIS2 {

/// <inheritdoc/>
sealed class KISItemDragControllerImpl : IKISItemDragController  {

  #region Localizable strings
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType2> CancelDraggingTxt = new Message<KeyboardEventType2>(
      "",
      defaultTemplate: "[<<1>>]: to cancel dragging",
      description: "Help string that explains how to cancel the dragging mode. It is shown at the"
          + " top of the screen when a KIS item is being dragged.\n"
          + " The <<1>> argument is the keyboard shortcut that cancels the mode.",
      example: "[Escape]: to cancel dragging");
  #endregion

  #region Helper Unity component to track lock state
  /// <summary>Locks all game's keyboard events and starts updating the drag targets.</summary>
  /// <remarks>
  /// Once this object is created, all the normal game keys will be locked. Including the key that
  /// calls system menu (ESC). The autosave abilty will also be blocked. To exit this mode user
  /// either need to press <c>ESC</c> or the code needs to cancel the drag mode via
  /// <see cref="CancelItemsLease"/>.
  /// <para>
  /// When this object is active, a GUI message will be shown at the top of the screen, explaining
  /// how the user can cancel the mode. It's imperative to not block this ability!
  /// </para>
  /// </remarks>
  /// <seealso cref="LeaseItems"/>
  /// <seealso cref="IKISDragTarget"/>
  class DragModeTracker : MonoBehaviour {
    /// <summary>The controller object to notify.</summary>
    public KISItemDragControllerImpl controller;

    #region Local fields
    const string TotalControlLock = "KISDragControllerUberLock";
    readonly ScreenMessage statusScreenMessage =
        new ScreenMessage("", Mathf.Infinity, ScreenMessageStyle.UPPER_CENTER);
    bool oldCanAutoSaveState;
    bool controlsLocked;
    Vector3 lastPointerPosition = -Vector2.one;
    #endregion

    #region MonoBehaviour callbacks
    void Awake() {
      statusScreenMessage.message = CancelDraggingTxt.Format(cancelEvent);
      InputLockManager.SetControlLock(
          ControlTypes.All & ~ControlTypes.CAMERACONTROLS, TotalControlLock);
      oldCanAutoSaveState = HighLogic.CurrentGame.Parameters.Flight.CanAutoSave;
      HighLogic.CurrentGame.Parameters.Flight.CanAutoSave = false;
      controlsLocked = true;
      DebugEx.Fine("KIS drag lock acquired");
    }

    void OnDestroy() {
      CancelLock();
    }

    void Update() {
      if (controlsLocked) {
        ScreenMessages.PostScreenMessage(statusScreenMessage);

        var pointerMoved = false;
        if (lastPointerPosition != Input.mousePosition) {
          lastPointerPosition = Input.mousePosition;
          pointerMoved = true;
        }
        var canConsume = false;
        foreach (var target in controller.dragTargets) {
          canConsume |= SafeCallbacks.Func(() => target.OnKISDrag(pointerMoved), false);
        }
        controller.dragIconObj.showNoGo = !canConsume;
       
        if (Input.GetKeyUp(cancelEvent.keyCode)) {
          // Delay release to not leak ESC key release to the game.
          AsyncCall.CallOnEndOfFrame(this, CancelLock);
        }
      }
    }
    #endregion

    #region API methods
    /// <summary>Stops any tracking activity and cleanups the object.</summary>
    public void CancelLock() {
      if (controlsLocked) {
        controlsLocked = false;
        ScreenMessages.RemoveMessage(statusScreenMessage);
        InputLockManager.RemoveControlLock(TotalControlLock);
        HighLogic.CurrentGame.Parameters.Flight.CanAutoSave = oldCanAutoSaveState;
        if (controller.isDragging) {
          controller.CancelItemsLease();
        }
        Hierarchy2.SafeDestory(gameObject);  // Ensure the tracking is over.
        DebugEx.Fine("KIS drag lock released");
      }
    }
    #endregion
  }
  #endregion

  #region IKISItemDragController properties
  /// <inheritdoc/>
  public bool isDragging {
    get { return leasedItems != null; }
  }

  /// <inheritdoc/>
  public InventoryItem[] leasedItems { get; private set; }

  /// <inheritdoc/>
  public UIKISInventorySlotDragIcon dragIconObj {
    get { return _dragIconObj; }
    private set {
      Hierarchy2.SafeDestory(_dragIconObj);
      _dragIconObj = value;
    }
  }
  UIKISInventorySlotDragIcon _dragIconObj;
  #endregion

  #region Local constants, properties and fields
  /// <summary>Keyboard event that cancels the dragging mode.</summary>
  static readonly Event cancelEvent = Event.KeyboardEvent("[esc]");

  /// <summary>Targets that need to get updated when dragging state is changed.</summary>
  IKISDragTarget[] dragTargets = new IKISDragTarget[0];

  /// <summary>Current lock tracking object.</summary>
  /// <remarks>
  /// This object only exists when there is an active dragging operation. Its main purpose is
  /// tracking the inoput locking state and notifying the drag targets.
  /// </remarks>
  /// <seealso cref="dragTargets"/>
  DragModeTracker dragTracker;

  /// <summary>Action that is called when the dragged items being consumed.</summary>
  /// <remarks>
  /// The callback can return <c>false</c> to indicate that the consumption is not allowed. It will
  /// be treated as an error situation, but the game consistency won't suffer.
  /// </remarks>
  Func<bool> consumeItemsFn;

  /// <summary>Action that is called when the dragging is cancelled.</summary>
  /// <remarks>It's a cleanup action. It must never fail.</remarks>
  Action cancelItemsLeaseFn;
  #endregion

  #region KISItemDragController implementation
  /// <inheritdoc/>
  public bool LeaseItems(Texture dragIcon, InventoryItem[] items,
                         Func<bool> consumeItemsFn, Action cancelItemsLeaseFn) {
    if (isDragging) {
      DebugEx.Error("Cannot start a new dragging: oldLeasedItems={0}, newLeasedItems={1}",
                    leasedItems.Length, items.Length);
      return false;
    }
    if (items.Length == 0) {
      DebugEx.Error("Cannot start dragging for an empty items list");
      return false;
    }
    leasedItems = items;
    this.consumeItemsFn = consumeItemsFn;
    this.cancelItemsLeaseFn = cancelItemsLeaseFn;
    StartDragIcon(dragIcon);
    Array.ForEach(dragTargets, t => SafeCallbacks.Action(t.OnKISDragStart));
    dragTracker = new GameObject().AddComponent<DragModeTracker>();
    dragTracker.controller = this;
    return true;
  }

  /// <inheritdoc/>
  public InventoryItem[] ConsumeItems() {
    if (!isDragging) {
      DebugEx.Error("Cannot consume items since nothing is being dragged");
      return null;
    }
    var consumed = consumeItemsFn();
    if (!consumed) {
      DebugEx.Fine("Items not consumed: provider refused the deal");
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
    DebugEx.Info("Cancel dragged items: count={0}", leasedItems.Length);
    try {
      cancelItemsLeaseFn();
    } catch (Exception ex) {
      // Don't let callers breaking the cleanup.
      DebugEx.Error("Custom cancel lease callback failed: {0}", ex);
    }
    ClearLease(isCancelled: true);
  }

  /// <inheritdoc/>
  public void RegisterTarget(IKISDragTarget target) {
    if (!dragTargets.Contains(target)) {
      dragTargets = dragTargets.Concat(new[] { target }).ToArray();
      if (isDragging) {
        SafeCallbacks.Action(target.OnKISDragStart);
      }
    } else {
      DebugEx.Warning("Target is already registered: {0}", target);
    }
  }

  /// <inheritdoc/>
  public void UnregisterTarget(IKISDragTarget target) {
    if (dragTargets.Contains(target)) {
      if (isDragging) {
        SafeCallbacks.Action(() => target.OnKISDragEnd(isCancelled: true));
      }
      dragTargets = dragTargets.Where((t, i) => t != target).ToArray();
    } else {
      DebugEx.Warning("Cannot unregister unknown target: {0}", target);
    }
  }
  #endregion

  #region Local utility methods
  /// <summary>Creates a drag icon object.</summary>
  /// <param name="dragIcon">The icon to show in the object.</param>
  void StartDragIcon(Texture dragIcon) {
    dragIconObj = UnityPrefabController.CreateInstance<UIKISInventorySlotDragIcon>(
        "KISDragIcon", UIMasterController.Instance.actionCanvas.transform);
    dragIconObj.slotImage = dragIcon;
  }

  /// <summary>Cleans up any leased state and unlocks game.</summary>
  void ClearLease(bool isCancelled) {
    Array.ForEach(dragTargets, t => SafeCallbacks.Action(() => t.OnKISDragEnd(isCancelled)));
    consumeItemsFn = null;
    cancelItemsLeaseFn = null;
    leasedItems = null;
    dragIconObj = null;
    if (dragTracker != null) {
      dragTracker.CancelLock();
      dragTracker = null;
    }
  }
  #endregion
}

}  // namespace
