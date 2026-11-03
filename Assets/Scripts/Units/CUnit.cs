using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct SDamageInfo
{
    public float damage;
    // public CSkill skill;
    public bool isFixed;
    public ERatioSwitch ratioSwitch;
    public bool enableCritical;
    public List<CStatusSO> stateOnHit;
    public float armorPenetration;
    public bool forceCritical;

    public SDamageInfo(float damage, bool isFixed, ERatioSwitch ratioSwitch, bool enableCritical, List<CStatusSO> stateOnHit
        ,float armorPenetration = 0f, bool forceCritical = false)
    {
        this.damage = damage;
        this.isFixed = isFixed;
        this.ratioSwitch = ratioSwitch;
        this.enableCritical = enableCritical;
        this.stateOnHit = stateOnHit;
        this.armorPenetration = armorPenetration;
        this.forceCritical = forceCritical;
    }

    public SDamageInfo (float damage, bool isFixed, bool enableCritical, List<CStatusSO> stateOnHit
        ,float armorPenetration = 0f, bool forceCritical = false)
    {
        this.damage = damage;
        this.isFixed = isFixed;
        this.ratioSwitch = ERatioSwitch.Constant;
        this.enableCritical = enableCritical;
        this.stateOnHit = stateOnHit;
        this.armorPenetration = armorPenetration;
        this.forceCritical = forceCritical;
    }

    public SDamageInfo(float damage, ERatioSwitch ratioSwitch = ERatioSwitch.Constant)
        : this(damage, false, ratioSwitch, true, null, 0f, false) { }
    public SDamageInfo(float damage, bool isFixed, ERatioSwitch ratioSwitch = ERatioSwitch.Constant)
        : this(damage, isFixed, ratioSwitch, true, null, 0f, false) { }
    public SDamageInfo(float damage, bool isFixed, List<CStatusSO> stateOnHit)
    : this(damage, isFixed, true, stateOnHit, 0f, false) { }
}

public enum ERatioSwitch
{
    Constant,   // 고정치
    ByMax,      // 최대치 비례
    ByCurrent,  // 현재치 비례
    ByLose      // 잃은수치 비례
}

public abstract class CUnit : MonoBehaviour
{
    #region 인스펙터
    [Header("기본 스텟")]
    [SerializeField] protected float _baseHpMax;
    [SerializeField] protected float _baseAttack;
    [SerializeField] protected float _baseDefence;
    protected float _baseCritRate = 0.05f;
    protected float _baseCritDmg = 1.5f;
    #endregion

    #region 내부 변수
    protected float _hpCurrent = 1;
    protected float _hpMax = 1;
    protected float _attack = 0;
    protected float _defence = 0;

    protected float _critRate = 0.05f;
    protected float _critDmg = 1.5f;

    protected float _movementMultiply = 1;
    protected float _attackSpeedMultiply = 1;

    protected List<CStatus> _status;
    #endregion

    #region 프로퍼티
    public float HpCurrent => _hpCurrent;
    public float HpMax => _hpMax;
    public float Attack => _attack;
    public float Defence => _defence;

    public float CriticalRate => _critRate;
    public float CriticalDamage => _critDmg;

    public bool IsStunned
    {
        get
        {
            bool temp = false;
            for(int i = 0; i < _status.Count; i++)
            {
                if (_status[i].Trigger.HasFlag(EStatusTrigger.IsStun)) temp = true;
            }
            return temp;
        }
    }

    public IReadOnlyList<CStatus> Status => _status;
    #endregion

    protected virtual void Awake()
    {
        _hpMax = _baseHpMax;
        _hpCurrent = _hpMax;
        _attack = _baseAttack;
        _defence = _baseDefence;
        _status = new List<CStatus>();
    }

    protected virtual void Update()
    {
        InvokeOnDelta(Time.deltaTime);
    }

    public virtual void ResyncStatus()
    {
        List<SStatusOnApply> effects = new List<SStatusOnApply>();
        
        for (int i = 0; i < _status.Count; i++)
        {
            if (_status[i].Trigger.HasFlag(EStatusTrigger.OnApply))
            {
                for (int j = 0; j < _status[i].OnApply.Count; j++)
                {
                    SStatusOnApply targetEffect = _status[i].OnApply[j];
                    if (targetEffect.applyStack)
                    {
                        if (targetEffect.isConstant) targetEffect.value *= _status[i].Stack;
                        else targetEffect.value = 1f + ((targetEffect.value - 1f) * _status[i].Stack);
                        targetEffect.applyStack = false;
                    }
                    effects.Add(targetEffect);
                }
            }
        }

        Dictionary<EStatusStats, float> statConstant = new Dictionary<EStatusStats, float>();
        Dictionary<EStatusStats, float> statMultiple = new Dictionary<EStatusStats, float>();
        Dictionary<EStatusStats, float> statDivide = new Dictionary<EStatusStats, float>();
        foreach (EStatusStats stat in Enum.GetValues(typeof(EStatusStats)))
        {
            statConstant.Add(stat, 0f);
            statMultiple.Add(stat, 1f);
            statDivide.Add(stat, 1f);
        }

        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i].isConstant)
            {
                statConstant[effects[i].target] += effects[i].value;
            }
            else
            {
                if (effects[i].value < 1f)
                {
                    statDivide[effects[i].target] *= effects[i].value;
                }
                else
                {
                    statMultiple[effects[i].target] += effects[i].value - 1f;
                }
            }
        }



        _hpMax = (_baseHpMax + statConstant[EStatusStats.Hp])
            * statMultiple[EStatusStats.Hp] * statDivide[EStatusStats.Hp];
        _hpCurrent = Mathf.Min(_hpCurrent, _hpMax);
        _attack = (_baseAttack + statConstant[EStatusStats.Attack])
            * statMultiple[EStatusStats.Attack] * statDivide[EStatusStats.Attack];
        _defence = (_baseDefence + statConstant[EStatusStats.Defence])
            * statMultiple[EStatusStats.Defence] * statDivide[EStatusStats.Defence];
        _critRate = (0.05f + statConstant[EStatusStats.CritRate])
            * statMultiple[EStatusStats.CritRate] * statDivide[EStatusStats.CritRate];
        _critDmg = (1.5f + statConstant[EStatusStats.CritDmg])
            * statMultiple[EStatusStats.CritDmg] * statDivide[EStatusStats.CritDmg];
        _movementMultiply = (1f + statConstant[EStatusStats.Movement])
            * statMultiple[EStatusStats.Movement] * statDivide[EStatusStats.Movement];
        _attackSpeedMultiply = (1f + statConstant[EStatusStats.AttackSpeed])
            * statMultiple[EStatusStats.AttackSpeed] * statDivide[EStatusStats.AttackSpeed];
    }

    public virtual void InvokeOnDelta(float t)
    {
        if (_status == null || _status.Count == 0) return;
        for (int i = _status.Count - 1; i >= 0; i--)
        {
            if (_status[i].Trigger.HasFlag(EStatusTrigger.OnDelta))
            {
                for (int j = 0; j < _status[i].OnDelta.Count; j++)
                {
                    _status[i].OnDelta[j](this, t);
                }
            }
            _status[i].OnDuration(this, t);
        }
    }

    public virtual void InvokeOnBattleStart(bool isBossBattle)
    {
        if (_status == null || _status.Count == 0) return;
        for (int i = _status.Count - 1; i >= 0; i--)
        {
            if (_status[i].Trigger.HasFlag(EStatusTrigger.OnBattleStart))
            {
                for (int j = 0; j < _status[i].OnBattleStart.Count; j++)
                {
                    _status[i].OnBattleStart[j](this, isBossBattle);
                }
            }
        }
    }

    public virtual void InvokeOnBattleEnd(bool isBossBattle)
    {
        if (_status == null || _status.Count == 0) return;
        for (int i = _status.Count - 1; i >= 0; i--)
        {
            if (_status[i].Trigger.HasFlag(EStatusTrigger.OnBattleEnd))
            {
                for (int j = 0; j < _status[i].OnBattleEnd.Count; j++)
                {
                    _status[i].OnBattleEnd[j](this, isBossBattle);
                }
            }
        }
    }

    /// <summary>
    /// 객체가 공격을 받을 때 호출이 되는 함수입니다.]
    /// 일반적으로 defender는 this을 쓰는것도 괜찮습니다.
    /// </summary>
    /// <param name="attacker"></param>
    /// <param name="defender"></param>
    /// <param name="damage"></param>
    public virtual void OnHit(CUnit attacker, CUnit defender, SDamageInfo damage)
    {
        float finalDamage = DamageCalculate(attacker, defender, damage);
        defender.GetDamage(finalDamage);
        if (damage.stateOnHit != null && damage.stateOnHit.Count > 0 && finalDamage > 0)
        {
            for (int i = 0; i < damage.stateOnHit.Count; i++)
            {
                defender.AddStatus(damage.stateOnHit[i].Id);
            }
        }
        AfterHit(attacker, defender);
    }

    /// <summary>
    /// 객체가 공격을 받을 때 호출이 되는 함수입니다.]
    /// </summary>
    /// <param name="attacker"></param>
    /// <param name="defender"></param>
    /// <param name="damage"></param>
    public virtual void OnHit(CUnit attacker, SDamageInfo damage)
    {
        OnHit(attacker, this, damage);
    }

    protected virtual float DamageCalculate(CUnit attacker, CUnit defender, SDamageInfo damage, out bool isCritical)
    {
        isCritical = false;
        float finalDamage = DamageRatioCaculate(attacker, defender, damage);
        if (damage.forceCritical)
        {
            finalDamage = finalDamage * attacker._critDmg;
            isCritical = true;
        }
        else
        {
            if (damage.enableCritical)
            {
                finalDamage = DamageCriticalCalculate(attacker, finalDamage, out isCritical);
            }
        }
        if (damage.isFixed)
        {   
            return damage.damage;
        }

        finalDamage = finalDamage * (100f / (100f + Mathf.Max((defender.Defence * (1 - damage.armorPenetration)), -99.9f)));

        List<CStatus> attackerStatus = new List<CStatus>();
        List<CStatus> defenderStatus = new List<CStatus>();

        if (attacker is CPlayer attackPlayer)
        {
            attackerStatus.AddRange(attackPlayer.EquipmentStatus);
        }
        attackerStatus.AddRange(attacker.Status);
        if (defender is CPlayer defendPlayer)
        {
            defenderStatus.AddRange(defendPlayer.EquipmentStatus);
        }
        defenderStatus.AddRange(defender.Status);

        for (int i = 0; i < attackerStatus.Count; i++)
        {
            if (attackerStatus[i].Trigger.HasFlag(EStatusTrigger.OnAttack))
            {
                for (int j = 0; j < attackerStatus[i].OnAttack.Count; j++)
                {
                    finalDamage = attackerStatus[i].OnAttack[j](attacker, defender, finalDamage, isCritical);
                }
            }
        }
        for (int i = 0; i < defenderStatus.Count; i++)
        {
            if (defenderStatus[i].Trigger.HasFlag(EStatusTrigger.OnDefence))
            {
                for (int j = 0; j < defenderStatus[i].OnDefence.Count; j++)
                {
                    finalDamage = defenderStatus[i].OnDefence[j](attacker, defender, finalDamage, isCritical);
                }
            }
        }
        for (int i = 0; i < defenderStatus.Count; i++)
        {
            if (defenderStatus[i].Trigger.HasFlag(EStatusTrigger.OnDamage))
            {
                for (int j = 0; j < defenderStatus[i].OnDamage.Count; j++)
                {
                    finalDamage = defenderStatus[i].OnDamage[j](attacker, defender, finalDamage);
                }
            }
        }
        for (int i = 0; i < attackerStatus.Count; i++)
        {
            if (attackerStatus[i].Trigger.HasFlag(EStatusTrigger.OnHit))
            {
                for (int j = 0; j < attackerStatus[i].OnHit.Count; j++)
                {
                    finalDamage = attackerStatus[i].OnHit[j](attacker, defender, finalDamage);
                }
            }
        }

        return finalDamage;
    }

    protected virtual float DamageCalculate(CUnit attacker, CUnit defender, SDamageInfo damage)
    {
        return DamageCalculate(attacker, defender, damage, out bool temp);
    }

    protected virtual float DamageCalculate(CUnit attacker, SDamageInfo damage, out bool isCritical)
    {
        return DamageCalculate(attacker, this, damage, out isCritical);
    }

    protected virtual float DamageCalculate(CUnit attacker, SDamageInfo damage)
    {
        return DamageCalculate(attacker, this, damage);
    }

    protected float DamageRatioCaculate(CUnit attacker, CUnit defender, SDamageInfo damage)
    {
        switch (damage.ratioSwitch)
        {
            case ERatioSwitch.Constant:
                return damage.damage;
            case ERatioSwitch.ByMax:
                return defender.HpMax * damage.damage;
            case ERatioSwitch.ByCurrent:
                return defender.HpCurrent * damage.damage;
            case ERatioSwitch.ByLose:
                return (defender.HpMax - defender.HpCurrent) * damage.damage;
            default:
                return damage.damage;
        }
    }
    protected float DamageCriticalCalculate(CUnit attacker, float damage, out bool isCritical)
    {
        isCritical = false;
        if (UnityEngine.Random.value < attacker._critRate)
        {
            isCritical = true;
            return damage * attacker._critDmg;
        }
        return damage;
    }

    protected virtual void AfterHit(CUnit attacker, CUnit defender)
    {

    }

    public virtual void GetDamage(float value, ERatioSwitch ratioSwitch = ERatioSwitch.Constant)
    {
        switch (ratioSwitch)
        {
            case ERatioSwitch.Constant:
                _hpCurrent -= value;
                break;
            case ERatioSwitch.ByMax:
                _hpCurrent -= (_hpMax * value);
                break;
            case ERatioSwitch.ByCurrent:
                _hpCurrent -= (_hpCurrent * value);
                break;
            case ERatioSwitch.ByLose:
                _hpCurrent -= ((_hpMax - _hpCurrent) * value);
                break;
        }
        _hpCurrent = Mathf.Min(_hpCurrent, _hpMax);
        _hpCurrent = Mathf.Max(_hpCurrent, 0f);

        if (_hpCurrent <= 0f)
        {
            Die();
        }
    }

    protected virtual void Die()
    {

    }

    public virtual void GetHeal(float value, ERatioSwitch ratioSwitch = ERatioSwitch.Constant)
    {
        switch (ratioSwitch)
        {
            case ERatioSwitch.Constant:
                _hpCurrent += value;
                break;
            case ERatioSwitch.ByMax:
                _hpCurrent += (_hpMax * value);
                break;
            case ERatioSwitch.ByCurrent:
                _hpCurrent += (_hpCurrent * value);
                break;
            case ERatioSwitch.ByLose:
                _hpCurrent += ((_hpMax - _hpCurrent) * value);
                break;
        }
        _hpCurrent = Mathf.Min(_hpCurrent, _hpMax);
        _hpCurrent = Mathf.Max(_hpCurrent, 0f);

        if (_hpCurrent <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// 상태이상을 id로 찾아서, 만들어서 부여합니다.
    /// </summary>
    /// <param name="id"></param>
    public void AddStatus(int id, float duration = -1, int stack = 1)
    {
        CStatus status = CStatus.MakeStatus(id, duration, stack);
        if (FindStatusStack(id) <= 0)
        {
            _status.Add(status);
            if (status.Trigger.HasFlag(EStatusTrigger.OnApply))
            {
                ResyncStatus();
            }
            return;
        }
        CStatus currentStatus = FindStatus(id);
        switch (status.StackSwitch)
        {
            case EStatusStack.StackAndRefresh:
                currentStatus.Stack += Mathf.Max(0, stack);
                if (currentStatus.Stack > currentStatus.MaxStack)
                {
                    currentStatus.Stack = currentStatus.MaxStack;
                }
                currentStatus.ResetTimer(duration);
                currentStatus.IsStackRemoving = false;
                if (status.Trigger.HasFlag(EStatusTrigger.OnApply))
                {
                    ResyncStatus();
                }
                break;
            case EStatusStack.StackIndividual:
                _status.Add(status);
                if (status.Trigger.HasFlag(EStatusTrigger.OnApply))
                {
                    ResyncStatus();
                }
                break;
            case EStatusStack.Refresh:
                currentStatus.ResetTimer(duration);
                currentStatus.IsStackRemoving = false;
                break;
            case EStatusStack.Ignore:
                break;
        }
    }

    /// <summary>
    /// 이미 선언되어있는 상태이상을 부여합니다.
    /// </summary>
    /// <param name="status"></param>
    public void AddStatus(CStatus status)
    {
        AddStatus(status.Id, status.Duration, status.Stack);
    }

    public int FindStatusStack(int id)
    {
        if (_status == null || _status.Count == 0) return 0;
        for (int i = 0; i < _status.Count; i++)
        {
            if (_status[i].Id == id) return _status[i].Stack;
        }
        return 0;
    }

    public CStatus FindStatus(int id)
    {
        if (_status == null || _status.Count == 0) return null;

        for(int i = 0; i < _status.Count; i++)
        {
            if (_status[i].Id == id) return _status[i];
        }
        return null;
    }

    /// <summary>
    /// id로 상태이상을 찾아서 삭제합니다. removeAll이 true라면 전부 삭제합니다. 제일 최근의 상태이상을 찾아 삭제합니다.
    /// 삭제한게 없다면 -1을 반환합니다. 삭제했다면, 그 상태이상의 스택수를 반환합니다.
    /// 여러개를 삭제했다면 가장 큰 스택수를 반환합니다.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="removeAll"></param>
    /// <returns></returns>
    public int RemoveStatus(int id, bool removeAll = true)
    {
        if (_status == null || _status.Count == 0) return -1;
        int temp = -1;

        for (int i = _status.Count - 1; i >= 0; i--)
        {
            if ( _status[i].Id == id)
            {
                if (temp < _status[i].Stack) temp = _status[i].Stack;
                //if (_status[i].Trigger.HasFlag(EStatusTrigger.OnRemove))
                //{
                //    for (int j = 0; j < _status[i].OnRemove.Count; j++)
                //    {
                //        _status[i].OnRemove[j](this);
                //    }
                //}
                _status.Remove(_status[i]);
                ResyncStatus();
                if (!removeAll) return temp;
            }
        }
        return temp;
    }
    /// <summary>
    /// 상태이상을 삭제합니다.
    /// 삭제한게 없다면 -1을 반환합니다. 삭제했다면, 그 상태이상의 스택수를 반환합니다.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="removeAll"></param>
    /// <returns></returns>
    public int RemoveStatus(CStatus status)
    {
        if (!_status.Contains(status)) return -1;
        int temp = status.Stack;
        //if (status.Trigger.HasFlag(EStatusTrigger.OnRemove))
        //{
        //    for (int i = 0; i < status.OnRemove.Count; i++)
        //    {
        //        status.OnRemove[i](this);
        //    }
        //}
        _status.Remove(status);
        ResyncStatus();
        return temp;
    }
}