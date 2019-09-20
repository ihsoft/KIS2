// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KIS2.UIKISInventorySlot;
using KSP.UI;
using System;
using System.Linq;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using KSPDev.Prefabs;
using KSPDev.Unity;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KIS2 {
  
public sealed class UIKISInventoryWindowController : UIScalableWindowController {

  public string dlgTitle {
    get { return unityWindow.title; }
    set { unityWindow.title = value; }
  }

  public int minGridWidth = 3;
  public int minGridHeight = 1;
  public int maxGridWidth = 6;
  public int maxGridHeight = 7;

  UIKISInventoryWindow unityWindow;

  #region UIScalableWindowController overrides
  public override void Awake() {
    base.Awake();
    unityWindow = GetComponent<UIKISInventoryWindow>();
  }

  /// <inheritdoc/>
  public override void Start() {
    //unityWindow = GetComponent<UIKISInventoryWindow>();
    unityWindow.onSlotHover.Add(OnSlotHover);
    unityWindow.onSlotClick.Add(OnSlotClick);
    unityWindow.onSlotAction.Add(OnSlotAction);
    unityWindow.onGridSizeChange.Add(OnSizeChanged);
  }
  #endregion

  #region Local utlity methods
  void OnSlotHover(Slot slot, bool isHover) {
    LogInfo("Pointer hover: slot={0}, isHover={1}", slot.slotIndex, isHover);
    if (isHover) {
      var tooltip = unityWindow.StartSlotTooltip();
      tooltip.title = "lalala";
      tooltip.UpdateLayout();
    }
  }

  void OnSlotClick(Slot slot, PointerEventData.InputButton button) {
    LogInfo("Clicked: slot={0}, button={1}", slot.slotIndex, button);
  }

  void OnSlotAction(Slot slot, int actionButtonNum,
      PointerEventData.InputButton button) {
    LogInfo("Clicked: slot={0}, action={1}, button={2}", slot.slotIndex, actionButtonNum, button);
  }

  Vector2 OnSizeChanged(Vector2 newSize) {
    return new Vector2(
        newSize.x <= minGridWidth ? newSize.x : minGridWidth,
        newSize.y <= minGridHeight ? newSize.y : minGridHeight);
  }
  #endregion

  #region Factory methods
  //FIXME: make it protected and expose from the descendatnts
  public static UIKISInventoryWindowController CreateDialog(string name, string title) {
    DebugEx.Fine("Create KIS inventory window: {0}", title);
    var dlg = UnityPrefabController.CreateInstance<UIKISInventoryWindowController>(
        name, UIMasterController.Instance.actionCanvas.transform);
    dlg.dlgTitle = title;
    return dlg;
  }
  #endregion

  #region API methods
  public void CloseWindow() {
    DebugEx.Fine("Destroy KIS inventory window: {0}", dlgTitle);
    Hierarchy.SafeDestory(gameObject);
  }

  public bool SetGridSize(int width, int height) {
    //FIXME
    return false;
  }

  //FIXME: make it private
//  public bool AddEmptySlot() {
//    var window = GetComponent<UIKISInventoryWindow>();
//    var slot = window.AddSlot();
//    slots.Add(slot);
//    
//    const string samplePartName = "KIS.Container1";
//    
//    var names = new string[] {
//      "KIS.bomb1",
//      "kis.concreteBase1",
//      "KIS.Container1",
//      "KIS.Container2",
//      "KIS.Container3",
//      "KIS.Container4",
//      "KIS.Container5",
//      "KIS.Container6",
//      "KIS.Container7",
//      "KIS.Container8",
//      "KIS.ContainerMount1",
//      "KIS.electricScrewdriver",
//      "KIS.evapropellant",
//      "KIS.guide",
//      "KIS.wrench",
//    };
//    var avPartIndex = (int) (UnityEngine.Random.value * names.Length);
//    var avPart = PartLoader.getPartInfoByName(names[avPartIndex]);
//    if (avPart == null) {
//      DebugEx.Error("*** bummer: no part {0}", samplePartName);
//    }
//    var icon = KISAPI.PartIconUtils.MakeDefaultIcon(avPart, 256, null);
//    slot.SetContent(icon, OnSlotClicked, OnSlotActionClicked);
//    
//    if (UnityEngine.Random.value < 0.20f) {
//      slot.hasScience = true;
//    }
//    if (UnityEngine.Random.value < 0.3f) {
//      SetResourceAmount(UnityEngine.Random.value, slot);
//    }
//    
//    //AsyncCall.CallOnEndOfFrame(this, ControlMoved);
//    return true;
//  }

  void SetResourceAmount(double percent, Slot slot) {
    string text;
    if (percent < 0.05) {
      text = "<5%";
    } else if (percent < 0.15) {
      text = "~10%";
    } else if (percent < 0.25) {
      text = "~20%";
    } else if (percent < 0.35) {
      text = "~30%";
    } else if (percent < 0.45) {
      text = "~40%";
    } else if (percent < 0.55) {
      text = "~50%";
    } else if (percent < 0.65) {
      text = "~60%";
    } else if (percent < 0.75) {
      text = "~70%";
    } else if (percent < 0.85) {
      text = "~80%";
    } else if (percent < 0.95) {
      text = "~90%";
    } else if (percent - 1 > double.Epsilon) {
      text = ">95%";
    } else {
      text = null;
    }
    slot.resourceStatus = text;
  }
  #endregion

  #region Internal methods
  /// <summary>
  /// Called from <see cref="KISModLoadController"/> when it's time to initialize.
  /// </summary>
  internal static void OnGameLoad() {
    PrefabLoader.LoadAllAssets(KSPUtil.ApplicationRootPath + "GameData/KIS/Prefabs/ui_prefabs");
    var dialog = UnityPrefabController.GetPrefab<UIKISInventoryWindow>();
    var baseType = dialog.gameObject.AddComponent<UIKISInventoryWindowController>();
    UnityPrefabController.RegisterPrefab(baseType, baseType.name);
  }
  #endregion
  
  #region Debug methods - DROP
  public static void DumpElement(Transform el) {
    DebugEx.Warning("*** For element: {0}", DbgFormatter.TranformPath(el));
    foreach (var component in el.gameObject.GetComponents<Component>()) {
      DebugEx.Warning("Component: {0}", component.GetType());
    }
    for (var i = 0; i < el.childCount; ++i) {
      DumpElement(el.GetChild(i));
    }
  }
  #endregion
}

}  // namespace
