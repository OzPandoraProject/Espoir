using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum EEquipmentAction
{
    None,
    [InspectorName("체력 상수 증가")]
    IncreaseHpMax,
    [InspectorName("체력 비율 변화")]
    MultiplyHpMax,
    [InspectorName("공격력 상수 증가")]
    IncreaseAttack,
    [InspectorName("공격력 비율 변화")]
    MultiplyAttack,
    [InspectorName("방어력 상수 증가")]
    IncreaseDefence,
    [InspectorName("방어력 비율 변화")]
    MultiplyDefence,
    [InspectorName("치명타율 상수 증가")]
    IncreaseCritRate,
    [InspectorName("치명타율 비율 변화")]
    MultiplyCritRate,
    [InspectorName("치명타피해율 상수 증가")]
    IncreaseCritDmg,
    [InspectorName("치명타피해율 비율 변화")]
    MultiplyCritDmg,
}

[Serializable]
public struct SEquipmentAction
{
    public EEquipmentAction action;
    [Tooltip("비율은 증가시 1.xx 감소시 0.xx로!")]
    public float param;
}

public enum EEquipmentSlot
{
    [InspectorName("목걸이")]
    Necklace,
    [InspectorName("귀걸이")]
    Earring,
    [InspectorName("팔찌")]
    Bracelet,
    [InspectorName("반지")]
    Ring,
}

public class CItemEquipment : CItem
{
    private EEquipmentSlot _slot;
    private int _setNumber;
    private List<Action<CPlayer>> _onEarlyCalculate;
    private List<Action<CPlayer>> _onLateCalculate;
    private List<CStatus> _equipmentStatus;
    private bool _isEquip;

    public override bool AvailableInLobby() => true;
    public override bool AvailableInBattle() => false;
    public EEquipmentSlot Slot => _slot;
    public int SetNumber => _setNumber;

    public bool IsEquip
    {
        get => _isEquip;
    }
    public List<CStatus> EquipmentStatus
    {
        get => _equipmentStatus;
    }

    public override int Quantity
    {
        get { return 1; }
        set { value = 1; }
    }

    public CItemEquipment(CItemEquipmentSO targetSO)
    {
        Setup(targetSO);
    }

    public bool OnEquip(CPlayer player)
    {
        if (_isEquip)
        {
            CLogger.Error("이미 장착되었는데, 장착기능에 접근함. 코드구조 확인!");
        }
        if (player.Equipment[this.Slot] != null)
        {
            CLogger.Error("슬롯에 다른 아이템이 있는데, 장착기능에 접근함. 장착취소. 코드구조 확인!");
            return false;
        }

        player.Equipment[this.Slot] = this;
        _isEquip = true;
        return true;
    }

    public bool OnUnequip(CPlayer player)
    {
        if (!_isEquip)
        {
            CLogger.Error("이미 해제되었는데, 해제기능에 접근함. 코드구조 확인!");
        }
        if (player.Equipment[this.Slot] == this)
        {
            player.Equipment[this.Slot] = null;
            _isEquip = false;
            return true;
        }

        CLogger.Error("플레이어에게 없는데, 해제기능에 접근함. 코드구조 확인!");
        return false;
    }

    public override bool OnDiscard(CPlayer player, int quantity)
    {
        if (_isEquip) OnUnequip(player);
        return true;
    }

    public void OnEarlySync(CPlayer player)
    {
        if (_onEarlyCalculate == null || _onEarlyCalculate.Count == 0) return;

        for(int i = 0; i < _onEarlyCalculate.Count; i++)
        {
            _onEarlyCalculate[i](player);
        }
    }

    public void OnLateSync(CPlayer player)
    {
        if (_onLateCalculate == null || _onLateCalculate.Count == 0) return;

        for (int i = 0; i < _onLateCalculate.Count; i++)
        {
            _onLateCalculate[i](player);
        }
    }

    public void OnStatusSync(CPlayer player)
    {
        if (_equipmentStatus == null || _equipmentStatus.Count == 0) return;

        for (int i = 0; i < _equipmentStatus.Count; i++)
        {
            player.EquipmentStatus.Add(_equipmentStatus[i]);
        }
    }

    public override bool Setup(CItemSO targetItem)
    {
        if (targetItem is CItemEquipmentSO targetEquipment)
        {
            this._originItem = targetEquipment;
            this._id = targetEquipment.Id;
            this._name = targetEquipment.Name;
            this._slot = targetEquipment.Slot;
            this._setNumber = targetEquipment.SetNumber;
            this._itemIcon = targetEquipment.ItemIcon;
            this._description = targetEquipment.Description;

            _onEarlyCalculate = new List<Action<CPlayer>>();
            _onLateCalculate = new List<Action<CPlayer>>();
            _equipmentStatus = new List<CStatus>();

            for (int i = 0; i < targetEquipment.Action.Count; i++)
            {
                EEquipmentAction action = targetEquipment.Action[i].action;
                float param = targetEquipment.Action[i].param;

                switch (action)
                {
                    case EEquipmentAction.None:
                        break;
                    case EEquipmentAction.IncreaseHpMax:
                        _onEarlyCalculate.Add(
                            (CPlayer player) => player.HpMaxBaseline += param
                            );
                        break;
                    case EEquipmentAction.MultiplyHpMax:
                         _onLateCalculate.Add(
                            (CPlayer player) => player.HpMaxBaseline *= param
                            );
                        break;
                    case EEquipmentAction.IncreaseAttack:
                        _onEarlyCalculate.Add(
                            (CPlayer player) => player.AttackBaseline += param
                            );
                        break;
                    case EEquipmentAction.MultiplyAttack:
                         _onLateCalculate.Add(
                            (CPlayer player) => player.AttackBaseline *= param
                            );
                        break;
                    case EEquipmentAction.IncreaseDefence:
                        _onEarlyCalculate.Add(
                            (CPlayer player) => player.DefenceBaseline += param
                            );
                        break;
                    case EEquipmentAction.MultiplyDefence:
                        _onLateCalculate.Add(
                            (CPlayer player) => player.DefenceBaseline *= param
                            );
                        break;
                    case EEquipmentAction.IncreaseCritRate:
                        _onEarlyCalculate.Add(
                            (CPlayer player) => player.CritRateBaseline += param
                            );
                        break;
                    case EEquipmentAction.MultiplyCritRate:
                        _onLateCalculate.Add(
                            (CPlayer player) => player.CritRateBaseline *= param
                            );
                        break;
                    case EEquipmentAction.IncreaseCritDmg:
                        _onEarlyCalculate.Add(
                            (CPlayer player) => player.CritDmgBaseline += param
                            );
                        break;
                    case EEquipmentAction.MultiplyCritDmg:
                        _onLateCalculate.Add(
                            (CPlayer player) => player.CritDmgBaseline *= param
                            );
                        break;
                }
            }

            for (int i = 0; i < targetEquipment.EquipmentStatus.Count; i++)
            {
                _equipmentStatus.Add(CStatus.MakeStatus(targetEquipment.EquipmentStatus[i]));
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
