// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KIS2.UIKISInventorySlot;
using KSPDev.Unity;
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

  #region Local fields
  const string ColumnsSliderTextPattern = "Width: {0} columns";
  const string RowsSliderTextPattern = "Height: {0} rows";
  Slot prefabSlot;
  IKISInventoryWindowController inventoryController;
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
  #endregion

  #region UIPrefabBaseScript overrides
  /// <inheritdoc/>
  public override void Start() {
    base.Start();
    inventoryController = GetComponent<IKISInventoryWindowController>();
    if (inventoryController == null) {
      LogError("Inventory controller is not found!");
    }
    prefabSlot = slotsGrid.transform.GetChild(0).GetComponent<Slot>();
    prefabSlot.transform.SetParent(null, worldPositionStays: true);
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
    while (slotsGrid.transform.childCount > 1) {
      DestroyImmediate(slotsGrid.transform.GetChild(1).gameObject);
    }
    return true;
  }
  #endregion

  #region IKSPDevPointerListener implementation - UIKISInventorySlot
  /// <inheritdoc/>
  public int priority { get { return 0; } }
  
  /// <inheritdoc/>
  public void OnPointerButtonClick(
      GameObject owner, Slot source, PointerEventData eventData) {
    inventoryController.OnSlotClick(this, source, eventData.button);
  }

  /// <inheritdoc/>
  public void OnPointerEnter(
      GameObject owner, Slot source, PointerEventData eventData) {
    hoveredSlot = source;
    inventoryController.OnSlotHover(this, source, true);
  }

  /// <inheritdoc/>
  public void OnPointerExit(
      GameObject owner, Slot source, PointerEventData eventData) {
    hoveredSlot = null;
    inventoryController.OnSlotHover(this, source, false);
  }
  #endregion

  #region Grid size change listener
  public void OnSizeChanged(UIKISHorizontalSliderControl slider) {
    var oldSize = new Vector2(gridWidth, gridHeight);
    var newSize = new Vector2(sizeColumnsSlider.value, sizeRowsSlider.value);
    // Restore sliders to the original and expect the proper size set from the callbacks.
    sizeColumnsSlider.value = oldSize.x;
    sizeRowsSlider.value = oldSize.y;
    newSize = inventoryController.OnSizeChanged(this, oldSize, newSize);
    SetGridSize((int) newSize.x, (int) newSize.y);
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
  /// <seealso cref="IKISInventoryWindowController.OnSlotHover"/>
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
  /// </remarks>
  /// <param name="width">The new width.</param>
  /// <param name="height">The new height.</param>
  /// <seealso cref="IKISInventoryWindowController.OnSizeChanged"/>
  /// <seealso cref="slots"/>
  /// <seealso cref="gridWidth"/>
  /// <seealso cref="gridHeight"/>
  public void SetGridSize(int width, int height) {
    gridWidth = width;
    gridHeight = height;
    var neededSlots = width * height;
    if (slotsGrid.transform.childCount == neededSlots) {
      return;  // We're already good.
    }
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
