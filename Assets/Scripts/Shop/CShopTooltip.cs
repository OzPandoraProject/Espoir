using UnityEngine;
using UnityEngine.EventSystems;

public class CShopTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private CInventoryUI _inventoryUI;
    private CItemSO _product;
    private RectTransform _row;

    public void Bind(CInventoryUI inventoryUI, CItemSO product)
    {
        _inventoryUI = inventoryUI;
        _product = product;
        _row = (RectTransform)transform;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_inventoryUI != null)
        {
            _inventoryUI.ShowProductTooltip(_product, _row);
        }
    }

    public void OnPointerExit(PointerEventData eventData) => HideTooltip();

    private void OnDisable() => HideTooltip();

    private void HideTooltip()
    {
        if (_inventoryUI != null)
        {
            _inventoryUI.HideProductTooltip(_row);
        }
    }
}