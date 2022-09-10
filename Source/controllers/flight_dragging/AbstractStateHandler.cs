// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections;
using KSP.UI;
using KSPDev.Unity;
using UnityEngine;

namespace KIS2.controllers.flight_dragging {

/// <summary>The base module for all flight dragging handlers. It wraps the common logic.</summary>
/// <remarks>
/// <p>
/// The idea of the concept is to start a <i>specific coroutine</i> to handle GUI actions at the given state of the
/// game. This coroutine starts and stop as needed, instead of having a huge common <c>Update</c> methods that handles
/// all the cases. It can save a lot of FPS.
/// </p>
/// <p>
/// It's assumed that there is exactly ONE handler running at the time. It must only be called once per an instance.
/// </p>
/// </remarks>
abstract class AbstractStateHandler {
  #region Inheritable fields
  /// <summary>The flight dragging controller that owns this handler.</summary>
  protected readonly FlightItemDragController hostObj;

  /// <summary>The current state of the handler.</summary>
  public bool isStarted { get; protected set; }

  /// <summary>The tooltip that is currently being presented.</summary>
  protected UIKISInventoryTooltip.Tooltip currentTooltip;
  #endregion

  #region Local fields
  Coroutine _coroutine;
  #endregion

  #region API methods
  /// <summary>Attaches this handler to the host.</summary>
  /// <remarks>It's a good place to do the handler state initialization.</remarks>
  protected AbstractStateHandler(FlightItemDragController hostObj) {
    this.hostObj = hostObj;
  }

  /// <summary>Requests this handler to become active and start handling GUI actions.</summary>
  /// <remarks>The overrides can prevent the action.</remarks>
  public virtual bool Start() {
    Stop();
    isStarted = true;
    _coroutine = hostObj.StartCoroutine(StateTrackingCoroutine());
    return true;
  }

  /// <summary>Requests this handler to stop any activity.</summary>
  public virtual void Stop() {
    if (_coroutine != null) {
      isStarted = false;
      hostObj.StopCoroutine(_coroutine);
      _coroutine = null;
    }
    DestroyCurrentTooltip();
  }
  #endregion

  #region Inheritable methods
  /// <summary>Implements the logic of the handler.</summary>
  /// <remarks>
  /// This coroutine can be just killed, so the code must not rely on the exit
  /// conditions. However, as a best practice, the internal loop may depend on <see cref="isStarted"/>.
  /// </remarks>
  /// <seealso cref="Start"/>
  /// <seealso cref="Stop"/>
  /// <seealso cref="isStarted"/>
  protected abstract IEnumerator StateTrackingCoroutine();

  /// <summary>Creates an empty tooltip that follows the mouse pointer.</summary>
  /// <remarks>Does nothing if the tooltip is already present.</remarks>
  /// <seealso cref="currentTooltip"/>
  protected void CreateTooltip() {
    if (currentTooltip == null) {
      currentTooltip = UnityPrefabController.CreateInstance<UIKISInventoryTooltip.Tooltip>(
          "inFlightControllerTooltip", UIMasterController.Instance.actionCanvas.transform);
      currentTooltip.ClearInfoFields();
    }
  }

  /// <summary>Destroys the current tooltip if one exists.</summary>
  /// <remarks>The tooltip is automatically destroyed on handler stop.</remarks>
  protected void DestroyCurrentTooltip() {
    if (currentTooltip != null) {
      HierarchyUtils.SafeDestroy(currentTooltip);
      currentTooltip = null;
    }
  }
  #endregion
}
}
