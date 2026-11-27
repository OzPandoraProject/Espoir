using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CShopUI : MonoBehaviour
{
    [SerializeField] private CShop _shop;
    [SerializeField] private CInventory _inventory;
    [SerializeField] private GameObject _shopPanel;
    [SerializeField] private RectTransform _productContent;
    [SerializeField] private GameObject _productRow;
    [SerializeField] private TMP_Text _goldText;
    [SerializeField] private TMP_Text _resultText;
    [SerializeField] private TMP_Text _sellButtonText;
    [SerializeField, Min(0.1f)] private float _closeDistance = 4f;
    [SerializeField] private CInventoryUI _inventoryUI;

    public bool IsOpen => _shopPanel != null && _shopPanel.activeInHierarchy;

    private void Awake()
    {
        _goldText.raycastTarget = false;
        _resultText.raycastTarget = false;
        _purchaseScroll = _productContent.GetComponentInParent<ScrollRect>();
        if (_purchaseScroll != null)
        {
            _purchaseScroll.horizontal = false;
            if (_purchaseScroll.horizontalScrollbar != null)
            {
                _purchaseScroll.horizontalScrollbar.gameObject.SetActive(false);
                _purchaseScroll.horizontalScrollbar = null;
            }
        }
        _productContent.anchorMin = new Vector2(0f, 1f);
        _productContent.anchorMax = new Vector2(1f, 1f);
        _productContent.pivot = new Vector2(0.5f, 1f);
        _productContent.sizeDelta = new Vector2(0f, _productContent.sizeDelta.y);
        _productContent.anchoredPosition = Vector2.zero;

        InitializeSellPanel();
        _productRow.SetActive(false);

        for (int i = 0; i < _shop.Products.Count; i++)
        {
            CItemSO product = _shop.Products[i];
            if (product == null) continue;

            int productIndex = i;
            GameObject row = Instantiate(_productRow, _productContent);
            Button buyButton = ConfigureProductRow(row, product);
            buyButton.onClick.AddListener(() => Buy(productIndex));
            row.SetActive(true);
        }

        Close();
    }

    private Button ConfigureProductRow(GameObject row, CItemSO product)
    {
        Image icon = row.transform.Find("ItemIcon").GetComponent<Image>();
        TMP_Text price = row.transform.Find("PriceText").GetComponent<TMP_Text>();
        Button button = row.transform.Find("BuyButton").GetComponent<Button>();

        foreach (Graphic graphic in row.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }
        Image rowBackground = row.GetComponent<Image>();
        if (rowBackground != null)
        {
            rowBackground.raycastTarget = true;
        }

        CShopTooltip hover = row.AddComponent<CShopTooltip>();
        hover.Bind(_inventoryUI, product);
        // 버튼 배경만 클릭을 받습니다.
        if (button.targetGraphic != null) button.targetGraphic.raycastTarget = true;
        Image buttonImage = button.GetComponent<Image>();
        if (buttonImage != null) buttonImage.raycastTarget = true;

        icon.sprite = product.ItemIcon;
        icon.preserveAspect = true;
        SetUIRect(icon.transform, new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(48f, 48f));
        price.text = $"{product.Cost:N0} G";
        price.fontSizeMin = 14f;
        price.fontSizeMax = 24f;
        price.enableAutoSizing = true;
        price.alignment = TextAlignmentOptions.Center;
        SetUIRect(price.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 48f));
        SetUIRect(button.transform, new Vector2(1f, 0.5f), new Vector2(-80f, 0f), new Vector2(120f, 36f));
        return button;
    }

    private void SetUIRect(Transform target, Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform rect = (RectTransform)target;
        if (target.parent.GetComponent<LayoutGroup>() != null)
        {
            LayoutElement element = target.GetComponent<LayoutElement>();
            if (element == null) element = target.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
        }
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    public void Open()
    {
        RefreshGold();
        _resultText.text = string.Empty;
        _shopPanel.SetActive(true);

        if (CGameManager.Instance != null && CGameManager.Instance.GameState == EGameState.Idle)
        {
            _changedGameState = true;
            CGameManager.Instance.SetGameState(EGameState.UI);
        }
    }

    public void Close()
    {
        EndSell();
        _shopPanel.SetActive(false);
        if (_changedGameState &&
            CGameManager.Instance != null &&
            CGameManager.Instance.GameState == EGameState.UI)
        {
            CGameManager.Instance.SetGameState(EGameState.Idle);
        }
        _changedGameState = false;
    }

    private void Buy(int productIndex)
    {
        if (!IsWithinShopRange())
        {
            Close();
            return;
        }

        bool bought = _shop.TryBuy(_inventory, productIndex);
        RefreshGold();

        _resultText.text = bought
            ? $"{_shop.Products[productIndex].Name} 1개를 구매했습니다."
            : "구매 실패. 골드와 가방 공간을 확인해 주세요.";
    }

    private void RefreshGold()
    {
        _goldText.text = $"{_inventory.Gold:N0} G";
    }

    private bool IsWithinShopRange()
    {
        if (_shop == null || _inventory == null)
        {
            return false;
        }

        Vector3 offset = _shop.transform.position - _inventory.transform.position;
        offset.y = 0f;

        return offset.sqrMagnitude <= _closeDistance * _closeDistance;
    }

    private void Update()
    {
        if (_shopPanel.activeSelf && !IsWithinShopRange())
        {
            Close();
        }
    }

    private const int SellSlotCount = 20;

    [Header("판매 대기 20칸")]
    [SerializeField] private GameObject _sellPanel;
    [SerializeField] private Transform _sellContent;
    [SerializeField] private Button _sellSlotTemplate;
    [SerializeField] private TMP_Text _sellTotalText;

    [Header("판매 수량 선택")]
    [SerializeField] private GameObject _quantityPanel;
    [SerializeField] private TMP_Text _quantityPrompt;
    [SerializeField] private TMP_InputField _quantityInput;

    private sealed class SellEntry
    {
        public CItem Item;
        public int Quantity;
        public int UnitPrice;
    }

    private readonly SellEntry[] _sellEntries = new SellEntry[SellSlotCount];
    private readonly Button[] _sellButtons = new Button[SellSlotCount];
    private readonly Image[] _sellIcons = new Image[SellSlotCount];
    private readonly TMP_Text[] _sellQuantities = new TMP_Text[SellSlotCount];
    private readonly Button[] _removeButtons = new Button[SellSlotCount];
    private bool _isSelling;
    private ScrollRect _purchaseScroll;
    private bool _purchaseWasActive;
    private bool _sellPanelReady;
    private CItem _pendingItem;
    private int _pendingEntryIndex = -1;
    private bool _changedGameState;

    private void InitializeSellPanel()
    {
        _sellPanelReady = _sellPanel != null && _sellContent != null &&
            _sellSlotTemplate != null && _sellTotalText != null &&
            _quantityPanel != null && _quantityPrompt != null && _quantityInput != null;

        if (!_sellPanelReady)
        {
            Debug.LogWarning("CShopUI: 판매 패널과 수량 선택 UI를 연결해 주세요.", this);
            return;
        }

        Transform template = _sellSlotTemplate.transform;
        Transform icon = template.Find("ItemIcon");
        Transform quantity = template.Find("QuantityText");
        Transform remove = template.Find("RemoveButton");

        if (icon == null || icon.GetComponent<Image>() == null ||
            quantity == null || quantity.GetComponent<TMP_Text>() == null ||
            remove == null || remove.GetComponent<Button>() == null)
        {
            _sellPanelReady = false;
            Debug.LogError("판매 슬롯에 ItemIcon(Image), QuantityText(TMP), RemoveButton(Button)이 필요합니다.", this);
            return;
        }

        _sellSlotTemplate.gameObject.SetActive(false);
        _sellTotalText.raycastTarget = false;
        for (int i = 0; i < SellSlotCount; i++)
        {
            int index = i;
            Button button = Instantiate(_sellSlotTemplate, _sellContent);
            button.name = $"SellSlot_{i + 1}";
            _sellButtons[i] = button;
            _sellIcons[i] = button.transform.Find("ItemIcon").GetComponent<Image>();
            _sellQuantities[i] = button.transform.Find("QuantityText").GetComponent<TMP_Text>();
            _removeButtons[i] = button.transform.Find("RemoveButton").GetComponent<Button>();
            TMP_Text quantityLabel = _sellQuantities[i];
            SetUIRect(quantityLabel.transform, new Vector2(1f, 0f), new Vector2(-26f, 15f), new Vector2(48f, 26f));
            quantityLabel.fontSize = 20f;
            quantityLabel.enableAutoSizing = false;
            quantityLabel.alignment = TextAlignmentOptions.BottomRight;
            quantityLabel.raycastTarget = false;
            quantityLabel.transform.SetAsLastSibling();
            _sellIcons[i].raycastTarget = false;
            button.onClick.AddListener(() => EditSellEntry(index));
            _removeButtons[i].onClick.AddListener(() => RemoveSellEntry(index));
            button.gameObject.SetActive(true);
        }

        _sellPanel.SetActive(false);
        _quantityPanel.SetActive(false);
        RefreshSellPanel();
    }

    public void BeginSell()
    {
        if (!_sellPanelReady)
        {
            _resultText.text = "판매 UI 연결을 확인해 주세요.";
            return;
        }

        if (_inventoryUI == null || !_shopPanel.activeSelf || !IsWithinShopRange())
        {
            return;
        }

        if (_isSelling)
        {
            CancelSell();
            return;
        }

        _isSelling = true;
        if (_sellButtonText != null)
        {
            _sellButtonText.text = "구매";
        }
        _inventoryUI.SellItemSelected += SelectSellItem;
        _inventoryUI.SetShopSellMode(true);
        if (_purchaseScroll != null)
        {
            _purchaseWasActive = _purchaseScroll.gameObject.activeSelf;
            _purchaseScroll.gameObject.SetActive(false);
        }
        _sellPanel.SetActive(true);
        RefreshSellPanel();
        _resultText.text = "가방 아이템을 우클릭하면 판매 수량을 고를 수 있습니다.";
    }

    private void SelectSellItem(int slotIndex, CItem item)
    {
        if (!_isSelling || !ReferenceEquals(_inventory.GetItem(slotIndex), item))
        {
            return;
        }

        if (item == null || item.Quantity <= 0 || item.Cost < 0 ||
            (!(item is CItemConsumable) && !(item is CItemEquipment)) ||
            (item is CItemEquipment equipment && equipment.IsEquip))
        {
            _resultText.text = "판매할 수 없는 아이템입니다.";
            return;
        }

        int entryIndex = -1;
        for (int i = 0; i < SellSlotCount; i++)
        {
            if (_sellEntries[i] != null && ReferenceEquals(_sellEntries[i].Item, item))
            {
                entryIndex = i;
                break;
            }
        }

        if (entryIndex < 0)
        {
            for (int i = 0; i < SellSlotCount; i++)
            {
                if (_sellEntries[i] == null)
                {
                    entryIndex = i;
                    break;
                }
            }
        }

        if (entryIndex < 0)
        {
            _resultText.text = "판매 목록은 최대 20칸입니다. 항목을 취소한 뒤 담아 주세요.";
            return;
        }

        CancelQuantity();
        if (item is CItemEquipment)
        {
            SetSellEntry(entryIndex, item, 1);
            return;
        }

        _pendingItem = item;
        _pendingEntryIndex = entryIndex;
        int selectedQuantity = _sellEntries[entryIndex] == null
            ? 1
            : Mathf.Min(_sellEntries[entryIndex].Quantity, item.Quantity);

        _quantityPrompt.text = $"{item.Name}\n몇 개 판매하시겠습니까? (1~{item.Quantity}개)";
        _quantityInput.SetTextWithoutNotify(selectedQuantity.ToString());
        _quantityPanel.SetActive(true);
        _quantityPanel.transform.SetAsLastSibling();
        _quantityInput.ActivateInputField();
    }

    // 수량 창의 '담기' 버튼에서 호출합니다.
    public void AddSelectedQuantity()
    {
        if (!_isSelling || _pendingItem == null || _pendingEntryIndex < 0)
        {
            return;
        }

        if (!_shopPanel.activeSelf || !IsWithinShopRange())
        {
            Close();
            return;
        }

        if (FindBagSlot(_pendingItem) < 0)
        {
            CancelQuantity();
            _resultText.text = "선택한 아이템이 가방에 없습니다. 다시 선택해 주세요.";
            return;
        }

        if (!int.TryParse(_quantityInput.text, out int quantity) ||
            quantity < 1 || quantity > _pendingItem.Quantity)
        {
            _quantityPrompt.text = $"1~{_pendingItem.Quantity} 사이의 수량을 입력해 주세요.";
            return;
        }

        SetSellEntry(_pendingEntryIndex, _pendingItem, quantity);
        CancelQuantity();
    }

    // 개당 판매가를 정수 골드로 계산하며 나머지는 버립니다.
    private int GetSellUnitPrice(CItem item)
    {
        return item is CItemEquipment ? item.Cost / 10 : item.Cost / 5;
    }

    private void SetSellEntry(int index, CItem item, int quantity)
    {
        int price = GetSellUnitPrice(item);
        _sellEntries[index] = new SellEntry
        {
            Item = item,
            Quantity = quantity,
            UnitPrice = price
        };
        RefreshSellPanel();
        _resultText.text = $"{item.Name} {quantity}개 담음 · 개당 {price:N0} G";
    }

    public void CancelQuantity()
    {
        _pendingItem = null;
        _pendingEntryIndex = -1;
        if (_quantityPanel != null) _quantityPanel.SetActive(false);
    }

    public void CancelSell()
    {
        EndSell();
        _resultText.text = "판매 목록을 취소했습니다.";
    }

    private void EditSellEntry(int index)
    {
        SellEntry entry = _sellEntries[index];
        if (entry == null) return;

        int slot = FindBagSlot(entry.Item);
        if (slot < 0)
        {
            _resultText.text = "가방에 없는 항목입니다. X 버튼으로 목록에서 제거해 주세요.";
            return;
        }

        SelectSellItem(slot, entry.Item);
    }

    private void RemoveSellEntry(int index)
    {
        if (_pendingEntryIndex == index) CancelQuantity();
        _sellEntries[index] = null;
        RefreshSellPanel();
    }

    private int FindBagSlot(CItem item)
    {
        for (int i = 0; i < _inventory.MaxItemSlot; i++)
        {
            if (ReferenceEquals(_inventory.GetItem(i), item)) return i;
        }
        return -1;
    }

    private void RefreshSellPanel()
    {
        long total = 0;
        int count = 0;

        for (int i = 0; i < SellSlotCount; i++)
        {
            SellEntry entry = _sellEntries[i];
            bool hasItem = entry != null;
            _sellButtons[i].interactable = hasItem;
            _sellIcons[i].sprite = hasItem ? entry.Item.ItemIcon : null;
            _sellIcons[i].gameObject.SetActive(hasItem);
            _sellQuantities[i].text = hasItem ? entry.Quantity.ToString() : string.Empty;
            _sellQuantities[i].gameObject.SetActive(hasItem);
            _removeButtons[i].gameObject.SetActive(hasItem);

            if (hasItem)
            {
                count++;
                total += (long)entry.UnitPrice * entry.Quantity;
            }
        }

        _sellTotalText.text = $"판매 목록 {count}/20칸 · 합계 {total:N0} G";

        List<CItem> pendingItems = new List<CItem>();

        for (int i = 0; i < _sellEntries.Length; i++)
        {
            if (_sellEntries[i] != null)
            {
                pendingItems.Add(_sellEntries[i].Item);
            }
        }

        if (_inventoryUI != null)
        {
            _inventoryUI.SetSalePendingItems(pendingItems);
        }
    }

    public void ConfirmSell()
    {
        if (!_isSelling || !_shopPanel.activeSelf)
        {
            return;
        }

        if (!IsWithinShopRange())
        {
            Close();
            return;
        }

        if (_quantityPanel.activeSelf)
        {
            _resultText.text = "수량 창에서 담기 또는 취소를 먼저 눌러 주세요.";
            return;
        }

        List<CInventory.SellRequest> requests = new List<CInventory.SellRequest>();
        for (int i = 0; i < SellSlotCount; i++)
        {
            SellEntry entry = _sellEntries[i];
            if (entry == null) continue;

            requests.Add(new CInventory.SellRequest(
                FindBagSlot(entry.Item), entry.Item, entry.Quantity, entry.UnitPrice));
        }

        if (requests.Count == 0)
        {
            _resultText.text = "판매할 아이템을 먼저 담아 주세요.";
            return;
        }

        if (!_inventory.TrySellItems(requests))
        {
            _resultText.text = "판매되지 않았습니다. 보유 수량, 목록과 골드 한도를 확인해 주세요.";
            return;
        }
        EndSell();
        RefreshGold();
        _resultText.text = "목록의 아이템을 모두 판매했습니다.";
    }

    private void EndSell()
    {
        if (_isSelling && _inventoryUI != null)
        {
            _inventoryUI.SellItemSelected -= SelectSellItem;
            _inventoryUI.SetShopSellMode(false);
        }

        if (_isSelling && _purchaseScroll != null)
        {
            _purchaseScroll.gameObject.SetActive(_purchaseWasActive);
        }

        _isSelling = false;
        if (_sellButtonText != null)
        {
            _sellButtonText.text = "판매";
        }
        System.Array.Clear(_sellEntries, 0, _sellEntries.Length);
        CancelQuantity();
        if (_sellPanel != null) _sellPanel.SetActive(false);
    }

    private void OnDisable()
    {
        Close();
    }
}
