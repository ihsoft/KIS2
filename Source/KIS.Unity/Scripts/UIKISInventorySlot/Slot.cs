// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.Unity;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace KIS2.UIKISInventorySlot {

/// <summary>Controller for a KIS inventory slot.</summary>
public sealed class Slot : UIControlBaseScript {
  #region Unity serialized fields
  [SerializeField]
  RawImage partImage = null;

  [SerializeField]
  Button slotButton = null;

  [SerializeField]
  Text stackSizeIndicatorText = null;

  [SerializeField]
  GameObject resourceIndicatorControl = null;

  [SerializeField]
  Text resourceIndicatorText = null;

  [SerializeField]
  GameObject scienceIndicatorControl = null;

  [SerializeField]
  GridLayoutGroup bottomControlsGrid = null;
  #endregion

  #region API properties and fields
  /// <summary>Zero based index of the slot.</summary>
  public int slotIndex {
    get { return transform.GetSiblingIndex(); }
  }

  /// <summary>Image to show as the slot's background.</summary>
  /// <remarks>If set to <c>null</c>, then the background is transparent.</remarks>
  public Texture slotImage {
    get { return partImage.gameObject.activeSelf ? partImage.texture : null; }
    set {
      partImage.texture = value;
      partImage.gameObject.SetActive(value != null);
    }
  }

  /// <summary>Defines if the slot can participate in the actions.</summary>
  public bool isLocked {
    get { return !slotButton.interactable; }
    set { slotButton.interactable = !value; }
  }

  /// <summary>String that identifies the slot's overall resources state</summary>
  /// <remarks>Only shown if not empty.</remarks>
  public string resourceStatus {
    get { return resourceIndicatorText.text; }
    set {
      resourceIndicatorText.text = value;
      resourceIndicatorControl.gameObject.SetActive(!string.IsNullOrEmpty(value));
    }
  }

  /// <summary>Size of the stack in the slot.</summary>
  /// <remarks>Only shown if not empty.</remarks>
  public string stackSize {
    get { return stackSizeIndicatorText.text; }
    set {
      stackSizeIndicatorText.text = value;
      stackSizeIndicatorText.gameObject.SetActive(!string.IsNullOrEmpty(value));
    }
  }

  /// <summary>Tells if part in the slot has science.</summary>
  public bool hasScience {
    get { return scienceIndicatorControl.gameObject.activeSelf; }
    set { scienceIndicatorControl.gameObject.SetActive(value); }
  }
  #endregion

  #region API methods
  /// <summary>
  /// Clears all UI controls, making the slot to look completely empty and unlocked.
  /// </summary>
  /// <seealso cref="isLocked"/>
  public void ClearContent() {
    partImage.texture = null;
    resourceStatus = null;
    stackSize = null;
    slotImage = null;
    hasScience = false;
    isLocked = false;
    while (bottomControlsGrid.transform.childCount > 0) {
      DestroyImmediate(bottomControlsGrid.transform.GetChild(0).gameObject);
    }
  }

  /// <summary>Sets a prefab state.</summary>
  /// <remarks>
  /// This will be a base state to create a new slot in the game. It only makes sense to call this
  /// method during the prefab initialization phase.
  /// </remarks>
  /// <seealso cref="UIPrefabBaseScript"/>
  public void InitPrefab() {
    //FIXME: Capture the first button as prefab.  
    ClearContent();
  }
  #endregion
}

}  // namespace
