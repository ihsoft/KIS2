// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.InputUtils;
using KSPDev.LogUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using KSPDev.ProcessingUtils;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>
/// Simple state machine for GUI actions that wraps input events and handle them depending on the state.   
/// </summary>
/// <remarks>
/// It may be used when GUI actions depend on a definite set of states. Instead of coding a lot of if/then or switch
/// statements, with this class it's possible to define the behavior once and simply react on the input actions from the
/// callbacks. As a convenience, this class also offers a way to quickly build a list of actions available in the
/// current mode to show them as a hint in GUI. 
/// </remarks>
/// <typeparam name="T">The enum to use as the states set.</typeparam>
public sealed class EventsHandlerStateMachine<T> where T : struct {
  #region API properties

  /// <summary>Current state of the machine.</summary>
  /// <remarks>
  /// Defines the actions that are currently active. The <c>null</c> state is a special case: it can be set, but it
  /// cannot have any actions associated with it. So, setting the state to <c>null</c> guarantees there will be no
  /// actions or hints processed while the machine is in the "null state". 
  /// </remarks>
  /// <value>The state or <c>null</c>.</value>
  /// <seealso cref="HandleActions"/>
  /// <seealso cref="GetHints"/>
  public T? currentState {
    get => _currentState;
    set {
      if (!value.HasValue || !_stateHandlers.TryGetValue(value.Value, out _currentStateHandlers)) {
        _currentStateHandlers = new List<HandlerDef>();
      }
      var oldState = _currentState;
      _currentState = value;
      if (!oldState.Equals(_currentState)) {
        SafeCallbacks.Action(() => ONAfterTransition?.Invoke(oldState, _currentState));
      }
    }
  }
  T? _currentState;

  /// <summary>Delegate to track an arbitrary state transition.</summary>
  /// <param name="fromState">The state before the change.</param>
  /// <param name="toState">The state after the change.</param>
  /// <seealso cref="currentState"/>
  /// <seealso cref="EventsHandlerStateMachine{T}.ONAfterTransition"/>
  public delegate void OnStateChangeHandler(T? fromState, T? toState);

  /// <summary>Event that fires when the state machine has changed its state.</summary>
  /// <remarks>
  /// The event is fired <i>after</i> the new state has been applied to the state machine and all the transition
  /// callbacks are handled. It's only fired when the state has actually changed. 
  /// </remarks>
  /// <seealso cref="OnStateChangeHandler"/>
  public event OnStateChangeHandler ONAfterTransition;
  #endregion

  #region Local data structures
  /// <summary>Container for the handler definition.</summary>
  /// <remarks>
  /// Keep it <c>struct</c> for the performance reasons. This element will be accessed from the <c>Update</c> method.
  /// </remarks>
  struct HandlerDef {
    /// <summary>Action that triggers the handler.</summary>
    /// <seealso cref="HandleActions"/>
    public ClickEvent actionEvent;

    /// <summary>Hint string for the action.</summary>
    /// FIXME: use message
    /// <seealso cref="GetHints"/>
    public string actionHint;

    /// <summary>Callback to call when the action is activated.</summary>
    /// <seealso cref="HandleActions"/>
    public Action actionFn;

    /// <summary>Runtime check function that tells if the action should be available at the particular moment.</summary>
    /// <remarks>
    /// This function is only called during the state change call. If availability changes withing the same state, the
    /// actor code must refresh the state of the machine. 
    /// </remarks>
    /// <seealso cref="currentState"/>
    public Func<bool> checkFn;
  }
  #endregion

  #region Local fields and properties
  /// <summary>Cached handlers for the current state.</summary>
  /// <remarks>It's refreshed each time the <see cref="currentState"/> is called.</remarks>
  List<HandlerDef> _currentStateHandlers = new();

  /// <summary>Handlers for the states.</summary>
  /// <remarks>It may not define all the states declared by <see cref="T"/>.</remarks>
  readonly Dictionary<T, List<HandlerDef>> _stateHandlers = new();
  #endregion

  #region API methods
  /// <summary>Defines an action for the state.</summary>
  /// <param name="state">The state to apply the action to.</param>
  /// <param name="hintText">
  /// The event hint message. It must refer the <paramref name="actionEvent"/>. If it's <c>null</c> or empty, then it
  /// won't show up in the <see cref="GetHints"/> result.
  /// </param>
  /// <param name="actionEvent">The <see cref="Event"/> that triggers the action.</param>
  /// <param name="actionFn">The callback to call when the action is triggered.</param>
  /// <param name="checkIfAvailable">
  /// The callback that tells if the action is available in the current game state. This function called in every
  /// <see cref="HandleActions"/> call, so it has to be performance optimized. If not set, then the action is assumed to
  /// be always active.
  /// </param>
  public void DefineAction(
      T state, Message<KeyboardEventType> hintText, ClickEvent actionEvent, Action actionFn,
      Func<bool> checkIfAvailable = null) {
    if (!_stateHandlers.TryGetValue(state, out var stateHandlers)) {
      stateHandlers = new List<HandlerDef>();
      _stateHandlers[state] = stateHandlers;
    }
    stateHandlers.Add(new HandlerDef() {
        actionEvent = actionEvent,
        actionHint = hintText.Format(actionEvent.unityEvent),
        actionFn = actionFn,
        checkFn = checkIfAvailable,
    });
  }

  /// <summary>
  /// Returns a list of user friendly strings that describe all the available events in the current state.
  /// </summary>
  /// <remarks>If the action doesn't have a hint string, then it will be ignored.</remarks>
  /// <returns>The event hints.</returns>
  /// <seealso cref="HandlerDef.checkFn"/>
  /// <seealso cref="HandlerDef.actionHint"/>
  public List<string> GetHintsList() {
    return _currentStateHandlers
        .Where(handler => handler.checkFn == null || handler.checkFn())
        .Select(x => x.actionHint)
        .Where(x => !string.IsNullOrEmpty(x))
        .ToList();
  }

  /// <summary>Returns a user friendly string that describes all the available events in the current state.</summary>
  /// <remarks>It's a syntax sugar to <see cref="GetHintsList"/>.</remarks>
  /// <param name="separator">The string to use to join hints for multiple events.</param>
  /// <returns>The event hints, joined with the separator.</returns>
  /// <seealso cref="HandlerDef.checkFn"/>
  /// <seealso cref="HandlerDef.actionHint"/>
  /// <seealso cref="GetHintsList"/>
  /// FIXME: do we need it?
  public string GetHints(string separator = "\n") {
    return string.Join(separator, GetHintsList());
  }

  /// <summary>
  /// Checks if any of the events, defined for the current state, is triggered, and calls the relevant callback. 
  /// </summary>
  /// <remarks>
  /// <p>
  /// The events are checked in the order of definition. The first matched event is triggered and the rest are skipped.
  /// </p>
  /// <p>This method is optimized for performance. It's intended to be called from the high FPS methods.</p>
  /// </remarks>
  /// <returns><c>true</c> if an action was triggered and the callback was invoked.</returns>
  public bool HandleActions() {
    // Don't use Linq or foreach! This method is called in every frame, so every bit counts.
    var handlersNum = _currentStateHandlers.Count;
    for (var i = 0; i < handlersNum; i++) {
      var handler = _currentStateHandlers[i];
      if ((handler.checkFn != null && !handler.checkFn()) || !handler.actionEvent.CheckClick()) {
        continue;
      }
      DebugEx.Fine("Trigger action: event={0}, index={1}", KeyboardEventType.Format(handler.actionEvent.unityEvent), i);
      handler.actionFn();
      return true;
    }
    return false;
  }
  #endregion
}

}
