using System.Collections.Generic;
using UnityEngine;

public sealed class CGamoriBindAttack : CGamoriSequenceAttack, IGamoriCine
{
    private enum EShape
    {
        Circle,
        Rectangle,
        Sector
    }

    [System.Serializable]
    private struct SThorn
    {
        public EShape shape;
        public float damageMultiplier;
        public Vector2 center, direction;
        public float impact, radius, halfWidth, halfAngle;

        /// <summary>
        /// 지정한 지점이 가시의 타격 범위 안에 있는지 확인합니다.
        /// </summary>
        public bool Contains(Vector2 point)
        {
            Vector2 offset = point - center;
            if (shape == EShape.Rectangle)
            {
                float forward = Vector2.Dot(offset, direction);
                float side = offset.x * direction.y - offset.y * direction.x;
                return forward >= 0f && forward <= radius && Mathf.Abs(side) <= halfWidth;
            }
            return offset.sqrMagnitude <= radius * radius &&
                (shape == EShape.Circle || Vector2.Angle(offset, direction) <= halfAngle);
        }
    }

    private readonly struct SStep
    {
        public readonly int state;
        public readonly float start, from, blend;

        /// <summary>
        /// 패턴 안에서 동작을 시작할 시각과 클립을 재생할 위치를 각각 초로 지정합니다.
        /// </summary>
        public SStep(string name, float start, float from = 0f, float blend = 0f)
        {
            state = Animator.StringToHash("Base Layer." + name);
            this.start = start;
            this.from = from;
            this.blend = blend;
        }
    }

    // 속박 성공 클립 순서: 무대, 대상, 복귀 꽃잎, 가시관.
    private const int _TARGET = 1, _RETURN = 2, _WREATH = 3;
    private const float _THRUST = 17.8833f, _STAB = 19.7833f, _PULL = 20.1533f;
    // 가시관 크기를 정할 때 기준으로 삼은 모델의 높이(m).
    private const float _REFERENCE_TARGET_HEIGHT = 1.667f * 0.9666f;
    private const float _BRANCH = 12.2333f;
    private const float _WARP_OUT = 12.4533f, _WARP_IN = 12.5533f;
    private const float _APPROACH_OUT = 16.9033f, _APPROACH_IN = 17.0033f;
    private const float _FAIL_END = 15f;
    private const float _WARP_DISTANCE = 3.4778f, _STAB_DISTANCE = 1.3171f;

    private static readonly SStep[] _DANCE =
    {
        new SStep("bind_open", 0f), new SStep("bind_start", 2.8f),
        new SStep("bind_loop", 3.6f), new SStep("bind_loop", 9f),
    };
    private static readonly SStep[] _SUCCESS =
    {
        new SStep("bind_tele", _BRANCH, 0f, 0.2f), new SStep("bind_loop", 13.4333f, 3.2333f, 0.3f),
        new SStep("bind_end_r", 15.0833f), new SStep("bind_tele", 16.6833f), new SStep("bind_thrust", _THRUST),
    };
    private static readonly SStep[] _FAIL =
    {
        new SStep("bind_end_l", _BRANCH, 0f, 0.15f), new SStep("Ready", 13.8333f, 0f, 0.6f),
    };

    [SerializeField] private SThorn[] _thorns;
    [SerializeField] private CStatusSO _bind;
    [SerializeField] private CStatusSO _slow;
    [SerializeField] private CStatusSO _cripple;
    [SerializeField, Min(1)] private int _requiredHits = 3;
    [SerializeField] private float _bindTime = 14.95f;
    [SerializeField] private float _execute = 21.4833f;
    [SerializeField] private TextAsset _cineData;
    [SerializeField] private float _cineIn = 15.06f, _cineOut = 20.3f, _cineReturn = 0.9f;
    [SerializeField] private TextAsset _boundPose;
    [SerializeField] private Shader _boundShader;
    [SerializeField] private float _playerHeight = 1.667f;
    [Tooltip("X는 시작 시각(초), Y는 종료 시각(초), Z는 재생 속도 배율입니다.")]
    [SerializeField] private Vector3 _slowMotion = new Vector3(19.77f, 19.9f, 0.3f);
    [SerializeField] private CGamoriEffectClip _failEffects;
    [SerializeField, Min(0f)] private float _centerDistance = 11.125f;
    [SerializeField] private float _fenceTime = 1.75f;
    [SerializeField] private float _fenceRadius = 11.6f;
    [SerializeField, Min(0f)] private float _fenceMargin = 0.6f;

    private CPlayer _lockedPlayer;
    private SStep[] _steps;
    private CGamoriEntranceTrack _track;
    private CGamoriBoundTarget _boundTarget;
    private readonly List<Renderer> _HIDDEN_COMPANIONS = new List<Renderer>();
    private Quaternion _facing;
    private float _normalSpeed = 1f;
    private bool _cineAsked, _cine, _slowed;
    private Vector3 _center, _target, _approach;
    private int _next, _step;
    private bool _centered, _branched, _success, _warped, _bound, _executed;

    protected override bool ContinueAfterPlayerDeath => _executed;
    protected override bool SettlesAtEnd => false;

    private float Unit => transform.lossyScale.y * CGamoriEffectClip.UNIT_SCALE;
    private int _hits;

    public bool IsPlaying => IsCasting && _cine && PatternTime < _cineOut + _cineReturn;

    public float PlayTime => PatternTime;

    public float CameraOut => _cineOut;

    public float ReturnTime => _cineReturn;

    public CGamoriEntranceTrack Track => _track;

    /// <inheritdoc/>
    public Vector3 ToWorld(Vector3 localPosition) =>
        _target + _facing * (new Vector3(-localPosition.x, localPosition.y, localPosition.z) * Unit);

    /// <inheritdoc/>
    public Vector3 ToWorldDirection(Vector3 localDirection) =>
        _facing * new Vector3(-localDirection.x, localDirection.y, localDirection.z);

    protected override void GetEffectPose(out Vector3 origin, out Quaternion rotation)
    {
        origin = _center;
        rotation = transform.rotation;
    }

    protected override void BeginPattern()
    {
        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        _hits = _next = _step = 0;
        _centered = _branched = _success = _warped = _bound = _executed = false;
        _cineAsked = _cine = false;
        _steps = _DANCE;
        _center = transform.position;
        if (TryGetThroneFront(out Vector3 throne, out Quaternion facing))
        {
            _center = throne + facing * Vector3.forward * _centerDistance;
            _center.y = transform.position.y;
        }
    }

    protected override void UpdatePattern(float time)
    {
        if (!_centered)
        {
            PlaceReturn(_RETURN, _center, true);
            if (time >= AppearTime)
            {
                transform.position = _center;
                _centered = true;
            }
        }
        if (time >= _fenceTime) KeepInside();

        if (!_branched) CountHits(time);
        if (!_branched && time >= _BRANCH) Branch();
        PlayMotion(time);

        if (_branched && !_success)
        {
            if (time >= _FAIL_END) CancelAttack();
            return;
        }
        if (_success) Execute(time);
    }

    private void CountHits(float time)
    {
        Vector3 offset = transform.InverseTransformPoint(Player.transform.position);
        while (_next < _thorns.Length && time >= _thorns[_next].impact)
        {
            float impact = _thorns[_next].impact;
            float multiplier = 0f;
            do
            {
                SThorn thorn = _thorns[_next++];
                if (thorn.Contains(new Vector2(offset.x, offset.z)))
                    multiplier = Mathf.Max(multiplier, thorn.damageMultiplier);
            } while (_next < _thorns.Length && Mathf.Approximately(_thorns[_next].impact, impact));

            if (!_centered || _hits >= _requiredHits || Mathf.Abs(offset.y) > 2f ||
                multiplier <= 0f || Player.HpCurrent <= 0f) continue;
            float before = Player.HpCurrent;
            HitTarget(multiplier);
            if (Player.HpCurrent >= before || Player.HpCurrent <= 0f || _hits >= _requiredHits) continue;
            if (++_hits < _requiredHits) Player.AddStatus(_slow.Id);
            else
            {
                Player.RemoveStatus(_slow.Id, true);
                Player.AddStatus(_cripple.Id);
                _lockedPlayer = Player;
                _lockedPlayer.EscapeActionsBlocked = true;
            }
        }
    }

    private void Branch()
    {
        _branched = true;
        _step = 0;
        _success = _hits >= _requiredHits;
        _steps = _success ? _SUCCESS : _FAIL;
        if (_success) return;
        Effects.StopSoon();
        if (_failEffects && _failEffects.CanPlay)
            _failEffects.Play(() => PatternTime, _center, transform.rotation, transform.lossyScale.y);
    }

    private void PlayMotion(float time)
    {
        while (_step < _steps.Length && time >= _steps[_step].start)
        {
            SStep step = _steps[_step++];
            float offset = step.from + (time - step.start);
            if (step.blend > 0f) MotionAnimator.CrossFadeInFixedTime(step.state, step.blend, 0, offset);
            else MotionAnimator.PlayInFixedTime(step.state, 0, offset);
        }
    }

    private void Execute(float time)
    {
        if (!_warped && time >= (_WARP_OUT + _WARP_IN) * 0.5f)
        {
            _warped = true;
            _target = Player.transform.position;
            _target.y = transform.position.y;
            Vector3 toward = Vector3.ProjectOnPlane(_target - transform.position, Vector3.up);
            Quaternion facing = toward.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toward) : transform.rotation;
            transform.rotation = facing;
            _facing = facing;
            _approach = facing * Vector3.forward;
            Effects.Move(_TARGET, _target, facing);
            Effects.Move(_WREATH, _target, facing);
        }
        HideBody((time >= _WARP_OUT && time < _WARP_IN) || (time >= _APPROACH_OUT && time < _APPROACH_IN));
        if (_warped) Stand(_target - _approach * ((time < _APPROACH_IN ? _WARP_DISTANCE : _STAB_DISTANCE) * Unit));

        if (!_bound && time >= _bindTime)
        {
            _bound = true;
            HoldTarget();
            BindTarget();
            if (_bind) Player.AddStatus(_bind.Id, _execute - _bindTime + 0.3f);
        }
        else if (_bound && !_executed) HoldTarget();
        if (_bound && !_cineAsked && time >= _cineIn) StartCine();
        SetSlow(_cine && time >= _slowMotion.x && time < _slowMotion.y);
        if (_HIDDEN_COMPANIONS.Count > 0 && time >= _cineOut + _cineReturn) ShowCompanions();
        if (!_executed && time >= _execute)
        {
            _executed = true;
            Player.Kill();
        }
    }

    private void StartCine()
    {
        _cineAsked = true;
        if (!_cineData || Caster is CGamoriDouble) return;
        var cineCamera = FindFirstObjectByType<CGamoriEntranceCamera>();
        if (!cineCamera) return;
        if (_track == null) _track = new CGamoriEntranceTrack(_cineData);
        _cine = true;
        cineCamera.Play(this);
        foreach (CCompanionFollow companion in FindObjectsByType<CCompanionFollow>(FindObjectsSortMode.None))
        {
            foreach (Renderer part in companion.GetComponentsInChildren<Renderer>())
            {
                if (!part.enabled) continue;
                part.enabled = false;
                _HIDDEN_COMPANIONS.Add(part);
            }
        }
    }

    private void HoldTarget()
    {
        Vector3 position = Player.transform.position;
        position.x = _target.x;
        position.z = _target.z;
        Player.transform.position = position;
    }

    private void ShowCompanions()
    {
        foreach (Renderer part in _HIDDEN_COMPANIONS)
        {
            if (part) part.enabled = true;
        }
        _HIDDEN_COMPANIONS.Clear();
    }

    private void SetSlow(bool slow)
    {
        if (slow == _slowed) return;
        _slowed = slow;
        if (slow) _normalSpeed = Time.timeScale;
        Time.timeScale = slow ? _normalSpeed * _slowMotion.z : _normalSpeed;
    }

    private void BindTarget()
    {
        if (!_boundPose) return;
        if (!_boundTarget) _boundTarget = gameObject.AddComponent<CGamoriBoundTarget>();
        float height = _boundTarget.Begin(Player, _boundPose, _boundShader, -_approach, () => PatternTime - _bindTime,
            _STAB - _bindTime, _PULL - _bindTime) * _playerHeight;
        Effects.Resize(_WREATH, height / (_REFERENCE_TARGET_HEIGHT * Unit));
    }

    private void Stand(Vector3 place)
    {
        if (!MotionRoot) return;
        Vector3 local = Quaternion.Inverse(transform.rotation) * (MotionRoot.position - transform.position);
        local.y = 0f;
        Vector3 origin = place - transform.rotation * local;
        origin.y = transform.position.y;
        transform.position = origin;
    }

    private void KeepInside()
    {
        Vector3 offset = Vector3.ProjectOnPlane(Player.transform.position - _center, Vector3.up);
        float limit = _fenceRadius * transform.lossyScale.y - _fenceMargin;
        if (offset.sqrMagnitude <= limit * limit) return;
        Vector3 back = offset.normalized * limit - offset;
        if (Player.TryGetComponent(out CharacterController body) && body.enabled) body.Move(back);
        else Player.transform.position += back;
    }

    protected override void EndPattern()
    {
        SetSlow(false);
        ShowCompanions();
        if (_boundTarget) _boundTarget.End();
        if (_lockedPlayer) _lockedPlayer.EscapeActionsBlocked = false;
        _lockedPlayer = null;
        if (_failEffects) _failEffects.Stop();
        if (!Player) return;
        if (_bound && _bind) Player.RemoveStatus(_bind.Id);
        if (_cripple) Player.RemoveStatus(_cripple.Id);
        if (_slow) Player.RemoveStatus(_slow.Id, true);
        if (_warped) SettleAtMotionRoot();
    }
}
