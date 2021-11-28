// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections;
using System.Collections.Generic;
using Experience.Effects;
using KISAPIv2;
using KSPDev.InputUtils;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using System.Linq;
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
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Drop on the surface",
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

  // ReSharper enable FieldCanBeMadeReadOnly.Local
  // ReSharper enable FieldCanBeMadeReadOnly.Global
  // ReSharper enable ConvertToConstant.Global
  // ReSharper enable MemberCanBePrivate.Global
  // ReSharper enable ConvertToConstant.Local
  #endregion

  #region Event static configs
  static readonly Event DropItemToSceneEvent = Event.KeyboardEvent("mouse0");
  static readonly Event PickupItemFromSceneEvent = Event.KeyboardEvent("mouse0");
  static readonly Event KisFlightModeSwitchEvent = Event.KeyboardEvent("j");
  #endregion

  #region Local fields and properties
  /// <summary>Model of the part or assembly that is being dragged.</summary>
  /// <seealso cref="_touchPointTransform"/>
  /// <seealso cref="_hitPointTransform"/>
  Transform _draggedModel;

  /// <summary>Transform of the point which received the pointer hit.</summary>
  /// <remarks>
  /// It can be attached to part if a part has been hit, or be a static object if it was surface.
  /// This transform, is dynamically created when something is hit, and destroyed when there is
  /// nothing.
  /// </remarks>
  /// <seealso cref="_touchPointTransform"/>
  Transform _hitPointTransform;

  /// <summary>Transform in the dragged model that should contact with the hit point.</summary>
  /// <remarks>It's always a child of the <see cref="_draggedModel"/>.</remarks>
  /// <seealso cref="_hitPointTransform"/>
  Transform _touchPointTransform;

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

  /// <summary>The tooltip the is currently being presented.</summary>
  UIKISInventoryTooltip.Tooltip _currentTooltip;
  #endregion

  #region MonoBehaviour overrides
  void Awake() {
    DebugEx.Fine("[{0}] Controller started", nameof(FlightItemDragController));
    ConfigAccessor.ReadFieldsInType(GetType(), null); // Read the static fields.

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
        _ => {
          _pickupTargetEventsHandler.currentState = null;
          targetPickupPart = null;
          DestroyCurrentTooltip();
          if (_trackPickupStateCoroutine != null) {
            StopCoroutine(_trackPickupStateCoroutine);
            _trackPickupStateCoroutine = null;
          }
        });
    _controllerStateMachine.AddStateHandlers(
        ControllerState.DraggingItems,
        _ => { _trackDraggingModeCoroutine = StartCoroutine(TrackDraggingModeCoroutine()); },
        _ => {
          _dropTargetEventsHandler.currentState = null;
          sourceDraggedPart = null;
          DestroyDraggedModel();
          if (_trackDraggingModeCoroutine != null) {
            StopCoroutine(_trackDraggingModeCoroutine);
            _trackDraggingModeCoroutine = null;
          }
        });
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
    return KisApi.ItemDragController.leasedItems.Length == 1;
  }

  /// <inheritdoc/>
  void IKisDragTarget.OnFocusTarget(IKisDragTarget newTarget) {
  }
  #endregion

  #region Idle state handling
  /// <summary>Handles the keyboard/mouse events when the controller is idle.</summary>
  /// <seealso cref="KisFlightModeSwitchEvent"/>
  /// <seealso cref="_controllerStateMachine"/>
  IEnumerator TrackIdleStateCoroutine() {
    while (_controllerStateMachine.currentState == ControllerState.Idle) {
      // Don't handle the keys in the same frame as the coroutine has started in to avoid double actions.
      yield return null;

      if (Input.anyKey && EventChecker2.CheckEventActive(KisFlightModeSwitchEvent)) {
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
      }
      _targetPickupPart = value;
      if (_targetPickupPart != null) {
        _targetPickupPart.SetHighlightType(Part.HighlightType.AlwaysOn);
        _targetPickupPart.SetHighlight(true, recursive: true);
      }
    }
  }
  Part _targetPickupPart;

  /// <summary>Handles the keyboard and mouse events when KIS pickup mode is enabled in flight.</summary>
  /// <remarks>
  /// It checks if the modifier key is released and brings teh controller to the idle state if that's the case.
  /// </remarks>
  /// <seealso cref="KisFlightModeSwitchEvent"/>
  /// <seealso cref="_controllerStateMachine"/>
  /// <seealso cref="_pickupTargetEventsHandler"/>
  IEnumerator TrackPickupStateCoroutine() {
    while (_controllerStateMachine.currentState == ControllerState.PickupModePending) {
      targetPickupPart = Mouse.HoveredPart != null && !Mouse.HoveredPart.isVesselEVA ? Mouse.HoveredPart : null;
      if (targetPickupPart == null) {
        _pickupTargetEventsHandler.currentState = null;
      } else if (targetPickupPart.children.Count == 0) {
        _pickupTargetEventsHandler.currentState = PickupTarget.SinglePart;
      } else {
        _pickupTargetEventsHandler.currentState = PickupTarget.PartAssembly;
      }
      UpdatePickupTooltip(targetPickupPart);

      // Don't handle the keys in the same frame as the coroutine has started in to avoid double actions.
      yield return null;

      _pickupTargetEventsHandler.HandleActions();
      if (!Input.anyKey || !EventChecker2.CheckEventActive(KisFlightModeSwitchEvent)) {
        _controllerStateMachine.currentState = ControllerState.Idle;
        break;
      }
    }
    // No code beyond this point! The coroutine is explicitly killed from the state machine.
  }
  Coroutine _trackPickupStateCoroutine;
  #endregion

  #region Drop state handling
  /// <summary>The material part that was a source for the drag operation.</summary>
  /// <remarks>
  /// This part will be considered "being dragged". It'll stay being material and (maybe) physical, but it may change
  /// its visual appearance to indicate its new status.
  /// </remarks>
  /// <value>The part or <c>null</c> if not part is being a source of the dragging operation.</value>
  Part sourceDraggedPart {
    get => _sourceDraggedPart;
    set {
      if (_sourceDraggedPart == value) {
        return;
      }
      if (_sourceDraggedPart != null) {
        RestorePartState(_sourceDraggedPart, _sourceDraggedPartSavedState);
      }
      _sourceDraggedPart = value;
      if (_sourceDraggedPart != null) {
        _sourceDraggedPartSavedState = MakeDraggedPartGhost(_sourceDraggedPart);
      }
    }
  }
  Part _sourceDraggedPart;
  Dictionary<int, Material[]> _sourceDraggedPartSavedState;

  /// <summary>Handles the keyboard and mouse events when KIS drop mode is active in flight.</summary>
  /// <seealso cref="_controllerStateMachine"/>
  /// <seealso cref="_dropTargetEventsHandler"/>
  IEnumerator TrackDraggingModeCoroutine() {
    var singleItem = KisApi.ItemDragController.leasedItems.Length == 1
        ? KisApi.ItemDragController.leasedItems[0]
        : null;
    sourceDraggedPart = singleItem?.materialPart;

    // Handle the dragging operation.
    while (_controllerStateMachine.currentState == ControllerState.DraggingItems) {
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

      // Don't handle the keys in the same frame as the coroutine has started in to avoid double actions.
      yield return null;

      _dropTargetEventsHandler.HandleActions();
    }
    // No code beyond this point! The coroutine is explicitly killed from the state machine.
  }
  Coroutine _trackDraggingModeCoroutine;
  #endregion

  #region Local utility methods
  /// <summary>Creates a part model, given there is only one item is being dragged.</summary>
  /// <remarks>
  /// The model will immediately become active, so it should be either disabled or positioned in the same frame.
  /// </remarks>
  void MakeDraggedModelFromItem(InventoryItem item) {
    if (_draggedModel != null) {
      return; // The model already exists.
    }
    DebugEx.Fine("Creating flight scene dragging model...");
    KisApi.ItemDragController.dragIconObj.gameObject.SetActive(false);
    _draggedModel = new GameObject("KisDragModel").transform;
    _draggedModel.gameObject.SetActive(true);
    var draggedPart = MakeSamplePart(item.avPart, item.itemConfig);
    var dragModel = KisApi.PartModelUtils.GetSceneAssemblyModel(draggedPart);
    dragModel.transform.SetParent(_draggedModel, worldPositionStays: false);
    dragModel.transform.rotation = draggedPart.initRotation;
    _touchPointTransform = new GameObject("surfaceTouchPoint").transform;
    _touchPointTransform.SetParent(_draggedModel, worldPositionStays: false);
    var bounds = draggedPart.FindModelComponents<Collider>().Select(x => x.bounds).Aggregate(
        (res, next) => {
          res.Encapsulate(next);
          return res;
        });
    var dist = bounds.center.y + bounds.extents.y;
    _touchPointTransform.position += -_draggedModel.up * dist;
    _touchPointTransform.rotation = Quaternion.LookRotation(-_draggedModel.up, -_draggedModel.forward);
    Hierarchy.SafeDestroy(draggedPart);
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

    var hitsBuffer = new RaycastHit[100];  // 100 is an empiric value.
    var hitsCount = Physics.RaycastNonAlloc(
        ray, hitsBuffer,
        maxDistance: _maxRaycastDistance,
        layerMask: (int)(KspLayerMask.Part | KspLayerMask.Kerbal | KspLayerMask.SurfaceCollider),
        queryTriggerInteraction: QueryTriggerInteraction.Ignore);
    var hit = hitsBuffer.Take(hitsCount)
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
    var needNewHitTransform = _hitPointTransform == null; // Will be used for logging.
    if (needNewHitTransform) {
      _hitPointTransform = new GameObject("KISHitTarget").transform;
    }
    _hitPointTransform.position = hit.point;

    // Find out what's was hit.
    var hitPart = FlightGlobals.GetPartUpwardsCached(hit.collider.transform.gameObject);
    if (hitPart == null) {
      // We've hit the surface. A lot of things may get wrong if the surface is not leveled!
      // Align the model to the celestial body normal and rely on the game's logic on the vessel
      // positioning. It may (and, likely, will) not match to what we may have presented in GUI.
      if (_hitPointTransform.parent != null || needNewHitTransform) {
        DebugEx.Fine(
            "Hit surface: collider={0}, celestialBody={1}", hit.collider.transform,
            FlightGlobals.ActiveVessel.mainBody);
        _hitPointTransform.SetParent(null);
      }
      var surfaceNorm = FlightGlobals.getUpAxis(FlightGlobals.ActiveVessel.mainBody, hit.point);
      _hitPointTransform.rotation = Quaternion.LookRotation(surfaceNorm);
      AlignTransforms.SnapAlign(_draggedModel, _touchPointTransform, _hitPointTransform);
      return DropTarget.Surface;
    }

    // We've hit a part. Bind to this point!
    if (_hitPointTransform.parent != hitPart.transform || needNewHitTransform) {
      DebugEx.Fine("Hit part: part={0}", hitPart);
      _hitPointTransform.SetParent(hitPart.transform);
    }
    //FIXME: choose "not-up" when hitting the up/down plane of the target part.
    _hitPointTransform.rotation = Quaternion.LookRotation(hit.normal, hitPart.transform.up);
    AlignTransforms.SnapAlign(_draggedModel, _touchPointTransform, _hitPointTransform);
    if (hitPart.isVesselEVA && hitPart.HasModuleImplementing<IKisInventory>()) {
      return DropTarget.KerbalInventory;
    }
    if (hitPart.HasModuleImplementing<IKisInventory>()) {
      return DropTarget.KisInventory;
    }
    return DropTarget.Part;
  }

  /// <summary>Consumes the item being dragged and makes a scene vessel from it.</summary>
  void CreateVesselFromDraggedItem() {
    var pos = _draggedModel.position;
    var rot = _draggedModel.rotation;
    var consumedItems = KisApi.ItemDragController.ConsumeItems();
    if (consumedItems == null || consumedItems.Length == 0) {
      DebugEx.Error("The leased item cannot be consumed");
      return;
    }
    //FIXME vessel.heightFromPartOffsetLocal = -vessel.HeightFromPartOffsetGlobal
    var protoVesselNode =
        KisApi.PartNodeUtils.MakeNewVesselNode(FlightGlobals.ActiveVessel, consumedItems[0], pos, rot);
    HighLogic.CurrentGame.AddVessel(protoVesselNode);
  }

  /// <summary>Makes a part from the saved config for the purpose of the part "holo" capture.</summary>
  /// <remarks>
  /// This part is not fully initialized and is not intended to live till the next frame. It method tries to capture as
  /// many dynamic behavior on the part as possible, but there are compromises.
  /// </remarks>
  /// <param name="partInfo">The part info to create.</param>
  /// <param name="itemConfig">The state of the part.</param>
  /// <returns>The sample part. It <i>must</i> be destroyed before the next frame starts!</returns>
  Part MakeSamplePart(AvailablePart partInfo, ConfigNode itemConfig) {
    var part = Instantiate(partInfo.partPrefab);
    part.gameObject.SetLayerRecursive(
        (int)KspLayer.Part, filterTranslucent: true, ignoreLayersMask: (int)KspLayerMask.TriggerCollider);
    part.partInfo = partInfo;
    //FIXME: do ground experiment parts setup.
    part.gameObject.SetActive(true);
    part.name = partInfo.name;
    part.persistentId = FlightGlobals.CheckPartpersistentId(part.persistentId, part, false, true);
    var actions = itemConfig.GetNode("ACTIONS");
    if (actions != null) {
      part.Actions.OnLoad(actions);
    }
    var events = itemConfig.GetNode("EVENTS");
    if (events != null) {
      part.Events.OnLoad(events);
    }
    var effects = itemConfig.GetNode("EFFECTS");
    if (effects != null) {
      part.Effects.OnLoad(effects);
    }
    var partData = itemConfig.GetNode("PARTDATA");
    if (partData != null) {
      part.OnLoad(partData);
    }
    var moduleIdx = 0;
    foreach (var configNode in itemConfig.GetNodes("MODULE")) {
      part.LoadModule(configNode, ref moduleIdx);
    }
    itemConfig.GetNodes("RESOURCE").ToList().ForEach(x => part.SetResource(x));

    part.InitializeModules();
    part.ModulesBeforePartAttachJoint();
    part.ModulesOnStart();
    part.ModulesOnStartFinished();

    return part;
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

  /// <summary>Updates or creates the in-flight tooltip with the part data.</summary>
  /// <remarks>It's intended to be called on every frame update. This method must be efficient.</remarks>
  /// <param name="hoveredPart">
  /// The part to make the tooltip for. If it's <c>null</c>, then the tooltip gets destroyed.
  /// </param>
  /// <seealso cref="DestroyCurrentTooltip"/>
  void UpdatePickupTooltip(Part hoveredPart) {
    if (hoveredPart == null) {
      DestroyCurrentTooltip();
      return;
    }
    CreateTooltip();
    if (_pickupTargetEventsHandler.currentState == PickupTarget.SinglePart) {
      KisContainerWithSlots.UpdateTooltip(_currentTooltip, new[] { InventoryItemImpl.FromPart(null, hoveredPart) });
    } else if (_pickupTargetEventsHandler.currentState == PickupTarget.PartAssembly) {
      // TODO(ihsoft): Implement!
      _currentTooltip.ClearInfoFields();
      _currentTooltip.title = CannotGrabHierarchyTooltipMsg;
      _currentTooltip.baseInfo.text =
          CannotGrabHierarchyTooltipDetailsMsg.Format(CountChildrenInHierarchy(hoveredPart));
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
  /// on the contracts state, if the part was a part of any.
  /// </p>
  /// </remarks>
  void HandleScenePartPickupAction() {
    var leasedItem = InventoryItemImpl.FromPart(null, _targetPickupPart);
    KisApi.ItemDragController.LeaseItems(
        KisApi.PartIconUtils.MakeDefaultIcon(leasedItem.materialPart),
        new[] { leasedItem },
        () => { // The consume action.
          var consumedPart = leasedItem.materialPart;
          if (consumedPart == null) {
            // This is not normally happening, but is expected.
            DebugEx.Error("The item's material part has disappeared before the drag operation has ended");
            leasedItem.materialPart = null;
            return false;
          }
          if (leasedItem.materialPart.parent != null) {
            DebugEx.Fine("Detaching on KIS move: part={0}, parent={1}", consumedPart, consumedPart.parent);
            consumedPart.decouple();
          }
          DebugEx.Info("Kill the part consumed by KIS in-flight pickup: {0}", consumedPart);
          consumedPart.Die();
          leasedItem.materialPart = null;
          return true;
        },
        () => { // The cancel action.
          leasedItem.materialPart = null; // It's a cleanup just in case.
        });
  }

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
  }
  #endregion

  #region API/Utility candidates
  /// <summary>Calculates ground experiment part power production modifier.</summary>
  /// <remarks>
  /// When the part is deployed by a kerbal, it's power generation can be adjusted by the kerbal's
  /// traits.
  /// </remarks>
  /// <param name="avPart">The part info.</param>
  /// <param name="actorVessel">The kerbal vessel to get traits from.</param>
  /// <returns>The delta to the power production of the part.</returns>
  static float GetExperimentPartPowerProductionModifier(AvailablePart avPart, Vessel actorVessel) {
    var sciencePart = avPart.partPrefab.FindModuleImplementing<ModuleGroundSciencePart>();
    if (sciencePart != null && sciencePart.PowerUnitsProduced > 0
        && actorVessel.rootPart.protoModuleCrew[0].HasEffect<DeployedSciencePowerSkill>()) {
      var effect = actorVessel.rootPart.protoModuleCrew[0].GetEffect<DeployedSciencePowerSkill>();
      if (effect != null) {
        return effect.GetValue();
      }
    }
    return 0;
  }

  /// <summary>Calculates ground experiment part science production modifier.</summary>
  /// <remarks>
  /// When the part is deployed by a kerbal, it's science output can be adjusted by the kerbal's
  /// traits.
  /// </remarks>
  /// <param name="avPart">The part info.</param>
  /// <param name="actorVessel">The kerbal vessel to get traits from.</param>
  /// <returns>The rate adjustment of the part.</returns>
  static float GetExperimentPartScienceModifier(AvailablePart avPart, Vessel actorVessel) {
    var experimentPart = avPart.partPrefab.FindModuleImplementing<ModuleGroundExperiment>();
    if (experimentPart != null
        && actorVessel.rootPart.protoModuleCrew[0].HasEffect<DeployedScienceExpSkill>()) {
      var effect = actorVessel.rootPart.protoModuleCrew[0].GetEffect<DeployedScienceExpSkill>();
      if (effect != null) {
        return effect.GetValue() * 100f;
      }
    }
    return 0;
  }
  #endregion
}

}
