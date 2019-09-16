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
/// This component can be used to avoid implementing per objet listeners when a container holds
/// multiple dynamic object. Instead of doing this, this component may be added to every active
/// element of the container item, and it will deliver events to the container, passing the item as
/// argument.
/// </remarks>
/// <typeparam name="TSource">
/// Component type of the <c>item</c>. It's a subject or a context of the event.
/// </typeparam>
/// <typeparam name="TTarget">
/// Component type of the <c>sink</c>. The game object of this component will be receiving events.
/// Only the components that implement pointer listener intefarce will receive the events.
/// </typeparam>
/// <seealso cref="IKSPDevPointerListener&lt;in TSource&gt;"/>
public class GenericPointerNotifier<TSource, TTarget> : UIControlBaseScript,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    where TSource : Component
    where TTarget : Component {

  #region Unity serialized fields
  [SerializeField]
  protected TTarget eventTarget;

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
  public virtual void OnPointerClick(PointerEventData eventData) {
    lastPointerData = eventData;
    for (var i = 0; i < listeners.Length; ++i) {
      listeners[i].OnPointerButtonClick(gameObject, eventSource, eventData);
    }
  }
  #endregion

  #region IPointerEnterHandler implementation
  public virtual void OnPointerEnter(PointerEventData eventData) {
    isHovered = true;
    lastPointerData = eventData;
    for (var i = 0; i < listeners.Length; ++i) {
      listeners[i].OnPointerEnter(gameObject, eventSource, eventData);
    }
  }
  #endregion

  #region IPointerExitHandler implementation
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
