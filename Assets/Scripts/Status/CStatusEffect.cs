using System;
using Unity.VisualScripting;
using UnityEngine;

public partial class CStatus
{
    public void Setup(int id, float duration = -1, int stack = 1)
    {
        CStatusSO targetSO = CStatusSO.Find(id);
        if (targetSO == null)
        {
            CLogger.Error($"CStatus : 상태이상 {id}를 찾을 수 없습니다.");
            return;
        }
        _id = targetSO.Id;
        if (duration == 0)
        {
            _initialTimer = 0;
            _isPermanent = true;
            _baseTimer = -1;
        }
        else if (duration < 0)
        {
            if (targetSO.Duration <= 0)
            {
                _initialTimer = 0;
                _isPermanent = true;
                _baseTimer = -1;
            }
            else
            {
                _initialTimer = Mathf.Max(0.01f, targetSO.Duration);
                _baseTimer = _initialTimer;
                _isPermanent = false;
            }
        }
        else
        {
            _initialTimer = Mathf.Max(0.01f, duration);
            _baseTimer = _initialTimer;
            _isPermanent = false;
        }
        _duration = _initialTimer;
        _name = targetSO.Name;
        _description = targetSO.Description;
        _tooltip = targetSO.Tooltip;
        _image = targetSO.Icon;
        _stack = stack > 0 ? stack : 1;
        _initialRemoveStack = targetSO.InitialRemoveStack > 0 ? targetSO.InitialRemoveStack : 1;
        _removeStack = targetSO.RemoveStack > 0 ? targetSO.RemoveStack : 1;
        _maxStack = targetSO.MaxStack > 0 ? targetSO.MaxStack : 0b1000000000000000; // 32768
        if (_stack > _maxStack)
        {
            _stack = _maxStack;
        }
        _stackSwitch = targetSO.Stack;
        _removeSwitch = targetSO.Remove;
        _removeTimer = Mathf.Max(0.01f, targetSO.StackDuration);
        _trigger = EStatusTrigger.None;

        if (targetSO.ActionList != null && targetSO.ActionList.Count > 0)
        {
            for (int i = 0; i < targetSO.ActionList.Count; i++)
            {
                SStatusAction targetAction = targetSO.ActionList[i];
                float param = targetAction.param;
                switch (targetAction.action)
                {
                    case EStatusAction.None:
                        break;
                    case EStatusAction.MultiplyHpMax:
                        _trigger |= EStatusTrigger.OnApply;
                        _onApply.Add(
                                new SStatusOnApply(EStatusStats.Hp, param, false, targetAction.applyStack)
                                );
                        break;
                    case EStatusAction.MultiplyAttack:
                        _trigger |= EStatusTrigger.OnApply;
                        _onApply.Add(
                                new SStatusOnApply(EStatusStats.Attack, param, false, targetAction.applyStack)
                                );
                        break;
                    case EStatusAction.MultiplyDefence:
                        _trigger |= EStatusTrigger.OnApply;
                        _onApply.Add(
                                new SStatusOnApply(EStatusStats.Defence, param, false, targetAction.applyStack)
                                );
                        break;
                    case EStatusAction.AddCriticalRate:
                        _trigger |= EStatusTrigger.OnApply;
                        _onApply.Add(
                                new SStatusOnApply(EStatusStats.CritRate, param, true, targetAction.applyStack)
                                );
                        break;
                    case EStatusAction.MultiplyCriticalRate:
                        _trigger |= EStatusTrigger.OnApply;
                        _onApply.Add(
                                new SStatusOnApply(EStatusStats.CritRate, param, false, targetAction.applyStack)
                                );
                        break;
                    case EStatusAction.MultiplyCriticalDmg:
                        _trigger |= EStatusTrigger.OnApply;
                        _onApply.Add(
                                new SStatusOnApply(EStatusStats.CritDmg, param, false, targetAction.applyStack)
                                );
                        break;
                    case EStatusAction.MultiplyAttackerDamage:
                        _trigger |= EStatusTrigger.OnAttack;
                        if (targetAction.applyStack)
                        {
                            _onAttack.Add((CUnit attacker, CUnit defender, float damage, bool isCritical) =>
                            {
                                return Mathf.Max(0, damage * (1f + (param - 1f) * _stack));
                            });
                        }
                        else
                        {
                            _onAttack.Add((CUnit attacker, CUnit defender, float damage, bool isCritical) =>
                            {
                                return Mathf.Max(0, damage * param);
                            });
                        }
                        break;
                    case EStatusAction.MultiplyDefenderDamage:
                        _trigger |= EStatusTrigger.OnDefence;
                        if (targetAction.applyStack)
                        {
                            _onDefence.Add((CUnit attacker, CUnit defender, float damage, bool isCritical) =>
                            {
                                return Mathf.Max(0, damage * (1f + (param - 1f) * _stack));
                            });
                        }
                        else
                        {
                            _onDefence.Add((CUnit attacker, CUnit defender, float damage, bool isCritical) =>
                            {
                                return Mathf.Max(0, damage * param);
                            });
                        }
                        break;
                    case EStatusAction.MultiplyMovement:
                        _trigger |= EStatusTrigger.OnApply;
                        _onApply.Add(
                                new SStatusOnApply(EStatusStats.Movement, param, false, targetAction.applyStack)
                                );
                        break;
                    case EStatusAction.Stun:
                        _trigger |= EStatusTrigger.IsStun;
                        break;
                    case EStatusAction.DamageOverTime:
                        _trigger |= EStatusTrigger.OnDelta;
                        _onDelta.Add((CUnit unit, float deltaTime) =>
                        {
                            unit.GetDamage(param * deltaTime *
                                (targetAction.applyStack ? _stack : 1), ERatioSwitch.Constant);
                        });
                        break;
                    case EStatusAction.DamageOverTimeByRatio:
                        _trigger |= EStatusTrigger.OnDelta;
                        _onDelta.Add((CUnit unit, float deltaTime) =>
                        {
                            unit.GetDamage(param * deltaTime *
                                (targetAction.applyStack ? _stack : 1), ERatioSwitch.ByMax);
                        });
                        break;
                    case EStatusAction.MultiplyAttackSpeed:
                        _trigger |= EStatusTrigger.OnApply;
                        _onApply.Add(
                                new SStatusOnApply(EStatusStats.AttackSpeed, param, false, targetAction.applyStack)
                                );
                        break;
                    case EStatusAction.RemoveWhenBattleOver:
                        _trigger |= EStatusTrigger.OnBattleEnd;
                        _onBattleEnd.Add((CUnit unit, bool onlyBoss) =>
                        {
                            unit.RemoveStatus(this);
                        });
                        break;
                }
            }
        }        

        if (targetSO.CustomStatus > 0)
        {
            SetCustomStatus(targetSO.CustomStatus);
        }

        return;
    }

    private void SetCustomStatus(int customStatus)
    {
        switch (customStatus)
        {
            // 폭주하는 질투
            // 5% 확률로 2배의 피해를 줌.
            case 1:
                _trigger |= EStatusTrigger.OnAttack;
                _onAttack.Add((CUnit attacker, CUnit defender, float damage, bool isCritical) =>
                {
                    if (UnityEngine.Random.value < 0.05f)
                    {
                        return damage * 2f;
                    }
                    else return damage;
                });
                break;
            // 나태의 파편
            // 전투당 한번, 최대체력의 50%를 초과하는 피해를 무효화합니다.
            case 2:
                _trigger |= EStatusTrigger.OnBattleStart | EStatusTrigger.OnDamage;
                _onBattleStart.Add((CUnit unit, bool isBossBattle) =>
                {
                    _boolParam = true;
                });
                _onDamage.Add((CUnit attacker, CUnit defender, float damage) =>
                {
                    if (_boolParam && damage > (defender.HpMax * 0.5))
                    {
                        _boolParam = false;
                        return 0;
                    }
                    else return damage;
                });
                break;
            // 탐욕의 항아리
            // 준 피해의 5%만큼 체력을 회복합니다.
            // 최대 최대체력의 5%
            case 3:
                _trigger |= EStatusTrigger.OnHit;
                _onHit.Add((CUnit attacker, CUnit defender, float damage) =>
                {
                    float healAmount = Mathf.Min(attacker.HpMax * 0.05f, damage * 0.05f);
                    attacker.GetHeal(healAmount, ERatioSwitch.Constant);
                    return damage;
                });
                break;
            // 방랑자의 발걸음
            // 공격을 하지 않는 2초마다, 다음 공격의 피해가 10% 증가합니다. (최대 50%)
            // 공격을 할 경우 피해량 증가가 초기화됩니다.
            case 4:
                _floatParam = 0f;
                _trigger |= EStatusTrigger.OnDelta | EStatusTrigger.OnAttack;
                _onDelta.Add((CUnit unit, float deltaTime) =>
                {
                    _floatParam += deltaTime;
                    if (_floatParam > 12f)
                    {
                        _floatParam = 12f;
                    }
                });
                _onAttack.Add((CUnit attacker, CUnit defender, float damage, bool isCritical) =>
                {
                    float bonusDamage = Mathf.Min(0.1f * Mathf.Floor(_floatParam / 2f), 0.5f);
                    _floatParam = 0f;
                    return damage * (1f + bonusDamage);
                });
                break;
            // 폭식의 아귀
            // 매 초 최대체력의 0.5%만큼 체력을 회복합니다.
            case 5:
                _trigger |= EStatusTrigger.OnDelta;
                _onDelta.Add((CUnit unit, float deltaTime) =>
                {
                    unit.GetHeal(unit.HpMax * 0.005f * deltaTime, ERatioSwitch.Constant);
                });
                break;
            // 분노의 잔재
            // 잃은 체력 2%당 주는 피해 +1%
            case 6:
                _trigger |= EStatusTrigger.OnAttack;
                _onAttack.Add((CUnit attacker, CUnit defender, float damage, bool isCritical) =>
                {
                    float lostHpRatio = 1f - (attacker.HpCurrent / attacker.HpMax);
                    float bonusDamage = lostHpRatio / 2f;
                    return damage * (1f + bonusDamage);
                });
                break;
            default:
                CLogger.Error($"CStatus : 알 수 없는 커스텀 상태이상 {customStatus}");
                break;
        }
    }
}