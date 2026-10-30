using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class CItem
{
    protected CItemSO _originItem;
    protected int _id;
    protected string _name;
    protected Sprite _itemIcon;
    protected string _description;
    protected int _quantity;

    public CItemSO OriginItem => _originItem;
    public int Id => _id;
    public string Name => _name;
    public Sprite ItemIcon => _itemIcon;
    public string Description => _description;
    
    public abstract int Quantity { get; set;}

    public abstract bool AvailableInLobby();
    public abstract bool AvailableInBattle();

    public abstract bool Setup(CItemSO originItem);

    /// <summary>
    /// 버릴 때 한번 실행해야 합니다.
    /// 버린 후, 남은 아이템이 없다면 true를 반환합니다.
    /// </summary>
    /// <param name="player"></param>
    /// <param name="quantity"></param>
    /// <returns></returns>
    public virtual bool OnDiscard(CPlayer player, int quantity) { return true; }

    public static CItem MakeItem(int id)
    {
        if (!CItemSO.IsBuilded) CItemSO.Build();
        CItemSO targetSO = CItemSO.Find(id);
        if (targetSO is CItemConsumableSO consumableSO)
        {
            CItemConsumable targetConsumable = new CItemConsumable(consumableSO);
            return targetConsumable;
        }
        else if (targetSO is CItemEquipmentSO equipmentSO)
        {
            CItemEquipment targetEquipment = new CItemEquipment(equipmentSO);
            return targetEquipment;
        }
        else
        {
            CLogger.Error("CItem.MakeItem에서 오류남");
            return null;
        }
    }
}
