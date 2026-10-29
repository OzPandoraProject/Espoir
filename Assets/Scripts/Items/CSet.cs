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

    private List<SStatusOnApply> _onCalculate;
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

    public List<SStatusOnApply> OnSync()
    {
        return _onCalculate;
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

        _onCalculate = new List<SStatusOnApply>();
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
                        _onCalculate.Add(
                            new SStatusOnApply(EStatusStats.Hp, param, true)
                            );
                        break;
                    case EEquipmentAction.MultiplyHpMax:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.Hp, param, false)
                           );
                        break;
                    case EEquipmentAction.IncreaseAttack:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.Attack, param, true)
                           );
                        break;
                    case EEquipmentAction.MultiplyAttack:
                        _onCalculate.Add(
                          new SStatusOnApply(EStatusStats.Attack, param, false)
                          );
                        break;
                    case EEquipmentAction.IncreaseDefence:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.Defence, param, true)
                           );
                        break;
                    case EEquipmentAction.MultiplyDefence:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.Defence, param, false)
                           );
                        break;
                    case EEquipmentAction.IncreaseCritRate:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.CritRate, param, true)
                           );
                        break;
                    case EEquipmentAction.MultiplyCritRate:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.CritRate, param, false)
                           );
                        break;
                    case EEquipmentAction.IncreaseCritDmg:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.CritDmg, param, true)
                           );
                        break;
                    case EEquipmentAction.MultiplyCritDmg:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.CritDmg, param, false)
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
                        _onCalculate.Add(
                            new SStatusOnApply(EStatusStats.Hp, param, true)
                            );
                        break;
                    case EEquipmentAction.MultiplyHpMax:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.Hp, param, false)
                           );
                        break;
                    case EEquipmentAction.IncreaseAttack:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.Attack, param, true)
                           );
                        break;
                    case EEquipmentAction.MultiplyAttack:
                        _onCalculate.Add(
                          new SStatusOnApply(EStatusStats.Attack, param, false)
                          );
                        break;
                    case EEquipmentAction.IncreaseDefence:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.Defence, param, true)
                           );
                        break;
                    case EEquipmentAction.MultiplyDefence:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.Defence, param, false)
                           );
                        break;
                    case EEquipmentAction.IncreaseCritRate:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.CritRate, param, true)
                           );
                        break;
                    case EEquipmentAction.MultiplyCritRate:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.CritRate, param, false)
                           );
                        break;
                    case EEquipmentAction.IncreaseCritDmg:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.CritDmg, param, true)
                           );
                        break;
                    case EEquipmentAction.MultiplyCritDmg:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.CritDmg, param, false)
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
                        _onCalculate.Add(
                            new SStatusOnApply(EStatusStats.Hp, param, true)
                            );
                        break;
                    case EEquipmentAction.MultiplyHpMax:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.Hp, param, false)
                           );
                        break;
                    case EEquipmentAction.IncreaseAttack:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.Attack, param, true)
                           );
                        break;
                    case EEquipmentAction.MultiplyAttack:
                        _onCalculate.Add(
                          new SStatusOnApply(EStatusStats.Attack, param, false)
                          );
                        break;
                    case EEquipmentAction.IncreaseDefence:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.Defence, param, true)
                           );
                        break;
                    case EEquipmentAction.MultiplyDefence:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.Defence, param, false)
                           );
                        break;
                    case EEquipmentAction.IncreaseCritRate:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.CritRate, param, true)
                           );
                        break;
                    case EEquipmentAction.MultiplyCritRate:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.CritRate, param, false)
                           );
                        break;
                    case EEquipmentAction.IncreaseCritDmg:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.CritDmg, param, true)
                           );
                        break;
                    case EEquipmentAction.MultiplyCritDmg:
                        _onCalculate.Add(
                           new SStatusOnApply(EStatusStats.CritDmg, param, false)
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
