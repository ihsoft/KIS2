// Kerbal Inventory System v2
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.GUIUtils;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using System.Linq;
using KISAPIv2;
using KSPDev.ConfigUtils;
using UnityEngine;
using KisPartNodeUtils = KISAPIv2.PartNodeUtils;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>Class that shows a dialog to spawn an arbitrary item in the inventory.</summary>
[PersistentFieldsDatabase("KIS2/settings2/KISConfig")]
class SpawnItemDialogController : MonoBehaviour, IHasGUI {
  #region Configuration
  // ReSharper disable FieldCanBeMadeReadOnly.Local
  // ReSharper disable FieldCanBeMadeReadOnly.Global
  // ReSharper disable ConvertToConstant.Global
  // ReSharper disable MemberCanBePrivate.Global
  // ReSharper disable ConvertToConstant.Local

  /// <summary>
  /// Indicates if the items spawned via the builder GUI should be checked for the inventory eligibility.
  /// </summary>
  /// <remarks>
  /// Be careful when setting it to <c>false</c>. Not all inventories will work fine if there are items added are beyond
  /// the limits.
  /// </remarks>
  [PersistentField("BuilderMode/noChecksForSpawnedItems")]
  bool _noChecksForSpawnedItems = false;
  #endregion

  #region Local fields and properties
  /// <summary>The dialog that is being currently shown.</summary>
  static SpawnItemDialogController _currentDialog;

  /// <summary>Maximum number of matches to present when doing the search.</summary>
  const int MaxFoundItems = 10;

  /// <summary>The control that tracks the GUI scale.</summary>
  GuiScaledSkin _guiScaledSkin;

  /// <summary>The inventory the currently open dialog is bound to.</summary>
  KisContainerWithSlots _tgtInventory;

  /// <summary>The text that is being searched for the parts as of now.</summary>
  string _searchText = "";

  /// <summary>The quantity settings from the dialog.</summary>
  /// <remarks>
  /// This is how many instances of the part will be attempted to be created in the inventory. Not all of them will be
  /// actually added!
  /// </remarks>
  /// <seealso cref="GuiSpawnItems"/>
  string _createQuantity = "1";

  /// <summary>Tracks the current GIO dialog position.</summary>
  Rect _guiMainWindowPos = new(100, 100, 1, 1);

  /// <summary>The parts that are found based on th search pattern.</summary>
  /// <seealso cref="_searchText"/>
  AvailablePart[] _foundMatches = {};

  /// <summary>The actions to be applied before the next frame update.</summary>
  readonly GuiActionsList _guiActionList = new();
  #endregion

  #region GUI styles
  GUIStyle _inputFieldLabel;
  GUIStyle _labelSection;
  #endregion

  #region API methods
  // ReSharper disable MemberCanBePrivate.Global

  /// <summary>Presents a dialog that adds a new item to the inventory.</summary>
  /// <remarks>
  /// There can be only one dialog active. If a dialog for a different inventory is requested, then it substitutes any
  /// dialog that was presented before.
  /// </remarks>
  /// <param name="inventory">The inventory to bound the dialog to.</param>
  public static void ShowDialog(KisContainerWithSlots inventory) {
    if (_currentDialog != null && _currentDialog._tgtInventory == inventory) {
      return; // NOOP
    }
    CloseCurrentDialog();
    var dialog = new GameObject("KisBuilder-ItemSpawnDialog-" + inventory.part.flightID);
    _currentDialog = dialog.AddComponent<SpawnItemDialogController>();
    _currentDialog._tgtInventory = inventory;
    DebugEx.Info("Spawn a new part dialog: dlg={0}, inventory={1}", _currentDialog, inventory);
  }

  /// <summary>Closes the currently open dialog if any.</summary>
  public static void CloseCurrentDialog() {
    if (_currentDialog == null) {
      return;
    }
    DebugEx.Info("Close a new part dialog: dlg={0}, inventory={1}", _currentDialog, _currentDialog._tgtInventory);
    Hierarchy.SafeDestroy(_currentDialog);
    _currentDialog = null;
  }

  // ReSharper enable MemberCanBePrivate.Global
  #endregion

  #region IHasGUI implementation
  /// <inheritdoc/>
  public void OnGUI() {
    // Check if the bound slot is no more visible.
    if (_tgtInventory == null || !_tgtInventory.isGuiOpen) {
      DebugEx.Info("Destroying the dialog due to the target inventory cannot accomodate the input anymore");
      CloseCurrentDialog();
      return;
    }
    using (new GuiSkinScope(_guiScaledSkin.scaledSkin)) {
      _guiMainWindowPos =
          GUILayout.Window(GetInstanceID(), _guiMainWindowPos, GuiMain, "KIS spawn item dialog", GUILayout.Height(0));
    }
  }
  #endregion

  #region MonoBehaviour overrides
  /// <summary>Initializes the dialog.</summary>
  void Awake() {
    DebugEx.Fine("[{0}] Controller started", nameof(SpawnItemDialogController));
    ConfigAccessor.ReadFieldsInType(GetType(), this);
    GuiUpdateSearch(_searchText);
    _guiScaledSkin = new GuiScaledSkin(() => GUI.skin, onSkinUpdatedFn: MakeGuiStyles);
  }
  #endregion

  #region Local utility methods
  /// <summary>Main GUI method.</summary>
  void GuiMain(int windowId) {
    _guiActionList.ExecutePendingGuiActions();

    GUILayout.Label("Inventory: " + DebugEx.ObjectToString(_tgtInventory), _labelSection);
    using (new GUILayout.HorizontalScope(GUI.skin.box)) {
      GUILayout.Label("Search:", _inputFieldLabel);
      GUI.changed = false;
      _searchText = GUILayout.TextField(_searchText, GUILayout.Width(300 * GameSettings.UI_SCALE));
      if (GUI.changed) {
        _guiActionList.Add(() => GuiUpdateSearch(_searchText));
      }
      GUILayout.Label("Quantity:", _inputFieldLabel);
      var oldQuantity = _createQuantity;
      _createQuantity = GUILayout.TextField(_createQuantity, GUILayout.Width(50 * GameSettings.UI_SCALE));
      if (!int.TryParse(_createQuantity, out _)) {
        _createQuantity = oldQuantity;
      }
    }
    
    if (_searchText.Length < 3) {
      GUILayout.Label("...give at least 3 characters...", _labelSection);
    } else if (_foundMatches.Length == 0) {
      GUILayout.Label("...nothing is found for the pattern...", _labelSection);
    } else {
      for (var i = 0; i < MaxFoundItems && i < _foundMatches.Length; i++) {
        var p = _foundMatches[i];
        using (new GUILayout.HorizontalScope(GUI.skin.box)) {
          GUILayout.Label(p.title + " (" + p.name + ")", _inputFieldLabel);
          GUILayout.FlexibleSpace();
          if (GUILayout.Button("Add", GUILayout.ExpandHeight(true))) {
            _guiActionList.Add(() => GuiSpawnItems(p.name));
          }
        }
      }
      if (_foundMatches.Length > MaxFoundItems) {
        var text = $"...there are more {_foundMatches.Length - MaxFoundItems} item(s), but they are not shown...";
        GUILayout.Label(text);
      }
    }
   
    if (GUILayout.Button("Close")) {
      _guiActionList.Add(() => Hierarchy.SafeDestroy(gameObject));
    }
    GUI.DragWindow();
  }

  /// <summary>Refreshes the list of found candidates.</summary>
  void GuiUpdateSearch(string pattern) {
    if (pattern.Length < 3) {
      _foundMatches = new AvailablePart[0];
      return;
    }
    pattern = pattern.ToLower();
    _foundMatches = PartLoader.Instance.loadedParts
        .Where(p => p.partConfig != null)
        .Where(p => p.name.ToLower().Contains(pattern) || p.title.ToLower().Contains(pattern))
        .OrderBy(p => p.name)
        .ToArray();
  }

  /// <summary>Spawns the item in the inventory.</summary>
  void GuiSpawnItems(string partName) {
    if (!int.TryParse(_createQuantity, out var quantity) || quantity < 1) {
      quantity = 1;
      DebugEx.Error("Wrong quantity: selected='{0}', fallback={1}", _createQuantity, quantity);
    }
    DebugEx.Info("Spawning parts: {0}, qty={1}", partName, quantity);
    for (var i = 0; i < quantity; i++) {
      var partSnapshot = KisPartNodeUtils.GetProtoPartSnapshot(PartLoader.getPartInfoByName(partName).partPrefab);
      var reasons = !_noChecksForSpawnedItems ? _tgtInventory.CheckCanAddPart(partSnapshot).ToArray() : new ErrorReason[0];
      if (reasons.Length == 0) {
        _tgtInventory.AddPart(partSnapshot);
      } else {
        UISoundPlayer.instance.Play(KisApi.CommonConfig.sndPathBipWrong);
        ScreenMessaging.ShowPriorityScreenMessage(
            ScreenMessaging.SetColorToRichText(
                DbgFormatter.C2S(reasons, predicate: x => x.guiString, separator: "\n"),
                ScreenMessaging.ErrorColor));
        break;
      }
    }
  }

  /// <summary>Creates the styles when the scale changes or initializes.</summary>
  void MakeGuiStyles(GUISkin skin) {
    _inputFieldLabel = new GUIStyle(skin.label) {
        wordWrap = false,
    };
    _labelSection = new GUIStyle(skin.box) {
        wordWrap = false,
    };
  }
  #endregion
}

}  // namespace
