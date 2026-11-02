using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum EStatusStats
{
    Hp = 0,
    Attack = 1,
    Defence = 2,
    CritRate = 3,
    CritDmg = 4,
    Movement = 5,
    AttackSpeed = 6,
}

public struct SStatusOnApply
{
    public EStatusStats target;
    public float value;
    public bool isConstant;

    public SStatusOnApply(EStatusStats target, float value, bool isConstant)
    {
        this.target = target;
        this.value = value;
        this.isConstant = isConstant;
    }
}

public enum EStatusStack
{
    [InspectorName("스택이 쌓이고, 지속시간이 갱신됨")]
    StackAndRefresh,    // 스택이 쌓이고 지속시간 갱신
    [InspectorName("별개의 상태이상으로 적용")]
    StackIndividual,    // 별개의 상태이상으로 적용
    [InspectorName("지속시간을 갱신함")]
    Refresh,            // 지속시간만 갱신
    [InspectorName("이미 있는 경우 적용안함")]
    Ignore              // 이미 있을경우 적용 안함
}
public enum EStatusRemove
{
    [InspectorName("스택에 상관없이, 시간이 지나면 사라짐")]
    RemoveAll,
    [InspectorName("스택이 하나씩 사라짐")]
    RemoveOne,
    [InspectorName("별개의 스택제거 시간이 있음")]
    RemoveFor,
}

[Flags]
public enum EStatusTrigger
{
    None = 0,           // 자체적으로 효과 없는 경우
    // 트리거 플래그
    OnEquip = 1 << 0,   // (Deprecated) 장비로서 적용되는 경우
    OnApply = 1 << 1,   // 적용될 때 적용되는 효과가 있는 경우
    OnRemove = 1 << 2,  // (Deprecated) 사라질 때 적용되는 효과가 있는 경우
    OnAttack = 1 << 3,  // 공격할 때 적용되는 효과가 있는 경우
    OnDefence = 1 << 4, // 방어할 때 적용되는 효과가 있는 경우
    OnHit = 1 << 5,  // 대미지 연산이 끝난 후, 대미지가 들어갈 때 적용되는 효과가 있는 경우
    OnDamage = 1 << 6, // 대미지 연산이 끝난 후, 대미지가 들어올 때 적용되는 효과가 있는 경우
    OnDelta = 1 << 7,   // Time.deltaTime마다 적용되는 효과가 있는 경우
    OnBattleStart = 1 << 8, // 전투가 끝날 때 적용되는 효과가 있는 경우 (그냥 상태이상 해제용)
    OnBattleEnd = 1 << 9, // 전투가 끝날 때 적용되는 효과가 있는 경우 (그냥 상태이상 해제용)
    // 효과 플래그
    IsStun = 1 << 16    // 기절 계열
}

public enum EStatusAction
{
    None,
    [InspectorName("최대체력 비율 변화")]
    MultiplyHpMax,
    [InspectorName("공격력 비율 변화")]
    MultiplyAttack,
    [InspectorName("방어력 비율 변화")]
    MultiplyDefence,
    [InspectorName("치명타확률 비율 변화 (상수, 플레이어용)")]
    AddCriticalRate,
    [InspectorName("치명타확률 비율 변화 (비율, 플레이어용)")]
    MultiplyCriticalRate,
    [InspectorName("치명타피해 비율 변화 (플레이어용)")]
    MultiplyCriticalDmg,
    [InspectorName("주는피해량 비율 변화")]
    MultiplyAttackerDamage,
    [InspectorName("받는피해량 비율 변화")]
    MultiplyDefenderDamage,
    [InspectorName("이동속도 변화")]
    MultiplyMovement,
    [InspectorName("기절")]
    Stun,
    [InspectorName("지속피해")]
    DamageOverTime,
    [InspectorName("최대체력 비례 지속피해")]
    DamageOverTimeByRatio,
    [InspectorName("공격속도 변화")]
    MultiplyAttackSpeed,
    [InspectorName("전투 종료시 제거")]
    RemoveWhenBattleOver,
}

[Serializable]
public struct SStatusAction
{
    public EStatusAction action;
    [Tooltip("비율은 증가시 1.xx 감소시 0.xx로!")]
    public float param;
}

[Serializable]
public struct SStatusApply
{
    [Tooltip("상태이상 Id")]
    public int statusId;
    [Tooltip("상태이상 지속시간 (0 : 영속, 음수 : 기본값)")]
    public float duration;
}


public partial class CStatus
{
    private int _id;
    private float _duration;
    private bool _isPermanent;
    private string _name;
    private string _description;
    private string _tooltip;
    private Sprite _image;
    private int _stack;
    private EStatusStack _stackSwitch;
    private EStatusRemove _removeSwitch;
    private float _initialTimer;
    private float _removeTimer;
    private EStatusTrigger _trigger;

    private int _intParam;
    private float _floatParam;
    private float _timerParam;
    private bool _boolParam;

    public int Id => _id;
    public float Duration => _duration;
    public string Name => _name;
    public string Description => _description;
    public string Tooltip => _tooltip;
    public Sprite Image => _image;
    public int Stack
    {
        get { return _stack; }
        set { _stack = value; }
    }
    public EStatusStack StackSwitch => _stackSwitch;
    public EStatusRemove RemoveSwitch => _removeSwitch;
    public float RemoveTimer => _removeTimer;
    public EStatusTrigger Trigger => _trigger;


    // private List<Action<CUnit>> _onEquip = new List<Action<CUnit>>();
    private List<SStatusOnApply> _onApply = new List<SStatusOnApply>();
    private List<Action<CUnit>> _onRemove = new List<Action<CUnit>>(); // Deprecated
    private List<Func<CUnit, CUnit, float, float>> _onAttack = new List<Func<CUnit, CUnit, float, float>>();
    private List<Func<CUnit, CUnit, float, float>> _onDefence = new List<Func<CUnit, CUnit, float, float>>();
    private List<Func<CUnit, CUnit, float, float>> _onHit = new List<Func<CUnit, CUnit, float, float>>();
    private List<Func<CUnit, CUnit, float, float>> _onDamage = new List<Func<CUnit, CUnit, float, float>>();
    private List<Action<CUnit, float>> _onDelta = new List<Action<CUnit, float>>();
    private List<Action<CUnit, bool>> _onBattleStart = new List<Action<CUnit, bool>>();
    private List<Action<CUnit, bool>> _onBattleEnd = new List<Action<CUnit, bool>>();

    //public List<Action<CUnit>> OnEquip
    //{
    //    get => _onEquip;
    //}

    public List<SStatusOnApply> OnApply
    {
        get => _onApply;
    }

    public List<Action<CUnit>> OnRemove
    {
        get => _onRemove;
    }

    public List<Func<CUnit, CUnit, float, float>> OnAttack
    {
        get => _onAttack;
    }

    public List<Func<CUnit, CUnit, float, float>> OnDefence
    {
        get => _onDefence;
    }
    public List<Func<CUnit, CUnit, float, float>> OnHit
    {
        get => _onHit;
    }

    public List<Func<CUnit, CUnit, float, float>> OnDamage
    {
        get => _onDamage;
    }

    public List<Action<CUnit, float>> OnDelta
    {
        get => _onDelta;
    }
    public List<Action<CUnit, bool>> OnBattleStart
    {
        get => _onBattleStart;
    }
    public List<Action<CUnit, bool>> OnBattleEnd
    {
        get => _onBattleEnd;
    }

    public CStatus(int id, float duration, int stack)
    {
        Setup(id, duration, stack);
    }

    public void OnDuration(CUnit target, float time)
    {
        if (_isPermanent) return;
        _duration -= time;
        if (_duration < 0)
        {
            switch (_removeSwitch)
            {
                case EStatusRemove.RemoveAll:
                    target.RemoveStatus(this);
                    break;
                case EStatusRemove.RemoveOne:
                    _stack -= 1;
                    if (_stack <= 0)
                    {
                        target.RemoveStatus(this);
                    }
                    else
                    {
                        _duration = _initialTimer + _duration;
                    }
                    break;
                case EStatusRemove.RemoveFor:
                    _stack -= 1;
                    if (_stack <= 0)
                    {
                        target.RemoveStatus(this);
                    }
                    else
                    {
                        _duration = _removeTimer + _duration;
                    }
                    break;
            }
        }

    }

    public static CStatus MakeStatus(SStatusApply targetStatus, int stack = 1)
    {
        return MakeStatus(targetStatus.statusId, targetStatus.duration, stack);
    }


    public static CStatus MakeStatus(int id, float duration = -1, int stack = 1)
    {
        CStatus status = new CStatus(id, duration, stack);
        return status;
    }

    public void ResetTimer(float t = -1)
    {
        if (t == 0)
        {
            _isPermanent = true;
            _duration = 0;
        }
        else if (t < 0)
        {
            if (_initialTimer <= 0)
            {
                _duration = 0;
                _isPermanent = true;
            }
            else
            {
                _duration = _initialTimer;
                _isPermanent = false;
            }
        }
        else
        {
            _isPermanent = false;
            _duration = t;
        }
    }

}
