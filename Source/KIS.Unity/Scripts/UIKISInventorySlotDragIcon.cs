// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.Unity;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace KIS2 {

/// <summary>UI class to represent an inventory slot while it's being dragged.</summary>
/// <remarks>All elements in the prefab will become non-raycast targets on init.</remarks>
public sealed class UIKISInventorySlotDragIcon : UIPrefabBaseScript {

  #region Unity serialized fields
  [SerializeField]
  RawImage partImage = null;

  [SerializeField]
  Image noGoOverlay = null;

  [SerializeField]
  GameObject resourceIndicatorControl = null;

  [SerializeField]
  Text resourceIndicatorText = null;

  [SerializeField]
  GameObject scienceIndicatorControl = null;

  [SerializeField]
  Text stackSizeIndicatorText = null;
  #endregion

  #region API fields an properties
  /// <summary>Image to show as the slot's background.</summary>
  /// <remarks>If set to <c>null</c>, then the background is transparent.</remarks>
  public Texture slotImage {
    get { return partImage.gameObject.activeSelf ? partImage.texture : null; }
    set {
      partImage.texture = value;
      partImage.gameObject.SetActive(value != null);
    }
  }

  /// <summary>String that identifies the slot's overall resources state.</summary>
  /// <remarks>Only shown if not empty.</remarks>
  public string resourceStatus {
    get { return resourceIndicatorText.text; }
    set {
      resourceIndicatorText.text = value;
      resourceIndicatorControl.gameObject.SetActive(!string.IsNullOrEmpty(value));
    }
  }

  /// <summary>Size of the stack in the slot.</summary>
  /// <remarks>Only shown if greater than <c>1</c>.</remarks>
  public int stackSize {
    get { return _stackSize; }
    set {
      _stackSize = value;
      if (value > 1) {
        stackSizeIndicatorText.text = "x" + value;
      }
      stackSizeIndicatorText.gameObject.SetActive(value > 1);
    }
  }
  int _stackSize;

  /// <summary>Tells if part in the slot has science.</summary>
  public bool hasScience {
    get { return scienceIndicatorControl.gameObject.activeSelf; }
    set { scienceIndicatorControl.gameObject.SetActive(value); }
  }

  /// <summary>Tells "action not allowed" icon is to be shown.</summary>
  public bool showNoGo {
    get { return noGoOverlay.gameObject.activeSelf; }
    set { noGoOverlay.gameObject.SetActive(value); }
  }
  #endregion

  #region UIPrefabBaseScript overrides
  /// <inheritdoc/>
  public override bool InitPrefab() {
    if (!base.InitPrefab()) {
      return false;
    }
    gameObject.GetComponentsInChildren<Graphic>().ToList()
        .ForEach(x => x.raycastTarget = false);
    showNoGo = false;
    stackSize = -1;
    resourceStatus = null;
    slotImage = null;
    return true;
  }
  #endregion
}

}  // namespace
