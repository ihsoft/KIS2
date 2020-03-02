// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.InputUtils;
using KSPDev.LogUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>
/// Simple state machine for GUI actions that wraps input events and handle them depending on the
/// state.   
/// </summary>
/// <remarks>
/// It may be used when GUI actions depend on a definite set of states. Instead of coding a lot of
/// if/then or switch statements, with this class it's possible to define the behavior once and
/// simply react on the input actions from the callbacks. As convenience, this class also offers a
/// way to quickly build a list of actions available in the current mode to show as a hint in GUI. 
/// </remarks>
/// <typeparam name="T">The enum to use as the states set.</typeparam>
public sealed class EventsHandlerStateMachine<T> where T : struct {
  /// <summary>Current state of the machine.</summary>
  /// <value>The state that was set explicitly or the default value of the enum.</value>
  public T currentState { get; private set; }

  #region Local data structures
  /// <summary>Container for the handler definition.</summary>
  /// <remarks>
  /// Keep it <c>struct</c> for the performance reasons. This element will be accessed from the
  /// <c>Update</c> method.
  /// </remarks>
  struct HandlerDef {
    /// <summary>Action that triggers the handler.</summary>
    /// <seealso cref="HandleActions"/>
    public Event actionEvent;

    /// <summary>Hint string for the action.</summary>
    /// FIXME: use message
    /// <seealso cref="GetHints"/>
    public string actionHint;

    /// <summary>Callback to call when the action is activated.</summary>
    /// <seealso cref="HandleActions"/>
    public Action actionFn;

    /// <summary>
    /// Runtime check function that tells if the action should be available at the particular
    /// moment.
    /// </summary>
    /// <remarks>
    /// This function is only called during the state change call. If availability changes withing
    /// the same state, the actor code must refresh the state of the machine. 
    /// </remarks>
    /// <seealso cref="SetState"/>
    public Func<bool> checkFn;
  }
  #endregion

  #region Local fields and properties
  /// <summary>Cached handlers for the current state.</summary>
  /// <remarks>It's refreshed each time the <see cref="SetState"/> method is called.</remarks>
  HandlerDef[] _currentStateHandlers = new HandlerDef[0];

  /// <summary>Handlers for the states.</summary>
  /// <remarks>It may ont define all the states, defined by <see cref="T"/>.</remarks>
  readonly Dictionary<T, List<HandlerDef>> _stateHandlers = new Dictionary<T, List<HandlerDef>>();
  #endregion

  #region API methods
  /// <summary>Defines an action for a state.</summary>
  /// <param name="state">The state to apply the action to.</param>
  /// <param name="hintText">The event hint message.</param>
  /// <param name="actionEvent">The event to trigger the action on.</param>
  /// <param name="actionFn">The callback to call when the action is triggered.</param>
  /// <param name="checkIfAvailable">
  /// The callback that tells if the action is available in the current game state. This function is
  /// only called during the state change call. If availability changed within the same state, then
  /// a state refresh needs to be triggered via <see cref="SetState"/>.
  /// </param>
  public void DefineAction(
      T state, Message<KeyboardEventType> hintText, Event actionEvent,
      Action actionFn,
      Func<bool> checkIfAvailable = null) {
    List<HandlerDef> stateHandlers;
    if (!_stateHandlers.TryGetValue(state, out stateHandlers)) {
      stateHandlers = new List<HandlerDef>();
      _stateHandlers[state] = stateHandlers;
    }
    stateHandlers.Add(new HandlerDef() {
        actionEvent = actionEvent,
        actionHint = hintText.Format(actionEvent),
        actionFn = actionFn,
        checkFn = checkIfAvailable,
    });
  }

  /// <summary>
  /// Returns user friendly string that describes all the available events in the current state. 
  /// </summary>
  /// <param name="separator">The string to use to join hints for multiple events.</param>
  /// <returns>The event hints, joined with the separator.</returns>
  public string GetHints(string separator = "\n") {
    if (_currentStateHandlers == null) {
      return "";
    }
    return string.Join(
        separator,
        _currentStateHandlers
            .Select(x => x.actionHint)
            .ToList());
  }

  /// <summary>
  /// Checks if any of the events, defined for the current state, is triggered, and calls the
  /// relevant callback. 
  /// </summary>
  /// <remarks>
  /// <para>
  /// The events are checked in the order of definition. The first matched event is triggered and
  /// the rest are skipped.
  /// </para>
  /// <para>
  /// This method is optimized for performance. It's intended to be called from methods like
  /// <c>Update</c> or <c>OnGUI</c>.
  /// </para>
  /// </remarks>
  /// <returns><c>true</c> if an action was triggered and the callback was invoked.</returns>
  public bool HandleActions() {
    // Don't use Linq or foreach! This method is called in every frame, so every bit counts.
    var handlersNum = _currentStateHandlers.Length;
    for (var i = 0; i < handlersNum; i++) {
      var handler = _currentStateHandlers[i];
      if (EventChecker2.CheckClickEvent(handler.actionEvent)) {
        DebugEx.Fine("Triggered action: {0}", KeyboardEventType.Format(handler.actionEvent));
        handler.actionFn();
        return true;
      }
    }
    return false;
  }

  /// <summary>Changes the current state.</summary>
  /// <remarks>
  /// The internal state of the machine is cached for the performance reasons. To refresh it,
  /// simply set the same state again.
  /// </remarks>
  /// <param name="newState"></param>
  public void SetState(T newState) {
    if (!currentState.Equals(newState)) {
      DebugEx.Fine("Switch to state: {0}", newState);
    }
    List<HandlerDef> stateHandlers;
    if (_stateHandlers.TryGetValue(newState, out stateHandlers)) {
      _currentStateHandlers = stateHandlers
          .Where(x => x.checkFn == null || x.checkFn())
          .ToArray();
    } else {
      _currentStateHandlers = new HandlerDef[0];
    }
    currentState = newState;
  }
  #endregion
}

}
