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
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>Controller that controls the inventory items movements between the inventories and the scene.</summary>
/// <remarks>
/// This controller is not made for dealing with actual KIS items movements or instantiating the parts. Instead, it
/// serves as a proxy to connect a provider, the class that offers item(s) for movement, and a target, the class that
/// can consume the item(s). The providers should pro-actively call the controller to offer the item(s) for dragging.
/// The targets must be reacting on their events to verify if they can/should consume anything from the controller, and
/// register themselves as targets if they can.
/// </remarks>
/// <seealso cref="IKisDragTarget"/>
// ReSharper disable once InconsistentNaming
public static class KisItemDragController {

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
    /// <summary>Indicates if user can cancel the dragged operation.</summary>
    /// <remarks>
    /// It affects the offered hints in GUI, as well as handling the appropriate action from the keyboard.
    /// </remarks>
    public bool canCancelInteractively;

    /// <summary>Tells if callbacks are being handling.</summary>
    /// <remarks>
    /// Not all actions with the controller are allowed in this mode. In particular, the consumption or cancelling are
    /// not allowed.
    /// </remarks>
    /// <seealso cref="IKisDragTarget.OnKisDrag"/>
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
      if (!_controlsLocked || !isDragging) {
        return;
      }
      if (canCancelInteractively) {
        _statusScreenMessage.message = CancelDraggingTxt.Format(CancelEvent);
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
      foreach (var target in _dragTargets) {
        canConsume |= SafeCallbacks.Func(() => target.OnKisDrag(pointerMoved), false);
      }
      isInCallbackCycle = false;
      dragIconObj.showNoGo = !canConsume;
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
      if (isDragging) {
        CancelItemsLease();
      }
      Hierarchy.SafeDestroy(gameObject);  // Ensure the tracking is over.
      DebugEx.Fine("KIS drag lock released");
    }
    #endregion
  }
  #endregion

  #region IKisItemDragController properties
  /// <summary>Tells if there are items being dragged by the controller.</summary>
  /// <seealso cref="leasedItems"/>
  public static bool isDragging => leasedItems != null;

  /// <summary>Items that are currently being dragged.</summary>
  /// <value>The list of items or <c>null</c> if nothing is being dragged.</value>
  /// <seealso cref="isDragging"/>
  /// <seealso cref="LeaseItems"/>
  public static InventoryItem[] leasedItems { get; private set; }

  /// <summary>Drag icon object for the current drag operation.</summary>
  /// <remarks>
  /// The active state of this object can be adjusted by the third-party scripts, but it must not
  /// be destroyed from the outside.
  /// </remarks>
  /// <value>The object or <c>null</c> if nothing is being dragged.</value>
  /// <seealso cref="isDragging"/>
  public static UiKisInventorySlotDragIcon dragIconObj {
    get => _dragIconObj;
    private set {
      Hierarchy.SafeDestroy(_dragIconObj);
      _dragIconObj = value;
    }
  }
  static UiKisInventorySlotDragIcon _dragIconObj;

  /// <summary>Target that currently has the pointer focus.</summary>
  /// <remarks>
  /// This property reflects what target is currently being hovered over by the mouse pointer. Any Unity game object can
  /// be that target if it has at least one component that implements the interface. When an object sets itself as a
  /// target it's assumed that object will control all the dragging logic, and the other drag listeners should stop
  /// acting.
  /// </remarks>
  /// <value>The target or <c>null</c> of there is none.</value>
  /// <seealso cref="SetFocusedTarget"/>
  public static IKisDragTarget focusedTarget { get; private set; }
  #endregion

  #region Local constants, properties and fields
  /// <summary>Keyboard event that cancels the dragging mode.</summary>
  static readonly Event CancelEvent = Event.KeyboardEvent("[esc]");

  /// <summary>Targets that need to get updated when dragging state is changed.</summary>
  static IKisDragTarget[] _dragTargets = new IKisDragTarget[0];

  /// <summary>Current lock tracking object.</summary>
  /// <remarks>
  /// This object only exists when there is an active dragging operation. Its main purpose is
  /// tracking the input locking state and notifying the drag targets.
  /// </remarks>
  /// <seealso cref="_dragTargets"/>
  static DragModeTracker _dragTracker;

  /// <summary>Action that is called when the dragged items being consumed.</summary>
  /// <remarks>
  /// The callback can return <c>false</c> to indicate that the consumption is not allowed. It will
  /// be treated as an error situation, but the game consistency won't suffer.
  /// </remarks>
  static Func<bool> _consumeItemsFn;

  /// <summary>Action that is called when the dragging is cancelled.</summary>
  /// <remarks>It's a cleanup action. It must never fail.</remarks>
  static Action _cancelItemsLeaseFn;

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
  /// <summary>Offers items for the dragging.</summary>
  /// <remarks>Items can belong to different inventories. The items can only be consumed all or none.</remarks>
  /// <param name="dragIcon">The icon that will be representing this operation.</param>
  /// <param name="items">
  /// The items being offered. The caller must ensure these items won't change their state between the start and the end
  /// of the drag operation. If this is not possible, the consume method must do verification and deny the operation if
  /// the change has occured.
  /// </param>
  /// <param name="consumeItemsFn">
  /// The function that will be called before consuming the items by the target. This function can cancel the operation,
  /// but it will be treated as an error by the target.
  /// </param>
  /// <param name="cancelItemsLeaseFn">
  /// The cleanup action that is called when the drag operation is cancelled. It's called before the
  /// <see cref="leasedItems"/> are cleaned up. This action must never fail.
  /// </param>
  /// <param name="allowInteractiveCancel">
  /// Indicates if user can cancel the drag operation from the keyboard. By default, the user MAY do this. If the
  /// provider that leases the items cannot allow it, it has to implement its own interactive approach.
  /// </param>
  /// <returns><c>true</c> if dragging has successfully started.</returns>
  /// <seealso cref="leasedItems"/>
  /// <seealso cref="ConsumeItems"/>
  /// <seealso cref="InventoryItem.isLocked"/>
  /// <seealso cref="IKisDragTarget.OnKisDragStart"/>
  public static bool LeaseItems(
      Texture dragIcon, ICollection<InventoryItem> items, Func<bool> consumeItemsFn, Action cancelItemsLeaseFn,
      bool allowInteractiveCancel = true) {
    ArgumentGuard2.HasElements(items, nameof(items));
    if (isDragging) {
      DebugEx.Error("Cannot start a new dragging: oldLeasedItems={0}, newLeasedItems={1}",
                    leasedItems.Length, items.Count);
      return false;
    }
    ArgumentGuard.NotNull(dragIcon, nameof(dragIcon));
    ArgumentGuard.NotNull(consumeItemsFn, nameof(consumeItemsFn));
    ArgumentGuard.NotNull(cancelItemsLeaseFn, nameof(cancelItemsLeaseFn));
    if (items.Count == 0) {
      DebugEx.Error("Cannot start dragging for an empty items list");
      return false;
    }
    DebugEx.Info("Leasing items: count={0}", items.Count);
    leasedItems = items.ToArray();
    _consumeItemsFn = consumeItemsFn;
    _cancelItemsLeaseFn = cancelItemsLeaseFn;
    StartDragIcon(dragIcon);
    Array.ForEach(_dragTargets, t => SafeCallbacks.Action(t.OnKisDragStart));
    _dragTracker = new GameObject().AddComponent<DragModeTracker>();
    _dragTracker.canCancelInteractively = allowInteractiveCancel;
    return true;
  }

  /// <summary>Indicates that the target is willing to consume the dragged items.</summary>
  /// <remarks>
  /// By calling this method the caller is stating that it's ready to take ownership to the <see cref="leasedItems"/>.
  /// If this method returns success, then the dragging mode ends.
  /// </remarks>
  /// <returns>
  /// The items to consume, or <c>null</c> if the provider refused the complete the deal. In the latter case the
  /// dragging operation stays running and unchanged.
  /// </returns>
  /// <seealso cref="leasedItems"/>
  /// <seealso cref="IKisDragTarget.OnKisDragEnd"/>
  public static InventoryItem[] ConsumeItems() {
    if (!isDragging) {
      DebugEx.Error("Cannot consume items since nothing is being dragged");
      return null;
    }
    if (_dragTracker.isInCallbackCycle) {
      throw new InvalidOperationException("Cannot consume items from a drag callback!");
    }
    if (!_consumeItemsFn()) {
      DebugEx.Warning("Items not consumed: provider refused the deal");
      return null;
    }
    var consumedItems = new List<InventoryItem>();
    foreach (var leasedItem in leasedItems) {
      // The items that don't belong to an inventory will get erased on leased items clear.
      if (leasedItem.inventory == null) {
        // Clearing the lease will erase the item.
        consumedItems.Add(leasedItem);
        continue;
      }
      // Remove the item from the inventory to have it erased once the lease has released.
      var detachedItem = leasedItem.inventory.DeleteItem(leasedItem.itemId);
      if (detachedItem != null) {
        consumedItems.Add(detachedItem);
      } else {
        // The inventory can block deletion for any reason, but it's not normally expected in the consume action.
        DebugEx.Warning(
            "Cannot delete item from inventory: itemId={0}, isLocked={1}", leasedItem.itemId, leasedItem.isLocked);
      }
    }
    DebugEx.Info("Consumed {0} items", consumedItems.Count);
    ClearLease(isCancelled: false);
    return consumedItems.ToArray();
  }

  /// <summary>Cancels the current dragging operation.</summary>
  /// <seealso cref="leasedItems"/>
  /// <seealso cref="IKisDragTarget.OnKisDragEnd"/>
  public static void CancelItemsLease() {
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

  /// <summary>Sets the target Unity object that is currently owns the dragging focus.</summary>
  /// <remarks>Only the Unity objects can get the focus.</remarks>
  /// <remarks>
  /// When the focused control looses focus, it must call this method with <c>null</c> to indicate that the focus has
  /// been released.
  /// </remarks>
  /// <param name="newTarget">The object that claims ownership on the focus.</param>
  /// <seealso cref="focusedTarget"/>
  /// <seealso cref="IKisDragTarget.unityComponent"/>
  public static void SetFocusedTarget(IKisDragTarget newTarget) {
    if (focusedTarget != newTarget) {
      var oldName = focusedTarget?.unityComponent != null ? focusedTarget.unityComponent.name : null;
      var newName = newTarget?.unityComponent != null ? newTarget.unityComponent.name : null;
      DebugEx.Fine("Focus target changed: old={0}, new={1}", oldName, newName);
      focusedTarget = newTarget;
      Array.ForEach(_dragTargets, t => SafeCallbacks.Action(() => t.OnFocusTarget(newTarget)));
    }
  }

  /// <summary>Registers a drop target that will be notified about the dragged items status.</summary>
  /// <remarks>
  /// The drag controller will ask all the registered targets about the currently dragged items. If none of them can
  /// accept the drop, then the controller's UI will make it clear to the user.
  /// <p>
  /// If a target is registered when the dragging state is ON, then this target will immediately get
  /// <see cref="IKisDragTarget.OnKisDragStart"/>.
  /// </p>
  /// </remarks>
  /// <param name="target">The target to register.</param>
  /// <seealso cref="IKisDragTarget.OnKisDrag"/>
  public static void RegisterTarget(IKisDragTarget target) {
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

  /// <summary>Unregisters the drop target.</summary>
  /// <remarks>
  /// It's a cleanup method. It can be called even if the target is not currently registered. If a target is
  /// unregistered when the dragging state is ON, then this target will immediately get
  /// <see cref="IKisDragTarget.OnKisDragEnd"/>.
  /// </remarks>
  /// <param name="target">The target to unregister.</param>
  public static void UnregisterTarget(IKisDragTarget target) {
    ArgumentGuard.NotNull(target, nameof(target));
    if (!_dragTargets.Contains(target)) {
      return;
    }
    if (isDragging) {
      SafeCallbacks.Action(() => target.OnKisDragEnd(isCancelled: true));
    }
    _dragTargets = _dragTargets.Where((t, _) => t != target).ToArray();
  }
  #endregion

  #region Local utility methods
  /// <summary>Creates a drag icon object.</summary>
  /// <param name="dragIcon">The icon to show in the object.</param>
  static void StartDragIcon(Texture dragIcon) {
    dragIconObj = UnityPrefabController.CreateInstance<UiKisInventorySlotDragIcon>(
        "KISDragIcon", UIMasterController.Instance.actionCanvas.transform);
    dragIconObj.slotImage = dragIcon;
  }

  /// <summary>Cleans up any leased state and unlocks game.</summary>
  static void ClearLease(bool isCancelled) {
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
