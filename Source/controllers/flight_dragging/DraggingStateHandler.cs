﻿// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KISAPIv2;
using KSP.UI;
using KSPDev.ConfigUtils;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.InputUtils;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using KSPDev.Unity;
using UnityEngine;

namespace KIS2.controllers.flight_dragging {

/// <summary>Handles the keyboard and mouse events when KIS drop mode is active in flight.</summary>
[PersistentFieldsDatabase("KIS2/settings2/KISConfig")]
sealed class DraggingStateHandler : AbstractStateHandler {
  #region Localizable strings
  /// <include file="../../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> DraggedPartDropPartHint = new(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Drop at the part",
      description: "TBD");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> DraggedPartDropSurfaceHint = new(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Put on the ground",
      description: "TBD");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message VesselPlacementModeHint = new(
      "",
      defaultTemplate: "Place as a vessel",
      description: "TBD");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<string> PartAttachmentNodePlacementModeHint = new(
      "",
      defaultTemplate: "Place at attachment node: <<1>>",
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
  static readonly Message<KeyboardEventType, KeyboardEventType> RotateBy30DegreesHint = new(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]/[<<2>>]</color></b>: Rotate by 30 degrees",
      description: "TBD");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType, KeyboardEventType> RotateBy5DegreesHint = new(
      "",
      defaultTemplate: "<b><color=#5a5>[<<1>>]/[<<2>>]</color></b>: Rotate by 5 degrees",
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
  #endregion

  #region Configuration
  // ReSharper disable FieldCanBeMadeReadOnly.Local
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

  /// <summary>The key that toggles dragging tooltip visibility.</summary>
  /// <remarks>
  /// It's a standard keyboard event definition. Even though it can have modifiers, avoid specifying them since it may
  /// affect the UX experience.
  /// </remarks>
  [PersistentField("PickupMode/toggleTooltipKey")]
  static string _toggleTooltipKey = "t";

  // ReSharper enable FieldCanBeMadeReadOnly.Local
  // ReSharper enable ConvertToConstant.Local
  #endregion

  #region Key bindings
  static ClickEvent _toggleTooltipEvent = new(Event.KeyboardEvent(_toggleTooltipKey));
  static readonly ClickEvent DropItemToSceneEvent = new(Event.KeyboardEvent("mouse0"));
  static readonly ClickEvent Rotate30LeftEvent = new(Event.KeyboardEvent("a"));
  static readonly ClickEvent Rotate30RightEvent = new(Event.KeyboardEvent("d"));
  static readonly ClickEvent Rotate5LeftEvent = new(Event.KeyboardEvent("q"));
  static readonly ClickEvent Rotate5RightEvent = new(Event.KeyboardEvent("e"));
  static readonly ClickEvent RotateResetEvent = new(Event.KeyboardEvent("space"));
  static readonly ClickEvent ToggleDropModeEvent = new(Event.KeyboardEvent("r"));
  static readonly ClickEvent NodeCycleLeftEvent = new(Event.KeyboardEvent("w"));
  static readonly ClickEvent NodeCycleRightEvent = new(Event.KeyboardEvent("s"));
  #endregion

  #region Local fields
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

  /// <summary>Transform in the dragged model for the vessel placement mode.</summary>
  /// <remarks>It's always a child of the <see cref="_draggedModel"/>.</remarks>
  /// <seealso cref="_hitPointTransform"/>
  /// <seealso cref="_vesselPlacementMode"/>
  Transform _vesselPlacementTouchPoint;

  /// <summary>Transforms for all of the attach nodes of the currently held assembly.</summary>
  /// <remarks>The names must be unique.</remarks>
  /// <seealso cref="_hitPointTransform"/>
  readonly Dictionary<string, Transform> _attachNodeTouchPoints = new();

  /// <summary>The list of attach node names to iterate over in the dragging GUI.</summary>
  /// <remarks>
  /// The list must be stably sorted to give a repeatable experience. The values have to be the keys from
  /// <see cref="_attachNodeTouchPoints"/>.
  /// </remarks>
  List<string> _attachNodeNames;

  /// <summary>The currently selected attach node if the placement node is not "vessel".</summary>
  /// <seealso cref="_vesselPlacementMode"/>
  string _currentAttachNodeName;

  /// <summary>Screen message to present at the top when tooltip is disabled.</summary>
  /// <seealso cref="_toggleTooltipEvent"/>
  readonly ScreenMessage _showTooltipMessage = new("", Mathf.Infinity, ScreenMessageStyle.UPPER_CENTER);
  #endregion

  #region AbstractStateHandler implementation
  /// <inheritdoc/>
  public override void Init(FlightItemDragController aHostObj) {
    base.Init(aHostObj);
    ConfigAccessor.ReadFieldsInType(GetType(), null); // All config fields are static.
    _toggleTooltipEvent = new ClickEvent(Event.KeyboardEvent(_toggleTooltipKey));

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
  }

  /// <inheritdoc/>
  public override void Stop() {
    base.Stop();
    CrewHatchController.fetch.EnableInterface();
    _dropTargetEventsHandler.currentState = null;
    SetDraggedMaterialPart(null);
    DestroyDraggedModel();
    ScreenMessages.RemoveMessage(_showTooltipMessage);
  }

  /// <inheritdoc/>
  protected override IEnumerator StateTrackingCoroutine() {
    var singleItem = KisApi.ItemDragController.leasedItems.Length == 1
        ? KisApi.ItemDragController.leasedItems[0]
        : null;
    SetDraggedMaterialPart(singleItem?.materialPart);

    // Handle the dragging operation.
    while (isStarted) {
      UpdateDropActions();
      CrewHatchController.fetch.DisableInterface(); // No hatch actions while we're targeting the drop location!

      // Track the mouse cursor position and update the view.
      if (KisApi.ItemDragController.focusedTarget == null) {
        // The cursor is in a free space, the controller deals with it.
        if (singleItem != null) {
          MakeDraggedModelFromItem(singleItem);
          _dropTargetEventsHandler.currentState = PositionModelInTheScene(singleItem);
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
  #endregion

  #region API methods

  /// <summary>Indicates if the currently dragged items can be handled by this handler.</summary>
  /// <seealso cref="IKisDragTarget.OnKisDrag"/>
  public bool CanHandleDraggedItems() {
    if (_dropTargetEventsHandler.currentState is DropTarget.Nothing or DropTarget.KisTarget) {
      // In these states the controller doesn't deal with the dragged item(s).
      return false;
    }
    return KisApi.ItemDragController.leasedItems.Length == 1;
  }

  #endregion

  #region Local utility methods
  /// <summary>Handles actions to rotate the dragged part around it's Z-axis.</summary>
  /// <remarks>
  /// The actions change rotation at the touch point level. If there are multiple points, the rotation should be reset
  /// before switching.
  /// </remarks>
  void UpdateDropActions() {
    if (Rotate30LeftEvent.CheckClick()) {
      _rotateAngle -= 30;
    } else if (Rotate30RightEvent.CheckClick()) {
      _rotateAngle += 30;
    }
    if (Rotate5LeftEvent.CheckClick()) {
      _rotateAngle -= 5;
    } else if (Rotate5RightEvent.CheckClick()) {
      _rotateAngle += 5;
    }
    if (RotateResetEvent.CheckClick()) {
      _rotateAngle = 0;
    }
    if (ToggleDropModeEvent.CheckClick()) {
      if (_attachNodeNames.Count > 0) {
        _vesselPlacementMode = !_vesselPlacementMode;
      } else {
        _vesselPlacementMode = true;
      }
    }
    if (!_vesselPlacementMode) {
      if (NodeCycleLeftEvent.CheckClick()) {
        var idx = _attachNodeNames.IndexOf(_currentAttachNodeName);
        _currentAttachNodeName = _attachNodeNames[(_attachNodeNames.Count + idx - 1) % _attachNodeNames.Count];
      } else if (NodeCycleRightEvent.CheckClick()) {
        var idx = _attachNodeNames.IndexOf(_currentAttachNodeName);
        _currentAttachNodeName = _attachNodeNames[(idx + 1) % _attachNodeNames.Count];
      }
    }
  }

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
    currentTooltip.title = _dropTargetEventsHandler.currentState == DropTarget.Surface
        ? PutOnTheGroundHint
        : AlignAtThePartHint;
    currentTooltip.baseInfo.text = _vesselPlacementMode
        ? VesselPlacementModeHint
        : PartAttachmentNodePlacementModeHint.Format(_currentAttachNodeName);
    var hints = _dropTargetEventsHandler.GetHintsList();
    hints.Add(TogglePlacementModeHint.Format(ToggleDropModeEvent.unityEvent));
    if (!_vesselPlacementMode) {
      hints.Add(CycleAttachNodesHint.Format(NodeCycleLeftEvent.unityEvent, NodeCycleRightEvent.unityEvent));
    }
    hints.Add(RotateBy30DegreesHint.Format(Rotate30LeftEvent.unityEvent, Rotate30RightEvent.unityEvent));
    hints.Add(RotateBy5DegreesHint.Format(Rotate5LeftEvent.unityEvent, Rotate5RightEvent.unityEvent));
    hints.Add(RotateResetHint.Format(RotateResetEvent.unityEvent));
    hints.Add(HideCursorTooltipHint.Format(_toggleTooltipEvent.unityEvent));
    currentTooltip.hints = string.Join("\n", hints);
    currentTooltip.UpdateLayout();
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
    DebugEx.Fine("Creating flight scene dragging model...");
    _rotateAngle = CalcPickupRotation(item.materialPart);
    KisApi.ItemDragController.dragIconObj.gameObject.SetActive(false);
    _draggedModel = new GameObject("KisDragModel").transform;
    _draggedModel.gameObject.SetActive(true);
    var draggedPart = MakeSamplePart(item);
    var dragModel = KisApi.PartModelUtils.GetSceneAssemblyModel(draggedPart);
    dragModel.transform.SetParent(_draggedModel, worldPositionStays: false);
    _vesselPlacementTouchPoint =
        MakeTouchPoint("surfaceTouchPoint", draggedPart, _draggedModel, Vector3.up, _draggedModel.forward);

    // Add touch points for every attach node.
    _attachNodeTouchPoints.Clear();
    foreach (var an in draggedPart.attachNodes) {
      var nodeTransform = new GameObject("attachNode-" + an.id).transform;
      nodeTransform.SetParent(_draggedModel, worldPositionStays: true);
      nodeTransform.localPosition = an.position / draggedPart.rescaleFactor;
      nodeTransform.localRotation = CheckIfParallel(an.orientation, Vector3.up)
          ? Quaternion.LookRotation(an.orientation, Vector3.forward)
          : Quaternion.LookRotation(an.orientation, Vector3.up);
      _attachNodeTouchPoints.Add(an.id, nodeTransform);
    }
    _attachNodeNames = new List<string>(_attachNodeTouchPoints.Keys);
    _attachNodeNames.Sort();
    _vesselPlacementMode = true;
    _currentAttachNodeName = "";
    if (_attachNodeNames.Count > 0) {
      _currentAttachNodeName = _attachNodeNames.IndexOf("bottom") != -1 ? "bottom" : _attachNodeNames[0];
    }
    if (item.materialPart != null && item.materialPart.parent != null) {
      var parentAttach = item.materialPart.FindAttachNodeByPart(item.materialPart.parent);
      if (parentAttach != null) {
        _currentAttachNodeName = parentAttach.id;
        _vesselPlacementMode = false;
      } else {
        DebugEx.Warning("Cannot find parent attach node on; {0}", item.materialPart);
      }
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

    var touchPoint = _vesselPlacementMode ? _vesselPlacementTouchPoint : _attachNodeTouchPoints[_currentAttachNodeName];

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
      AlignTransforms.SnapAlign(_draggedModel, touchPoint, _hitPointTransform);
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
    AlignTransforms.SnapAlign(_draggedModel, touchPoint, _hitPointTransform);

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
