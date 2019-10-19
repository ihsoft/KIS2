// Kerbal Development tools for Unity scripts.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using KSPDev.Unity;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KSPDev.Unity {

/// <summary>Component that routes pointer events to another component.</summary>
/// <remarks>
/// This component can be used to avoid implementing per object listeners when container holds
/// multiple dynamic objects. This component may be added to every active element of the container,
/// and it will deliver events to the container, passing the element as an argument.
/// <para>
/// One example of such setup could be a layout group that contains many dynamic elements (like a
/// Grid). Instead of implementing event listeners in each element, this component could be added to
/// the element's prefab and it will be delivering click/hover events right to the specified target.
/// </para>
/// </remarks>
/// <typeparam name="TSource">
/// Component type of the event <c>source</c>. It can be the actual object that received the pointer
/// event, or it can be any object up or down the hierarchy. E.g. the events can be listened by a
/// button object, but in the callback the button's image may be passed. This object will be passed
/// as "context" in the callback.
/// </typeparam>
/// <typeparam name="TTarget">
/// Component type of the <c>sink</c>. The game object of this component will be receiving the
/// routed events. Only the components that implement
/// <see cref="IKSPDevPointerListener&lt;TSource&gt;"/> interface will receive the events.
/// </typeparam>
/// <seealso cref="eventSource"/>
/// <seealso cref="eventTarget"/>
public class GenericPointerNotifier<TSource, TTarget> : UIControlBaseScript,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    where TSource : Component
    where TTarget : Component {

  #region Unity serialized fields
  /// <summary>Object to send event to.</summary>
  [SerializeField]
  protected TTarget eventTarget;

  /// <summary>Object to provide as a callback argument.</summary>
  [SerializeField]
  protected TSource eventSource;
  #endregion

  #region API properties
  /// <summary>Tells if pointer is hovering over the object.</summary>
  public bool isHovered { get; private set; }

  /// <summary>Returns the data from the last event.</summary>
  /// <remarks>The data is not erased between the frames.</remarks>
  public PointerEventData lastPointerData { get; private set; }
  #endregion

  #region Local fields and properties
  IKSPDevPointerListener<TSource>[] listeners;
  #endregion

  #region MonoBehaviour methods
  /// <inheritdoc/>
  protected virtual void Start() {
    listeners = eventTarget.GetComponents<IKSPDevPointerListener<TSource>>()
        .OrderBy(l => l.priority)
        .ToArray();
    if (listeners.Length == 0) {
      LogWarning("No listeners found");
    }
  }
  #endregion

  #region IPointerClickHandler implementation
  /// <inheritdoc/>
  public virtual void OnPointerClick(PointerEventData eventData) {
    lastPointerData = eventData;
    for (var i = 0; i < listeners.Length; ++i) {
      listeners[i].OnPointerButtonClick(gameObject, eventSource, eventData);
    }
  }
  #endregion

  #region IPointerEnterHandler implementation
  /// <inheritdoc/>
  public virtual void OnPointerEnter(PointerEventData eventData) {
    isHovered = true;
    lastPointerData = eventData;
    for (var i = 0; i < listeners.Length; ++i) {
      listeners[i].OnPointerEnter(gameObject, eventSource, eventData);
    }
  }
  #endregion

  #region IPointerExitHandler implementation
  /// <inheritdoc/>
  public virtual void OnPointerExit(PointerEventData eventData) {
    isHovered = false;
    lastPointerData = eventData;
    for (var i = 0; i < listeners.Length; ++i) {
      listeners[i].OnPointerExit(gameObject, eventSource, eventData);
    }
  }
  #endregion
}

}  // namespace
