using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CSet
{
    protected CSetSO _originSet;
    protected int _id;
    protected string _name;
    protected string _description;

    private List<Action<CPlayer>> _onEarlyCalculate;
    private List<Action<CPlayer>> _onLateCalculate;
    private List<CStatus> _equipmentStatus;

    public CSetSO OriginSet => _originSet;
    public int Id => _id;
    public string Name => _name;
    public string Description => _description;

    public CSet(CSetSO originSet, int setCount)
    {
        Setup(originSet, setCount);
    }

    public static CSet MakeSet(int id, int setCount)
    {
        if (!CSetSO.IsBuilded) CSetSO.Build();
        CSetSO targetSO = CSetSO.Find(id);
        return new CSet(targetSO, setCount);
    }

    public void OnEarlySync(CPlayer player)
    {
        if (_onEarlyCalculate == null || _onEarlyCalculate.Count == 0) return;

        for (int i = 0; i < _onEarlyCalculate.Count; i++)
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

    public bool Setup(CSetSO targetSet, int setCount)
    {
        this._originSet = targetSet;
        this._id = targetSet.Id;
        this._name = targetSet.Name;
        this._description = targetSet.Description;

        _onEarlyCalculate = new List<Action<CPlayer>>();
        _onLateCalculate = new List<Action<CPlayer>>();
        _equipmentStatus = new List<CStatus>();

        if (setCount >= 2)
        {
            for (int i = 0; i < targetSet.ActionSet2.Count; i++)
            {
                EEquipmentAction action = targetSet.ActionSet2[i].action;
                float param = targetSet.ActionSet2[i].param;

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
            for (int i = 0; i < targetSet.EquipmentStatusSet2.Count; i++)
            {
                _equipmentStatus.Add(CStatus.MakeStatus(targetSet.EquipmentStatusSet2[i]));
            }
        }

        if (setCount >= 3)
        {
            for (int i = 0; i < targetSet.ActionSet3.Count; i++)
            {
                EEquipmentAction action = targetSet.ActionSet3[i].action;
                float param = targetSet.ActionSet3[i].param;

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

            for (int i = 0; i < targetSet.EquipmentStatusSet3.Count; i++)
            {
                _equipmentStatus.Add(CStatus.MakeStatus(targetSet.EquipmentStatusSet3[i]));
            }
        }

        if (setCount >= 4)
        {
            for (int i = 0; i < targetSet.ActionSet4.Count; i++)
            {
                EEquipmentAction action = targetSet.ActionSet4[i].action;
                float param = targetSet.ActionSet4[i].param;

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

            for (int i = 0; i < targetSet.EquipmentStatusSet4.Count; i++)
            {
                _equipmentStatus.Add(CStatus.MakeStatus(targetSet.EquipmentStatusSet4[i]));
            }
        }

        return true;
    }
}
