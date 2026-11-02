using System;
using Unity.VisualScripting;

public partial class CStatus
{
    public void Setup(int id, float duration = -1, int stack = 1)
    {
        CStatusSO targetSO = CStatusSO.Find(id);
        _id = targetSO.Id;
        if (duration == 0)
        {
            _initialTimer = 0;
            _isPermanent = true;
        }
        else if (duration < 0)
        {
            if (targetSO.Duration <= 0)
            {
                _initialTimer = 0;
                _isPermanent = true;
            }
            else
            {
                _initialTimer = targetSO.Duration;
                _isPermanent = false;
            }
        }
        else
        {
            _initialTimer = duration;
            _isPermanent = false;
        }
        _duration = _initialTimer;
        _name = targetSO.Name;
        _description = targetSO.Description;
        _tooltip = targetSO.Tooltip;
        _image = targetSO.Icon;
        _stack = 1;
        _stackSwitch = targetSO.Stack;
        _removeSwitch = targetSO.Remove;
        _removeTimer = targetSO.StackDuration;
        _trigger = EStatusTrigger.None;

        if (targetSO.ActionList == null || targetSO.ActionList.Count == 0) return;
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
                            new SStatusOnApply(EStatusStats.Hp, param, false)
                            );
                    break;
                case EStatusAction.MultiplyAttack:
                    _trigger |= EStatusTrigger.OnApply;
                    _onApply.Add(
                            new SStatusOnApply(EStatusStats.Attack, param, false)
                            );
                    break;
                case EStatusAction.MultiplyDefence:
                    _trigger |= EStatusTrigger.OnApply;
                    _onApply.Add(
                            new SStatusOnApply(EStatusStats.Defence, param, false)
                            );
                    break;
                case EStatusAction.AddCriticalRate:
                    _trigger |= EStatusTrigger.OnApply;
                    _onApply.Add(
                            new SStatusOnApply(EStatusStats.CritRate, param, true)
                            );
                    break;
                case EStatusAction.MultiplyCriticalRate:
                    _trigger |= EStatusTrigger.OnApply;
                    _onApply.Add(
                            new SStatusOnApply(EStatusStats.CritRate, param, false)
                            );
                    break;
                case EStatusAction.MultiplyCriticalDmg:
                    _trigger |= EStatusTrigger.OnApply;
                    _onApply.Add(
                            new SStatusOnApply(EStatusStats.CritDmg, param, false)
                            );
                    break;
                case EStatusAction.MultiplyAttackerDamage:
                    _trigger |= EStatusTrigger.OnAttack;
                    _onAttack.Add((CUnit attacker, CUnit defender, float damage) =>
                    {
                        return damage * param;
                    });
                    break;
                case EStatusAction.MultiplyDefenderDamage:
                    _trigger |= EStatusTrigger.OnDefence;
                    _onDefence.Add((CUnit attacker, CUnit defender, float damage) =>
                    {
                        return damage * param;
                    });
                    break;
                case EStatusAction.MultiplyMovement:
                    _trigger |= EStatusTrigger.OnApply;
                    _onApply.Add(
                            new SStatusOnApply(EStatusStats.Movement, param, false)
                            );
                    break;
                case EStatusAction.Stun:
                    _trigger |= EStatusTrigger.IsStun;
                    break;
                case EStatusAction.DamageOverTime:
                    _trigger |= EStatusTrigger.OnDelta;
                    _onDelta.Add((CUnit unit, float deltaTime) =>
                    {
                        unit.GetDamage(param * deltaTime, ERatioSwitch.Constant);
                    });
                    break;
                case EStatusAction.DamageOverTimeByRatio:
                    _trigger |= EStatusTrigger.OnDelta;
                    _onDelta.Add((CUnit unit, float deltaTime) =>
                    {
                        unit.GetDamage(param * deltaTime, ERatioSwitch.ByMax);
                    });
                    break;
                case EStatusAction.MultiplyAttackSpeed:
                    _trigger |= EStatusTrigger.OnApply;
                    _onApply.Add(
                            new SStatusOnApply(EStatusStats.AttackSpeed, param, false)
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
            case 0:
                break;
            case 1:
                break;
            case 2:
                break;
            case 3:
                break;
            case 4:
                break;
            default:
                CLogger.Error($"CStatus : 알 수 없는 커스텀 상태이상 {customStatus}");
                break;
        }
    }
}