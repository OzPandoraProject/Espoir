using UnityEngine;

public sealed class CGamoriStompAttack : CGamoriSequenceAttack, IGamoriCine
{
    // 클립 순서: 왕좌, 복귀 꽃잎, 돌진 경로, 돌진, 마지막 가시, 내려찍기.
    private const int _RETURN = 1, _LANE = 2, _DASH = 3, _FINAL = 4, _LAND = 5;

    [SerializeField] private float _teleport, _stomp, _dash, _burst;
    [SerializeField] private float _stompRadius, _burstRadius;
    // 애니메이션에 포함된 도약, 대상까지의 돌진, 대상을 지난 뒤의 이동 거리(m).
    [SerializeField] private float _leapLength = 8.3329f;
    [SerializeField] private float _dashToTarget = 3.2545f, _dashPast = 5.4659f;
    [SerializeField, Min(1f)] private float _turnSpeed = 540f;
    // 착지 시점을 기준으로 조준 시작과 종료 시각을 역산한다.
    [SerializeField, Min(0f)] private float _leapAimStart = 1.6f, _leapAimLock = 0.5f;
    [SerializeField] private Vector2 _leapRange = new Vector2(3f, 22f);
    [SerializeField, Min(0f)] private float _burstAim = 0.6f;
    [SerializeField] private TextAsset _cineData;
    [SerializeField] private float _cineIn = 2f, _cineOut = 7.05f, _cineReturn = 0.6f;
    private CGamoriEntranceTrack _track;
    private bool _cineAsked, _cine;
    private Vector3 _throne, _landing, _leapDirection, _dashDirection, _burstCenter;
    private Quaternion _rotation;
    private float _leapDistance, _dashDistance;
    private int _step;
    [SerializeField, Min(0f)] private float _dashMultiplier = 4.2f;
    [SerializeField, Range(0f, 1f)] private float _burstHealthRatio = 0.99f;
    [SerializeField] private float _dashHalfWidth = 1f;
    private float _dashProgress;
    private bool _dashHit;
    private bool _leapLocked, _dashLocked, _burstAimed, _burstLocked;

    private float Unit => transform.lossyScale.y * CGamoriEffectClip.UNIT_SCALE;

    public bool IsPlaying => IsCasting && _cine && PatternTime < _cineOut + _cineReturn;

    public float PlayTime => PatternTime;

    public float CameraOut => _cineOut;

    public float ReturnTime => _cineReturn;

    public CGamoriEntranceTrack Track => _track;

    /// <inheritdoc/>
    public Vector3 ToWorld(Vector3 localPosition) =>
        _throne + _rotation * (new Vector3(-localPosition.x, localPosition.y, localPosition.z) * Unit);

    /// <inheritdoc/>
    public Vector3 ToWorldDirection(Vector3 localDirection) =>
        _rotation * new Vector3(-localDirection.x, localDirection.y, localDirection.z);

    private void Start()
    {
        TryGetThroneFront(out _throne, out _rotation);
    }

    protected override void GetEffectPose(out Vector3 origin, out Quaternion rotation)
    {
        origin = _throne;
        rotation = _rotation;
    }

    protected override void BeginPattern()
    {
        _step = 0;
        _dashProgress = 0f;
        _dashDirection = Vector3.zero;
        _dashHit = false;
        _leapLocked = _dashLocked = _burstAimed = _burstLocked = false;
        _cineAsked = _cine = false;
        _leapDirection = _rotation * Vector3.forward;
        _leapDistance = _leapLength * Unit;
        _landing = _throne + _leapDirection * _leapDistance;
    }

    protected override void UpdatePattern(float time)
    {
        if (_step == 0)
        {
            PlaceReturn(_RETURN, _throne);
            if (time < _teleport) return;
            transform.SetPositionAndRotation(_throne, _rotation);
            _step++;
        }

        if (!_cineAsked && time >= _cineIn) StartCine();

        if (!_leapLocked && time >= _stomp - _leapAimStart)
        {
            Vector3 toward = Vector3.ProjectOnPlane(Player.transform.position - _throne, Vector3.up);
            if (toward.sqrMagnitude > 0.01f)
            {
                _leapDirection = toward.normalized;
                _leapDistance = Mathf.Clamp(toward.magnitude, _leapRange.x, _leapRange.y);
            }
            _landing = _throne + _leapDirection * _leapDistance;
            _leapLocked = time >= _stomp - _leapAimLock;
        }
        Quaternion leapFacing = Quaternion.LookRotation(_leapDirection);
        Effects.Move(_LAND, _landing, leapFacing);
        Effects.SetPathLength(_LAND, _leapDistance / (_leapLength * Unit));

        if (_step == 1 && time >= _stomp)
        {
            if (IsInCircle(_landing, _stompRadius)) HitTarget();
            _step++;
        }

        if (_step == 2 && !_dashLocked)
        {
            Vector3 toward = Vector3.ProjectOnPlane(Player.transform.position - _landing, Vector3.up);
            if (toward.sqrMagnitude > 0.01f)
            {
                _dashDirection = toward.normalized;
                _dashDistance = Mathf.Max(toward.magnitude, 0.5f);
            }
            else if (_dashDirection == Vector3.zero)
            {
                _dashDirection = _leapDirection;
                _dashDistance = _dashToTarget * Unit;
            }
            Effects.Move(_LANE, _landing, Quaternion.LookRotation(_dashDirection));
            Effects.Stretch(_LANE, (_dashDistance + _dashPast * Unit) / ((_dashToTarget + _dashPast) * Unit));
            if (time >= _dash)
            {
                _dashLocked = true;
                _burstCenter = _landing + _dashDirection * _dashDistance;
                Effects.Move(_DASH, _burstCenter, Quaternion.LookRotation(_dashDirection));
            }
        }

        Follow();

        if (_dashLocked && !_burstLocked)
        {
            if (!_burstAimed)
            {
                _burstCenter = Player.transform.position;
                _burstCenter.y = transform.position.y;
                Effects.Move(_FINAL, _burstCenter, transform.rotation);
                _burstAimed = time >= _burst - _burstAim;
            }
            if (time >= _burst)
            {
                _burstLocked = true;
                if (IsInCircle(_burstCenter, _burstRadius)) Player.GetDamage(_burstHealthRatio, ERatioSwitch.ByMax);
            }
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
    }

    private void Follow()
    {
        if (!MotionRoot) return;
        Vector3 local = Quaternion.Inverse(transform.rotation) * (MotionRoot.position - transform.position);
        float unit = Unit, leap = _leapLength * unit, reach = _dashToTarget * unit;
        float along = local.z;
        Vector3 place, heading;
        if (_step < 2 || along <= leap)
        {
            place = _throne + _leapDirection * (Mathf.Clamp01(along / leap) * _leapDistance);
            heading = _step < 2 ? _leapDirection : _dashDirection;
        }
        else
        {
            float past = along - leap;
            float mapped = past <= reach ? past * _dashDistance / reach : _dashDistance + (past - reach);
            place = _landing + _dashDirection * mapped;
            if (_dashLocked && !_dashHit && mapped > _dashProgress)
            {
                Vector3 offset = Player.transform.position - _landing;
                float nearest = Mathf.Clamp(Vector3.Dot(offset, _dashDirection), _dashProgress, mapped);
                Vector3 side = Vector3.ProjectOnPlane(offset, Vector3.up) - nearest * _dashDirection;
                if (Mathf.Abs(offset.y) < 2f && side.magnitude <= _dashHalfWidth * Scale)
                {
                    HitTarget(_dashMultiplier);
                    _dashHit = true;
                }
                _dashProgress = mapped;
            }
            heading = _dashDirection;
        }
        if (heading == Vector3.zero) heading = _leapDirection;
        Quaternion facing = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(heading),
            _turnSpeed * Time.deltaTime);
        Vector3 flat = new Vector3(local.x, 0f, local.z);
        Vector3 origin = place - facing * flat;
        origin.y = _throne.y;
        transform.SetPositionAndRotation(origin, facing);
    }

    protected override bool SettlesAtEnd => false;

    protected override void EndPattern()
    {
        if (_step >= 1 && MotionRoot) SettleAtMotionRoot();
    }

    private bool IsInCircle(Vector3 center, float radius)
    {
        Vector3 offset = Player.transform.position - center;
        radius *= Scale;
        return Mathf.Abs(offset.y) < 2f && offset.x * offset.x + offset.z * offset.z <= radius * radius;
    }
}
