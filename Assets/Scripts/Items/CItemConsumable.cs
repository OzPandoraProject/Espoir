using System;
using System.Collections.Generic;
using UnityEngine;

public enum EConsumableEffect
{
    None,
    [InspectorName("상수 회복")]
    HealConstant,
    [InspectorName("상수 피해")]
    DealConstant,
    [InspectorName("비율 회복")]
    HealRatio,
    [InspectorName("비율 피해")]
    DealRatio,
}

[Serializable]
public class CConsumableAction
{
    public EConsumableEffect effect;
    [Tooltip("비율은 증가시 1.xx 감소시 0.xx로!")]
    public float value;
}

public class CItemConsumable : CItem
{
    List<Action<CUnit>> _onUse;
    List<CStatus> _itemStatus;
    bool _isThrow = false;

    public override bool AvailableInLobby() => false;
    public override bool AvailableInBattle() => true;

    public override int Quantity
    {
        get { return _quantity; }
        set { _quantity = value; }
    }

    public CItemConsumable(CItemConsumableSO targetItem)
    {
        Setup(targetItem);
    }

    public void OnUse(CUnit unit)
    {
        if (_onUse != null && _onUse.Count != 0)
        {
            for (int i = 0; i < _onUse.Count; i++)
            {
                _onUse[i](unit);
            }
        }

        if (_itemStatus != null && _itemStatus.Count != 0)
        {
            for (int i = 0; i < _itemStatus.Count; i++)
            {
                unit.AddStatus(_itemStatus[i]);
            }
        }
            
    }

    public override bool OnDiscard(CPlayer player, int quantity)
    {
        _quantity -= quantity;
        if (_quantity <= 0) return true;
        else return false;
    }

    public override bool Setup(CItemSO targetItem)
    {
        if (targetItem is CItemConsumableSO targetConsumable)
        {
            this._originItem = targetConsumable;
            this._id = targetConsumable.Id;
            this._name = targetConsumable.Name;
            this._itemIcon = targetConsumable.ItemIcon;
            this._description = targetConsumable.Description;
            this._rarity = targetConsumable.Rarity;
            this._isThrow = targetConsumable.IsThrow;

            _onUse = new List<Action<CUnit>>();
            _itemStatus = new List<CStatus>();

            for (int i = 0; i < targetConsumable.Action.Count; i++)
            {
                EConsumableEffect actionData = targetConsumable.Action[i].effect;
                float temp = targetConsumable.Action[i].value;
                switch (actionData)
                {
                    case EConsumableEffect.None:
                        _onUse.Add((CUnit unit) => { });
                        break;
                    case EConsumableEffect.HealConstant:
                        _onUse.Add((CUnit unit) => unit.GetHeal(temp));
                        break;
                    case EConsumableEffect.HealRatio:
                        _onUse.Add((CUnit unit) => unit.GetHeal(temp, ERatioSwitch.ByMax));
                        break;
                    case EConsumableEffect.DealConstant:
                        _onUse.Add((CUnit unit) => unit.GetDamage(temp));
                        break;
                    case EConsumableEffect.DealRatio:
                        _onUse.Add((CUnit unit) => unit.GetDamage(temp, ERatioSwitch.ByMax));
                        break;
                }
            }

            for (int i = 0; i < targetConsumable.ItemStatus.Count; i++)
            {
                _itemStatus.Add(CStatus.MakeStatus(targetConsumable.ItemStatus[i]));
            }

            return true;
        }
        else
        {
            CLogger.Error("CItemConsumable : 소모품이 아닌 아이템이 들어옴!");
            return false;
        }
    }


}
