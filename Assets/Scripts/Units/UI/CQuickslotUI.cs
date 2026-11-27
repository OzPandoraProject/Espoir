using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CQuickslotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TMP_Text _cooldownText;
    [SerializeField] private TMP_Text _quantityText;
    [SerializeField] private Image _durationFill;
    [SerializeField] private Image _itemIcon;
    [SerializeField] private CTooltipUI _tooltipUI;

    [SerializeField] private bool _showingAvailable;
    [Tooltip("켜면 남은 쿨타임을 1초 이상은 올림 정수, 1초 미만만 소수 1자리로 씁니다.")]
    [SerializeField] private bool _useWholeSeconds = false;
    [Tooltip("켜면 마우스를 올려도 설명을 띄우지 않습니다(꾹 누름 등 다른 컴포넌트가 Tooltip UI 로 띄움).")]
    [SerializeField] private bool _tooltipOnHold = false;

    private bool _hasItem;
    private string _name = "";
    private string _tooltip = "";

    public bool HasItem => _hasItem;
    public string Name => _name;
    public string Description => _tooltip;

    public string Tooltip => $"{_name}\n<size=75%>{_tooltip}</size>";

    public void RefreshIteminfo(int id, int quantity)
    {
        CItemSO item = null;
        if (id != -1)
        {
            CItemSO.TryFind(id, out item);
        }

        if (item == null)
        {
            _hasItem = false;
            _itemIcon.sprite = null;
            _itemIcon.enabled = false;
            _durationFill.gameObject.SetActive(false);
            _quantityText.text = "";
            _name = "";
            _tooltip = "";
            this.RefreshCooldown(0f, 0f);
        }
        else
        {
            _hasItem = true;
            _itemIcon.sprite = item.ItemIcon;
            _itemIcon.enabled = true;
            _durationFill.gameObject.SetActive(true);
            _quantityText.text = quantity.ToString();
            _name = item.Name;
            _tooltip = item.Description;
        }
    }

    public void RefreshCooldown(float ratio, float remainCooldown)
    {
        _durationFill.fillAmount = Mathf.Clamp01(ratio);

        if (remainCooldown <= 0) _cooldownText.gameObject.SetActive(false);
        else
        {
            _cooldownText.text = _useWholeSeconds && remainCooldown >= 1f
                ? Mathf.CeilToInt(remainCooldown).ToString()
                : remainCooldown.ToString("F1");
            _cooldownText.gameObject.SetActive(true);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_tooltipOnHold) return;
        if (_hasItem) _tooltipUI.Enable(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _tooltipUI.Disable();
    }
}
