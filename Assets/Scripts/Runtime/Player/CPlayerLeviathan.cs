using UnityEngine;

public enum ELeviathanState
{
    None,
    Buff,
    Finisher
}

/// <summary>
/// 레비아탄의 버프, 피니쉬 중첩과 타격을 관리합니다. HUD는 현재 상태를 읽어 표시합니다.
/// </summary>
public class CPlayerLeviathan
{
    private CLeviathanSkillSO _settings;
    private ELeviathanState _state;
    private int _stack;
    private bool _hasFlowCondition;
    private bool _isFinisherPending;
    private bool _isManualFinisher;
    private int _finisherStack;
    private int _finisherHitCount;
    private int _nextHit;
    private int _landedHits;
    private Vector3 _origin;
    private Quaternion _rotation;
    private CPlayer _finisherOwner;

    public CLeviathanSkillSO Settings => _settings;
    public ELeviathanState State => _state;
    public int Stack => _stack;
    public int MaxStack => _settings != null ? _settings.MaxFinisherStack : 0;
    public bool IsEnhanced => _settings != null && _stack >= _settings.MaxFinisherStack;
    public bool HasFlowCondition => _hasFlowCondition;
    public int FinisherHitCount => _state == ELeviathanState.Finisher ? _finisherHitCount : PlannedHitCount;
    public int FinisherHitsDone => _state == ELeviathanState.Finisher ? _nextHit : 0;

    private int PlannedHitCount => _settings == null ? 0 :
        _settings.BaseHitCount + (IsEnhanced ? _settings.BonusHitCount : 0);

    internal bool WasFinisherEnhanced => _finisherStack >= MaxStack;
    internal bool IsIdle => _state == ELeviathanState.None;
    internal bool IsBuffActive => _state == ELeviathanState.Buff;
    internal bool IsFinisherPending => _isFinisherPending;

    internal static bool HasValidSettings(CLeviathanSkillSO settings)
    {
        return settings.BuffStatus != null && settings.FlowStatus != null &&
            settings.FirstHitTime < settings.LastHitTime &&
            settings.PenetrationHitCount <= settings.BaseHitCount &&
            settings.Steps[0].hits != null && settings.Steps[0].hits.Length > 0;
    }

    internal void Initialize(CLeviathanSkillSO settings)
    {
        _settings = settings;
    }

    internal void BeginBuff(CPlayer player)
    {
        // 피니쉬 때 흐름을 다시 보지 않도록 시전 순간의 흐름을 기록한다.
        _hasFlowCondition = player.FindStatusStack(_settings.FlowStatus.Id) >= _settings.RequiredFlowStack;
        _stack = 0;
        _isFinisherPending = false;
        _state = ELeviathanState.Buff;
        player.AddStatus(_settings.BuffStatus.Id);
    }

    internal void AddStack()
    {
        if (_state != ELeviathanState.Buff || _isFinisherPending) return;

        _stack = Mathf.Min(_stack + 1, _settings.MaxFinisherStack);
    }

    internal void CheckExpired(CPlayer player)
    {
        if (_state != ELeviathanState.Buff) return;

        // 공통 상태 타이머를 기준으로 자동 피니쉬 시점을 확인한다.
        CStatus buff = player.FindStatus(_settings.BuffStatus.Id);
        if (buff == null || buff.Duration <= _settings.BuffStatus.Duration - _settings.FinisherDelay)
        {
            _isFinisherPending = true;
        }
    }

    internal void BeginFinisher(CPlayer player, bool isManual)
    {
        _finisherHitCount = PlannedHitCount;
        _isManualFinisher = isManual;
        _finisherStack = _stack;

        // 피니쉬에도 버프를 적용하고 종료 시 제거한다. 시작이 늦어져 만료됐다면 다시 부여한다.
        CStatus buff = player.FindStatus(_settings.BuffStatus.Id);
        if (buff == null)
        {
            player.AddStatus(_settings.BuffStatus.Id, duration: 0f);
        }

        else
        {
            buff.ResetTimer(0f);
        }

        _finisherOwner = player;
        _state = ELeviathanState.Finisher;
        _isFinisherPending = false;
        _nextHit = 0;
        _landedHits = 0;
        _origin = player.transform.position;
        _rotation = player.transform.rotation;
    }

    internal void UpdateFinisher(CPlayer player, CPlayerAttack attack, float time)
    {
        if (_state != ELeviathanState.Finisher) return;

        while (_nextHit < _finisherHitCount && time >= GetHitTime(_nextHit))
        {
            int index = _nextHit;
            _nextHit++;

            bool isLastHit = index == _finisherHitCount - 1;
            bool isPenetrating = _hasFlowCondition && index >= _finisherHitCount - _settings.PenetrationHitCount;
            SSkillHit hit = _settings.Steps[0].hits[0];
            hit.damageRatio = isLastHit ? _settings.LastHitDamageRatio : _settings.HitDamageRatio;
            hit.markedDamageRatio = 0f;

            // 첫 적중 전까지는 낙인 가득(5중첩)을 피해 계산 전에 걸고, 이후 타격은 낙인 시간만 갱신한다.
            EHitMark mark = _landedHits == 0 ? EHitMark.Fill : EHitMark.Refresh;
            SPlayerHitResult result = attack.Attack(player, hit, _origin, _rotation, mark, null,
                isPenetrating ? _settings.ArmorPenetration : 0f, _hasFlowCondition && isLastHit);

            if (!result.hasHit) continue;

            _landedHits++;
            player.Passive.GainFlow(player, player.Passive.FlowPerHit(result.hitFullyMarkedTarget));
        }
    }

    internal void End(bool canceled = false)
    {
        if (_state != ELeviathanState.Finisher) return;

        if (!canceled && _nextHit < _finisherHitCount)
        {
            CLogger.Warn($"레비아탄 피니쉬가 {_nextHit}/{_finisherHitCount}타에서 끊겼습니다.");
        }

        if (!canceled && _nextHit >= _finisherHitCount)
        {
            CQuestManager.ReportLeviathanFinisher(_finisherStack, MaxStack, _isManualFinisher);
        }

        RemoveFinisherBuff();
        _state = ELeviathanState.None;
        _stack = 0;
        _hasFlowCondition = false;
    }

    internal void Clear(CPlayer player)
    {
        if (_settings != null && player != null && _state == ELeviathanState.Buff)
        {
            player.RemoveStatus(_settings.BuffStatus.Id);
        }

        RemoveFinisherBuff();
        _state = ELeviathanState.None;
        _stack = 0;
        _hasFlowCondition = false;
        _isFinisherPending = false;
    }

    private void RemoveFinisherBuff()
    {
        if (_finisherOwner != null && _settings != null && _state == ELeviathanState.Finisher)
        {
            _finisherOwner.RemoveStatus(_settings.BuffStatus.Id);
        }

        _finisherOwner = null;
    }

    private float GetHitTime(int index)
    {
        if (_finisherHitCount <= 1) return _settings.FirstHitTime;

        return Mathf.Lerp(_settings.FirstHitTime, _settings.LastHitTime, (float)index / (_finisherHitCount - 1));
    }
}
