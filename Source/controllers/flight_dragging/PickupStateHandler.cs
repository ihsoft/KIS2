// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections;
using System.Linq;
using KISAPIv2;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.LogUtils;
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
      description: "The tooltip status to present when the KIS grabbing mode is activated, but no part is being"
      + " focused.");

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

  /// <summary>Defines the currently focused pickup target.</summary>
  /// <remarks>The <c>null</c> state is used to indicate that nothing of the interest is being focused.</remarks>
  enum PickupTarget {
    /// <summary>A lone part or the last child of a vessel is being hovered.</summary>
    SinglePart,
    /// <summary>The part focused has some children.</summary>
    PartAssembly,
    /// <summary>The part focused belongs to a vessel that hasn't yet completed unpacking.</summary>
    PackedVessel,
  }

  /// <summary>The events state machine to control the pickup stage.</summary>
  readonly EventsHandlerStateMachine<PickupTarget> _pickupTargetEventsHandler = new();
  #endregion

  #region AbstractStateHandler implementation
  /// <inheritdoc/>
  public PickupStateHandler(FlightItemDragController hostObj) : base(hostObj) {
    _pickupTargetEventsHandler.ONAfterTransition += (oldState, newState) => {
      DebugEx.Fine("Pickup target state changed: {0} => {1}", oldState, newState);
    };
    _pickupTargetEventsHandler.DefineAction(
        PickupTarget.SinglePart, TakeFocusedPartHint, hostObj.pickupItemFromSceneEvent, HandleScenePartPickupAction);
  }

  /// <inheritdoc/>
  public override void Stop() {
    base.Stop();
    DestroyCurrentTooltip();
    CrewHatchController.fetch.EnableInterface();
    _pickupTargetEventsHandler.currentState = null;
    targetPickupPart = null;
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
      } else if (targetPickupPart.children.Count == 0) {
        _pickupTargetEventsHandler.currentState = PickupTarget.SinglePart;
      } else {
        _pickupTargetEventsHandler.currentState = PickupTarget.PartAssembly;
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
    }
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

  /// <summary>Returns the total number of the parts in the hierarchy.</summary>
  static int CountChildrenInHierarchy(Part p) {
    return p.children.Count + p.children.Sum(CountChildrenInHierarchy);
  }
  #endregion
}
}
