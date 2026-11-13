using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CInventoryUI : MonoBehaviour
{
    [SerializeField] private GameObject _InventoryPanel;
    [SerializeField] private CInventory _inventory;
    [SerializeField] private Transform _slotContent;
    [SerializeField] private GameObject _itemTooltip;
    [SerializeField] private Image _tooltipIcon;
    [SerializeField] private TMP_Text _tooltipName;
    [SerializeField] private TMP_Text _tooltipInfo;
    [SerializeField] private GameObject _itemContextMenu;
    [SerializeField] private Button _useButton;
    [SerializeField] private Button _quickSlotButton;
    [SerializeField] private Button _moveButton;
    [SerializeField] private Button _discardButton;
    [SerializeField] private Transform _quickSlotContent;
    [SerializeField] private Image _necklaceIcon;
    [SerializeField] private Image _earringIcon;
    [SerializeField] private Image _braceletIcon;
    [SerializeField] private Image _ringIcon;
    [SerializeField] private bool _isBattle;
    [SerializeField] private Image[] _hudQuickSlotIcons = new Image[4];
    [SerializeField] private TMP_Text[] _hudQuickSlotQuantities = new TMP_Text[4];
    [SerializeField] private TMP_Text[] _hudQuickSlotCooldownTexts = new TMP_Text[4];
    [SerializeField] private Transform _playerQuickSlotContent;
    [SerializeField] private Image[] __hudQuickSlotCooldownFills;
    [SerializeField] private TMP_Text _goldText;

    private enum MenuTarget
    {
        Bag,
        Equipment,
        QuickSlot
    }

    private Image[] _itemIcons;
    private TMP_Text[] _quantityTexts;
    private Image[] _quickSlotIcons;
    private TMP_Text[] _quickSlotQuantityTexts;
    private MenuTarget _menuTarget;
    private int _selectedSlotIndex = -1;
    private int _selectedQuickIndex = -1;
    private EEquipmentSlot _selectedEquipmentSlot;
    private bool _isMoving;
    private bool _isRegisteringQuickSlot;
    private Image[] _cooldownFills;
    private TMP_Text[] _cooldownTexts;
    private Image[] _quickCooldownFills;
    private TMP_Text[] _quickCooldownTexts;

    private void Awake()
    {
        _itemIcons = new Image[_inventory.MaxItemSlot];
        _quantityTexts = new TMP_Text[_inventory.MaxItemSlot];
        _cooldownFills = new Image[_inventory.MaxItemSlot];
        _cooldownTexts = new TMP_Text[_inventory.MaxItemSlot];
        _quickCooldownFills = new Image[_inventory.QuickSlotLength];
        _quickCooldownTexts = new TMP_Text[_inventory.QuickSlotLength];
        CopyCooldownUIToSlots(_slotContent, _inventory.MaxItemSlot);
        CopyCooldownUIToSlots(_quickSlotContent, _inventory.QuickSlotLength);

        for (int i = 0; i < _itemIcons.Length; i++)
        {
            int index = i;
            RectTransform slot = (RectTransform)_slotContent.GetChild(i);

            _itemIcons[i] = slot.Find("ItemIcon").GetComponent<Image>();
            _quantityTexts[i] = slot.Find("QuantityText").GetComponent<TMP_Text>();

            _cooldownFills[i] = slot.Find("CooldownFill")?.GetComponent<Image>();
            _cooldownTexts[i] = slot.Find("CooldownText")?.GetComponent<TMP_Text>();

            BindSlotEvents(
                slot,
                () => ShowItemTooltip(_inventory.GetItem(index), slot),
                () => ShowBagMenu(index, slot),
                () => SelectMoveDestination(index));
        }

        _quickSlotIcons = new Image[_inventory.QuickSlotLength];
        _quickSlotQuantityTexts = new TMP_Text[_inventory.QuickSlotLength];

        for (int i = 0; i < _quickSlotIcons.Length; i++)
        {
            int index = i;
            RectTransform slot = (RectTransform)_quickSlotContent.GetChild(i);

            _quickSlotIcons[i] = slot.Find("ItemIcon").GetComponent<Image>();
            _quickSlotQuantityTexts[i] = slot.Find("QuantityText").GetComponent<TMP_Text>();
            _quickCooldownFills[i] = slot.Find("CooldownFill")?.GetComponent<Image>();
            _quickCooldownTexts[i] = slot.Find("CooldownText")?.GetComponent<TMP_Text>();

            BindSlotEvents(slot, () => ShowQuickSlotTooltip(index, slot),
                () => ShowQuickSlotMenu(index, slot),
                () => SelectQuickSlot(index));
        }

        BindEquipmentSlot(_necklaceIcon, EEquipmentSlot.Necklace);
        BindEquipmentSlot(_earringIcon, EEquipmentSlot.Earring);
        BindEquipmentSlot(_braceletIcon, EEquipmentSlot.Bracelet);
        BindEquipmentSlot(_ringIcon, EEquipmentSlot.Ring);

        _useButton.onClick.AddListener(OnClickUseButton);
        _moveButton.onClick.AddListener(OnClickMoveButton);
        _discardButton.onClick.AddListener(OnClickDiscardButton);
        _quickSlotButton.onClick.AddListener(OnClickQuickSlotButton);

        HideTooltip();
        CloseContextMenu();
    }

    private void OnEnable()
    {
        _inventory.OnInventoryChanged += RefreshInventory;
        _inventory.OnGoldChanged += RefreshGold;
    }

    private void OnDisable()
    {
        _inventory.OnInventoryChanged -= RefreshInventory;
        _inventory.OnGoldChanged -= RefreshGold;
    }

    private void Start()
    {
        RefreshInventory();
    }


    private void Update()
    {
        HandleQuickSlotKeys();
        RefreshCooldownDisplays();

        if (Input.GetKeyUp(KeyCode.I))
        {
            if (_InventoryPanel.activeSelf)
            {
                _InventoryPanel.SetActive(false);
                HideTooltip();
                CloseContextMenu();
            }
            else if (_inventory.CanOpenInventory)
            {
                _InventoryPanel.SetActive(true);
                RefreshInventory();
            }
        }

        if (_itemContextMenu.activeInHierarchy && Input.GetMouseButtonDown(0))
        {
            RectTransform menuRect = (RectTransform)_itemContextMenu.transform;
            Canvas canvas = menuRect.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

            if (!RectTransformUtility.RectangleContainsScreenPoint(menuRect, Input.mousePosition, eventCamera))
            {
                CloseContextMenu();
            }

        }
    }

    private void RefreshInventory()
    {
        for (int i = 0; i < _itemIcons.Length; i++)
        {
            CItem item = _inventory.GetItem(i);
            SetIcon(_itemIcons[i], item?.ItemIcon);

            RefreshGold();

            bool showQuantity = item is CItemConsumable;
            _quantityTexts[i].gameObject.SetActive(showQuantity);

            if (showQuantity)
            {
                _quantityTexts[i].text = item.Quantity.ToString();
            }
        }

        SetIcon(_necklaceIcon, _inventory.GetEquipment(EEquipmentSlot.Necklace)?.ItemIcon);
        SetIcon(_earringIcon, _inventory.GetEquipment(EEquipmentSlot.Earring)?.ItemIcon);
        SetIcon(_braceletIcon, _inventory.GetEquipment(EEquipmentSlot.Bracelet)?.ItemIcon);
        SetIcon(_ringIcon, _inventory.GetEquipment(EEquipmentSlot.Ring)?.ItemIcon);

        RefreshQuickSlots();
    }

    private void RefreshQuickSlots()
    {
        for (int i = 0; i < _quickSlotIcons.Length; i++)
        {
            int itemId = _inventory.GetQuickSlotId(i);
            bool isRegistered = itemId != -1;

            CItemSO item = GetQuickSlotItemSO(i);
            Sprite icon = item != null ? item.ItemIcon : null;
            string quantity = isRegistered ? _inventory.GetTotalQuantity(itemId).ToString() : "0";


            SetIcon(_quickSlotIcons[i], item != null ? item.ItemIcon : null);
            _quickSlotQuantityTexts[i].text = quantity;
            _quickSlotQuantityTexts[i].gameObject.SetActive(isRegistered);

            if (i < _hudQuickSlotIcons.Length)
            {
                SetIcon(_hudQuickSlotIcons[i], icon);
            }

            if (i < _hudQuickSlotQuantities.Length && _hudQuickSlotQuantities[i] != null)
            {
                _hudQuickSlotQuantities[i].text = quantity;
            }
        }
    }

    private void SetIcon(Image image, Sprite sprite)
    {
        if (image == null) return;

        image.sprite = sprite;
        image.enabled = sprite != null;
        image.gameObject.SetActive(sprite != null);
    }

    private void BindEquipmentSlot(Image icon, EEquipmentSlot equipmentSlot)
    {
        if (icon == null || !(icon.transform.parent is RectTransform slot))
        {
            return;
        }

        BindSlotEvents(slot, () => ShowItemTooltip(_inventory.GetEquipment(equipmentSlot), slot), () => ShowEquipmentMenu(equipmentSlot, slot));
    }

    private void BindSlotEvents(RectTransform slot, System.Action onHover, System.Action onRightClick, System.Action onLeftClick = null)
    {
        EventTrigger trigger = slot.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = slot.gameObject.AddComponent<EventTrigger>();
        }

        var enter = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };
        enter.callback.AddListener(_ => onHover());
        trigger.triggers.Add(enter);

        var exit = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerExit
        };
        exit.callback.AddListener(_ => HideTooltip());
        trigger.triggers.Add(exit);

        var click = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerClick
        };
        click.callback.AddListener(data =>
        {
            if (!(data is PointerEventData pointer)) return;

            if (pointer.button == PointerEventData.InputButton.Right)
            {
                onRightClick();
            }
            else if (pointer.button == PointerEventData.InputButton.Left)
            {
                onLeftClick?.Invoke();
            }
        });
        trigger.triggers.Add(click);
    }

    private void ShowItemTooltip(CItem item, RectTransform slot)
    {
        if (item == null) return;

        ShowTooltip(item.ItemIcon, item.Name, item.Description, slot);
    }

    private void ShowQuickSlotTooltip(int index, RectTransform slot)
    {
        CItemSO item = GetQuickSlotItemSO(index);
        if (item == null) return;

        ShowTooltip(item.ItemIcon, item.Name, item.Description, slot);
    }

    private CItemSO GetQuickSlotItemSO(int index)
    {
        int itemId = _inventory.GetQuickSlotId(index);
        if (itemId == -1) return null;

        try
        {
            return CItemSO.Find(itemId);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private void ShowTooltip(
        Sprite icon, string itemName, string description, RectTransform slot)
    {
        if (_itemContextMenu.activeInHierarchy) return;

        _tooltipIcon.sprite = icon;
        _tooltipName.text = itemName;
        _tooltipInfo.text = description;

        SetUIPosition(_itemTooltip, slot);
        _itemTooltip.SetActive(true);
    }

    private void HideTooltip()
    {
        _itemTooltip.SetActive(false);
    }

    private void SetUIPosition(GameObject targetUI, RectTransform slot)
    {
        Vector3[] corners = new Vector3[4];
        slot.GetWorldCorners(corners);
        targetUI.transform.position = (corners[2] + corners[3]) / 2f;
    }

    private void ShowBagMenu(int index, RectTransform slot)
    {
        CItem item = _inventory.GetItem(index);
        if (item == null) return;

        CloseContextMenu();
        _menuTarget = MenuTarget.Bag;
        _selectedSlotIndex = index;

        SetMenuButton(_useButton, item is CItemEquipment ? "장착" : "사용");
        SetMenuButton(_discardButton, "버리기");
        SetMenuButton(_moveButton, "이동");
        SetMenuButton(_quickSlotButton, item is CItemConsumable ? "퀵슬롯 등록" : null);

        OpenContextMenu(slot);
    }

    private void ShowEquipmentMenu(EEquipmentSlot equipmentSlot, RectTransform slot)
    {
        if (_inventory.GetEquipment(equipmentSlot) == null) return;

        CloseContextMenu();
        _menuTarget = MenuTarget.Equipment;
        _selectedEquipmentSlot = equipmentSlot;

        SetMenuButton(_useButton, "장착 해제");
        SetMenuButton(_discardButton, null);
        SetMenuButton(_moveButton, null);
        SetMenuButton(_quickSlotButton, null);

        OpenContextMenu(slot);
    }

    private void ShowQuickSlotMenu(int index, RectTransform slot)
    {
        int itemId = _inventory.GetQuickSlotId(index);
        if (itemId == -1) return;

        CloseContextMenu();
        _menuTarget = MenuTarget.QuickSlot;
        _selectedQuickIndex = index;

        SetMenuButton(_useButton, _inventory.GetTotalQuantity(itemId) > 0 ? "사용" : null);
        SetMenuButton(_discardButton, "등록 해제");
        SetMenuButton(_moveButton, null);
        SetMenuButton(_quickSlotButton, null);

        OpenContextMenu(slot);
    }

    private void SetMenuButton(Button button, string label)
    {
        button.gameObject.SetActive(label != null);
        if (label == null) return;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = label;
        }
    }

    private void OpenContextMenu(RectTransform slot)
    {
        HideTooltip();
        SetUIPosition(_itemContextMenu, slot);
        _itemContextMenu.SetActive(true);
    }

    private void CloseContextMenu()
    {
        _itemContextMenu.SetActive(false);
        _selectedSlotIndex = -1;
        _selectedQuickIndex = -1;
        _isMoving = false;
        _isRegisteringQuickSlot = false;
    }

    private void OnClickUseButton()
    {
        bool success;

        switch (_menuTarget)
        {
            case MenuTarget.Equipment:
                success = _inventory.TryUnequipItem(_selectedEquipmentSlot, _isBattle);
                break;

            case MenuTarget.QuickSlot:
                success = _inventory.TryUseQuickSlot(_selectedQuickIndex, _isBattle);
                break;

            default:
                CItem item = _inventory.GetItem(_selectedSlotIndex);
                if (item is CItemEquipment)
                {
                    success = _inventory.TryEquipItem(_selectedSlotIndex, _isBattle);
                }
                else
                {
                    success = item is CItemConsumable &&
                              _inventory.TryUseItem(_selectedSlotIndex, _isBattle);
                }
                break;
        }

        if (!success)
        {
            Debug.LogWarning("아이템 사용, 장착 또는 장착 해제에 실패했습니다.");
        }

        CloseContextMenu();
    }

    private void OnClickDiscardButton()
    {
        bool success;

        if (_menuTarget == MenuTarget.QuickSlot)
        {
            success = _inventory.ClearQuickSlot(_selectedQuickIndex);
        }
        else if (_menuTarget == MenuTarget.Bag && _selectedSlotIndex >= 0)
        {
            success = _inventory.TryDiscardItem(_selectedSlotIndex, 1);
        }
        else
        {
            return;
        }

        if (!success)
        {
            Debug.LogWarning("아이템 버리기 또는 퀵슬롯 등록 해제에 실패했습니다.");
        }

        CloseContextMenu();
    }

    private void OnClickMoveButton()
    {
        if (_menuTarget != MenuTarget.Bag || _selectedSlotIndex < 0) return;

        _isMoving = true;
        _itemContextMenu.SetActive(false);
    }

    private void SelectMoveDestination(int index)
    {
        if (!_isMoving) return;

        if (!_inventory.TryMoveItem(_selectedSlotIndex, index))
        {
            Debug.LogWarning("아이템을 옮길 수 없습니다.");
        }

        CloseContextMenu();
    }

    private void OnClickQuickSlotButton()
    {
        if (_menuTarget != MenuTarget.Bag || !(_inventory.GetItem(_selectedSlotIndex) is CItemConsumable))
        {
            return;
        }

        _isRegisteringQuickSlot = true;
        _itemContextMenu.SetActive(false);
    }

    private void SelectQuickSlot(int index)
    {
        if (!_isRegisteringQuickSlot) return;

        CItem item = _inventory.GetItem(_selectedSlotIndex);
        if (item is CItemConsumable)
        {
            _inventory.RegisterQuickSlot(index, item.Id);
        }

        CloseContextMenu();
    }

    private static readonly KeyCode[] _quickSlotKeys =
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4
    };

    private void HandleQuickSlotKeys()
    {
        int slotCount = Mathf.Min(_quickSlotKeys.Length, _inventory.QuickSlotLength);

        for (int i = 0; i < slotCount; i++)
        {
            if (Input.GetKeyDown(_quickSlotKeys[i]))
            {
                _inventory.TryUseQuickSlot(i, _isBattle);
            }
        }
    }

    private const float CooldownDuration = 15f;

    private void RefreshCooldownDisplays()
    {
        if (_InventoryPanel.activeInHierarchy)
        {
            for (int i = 0; i < _cooldownFills.Length; i++)
            {
                CItem item = _inventory.GetItem(i);
                float remaining = item is CItemConsumable
                    ? _inventory.GetCooldownRemaining(item.Id)
                    : 0f;

                SetCooldownDisplay(_cooldownFills[i], _cooldownTexts[i], remaining);
            }
        }

        for (int i = 0; i < _inventory.QuickSlotLength; i++)
        {
            int itemId = _inventory.GetQuickSlotId(i);
            float remaining = itemId == -1
                ? 0f
                : _inventory.GetCooldownRemaining(itemId);

            SetCooldownDisplay(_quickCooldownFills[i], _quickCooldownTexts[i], remaining);

            Image hudFill = __hudQuickSlotCooldownFills != null &&
                            i < __hudQuickSlotCooldownFills.Length
                ? __hudQuickSlotCooldownFills[i]
                : null;

            TMP_Text hudText = _hudQuickSlotCooldownTexts != null &&
                               i < _hudQuickSlotCooldownTexts.Length
                ? _hudQuickSlotCooldownTexts[i]
                : null;

            SetCooldownDisplay(hudFill, hudText, remaining);
        }
    }

    private void SetCooldownDisplay(Image fill, TMP_Text text, float remaining)
    {
        bool showCooldown = remaining > 0f;

        if (fill != null)
        {
            fill.fillAmount = Mathf.Clamp01(remaining / CooldownDuration);
            fill.gameObject.SetActive(showCooldown);
        }

        if (text != null)
        {
            if (showCooldown)
            {
                text.text = Mathf.CeilToInt(remaining).ToString();
            }

            text.gameObject.SetActive(showCooldown);
        }
    }

    private void CopyCooldownUIToSlots(Transform content, int slotCount)
    {
        if (content == null || content.childCount == 0) return;

        Transform firstSlot = content.GetChild(0);
        Transform fillTemplate = firstSlot.Find("CooldownFill");
        Transform textTemplate = firstSlot.Find("CooldownText");

        int count = Mathf.Min(slotCount, content.childCount);

        for (int i = 1; i < count; i++)
        {
            Transform slot = content.GetChild(i);

            if (fillTemplate != null && slot.Find("CooldownFill") == null)
            {
                Transform fill = Instantiate(fillTemplate, slot, false);
                fill.name = "CooldownFill";
                fill.gameObject.SetActive(false);
            }

            if (textTemplate != null && slot.Find("CooldownText") == null)
            {
                Transform text = Instantiate(textTemplate, slot, false);
                text.name = "CooldownText";
                text.gameObject.SetActive(false);
            }
        }
    }

    private void RefreshGold()
    {
        if (_goldText == null) return;
        _goldText.text = $"{_inventory.Gold:N0} G";
    }
}
