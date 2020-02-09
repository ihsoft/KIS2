// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Experience.Effects;
using KISAPIv2;
using KSPDev.ConfigUtils;
using KSPDev.InputUtils;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using System.Linq;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>Controller that deals with dragged items in the editor scenes.</summary>
[KSPAddon(KSPAddon.Startup.Flight, false /*once*/)]
internal sealed class FlightItemDragController : MonoBehaviour, IKisDragTarget {
  #region Event static configs
  static readonly Event DropItemToScene = Event.KeyboardEvent("mouse0");
  #endregion

  #region Local fields and properties
  bool isActiveModelInScene =>
      _savedFlightPartModel != null && _savedFlightPartModel.gameObject.activeSelf;

  static readonly Color GoodToPlaceColor = new Color(0f, 1f, 0f, 0.2f);
  static readonly Color NotGoodToPlaceColor = new Color(1f, 0f, 0f, 0.2f);

  const float MaxRaycastDistance = 50;
  Transform _savedFlightPartModel;
  Transform _hitTransform;
  Transform _surfaceTouchPoint;
  #endregion

  #region MonoBehaviour overrides
  void Awake() {
    DebugEx.Fine("[{0}] Controller started", nameof(FlightItemDragController));
    KisApi.ItemDragController.RegisterTarget(this);
  }

  void OnDestroy() {
    DebugEx.Fine("[{0}] Controller stopped", nameof(FlightItemDragController));
    KisApi.ItemDragController.UnregisterTarget(this);
  }

  void Update() {
    if (!Input.anyKeyDown) {
      return; // Only event handlers are here. 
    }
    if (isActiveModelInScene) {
      if (EventChecker2.CheckClickEvent(DropItemToScene)) {
        CreateVesselFromDraggedItem();
      }
    } else if (!KisApi.ItemDragController.isDragging) {
      //FIXME: implement
    }
  }
  #endregion

  #region IKisDragTarget implementation
  /// <inheritdoc/>
  void IKisDragTarget.OnKisDragStart() {
    if (KisApi.ItemDragController.focusedTarget == null
        && KisApi.ItemDragController.leasedItems.Length == 1) {
      MakeDraggedModelFromItem();
    }
  }

  /// <inheritdoc/>
  void IKisDragTarget.OnKisDragEnd(bool isCancelled) {
    DestroyDraggedModel();
  }

  /// <inheritdoc/>
  bool IKisDragTarget.OnKisDrag(bool pointerMoved) {
    //FIXME: how to know result?
    PositionModelInTheScene();
    return isActiveModelInScene; // Not of our concern.
  }

  /// <inheritdoc/>
  void IKisDragTarget.OnFocusTarget(GameObject newTarget) {
    if (newTarget != null) {
      if (_savedFlightPartModel != null) {
        _savedFlightPartModel.gameObject.SetActive(false);
      }
    } else {
      if (KisApi.ItemDragController.isDragging) {
        if (_savedFlightPartModel == null) {
          MakeDraggedModelFromItem();
        } else {
          _savedFlightPartModel.gameObject.SetActive(true);
        }
      }
    }
  }
  #endregion

  #region Local utility methods
  /// <summary>Creates a part model, given there is only one item is being dragged.</summary>
  /// <remarks>
  /// The model will immediately become, so it should be either disabled or positioned in teh same
  /// frame.
  /// </remarks>
  void MakeDraggedModelFromItem() {
    DestroyDraggedModel();
    if (KisApi.ItemDragController.leasedItems.Length != 1) {
      return; // Cannot be placed into the world.
    }
    DebugEx.Fine("Creating flight scene dragging model...");
    _savedFlightPartModel = new GameObject("KisDragModel").transform; //FIXME: make the model.
    _savedFlightPartModel.gameObject.SetActive(true); //FIXME: needed?
    var partItem = KisApi.ItemDragController.leasedItems[0];
    var draggedPart = MakeSamplePart(partItem.avPart, partItem.itemConfig);
    var dragModel = KisApi.PartModelUtils.GetSceneAssemblyModel(draggedPart);
    dragModel.transform.SetParent(_savedFlightPartModel, worldPositionStays: false);
    dragModel.transform.rotation = draggedPart.initRotation;
    _surfaceTouchPoint = new GameObject("surfaceTouchPoint").transform;
    _surfaceTouchPoint.SetParent(_savedFlightPartModel, worldPositionStays: false);
    //FIXME: take it from the colliders, not meshes.
    var bounds = KisApi.PartModelUtils.GetPartBounds(draggedPart);
    var dist = bounds.center.y + bounds.extents.y;
    _surfaceTouchPoint.position += -_savedFlightPartModel.up * dist;  
    _surfaceTouchPoint.rotation = Quaternion.LookRotation(
        -_savedFlightPartModel.up, -_savedFlightPartModel.forward);
    Hierarchy.SafeDestory(draggedPart);
  }

  /// <summary>Cleans up the dragged model.</summary>
  /// <remarks>It's safe to call it many times.</remarks>
  void DestroyDraggedModel() {
    if (_savedFlightPartModel == null) {
      return; // Nothing to do.
    }
    DebugEx.Fine("Destroying flight scene dragging model...");
    Hierarchy.SafeDestory(_savedFlightPartModel);
    _savedFlightPartModel = null;
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

    RaycastHit hit;
    var colliderHit = Physics.Raycast(
        ray, out hit, maxDistance: MaxRaycastDistance,
        //FIXME also 0x800000?
        layerMask: (int)(KspLayerMask.Part | KspLayerMask.Kerbal | KspLayerMask.SurfaceCollider),
        queryTriggerInteraction: QueryTriggerInteraction.Ignore);
    var color = colliderHit
        ? GoodToPlaceColor
        : NotGoodToPlaceColor;
    foreach (var renderer in _savedFlightPartModel.GetComponentsInChildren<Renderer>()) {
      renderer.material.color = color;
    }

    // If no surface or parts is hit, then just cleanup the hit point. 
    if (!colliderHit) {
      DebugEx.Fine("Not hitting anything. Destroying the hit object...");
      Hierarchy.SafeDestory(_hitTransform);
      _hitTransform = null;
      return false;
    }
    var newHitTransform = false;
    if (_hitTransform == null) {
      _hitTransform = new GameObject("KISHitTarget").transform;
      newHitTransform = true;
    }
    _hitTransform.position = hit.point;

    // Adjust hit object hierarchy.
    var hitPart = FlightGlobals.GetPartUpwardsCached(hit.collider.transform.gameObject);
    if (hitPart == null) {
      // Hit the surface. A lot of things may get wrong if the surface is not leveled!
      // Align the model to the celestial body normal and rely on the game's logic on the vessel
      // positioning. It may (and, likely, will) not match to what we may have presented in GUI.
      if (_hitTransform.parent != null || newHitTransform) {
        DebugEx.Fine("Hit surface: at={0}, norm={1}", hit.point, hit.normal);
        _hitTransform.SetParent(null);
      }
      var surfaceNorm = FlightGlobals.getUpAxis(FlightGlobals.ActiveVessel.mainBody, hit.point);
      _hitTransform.rotation = Quaternion.LookRotation(surfaceNorm);
    } else {
      // Hit a part. Bind to this point!
      if (_hitTransform.parent == null || newHitTransform) {
        DebugEx.Fine("Hit part: part={0}, at={1}, norm={2}", hitPart, hit.point, hit.normal);
        _hitTransform.SetParent(hitPart.transform);
      }
      //FIXME: choose "not-up" when hitting the up/down plane of the target part. 
      _hitTransform.rotation = Quaternion.LookRotation(hit.normal, hitPart.transform.up);
    }
    AlignTransforms.SnapAlign(_savedFlightPartModel, _surfaceTouchPoint, _hitTransform);
    return true;
  }

  /// <summary>Consumes the item being dragged and makes a scene vessel from it.</summary>
  void CreateVesselFromDraggedItem() {
    var pos = _savedFlightPartModel.position;
    var rot = _savedFlightPartModel.rotation;
    var partItem = KisApi.ItemDragController.leasedItems[0];
    //FIXME: cancel for debug, must be consume in prod
    KisApi.ItemDragController.CancelItemsLease();
    //FIXME vessel.heightFromPartOffsetLocal = -vessel.HeightFromPartOffsetGlobal
    var protoVesselNode = GetProtoVesselNode(FlightGlobals.ActiveVessel, partItem.avPart, pos, rot);
    HighLogic.CurrentGame.AddVessel(protoVesselNode);
  }

  static ConfigNode GetProtoVesselNode(
      Vessel actorVessel, AvailablePart avPart, Vector3 partPosition, Quaternion rotation) {
    //CheckGroundCollision()
    var orbit = new Orbit(actorVessel.orbit);
    var vesselName = avPart.title;
    var configNode = ProtoVessel.CreateVesselNode(
        vesselName, VesselType.Debris, orbit, 0,
        new[] {
            //FIXME: itemconfig
            CreatePartNode(avPart, null, actorVessel)
        });

    //FIXME: need more than one part, !vesselSpawning && skipGroundPositioning 
    configNode.SetValue("skipGroundPositioning", newValue: true, createIfNotFound: true);
    configNode.SetValue("vesselSpawning", newValue: true, createIfNotFound: true);
    configNode.SetValue("prst", newValue: true, createIfNotFound: true);
    configNode.SetValue("sit", newValue: actorVessel.SituationString, createIfNotFound: true);
    //FIXME: do it when placing on surface only? or relay on repositioning?
    configNode.SetValue("landed", newValue: actorVessel.Landed, createIfNotFound: true);
    configNode.SetValue("splashed", newValue: actorVessel.Splashed, createIfNotFound: true);
    configNode.SetValue("displaylandedAt", actorVessel.displaylandedAt, createIfNotFound: true);
    
    double alt;
    double lat;
    double lon;
    actorVessel.mainBody.GetLatLonAlt(partPosition, out lat, out lon, out alt);
    configNode.SetValue("lat", newValue: lat, createIfNotFound: true);
    configNode.SetValue("lon", newValue: lon, createIfNotFound: true);
    configNode.SetValue("alt", newValue: alt, createIfNotFound: true);

    var refRotation = actorVessel.mainBody.bodyTransform.rotation.Inverse() * rotation;
    configNode.SetValue(
        "rot", newValue: KSPUtil.WriteQuaternion(refRotation), createIfNotFound: true);
    configNode.SetValue("PQSMin", 0, createIfNotFound: true);
    configNode.SetValue("PQSMax", 0, createIfNotFound: true);

    //FIXME
    DebugEx.Warning("*** vessel node:\n{0}", configNode);

    return configNode;
  }

  Part MakeSamplePart(AvailablePart partInfo, ConfigNode itemConfig) {
    var part = Instantiate(partInfo.partPrefab);
    part.gameObject.SetLayerRecursive((int) KspLayer.Part,
                                      filterTranslucent: true,
                                      ignoreLayersMask: (int) KspLayerMask.TriggerCollider);
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
  #endregion

  #region API/Utility candidates
  /// <summary>Creates a part node snapshot, given the custom config.</summary>
  /// <remarks>
  /// Takes into account if the part being snapshot`ed is a ground experiment part. If it is, then
  /// the actor's vessel modifiers will be applied.
  /// </remarks>
  /// <param name="avPart">The part info.</param>
  /// <param name="partNode">The optional part's config.</param>
  /// <param name="actorVessel">The actor vessel to get modifiers and flag from.</param>
  /// <returns>The part's config node.</returns>
  static ConfigNode CreatePartNode(AvailablePart avPart, ConfigNode partNode, Vessel actorVessel) {
    var flightId = ShipConstruction.GetUniqueFlightID(HighLogic.CurrentGame.flightState);
    var configNode = ProtoVessel.CreatePartNode(avPart.name, flightId, null);
    configNode.SetValue("flag", actorVessel.rootPart.flagURL, createIfNotFound: true);
    var prefabNodes = avPart.partConfig.GetNodes("MODULE");
    partNode = partNode ?? avPart.partConfig;
    var itemNodes = partNode.GetNodes("MODULE");
    var itemNodeIndex = 0;
    for (var i = 0; i < prefabNodes.Length; i++) {
      var prefabNode = prefabNodes[i];
      while (itemNodeIndex < itemNodes.Length
          && prefabNode.GetValue("name") != itemNodes[itemNodeIndex].GetValue("name")) {
        DebugEx.Warning(
            "Skip item module CFG due to it doesn't match the prefab:"
            + " part={0}, prefabModule={1}, itemModule={2}",
            avPart.name, i, itemNodeIndex);
        ++itemNodeIndex;
      }
      var moduleNode = itemNodeIndex < itemNodes.Length
          ? itemNodes[itemNodeIndex].CreateCopy()
          : new ConfigNode("MODULE");
      var moduleName = moduleNode.GetValue("name");
      if (moduleName == nameof(ModuleGroundSciencePart)) {
        // Adjust power production to the EVA kerbal skill.
        var powerUnits = ConfigAccessor.GetValueByPath<float>(moduleNode, "powerUnitsProduced")
            ?? 0;
        powerUnits += GetExperimentPartPowerProductionModifier(avPart, actorVessel);
        moduleNode.SetValue("powerUnitsProduced", powerUnits);
      } else if (moduleName == nameof(ModuleGroundSciencePart)) {
        // Adjust science modifier to the EVA kerbal skill.
        var scienceModifier = GetExperimentPartScienceModifier(avPart, actorVessel);
        moduleNode.SetValue("ScienceModifierRate", scienceModifier, createIfNotFound: true);
      }
      configNode.AddNode(moduleNode);
    }
    return configNode;
  }

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
