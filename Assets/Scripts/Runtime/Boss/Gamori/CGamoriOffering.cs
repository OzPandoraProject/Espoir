using UnityEngine;

/// <summary>
/// 정기 흡수 패턴에 소환되는 제물입니다.
/// </summary>
public sealed class CGamoriOffering : CEnemy
{
    public override string DisplayName => _name;

    private static readonly int _ALPHA = Shader.PropertyToID("_Alpha");
    private static readonly int _HOT = Shader.PropertyToID("_Hot");
    private static readonly int _DISSOLVE = Shader.PropertyToID("_Dissolve");
    private static readonly int _PULL = Shader.PropertyToID("_Pull");
    private static readonly int _LEAN = Shader.PropertyToID("_Lean");
    private static readonly int _PULL_DIR = Shader.PropertyToID("_PullDir");
    private static readonly int _BASE = Shader.PropertyToID("_Base");
    private static readonly int _HEIGHT = Shader.PropertyToID("_Height");
    private static readonly int _SEED = Shader.PropertyToID("_Seed");
    private static readonly int _FEAR_STATE = Animator.StringToHash("Fear");
    private static readonly int _DEATH_STATE = Animator.StringToHash("Death");
    private static readonly int _RUN_STATE = Animator.StringToHash("Run");

    [SerializeField] private Animator _animator;
    [SerializeField] private Renderer[] _renderers;
    [SerializeField, Min(0.1f)] private float _height = 1f;
    [SerializeField, Min(0.01f)] private float _riseTime = 0.16f;
    [SerializeField, Min(0f)] private float _burnDelay = 0.45f;
    [SerializeField, Min(0.1f)] private float _killedBurnTime = 0.7f;
    [SerializeField, Min(0.1f)] private float _absorbedBurnTime = 1.45f;
    [SerializeField, Min(0f)] private float _leanAmount = 0.14f;

    [Header("도망")]
    [SerializeField, Min(0f)] private float _runSpeed = 5.6f;
    [SerializeField] private Vector2 _runTime = new Vector2(1.3f, 2.8f);
    [SerializeField] private Vector2 _restTime = new Vector2(0.3f, 0.6f);
    [SerializeField, Min(0f)] private float _turnSpeed = 600f;
    [SerializeField, Min(0f)] private float _firstRest = 2.5f;
    [SerializeField, Min(0f)] private float _dragSpeed = 11f;

    private Transform _threat;
    private Vector3 _stageCenter, _throne, _heading, _dragTo;
    private float _stageRadius, _keepAway, _keepWithin, _stateLeft;
    private bool _fleeing, _running, _safe, _dragging;
    private MaterialPropertyBlock _properties;
    private Vector3 _fullScale;
    private float _age, _deadFor = -1f, _burnTime, _lean, _leanTarget;
    private bool _pulled;

    public bool IsAlive => _deadFor < 0f && HpCurrent > 0f;

    /// <summary>
    /// 제물의 현재 체력과 최대 체력을 함께 설정합니다.
    /// </summary>
    public void SetHealth(float hp)
    {
        _hpCurrent = _hpMax = _baseHpMax = hp;
    }

    /// <summary>
    /// 지정한 크기와 개체별 무늬로 등장합니다.
    /// </summary>
    public void Appear(float size, float seed, Vector3 lookAt)
    {
        Vector3 toward = Vector3.ProjectOnPlane(lookAt - transform.position, Vector3.up);
        if (toward.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(toward);
        _fullScale = Vector3.one * size;
        transform.localScale = new Vector3(_fullScale.x * 1.12f, 0f, _fullScale.z * 1.12f);
        _properties = new MaterialPropertyBlock();
        _properties.SetFloat(_HEIGHT, _height * size);
        _properties.SetFloat(_SEED, seed);
        if (_animator) _animator.Play(_FEAR_STATE, 0, Mathf.Repeat(seed * 0.37f, 1f));
        Apply();
    }

    /// <summary>
    /// 가모리 쪽으로 끌리기 시작합니다. 흡수되기 조금 전에 부릅니다.
    /// </summary>
    public void Pull(Vector3 toward, float within = float.MaxValue)
    {
        if (_properties == null || _pulled) return;
        _pulled = true;
        _fleeing = false;
        if (_running) Rest(0f);
        Vector3 away = Vector3.ProjectOnPlane(transform.position - toward, Vector3.up);
        if (away.magnitude > within)
        {
            _dragging = true;
            _dragTo = toward + away.normalized * within;
            _dragTo.y = transform.position.y;
        }
        _leanTarget = _leanAmount;
        _properties.SetVector(_PULL_DIR, Vector3.ProjectOnPlane(toward - transform.position, Vector3.up).normalized);
    }

    /// <summary>
    /// 흡수되어 쓰러집니다. 끌려가며 천천히 타 들어간 뒤 사라집니다.
    /// </summary>
    public void Absorb()
    {
        if (_deadFor >= 0f) return;
        _leanTarget = _leanAmount * 2.5f;
        Fall(_absorbedBurnTime);
    }

    /// <summary>
    /// 왕좌 주변의 지정 범위 안에서 대상을 피해 이동합니다.
    /// </summary>
    public void Flee(Vector3 center, float radius, Vector3 throne, float keepAway, float keepWithin, Transform threat)
    {
        _stageCenter = center;
        _stageRadius = radius;
        _throne = throne;
        _keepAway = keepAway;
        _keepWithin = keepWithin;
        _threat = threat;
        _fleeing = true;
        _running = false;
        _stateLeft = _riseTime + _firstRest;
    }

    /// <summary>
    /// 흡수가 시작되어 더는 쓰러뜨릴 수 없게 합니다. 도망을 멈추고 제자리에서 떱니다.
    /// </summary>
    public void SetSafe()
    {
        _safe = true;
        _fleeing = false;
        if (_running) Rest(0f);
    }

    /// <inheritdoc/>
    public override void GetDamage(float value, ERatioSwitch ratioSwitch = ERatioSwitch.Constant,
        bool penetrateInvincible = false)
    {
        if (_safe) return;
        base.GetDamage(value, ratioSwitch, penetrateInvincible);
    }

    private void Roam()
    {
        _stateLeft -= Time.deltaTime;
        if (!_running)
        {
            if (_stateLeft > 0f) return;
            if (!PickHeading())
            {
                _stateLeft = 0.2f;
                return;
            }
            _running = true;
            _stateLeft = Random.Range(_runTime.x, _runTime.y);
            if (_animator) _animator.CrossFadeInFixedTime(_RUN_STATE, 0.12f);
            return;
        }
        Vector3 next = transform.position + _heading * (_runSpeed * Time.deltaTime);
        if (_stateLeft <= 0f || !CanStand(next))
        {
            Rest(Random.Range(_restTime.x, _restTime.y));
            return;
        }
        transform.position = next;
        transform.rotation = Quaternion.RotateTowards(transform.rotation,
            Quaternion.LookRotation(_heading), _turnSpeed * Time.deltaTime);
    }

    private void Rest(float time)
    {
        _running = false;
        _stateLeft = time;
        if (_animator) _animator.CrossFadeInFixedTime(_FEAR_STATE, 0.15f);
    }

    private bool PickHeading()
    {
        float best = float.MinValue;
        bool found = false;
        for (int i = 0; i < 10; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            var direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            Vector3 spot = transform.position + direction * (_runSpeed * _runTime.x);
            if (!CanStand(spot)) continue;
            float score = Random.Range(0f, 8f);
            if (_threat) score += Mathf.Min(Vector3.ProjectOnPlane(spot - _threat.position, Vector3.up).magnitude, 7f);
            if (score <= best) continue;
            best = score;
            _heading = direction;
            found = true;
        }
        return found;
    }

    // 무대 밖에서 소환된 제물은 무대 안쪽으로 접근할 수 있다.
    private bool CanStand(Vector3 spot)
    {
        float fromThrone = Flat(spot - _throne), fromCenter = Flat(spot - _stageCenter);
        float nowThrone = Flat(transform.position - _throne), nowCenter = Flat(transform.position - _stageCenter);
        return (fromCenter <= _stageRadius || fromCenter <= nowCenter)
            && (fromThrone >= _keepAway || fromThrone >= nowThrone)
            && (fromThrone <= _keepWithin || fromThrone <= nowThrone);
    }

    private static float Flat(Vector3 offset) => new Vector2(offset.x, offset.z).magnitude;

    protected override void Die()
    {
        if (_deadFor >= 0f) return;
        Fall(_killedBurnTime);
    }

    private void Fall(float burnTime)
    {
        _deadFor = 0f;
        _burnTime = burnTime;
        _fleeing = _running = false;
        foreach (Collider body in GetComponentsInChildren<Collider>()) body.enabled = false;
        if (_animator) _animator.CrossFadeInFixedTime(_DEATH_STATE, 0.1f);
    }

    private void LateUpdate()
    {
        if (_properties == null) return;
        _age += Time.deltaTime;
        if (_deadFor >= 0f) _deadFor += Time.deltaTime;
        if (_fleeing && _deadFor < 0f) Roam();
        if (_dragging)
        {
            transform.position = Vector3.MoveTowards(transform.position, _dragTo, _dragSpeed * Time.deltaTime);
            _dragging = transform.position != _dragTo;
        }
        _lean = Mathf.MoveTowards(_lean, _leanTarget, Time.deltaTime * _leanAmount * 3f);
        Apply();
        if (_deadFor >= _burnDelay + _burnTime) Destroy(gameObject);
    }

    private void Apply()
    {
        float rise = 1f - Mathf.Pow(1f - Mathf.Clamp01(_age / _riseTime), 3f);
        float over = 1f + 0.14f * Mathf.Sin(Mathf.PI * Mathf.Clamp01((_age - _riseTime) / 0.18f));
        float width = 1.12f - 0.12f * rise;
        transform.localScale = new Vector3(_fullScale.x * width, _fullScale.y * rise * over, _fullScale.z * width);

        float burn = _deadFor < 0f ? 0f : Mathf.Clamp01((_deadFor - _burnDelay) / _burnTime);
        _properties.SetVector(_BASE, transform.position);
        _properties.SetFloat(_ALPHA, Mathf.Clamp01(_age / 0.06f));
        _properties.SetFloat(_HOT, Mathf.Exp(-_age / 0.22f) + (_deadFor >= 0f ? 0.25f * (1f - burn) : 0f));
        _properties.SetFloat(_DISSOLVE, burn > 0f ? 0.04f + 1.02f * burn : 0f);
        _properties.SetFloat(_PULL, _pulled ? 1f : 0f);
        _properties.SetFloat(_LEAN, _lean * _fullScale.y);
        foreach (Renderer part in _renderers)
        {
            if (part) part.SetPropertyBlock(_properties);
        }
    }
}
