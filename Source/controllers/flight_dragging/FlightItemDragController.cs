// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KISAPIv2;
using KSPDev.ConfigUtils;
using KSPDev.InputUtils;
using KSPDev.LogUtils;
using KSPDev.ProcessingUtils;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2.controllers.flight_dragging {

/// <summary>Controller that deals with dragged items in the flight scenes.</summary>
[PersistentFieldsDatabase("KIS2/settings2/KISConfig")]
[KSPAddon(KSPAddon.Startup.Flight, false /*once*/)]
public sealed class FlightItemDragController : MonoBehaviour, IKisDragTarget {
  #region Configuration
  // ReSharper disable FieldCanBeMadeReadOnly.Local
  // ReSharper disable ConvertToConstant.Local

  /// <summary>The key that activates the in-flight pickup mode.</summary>
  /// <remarks>
  /// It's a standard keyboard event definition. Even though it can have modifiers, avoid specifying them since it may
  /// affect the UX experience.
  /// </remarks>
  [PersistentField("PickupMode/actionKey")]
  string _flightActionKey = "j";

  /// <summary>The key that toggles dragging tooltip visibility.</summary>
  /// <remarks>
  /// It's a standard keyboard event definition. Even though it can have modifiers, avoid specifying them since it may
  /// affect the UX experience.
  /// </remarks>
  [PersistentField("PickupMode/toggleTooltipKey")]
  string _toggleTooltipKey = "j";

  /// <summary>The renderer to apply to the scene part that is being dragged.</summary>
  /// <remarks>If it's an empty string, than the shader on the part won't be changed.</remarks>
  [PersistentField("PickupMode/holoPartShader")]
  public string stdTransparentRenderer = "Transparent/Diffuse";

  /// <summary>The color and transparency of the holo model of the part being dragged.</summary>
  [PersistentField("PickupMode/holoColor")]
  public Color holoColor = new(0f, 1f, 1f, 0.7f);

  /// <summary>Distance from the camera of the object that cannot be placed anywhere.</summary>
  /// <remarks>If an item cannot be dropped, it will be "hanging" at the camera at this distance.</remarks>
  /// <seealso cref="maxRaycastDistance"/>
  [PersistentField("PickupMode/hangingObjectDistance")]
  public float hangingObjectDistance = 10.0f;

  /// <summary>Maximum distance from the current camera to the hit point, where an item can be dropped.</summary>
  /// <remarks>
  /// This setting limits how far the mod will be looking for the possible placement location, but it doesn't define the
  /// maximum interaction distance.
  /// </remarks>
  /// <seealso cref="hangingObjectDistance"/>
  [PersistentField("PickupMode/maxRaycastDistance")]
  public float maxRaycastDistance = 50;

  // ReSharper enable FieldCanBeMadeReadOnly.Local
  // ReSharper enable ConvertToConstant.Local
  #endregion

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

  #region Key bindings for all handlers
  // Configurable.
  public ClickEvent toggleTooltipEvent;
  public ClickEvent pickupModeSwitchEvent;
  // Static.
  public readonly ClickEvent pickupItemFromSceneEvent = new(Event.KeyboardEvent("mouse0"));
  public readonly ClickEvent dropItemToSceneEvent = new(Event.KeyboardEvent("mouse0"));
  public readonly ClickEvent rotateLeftEvent = new(Event.KeyboardEvent("a"));
  public readonly ClickEvent rotateRightEvent = new(Event.KeyboardEvent("d"));
  public readonly ClickEvent rotateResetEvent = new(Event.KeyboardEvent("space"));
  public readonly ClickEvent toggleDropModeEvent = new(Event.KeyboardEvent("r"));
  public readonly ClickEvent nodeCycleLeftEvent = new(Event.KeyboardEvent("w"));
  public readonly ClickEvent nodeCycleRightEvent = new(Event.KeyboardEvent("s"));
  #endregion

  #region State handlers
  readonly IdleStateHandler _idleStateHandlerHandler;
  readonly PickupStateHandler _pickupStateHandlerHandler;
  readonly DraggingStateHandler _draggingStateHandlerHandler;
  public FlightItemDragController() {
    _idleStateHandlerHandler = new(this);
    _pickupStateHandlerHandler = new(this);
    _draggingStateHandlerHandler = new(this);
  }
  #endregion

  #region MonoBehaviour overrides
  void Awake() {
    DebugEx.Fine("[{0}] Controller started", nameof(FlightItemDragController));

    ConfigAccessor.ReadFieldsInType(GetType(), this);
    pickupModeSwitchEvent = new ClickEvent(Event.KeyboardEvent(_flightActionKey));
    toggleTooltipEvent = new(Event.KeyboardEvent(_toggleTooltipKey));
    
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
