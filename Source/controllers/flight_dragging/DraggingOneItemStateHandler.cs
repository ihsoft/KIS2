﻿// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KISAPIv2;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using UnityEngine;

namespace KIS2.controllers.flight_dragging {

/// <summary>Handles the keyboard and mouse events when KIS drop mode is active in flight.</summary>
sealed class DraggingOneItemStateHandler : AbstractStateHandler {
  #region Localizable strings
  /// <include file="../../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> DraggedPartDropPartHint = new(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Drop the part",
      description: "TBD");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message VesselPlacementModeHint = new(
      "",
      defaultTemplate: "Place as a vessel",
      description: "TBD");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<string> AttachmentNodeSelectedHint = new(
      "",
      defaultTemplate: "Attachment node selected: <color=yellow><b><<1>></b></color>",
      description: "TBD");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> ShowCursorTooltipHint = new(
      "",
      defaultTemplate: "[<<1>>]: Show tooltip",
      description: "TBD");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> HideCursorTooltipHint = new(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Hide tooltip",
      description: "TBD");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType, KeyboardEventType> CycleAttachNodesHint = new(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]/[<<2>>]</color></b>: Select attach node",
      description: "TBD");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType, KeyboardEventType> RotateByDegreesHint = new(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]/[<<2>>]</color></b>: Rotate by 15 degrees",
      description: "TBD");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message<KeyboardEventType> RotateResetHint = new(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Reset rotation",
      description: "TBD");

  // /// <include file="../../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> TogglePlacementModeHint = new(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Toggle placement mode",
      description: "TBD");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message PutOnTheGroundHint = new(
      "",
      defaultTemplate: "Put on the ground",
      description: "TBD");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message AlignAtThePartHint = new(
      "",
      defaultTemplate: "Drop at the part",
      description: "TBD");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> EnterAttachModeHint = new(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Hold to start the attach mode",
      description: "TBD");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> AttachModeActionHint = new(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Attach the part",
      description: "TBD");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message AttachToThePartHint = new(
      "",
      defaultTemplate: "Attach to part",
      description: "TBD");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message CannotAttachNoPartSelectedHint = new(
      "",
      defaultTemplate: "Select target part",
      description: "TBD");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message CannotAttachGenericHint = new(
      "",
      defaultTemplate: "Cannot attach part",
      description: "TBD");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message CannotAttachToKerbalError = new(
      "",
      defaultTemplate: "Kerbal is not a good target",
      description: "TBD");

  static readonly Message CreatingNewPartStatus = new(
      "",
      defaultTemplate: "Creating new part...",
      description: "TBD");
  #endregion

  #region Local fields
  /// <summary>Defines the drop action that is currently in effect.</summary>
  enum DropAction {
    /// <summary>The mouse cursor doesn't hit anything reasonable.</summary>
    NothingHit,
    /// <summary>The mouse cursor hovers over the surface or a part, and it's ok to drop.</summary>
    DropActionAllowed,
    /// <summary>The mouse cursor hovers over the surface or a part, and it's NOT ok to drop.</summary>
    DropActionImpossible,
    /// <summary>The attach mode selected and the mouse cursor hovers over a part which can be attached to.</summary>
    AttachActionAllowed,
    /// <summary>The attach mode selected, but the attach action is not possible.</summary>
    AttachActionImpossible,
    /// <summary>The attach mode selected, but no target part selected.</summary>
    NoPartSelectedForAttach,
    /// <summary>The mouse cursor hovers over a KIS target.</summary>
    /// <remarks>Such targets handle the drop logic themselves. So, the controller just stops interfering.</remarks>
    OverKisTarget,
  }

  /// <summary>The events state machine to control the drop stage.</summary>
  readonly EventsHandlerStateMachine<DropAction> _dropActionEventsHandler = new();

  /// <summary>Item that is currently being dragged.</summary>
  InventoryItem _draggedItem;

  /// <summary>Model of the part or assembly that is being dragged.</summary>
  /// <seealso cref="_vesselPlacementTouchPoint"/>
  /// <seealso cref="_hitPointTransform"/>
  Transform _draggedModel;

  /// <summary>Transform of the point which received the pointer hit.</summary>
  /// <remarks>
  /// It can be attached to part if a part has been hit, or be a static object if it was surface. This transform is
  /// dynamically created when something is hit and destroyed when there is nothing.
  /// </remarks>
  /// <seealso cref="_vesselPlacementTouchPoint"/>
  Transform _hitPointTransform;

  /// <summary>The rotation angle of the dragged part athe point of attachment.</summary>
  /// <remarks>
  /// It rotates the model around the normal at the attachment point. This angle applies to
  /// <see cref="_hitPointTransform"/> to make all the further transformations accounting it.
  /// </remarks>
  float _rotateAngle;

  /// <summary>The part that is being hit with the current drag.</summary>
  /// <seealso cref="_hitPointTransform"/>
  Part _hitPart;

  /// <summary>Indicates if the current mode is a vessel placement.</summary>
  /// <remarks>
  /// In the vessel placement mode the dragged parts are considered to be a vessel that should be "properly" placed on
  /// the ground. It means, that the assembly's logical "UP" should match the current situation "UP" meaning. On the
  /// surface of a celestial body the "UP" is usually defined by the gravitation normal vector.
  /// </remarks>
  bool _vesselPlacementMode = true;

  /// <summary>Indicates that the part should be attached on drop.</summary>
  bool _attachModeRequested;

  /// <summary>User friendly string that explains why the attachment cannot be done.</summary>
  string _cannotAttachDetails;

  /// <summary>Transform in the dragged model for the vessel placement mode.</summary>
  /// <remarks>It's always a child of the <see cref="_draggedModel"/>.</remarks>
  /// <seealso cref="_hitPointTransform"/>
  /// <seealso cref="_vesselPlacementMode"/>
  Transform _vesselPlacementTouchPoint;

  /// <summary>Transforms for all of the attach nodes of the currently held assembly.</summary>
  /// <remarks>The names must be unique.</remarks>
  /// <seealso cref="_hitPointTransform"/>
  readonly Dictionary<string, Transform> _attachNodeTouchPoints = new();

  /// <summary>The list of attach nodes to iterate over in the dragging GUI.</summary>
  /// <remarks>
  /// The list must be stably sorted to give a repeatable experience. The node IDs have to be the keys from
  /// <see cref="_attachNodeTouchPoints"/>.
  /// </remarks>
  AttachNode[] _attachNodes;

  /// <summary>The currently selected attach node if the placement mode is not "vessel".</summary>
  /// <seealso cref="_vesselPlacementMode"/>
  /// <seealso cref="_attachNodes"/>
  AttachNode _currentAttachNode;

  /// <summary>Screen message to present at the top when tooltip is disabled.</summary>
  /// <seealso cref="FlightItemDragController.toggleTooltipEvent"/>
  readonly ScreenMessage _showTooltipMessage = new("", Mathf.Infinity, ScreenMessageStyle.UPPER_CENTER);

  #endregion

  #region AbstractStateHandler implementation
  /// <inheritdoc/>
  public DraggingOneItemStateHandler(FlightItemDragController hostObj) : base(hostObj) {
    _dropActionEventsHandler.ONAfterTransition += (oldState, newState) => {
      DebugEx.Fine("Actions handler state changed: {0} => {1}", oldState, newState);
      _attachModeRequested = false;  // The relevant handlers should update it to the actual state.
    };

    // Free mode. Place the dragged assembly at any place within the distance.
    _dropActionEventsHandler.DefineAction(
        DropAction.DropActionAllowed, DraggedPartDropPartHint, hostObj.dropItemToSceneEvent,
        PlaceDraggedItem);
    _dropActionEventsHandler.DefineCustomHandler(
        DropAction.DropActionAllowed,
        RotateByDegreesHint.Format(hostObj.rotateLeftEvent.unityEvent, hostObj.rotateRightEvent.unityEvent),
        HandleRotateEvents);
    _dropActionEventsHandler.DefineAction(
        DropAction.DropActionAllowed, RotateResetHint, hostObj.rotateResetEvent, () => _rotateAngle = 0);
    _dropActionEventsHandler.DefineCustomHandler(
        DropAction.DropActionAllowed, 
        CycleAttachNodesHint.Format(hostObj.nodeCycleLeftEvent.unityEvent, hostObj.nodeCycleRightEvent.unityEvent),
        HandleCycleNodesEvents,
        checkIfAvailable: () => !_vesselPlacementMode && _attachNodes.Length > 1);
    _dropActionEventsHandler.DefineAction(
        DropAction.DropActionAllowed, TogglePlacementModeHint, hostObj.toggleDropModeEvent,
        () => _vesselPlacementMode = !_vesselPlacementMode,
        checkIfAvailable: () => _attachNodes.Length > 0);
    _dropActionEventsHandler.DefineCustomHandler(
        DropAction.DropActionAllowed, 
        EnterAttachModeHint.Format(hostObj.switchAttachModeKey.unityEvent),
        () => _attachModeRequested = hostObj.switchAttachModeKey.isEventActive,
        checkIfAvailable: () => !_vesselPlacementMode);
    _dropActionEventsHandler.DefineCustomHandler(
        DropAction.DropActionAllowed, HideCursorTooltipHint.Format(hostObj.toggleTooltipEvent.unityEvent),
        () => false);  // Only for the hints.

    // Attach mode.
    _dropActionEventsHandler.DefineAction(
        DropAction.AttachActionAllowed, AttachModeActionHint, hostObj.attachPartEvent,
        () => {});
    _dropActionEventsHandler.DefineCustomHandler(
        DropAction.AttachActionAllowed, null,
        () => _attachModeRequested = hostObj.switchAttachModeKey.isEventActive);
    _dropActionEventsHandler.DefineCustomHandler(
        DropAction.AttachActionAllowed, HideCursorTooltipHint.Format(hostObj.toggleTooltipEvent.unityEvent),
        () => false);  // Only for the hints.

    // Attach action is not possible.
    _dropActionEventsHandler.DefineCustomHandler(
        DropAction.AttachActionImpossible, null,
        () => _attachModeRequested = hostObj.switchAttachModeKey.isEventActive);

    // No focus part for attach mode.
    _dropActionEventsHandler.DefineCustomHandler(
        DropAction.NoPartSelectedForAttach, null,
        () => _attachModeRequested = hostObj.switchAttachModeKey.isEventActive);
  }

  /// <inheritdoc/>
  public override void Stop() {
    base.Stop();
    CrewHatchController.fetch.EnableInterface();
    _dropActionEventsHandler.currentState = null;
    SetDraggedMaterialPart(null);
    DestroyDraggedModel();
    ScreenMessages.RemoveMessage(_showTooltipMessage);
  }

  /// <inheritdoc/>
  protected override IEnumerator StateTrackingCoroutine() {
    _draggedItem = KisApi.ItemDragController.leasedItems[0];
    SetDraggedMaterialPart(_draggedItem.materialPart);
    MakeDraggedModelFromItem(_draggedItem);

    // Handle the dragging operation.
    while (isStarted) {
      CrewHatchController.fetch.DisableInterface(); // No hatch actions while we're targeting the drop location!

      if (KisApi.ItemDragController.focusedTarget == null) {
        // The holo model is hovering in the scene.
        _draggedModel.gameObject.SetActive(true);
        PositionModelInTheScene(_draggedItem);
        if (_hitPointTransform == null) {
          _dropActionEventsHandler.currentState = DropAction.NothingHit;
        } else if (!_attachModeRequested) {
          _dropActionEventsHandler.currentState = DropAction.DropActionAllowed;
        } else {
          if (_hitPart == null) {
            _dropActionEventsHandler.currentState = DropAction.NoPartSelectedForAttach;
          } else {
            _dropActionEventsHandler.currentState =
                CheckIfCanAttach() ? DropAction.AttachActionAllowed : DropAction.AttachActionImpossible;
          }
        }
      } else {
        // The mouse pointer is over a KIS inventory dialog. It will handle the behavior on itself.
        _draggedModel.gameObject.SetActive(false);
        _dropActionEventsHandler.currentState = DropAction.OverKisTarget;
      }
      UpdateDropTooltip();

      // Don't handle the keys in the same frame as the coroutine has started in to avoid double actions.
      yield return null;

      _dropActionEventsHandler.HandleActions();
    }
    // No logic beyond this point! The coroutine can be explicitly killed.
  }
  #endregion

  #region Local utility methods
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
  static float CalcPickupRotation(Component p) {
    if (p == null) {
      return 0;
    }
    var partTransform = p.transform;
    var partUp = partTransform.up;
    var goldenDir = Quaternion.LookRotation(partUp) * Vector3.down;
    var partDir = partTransform.forward;
    var dir = Vector3.Dot(partUp, Vector3.Cross(partDir, goldenDir)) < 0 ? 1.0f : -1.0f;
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
  /// <seealso cref="_dropActionEventsHandler"/>
  void UpdateDropTooltip() {
    if (hostObj.toggleTooltipEvent.CheckClick()) {
      _showTooltip = !_showTooltip;
    }
    if (!_showTooltip) {
      _showTooltipMessage.message = ShowCursorTooltipHint.Format(hostObj.toggleTooltipEvent.unityEvent);
      ScreenMessages.PostScreenMessage(_showTooltipMessage);
    } else {
      ScreenMessages.RemoveMessage(_showTooltipMessage);
    }

    string title = null;
    string baseInfo = null;
    var overrideVisibility = false;
    switch (_dropActionEventsHandler.currentState) {
      case DropAction.NothingHit or DropAction.OverKisTarget:
        break;
      case DropAction.DropActionAllowed:
        title = _hitPart == null ? PutOnTheGroundHint : AlignAtThePartHint;
        baseInfo = _vesselPlacementMode
            ? VesselPlacementModeHint
            : AttachmentNodeSelectedHint.Format(_currentAttachNode.id);
        break;
      case DropAction.DropActionImpossible:
        // TODO(ihsoft): Implement!
        break;
      case DropAction.AttachActionAllowed:
        title = AttachToThePartHint;
        baseInfo = _vesselPlacementMode
            ? VesselPlacementModeHint
            : AttachmentNodeSelectedHint.Format(_currentAttachNode.id);
        break;
      case DropAction.NoPartSelectedForAttach:
        title = CannotAttachNoPartSelectedHint;
        overrideVisibility = true;
        break;
      case DropAction.AttachActionImpossible:
        title = CannotAttachGenericHint;
        baseInfo = _cannotAttachDetails;
        overrideVisibility = true;
        break;
    }

    if (title != null && (_showTooltip || overrideVisibility)) {
      CreateTooltip();
      currentTooltip.title = title;
      currentTooltip.baseInfo.text = baseInfo;
      currentTooltip.hints = _dropActionEventsHandler.GetHints();
      currentTooltip.UpdateLayout();
    } else {
      DestroyCurrentTooltip();
    }
  }
  bool _showTooltip = true;

  /// <summary>Creates a part model, given there is only one item is being dragged.</summary>
  /// <remarks>
  /// The model will immediately become active, so it should be either disabled or positioned in the same frame.
  /// </remarks>
  void MakeDraggedModelFromItem(InventoryItem item) {
    if (_draggedModel != null) {
      return; // The model already exists.
    }
    DebugEx.Fine("Creating flight scene dragging model: root={0}", item.snapshot.partName);
    KisApi.ItemDragController.dragIconObj.gameObject.SetActive(false);
    var draggedPart = MakeSamplePart(item);
    _draggedModel = KisApi.PartModelUtils.GetSceneAssemblyModel(draggedPart).transform;
    _vesselPlacementTouchPoint =
        MakeTouchPoint("surfaceTouchPoint", draggedPart, _draggedModel, Vector3.up, _draggedModel.forward);

    _vesselPlacementMode = true;
    _currentAttachNode = null;
    _rotateAngle = CalcPickupRotation(item.materialPart);

    // Add touch points for every attach node.
    _attachNodeTouchPoints.Clear();
    _attachNodes = GetAllAttachNodes(draggedPart).OrderBy(x => x.id).ToArray();
    foreach (var an in _attachNodes) {
      var nodeTransform = new GameObject("attachNode-" + an.id).transform;
      nodeTransform.SetParent(_draggedModel, worldPositionStays: false);
      var orientation = an.nodeType != AttachNode.NodeType.Surface ? an.orientation : -an.orientation;
      //FIXME: Up is screwed.
      nodeTransform.localRotation = CheckIfParallel(orientation, Vector3.up)
          ? Quaternion.LookRotation(orientation, Vector3.forward)
          : Quaternion.LookRotation(orientation, Vector3.up);
      nodeTransform.localPosition = an.position / draggedPart.rescaleFactor;
      nodeTransform.localScale = Vector3.one;
      _attachNodeTouchPoints.Add(an.id, nodeTransform);

      // Pick the best default mode.
      if (an.nodeType == AttachNode.NodeType.Surface || _currentAttachNode == null && an.id == "bottom") {
        _vesselPlacementMode = false;
        _currentAttachNode = an;
        _rotateAngle = 0;
      }
    }
    if (item.materialPart != null && item.materialPart.parent != null) {
      var parentAttach = item.materialPart.FindAttachNodeByPart(item.materialPart.parent);
      if (parentAttach != null) {
        _currentAttachNode = parentAttach;
        _vesselPlacementMode = false;
      } else {
        DebugEx.Warning("Cannot find parent attach node on; {0}", item.materialPart);
      }
    } else if (_attachNodes.Length > 0) {
      _currentAttachNode = _attachNodes[0];
    }
    Hierarchy.SafeDestroy(draggedPart);
  }

  /// <summary>Adds a transform for the requested vessel orientation.</summary>
  /// <param name="tpName">The name of the transform.</param>
  /// <param name="srcPart">The part to capture colliders from. It must be in the default position and rotation.</param>
  /// <param name="tgtModel">The part model to attach the transform to.</param>
  /// <param name="direction">The main (forward) direction.</param>
  /// <param name="upwards">The "upwards" direction of the orientation.</param>
  Transform MakeTouchPoint(string tpName, Part srcPart, Transform tgtModel, Vector3 direction, Vector3 upwards) {
    var ptTransform = new GameObject(tpName).transform;
    ptTransform.SetParent(tgtModel, worldPositionStays: false);
    var distance = srcPart.FindModelComponents<Collider>()
        .Where(c => c.gameObject.layer == (int)KspLayer.Part)
        .Select(c => c.ClosestPoint(c.transform.position + -direction * 100).y)
        .Min();
    ptTransform.position += direction * distance;
    ptTransform.rotation = Quaternion.LookRotation(-direction, upwards);
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
  void PositionModelInTheScene(InventoryItem draggedItem) {
    var camera = FlightCamera.fetch.mainCamera;
    var ray = camera.ScreenPointToRay(Input.mousePosition);

    var hitsCount = Physics.RaycastNonAlloc(
        ray, _hitsBuffer,
        maxDistance: hostObj.maxRaycastDistance,
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
      _draggedModel.position = cameraTransform.position + ray.direction * hostObj.hangingObjectDistance;
      _draggedModel.rotation = cameraTransform.rotation;
      return;
    }
    var freshHitTransform = _hitPointTransform == null; // Will be used for logging.
    if (freshHitTransform) {
      _hitPointTransform = new GameObject("KISHitTarget").transform;
    }
    _hitPointTransform.position = hit.point;

    var touchPoint = _vesselPlacementMode ? _vesselPlacementTouchPoint : _attachNodeTouchPoints[_currentAttachNode.id];

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
          Quaternion.AngleAxis(_rotateAngle, hit.normal) * Quaternion.LookRotation(hit.normal, -fwd);
      AlignTransforms.SnapAlign(_draggedModel, touchPoint, _hitPointTransform);
      return;
    }

    // We've hit a part. Bind to this point!
    if (_hitPointTransform.parent != _hitPart.transform || freshHitTransform) {
      DebugEx.Fine("Hit part: part={0}", _hitPart);
      _hitPointTransform.SetParent(_hitPart.transform);
    }
    var partUp = _hitPart.transform.up;
    var partFwd = !CheckIfParallel(partUp, hit.normal) ? partUp : Vector3.up;
    _hitPointTransform.rotation =
        Quaternion.AngleAxis(_rotateAngle, hit.normal) * Quaternion.LookRotation(hit.normal, -partFwd);
    AlignTransforms.SnapAlign(_draggedModel, touchPoint, _hitPointTransform);
  }
  readonly RaycastHit[] _hitsBuffer = new RaycastHit[100];  // 100 is an arbitrary reasonable value for the hits count.

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
    var transform = part.transform;
    transform.position = Vector3.zero;
    transform.rotation = part.initRotation;
    part.InitializeModules();

    return part;
  }

  /// <summary>
  /// Consumes the item being dragged and either creates a new scene vessel from the item, or moves a material part in
  /// the scene.
  /// </summary>
  void PlaceDraggedItem() {
    var materialPart = _draggedItem.materialPart;
    if (materialPart != null) {
      _draggedItem.materialPart = null; // From here we'll be handling the material part.
    }

    // Consuming items will change the state, so capture all the important values before doing it.
    var refPart = _tgtPart ? _tgtPart : _hitPart;
    var refTransform = UnityEngine.Object.Instantiate(_hitPointTransform);
    var refPosition = _draggedModel.position;
    var refRotation = _draggedModel.rotation;

    var consumedItems = KisApi.ItemDragController.ConsumeItems(); // This changes the controller state!
    if (consumedItems == null || consumedItems.Length == 0) {
      DebugEx.Error("The leased item cannot be consumed");
      _draggedItem.materialPart = materialPart; // It didn't work, return the part back to the item.
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
      KisApi.VesselUtils.MoveVessel(materialPart.vessel, refPosition, refRotation, _hitPart);
    } else {
      var msg = ScreenMessages.PostScreenMessage(CreatingNewPartStatus, float.MaxValue, ScreenMessageStyle.UPPER_RIGHT);
      hostObj.StartCoroutine(
          VesselUtilsImpl.CreateLonePartVesselAndWait(
              consumedItems[0].snapshot, refPosition, refRotation,
              refTransform: refTransform, refPart: refPart,
              vesselCreatedFn: _ => ScreenMessages.RemoveMessage(msg)));
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
  Dictionary<int, Material[]> MakeDraggedPartGhost(Part p) {
    var resetRenderers = p.GetComponentsInChildren<Renderer>();
    var res = resetRenderers.ToDictionary(x => x.GetHashCode(), x => x.materials);
    Shader holoShader = null;
    if (hostObj.stdTransparentRenderer != "") {
      holoShader = Shader.Find(hostObj.stdTransparentRenderer);
      if (holoShader == null) {
        DebugEx.Error("Cannot find standard transparent renderer: {0}", hostObj.stdTransparentRenderer);
      }
    }
    if (holoShader != null) {
      DebugEx.Fine("Turning the target part into a holo: {0}", p);
      foreach (var resetRenderer in resetRenderers) {
        var newMaterials = new Material[resetRenderer.materials.Length];
        for (var i = 0; i < resetRenderer.materials.Length; ++i) {
          newMaterials[i] = new Material(holoShader) {
              color = hostObj.holoColor
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

  /// <summary>Verifies if two vectors are "almost parallel" regardless to their relative direction.</summary>
  static bool CheckIfParallel(Vector3 v1, Vector3 v2) {
    var angle = Vector3.Angle(v1, v2);
    return angle is < 1.0f or > 179.0f;
  }

  /// <summary>Returns all attach nodes on the part as a plain list.</summary>
  static IEnumerable<AttachNode> GetAllAttachNodes(Part p) {
    foreach (var an in p.attachNodes) {
      if (an.attachedPart != null && an.owner.parent != an.attachedPart) {
        continue;  // Skip occupied nodes, but allow the parent node since this link will be broken on move.        
      }
      yield return an;
    }
    if (p.attachRules.srfAttach && p.srfAttachNode.attachedPart == null) {
      // The name check is required! Sometimes the surface attach node objects exist when they shouldn't.
      yield return p.srfAttachNode;
    }
  }

  /// <summary>Handles part rotation events.</summary>
  /// <seealso cref="_dropActionEventsHandler"/>
  bool HandleRotateEvents() {
    if (hostObj.rotateLeftEvent.CheckClick()) {
      _rotateAngle -= 15;
      return true;
    }
    if (hostObj.rotateRightEvent.CheckClick()) {
      _rotateAngle += 15;
      return true;
    }
    return false;
  }

  /// <summary>Handles drop attach node selection events.</summary>
  /// <seealso cref="_dropActionEventsHandler"/>
  bool HandleCycleNodesEvents() {
    if (hostObj.nodeCycleLeftEvent.CheckClick()) {
      var idx = _attachNodes.IndexOf(_currentAttachNode);
      _currentAttachNode = _attachNodes[(_attachNodes.Length + idx - 1) % _attachNodes.Length];
      return true;
    }
    if (hostObj.nodeCycleRightEvent.CheckClick()) {
      var idx = _attachNodes.IndexOf(_currentAttachNode);
      _currentAttachNode = _attachNodes[(idx + 1) % _attachNodes.Length];
      return true;
    }
    return false;
  }

  /// <summary>Verifies if currently dragged part can be attached to the target.</summary>
  /// <remarks>
  /// The exhaustive set of conditions must be checked. The user friendly details should be listed in
  /// <see cref="_cannotAttachDetails"/> field. This information will be shown in the tooltip.
  /// </remarks>
  /// <returns><c>true</c> if the attachment can be made.</returns>
  /// <seealso cref="_hitPart"/>
  /// <seealso cref="_currentAttachNode"/>
  bool CheckIfCanAttach() {
    _cannotAttachDetails = null;
    if (_hitPart.isVesselEVA) {
      _cannotAttachDetails = CannotAttachToKerbalError;
    }
    return _cannotAttachDetails == null;
  }
  #endregion
}
}
