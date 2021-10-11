// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Collections;
using KSPDev.LogUtils;
using UniLinq;
using UnityEngine;
using UnityEngine.UI;

// ReSharper disable once CheckNamespace
namespace KSPDev.PrefabUtils {

/// <summary>Controller for arranging the GUI dialogs on the screen so that they are not overlapping.</summary>
/// <remarks>
/// The columns in every grid's row are calculated independently from the other rows. I.e. each row can have different
/// number of columns. The heights of the columns in a single row will be the same. 
/// </remarks>
public static class UIDialogsGridController {
  #region API fields and properties
  // ReSharper disable MemberCanBePrivate.Global
  // ReSharper disable FieldCanBeMadeReadOnly.Global
  // ReSharper disable ConvertToConstant.Global

  /// <summary>A collection of the currently managed dialogs.</summary>
  /// <remarks>It's a copy of the actual collection, so any changes to it won't make sense.</remarks>
  /// <value>The full list of the dialogs under the grid's control.</value>
  public static GameObject[] managedDialogs => OpenDialogs.ToArray();

  /// <summary>Duration for a dialog to move from its original position to the right layout position.</summary>
  /// <see cref="ArrangeDialogs"/>
  public static float dialogMoveAnimationDuration = 0.2f; // Seconds.

  // ReSharper enable FieldCanBeMadeReadOnly.Global
  // ReSharper enable MemberCanBePrivate.Global
  // ReSharper enable ConvertToConstant.Global
  #endregion

  #region Local fields and properties
  /// <summary>The left side inventory window offset in the <i>FLIGHT</i> scene.</summary>
  /// <seealso cref="GetDefaultDlgPos"/>
  const float DlgFlightSceneXOffset = 10.0f;

  /// <summary>The top side inventory window offset in the <i>FLIGHT</i> scene.</summary>
  /// <remarks>Must not be overlapping with the flight controls.</remarks>
  /// <seealso cref="GetDefaultDlgPos"/>
  const float DlgFlightSceneYOffset = 40.0f;

  /// <summary>The left side inventory window offset in the <i>EDIT</i> scene.</summary>
  /// <remarks>Must not be overlapping the parts selection side panel.</remarks>
  /// <seealso cref="GetDefaultDlgPos"/>
  const float DlgEditorSceneXOffset = 280.0f;

  /// <summary>The top side inventory window offset in the <i>EDIT</i> scene.</summary>
  /// <remarks>Must not be overlapping the editor controls at the top.</remarks>
  /// <seealso cref="GetDefaultDlgPos"/>
  const float DlgEditorSceneYOffset = 40.0f;

  /// <summary>Distance between auto-arranged windows.</summary>
  /// <seealso cref="ArrangeDialogs"/>
  const float WindowsGapSize = 5;

  /// <summary>All opened dialogs.</summary>
  static readonly List<GameObject> OpenDialogs = new();

  /// <summary>Dialogs that are in a process of aligning.</summary>
  static readonly Dictionary<int, Coroutine> MovingDialogs = new();
  #endregion

  #region API methods
  /// <summary>Add a dialog into the grid.</summary>
  /// <remarks>
  /// It will trigger dialogs re-arranging. The dialog being added will be positioned at the right location right-away,
  /// no animation will be applied.
  /// </remarks>
  /// <param name="dialogObject">
  /// The dialog component to add. It can be any <see cref="MonoBehaviour"/> component, but its <c>transform</c> must
  /// be <c>RectTransform</c>, or else the dialog will be refused.
  /// </param>
  /// <seealso cref="ArrangeDialogs"/>
  /// <seealso cref="managedDialogs"/>
  public static void AddDialog(GameObject dialogObject) {
    if (dialogObject.transform as RectTransform == null) {
      DebugEx.Error("Dialog doesn't have RectTransform: {0}", dialogObject);
      return;
    }
    OpenDialogs.Add(dialogObject);
    DebugEx.Fine("Added dialog to grid: {0}, id={1}", dialogObject, dialogObject.GetInstanceID());
    ArrangeDialogs_Impl(noAnimationDialog: dialogObject);
  }

  /// <summary>Removes dialog from the grid.</summary>
  /// <param name="dialogObject">The dialog to remove.</param>
  /// <returns>
  /// <c>true</c> if the dialog was removed. <c>false</c> if the requested dialog was not under the grid's control.
  /// </returns>
  /// <seealso cref="ArrangeDialogs"/>
  /// <seealso cref="managedDialogs"/>
  public static bool RemoveDialog(GameObject dialogObject) {
    if (!OpenDialogs.Remove(dialogObject)) {
      return false; // The dialog is not in the grid.
    }
    MovingDialogs.Remove(dialogObject.GetInstanceID());
    DebugEx.Fine("Removed dialog from grid: {0}, id={1}", dialogObject, dialogObject.GetInstanceID());
    ArrangeDialogs();
    return true;
  }

  /// <summary>Changes the position of the dialog in the grid's queue.</summary>
  /// <remarks>
  /// The position defines where the dialog appears on the screen. The dialogs placed at the beginning of the queue are
  /// shown at the top-left corner of the screen. 
  /// </remarks>
  /// <param name="from">The index to move the dialog from.</param>
  /// <param name="to">
  /// The optional index to move the dialog to. If not set, then the dialog is moved to the end of the queue.
  /// </param>
  /// <returns>
  /// <c>true</c> if the operation was successful. If the source or target indexes were bad, then the result will be
  /// <c>false</c>.
  /// </returns>
  /// <seealso cref="ArrangeDialogs"/>
  /// <seealso cref="managedDialogs"/>
  public static bool ChangeDialogPosition(int from, int? to = null) {
    if (from >= OpenDialogs.Count) {
      DebugEx.Error("No dialog at the position: from={0}, count={1}", from, OpenDialogs.Count);
      return false;
    }
    var toIndex = to ?? OpenDialogs.Count - 1;
    if (toIndex >= OpenDialogs.Count) {
      DebugEx.Warning("The destination index is out of bounds: to={0}, count={1}", toIndex, OpenDialogs.Count);
    }
    var tmp = OpenDialogs[toIndex];
    OpenDialogs[toIndex] = OpenDialogs[from];
    OpenDialogs[from] = tmp;
    return true;
  }

  /// <summary>Arranges the dialogs so that they are not overlapping each other.</summary>
  /// <remarks>
  /// <p>The misplaced dialogs will be moved to the right position using animation.</p>
  /// <p>
  /// The dialogs are arranged in the grid by their position. I.e. the first added dialog comes first (top-left), and
  /// the least added dialog comes last (bottom-right). To change the dialog position in the layout, use
  /// the <see cref="ChangeDialogPosition"/> method.
  /// </p>
  /// </remarks>
  /// <seealso cref="managedDialogs"/>
  /// <seealso cref="dialogMoveAnimationDuration"/>
  /// <seealso cref="ChangeDialogPosition"/>
  public static void ArrangeDialogs() {
    ArrangeDialogs_Impl();
  }
  #endregion

  #region Local untility methods
  /// <summary>Gives a staring position for the inventory dialogs, depending on the current scene.</summary>
  /// <remarks>The position should be chose so that the vital scene controls are not hidden.</remarks>
  static Vector3 GetDefaultDlgPos() {
    if (HighLogic.LoadedSceneIsFlight) {
      return new Vector3(
          -Screen.width / 2.0f + DlgFlightSceneXOffset * GameSettings.UI_SCALE,
          Screen.height / 2.0f - DlgFlightSceneYOffset * GameSettings.UI_SCALE,
          0);
    }
    if (HighLogic.LoadedSceneIsEditor) {
      return new Vector3(
          -Screen.width / 2.0f + DlgEditorSceneXOffset * GameSettings.UI_SCALE,
          Screen.height / 2.0f - DlgEditorSceneYOffset * GameSettings.UI_SCALE,
          0);
    }
    return Vector3.zero; // Fallback.
  }

  /// <summary>Animates setting window's position.</summary>
  // ReSharper disable once MemberCanBeMadeStatic.Local
  static IEnumerator AnimateMoveWindow(Transform tgtDlgMainRect, Vector3 tgtPos) {
    var srcPos = tgtDlgMainRect.position;
    var animationTime = 0.0f;
    while (Vector3.SqrMagnitude(tgtDlgMainRect.position - tgtPos) > float.Epsilon) {
      yield return null;
      if (tgtDlgMainRect == null || !tgtDlgMainRect.gameObject.activeInHierarchy) {
        break;
      }
      animationTime += Time.deltaTime;
      tgtDlgMainRect.position = Vector3.Lerp(srcPos, tgtPos, animationTime / dialogMoveAnimationDuration);
    }
  }

  /// <summary>Arranges the dialogs.</summary>
  /// <param name="noAnimationDialog">
  /// If set, then this dialog will get positioned <i>INSTANTLY</i>, without using the animated logic.
  /// </param>
  static void ArrangeDialogs_Impl(Object noAnimationDialog = null) {
    // The mono objects can die at any moment. Cleanup such entries.
    for (var i = OpenDialogs.Count - 1; i >= 0; i--) {
      var dlg = OpenDialogs[i];
      if (dlg == null || !dlg.gameObject.activeInHierarchy) {
        DebugEx.Fine("Cleanup NULL or inactive dialog instance at {0}", i);
        OpenDialogs.RemoveAt(i);
      }
    }
    var deadEntries = MovingDialogs.Where(x => x.Value == null).Select(x => x.Key);
    foreach (var deadEntry in deadEntries) {
      DebugEx.Fine("Cleanup NULL coroutine for dialog: id={0}", deadEntry);
      MovingDialogs.Remove(deadEntry);
    }

    // Recalculate positions.
    var dlgPos = GetDefaultDlgPos();
    foreach (var dialog in OpenDialogs) {
      var dlgMainRect = dialog.transform as RectTransform;
      LayoutRebuilder.ForceRebuildLayoutImmediate(dlgMainRect);
      if (Vector3.SqrMagnitude(dlgMainRect.position - dlgPos) > float.Epsilon) {
        var dlgId = dialog.GetInstanceID();
        if (MovingDialogs.ContainsKey(dlgId)) {
          // It's not known who was actually running the coroutine, so try deleting it from all the components.
          dialog.GetComponents<MonoBehaviour>().ToList().ForEach(x => x.StopCoroutine(MovingDialogs[dlgId]));
          MovingDialogs.Remove(dlgId);
        }
        if (dialog != noAnimationDialog) {
          // We don't care which component will be running the coroutine. As long as the object is active, we're fine.
          var coroutine = dialog.GetComponent<MonoBehaviour>().StartCoroutine(AnimateMoveWindow(dlgMainRect, dlgPos));
          MovingDialogs.Add(dlgId, coroutine);
        } else {
          dlgMainRect.position = dlgPos;
        }
      }
      // TODO(ihsoft): Check for overflow and create rows.
      dlgPos.x += dlgMainRect.sizeDelta.x * dlgMainRect.lossyScale.x + WindowsGapSize * GameSettings.UI_SCALE;
    }
  }
  #endregion
}

}  // namespace
