﻿// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections;
using System.Collections.Generic;
using KISAPIv2;
using KSPDev.InputUtils;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using System.Linq;
using System.Reflection;
using KSP.UI;
using KSPDev.ConfigUtils;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.ProcessingUtils;
using KSPDev.Unity;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>Controller that deals with dragged items in the flight scenes.</summary>
[KSPAddon(KSPAddon.Startup.Flight, false /*once*/)]
[PersistentFieldsDatabase("KIS2/settings2/KISConfig")]
sealed class FlightItemDragController : MonoBehaviour, IKisDragTarget {
  #region Localizable strings
  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> TakeFocusedPartHint = new(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Grab the part",
      description: "The tooltip status to present when the KIS grabbing mode is activated, but no part is being"
      + " focused.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> DraggedPartDropPartHint = new(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Drop at the part",
      description: "TBD");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> DraggedPartDropSurfaceHint = new(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Put on the ground",
      description: "TBD");
  
  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message VesselPlacementModeHint = new(
      "",
      defaultTemplate: "Vessel placement mode",
      description: "TBD");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> ShowCursorTooltipHint = new(
      "",
      defaultTemplate: "[<<1>>]: Show tooltip",
      description: "TBD");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> HideCursorTooltipHint = new(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Hide tooltip",
      description: "TBD");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType, KeyboardEventType> RotateBy30DegreesHint = new(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]/[<<2>>]</color></b>: Rotate by 30 degrees",
      description: "TBD");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType, KeyboardEventType> RotateBy5DegreesHint = new(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]/[<<2>>]</color></b>: Rotate by 5 degrees",
      description: "TBD");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message<KeyboardEventType> RotateResetHint = new(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Reset rotation",
      description: "TBD");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message/*"/>
  static readonly Message CannotGrabHierarchyTooltipMsg = new(
      "",
      defaultTemplate: "Cannot grab a hierarchy",
      description: "It's a temp string. DO NOT localize it!");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<int> CannotGrabHierarchyTooltipDetailsMsg = new(
      "",
      defaultTemplate: "<<1>> part(s) attached",
      description: "It's a temp string. DO NOT localize it!");
  
  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message PutOnTheGroundHint = new(
      "",
      defaultTemplate: "Put on the ground",
      description: "TBD");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message AlignAtThePartHint = new(
      "",
      defaultTemplate: "Drop at the part",
      description: "TBD");
  #endregion

  #region Configuration
  // ReSharper disable FieldCanBeMadeReadOnly.Local
  // ReSharper disable FieldCanBeMadeReadOnly.Global
  // ReSharper disable ConvertToConstant.Global
  // ReSharper disable MemberCanBePrivate.Global
  // ReSharper disable ConvertToConstant.Local

  /// <summary>The renderer to apply to the scene part that is being dragged.</summary>
  /// <remarks>If it's an empty string, than the shader on the part won't be changed.</remarks>
  [PersistentField("PickupMode/holoPartShader")]
  static string _stdTransparentRenderer = "Transparent/Diffuse";

  /// <summary>The color and transparency of the holo model of the part being dragged.</summary>
  [PersistentField("PickupMode/holoColor")]
  static Color _holoColor = new(0f, 1f, 1f, 0.7f);

  /// <summary>Distance from the camera of the object that cannot be placed anywhere.</summary>
  /// <remarks>If an item cannot be dropped, it will be "hanging" at the camera at this distance.</remarks>
  /// <seealso cref="_maxRaycastDistance"/>
  [PersistentField("PickupMode/hangingObjectDistance")]
  static float _hangingObjectDistance = 10.0f;

  /// <summary>Maximum distance from the current camera to the hit point, where an item can be dropped.</summary>
  /// <remarks>
  /// This setting limits how far the mod will be looking for the possible placement location, but it doesn't define the
  /// maximum interaction distance.
  /// </remarks>
  /// <seealso cref="_hangingObjectDistance"/>
  [PersistentField("PickupMode/maxRaycastDistance")]
  static float _maxRaycastDistance = 50;

  /// <summary>The key that activates the in-flight pickup mode.</summary>
  /// <remarks>
  /// It's a standard keyboard event definition. Even though it can have modifiers, avoid specifying them since it may
  /// affect the UX experience.
  /// </remarks>
  [PersistentField("PickupMode/actionKey")]
  static string _flightActionKey = "j";

  /// <summary>The key that toggles dragging tooltip visibility.</summary>
  /// <remarks>
  /// It's a standard keyboard event definition. Even though it can have modifiers, avoid specifying them since it may
  /// affect the UX experience.
  /// </remarks>
  [PersistentField("PickupMode/toggleTooltipKey")]
  static string _toggleTooltipKey = "t";

  // ReSharper enable FieldCanBeMadeReadOnly.Local
  // ReSharper enable FieldCanBeMadeReadOnly.Global
  // ReSharper enable ConvertToConstant.Global
  // ReSharper enable MemberCanBePrivate.Global
  // ReSharper enable ConvertToConstant.Local
  #endregion

  #region Event static configs
  static readonly ClickEvent DropItemToSceneEvent = new(Event.KeyboardEvent("mouse0"));
  static readonly ClickEvent PickupItemFromSceneEvent = new(Event.KeyboardEvent("mouse0"));
  static Event _pickupModeSwitchEvent = Event.KeyboardEvent(_flightActionKey);
  static ClickEvent _toggleTooltipEvent = new(Event.KeyboardEvent(_toggleTooltipKey));

  // TODO(ihsoft): Make all values below configurable. 
  static readonly ClickEvent Rotate30LeftEvent = new(Event.KeyboardEvent("a"));
  static readonly ClickEvent Rotate30RightEvent = new(Event.KeyboardEvent("d"));
  static readonly ClickEvent Rotate5LeftEvent = new(Event.KeyboardEvent("q"));
  static readonly ClickEvent Rotate5RightEvent = new(Event.KeyboardEvent("e"));
  static readonly ClickEvent RotateResetEvent = new(Event.KeyboardEvent("space"));
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

  /// <summary>Defines the currently focused pickup target.</summary>
  /// <remarks>The <c>null</c> state is used to indicate that nothing of the interest is being focused.</remarks>
  enum PickupTarget {
    /// <summary>A lone part or the last child of a vessel is being hovered.</summary>
    SinglePart,
    /// <summary>The part focused has some children.</summary>
    PartAssembly,
  }

  /// <summary>The events state machine to control the pickup stage.</summary>
  readonly EventsHandlerStateMachine<PickupTarget> _pickupTargetEventsHandler = new();

  /// <summary>Defines the currently focused drop target.</summary>
  /// <remarks>The <c>null</c> state is used to indicate that nothing of the interest is being focused.</remarks>
  enum DropTarget {
    /// <summary>The mouse cursor doesn't hit anything reasonable.</summary>
    Nothing,
    /// <summary>The mouse cursor hovers over the surface.</summary>
    Surface,
    /// <summary>The mouse cursor hovers over a part.</summary>
    Part,
    /// <summary>The mouse cursor hovers over a KIS inventory part.</summary>
    KisInventory,
    /// <summary>The mouse cursor hovers over a kerbal (they always have KIS inventory).</summary>
    /// <remarks>This state is favored over the <see cref="KisInventory"/> when a kerbal is being focused.</remarks>
    KerbalInventory,
    /// <summary>The mouse cursor hovers over a KIS target.</summary>
    /// <remarks>Such targets handle the drop logic themselves. So, the controller just stops interfering.</remarks>
    KisTarget,
  }

  /// <summary>The events state machine to control the drop stage.</summary>
  readonly EventsHandlerStateMachine<DropTarget> _dropTargetEventsHandler = new();

  /// <summary>The tooltip that is currently being presented.</summary>
  UIKISInventoryTooltip.Tooltip _currentTooltip;
  #endregion

  #region MonoBehaviour overrides
  void Awake() {
    DebugEx.Fine("[{0}] Controller started", nameof(FlightItemDragController));
    ConfigAccessor.ReadFieldsInType(GetType(), null); // Read the static fields.
    _pickupModeSwitchEvent = Event.KeyboardEvent(_flightActionKey);
    _toggleTooltipEvent = new ClickEvent(Event.KeyboardEvent(_toggleTooltipKey));

    // Setup the controller state machine.
    _controllerStateMachine.onAfterTransition += (before, after) => {
      DebugEx.Fine("In-flight controller state changed: {0} => {1}", before, after);
    };
    _controllerStateMachine.AddStateHandlers(
        ControllerState.Idle,
        _ => { _trackIdleStateCoroutine = StartCoroutine(TrackIdleStateCoroutine()); },
        _ => {
          if (_trackIdleStateCoroutine != null) {
            StopCoroutine(_trackIdleStateCoroutine);
          }
        });
    _controllerStateMachine.AddStateHandlers(
        ControllerState.PickupModePending,
        _ => { _trackPickupStateCoroutine = StartCoroutine(TrackPickupStateCoroutine()); },
        _ => CleanupTrackPickupState());
    _controllerStateMachine.AddStateHandlers(
        ControllerState.DraggingItems,
        _ => { _trackDraggingStateCoroutine = StartCoroutine(TrackDraggingStateCoroutine()); },
        _ => CleanupTrackDraggingState());
    _controllerStateMachine.currentState = ControllerState.Idle;

    // Setup the pickup target state machine.
    _pickupTargetEventsHandler.ONAfterTransition += (oldState, newState) => {
      DebugEx.Fine("Pickup target state changed: {0} => {1}", oldState, newState);
    };
    _pickupTargetEventsHandler.DefineAction(
        PickupTarget.SinglePart, TakeFocusedPartHint, PickupItemFromSceneEvent, HandleScenePartPickupAction);

    // Setup the drop target state machine.
    _dropTargetEventsHandler.ONAfterTransition += (oldState, newState) => {
      DebugEx.Fine("Drop target state changed: {0} => {1}", oldState, newState);
    };
    _dropTargetEventsHandler.DefineAction(
        DropTarget.Surface, DraggedPartDropSurfaceHint, DropItemToSceneEvent, CreateVesselFromDraggedItem);
    _dropTargetEventsHandler.DefineAction(
        DropTarget.Part, DraggedPartDropPartHint, DropItemToSceneEvent, CreateVesselFromDraggedItem);
    _dropTargetEventsHandler.DefineAction(
        DropTarget.KerbalInventory, DraggedPartDropPartHint, DropItemToSceneEvent, CreateVesselFromDraggedItem);
    _dropTargetEventsHandler.DefineAction(
        DropTarget.KisInventory, DraggedPartDropPartHint, DropItemToSceneEvent, CreateVesselFromDraggedItem);
    KisApi.ItemDragController.RegisterTarget(this);
  }

  void OnDestroy() {
    DebugEx.Fine("[{0}] Controller stopped", nameof(FlightItemDragController));
    KisApi.ItemDragController.UnregisterTarget(this);

    // Ensure the state machines did their cleanups.
    _controllerStateMachine.currentState = null;
    _pickupTargetEventsHandler.currentState = null;
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
    if (_dropTargetEventsHandler.currentState is DropTarget.Nothing or DropTarget.KisTarget) {
      // In these states the controller doesn't deal with the dragged item(s).
      return false;
    }
    return KisApi.ItemDragController.leasedItems.Length == 1;
  }

  /// <inheritdoc/>
  void IKisDragTarget.OnFocusTarget(IKisDragTarget newTarget) {
  }
  #endregion

  #region Idle state handling
  /// <summary>Handles the keyboard/mouse events when the controller is idle.</summary>
  /// <seealso cref="_pickupModeSwitchEvent"/>
  /// <seealso cref="_controllerStateMachine"/>
  IEnumerator TrackIdleStateCoroutine() {
    while (_controllerStateMachine.currentState == ControllerState.Idle) {
      // Don't handle the keys in the same frame as the coroutine has started in to avoid double actions.
      yield return null;

      if (Input.anyKey && EventChecker2.CheckEventActive(_pickupModeSwitchEvent)) {
        _controllerStateMachine.currentState = ControllerState.PickupModePending;
        break;
      }
    }
    // No code beyond this point! The coroutine is explicitly killed from the state machine.
  }
  Coroutine _trackIdleStateCoroutine;
  #endregion

  #region Pickup state handling
  /// <summary>The part that is currently hovered over in the part pickup mode.</summary>
  /// <remarks>Setting this property affects the hovered part(s) highlighting</remarks>
  /// <value>The part or <c>null</c> if no acceptable part is being hovered over.</value>
  Part targetPickupPart {
    get => _targetPickupPart;
    set {
      if (_targetPickupPart == value) {
        return;
      }
      if (_targetPickupPart != null) {
        _targetPickupPart.SetHighlight(false, recursive: true);
        _targetPickupPart.SetHighlightDefault();
        _targetPickupItem = null;
      }
      _targetPickupPart = value;
      if (_targetPickupPart != null) {
        _targetPickupPart.SetHighlightType(Part.HighlightType.AlwaysOn);
        _targetPickupPart.SetHighlight(true, recursive: true);
        if (_targetPickupPart.children.Count == 0) {
          _targetPickupItem = InventoryItemImpl.FromPart(_targetPickupPart);
          _targetPickupItem.materialPart = _targetPickupPart;
        }
      }
    }
  }
  Part _targetPickupPart;
  InventoryItem _targetPickupItem;

  /// <summary>Handles the keyboard and mouse events when KIS pickup mode is enabled in flight.</summary>
  /// <remarks>
  /// It checks if the modifier key is released and brings the controller to the idle state if that's the case.
  /// </remarks>
  /// <seealso cref="_pickupModeSwitchEvent"/>
  /// <seealso cref="_controllerStateMachine"/>
  /// <seealso cref="_pickupTargetEventsHandler"/>
  IEnumerator TrackPickupStateCoroutine() {
    var beingSettledField = typeof(ModuleCargoPart).GetField(
        "beingSettled", BindingFlags.Instance | BindingFlags.NonPublic);
    if (beingSettledField == null) {
      DebugEx.Error("Cannot find beingSettled field in cargo module");
    }
    while (_controllerStateMachine.currentState == ControllerState.PickupModePending) {
      CrewHatchController.fetch.DisableInterface(); // No hatch actions while we're targeting the part!

      var hoveredPart = Mouse.HoveredPart != null && !Mouse.HoveredPart.isVesselEVA ? Mouse.HoveredPart : null;
      if (hoveredPart != null && hoveredPart.isCargoPart() && beingSettledField != null) {
        var cargoModule = hoveredPart.FindModuleImplementing<ModuleCargoPart>();
        var isBeingSettling = (bool) beingSettledField.GetValue(cargoModule);
        if (isBeingSettling) {
          hoveredPart = null;
        }
      }
      targetPickupPart = hoveredPart;
      
      if (targetPickupPart == null) {
        _pickupTargetEventsHandler.currentState = null;
      } else if (targetPickupPart.children.Count == 0) {
        _pickupTargetEventsHandler.currentState = PickupTarget.SinglePart;
      } else {
        _pickupTargetEventsHandler.currentState = PickupTarget.PartAssembly;
      }
      if (!Input.anyKey || !EventChecker2.CheckEventActive(_pickupModeSwitchEvent)) {
        _controllerStateMachine.currentState = ControllerState.Idle;
        break;
      }
      UpdatePickupTooltip();

      // Don't handle the keys in the same frame as the coroutine has started in to avoid the double actions.
      yield return null;

      _pickupTargetEventsHandler.HandleActions();
    }
    // No code beyond this point! The coroutine may be explicitly killed from the state machine.
  }
  Coroutine _trackPickupStateCoroutine;

  /// <summary>Cleans ups any state created in <see cref="TrackPickupStateCoroutine"/>.</summary>
  /// <remarks>
  /// Doing the cleanup in the coroutine itself is a bad idea due to it can die at any moment. Instead, the
  /// <see cref="_controllerStateMachine"/> should take care about it when the state is changed.
  /// </remarks>
  void CleanupTrackPickupState() {
    CrewHatchController.fetch.EnableInterface();
    _pickupTargetEventsHandler.currentState = null;
    targetPickupPart = null;
    DestroyCurrentTooltip();
    if (_trackPickupStateCoroutine != null) {
      StopCoroutine(_trackPickupStateCoroutine);
      _trackPickupStateCoroutine = null;
    }
  }

  /// <summary>Updates or creates the in-flight tooltip with the part data.</summary>
  /// <remarks>It's intended to be called on every frame update. This method must be efficient.</remarks>
  /// <seealso cref="DestroyCurrentTooltip"/>
  /// <seealso cref="targetPickupPart"/>
  void UpdatePickupTooltip() {
    if (targetPickupPart == null) {
      DestroyCurrentTooltip();
      return;
    }
    CreateTooltip();
    if (_pickupTargetEventsHandler.currentState == PickupTarget.SinglePart) {
      KisContainerWithSlots.UpdateTooltip(_currentTooltip, new[] { _targetPickupItem });
    } else if (_pickupTargetEventsHandler.currentState == PickupTarget.PartAssembly) {
      // TODO(ihsoft): Implement!
      _currentTooltip.ClearInfoFields();
      _currentTooltip.title = CannotGrabHierarchyTooltipMsg;
      _currentTooltip.baseInfo.text =
          CannotGrabHierarchyTooltipDetailsMsg.Format(CountChildrenInHierarchy(targetPickupPart));
    }
    _currentTooltip.hints = _pickupTargetEventsHandler.GetHints();
    _currentTooltip.UpdateLayout();
  }

  /// <summary>Handles the in-flight part/hierarchy pick up event action.</summary>
  /// <remarks>
  /// <p>It's required that there is a part being hovered. This part will be offered for the KIS dragging action.</p>
  /// <p>If the part being consumed is attached to a vessel, it will get decoupled first.</p>
  /// <p>
  /// The consumed part will <i>DIE</i>. Which will break the link between the part and the item. And it may have effect
  /// on the contracts state if the part was a part of any.
  /// </p>
  /// </remarks>
  void HandleScenePartPickupAction() {
    var leasedItem = _targetPickupItem;
    KisApi.ItemDragController.LeaseItems(
        KisApi.PartIconUtils.MakeDefaultIcon(leasedItem.materialPart),
        new[] { leasedItem },
        () => { // The consume action.
          var consumedPart = leasedItem.materialPart;
          if (consumedPart != null) {
            if (consumedPart.parent != null) {
              DebugEx.Fine("Detaching on KIS move: part={0}, parent={1}", consumedPart, consumedPart.parent);
              consumedPart.decouple();
            }
            DebugEx.Info("Kill the part consumed by KIS in-flight pickup: {0}", consumedPart);
            consumedPart.Die();
            leasedItem.materialPart = null;
          }
          return true;
        },
        () => { // The cancel action.
          leasedItem.materialPart = null; // It's a cleanup just in case.
        });
  }
  #endregion

  #region Drop state handling
  /// <summary>Model of the part or assembly that is being dragged.</summary>
  /// <seealso cref="_touchPointTransform"/>
  /// <seealso cref="_hitPointTransform"/>
  Transform _draggedModel;

  /// <summary>Transform of the point which received the pointer hit.</summary>
  /// <remarks>
  /// It can be attached to part if a part has been hit, or be a static object if it was surface. This transform is
  /// dynamically created when something is hit and destroyed when there is nothing.
  /// </remarks>
  /// <seealso cref="_touchPointTransform"/>
  Transform _hitPointTransform;

  /// <summary>The part that is being hit with the current drag.</summary>
  /// <seealso cref="_hitPointTransform"/>
  Part _hitPart;

  /// <summary>Transform in the dragged model that should contact with the hit point.</summary>
  /// <remarks>It's always a child of the <see cref="_draggedModel"/>.</remarks>
  /// <seealso cref="_hitPointTransform"/>
  Transform _touchPointTransform;

  /// <summary>Handles the keyboard and mouse events when KIS drop mode is active in flight.</summary>
  /// <seealso cref="_controllerStateMachine"/>
  /// <seealso cref="_dropTargetEventsHandler"/>
  IEnumerator TrackDraggingStateCoroutine() {
    var singleItem = KisApi.ItemDragController.leasedItems.Length == 1
        ? KisApi.ItemDragController.leasedItems[0]
        : null;
    SetDraggedMaterialPart(singleItem?.materialPart);

    // Handle the dragging operation.
    while (_controllerStateMachine.currentState == ControllerState.DraggingItems) {
      UpdateDropActions();
      CrewHatchController.fetch.DisableInterface(); // No hatch actions while we're targeting the drop location!

      // Track the mouse cursor position and update the view.
      if (KisApi.ItemDragController.focusedTarget == null) {
        // The cursor is in a free space, the controller deals with it.
        if (singleItem != null) {
          MakeDraggedModelFromItem(singleItem);
          _dropTargetEventsHandler.currentState = PositionModelInTheScene(singleItem);
          //FIXME: highlight cannot drop cases.
        }
      } else {
        _dropTargetEventsHandler.currentState = DropTarget.KisTarget;
        DestroyDraggedModel();
      }
      UpdateDropTooltip();

      // Don't handle the keys in the same frame as the coroutine has started in to avoid double actions.
      yield return null;

      _dropTargetEventsHandler.HandleActions();
    }
    // No code beyond this point! The coroutine may be explicitly killed from the state machine.
  }
  Coroutine _trackDraggingStateCoroutine;

  /// <summary>Cleans ups any state created in <see cref="TrackDraggingStateCoroutine"/>.</summary>
  /// <remarks>
  /// Doing the cleanup in the coroutine itself is a bad idea due to it can die at any moment. Instead, the
  /// <see cref="_controllerStateMachine"/> should take care about it when the state is changed.
  /// </remarks>
  void CleanupTrackDraggingState() {
    CrewHatchController.fetch.EnableInterface();
    _dropTargetEventsHandler.currentState = null;
    SetDraggedMaterialPart(null);
    DestroyDraggedModel();
    if (_trackDraggingStateCoroutine != null) {
      StopCoroutine(_trackDraggingStateCoroutine);
      _trackDraggingStateCoroutine = null;
    }
    CleanupDropTooltip();
  }

  /// <summary>Handles actions to rotate the dragged part around it's Z-axis.</summary>
  /// <remarks>
  /// The actions change rotation at the touch point level. If there are multiple points, the rotation should be reset
  /// before switching.
  /// </remarks>
  void UpdateDropActions() {
    if (Rotate30LeftEvent.CheckClick()) {
      _rotateAngle -= 30;
    }
    if (Rotate30RightEvent.CheckClick()) {
      _rotateAngle += 30;
    }
    if (Rotate5LeftEvent.CheckClick()) {
      _rotateAngle -= 5;
    }
    if (Rotate5RightEvent.CheckClick()) {
      _rotateAngle += 5;
    }
    if (RotateResetEvent.CheckClick()) {
      _rotateAngle = 0;
    }
  }
  float _rotateAngle;

  /// <summary>Returns the rotation angle of the provided part.</summary>
  /// <remarks>
  /// <p>
  /// The angle is calculated the projected part placement as if it was put at the hit point via UI. It's a way to
  /// preserve the rotation when moving a material part in the scene.
  /// </p>
  /// <p>If the angle looks very close to the construction mode rotation, it gets rounded to the closest value.</p>
  /// </remarks>
  /// <param name="p">The part to get the angle for. If it's <c>null</c>, then the angle will be zero.</param>
  /// <returns>The angle in degrees.</returns>
  float CalcPickupRotation(Part p) {
    if (p == null) {
      return 0;
    }
    //FIXME: it's specific to the parent part's parent.
    var partTransform = p.transform;
    var goldenDir = Quaternion.LookRotation(partTransform.up) * Vector3.up;
    var partDir = partTransform.forward;
    var dir = Vector3.Dot(partTransform.up, Vector3.Cross(partDir, goldenDir)) < 0 ? 1.0f : -1.0f;
    var rawAngle = Vector3.Angle(partDir, goldenDir) * dir;
    
    var baseValue = (int) Math.Truncate(rawAngle * 100) % 100;
    return baseValue switch {
        0 => (float)Math.Truncate(rawAngle),
        99 => (float)Math.Truncate(rawAngle + 1),
        -99 => (float)Math.Truncate(rawAngle - 1),
        _ => rawAngle
    };
  }

  /// <summary>Updates or creates the in-flight tooltip with the drop info.</summary>
  /// <remarks>It's intended to be called on every frame update. This method must be efficient.</remarks>
  /// <seealso cref="DestroyCurrentTooltip"/>
  /// <seealso cref="_dropTargetEventsHandler"/>
  void UpdateDropTooltip() {
    if (_toggleTooltipEvent.CheckClick()) {
      _showTooltip = !_showTooltip;
    }
    if (!_showTooltip) {
      _showTooltipMessage.message = ShowCursorTooltipHint.Format(_toggleTooltipEvent.unityEvent);
      ScreenMessages.PostScreenMessage(_showTooltipMessage);
      DestroyCurrentTooltip();
      return;
    }
    ScreenMessages.RemoveMessage(_showTooltipMessage);
    if (_dropTargetEventsHandler.currentState is DropTarget.Nothing or DropTarget.KisTarget) {
      DestroyCurrentTooltip();
      return;
    }
    CreateTooltip();
    _currentTooltip.title = _dropTargetEventsHandler.currentState == DropTarget.Surface
        ? PutOnTheGroundHint
        : AlignAtThePartHint;
    _currentTooltip.baseInfo.text = VesselPlacementModeHint;
    var hints = _dropTargetEventsHandler.GetHintsList();
    hints.Add(RotateBy30DegreesHint.Format(Rotate30LeftEvent.unityEvent, Rotate30RightEvent.unityEvent));
    hints.Add(RotateBy5DegreesHint.Format(Rotate5LeftEvent.unityEvent, Rotate5RightEvent.unityEvent));
    hints.Add(RotateResetHint.Format(RotateResetEvent.unityEvent));
    hints.Add(HideCursorTooltipHint.Format(_toggleTooltipEvent.unityEvent));
    _currentTooltip.hints = string.Join("\n", hints);
    _currentTooltip.UpdateLayout();
  }
  bool _showTooltip = true;
  readonly ScreenMessage _showTooltipMessage = new("", Mathf.Infinity, ScreenMessageStyle.UPPER_CENTER);

  /// <summary>Cleanups anything related to the tooltip.</summary>
  void CleanupDropTooltip() {
    DestroyCurrentTooltip();
    ScreenMessages.RemoveMessage(_showTooltipMessage);
  }

  /// <summary>Creates a part model, given there is only one item is being dragged.</summary>
  /// <remarks>
  /// The model will immediately become active, so it should be either disabled or positioned in the same frame.
  /// </remarks>
  void MakeDraggedModelFromItem(InventoryItem item) {
    if (_draggedModel != null) {
      return; // The model already exists.
    }
    DebugEx.Fine("Creating flight scene dragging model...");
    _rotateAngle = CalcPickupRotation(item.materialPart);
    KisApi.ItemDragController.dragIconObj.gameObject.SetActive(false);
    _draggedModel = new GameObject("KisDragModel").transform;
    _draggedModel.gameObject.SetActive(true);
    var draggedPart = MakeSamplePart(item);
    var dragModel = KisApi.PartModelUtils.GetSceneAssemblyModel(draggedPart);
    dragModel.transform.SetParent(_draggedModel, worldPositionStays: false);
    _touchPointTransform =
        MakeTouchPoint("surfaceTouchPoint", draggedPart, _draggedModel, Vector3.up, _draggedModel.forward);
    Hierarchy.SafeDestroy(draggedPart);
  }

  /// <summary>Adds a transform for the requested vessel orientation.</summary>
  /// <param name="tpName">The name of the transform.</param>
  /// <param name="srcPart">The part to capture colliders from. It must be in the default position and rotation.</param>
  /// <param name="tgtModel">The part model to attach the transform to.</param>
  /// <param name="direction">The main (forward) direction.</param>
  /// <param name="upwards">The "upwards" direction of the orientation.</param>
  /// <returns></returns>
  Transform MakeTouchPoint(string tpName, Part srcPart, Transform tgtModel, Vector3 direction, Vector3 upwards) {
    var ptTransform = new GameObject(tpName).transform;
    ptTransform.SetParent(tgtModel, worldPositionStays: false);
    var distance = srcPart.FindModelComponents<Collider>()
        .Where(c => c.gameObject.layer == (int)KspLayer.Part)
        .Select(c => c.ClosestPoint(c.transform.position + -direction * 100).y)
        .Min();
    ptTransform.position += direction * distance;
    ptTransform.rotation = Quaternion.LookRotation(-direction, -upwards);
    return ptTransform;
  }

  /// <summary>Cleans up the dragged model.</summary>
  /// <remarks>It's safe to call it many times.</remarks>
  void DestroyDraggedModel() {
    if (_draggedModel == null) {
      return; // Nothing to do.
    }
    DebugEx.Fine("Destroying flight scene dragging model...");
    if (KisApi.ItemDragController.isDragging) {
      KisApi.ItemDragController.dragIconObj.gameObject.SetActive(true);
    }
    Hierarchy.SafeDestroy(_draggedModel);
    _draggedModel = null;
  }

  /// <summary>Aligns the dragged model to the pointer.</summary>
  /// <remarks>
  /// If pointer hits the surface, then a drop mode is engaged, and the attach mode is disallowed.
  /// If pointer hits a part, then both the drop and the attach modes are allowed. If nothing is
  /// hit, then this method returns <c>false</c>, and the model is aligned to the pointer at a
  /// constant distance from the camera.
  /// </remarks>
  /// <returns>The target type being hit by the ray cast.</returns>
  DropTarget? PositionModelInTheScene(InventoryItem draggedItem) {
    var camera = FlightCamera.fetch.mainCamera;
    var ray = camera.ScreenPointToRay(Input.mousePosition);

    var hitsCount = Physics.RaycastNonAlloc(
        ray, _hitsBuffer,
        maxDistance: _maxRaycastDistance,
        layerMask: (int)(KspLayerMask.Part | KspLayerMask.Kerbal | KspLayerMask.SurfaceCollider),
        queryTriggerInteraction: QueryTriggerInteraction.Ignore);
    var hit = _hitsBuffer.Take(hitsCount)
        .OrderBy(x => x.distance) // Unity doesn't sort the result.
        .FirstOrDefault(
            x => draggedItem.materialPart == null || x.transform.gameObject != draggedItem.materialPart.gameObject);

    // If no surface or part is hit, then show the part being carried.
    if (hit.Equals(default(RaycastHit))) {
      Hierarchy.SafeDestroy(_hitPointTransform);
      _hitPointTransform = null;
      var cameraTransform = camera.transform;
      _draggedModel.position = cameraTransform.position + ray.direction * _hangingObjectDistance;
      _draggedModel.rotation = cameraTransform.rotation;
      return DropTarget.Nothing;
    }
    var freshHitTransform = _hitPointTransform == null; // Will be used for logging.
    if (freshHitTransform) {
      _hitPointTransform = new GameObject("KISHitTarget").transform;
    }
    _hitPointTransform.position = hit.point;

    // Find out what was hit.
    _hitPart = FlightGlobals.GetPartUpwardsCached(hit.collider.gameObject);
    if (_hitPart == null) {
      // The hit that is not a part is a surface. It cannot happen in orbit.
      if (_hitPointTransform.parent != null || freshHitTransform) {
        DebugEx.Fine(
            "Hit surface: collider={0}, celestialBody={1}", hit.collider.transform,
            FlightGlobals.ActiveVessel.mainBody);
        _hitPointTransform.SetParent(null);
      }
      var surfaceNormal = FlightGlobals.getUpAxis(FlightGlobals.fetch.activeVessel.mainBody, hit.point);
      Vector3 fwd;
      if (!CheckIfParallel(surfaceNormal, hit.normal)) {
        // If placed on a slope, the "uphill" direction is assumed to be the base for the sake of rotation.
        var left = Vector3.Cross(hit.normal, surfaceNormal);
        fwd = Vector3.Cross(left, hit.normal);
      } else {
        // On a flat surface assume an arbitrary "default direction".
        fwd = Vector3.up;  // Default.
      }
      _hitPointTransform.rotation =
          Quaternion.AngleAxis(_rotateAngle, hit.normal) * Quaternion.LookRotation(hit.normal, fwd);
      AlignTransforms.SnapAlign(_draggedModel, _touchPointTransform, _hitPointTransform);
      return DropTarget.Surface;
    }

    // We've hit a part. Bind to this point!
    if (_hitPointTransform.parent != _hitPart.transform || freshHitTransform) {
      DebugEx.Fine("Hit part: part={0}", _hitPart);
      _hitPointTransform.SetParent(_hitPart.transform);
    }
    var partUp = _hitPart.transform.up;
    var partFwd = !CheckIfParallel(partUp, hit.normal) ? partUp : Vector3.up;
    _hitPointTransform.rotation =
        Quaternion.AngleAxis(_rotateAngle, hit.normal) * Quaternion.LookRotation(hit.normal, partFwd);
    AlignTransforms.SnapAlign(_draggedModel, _touchPointTransform, _hitPointTransform);

    if (_hitPart.isVesselEVA && _hitPart.HasModuleImplementing<IKisInventory>()) {
      return DropTarget.KerbalInventory;
    }
    return _hitPart.HasModuleImplementing<IKisInventory>() ? DropTarget.KisInventory : DropTarget.Part;
  }
  RaycastHit[] _hitsBuffer = new RaycastHit[100];  // 100 is an arbitrary reasonable value for the hits count.

  /// <summary>Makes a part from the saved config for the purpose of the part model capture.</summary>
  /// <remarks>
  /// This part is not fully initialized and is not intended to live till the next frame. It method tries to capture as
  /// many dynamic behavior on the part as possible, but there are compromises.
  /// </remarks>
  /// <param name="item">The item to create the part from.</param>
  /// <returns>
  /// A sample part with the default position and rotation. It <i>must</i> be destroyed before the next frame starts!
  /// </returns>
  static Part MakeSamplePart(InventoryItem item) {
    var part = item.snapshot.CreatePart();
    part.gameObject.SetLayerRecursive(
        (int)KspLayer.Part, filterTranslucent: true, ignoreLayersMask: (int)KspLayerMask.TriggerCollider);
    part.gameObject.SetActive(true);
    part.transform.position = Vector3.zero;
    part.transform.rotation = part.initRotation;
    part.InitializeModules();

    return part;
  }
  #endregion

  #region Local utility methods
  /// <summary>Consumes the item being dragged and makes a scene vessel from it.</summary>
  void CreateVesselFromDraggedItem() {
    var pos = _draggedModel.position;
    var rot = _draggedModel.rotation;

    var canConsumeItem = KisApi.ItemDragController.leasedItems[0];
    var materialPart = canConsumeItem.materialPart;
    if (materialPart != null) {
      canConsumeItem.materialPart = null; // From here we'll be handling the material part.
    }

    // Consuming items will change the state, so capture all the important values before doing it.
    var consumedItems = KisApi.ItemDragController.ConsumeItems();
    if (consumedItems == null || consumedItems.Length == 0) {
      DebugEx.Error("The leased item cannot be consumed");
      canConsumeItem.materialPart = materialPart; // It didn't work, return the part back to the item.
      return;
    }

    if (materialPart != null) {
      // Only reposition the existing part.
      if (materialPart.parent != null) {
        DebugEx.Info("Decouple the dragged part: part={0}, parent={1}", materialPart, materialPart.parent);
        materialPart.decouple();
        materialPart.vessel.vesselType = VesselType.DroppedPart;
        materialPart.vessel.vesselName = materialPart.partInfo.title;
      }
      KisApi.VesselUtils.MoveVessel(materialPart.vessel, pos, rot, _hitPart);
    } else {
      // Create a new vessel from the item.
      DebugEx.Info("Create new vessel from the dragged part: part={0}", consumedItems[0].avPart.name);
      var protoVesselNode =
          KisApi.PartNodeUtils.MakeNewVesselNode(FlightGlobals.ActiveVessel, consumedItems[0], pos, rot);
      HighLogic.CurrentGame.AddVessel(protoVesselNode);
    }
  }

  /// <summary>Destroys any tooltip that is existing in the controller.</summary>
  /// <remarks>It's a cleanup method and it's safe to call it from any state.</remarks>
  void DestroyCurrentTooltip() {
    if (_currentTooltip != null) {
      HierarchyUtils.SafeDestroy(_currentTooltip);
      _currentTooltip = null;
    }
  }

  void CreateTooltip() {
    if (_currentTooltip == null) {
      _currentTooltip = UnityPrefabController.CreateInstance<UIKISInventoryTooltip.Tooltip>(
          "inFlightControllerTooltip", UIMasterController.Instance.actionCanvas.transform);
    }
  }

  /// <summary>Sets the material part that was a source for the drag operation.</summary>
  /// <remarks>
  /// This part will be considered "being dragged". It'll stay being material and (maybe) physical, but it may change
  /// its visual appearance to indicate its new status.
  /// </remarks>
  void SetDraggedMaterialPart(Part materialPart) {
    if (_sourceDraggedPart == materialPart) {
      return;
    }
    if (_sourceDraggedPart != null) {
      RestorePartState(_sourceDraggedPart, _sourceDraggedPartSavedState);
    }
    _sourceDraggedPart = materialPart;
    if (_sourceDraggedPart != null) {
      _sourceDraggedPartSavedState = MakeDraggedPartGhost(_sourceDraggedPart);
    }
  }
  Part _sourceDraggedPart;
  Dictionary<int, Material[]> _sourceDraggedPartSavedState;

  /// <summary>Returns the total number of the parts in the hierarchy.</summary>
  static int CountChildrenInHierarchy(Part p) {
    return p.children.Count + p.children.Sum(CountChildrenInHierarchy);
  }

  /// <summary>Turns the designated part into a "holo".</summary>
  /// <remarks>
  /// <p>
  /// The part in question changes it's visual appearance to highlight the "dragging state". However, this part stays
  /// fully physical and its game role doesn't change.
  /// </p>
  /// <p>
  /// It's not defined how exactly the part appearance will be changed, but the changes must be limited to the materials
  /// modifications.
  /// </p>
  /// </remarks>
  /// <param name="p">The part to turn into a "holo".</param>
  /// <returns>
  /// <p>
  /// The saved state of the original materials. The caller is responsible to use this information to restore the
  /// original part appearance when applicable.
  /// </p>
  /// <p>
  /// The key in the dictionary is the renderer's object hash code. It's expected that the set of the renderers on the
  /// part won't change while the part is in the dragging state.
  /// </p>
  /// </returns>
  /// <seealso cref="RestorePartState"/>
  static Dictionary<int, Material[]> MakeDraggedPartGhost(Part p) {
    var resetRenderers = p.GetComponentsInChildren<Renderer>();
    var res = resetRenderers.ToDictionary(x => x.GetHashCode(), x => x.materials);
    Shader holoShader = null;
    if (_stdTransparentRenderer != "") {
      holoShader = Shader.Find(_stdTransparentRenderer);
      if (holoShader == null) {
        DebugEx.Error("Cannot find standard transparent renderer: {0}", _stdTransparentRenderer);
      }
    }
    if (holoShader != null) {
      DebugEx.Fine("Turning the target part into a holo: {0}", p);
      foreach (var resetRenderer in resetRenderers) {
        var newMaterials = new Material[resetRenderer.materials.Length];
        for (var i = 0; i < resetRenderer.materials.Length; ++i) {
          newMaterials[i] = new Material(holoShader) {
              color = _holoColor
          };
        }
        resetRenderer.materials = newMaterials;
      }
    }
    p.SetHighlight(false, recursive: true);
    p.SetHighlightType(Part.HighlightType.Disabled);
    return res;
  }

  /// <summary>Restores the part appearance.</summary>
  /// <param name="p">The part to restore the state of.</param>
  /// <param name="savedState">
  /// The saved state to apply to the part. It must exactly match to the state that was at the moment of making the
  /// snapshot. If it doesn't, the state will not be restored and an error will be logged.
  /// </param>
  /// <seealso cref="MakeDraggedPartGhost"/>
  static void RestorePartState(Part p, IReadOnlyDictionary<int, Material[]> savedState) {
    DebugEx.Fine("Restoring renderers on: {0}", p);
    var renderers = p.GetComponentsInChildren<Renderer>();
    foreach (var renderer in renderers) {
      if (savedState.TryGetValue(renderer.GetHashCode(), out var materials)) {
        if (renderer.materials.Length == materials.Length) {
          renderer.materials = materials;
        } else {
          DebugEx.Error("Cannot restore renderer materials: renderer={0}, actualCount={1}, savedCount={2}",
                        renderer, renderer.materials.Length, materials.Length);
        }
      } else {
        DebugEx.Error("The part's renderer is not found in the saved state: {0}", renderer);
      }
    }
    p.RefreshHighlighter();
    p.SetHighlightType(Part.HighlightType.OnMouseOver);
    p.SetHighlightDefault();
  }

  /// <summary>Verifies if two vector are "almost parallel".</summary>
  bool CheckIfParallel(Vector3 v1, Vector3 v2) {
    var angle = Vector3.Angle(v1, v2);
    return angle is < 1.0f or > 179.0f;
  }
  #endregion
}

}
