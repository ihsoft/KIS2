﻿// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using KISAPIv2;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>Controller that deals with dragged items in the editor scenes.</summary>
[KSPAddon(KSPAddon.Startup.EditorAny, false /*once*/)]
internal sealed class EditorItemDragController : MonoBehaviour, IKisDragTarget {

  #region Local fields and properties
  /// <summary>
  /// A part that was dragged in the editor before entering a KIS inventory dialog.
  /// </summary>
  Part _savedEditorPart;
  #endregion

  #region MonoBehaviour overrides
  void Awake() {
    DebugEx.Fine("[{0}] Started", nameof(EditorItemDragController));
    KisApi.ItemDragController.RegisterTarget(this);
  }

  void OnDestroy() {
    DebugEx.Fine("[{0}] Stopped", nameof(EditorItemDragController));
    KisApi.ItemDragController.UnregisterTarget(this);
  }
  #endregion

  #region IKisDragTarget implementation
  /// <inheritdoc/>
  void IKisDragTarget.OnKisDragStart() {
    // Not interested.
  }

  /// <inheritdoc/>
  void IKisDragTarget.OnKisDragEnd(bool isCancelled) {
    // Not interested.
  }

  /// <inheritdoc/>
  bool IKisDragTarget.OnKisDrag(bool pointerMoved) {
    return false; // This target doesn't deal with dropped items.
  }

  /// <inheritdoc/>
  void IKisDragTarget.OnFocusTarget(GameObject newTarget) {
    // DRAGGING EDITOR PART - hover over KIS window. 
    // Take the editors dragged part and start KIS dragging for it. Hide the part in the editor.
    if (newTarget != null && EditorLogic.SelectedPart != null) {
      _savedEditorPart = EditorLogic.SelectedPart;
      KisApi.ItemDragController.LeaseItems(
          KisApi.PartIconUtils.MakeDefaultIcon(_savedEditorPart),
          new InventoryItem[] { new InventoryItemImpl(null, _savedEditorPart) },
          EditorItemsConsumed, EditorItemsCancelled);
      UIPartActionController.Instance.partInventory.editorPartDroppedBlockSfx = true;
      EditorLogic.fetch.ReleasePartToIcon();
      _savedEditorPart.gameObject.SetLayerRecursive(LayerMask.NameToLayer("UIAdditional"));
      _savedEditorPart.highlighter.ReinitMaterials();
      _savedEditorPart.SetHighlightType(Part.HighlightType.OnMouseOver);
      _savedEditorPart.SetHighlight(active: false, recursive: true);
      return;
    }

    // DRAGGING EDITOR PART - focus blur from a KIS window.
    // Cancel KIS dragging and return the dragged part back to the editor. 
    if (newTarget == null && KisApi.ItemDragController.isDragging && _savedEditorPart != null) {
      KisApi.ItemDragController.CancelItemsLease();
      return;
    }

    // DRAGGING KIS INVENTORY PART - focus blur from a KIS window.
    // If there is just one part, consume it and start the editor's drag. Otherwise, do nothing and
    // let KIS inventories to handle the logic.
    if (newTarget == null && KisApi.ItemDragController.isDragging && _savedEditorPart == null) {
      if (KisApi.ItemDragController.leasedItems.Length == 1) {
        var items = KisApi.ItemDragController.ConsumeItems();
        if (items == null) {
          DebugEx.Error("Unexpected consume operation abort! Please, file a bug if you see this.");
          return; // Not expected, but must be handled.
        }
        UIPartActionController.Instance.partInventory.editorPartPickedBlockSfx = true;
        EditorLogic.fetch.SetIconAsPart(MakeEditorPartFromItem(items[0]));
      }
    }
  }
  #endregion

  #region Local utility methods
  /// <summary>Returns the dragged item back to the editor when KIS dragging is cancelled.</summary>
  /// <seealso cref="_savedEditorPart"/>
  void EditorItemsCancelled() {
    if (_savedEditorPart != null) {
      UIPartActionController.Instance.partInventory.editorPartPickedBlockSfx = true;
      EditorLogic.fetch.SetIconAsPart(_savedEditorPart);
      _savedEditorPart = null;
    } 
  }

  /// <summary>Cleans up the editor dragging part that was consumed by an inventory.</summary>
  /// <returns>Always <c>true</c>.</returns>
  /// <seealso cref="_savedEditorPart"/>
  bool EditorItemsConsumed() {
    if (_savedEditorPart != null) {
      Hierarchy.SafeDestory(_savedEditorPart);
      _savedEditorPart = null;
    }
    return true;
  }

  /// <summary>Creates part and restores its state from config node.</summary>
  Part MakeEditorPartFromItem(InventoryItem item) {
    var partInfo = item.avPart;
    var part = Instantiate(partInfo.partPrefab);
    part.gameObject.SetActive(true);
    part.name = partInfo.name;
    part.persistentId =
        FlightGlobals.CheckPartpersistentId(part.persistentId, part, false, true,0U);
    var actions = item.itemConfig.GetNode("ACTIONS");
    if (actions != null) {
      part.Actions.OnLoad(actions);
    }
    var events = item.itemConfig.GetNode("EVENTS");
    if (events != null) {
      part.Events.OnLoad(events);
    }
    var effects = item.itemConfig.GetNode("EFFECTS");
    if (effects != null) {
      part.Effects.OnLoad(effects);
    }
    var partData = item.itemConfig.GetNode("PARTDATA");
    if (partData != null) {
      part.OnLoad(partData);
    }
    var moduleIdx = 0;
    foreach (var configNode in item.itemConfig.GetNodes("MODULE")) {
      part.LoadModule(configNode, ref moduleIdx);
    }
    item.itemConfig.GetNodes("RESOURCE").ToList().ForEach(x => part.SetResource(x));
    
    part.InitializeModules();
    part.ModulesBeforePartAttachJoint();
    part.ModulesOnStart();
    part.ModulesOnStartFinished();

    return part;
  }
  #endregion
}

}