// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KIS2.UIKISInventorySlot;
using KSPDev.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace KIS2 {

/// <summary>
/// Unity class that controls KIS inventory layout and its basic GUI functionality.
/// </summary>
public sealed class UIKISInventoryWindow : UIPrefabBaseScript,
    IKSPDevPointerListener<Slot> {
  #region Unity serialized fields
  [SerializeField]
  Text headerText = null;

  [SerializeField]
  GridLayoutGroup slotsGrid = null;

  [SerializeField]
  UIKISHorizontalSliderControl sizeColumnsSlider = null;

  [SerializeField]
  UIKISHorizontalSliderControl sizeRowsSlider = null;
  #endregion

  #region Callback handlers
  /// <summary>Handles mouse button clicks on a slot.</summary>
  /// <param name="host">The Unity class that sent the event.</param>
  /// <param name="slot">The Unity slot class for which this evnt was generated.</param>
  /// <param name="button">The pointer button that was clicked.</param>
  /// <seealso cref="onSlotClick"/>
  public delegate void OnSlotClick(
      UIKISInventoryWindow host, Slot slot, PointerEventData.InputButton button);

  /// <summary>Handles slot's pointer enter/leave events.</summary>
  /// <param name="host">The Unity class that sent the event.</param>
  /// <param name="slot">The Unity slot class for which this evnt was generated.</param>
  /// <param name="isHover">
  /// <c>true</c> if mouse pointer has entered the control's <c>Rect</c>.
  /// </param>
  /// <seealso cref="onSlotHover"/>
  public delegate void OnSlotHover(UIKISInventoryWindow host, Slot slot, bool isHover);

  /// <summary>Handles actions on a slot.</summary>
  /// <param name="host">The Unity class that sent the event.</param>
  /// <param name="slot">The Unity slot class for which this evnt was generated.</param>
  /// <param name="actionButtonNum">The number of the button on the slot that was clicked.</param>
  /// <param name="button">The pointer button that was clicked.</param>
  /// <seealso cref="onSlotAction"/>
  public delegate void OnSlotAction(
      UIKISInventoryWindow host, Slot slot, int actionButtonNum,
      PointerEventData.InputButton button);

  /// <summary>Called when inventory grid size change is requested via GUI.</summary>
  /// <remarks>
  /// When the size is changed via API, this callback is not called. In case of there are multiple
  /// handlers on the callback, the <i>minumum</i> size will be used as final at the end of the
  /// chain. Each handler will be called with a rect, adjusted by the previous callbacks, so the
  /// calling order may be important.
  /// </remarks>
  /// <param name="host">The Unity class that sent the event.</param>
  /// <param name="oldSize">The size before the change.</param>
  /// <param name="newSize">The new size being applied.</param>
  /// <returns>The size that should actually be applied.</returns>
  /// <seealso cref="onGridSizeChange"/>
  public delegate Vector2 OnGridSizeChange(
      UIKISInventoryWindow host, Vector2 oldSize, Vector2 newSize);
  #endregion

  #region Local fields
  const string ColumnsSliderTextPattern = "Width: {0} columns";
  const string RowsSliderTextPattern = "Height: {0} rows";
  Slot prefabSlot;
  #endregion

  #region API properties and fields
  /// <summary>Number of slots per a line of inventory grid.</summary>
  /// <seealso cref="SetGridSize"/>
  public int gridWidth {
    get {
      return _gridWidth;
    }
    private set {
      _gridWidth = value;
      slotsGrid.constraintCount = value;
      sizeColumnsSlider.value = value;
      sizeColumnsSlider.text = string.Format(ColumnsSliderTextPattern, value);
    }
  }
  int _gridWidth;
  
  /// <summary>Number of the slot rows in the inventory grid.</summary>
  /// <seealso cref="SetGridSize"/>
  public int gridHeight {
    get {
      return _gridHeight;
    }
    private set {
      _gridHeight = value;
      sizeRowsSlider.value = value;
      sizeRowsSlider.text = string.Format(RowsSliderTextPattern, value);
    }
  }
  int _gridHeight;

  /// <summary>Inventory window title.</summary>
  public string title {
    get { return headerText.text; }
    set { headerText.text = value; }
  }

  /// <summary>Currently started tooltip.</summary>
  /// <remarks>
  /// The tooltip is bound to the hovered slot. When pointer goes out, the tooltip gets destroyed.
  /// </remarks>
  /// <seealso cref="StartSlotTooltip"/>
  /// <value><c>null</c> if not tooltip is currently presented.</value>
  /// <seealso cref="StartSlotTooltip"/>
  public UIKISInventoryTooltip.Tooltip currentTooltip {
    get { return _currentTooltip; }
    private set {
      DestroyTooltip();
      _currentTooltip = value;
    }
  }
  UIKISInventoryTooltip.Tooltip _currentTooltip;

  /// <summary>Slot that is currently being hovered over.</summary>
  /// <value><c>null</c> pointer doesn't hover over any slot of this inventory.</value>
  public Slot hoveredSlot {
    get { return _hoveredSlot; }
    private set {
      if (value == null || value != _hoveredSlot) {
        DestroyTooltip();
      }
      _hoveredSlot = value;
    }
  }
  Slot _hoveredSlot;

  /// <summary>Currently allocated slot controls.</summary>
  /// <remarks>
  /// When new slots are added due to size increase, they are added at the end of the list. When the
  /// size gets reduced, the slots at the end are destroyed first.
  /// </remarks>
  /// <seealso cref="SetGridSize"/>
  public Slot[] slots {
    get { return _slots; }
    private set { _slots = value; }
  }
  Slot[] _slots = new Slot[0];

  /// <summary>Minmum size of the grid.</summary>
  /// <remarks>
  /// Consistency is not checked. The caller is resposible to keep the grid size relevant to the
  /// window controls.
  /// </remarks>
  /// <seealso cref="maxSize"/>
  /// <seealso cref="SetGridSize"/>
  public Vector2 minSize {
    get { return new Vector2(sizeColumnsSlider.minValue, sizeRowsSlider.minValue); }
    set {
      sizeColumnsSlider.minValue = value.x;
      sizeRowsSlider.minValue = value.y;
    }
  }

  /// <summary>Maximum size of the grid.</summary>
  /// <remarks>
  /// Consistency is not checked. The caller is resposible to keep the grid size relevant to the
  /// window controls.
  /// </remarks>
  /// <seealso cref="minSize"/>
  /// <seealso cref="SetGridSize"/>
  public Vector2 maxSize {
    get { return new Vector2(sizeColumnsSlider.maxValue, sizeRowsSlider.maxValue); }
    set {
      sizeColumnsSlider.maxValue = value.x;
      sizeRowsSlider.maxValue = value.y;
    }
  }

  /// <summary>
  /// Callback that is called when any of the major pointer buttons is clicked on top of a slot. 
  /// </summary>
  public readonly List<OnSlotClick> onSlotClick = new List<OnSlotClick>();

  /// <summary>Callback that is called when pointer enters or leaves a slot.</summary>
  public readonly List<OnSlotHover> onSlotHover = new List<OnSlotHover>();

  /// <summary>
  /// Callback that is called when any of the major pointer buttons is clicked over a slot action
  /// button.
  /// </summary>
  public readonly List<OnSlotAction> onSlotAction = new List<OnSlotAction>();

  /// <summary>Callback that is called when UI wants to change the inventory grid size.</summary>
  public readonly List<OnGridSizeChange> onGridSizeChange = new List<OnGridSizeChange>();
  #endregion

  #region UIPrefabBaseScript overrides
  /// <inheritdoc/>
  public override void Start() {
    base.Start();
    if (!isPrefab) {
      prefabSlot = slotsGrid.transform.GetChild(0).GetComponent<Slot>();
      prefabSlot.transform.SetParent(null, worldPositionStays: true);
    }
  }

  /// <inheritdoc/>
  public override bool InitPrefab() {
    if (!base.InitPrefab()) {
      return false;
    }
    if (slotsGrid.transform.childCount == 0) {
      LogError("There must be at least one slot instance! None found.");
      return false;
    }
    var firstSlot = slotsGrid.transform.GetChild(0).GetComponent<Slot>();
    if (firstSlot == null) {
      LogError("The first slot prefab doesn't implement required type!");
      return false;
    }
    firstSlot.InitPrefab();
    firstSlot.gameObject.SetActive(false);
    while (slotsGrid.transform.childCount > 1) {
      DestroyImmediate(slotsGrid.transform.GetChild(1).gameObject);
    }
    slotsGrid.gameObject.SetActive(false);
    return true;
  }
  #endregion

  #region IKSPDevPointerListener implementation - UIKISInventorySlot
  /// <inheritdoc/>
  public int priority { get { return 0; } }
  
  /// <inheritdoc/>
  public void OnPointerButtonClick(
      GameObject owner, Slot source, PointerEventData eventData) {
    onSlotClick.ForEach(notify => notify(this, source, eventData.button));
  }

  /// <inheritdoc/>
  public void OnPointerEnter(
      GameObject owner, Slot source, PointerEventData eventData) {
    hoveredSlot = source;
    onSlotHover.ForEach(notify => notify(this, source, true));
  }

  /// <inheritdoc/>
  public void OnPointerExit(
      GameObject owner, Slot source, PointerEventData eventData) {
    hoveredSlot = null;
    onSlotHover.ForEach(notify => notify(this, source, false));
  }
  #endregion

  #region Unity only listeners
  /// <summary>Not for external usage!</summary>
  public void OnSizeChanged(UIKISHorizontalSliderControl slider) {
    // Restore sliders to the original and expect the proper positions set in SetGridSize.
    var newWidth = (int) sizeColumnsSlider.value;
    var newHeight = (int) sizeRowsSlider.value;
    sizeColumnsSlider.value = gridWidth;
    sizeRowsSlider.value = gridHeight;
    SetGridSize(newWidth, newHeight);
  }
  #endregion

  #region API methods
  /// <summary>
  /// Creates a tooltip to be presented as long as pointer is hovering over the current slot. 
  /// </summary>
  /// <remarks>
  /// An error will be logged if this method is called when no slot is hovered over.
  /// </remarks>
  /// <returns>The tooltip that was created.</returns>
  /// <seealso cref="onSlotHover"/>
  /// <seealso cref="currentTooltip"/>
  public UIKISInventoryTooltip.Tooltip StartSlotTooltip() {
    if (hoveredSlot == null) {
      LogError("Cannot start tooltip when no slot is hovered!");
      return null;
    }
    currentTooltip = UnityPrefabController.CreateInstance<UIKISInventoryTooltip.Tooltip>(
        "tooltip", transform.parent);
    currentTooltip.followsPointer = true;
    return currentTooltip;
  }

  /// <summary>Sets new size of the inventory grid.</summary>
  /// <remarks>
  /// This method ensures that every grid cell has a slot object. If due to resize more cells are
  /// needed, then extra slot objects are added at the end of the <see cref="slots"/>. If cells need
  /// to be removed, then they are removed starting from the very last one in the list.
  /// <para>The callbacks can affect the actual size that will be set.</para>
  /// </remarks>
  /// <param name="width">The new width.</param>
  /// <param name="height">The new height.</param>
  /// <seealso cref="onGridSizeChange"/>
  /// <seealso cref="slots"/>
  /// <seealso cref="gridWidth"/>
  /// <seealso cref="gridHeight"/>
  public void SetGridSize(int width, int height) {
    // Negotiate the new size woth the callbacks. 
    var oldSize = new Vector2(gridWidth, gridHeight);
    var newSize = new Vector2(width, height);
    var originalNewSize = newSize;
    foreach (var callback in onGridSizeChange) {
      newSize = Vector2.Min(newSize, callback(this, oldSize, newSize));
    }
    if (newSize != originalNewSize) {
      LogInfo("Resize bounds changed by handlers: original={0}, actual={1}",
              originalNewSize, newSize);
    }
    width = (int) newSize.x;
    height = (int) newSize.y;

    gridWidth = width;
    gridHeight = height;
    var neededSlots = width * height;
    slotsGrid.gameObject.SetActive(neededSlots > 0);
    if (slotsGrid.transform.childCount != neededSlots) {
      LogInfo("Resizing slots grid: width={0}, height={1}", width, height);
      while (slotsGrid.transform.childCount > neededSlots) {
        DeleteSlot();
      }
      while (slotsGrid.transform.childCount < neededSlots) {
        AddSlot();
      }
      slots = slotsGrid.transform.Cast<Transform>()
          .Select(t => t.GetComponent<Slot>())
          .ToArray();
    }
    SendMessage("ControlUpdated", gameObject, SendMessageOptions.DontRequireReceiver);
  }
  #endregion

  #region Local utility methods
  void DeleteSlot() {
    var slotObj = slotsGrid.transform.GetChild(slotsGrid.transform.childCount - 1).gameObject;
    slotObj.name = "$disposed";
    slotObj.transform.SetParent(null, worldPositionStays: true);
    slotObj.SetActive(false);
    Destroy(slotObj);
  }

  Slot AddSlot() {
    var newSlot = Instantiate<Slot>(prefabSlot, slotsGrid.transform, worldPositionStays: true);
    newSlot.name = "Slot";
    newSlot.gameObject.SetActive(true);
    return newSlot;
  }

  void DestroyTooltip() {
    if (_currentTooltip != null) {
      _currentTooltip.gameObject.SetActive(false);
      Destroy(_currentTooltip.gameObject);
    }
    _currentTooltip = null;
  }
  #endregion
}

}  // namespace
