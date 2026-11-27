using System.Collections.Generic;
using UnityEngine;

public sealed class CGamoriBoss : CGamoriCombatant, IBoss
{
    [System.Serializable]
    private struct SPhase
    {
        [Range(0f, 1f)] public float health;
        public CGamoriAttack attack;
        public CGamoriAttack repeatAttack;
    }

    [System.Serializable]
    private struct STempo
    {
        [Range(0f, 1f)] public float minHealth;
        public Vector2 delay;
        [Range(0f, 1f)] public float subChance;

        /// <summary>
        /// 체력 비율에 따라 적용할 공격 간격(초)과 서브 공격 확률을 설정합니다.
        /// </summary>
        public STempo(float minHealth, float minDelay, float maxDelay, float subChance)
        {
            this.minHealth = minHealth;
            delay = new Vector2(minDelay, maxDelay);
            this.subChance = subChance;
        }
    }

    private static readonly STempo _OPENING_TEMPO = new STempo(0.65f, 1.8f, 2.3f, 0.3f);

    [SerializeField] private Transform _throneFront;
    [SerializeField] private SPhase[] _phases;
    [SerializeField] private CGamoriAttack[] _basic;
    [SerializeField] private CGamoriAttack[] _sub;
    [SerializeField] private CGamoriRushAttack _rush;
    [SerializeField] private STempo[] _tempo =
    {
        _OPENING_TEMPO,
        new STempo(0.35f, 1.1f, 1.5f, 0.4f),
        new STempo(0f, 0.85f, 1.2f, 0.5f)
    };
    [SerializeField, Min(0f)] private float _firstAttackDelay = 1.5f;
    [SerializeField] private float _closeRange = 4f;
    [SerializeField] private float _rushRange = 6f;
    [SerializeField] private float _farRange = 8f;
    [SerializeField] private float _escapeRange = 12f;
    [SerializeField, Min(0f)] private float _farRushDelay = 1f;
    [SerializeField] private int _cloneLimit = 2;
    [SerializeField] private float _rushCooldown = 10f;
    [SerializeField, Min(0f)] private float _crossCooldown = 18f;
    [SerializeField, Min(1)] private int _subAttacksBeforeRush = 2;

    private readonly Queue<int> _PENDING = new Queue<int>();
    private readonly HashSet<int> _USED_PHASES = new HashSet<int>();
    private readonly List<CGamoriDouble> _CLONES = new List<CGamoriDouble>();
    private CPlayer _player;
    private CGamoriAttack _lastAttack;
    private int _subAttacksSinceRush;
    private float _nextCrossTime;
    private float _previousHealthRatio;
    private float _nextAttackTime;
    private float _timeAtRange;
    private float _nextRushTime;
    private bool _recovering;
    private bool _rushReserved;
    private CGamoriSummonAttack _summoning;

    public bool IsGroggy => false;
    public override EBossClear BossId => EBossClear.Luxuria;

    internal Transform ThroneFront => _throneFront;

    internal void BeginBattle(CPlayer player)
    {
        _player = player;
        _nextCrossTime = Time.time + _crossCooldown;
        CGameManager.Instance.BeginBossTimer(BossId);
        _previousHealthRatio = HpCurrent / HpMax;
        _nextAttackTime = Time.time + _firstAttackDelay;
    }

    protected override void Update()
    {
        base.Update();
        if (!_player || HpCurrent <= 0f || _player.HpCurrent <= 0f ||
            CGameManager.Instance.GameState != EGameState.Idle) return;

        TrackHealth();
        if (IsCasting) return;
        float health = HpCurrent / HpMax;
        STempo tempo = _tempo.Length > 0 ? _tempo[_tempo.Length - 1] : _OPENING_TEMPO;
        foreach (STempo stage in _tempo)
        {
            if (health <= stage.minHealth) continue;
            tempo = stage;
            break;
        }
        if (_recovering)
        {
            _recovering = false;
            float delay = Random.Range(tempo.delay.x, tempo.delay.y);
            _nextAttackTime = Time.time + Mathf.Max(delay, _lastAttack.MinNextAttackDelay);
        }

        float distance = Vector3.ProjectOnPlane(_player.transform.position - transform.position, Vector3.up).magnitude;
        _timeAtRange = distance >= _farRange ? _timeAtRange + Time.deltaTime : 0f;
        if (distance >= _escapeRange || (distance >= _farRange && _timeAtRange >= _farRushDelay))
            _rushReserved = true;
        if (Time.time < _nextAttackTime || IsStunned) return;
        while (_PENDING.Count > 0)
        {
            int index = _PENDING.Peek();
            SPhase phase = _phases[index];
            CGamoriAttack attack = phase.repeatAttack && _USED_PHASES.Contains(index)
                ? phase.repeatAttack : phase.attack;
            if (!attack || (attack is CGamoriSummonAttack && _CLONES.Count >= _cloneLimit))
            {
                _PENDING.Dequeue();
                continue;
            }
            if (!TryStartAttack(attack)) return;
            if (phase.repeatAttack) _USED_PHASES.Add(index);
            _PENDING.Dequeue();
            return;
        }
        if (_rushReserved && TryStartAttack(_rush)) return;
        bool basicOnly = _tempo.Length > 0 && health > _tempo[0].minHealth &&
            (!_lastAttack || System.Array.IndexOf(_sub, _lastAttack) >= 0);
        if (basicOnly)
        {
            if (distance <= _farRange) TryCastRandomAttack(_basic, distance);
            return;
        }
        if (_subAttacksSinceRush >= _subAttacksBeforeRush && distance <= _rushRange && TryStartAttack(_rush)) return;
        if (distance > _farRange) return;
        bool sub = Random.value < tempo.subChance;
        if (!TryCastRandomAttack(sub ? _sub : _basic, distance)) TryCastRandomAttack(sub ? _basic : _sub, distance);
    }

    private bool TryCastRandomAttack(CGamoriAttack[] attacks, float distance)
    {
        CGamoriAttack selected = null;
        int candidates = 0;
        foreach (CGamoriAttack attack in attacks)
        {
            if (!attack || attack == _lastAttack || !CanUseAttack(attack)) continue;
            bool ranged = attack is CGamoriHeartAttack || attack is CGamoriSpikeAttack ||
                attack is CGamoriRingAttack ring && ring.DealsPeriodicDamage;
            if (distance > _closeRange && attack != _rush && !ranged) continue;
            candidates++;
            if (Random.Range(0, candidates) == 0) selected = attack;
        }
        return selected && TryStartAttack(selected);
    }

    private bool TryStartAttack(CGamoriAttack attack)
    {
        if (!CanUseAttack(attack) || !TryCast(attack, _player)) return false;
        _lastAttack = attack;
        _recovering = true;
        StartAttackCooldown(attack);
        if (attack == _rush)
        {
            _rushReserved = false;
            _timeAtRange = 0f;
            _subAttacksSinceRush = 0;
        }
        else if (System.Array.IndexOf(_sub, attack) >= 0)
        {
            _subAttacksSinceRush++;
        }
        return true;
    }

    internal bool CanUseAttack(CGamoriAttack attack)
    {
        if (attack is CGamoriCrossAttack && Time.time < _nextCrossTime) return false;
        if (attack is CGamoriRushAttack && Time.time < _nextRushTime) return false;
        if (Conflicts(attack, CurrentAttack)) return false;
        foreach (CGamoriDouble clone in _CLONES)
        {
            if (clone && Conflicts(attack, clone.CurrentAttack)) return false;
        }
        return true;
    }

    private static bool Conflicts(CGamoriAttack next, CGamoriAttack current)
    {
        if (!current) return false;
        bool fence = next is CGamoriRingAttack || next is CGamoriBindAttack;
        return (fence && current is CGamoriRushAttack) ||
            (next is CGamoriRushAttack && (current is CGamoriRingAttack ||
            current is CGamoriBindAttack || current is CGamoriRushAttack));
    }

    /// <summary>
    /// 가모리는 별도의 무력화 게이지를 사용하지 않습니다.
    /// </summary>
    public bool TryGetGroggyInfo(out float remainHp, out float maxHp, out float remainTime, out float maxTime)
    {
        remainHp = maxHp = remainTime = maxTime = 0f;
        return false;
    }

    internal void StartAttackCooldown(CGamoriAttack attack)
    {
        if (attack is CGamoriRushAttack) _nextRushTime = Time.time + _rushCooldown;
        if (attack is CGamoriCrossAttack) _nextCrossTime = Time.time + _crossCooldown;
    }

    internal bool CanSummonClone()
    {
        _CLONES.RemoveAll(clone => !clone || clone.HpCurrent <= 0f);
        return !_summoning && _CLONES.Count < _cloneLimit;
    }

    internal void ReserveClone(CGamoriSummonAttack summon) => _summoning = summon;

    internal void ReleaseClone(CGamoriSummonAttack summon)
    {
        if (_summoning == summon) _summoning = null;
    }

    internal void RegisterClone(CGamoriDouble clone) => _CLONES.Add(clone);
    internal void RemoveClone(CGamoriDouble clone) => _CLONES.Remove(clone);

    private void TrackHealth()
    {
        float health = HpCurrent / HpMax;
        if (health == _previousHealthRatio) return;

        for (int i = 0; i < _phases.Length; i++)
        {
            float threshold = _phases[i].health;
            if (_previousHealthRatio > threshold && health <= threshold) _PENDING.Enqueue(i);
        }
        _previousHealthRatio = health;
    }

    /// <summary>
    /// 피해로 통과한 체력 구간의 패턴을 예약합니다.
    /// </summary>
    public override void GetDamage(float value, ERatioSwitch ratioSwitch = ERatioSwitch.Constant,
        bool penetrateInvincible = false)
    {
        if (!isActiveAndEnabled) return;
        base.GetDamage(value, ratioSwitch, penetrateInvincible);
        if (_player) TrackHealth();
    }

    /// <summary>
    /// 회복한 체력을 다음 패턴의 기준으로 갱신합니다.
    /// </summary>
    public override void GetHeal(float value, ERatioSwitch ratioSwitch = ERatioSwitch.Constant, bool forceHeal = false)
    {
        base.GetHeal(value, ratioSwitch, forceHeal);
        if (_player) TrackHealth();
    }

    protected override void Die()
    {
        EndBattle(true);
        base.Die();
    }

    private void EndBattle(bool cleared = false)
    {
        if (_player && CGameManager.Instance) CGameManager.Instance.EndBossTimer(BossId, cleared);
        _player = null;
        _PENDING.Clear();
        _USED_PHASES.Clear();
        ClearDoubles();
        _summoning = null;
        _recovering = _rushReserved = false;
        _lastAttack = null;
        _subAttacksSinceRush = 0;
        _nextRushTime = _timeAtRange = 0f;
    }

    internal void ClearDoubles()
    {
        for (int i = _CLONES.Count - 1; i >= 0; i--)
        {
            if (_CLONES[i]) _CLONES[i].Vanish();
        }
        _CLONES.Clear();
    }

    protected override void OnDisable()
    {
        EndBattle();
        base.OnDisable();
    }
}
