/*  지금 생각하는 구조
 *    - CInventory는 그냥 스크립트로 CItem을 담고, 정상적인 상태인지만 확인
 *    - CInventoryUI로 인벤토리 띄웠을 때 보여지는거 관리
 *    
 *    - CItem은 아이템 정보랑 갯수 담기 (CItemSO의 데이터 복사)
 *    - CItemShown은 인벤토리를 띄웠을 때 보여주는 용도로 사용하기
 */

using System;
using System.Collections.Generic;
using UnityEngine;

public class CInventory : MonoBehaviour
{
    [SerializeField] private CPlayer _player;
    [SerializeField] private int _gold;

    private List<CItem> _items = new List<CItem>();

    private const int _maxItemSlot = 64;
    private const int _maxItemQuantity = 99;
    private const int QuickSlot = 4;
    private const int EmptyQuickSlotId = -1;
    private const float ConsumableCooltime = 15f;
    private int[] _quickSlotIds = new int[QuickSlot];
    // 아이템 ID별 쿨타임 종료 시각
    // !! 아이템 쿨타임을 아이템 클래스가 들고있는 방향으로 
    private Dictionary<int, float> _cooldownEndTimes = new Dictionary<int, float>();
    private bool _canOpenInventory = true;
    // UI가 데이터를 다시 읽도록 알리는 이벤트
    public event Action OnInventoryChanged;
    public int MaxItemSlot => _maxItemSlot;
    public int QuickSlotLength => QuickSlot;
    public static int MaxSlot => _maxItemSlot;
    public static int MaxQuantity => _maxItemQuantity;

    // 게임 매니저가 설정하고 UI가 확인
    public bool CanOpenInventory
    {
        get => _canOpenInventory;
        set => _canOpenInventory = value;
    }

    private void Awake()
    {
        if (_player == null)
        {
            _player = GetComponent<CPlayer>();
        }

        for (int i = 0; i < _maxItemSlot; i++)
        {
            _items.Add(null);
        }

        for (int i = 0; i < QuickSlot; i++)
        {
            _quickSlotIds[i] = EmptyQuickSlotId;
        }
    }

    // 가방 조회하기

    public CItem GetItem(int slotIndex)
    {
        if (!IsValidSlot(slotIndex))
        {
            return null;
        }

        return _items[slotIndex];
    }

    // 이거는 하드코딩의 예시. 1~64까지 다 돌아보는 것임.
    // !! 전체 순회가 아니라 빈 슬롯을 보관하는 (딕셔너리나 list 등) 것을 따로 만들어서 순회 속도를 줄일 수 있다.
    public int FindEmptySlot()
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i] == null)
            {
                return i;
            }
        }

        return -1;
    }

    //!! 이것도 딕셔너리로 모든 아이템 기록해 둘 수 있지 않을까..??? 
    public int GetTotalQuantity(int itemId)
    {
        int total = 0;

        for (int i = 0; i < _items.Count; i++)
        {
            CItem item = _items[i];

            if (item != null && item.Id == itemId)
            {
                total += item.Quantity;
            }
        }

        return total;
    }

    // 아이템 획득

    
    public int Debugitemcode;

    [ContextMenu("아이템 추가")]

    public void Debugadditem()
    {
        int added = AddItem(Debugitemcode);
        Debug.Log($"아이템 추가 : {added}개");
    }

    // 실제로 추가한 수량을 반환
    public int AddItem(int id, int quantity = 1)
    {
        if (quantity <= 0)
        {
            return 0;
        }

        /*
        !! if(TryMakeItem(id, out CItem item))
        {
            이제 인벤터리에 넣는 모시깽~~
        }
        */

        CItem sample = TryMakeItem(id);

        if (sample == null)
        {
            return 0;
        }

        bool stackable = sample is CItemConsumable;
        int remaining = quantity;

        // 기존 스택에 들어갈 수량 계산
        if (stackable)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                CItem current = _items[i];

                if (!(current is CItemConsumable) || current.Id != id)
                {
                    continue;
                }

                int space = Mathf.Max(0, _maxItemQuantity - current.Quantity);

                remaining -= Mathf.Min(space, remaining);
            }
        }

        // 빈 슬롯에 필요한 객체를 먼저 준비
        List<CItem> newItems = new List<CItem>();

        for (int i = 0; i < _items.Count && remaining > 0; i++)
        {
            if (_items[i] != null)
            {
                continue;
            }

            CItem item = newItems.Count == 0 ? sample : TryMakeItem(id);

            if (item == null)
            {
                return 0;
            }

            int count = stackable ? Mathf.Min(_maxItemQuantity, remaining) : 1;

            item.Quantity = count;
            newItems.Add(item);
            remaining -= count;
        }

        int addedQuantity = quantity - remaining;

        if (addedQuantity == 0)
        {
            return 0;
        }

        // 객체 준비가 끝나면 기존 스택부터 실제 반영
        int toAdd = addedQuantity;

        if (stackable)
        {
            for (int i = 0; i < _items.Count && toAdd > 0; i++)
            {
                CItem current = _items[i];

                if (!(current is CItemConsumable) || current.Id != id)
                {
                    continue;
                }

                int space = Mathf.Max(0, _maxItemQuantity - current.Quantity);

                int count = Mathf.Min(space, toAdd);

                current.Quantity += count;
                toAdd -= count;
            }
        }

        // 가장 앞쪽 빈 슬롯부터 새 객체 배치
        int newItemIndex = 0;

        for (int i = 0; i < _items.Count && newItemIndex < newItems.Count; i++)
        {
            if (_items[i] == null)
            {
                _items[i] = newItems[newItemIndex];
                newItemIndex++;
            }
        }

        OnInventoryChanged?.Invoke();
        return addedQuantity;
    }

    public bool TryMoveItem(int fromIndex, int toIndex)
    {
        if (!IsValidSlot(fromIndex) || !IsValidSlot(toIndex))
        {
            return false;
        }

        if (fromIndex == toIndex || _items[fromIndex] == null)
        {
            return false;
        }
        CItem temp = _items[toIndex];

        _items[toIndex] = _items[fromIndex];
        _items[fromIndex] = temp;

        OnInventoryChanged?.Invoke();
        return true;
    }

    // 아이템 버리기

    public bool TryDiscardItem(int slotIndex, int quantity = 1)
    {
        if (!IsValidSlot(slotIndex) || quantity <= 0 || _player == null)
        {
            return false;
        }

        CItem item = _items[slotIndex];

        if (item == null || item.Quantity < quantity)
        {
            return false;
        }

        if (item is CItemEquipment equipment)
        {
            if (equipment.IsEquip || quantity != 1)
            {
                return false;
            }
        }
        else if (!(item is CItemConsumable))
        {
            return false;
        }

        // 팀 함수에서 수량을 차감하므로 여기서 다시 차감하지 않음
        bool removeSlot = item.OnDiscard(_player, quantity);

        if (removeSlot)
        {
            _items[slotIndex] = null;
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    // 소모품 사용

    // 호출하는 쪽에서 현재 전투 여부를 전달
    public bool TryUseItem(int slotIndex, bool isBattle)
    {
        if (!IsValidSlot(slotIndex) || _player == null)
        {
            return false;
        }

        CItemConsumable item =
            _items[slotIndex] as CItemConsumable;

        if (item == null || item.Quantity <= 0)
        {
            return false;
        }

        CItemConsumableSO origin = item.OriginItem as CItemConsumableSO;

        // 투척형은 매니저 나오기 전까진 사용 안함
        if (origin == null || origin.IsThrow)
        {
            return false;
        }

        if (GetCooldownRemaining(item.Id) > 0f)
        {
            return false;
        }

        item.OnUse(_player);

        // OnUse()는 수량을 줄이지 않으니 차감
        item.Quantity -= 1;

        if (item.Quantity == 0)
        {
            _items[slotIndex] = null;
        }

        _cooldownEndTimes[item.Id] = Time.time + ConsumableCooltime;

        OnInventoryChanged?.Invoke();
        return true;
    }

    public float GetCooldownRemaining(int itemId)
    {
        if (!_cooldownEndTimes.TryGetValue(itemId, out float endTime))
        {
            return 0f;
        }

        float remaining = endTime - Time.time;

        if (remaining > 0f)
        {
            return remaining;
        }

        _cooldownEndTimes.Remove(itemId);
        return 0f;
    }

    // 장비

    public CItemEquipment GetEquipment(EEquipmentSlot slot)
    {
        if (_player == null || _player.Equipment == null)
        {
            return null;
        }

        _player.Equipment.TryGetValue(slot, out CItemEquipment equipment);

        return equipment;
    }

    public bool TryEquipItem(int bagSlotIndex, bool isBattle)
    {
        if (!IsValidSlot(bagSlotIndex) || _player == null || _player.Equipment == null)
        {
            return false;
        }

        CItemEquipment incoming = _items[bagSlotIndex] as CItemEquipment;

        if (incoming == null || incoming.IsEquip)
        {
            return false;
        }

        bool available = isBattle ? incoming.AvailableInBattle() : incoming.AvailableInLobby();

        if (!available)
        {
            return false;
        }

        Dictionary<EEquipmentSlot, CItemEquipment> equipped = _player.Equipment;

        bool hadKey = equipped.TryGetValue(incoming.Slot, out CItemEquipment previous);

        if (previous == null)
        {
            // 기존 OnEquip()이 슬롯 키를 필요로 함
            if (!hadKey)
            {
                equipped[incoming.Slot] = null;
            }

            if (!incoming.OnEquip(_player))
            {
                if (!hadKey && equipped[incoming.Slot] == null)
                {
                    equipped.Remove(incoming.Slot);
                }

                return false;
            }

            _items[bagSlotIndex] = null;
        }
        else
        {
            if (ReferenceEquals(previous, incoming) ||
                !previous.IsEquip)
            {
                return false;
            }

            if (!previous.OnUnequip(_player))
            {
                return false;
            }

            if (!incoming.OnEquip(_player))
            {
                // 새 장비를 장착하지 못하면 기존 장비 복원
                if (!previous.OnEquip(_player))
                {
                    CLogger.Error(
                        "CInventory: 교체 전 장비를 복원하지 못했습니다.");
                }

                return false;
            }

            // 기존 장비를 새 장비가 있던 가방 슬롯에 보관
            _items[bagSlotIndex] = previous;
        }

        _player.EquipmentResync();
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool TryUnequipItem(EEquipmentSlot slot, bool isBattle)
    {
        if (isBattle || _player == null || _player.Equipment == null)
        {
            return false;
        }

        int emptySlot = FindEmptySlot();

        if (emptySlot < 0)
        {
            return false;
        }

        if (!_player.Equipment.TryGetValue(
                slot, out CItemEquipment equipment) || equipment == null || !equipment.IsEquip)
        {
            return false;
        }

        if (!equipment.OnUnequip(_player))
        {
            return false;
        }

        _items[emptySlot] = equipment;

        _player.EquipmentResync();
        OnInventoryChanged?.Invoke();
        return true;
    }

    // 퀵슬롯

    public bool RegisterQuickSlot(int quickIndex, int itemId)
    {
        if (!IsValidQuickSlot(quickIndex))
        {
            return false;
        }

        for (int i = 0; i < _items.Count; i++)
        {
            CItem item = _items[i];

            if (!(item is CItemConsumable) || item.Id != itemId || item.Quantity <= 0)
            {
                continue;
            }

            for (int slot = 0; slot < _quickSlotIds.Length; slot++)
            {
                if (slot != quickIndex && _quickSlotIds[slot] == itemId)
                {
                    _quickSlotIds[slot] = EmptyQuickSlotId;
                }
            }

            _quickSlotIds[quickIndex] = itemId;
            OnInventoryChanged?.Invoke();
            return true;
        }

        return false;
    }

    public bool ClearQuickSlot(int quickIndex)
    {
        if (!IsValidQuickSlot(quickIndex))
        {
            return false;
        }

        _quickSlotIds[quickIndex] = EmptyQuickSlotId;
        OnInventoryChanged?.Invoke();
        return true;
    }

    public int GetQuickSlotId(int quickIndex)
    {
        if (!IsValidQuickSlot(quickIndex))
        {
            return EmptyQuickSlotId;
        }

        return _quickSlotIds[quickIndex];
    }

    public void GetItemCooldown(int itemId, out float ratio, out float remainCooldown)
    {
        remainCooldown = GetCooldownRemaining(itemId);
        ratio = Mathf.Clamp01(remainCooldown / ConsumableCooltime);
    }

    public bool TryUseQuickSlot(int quickIndex, bool isBattle)
    {
        int itemId = GetQuickSlotId(quickIndex);

        if (itemId == EmptyQuickSlotId)
        {
            return false;
        }

        for (int i = 0; i < _items.Count; i++)
        {
            CItem item = _items[i];

            if (item is CItemConsumable &&  item.Id == itemId && item.Quantity > 0)
            {
                return TryUseItem(i, isBattle);
            }
        }

        return false;
    }

    // 내부 검사

    private bool IsValidSlot(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < _items.Count;
    }

    private bool IsValidQuickSlot(int quickIndex)
    {
        return quickIndex >= 0 && quickIndex < QuickSlot;
    }

    private CItem TryMakeItem(int id)
    {
        try
        {
            CItem item = CItem.MakeItem(id);

            if (item is CItemConsumable || item is CItemEquipment)
            {
                return item;
            }

            return null;
        }
        catch (KeyNotFoundException)
        {
            CLogger.Error(
                $"CInventory: 등록되지 않은 아이템 ID {id}");

            return null;
        }
        catch (ArgumentException exception)
        {
            CLogger.Error(
                $"CInventory: 아이템 생성 데이터 확인 필요. {exception.Message}");

            return null;
        }
    }

    public int Gold => _gold;
    public event Action OnGoldChanged;

    public bool TryAddGold(int amount)
    {
        if (amount <= 0 || (long)_gold + amount > int.MaxValue)
        {
            return false;
        }

        _gold += amount;
        OnGoldChanged?.Invoke();
        return true;
    }

    public bool TrySpendGold(int amount)
    {
        if (amount <= 0 || amount > _gold)
        {
            return false;
        }

        _gold -= amount;
        OnGoldChanged?.Invoke();
        return true;
    }

    /*
    private bool TryMakeItem(int id, out CItem item)
    {
        try
        {
            item = CItem.MakeItem(id);

            if (item is CItemConsumable ||
                item is CItemEquipment)
            {
                return true;
            }

            return false;
        }
        catch (KeyNotFoundException)
        {
            CLogger.Error(
                $"CInventory: 등록되지 않은 아이템 ID {id}");
            item = null;

            return false;
        }
        catch (ArgumentException exception)
        {
            CLogger.Error(
                $"CInventory: 아이템 생성 데이터 확인 필요. {exception.Message}");
            item = null;

            return false;
        }
    }*/
}