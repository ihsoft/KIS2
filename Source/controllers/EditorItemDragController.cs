// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KISAPIv2;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using KSPDev.ProcessingUtils;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>Controller that deals with dragged items in the editor scenes.</summary>
[KSPAddon(KSPAddon.Startup.EditorAny, false /*once*/)]
sealed class EditorItemDragController : MonoBehaviour, IKisDragTarget {

  #region MonoBehaviour overrides
  void Awake() {
    DebugEx.Fine("[{0}] Controller started", nameof(EditorItemDragController));
    KisApi.ItemDragController.RegisterTarget(this);
  }

  void OnDestroy() {
    DebugEx.Fine("[{0}] Controller stopped", nameof(EditorItemDragController));
    KisApi.ItemDragController.UnregisterTarget(this);
  }
  #endregion

  #region IKisDragTarget implementation
  /// <inheritdoc/>
  public Component unityComponent => this;

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
  void IKisDragTarget.OnFocusTarget(IKisDragTarget newTarget) {
    // DRAGGING EDITOR PART - hover over KIS window. 
    // Take the editors dragged part and start KIS dragging for it. Hide the part in the editor.
    if (newTarget != null && EditorLogic.SelectedPart != null) {
      // We cannot nicely handle the cancel action when the pointer is hovering over an inventory dialog.
      // So, always start the dragging, but indicate if the item cannot be added anywhere.
      var draggedItem = InventoryItemImpl.FromPart(EditorLogic.SelectedPart);
      draggedItem.materialPart = EditorLogic.SelectedPart;
      KisApi.ItemDragController.LeaseItems(
          KisApi.PartIconUtils.MakeDefaultIcon(draggedItem.materialPart),
          new InventoryItem[] { draggedItem },
          EditorItemsConsumed, EditorItemsCancelled,
          allowInteractiveCancel: false);
      UIPartActionController.Instance.partInventory.editorPartDroppedBlockSfx = true;
      EditorLogic.fetch.ReleasePartToIcon();
      draggedItem.materialPart.gameObject.SetLayerRecursive(LayerMask.NameToLayer("UIAdditional"));
      draggedItem.materialPart.highlighter.ReinitMaterials();
      draggedItem.materialPart.SetHighlightType(Part.HighlightType.OnMouseOver);
      draggedItem.materialPart.SetHighlight(active: false, recursive: true);
      return;
    }

    // DRAGGING EDITOR PART - focus blur from a KIS window.
    // Cancel KIS dragging and return the dragged part back to the editor. 
    if (newTarget == null && KisApi.ItemDragController.isDragging
        && KisApi.ItemDragController.leasedItems[0].materialPart != null) {
      KisApi.ItemDragController.CancelItemsLease();
      return;
    }

    // DRAGGING KIS INVENTORY PART - focus blur from a KIS window.
    // If there is just one part, consume it and start the editor's drag. Otherwise, do nothing and
    // let KIS inventories to handle the logic.
    if (newTarget == null && KisApi.ItemDragController.isDragging
        && KisApi.ItemDragController.leasedItems[0].materialPart == null) {
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
  void EditorItemsCancelled() {
    Preconditions.HasSize(KisApi.ItemDragController.leasedItems, 1);
    var item = KisApi.ItemDragController.leasedItems[0];
    UIPartActionController.Instance.partInventory.editorPartPickedBlockSfx = true;
    EditorLogic.fetch.SetIconAsPart(item.materialPart);
    item.materialPart = null;
  }

  /// <summary>Cleans up the editor dragging part that was consumed by an inventory.</summary>
  /// <returns>Always <c>true</c>.</returns>
  bool EditorItemsConsumed() {
    Preconditions.HasSize(KisApi.ItemDragController.leasedItems, 1);
    var item = KisApi.ItemDragController.leasedItems[0];
    Hierarchy.SafeDestroy(item.materialPart);
    item.materialPart = null;
    return true;
  }

  /// <summary>Creates part and restores its state from config node.</summary>
  /// <remarks>
  /// The part is created non-started! Be careful with manual part's starting. The down stream logic may choose to start
  /// the part regardless to fact it has already started. And it will have a lot of bad side effects. Only start the
  /// part if are confident it must be done.
  /// </remarks>
  Part MakeEditorPartFromItem(InventoryItem item) {
    var part = item.snapshot.CreatePart();
    part.InitializeModules();
    part.ModulesBeforePartAttachJoint();
    return part;
  }
  #endregion
}

}
