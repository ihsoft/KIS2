// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.LogUtils;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using KSP.UI;
using System.Reflection;
using System;
using KSPDev.ModelUtils;

namespace KIS2 {

[KSPAddon(KSPAddon.Startup.FlightAndEditor, false /*once*/)]
public sealed class TestController : MonoBehaviour {

  public string openGUIKey = "Tab";
  static Event openGUIEvent;

  void Awake() {
    if (!string.IsNullOrEmpty(openGUIKey)) {
      openGUIEvent = Event.KeyboardEvent(openGUIKey);
    }
  }

  UIKISInventoryWindowController inventoryWindow;

  void DumpDlg(Transform dlg) {
    foreach (var path in Hierarchy.ListHirerahcy(dlg)) {
      var obj = Hierarchy.FindTransformByPath(dlg.parent, path);
      DebugEx.Warning("*** for path: {0}", path);
      if (obj != null) {
        foreach (var c in obj.GetComponents<UnityEngine.Object>()) {
          DebugEx.Warning("type={0}", c.GetType());
        }
      }
    }
  }
  
  void OnGUI() {
    if (openGUIEvent != null && Event.current.Equals(openGUIEvent)) {
      Event.current.Use();
      if (inventoryWindow != null) {
        inventoryWindow.CloseWindow();
      } else {
        inventoryWindow = UIKISInventoryWindowController.CreateDialog(
            "KISInventoryDialog", "Inventory: FOO");
      }
    }
    
    if (Event.current.Equals(Event.KeyboardEvent("L")) && inventoryWindow != null) {
      inventoryWindow.dlgTitle += " L"; 
    }
    if (Event.current.Equals(Event.KeyboardEvent("1")) && inventoryWindow != null) {
      inventoryWindow.SetGridSize(3, 1);
    }
    if (Event.current.Equals(Event.KeyboardEvent("2")) && inventoryWindow != null) {
      inventoryWindow.SetGridSize(3, 2);
    }
    if (Event.current.Equals(Event.KeyboardEvent("3")) && inventoryWindow != null) {
      inventoryWindow.SetGridSize(3, 3);
    }
    if (Event.current.Equals(Event.KeyboardEvent("4")) && inventoryWindow != null) {
      inventoryWindow.SetGridSize(4, 1);
    }
    if (Event.current.Equals(Event.KeyboardEvent("5")) && inventoryWindow != null) {
      inventoryWindow.SetGridSize(4, 2);
    }
    if (Event.current.Equals(Event.KeyboardEvent("6")) && inventoryWindow != null) {
      inventoryWindow.SetGridSize(4, 3);
    }
  }
}

}