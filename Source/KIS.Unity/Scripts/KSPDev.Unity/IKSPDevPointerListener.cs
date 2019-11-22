// Kerbal Development tools for Unity scripts.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using UnityEngine;
using UnityEngine.EventSystems;

// ReSharper disable once CheckNamespace
namespace KSPDev.Unity {

/// <summary>Interface for the listeners that need "routed" pointer events.</summary>
/// <remarks>
/// The "routed" events are those that happen on the different objects in the hierarchy, but then
/// were delivered to a specific object that handles them all.
/// </remarks>
/// <seealso cref="GenericPointerNotifier&lt;TSource, TTarget&gt;"/>
public interface IKspDevPointerListener<in T> {
  /// <summary>Defines in what order the components will be handling events.</summary>
  /// <remarks>
  /// When order override is not needed, simply return zero, which is a default that means "main
  /// logic, order doesn't matter". Negative values are used to kick in before the main logic, and
  /// positive values turn the callbacks into cleanup methods.
  /// </remarks>
  int priority { get; }

  /// <summary>
  /// Called when any of the three major mouse buttons is clicked over the <c>owner</c>.
  /// </summary>
  /// <remarks>
  /// Only "real" clicks are considered. I.e. the pointer button must be released at the exactly
  /// same position as it was clicked, and there must be no movements in between.
  /// </remarks>
  /// <param name="owner">The object that the event was sent to by Unity.</param>
  /// <param name="subject">
  /// The component that is this event about. It may or may not belong to the owner.
  /// </param>
  /// <param name="eventData">The event data, associated with the click.</param>
  /// <seealso cref="PointerEventData.InputButton"/>
  void OnPointerButtonClick(GameObject owner, T subject, PointerEventData eventData);

  /// <summary>Called when pointer hovers over the <c>owner</c> first time.</summary>
  /// <param name="owner">The object that the event was sent to by Unity.</param>
  /// <param name="subject">
  /// The component that is this event about. It may or may not belong to the owner.
  /// </param>
  /// <param name="eventData">The event data, associated with the move.</param>
  void OnPointerEnter(GameObject owner, T subject, PointerEventData eventData);

  /// <summary>Called when pointer leaves the <c>owner's</c> space.</summary>
  /// <param name="owner">The object that the event was sent to by Unity.</param>
  /// <param name="subject">
  /// The component that is this event about. It may or may not belong to the owner.
  /// </param>
  /// <param name="eventData">The event data, associated with the move.</param>
  void OnPointerExit(GameObject owner, T subject, PointerEventData eventData);
}

}
