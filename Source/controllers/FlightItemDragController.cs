// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections;
using Experience.Effects;
using KISAPIv2;
using KSPDev.InputUtils;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using System.Linq;
using KSP.UI;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.Unity;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>Controller that deals with dragged items in the flight scenes.</summary>
[KSPAddon(KSPAddon.Startup.Flight, false /*once*/)]
sealed class FlightItemDragController : MonoBehaviour, IKisDragTarget {
  #region Localizable strings
  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> TakeFocusedPartHint = new(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Grab the part",
      description: "The tooltip status to present when the KIS grabbing mode is activated, but no part is being"
      + " focused.");
  #endregion

  #region Event static configs
  static readonly Event DropItemToSceneEvent = Event.KeyboardEvent("mouse0");
  static readonly Event PickupItemFromSceneEvent = Event.KeyboardEvent("mouse0");
  static readonly Event KisFlightModeSwitchEvent = Event.KeyboardEvent("j");
  #endregion

  #region Local fields and properties
  /// <summary>Tells if there is an item model being displayed in the scene.</summary>
  /// <remarks>
  /// When it's the case, it means the pointer is placed outside of any GUI element. However, it
  /// doesn't mean that the dragged item can be dropped into the scene. 
  /// </remarks>
  bool isActiveModelInScene => _draggedModel != null;

  /// <summary>
  /// Main texture color for the dragged model renderers when the item can be dropped at the pointed
  /// located. 
  /// </summary>
  /// <seealso cref="MaxRaycastDistance"/>
  static readonly Color GoodToPlaceColor = new Color(0f, 1f, 0f, 0.2f);

  /// <summary>
  /// Main texture color for the dragged model renderers when the item CANNOT be dropped at the
  /// pointed located. It's also used fro the hanging item model coloring.
  /// </summary>
  /// <seealso cref="MaxRaycastDistance"/>
  /// <seealso cref="HangingObjectDistance"/>
  static readonly Color NotGoodToPlaceColor = new Color(1f, 0f, 0f, 0.2f);

  /// <summary>Distance from the camera of the object that cannot be placed anywhere.</summary>
  /// <remarks>
  /// If an item cannot be dropped, it will be "hanging" at the camera at this distance. It mostly
  /// affects the object's size.
  /// </remarks>
  /// <seealso cref="NotGoodToPlaceColor"/>
  const float HangingObjectDistance = 10.0f;

  /// <summary>
  /// Maximum distance from the current camera to the hit point, where an item can be dropped.  
  /// </summary>
  /// <remarks>
  /// Hit points out of this radius will not be considered as the drop points. The dragged object
  /// will not be allowed to drop. 
  /// </remarks>
  /// <seealso cref="HangingObjectDistance"/>
  const float MaxRaycastDistance = 50;

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

  // TODO(ihsoft): Implement support ofr the multi items pickup!
  enum InFlightActionMode {
    SinglePartFocused,
    PartAssemblyFocused,
  }
  readonly EventsHandlerStateMachine<InFlightActionMode> _slotEventsHandler = new();

  /// <summary>The tooltip the is currently being presented.</summary>
  UIKISInventoryTooltip.Tooltip _currentTooltip;

  /// <summary>The part to highlight for the purpose of the KIS action.</summary>
  /// <remarks>The handler of the property change will control the UI appearance of the "hover action".</remarks>
  /// <seealso cref="_slotEventsHandler"/>
  Part hoveredPart {
    get => _hoveredPart;
    set {
      if (_hoveredPart == value) {
        return;
      }
      if (_hoveredPart != null) {
        _hoveredPart.SetHighlight(false, recursive: true);
        _hoveredPart.SetHighlightDefault();
      }
      _hoveredPart = value;
      if (_hoveredPart != null) {
        _hoveredPart.SetHighlightType(Part.HighlightType.AlwaysOn);
        _hoveredPart.SetHighlight(true, recursive: true);
      }
      if (_hoveredPart == null) {
        _slotEventsHandler.currentState = null;
      } else if (_hoveredPart.children.Count == 0) {
        _slotEventsHandler.currentState = InFlightActionMode.SinglePartFocused;
      } else {
        _slotEventsHandler.currentState = InFlightActionMode.PartAssemblyFocused;
      }
    }
  }
  Part _hoveredPart;
  #endregion

  #region MonoBehaviour overrides
  void Awake() {
    DebugEx.Fine("[{0}] Controller started", nameof(FlightItemDragController));
    KisApi.ItemDragController.RegisterTarget(this);
    _slotEventsHandler.DefineAction(
        InFlightActionMode.SinglePartFocused, TakeFocusedPartHint, PickupItemFromSceneEvent, HandleScenePartPickup);
  }

  void OnDestroy() {
    DebugEx.Fine("[{0}] Controller stopped", nameof(FlightItemDragController));
    KisApi.ItemDragController.UnregisterTarget(this);
    _slotEventsHandler.currentState = null;
  }

  /// <summary>Handles the in-flight keyboard and mouse input.</summary>
  void Update() {
    if (!Input.anyKeyDown) {
      return; // Only key/mouse click events below this line. 
    }
    // Below this point only THIS FRAME PRESS DOWN events can be checked. It's a performance requirement.
    if (isActiveModelInScene) {
      if (EventChecker.CheckClickEvent(DropItemToSceneEvent)) {
        CreateVesselFromDraggedItem();
      }
    } else if (EventChecker.CheckClickEvent(KisFlightModeSwitchEvent)) {
      StartCoroutine(KisPickupEventHandler());
    }
  }
  #endregion

  #region IKisDragTarget implementation
  /// <inheritdoc/>
  public Component unityComponent => this;

  /// <inheritdoc/>
  void IKisDragTarget.OnKisDragStart() {
    if (KisApi.ItemDragController.focusedTarget == null && KisApi.ItemDragController.leasedItems.Length == 1) {
      MakeDraggedModelFromItem();
    }
  }

  /// <inheritdoc/>
  void IKisDragTarget.OnKisDragEnd(bool isCancelled) {
    DestroyDraggedModel();
  }

  /// <inheritdoc/>
  bool IKisDragTarget.OnKisDrag(bool pointerMoved) {
    return _draggedModel != null && PositionModelInTheScene();
  }

  /// <inheritdoc/>
  void IKisDragTarget.OnFocusTarget(IKisDragTarget newTarget) {
    if (newTarget != null) {
      DestroyDraggedModel();
    } else if (KisApi.ItemDragController.isDragging && KisApi.ItemDragController.leasedItems.Length == 1) {
      MakeDraggedModelFromItem();
    }
  }
  #endregion

  #region Local utility methods
  /// <summary>Creates a part model, given there is only one item is being dragged.</summary>
  /// <remarks>
  /// The model will immediately become active, so it should be either disabled or positioned in the same frame.
  /// </remarks>
  void MakeDraggedModelFromItem() {
    DestroyDraggedModel();
    if (KisApi.ItemDragController.leasedItems.Length != 1) {
      DebugEx.Warning("Cannot make dragged model for multiple parts: leasedCount={0}",
                      KisApi.ItemDragController.leasedItems.Length);
      return; // Cannot be placed into the world.
    }
    DebugEx.Fine("Creating flight scene dragging model...");
    if (KisApi.ItemDragController.isDragging) {
      KisApi.ItemDragController.dragIconObj.gameObject.SetActive(false);
    }
    _draggedModel = new GameObject("KisDragModel").transform; //FIXME: make the model.
    _draggedModel.gameObject.SetActive(true); //FIXME: needed?
    var partItem = KisApi.ItemDragController.leasedItems[0];
    var draggedPart = MakeSamplePart(partItem.avPart, partItem.itemConfig);
    var dragModel = KisApi.PartModelUtils.GetSceneAssemblyModel(draggedPart);
    dragModel.transform.SetParent(_draggedModel, worldPositionStays: false);
    dragModel.transform.rotation = draggedPart.initRotation;
    _touchPointTransform = new GameObject("surfaceTouchPoint").transform;
    _touchPointTransform.SetParent(_draggedModel, worldPositionStays: false);
    var bounds = draggedPart.FindModelComponents<Collider>()
        .Select(x => x.bounds)
        .Aggregate(
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
  /// <returns><c>true</c> if the item can be dropped into the scene.</returns>
  bool PositionModelInTheScene() {
    var camera = FlightCamera.fetch.mainCamera;
    var ray = camera.ScreenPointToRay(Input.mousePosition);

    var colliderHit = Physics.Raycast(
        ray, out var hit, maxDistance: MaxRaycastDistance,
        //FIXME also 0x800000?
        layerMask: (int)(KspLayerMask.Part | KspLayerMask.Kerbal | KspLayerMask.SurfaceCollider),
        queryTriggerInteraction: QueryTriggerInteraction.Ignore);
    var color = colliderHit
        ? GoodToPlaceColor
        : NotGoodToPlaceColor;
    foreach (var renderer in _draggedModel.GetComponentsInChildren<Renderer>()) {
      renderer.material.color = color;
    }

    // If no surface or part is hit, then show the part being carried. 
    if (!colliderHit) {
      Hierarchy.SafeDestroy(_hitPointTransform);
      _hitPointTransform = null;
      var cameraTransform = camera.transform;
      _draggedModel.position =
          cameraTransform.position + ray.direction * HangingObjectDistance;
      _draggedModel.rotation = cameraTransform.rotation;

      //FIXME: position on screen
      return false;
    }
    var needNewHitTransform = _hitPointTransform == null; // Will be used for logging.
    if (needNewHitTransform) {
      _hitPointTransform = new GameObject("KISHitTarget").transform;
    }
    _hitPointTransform.position = hit.point;

    // Find out if a part was hit.
    var hitPart = FlightGlobals.GetPartUpwardsCached(hit.collider.transform.gameObject);
    if (hitPart == null) {
      // We've hit the surface. A lot of things may get wrong if the surface is not leveled!
      // Align the model to the celestial body normal and rely on the game's logic on the vessel
      // positioning. It may (and, likely, will) not match to what we may have presented in GUI.
      if (_hitPointTransform.parent != null || needNewHitTransform) {
        DebugEx.Fine("Hit surface: collider={0}, celestialBody={1}",
                     hit.collider.transform, FlightGlobals.ActiveVessel.mainBody);
        _hitPointTransform.SetParent(null);
      }
      var surfaceNorm = FlightGlobals.getUpAxis(FlightGlobals.ActiveVessel.mainBody, hit.point);
      _hitPointTransform.rotation = Quaternion.LookRotation(surfaceNorm);
    } else {
      // We've hit a part. Bind to this point!
      if (_hitPointTransform.parent != hitPart.transform || needNewHitTransform) {
        DebugEx.Fine("Hit part: part={0}", hitPart);
        _hitPointTransform.SetParent(hitPart.transform);
      }
      //FIXME: choose "not-up" when hitting the up/down plane of the target part. 
      _hitPointTransform.rotation = Quaternion.LookRotation(hit.normal, hitPart.transform.up);
    }
    AlignTransforms.SnapAlign(_draggedModel, _touchPointTransform, _hitPointTransform);
    return true;
  }

  /// <summary>Consumes the item being dragged and makes a scene vessel from it.</summary>
  void CreateVesselFromDraggedItem() {
    if (KisApi.ItemDragController.leasedItems.Length != 1) {
      throw new InvalidOperationException("Exactly one dragged item required to create a vessel");
    }
    var pos = _draggedModel.position;
    var rot = _draggedModel.rotation;
    var consumedItems = KisApi.ItemDragController.ConsumeItems();
    if (consumedItems == null || consumedItems.Length == 0) {
      DebugEx.Error("The leased item cannot be consumed");
      return;
    }
    //FIXME vessel.heightFromPartOffsetLocal = -vessel.HeightFromPartOffsetGlobal
    var protoVesselNode = KisApi.PartNodeUtils.MakeNewVesselNode(FlightGlobals.ActiveVessel, consumedItems[0], pos, rot);
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
        (int) KspLayer.Part, filterTranslucent: true, ignoreLayersMask: (int) KspLayerMask.TriggerCollider);
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

  /// <summary>Handles the keyboard and mouse events when KIS pickup  mode is enabled in flight.</summary>
  /// <remarks>It automatically ends as soon as the appropriate modifier key is released.</remarks>
  /// <seealso cref="KisFlightModeSwitchEvent"/>
  IEnumerator KisPickupEventHandler() {
    DebugEx.Info("Start KIS events handler in-flight");
    while (true) {
      if (KisApi.ItemDragController.isDragging) {
        // It's a safe guard. API can start it in background.
        DebugEx.Warning("Unexpectedly the dragging operation started while in in-flight action mode");
        break;
      }
      if (!EventChecker2.CheckEventActive(KisFlightModeSwitchEvent)) {
        break;
      }
      hoveredPart = Mouse.HoveredPart != null && !Mouse.HoveredPart.isVesselEVA ? Mouse.HoveredPart : null;
      UpdateInFlightTooltip();
      _slotEventsHandler.HandleActions();
      yield return null;
    }
    hoveredPart = null;
    KillInFlightTooltip();
    DebugEx.Info("End KIS events handler in-flight");
  }


  /// <summary>Updates the in-flight tooltip with the current data.</summary>
  /// <remarks>It's intended to be called on evey frame update. This method must be efficient.</remarks>
  void UpdateInFlightTooltip() {
    if (_currentTooltip == null) {
      _currentTooltip = UnityPrefabController.CreateInstance<UIKISInventoryTooltip.Tooltip>(
          "inFlightControllerTooltip", UIMasterController.Instance.actionCanvas.transform);
    }
    if (_slotEventsHandler.currentState == InFlightActionMode.SinglePartFocused) {
      KisContainerWithSlots.UpdateTooltip(_currentTooltip, new[] { InventoryItemImpl.FromPart(null, hoveredPart) });
    } else if (_slotEventsHandler.currentState == InFlightActionMode.PartAssemblyFocused) {
      // TODO(ihsoft): Implement!
      _currentTooltip.ClearInfoFields();
      _currentTooltip.title = "Cannot grab a hierarchy";
      _currentTooltip.baseInfo.text = string.Format("{0} part(s) attached", CountChildrenInHierarchy(hoveredPart));
    } else {
      _currentTooltip.ClearInfoFields();
      _currentTooltip.title = "Focus a part";
      _currentTooltip.baseInfo.text = null;
    }
    _currentTooltip.hints = _slotEventsHandler.GetHints();
    _currentTooltip.UpdateLayout();
  }

  /// <summary>Returns the total number of the parts in the hierarchy.</summary>
  static int CountChildrenInHierarchy(Part p) {
    return p.children.Count + p.children.Sum(CountChildrenInHierarchy);
  }

  /// <summary>Kills the in-flight tooltip if one exists.</summary>
  /// <remarks>It's a cleanup method. It never fails.</remarks>
  void KillInFlightTooltip() {
    if (_currentTooltip != null) {
      HierarchyUtils.SafeDestroy(_currentTooltip);
      _currentTooltip = null;
    }
  }

  /// <summary>Handles the in-flight part/hierarchy pick up action.</summary>
  void HandleScenePartPickup() {
    if (Mouse.HoveredPart == null) {
      // Unexpectedly this method is called when no part ios being hovered on. There is nothing to pick up!
      DebugEx.Info("No focused part to handle pickup");
      UISoundPlayer.instance.Play(KisApi.CommonConfig.sndPathBipWrong);
      return;  // Nothing to do.
    }
    // TODO(ihsoft): Implement!
    DebugEx.Warning("*** Pickup action executed!!!");
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
