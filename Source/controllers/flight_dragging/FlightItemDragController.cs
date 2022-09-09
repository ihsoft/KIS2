// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KISAPIv2;
using KSPDev.LogUtils;
using KSPDev.ProcessingUtils;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2.controllers.flight_dragging {

/// <summary>Controller that deals with dragged items in the flight scenes.</summary>
[KSPAddon(KSPAddon.Startup.Flight, false /*once*/)]
public sealed class FlightItemDragController : MonoBehaviour, IKisDragTarget {
  #region Local fields and properties
  /// <summary>The exhaustive definition of the controller states.</summary>
  enum ControllerState {
    /// <summary>The controller isn't handling anything.</summary>
    Idle,
    /// <summary>The controller is in a pickup mode and expects user's input.</summary>
    PickupModePending,
    /// <summary>The items are being dragged.</summary>
    DraggingItems,
  }

  /// <summary>The state machine of the controller.</summary>
  /// <remarks>
  /// If the state needs to be changed from the user input, use a delayed change (by one frame). Or else, the handlers
  /// from the different states may detect the same mouse/keyboard event.
  /// </remarks>
  readonly SimpleStateMachine<ControllerState> _controllerStateMachine = new(strict: false);
  #endregion

  #region State handlers
  readonly IdleStateHandler _idleStateHandlerHandler = new();
  readonly PickupStateHandler _pickupStateHandlerHandler = new();
  readonly DraggingStateHandler _draggingStateHandlerHandler = new();
  #endregion

  #region MonoBehaviour overrides
  void Awake() {
    DebugEx.Fine("[{0}] Controller started", nameof(FlightItemDragController));
    _idleStateHandlerHandler.Init(this);
    _pickupStateHandlerHandler.Init(this);
    _draggingStateHandlerHandler.Init(this);
    
    // Setup the controller state machine.
    _controllerStateMachine.onAfterTransition += (before, after) => {
      DebugEx.Fine("In-flight controller state changed: {0} => {1}", before, after);
    };
    _controllerStateMachine.AddStateHandlers(
        ControllerState.Idle,
        _ => _idleStateHandlerHandler.Start(),
        _ => _idleStateHandlerHandler.Stop());
    _controllerStateMachine.AddStateHandlers(
        ControllerState.PickupModePending,
        _ => _pickupStateHandlerHandler.Start(),
        _ => _pickupStateHandlerHandler.Stop());
    _controllerStateMachine.AddStateHandlers(
        ControllerState.DraggingItems,
        _ => _draggingStateHandlerHandler.Start(),
        _ => _draggingStateHandlerHandler.Stop());
    _controllerStateMachine.currentState = ControllerState.Idle;

    KisApi.ItemDragController.RegisterTarget(this);
  }

  void OnDestroy() {
    DebugEx.Fine("[{0}] Controller stopped", nameof(FlightItemDragController));
    KisApi.ItemDragController.UnregisterTarget(this);

    // Ensure the state machines did their cleanups.
    _controllerStateMachine.currentState = null;
  }
  #endregion

  #region IKisDragTarget implementation
  /// <inheritdoc/>
  public Component unityComponent => this;

  /// <inheritdoc/>
  void IKisDragTarget.OnKisDragStart() {
    _controllerStateMachine.currentState = ControllerState.DraggingItems;
  }

  /// <inheritdoc/>
  void IKisDragTarget.OnKisDragEnd(bool isCancelled) {
    _controllerStateMachine.currentState = ControllerState.Idle;
  }

  /// <inheritdoc/>
  bool IKisDragTarget.OnKisDrag(bool pointerMoved) {
    return _draggingStateHandlerHandler.CanHandleDraggedItems();
  }

  /// <inheritdoc/>
  void IKisDragTarget.OnFocusTarget(IKisDragTarget newTarget) {
  }
  #endregion

  #region API methods
  public void ToIdleState() {
    _controllerStateMachine.currentState = ControllerState.Idle;
  }
  
  public void ToPickupState() {
    _controllerStateMachine.currentState = ControllerState.PickupModePending;
  }

  public void ToDropState() {
    _controllerStateMachine.currentState = ControllerState.DraggingItems;
  }
  #endregion
}

}
