// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections;
using System.Linq;
using KISAPIv2;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.LogUtils;
using KSPDev.ProcessingUtils;
using UnityEngine;

namespace KIS2.controllers.flight_dragging {

/// <summary>Handles the in-flight part/hierarchy pick up event action.</summary>
/// <remarks>
/// <p>It's required that there is a part being hovered. This part will be offered for the KIS dragging action.</p>
/// <p>If the part being dragged is attached to a vessel, then it will get decoupled on consume.</p>
/// <p>
/// The consumed part will <i>DIE</i>. Which will break the link between the part and the item. And it may have effects
/// on the contracts state if the part was a part of any.
/// </p>
/// </remarks>
sealed class PickupStateHandler : AbstractStateHandler {
  #region Localizable strings
  /// <include file="../../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> TakeFocusedPartHint = new(
      "#KIS2-TBD",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Grab the part",
      description: "The keyboard hint to present when the focused part can be picked up to move it into inventory.");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> PickupGroundPartHint = new(
      "#KIS2-TBD",
      defaultTemplate: "<b><color=#5a5>[<<1>>]</color></b>: Collect ground part",
      description: "The keyboard hint to present when the focused part is a deployed ground part that can be picked"
      + " up by the kerbal.");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message/*"/>
  static readonly Message VesselNotReadyMsg = new(
      "#KIS2-TBD",
      defaultTemplate: "Part not ready",
      description: "The message to present when the hovered part cannot be picked up because of its vessel has not yet"
      + " unpacked.");
  
  /// <include file="../../SpecialDocTags.xml" path="Tags/Message/*"/>
  static readonly Message CannotGrabHierarchyTooltipMsg = new(
      "#KIS2-TBD",
      defaultTemplate: "Cannot grab a hierarchy",
      description: "It's a temp string. DO NOT localize it!");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<int> CannotGrabHierarchyTooltipDetailsMsg = new(
      "#KIS2-TBD",
      defaultTemplate: "<<1>> part(s) attached",
      description: "It's a temp string. DO NOT localize it!");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message/*"/>
  static readonly Message KerbalNeededToPickupPartErr = new(
      "#KIS2-TBD",
      defaultTemplate: "Kerbal needed to work with this part",
      description: "Error string to present when the target EVA part can only be handled by a kerbal.");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message/*"/>
  static readonly Message DeployedGroundPartInfo = new(
      "#KIS2-TBD",
      defaultTemplate: "<b><color=yellow>Deployed ground part</color></b>",
      description: "Info string to add to part info when the focused part is a deployed ground part.");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message/*"/>
  static readonly Message DeployingGroundPartMsg = new(
      "#KIS2-TBD",
      defaultTemplate: "Part is being deployed",
      description: "Info string to present when the target EVA part is a ground part that is in a process of deploying"
      + " into the scene. In this state the part cannot be interacted with.");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message/*"/>
  static readonly Message RetrievingGroundPartMsg = new(
      "#KIS2-TBD",
      defaultTemplate: "Part is being retrieved",
      description: "Info string to present when the target EVA part is a ground part that is in a process of being"
      + " retrieved from the scene. In this state the part cannot be interacted with.");

  /// <include file="../../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<string> RetrievingPartMsg = new(
      "#KIS2-TBD",
      defaultTemplate: "Retrieving part: <<1>>",
      description: "Status message that is shown while a ground part is being retrieved. The parameter is the part's"
      + " name.");
  #endregion

  #region Local fields
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
        _groundPartModule = null;
      }
      _targetPickupPart = value;
      if (_targetPickupPart != null) {
        _targetPickupPart.SetHighlightType(Part.HighlightType.AlwaysOn);
        _targetPickupPart.SetHighlight(true, recursive: true);
        if (_targetPickupPart.children.Count == 0) {
          _targetPickupItem = InventoryItemImpl.FromPart(_targetPickupPart);
          _targetPickupItem.materialPart = _targetPickupPart;
          _groundPartModule = _targetPickupPart.FindModulesImplementing<ModuleGroundPart>().FirstOrDefault();
        }
      }
    }
  }
  Part _targetPickupPart;
  InventoryItem _targetPickupItem;
  ModuleGroundPart _groundPartModule;

  static readonly ReflectedField<ModuleGroundPart, bool> GroundPartBeingRetrievedField = new("beingRetrieved");
  static readonly ReflectedField<ModuleGroundPart, bool> GroundPartBeingDeployedField = new("beingDeployed");
  static readonly ReflectedField<ModuleGroundPart, bool> GroundPartDeployedOnGroundField = new("deployedOnGround");

  /// <summary>Defines the currently focused pickup target.</summary>
  /// <remarks>The <c>null</c> state is used to indicate that nothing of the interest is being focused.</remarks>
  enum PickupTarget {
    /// <summary>Lone part or the last child of a vessel.</summary>
    SinglePart,
    /// <summary>Part that has some children.</summary>
    PartAssembly,
    /// <summary>Part that belongs to a packed vessel (not yet live in the world).</summary>
    PackedVessel,
    /// <summary>Part that implements ground part module AND is deployed on the ground.</summary>
    DeployedGroundPart,
    /// <summary>Part that implements ground part module AND is in a process of being deployed.</summary>
    PendingDeployGroundPart,
    /// <summary>Part that implements ground part module AND is in a process of being retrieved.</summary>
    PendingRetrieveGroundPart,
  }

  /// <summary>The events state machine to control the pickup stage.</summary>
  readonly EventsHandlerStateMachine<PickupTarget> _pickupTargetEventsHandler = new();

  /// <summary>The control that blocks RMB action during this handler lifetime.</summary>
  RightClickBlocker _rightClickBlocker;
  #endregion

  #region AbstractStateHandler implementation
  /// <inheritdoc/>
  public PickupStateHandler(FlightItemDragController hostObj) : base(hostObj) {
    _pickupTargetEventsHandler.ONAfterTransition += (oldState, newState) => {
      DebugEx.Fine("Pickup target state changed: {0} => {1}", oldState, newState);
    };
    _pickupTargetEventsHandler.DefineAction(
        PickupTarget.SinglePart, TakeFocusedPartHint, hostObj.pickupItemFromSceneEvent, HandleScenePartPickupAction);
    _pickupTargetEventsHandler.DefineAction(
        PickupTarget.DeployedGroundPart, PickupGroundPartHint, hostObj.pickupGroundPartFromSceneEvent,
        HandleSceneGroundPartPickupAction, () => FlightGlobals.ActiveVessel.isEVA);
  }

  /// <inheritdoc/>
  public override bool Start() {
    var res = base.Start();
    if (!res) {
      return false;
    }
    _rightClickBlocker = hostObj.gameObject.AddComponent<RightClickBlocker>();
    return true;
  }

  /// <inheritdoc/>
  public override void Stop() {
    base.Stop();
    DestroyCurrentTooltip();
    CrewHatchController.fetch.EnableInterface();
    _pickupTargetEventsHandler.currentState = null;
    targetPickupPart = null;
    Object.DestroyImmediate(_rightClickBlocker);
    _rightClickBlocker = null;
  }

  /// <inheritdoc/>
  protected override IEnumerator StateTrackingCoroutine() {
    while (isStarted) {
      CrewHatchController.fetch.DisableInterface(); // No hatch actions while we're targeting the part!

      var tgtPart = Mouse.HoveredPart;
      targetPickupPart = tgtPart != null && !tgtPart.isVesselEVA ? tgtPart : null;
      if (targetPickupPart == null) {
        _pickupTargetEventsHandler.currentState = null;
      } else if (targetPickupPart.vessel.packed) {
        _pickupTargetEventsHandler.currentState = PickupTarget.PackedVessel;
      } else {
        if (_groundPartModule != null && GroundPartBeingDeployedField.Get(_groundPartModule)) {
          _pickupTargetEventsHandler.currentState = PickupTarget.PendingDeployGroundPart;
        } else if (_groundPartModule != null && GroundPartBeingRetrievedField.Get(_groundPartModule)) {
          _pickupTargetEventsHandler.currentState = PickupTarget.PendingRetrieveGroundPart;
        } else if (_groundPartModule != null && GroundPartDeployedOnGroundField.Get(_groundPartModule)) {
          _pickupTargetEventsHandler.currentState = PickupTarget.DeployedGroundPart;
        } else if (targetPickupPart.children.Count == 0) {
          _pickupTargetEventsHandler.currentState = PickupTarget.SinglePart;
        } else {
          _pickupTargetEventsHandler.currentState = PickupTarget.PartAssembly;
        }
      }
      if (!Input.anyKey || !hostObj.pickupModeSwitchEvent.isEventActive) {
        hostObj.ToIdleState();
        break;
      }
      UpdateTooltip();

      // Don't handle the keys in the same frame as the coroutine has started in to avoid the double actions.
      yield return null;

      _pickupTargetEventsHandler.HandleActions();
    }
    // No logic beyond this point! The coroutine can be explicitly killed.
  }
  #endregion

  #region Local utility methods
  /// <summary>Updates or creates the in-flight tooltip with the part data.</summary>
  /// <remarks>It's intended to be called on every frame update. This method must be efficient.</remarks>
  /// <seealso cref="targetPickupPart"/>
  void UpdateTooltip() {
    if (targetPickupPart == null) {
      DestroyCurrentTooltip();
      return;
    }
    CreateTooltip();
    switch (_pickupTargetEventsHandler.currentState) {
      case PickupTarget.PackedVessel:
        currentTooltip.ClearInfoFields();
        currentTooltip.title = VesselNotReadyMsg;
        break;
      case PickupTarget.SinglePart:
        KisContainerWithSlots.UpdateTooltip(currentTooltip, new[] { _targetPickupItem });
        break;
      case PickupTarget.PartAssembly:
        // TODO(ihsoft): Implement!
        currentTooltip.title = CannotGrabHierarchyTooltipMsg;
        currentTooltip.baseInfo.text =
            CannotGrabHierarchyTooltipDetailsMsg.Format(CountChildrenInHierarchy(targetPickupPart));
        break;
      case PickupTarget.DeployedGroundPart:
        currentTooltip.ClearInfoFields();
        if (FlightGlobals.ActiveVessel.isEVA) {
          KisContainerWithSlots.UpdateTooltip(currentTooltip, new[] { _targetPickupItem });
          currentTooltip.baseInfo.text += "\n" + DeployedGroundPartInfo;
        } else {
          currentTooltip.title = KerbalNeededToPickupPartErr;
          currentTooltip.baseInfo.text = DeployedGroundPartInfo;
        }
        break;
      case PickupTarget.PendingDeployGroundPart:
        currentTooltip.ClearInfoFields();
        currentTooltip.title = DeployingGroundPartMsg;
        break;
      case PickupTarget.PendingRetrieveGroundPart:
        currentTooltip.ClearInfoFields();
        currentTooltip.title = RetrievingGroundPartMsg;
        break;
    }
    currentTooltip.hints = _pickupTargetEventsHandler.GetHints();
    currentTooltip.UpdateLayout();
  }

  /// <summary>Reacts on pickup UI action and starts the drag operation.</summary>
  /// <remarks>Once the dragging operation starts, this handler gets stopped via the host.</remarks>
  void HandleScenePartPickupAction() {
    var leasedItem = _targetPickupItem;  // Make a copy since the field will change.
    KisItemDragController.LeaseItems(
        PartIconUtils.MakeDefaultIcon(leasedItem.materialPart),
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

  /// <summary>Reacts on ground part pickup UI action.</summary>
  /// <remarks>
  /// The pickup process takes time. The part may be in an inconsistent state and must not be affected by the drag
  /// operations.
  /// </remarks>
  void HandleSceneGroundPartPickupAction() {
    _groundPartModule.RetrievePart();
    if (GroundPartBeingRetrievedField.Get(_groundPartModule)) {
      DebugEx.Info("Triggered ground part pickup: {0}", _targetPickupPart);
      _groundPartModule.gameObject.AddComponent<GroundPartRetrieveTracker>();
    } else {
      DebugEx.Warning("Cannot pickup ground part: {0}", _targetPickupPart);
      UISoundPlayer.instance.Play(CommonConfig.sndPathBipWrong);
    }
  }

  /// <summary>Returns the total number of the parts in the hierarchy.</summary>
  static int CountChildrenInHierarchy(Part p) {
    return p.children.Count + p.children.Sum(CountChildrenInHierarchy);
  }
  #endregion

  /// <summary>Component that tracks and reports ground part retrieve status.</summary>
  /// <remarks>It must be added to the part's gameobject.</remarks>
  class GroundPartRetrieveTracker : MonoBehaviour {
    ScreenMessage _message;

    void Awake() {
      var part = gameObject.GetComponent<Part>();
      _message = ScreenMessages.PostScreenMessage(
          RetrievingPartMsg.Format(part.partInfo.title), float.MaxValue, ScreenMessageStyle.UPPER_RIGHT);
    }

    void OnDestroy() {
      ScreenMessages.RemoveMessage(_message);
    }
  }
}
}
