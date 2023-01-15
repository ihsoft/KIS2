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
  public readonly ClickEvent pickupModeSwitchEvent = new("j");
  
  /// <summary>The key that toggles dragging tooltip visibility.</summary>
  [PersistentField("PickupMode/toggleTooltipKey")]
  public readonly ClickEvent toggleTooltipEvent = new("j");

  /// <summary>The key that activates the part attach mode.</summary>
  [PersistentField("PickupMode/attachKey")]
  public readonly ClickEvent switchAttachModeKey = new("k");

  /// <summary>The renderer to apply to the scene part that is being dragged.</summary>
  /// <remarks>If it's an empty string, than the shader on the part won't be changed.</remarks>
  [PersistentField("PickupMode/holoPartShader")]
  public string stdTransparentRenderer = "Transparent/Diffuse";

  /// <summary>The color and transparency of the holo model of the part being dragged.</summary>
  [PersistentField("PickupMode/holoColor")]
  public Color holoColor = new(0f, 1f, 1f, 0.7f);

  /// <summary>Scale adjuster to the target part attach nodes detection algorithm.</summary>
  /// <remarks>
  /// The bigger is the scale, the less precise hit to the attach node is needed to get the dragged item adjusted to it.
  /// </remarks>
  [PersistentField("PickupMode/attachNodePrecision")]
  public float attachNodePrecision = 1.0f;

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
    None,
    /// <summary>The controller is ready to process a pickup command.</summary>
    WaitingForPickup,
    /// <summary>The controller is in a pickup mode and expects user's input.</summary>
    PickupModePending,
    /// <summary>The is exactly one item being dragged.</summary>
    DraggingOneItem,
    /// <summary>The are _some_ items being dragged.</summary>
    DraggingMultipleItems,
  }

  /// <summary>The state machine of the controller.</summary>
  /// <remarks>
  /// If the state needs to be changed from the user input, use a delayed change (by one frame). Or else, the handlers
  /// from the different states may detect the same mouse/keyboard event.
  /// </remarks>
  readonly SimpleStateMachine<ControllerState> _controllerStateMachine = new(strict: false);
  #endregion

  #region Static key bindings for all handlers
  public readonly ClickEvent pickupItemFromSceneEvent = new("mouse0");
  public readonly ClickEvent putOnEquipmentFromSceneEvent = new("mouse1");
  public readonly ClickEvent pickupGroundPartFromSceneEvent = new("mouse1");
  public readonly ClickEvent dropItemToSceneEvent = new("mouse0");
  public readonly ClickEvent attachPartEvent = new("mouse0");
  public readonly ClickEvent rotateLeftEvent = new("a");
  public readonly ClickEvent rotateRightEvent = new("d");
  public readonly ClickEvent rotateResetEvent = new("space");
  public readonly ClickEvent toggleDropModeEvent = new("r");
  public readonly ClickEvent nodeCycleLeftEvent = new("w");
  public readonly ClickEvent nodeCycleRightEvent = new("s");
  #endregion

  #region State handlers
  readonly WaitingForPickupStateHandler _waitingForPickupStateHandlerHandler;
  readonly PickupStateHandler _pickupStateHandlerHandler;
  readonly DraggingOneItemStateHandler _draggingOneItemOneItemStateHandler;
  public FlightItemDragController() {
    _waitingForPickupStateHandlerHandler = new(this);
    _pickupStateHandlerHandler = new(this);
    _draggingOneItemOneItemStateHandler = new(this);
  }
  #endregion

  #region MonoBehaviour overrides
  void Awake() {
    DebugEx.Fine("[{0}] Controller started", nameof(FlightItemDragController));
    ConfigAccessor.ReadFieldsInType(GetType(), this);
    
    // Setup the controller state machine.
    _controllerStateMachine.onAfterTransition += (before, after) => {
      DebugEx.Fine("In-flight controller state changed: {0} => {1}", before, after);
    };
    _controllerStateMachine.AddStateHandlers(ControllerState.None);
    _controllerStateMachine.AddStateHandlers(
        ControllerState.WaitingForPickup,
        _ => _waitingForPickupStateHandlerHandler.Start(),
        _ => _waitingForPickupStateHandlerHandler.Stop());
    _controllerStateMachine.AddStateHandlers(
        ControllerState.PickupModePending,
        _ => _pickupStateHandlerHandler.Start(),
        _ => _pickupStateHandlerHandler.Stop());
    _controllerStateMachine.AddStateHandlers(
        ControllerState.DraggingOneItem,
        _ => _draggingOneItemOneItemStateHandler.Start(),
        _ => _draggingOneItemOneItemStateHandler.Stop());
    _controllerStateMachine.AddStateHandlers(ControllerState.DraggingMultipleItems);
    _controllerStateMachine.currentState = ControllerState.WaitingForPickup;

    KisItemDragController.RegisterTarget(this);
  }

  void OnDestroy() {
    DebugEx.Fine("[{0}] Controller stopped", nameof(FlightItemDragController));
    KisItemDragController.UnregisterTarget(this);

    // Ensure the state machines did their cleanups.
    _controllerStateMachine.currentState = null;
  }
  #endregion

  #region IKisDragTarget implementation
  /// <inheritdoc/>
  public Component unityComponent => this;

  /// <inheritdoc/>
  void IKisDragTarget.OnKisDragStart() {
    UpdateControllerState();
  }

  /// <inheritdoc/>
  void IKisDragTarget.OnKisDragEnd(bool isCancelled) {
    UpdateControllerState();
  }

  /// <inheritdoc/>
  bool IKisDragTarget.OnKisDrag(bool pointerMoved) {
    return _controllerStateMachine.currentState == ControllerState.DraggingOneItem;
  }

  /// <inheritdoc/>
  void IKisDragTarget.OnFocusTarget(IKisDragTarget newTarget) {
    UpdateControllerState();
  }
  #endregion

  #region API methods
  public void ToIdleState() {
    _controllerStateMachine.currentState = ControllerState.WaitingForPickup;
  }
  
  public void ToPickupState() {
    _controllerStateMachine.currentState = ControllerState.PickupModePending;
  }

  public void ToDropOneItemState() {
    _controllerStateMachine.currentState = ControllerState.DraggingOneItem;
  }
  #endregion

  #region Local utility methods
  /// <summary>Updates flight controller state to match the current drag controller state.</summary>
  void UpdateControllerState() {
    if (KisItemDragController.focusedTarget != null) {
      _controllerStateMachine.currentState = ControllerState.None;
    } else if (!KisItemDragController.isDragging) {
      _controllerStateMachine.currentState = ControllerState.WaitingForPickup;
    } else if (KisItemDragController.leasedItems.Length == 1) {
      _controllerStateMachine.currentState = ControllerState.DraggingOneItem;
    } else {
      _controllerStateMachine.currentState = ControllerState.DraggingMultipleItems;
    }
  }
  #endregion
}

}
