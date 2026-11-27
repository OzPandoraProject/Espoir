using UnityEngine;

/// <summary>
/// 제물을 소환하고 살아남은 수에 따라 보스의 체력을 회복합니다.
/// </summary>
public sealed class CGamoriDrainAttack : CGamoriSequenceAttack
{
    [System.Serializable]
    private struct SOffering
    {
        public Vector3 offset;
        public float spawn, absorb;
        public float height;
    }

    // 흡수 클립 순서: 왕좌, 대상, 복귀 꽃잎, 각 제물의 흡수 꽃잎.
    private const int _STRIKE = 1, _RETURN = 2, _ABSORB = 3;

    [SerializeField] private CGamoriOffering _offering;
    [SerializeField] private CStatusSO _shield;
    [SerializeField] private SOffering[] _placements;
    [SerializeField] private float _teleport, _shieldTime, _openTime;
    [SerializeField, Min(1f)] private float _offeringHp = 800f;
    [SerializeField, Range(0f, 1f)] private float _healRatio;
    [SerializeField] private float _slamTime = 1.2f;
    [SerializeField] private float _slamRadius = 9.82f;
    [SerializeField, Range(0f, 180f)] private float _slamHalfAngle = 60f;
    [SerializeField] private Vector3 _slamOrigin = new Vector3(0.165f, 0f, -0.506f);
    // 왕좌로 끌어당기는 시각과 쓰러지는 시각은 흡수 완료 시점에서 역산한다.
    [SerializeField] private float _pullLead = 2.2f, _fallLead = 1.7f;

    [SerializeField] private float _stageCenter = 11.125f, _stageRadius = 13.6f;
    [SerializeField] private float _fleeNear = 6.5f, _fleeFar = 40f;
    [SerializeField] private float _absorbWithin = 40f;
    [SerializeField, Min(1f)] private float _petalGrow = 1.35f;

    [SerializeField] private float _winOut = 27.19f, _winIn = 27.3f, _winHit = 27.66f;

    [Header("흡수 실패")]
    [SerializeField] private CGamoriEffectClip _failEffects;
    [SerializeField] private AnimationClip[] _failMotions;
    [SerializeField] private float[] _failMotionStarts;
    [SerializeField] private float _failFrom = 7.4f, _failUntil = 15.4f;
    [SerializeField] private float _failStart = 15.4f, _failOpen = 16.75f, _failOut = 19.14f, _failIn = 19.25f;
    [SerializeField] private float _failHit = 19.61f, _failEnd = 21.35f;
    [SerializeField] private float _seatTime = 5.3f, _standTime = 25.65f, _failStandTime = 17.65f;

    [Header("마무리 공격")]
    [SerializeField, InspectorName("순간이동 거리")] private float _failDistance = 1.3586f;
    [SerializeField, InspectorName("타격 반경")] private float _failRadius = 2.2828f;
    [SerializeField, InspectorName("왕좌 쪽 타격 중심 오프셋")] private float _failCenter = 0.2175f;
    [SerializeField, InspectorName("경고 선행 시간 (초)")] private float _failWarnLead = 0.7f;
    [SerializeField] private float _strikeMultiplier = 6.4f;

    private CGamoriSeatDrape _drape;
    private CGamoriOffering[] _units;
    private bool[] _absorbed;
    private Transform _hand;
    private bool _offeringsProtected;
    private int[] _failStates;
    private Vector3 _throne, _strikeCenter;
    private Quaternion _rotation;
    private int _nextOffering, _failMotion;
    private bool _atThrone, _shielded, _slammed, _failed, _strikeLocked, _strikeHit;

    private float Unit => Scale * CGamoriEffectClip.UNIT_SCALE;

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
        _units = new CGamoriOffering[_placements.Length];
        _absorbed = new bool[_placements.Length];
        _nextOffering = _failMotion = 0;
        _atThrone = _slammed = _failed = _strikeLocked = _strikeHit = _offeringsProtected = false;
        if (!_drape) _drape = GetComponentInChildren<CGamoriSeatDrape>();
    }

    protected override void UpdatePattern(float time)
    {
        if (!_atThrone)
        {
            PlaceReturn(_RETURN, _throne);
            if (time >= _teleport)
            {
                transform.SetPositionAndRotation(_throne, _rotation);
                _atThrone = true;
            }
        }
        if (!_slammed && time >= _slamTime) Slam();
        SpawnOfferings(time);
        if (_drape) _drape.SetSeated(time >= _seatTime && PatternTime < (_failed ? _failStandTime : _standTime));

        if (!_shielded && !_failed && time >= _shieldTime && time < _openTime)
        {
            Caster.AddStatus(_shield.Id, _openTime - time);
            _shielded = true;
        }

        if (!_failed && time >= _failFrom && time < _failUntil && AllFallen())
        {
            if (!_failEffects || !_failEffects.isActiveAndEnabled || !_failEffects.CanPlay ||
                !HasMotions(_failMotions, _failMotionStarts))
            {
                CancelAttack();
                return;
            }
            BeginFailedAbsorption();
        }
        if (_failed)
        {
            UpdateFailedAbsorption(PatternTime);
            return;
        }

        AbsorbOfferings(time);
        if (_shielded && time >= _openTime) ClearShield();
        Strike(Effects, time, _winOut, _winIn, _winHit);
    }

    private void Slam()
    {
        _slammed = true;
        Vector3 offset = Player.transform.position - transform.TransformPoint(_slamOrigin);
        float height = Mathf.Abs(offset.y);
        offset.y = 0f;
        if (height < 2f && offset.magnitude <= _slamRadius * Scale
            && Vector3.Angle(offset, transform.forward) <= _slamHalfAngle) HitTarget();
    }

    private void SpawnOfferings(float time)
    {
        while (_nextOffering < _placements.Length && time >= _placements[_nextOffering].spawn)
        {
            int index = _nextOffering++;
            SOffering placement = _placements[index];
            CGamoriOffering unit = Instantiate(_offering, transform.TransformPoint(placement.offset), _rotation);
            unit.SetHealth(_offeringHp);
            unit.Appear(placement.height * Unit, 1.9f + index * 2.3f, Player.transform.position);
            unit.Flee(_throne + _rotation * Vector3.forward * _stageCenter, _stageRadius,
                _throne, _fleeNear, _fleeFar, Player.transform);
            _units[index] = unit;
        }
    }

    private bool AllFallen()
    {
        if (_nextOffering < _placements.Length) return false;
        foreach (CGamoriOffering unit in _units)
        {
            if (unit && unit.IsAlive) return false;
        }
        return true;
    }

    private void AbsorbOfferings(float time)
    {
        if (!_offeringsProtected && time >= _failUntil)
        {
            _offeringsProtected = true;
            foreach (CGamoriOffering unit in _units)
            {
                if (unit && unit.IsAlive) unit.SetSafe();
            }
        }
        for (int i = 0; i < _units.Length; i++)
        {
            CGamoriOffering unit = _units[i];
            float absorb = _placements[i].absorb;
            if (unit && unit.IsAlive)
            {
                if (time >= absorb - _pullLead) unit.Pull(_throne, _absorbWithin);
                if (time >= absorb - _fallLead)
                {
                    unit.Absorb();
                    AimPetals(i, unit.transform.position);
                    _absorbed[i] = true;
                }
            }
            else if (i < _nextOffering && !_absorbed[i] && time < absorb) Effects.StopOne(_ABSORB + i);
            if (!_absorbed[i] || time < absorb) continue;
            Caster.GetHeal(_healRatio, ERatioSwitch.ByMax);
            _absorbed[i] = false;
        }
    }

    // 왼손을 고정한 채 꽃잎의 시작점만 제물 위치에 맞춘다.
    private void AimPetals(int index, Vector3 place)
    {
        if (!_hand)
        {
            foreach (Transform bone in GetComponentsInChildren<Transform>())
            {
                if (bone.name != "handL") continue;
                _hand = bone;
                break;
            }
        }
        Vector3 hand = _hand ? _hand.position : _throne + Vector3.up * (1.5f * Unit);
        Vector3 recorded = transform.TransformPoint(_placements[index].offset) - hand, now = place - hand;
        if (recorded.sqrMagnitude < 0.01f || now.sqrMagnitude < 0.01f) return;
        Effects.Aim(_ABSORB + index, hand, recorded, now, _petalGrow);
    }

    private void BeginFailedAbsorption()
    {
        _failed = true;
        SeekPattern(_failStart);
        Effects.StopSoon();
        _failEffects.Play(() => PatternTime, _throne, _rotation, Scale);
        if (_failStates != null) return;
        _failStates = new int[_failMotions.Length];
        for (int i = 0; i < _failMotions.Length; i++)
        {
            _failStates[i] = Animator.StringToHash("Base Layer." + _failMotions[i].name);
        }
    }

    private void UpdateFailedAbsorption(float time)
    {
        if (_shielded && time >= _failOpen) ClearShield();
        Strike(_failEffects, time, _failOut, _failIn, _failHit);
        if (time >= _failEnd) CancelAttack();
    }

    private void Strike(CGamoriEffectClip effects, float time, float outTime, float inTime, float hitTime)
    {
        HideBody(time >= outTime && time < inTime);
        if (_strikeLocked || time < outTime - _failWarnLead)
        {
            if (!_strikeHit && time >= hitTime) StrikeHit();
            return;
        }

        Vector3 target = Player.transform.position;
        target.y = _throne.y;
        Vector3 away = Vector3.ProjectOnPlane(target - _throne, Vector3.up);
        Quaternion outward = away.sqrMagnitude > 0.01f ? Quaternion.LookRotation(away) : _rotation;
        _strikeCenter = target - outward * Vector3.forward * (_failCenter * Unit);
        effects.Move(_STRIKE, _strikeCenter, outward);

        if (time >= (outTime + inTime) * 0.5f)
        {
            _strikeLocked = true;
            Quaternion facing = outward * Quaternion.Euler(0f, 180f, 0f);
            Vector3 place = target + outward * Vector3.forward * (_failDistance * Unit);
            Vector3 local = MotionRoot ? Quaternion.Inverse(transform.rotation)
                * (MotionRoot.position - transform.position)
                : Vector3.zero;
            local.y = 0f;
            transform.SetPositionAndRotation(place - facing * local, facing);
        }
    }

    private void StrikeHit()
    {
        _strikeHit = true;
        Vector3 offset = Player.transform.position - _strikeCenter;
        float reach = _failRadius * Unit;
        if (Mathf.Abs(offset.y) < 2f && offset.x * offset.x + offset.z * offset.z <=
            reach * reach) HitTarget(_strikeMultiplier);
    }

    protected override void SamplePatternMotion(float time)
    {
        if (!_failed || _failStates == null)
        {
            base.SamplePatternMotion(time);
            return;
        }
        while (_failMotion + 1 < _failMotions.Length && time >= _failMotionStarts[_failMotion + 1]) _failMotion++;
        float phase = (time - _failMotionStarts[_failMotion]) / _failMotions[_failMotion].length;
        MotionAnimator.Play(_failStates[_failMotion], 0, Mathf.Clamp(phase, 0f, 0.999f));
    }

    private void ClearShield()
    {
        Caster.RemoveStatus(_shield.Id);
        _shielded = false;
    }

    protected override void EndPattern()
    {
        HideBody(false);
        if (_drape) _drape.SetSeated(false);
        if (_shielded) ClearShield();
        if (_failEffects) _failEffects.Stop();
        if (_units == null) return;
        foreach (CGamoriOffering unit in _units)
        {
            if (unit && unit.IsAlive) Destroy(unit.gameObject);
        }
    }
}
