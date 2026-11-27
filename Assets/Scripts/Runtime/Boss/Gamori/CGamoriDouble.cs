using UnityEngine;

/// <summary>
/// 가모리가 소환한 분신의 공격과 소멸을 처리합니다.
/// </summary>
public sealed class CGamoriDouble : CGamoriCombatant
{
    private static readonly int _BURN = Shader.PropertyToID("_GamoriBurn");

    [SerializeField] private CGamoriAttack[] _patterns;
    [SerializeField, Min(0.1f)] private Vector2 _interval = new Vector2(2.8f, 3.6f);
    [SerializeField, Min(0)] private int _attacksBeforeSummon = 2;
    [SerializeField, Min(0.05f)] private float _vanishTime = 0.5f;

    private Renderer[] _renderers;
    private MaterialPropertyBlock _properties;
    private Transform _hips;
    private float _vanishElapsed = -1f;

    private CPlayer _target;
    private CGamoriChase _chase;
    private CGamoriAttack _lastAttack;
    private CGamoriSummonAttack _summon;
    private float _nextAttackTime;
    private int _attackCount;
    private bool _canSummon;
    private bool _recovering;

    public override string DisplayName => _name;
    internal CGamoriBoss Owner { get; private set; }

    internal void Summon(CPlayer target, CGamoriBoss owner, bool canSummon, float hp, float attack)
    {
        _target = target;
        Owner = owner;
        _canSummon = canSummon;
        TryGetComponent(out _summon);
        _hpCurrent = _hpMax = _baseHpMax = hp;
        _attack = _baseAttack = attack;
        _nextAttackTime = Time.time + Random.Range(_interval.x, _interval.y);
        TryGetComponent(out _chase);
    }

    /// <summary>
    /// 분신의 공격과 충돌을 끄고 소멸 동작을 시작합니다.
    /// </summary>
    public void Vanish()
    {
        if (_vanishElapsed >= 0f) return;
        _vanishElapsed = 0f;
        if (Owner) Owner.RemoveClone(this);
        ResetBattleState();
        if (_chase) _chase.enabled = false;
        foreach (Collider body in GetComponentsInChildren<Collider>()) body.enabled = false;
        // 소멸한 부분에 색 보정 마스크가 남지 않도록 함께 끈다.
        foreach (CEnvironmentGradeMask mask in GetComponentsInChildren<CEnvironmentGradeMask>()) mask.enabled = false;
        _renderers = GetComponentsInChildren<Renderer>();
        _properties = new MaterialPropertyBlock();
        foreach (Transform bone in GetComponentsInChildren<Transform>())
        {
            if (bone.name != "hips") continue;
            _hips = bone;
            break;
        }
    }

    private void UpdateVanish()
    {
        _vanishElapsed += Time.deltaTime;
        float unit = transform.lossyScale.y * CGamoriEffectClip.UNIT_SCALE;
        float height = _hips ? _hips.position.y : transform.position.y + unit;
        Vector4 burn = new Vector4(Mathf.Clamp01(_vanishElapsed / _vanishTime), height, 1f / unit, 0f);
        foreach (Renderer part in _renderers)
        {
            if (!part) continue;
            part.GetPropertyBlock(_properties);
            _properties.SetVector(_BURN, burn);
            part.SetPropertyBlock(_properties);
        }
        if (_vanishElapsed >= _vanishTime) Destroy(gameObject);
    }

    protected override void Update()
    {
        if (_vanishElapsed >= 0f)
        {
            UpdateVanish();
            return;
        }
        base.Update();
        if (!_target || _target.HpCurrent <= 0f || !Owner || IsCasting ||
            CGameManager.Instance.GameState != EGameState.Idle) return;
        if (_recovering)
        {
            _recovering = false;
            _nextAttackTime = Time.time + Random.Range(_interval.x, _interval.y);
        }
        if (Time.time < _nextAttackTime || IsStunned) return;
        if (_canSummon && _attackCount >= _attacksBeforeSummon && TryCast(_summon, _target))
        {
            _canSummon = false;
            _recovering = true;
            return;
        }
        if (_chase && _chase.IsMoving) return;
        CGamoriAttack choice = null;
        int candidates = 0;
        foreach (CGamoriAttack pattern in _patterns)
        {
            if (!pattern || pattern == _lastAttack || !Owner.CanUseAttack(pattern)) continue;
            candidates++;
            if (Random.Range(0, candidates) == 0) choice = pattern;
        }
        if (!choice || !TryCast(choice, _target)) return;
        Owner.StartAttackCooldown(choice);
        _attackCount++;
        _lastAttack = choice;
        _recovering = true;
    }

    protected override void Die()
    {
        Vanish();
    }
}
