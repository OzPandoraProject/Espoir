using System.Collections.Generic;
using UnityEngine;

public abstract class CGamoriSequenceAttack : CGamoriAttack
{
    [SerializeField] private CGamoriEffectClip _effects;
    [SerializeField] private AnimationClip[] _motions;
    [SerializeField] private float[] _motionStarts;
    [SerializeField] private float _duration;
    [SerializeField] private Transform _motionRoot;
    [SerializeField, Min(0f)] private float _vanish;
    [SerializeField, Min(0f)] private float _appear;
    // 복귀 꽃잎 클립의 원래 길이(m).
    [SerializeField, Min(0f)] private float _returnDistance;
    private Vector3 _startPlace;
    private readonly List<SkinnedMeshRenderer> _HIDDEN_BODY = new List<SkinnedMeshRenderer>();
    private bool _hidden, _appeared;
    private int _motion;
    private int[] _stateHashes;

    protected CGamoriEffectClip Effects => _effects;
    protected Transform MotionRoot => _motionRoot;
    protected float AppearTime => _appear;

    /// <summary>
    /// 왕좌 앞 자리와 방향입니다. 기준점이 없으면 현재 위치와 방향을 사용합니다.
    /// </summary>
    protected bool TryGetThroneFront(out Vector3 place, out Quaternion facing)
    {
        if (TryGetComponent(out CGamoriBoss boss) && boss.ThroneFront)
        {
            boss.ThroneFront.GetPositionAndRotation(out place, out facing);
            return true;
        }
        transform.GetPositionAndRotation(out place, out facing);
        return false;
    }

    protected override bool TryStart(Transform target)
    {
        if (!_effects || !_effects.isActiveAndEnabled || !_effects.CanPlay || !HasMotions(_motions, _motionStarts))
            return false;
        if (!CanBegin() || !TryPlayMotion("Base Layer." + _motions[0].name)) return false;
        if (UsesPatternClock && _stateHashes == null)
        {
            _stateHashes = new int[_motions.Length];
            for (int i = 0; i < _motions.Length; i++)
                _stateHashes[i] = Animator.StringToHash("Base Layer." + _motions[i].name);
        }
        _motion = 0;
        _appeared = false;
        _startPlace = transform.position;
        BeginPattern();
        GetEffectPose(out Vector3 origin, out Quaternion rotation);
        _effects.Play(() => PatternTime, origin, rotation, transform.lossyScale.y);
        return true;
    }

    protected static bool HasMotions(AnimationClip[] clips, float[] starts)
    {
        if (clips == null || starts == null || clips.Length == 0 || clips.Length != starts.Length) return false;
        foreach (AnimationClip clip in clips)
        {
            if (!clip) return false;
        }
        return true;
    }

    protected override void SamplePatternMotion(float time)
    {
        while (_motion + 1 < _motions.Length && time >= _motionStarts[_motion + 1]) _motion++;
        AnimationClip clip = _motions[_motion];
        // 반복 클립이 첫 프레임으로 돌아가는 것을 막는다.
        float phase = Mathf.Clamp((time - _motionStarts[_motion]) / clip.length, 0f, 0.999f);
        MotionAnimator.Play(_stateHashes[_motion], 0, phase);
    }

    private void LateUpdate()
    {
        if (!IsCasting) return;
        if (!Player || !Player.gameObject.activeInHierarchy || (Player.HpCurrent <= 0f && !ContinueAfterPlayerDeath))
        {
            CancelAttack();
            return;
        }
        float t = PatternTime;
        HoldStartPlace(t);
        while (!UsesPatternClock && _motion + 1 < _motions.Length && t >= _motionStarts[_motion + 1])
            TryPlayMotion("Base Layer." + _motions[++_motion].name);
        UpdatePattern(t);
        if (!IsCasting) return;
        if (t >= _duration) CancelAttack();
    }

    /// <summary>
    /// 공격 시작 위치와 도착 지점 사이에 복귀 이펙트를 맞춥니다.
    /// </summary>
    protected void PlaceReturn(int index, Vector3 arrival, bool startBehind = false)
    {
        Vector3 away = Vector3.ProjectOnPlane(_startPlace - arrival, Vector3.up);
        Quaternion facing = transform.rotation;
        if (away.sqrMagnitude > 0.01f) facing = Quaternion.LookRotation(startBehind ? -away : away);
        _effects.Move(index, arrival, facing);
        float recorded = _returnDistance * transform.lossyScale.y * CGamoriEffectClip.UNIT_SCALE;
        if (recorded > 0.01f) _effects.SetPathLength(index, away.magnitude / recorded);
    }

    // 클립 원점이 순간이동의 도착 위치이므로 사라지기 전까지 원래 위치를 유지한다.
    private void HoldStartPlace(float time)
    {
        if (_appear <= 0f) return;
        if (time >= _vanish && time < _appear) HideBody(true);
        else if (time >= _appear && !_appeared)
        {
            _appeared = true;
            HideBody(false);
        }
        if (time >= _appear || !_motionRoot) return;
        Vector3 displacement = Vector3.ProjectOnPlane(_motionRoot.position - transform.position, Vector3.up);
        _motionRoot.position -= displacement;
    }

    /// <summary>
    /// 순간이동으로 사라져 있는 동안 몸을 감추거나 다시 보입니다.
    /// </summary>
    protected void HideBody(bool hidden)
    {
        if (hidden == _hidden) return;
        _hidden = hidden;
        if (hidden)
        {
            GetComponentsInChildren(true, _HIDDEN_BODY);
            for (int i = _HIDDEN_BODY.Count - 1; i >= 0; i--)
            {
                if (_HIDDEN_BODY[i].enabled) _HIDDEN_BODY[i].enabled = false;
                else _HIDDEN_BODY.RemoveAt(i);
            }
            return;
        }
        foreach (SkinnedMeshRenderer part in _HIDDEN_BODY)
        {
            if (part) part.enabled = true;
        }
        _HIDDEN_BODY.Clear();
    }

    protected void Face(Vector3 point)
    {
        Vector3 forward = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
        if (forward.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(forward);
    }

    protected virtual bool ContinueAfterPlayerDeath => false;
    protected virtual bool SettlesAtEnd => true;
    protected virtual bool CanBegin() => true;
    protected virtual void BeginPattern() { }

    /// <summary>
    /// 이펙트를 재생할 월드 위치와 회전을 반환합니다.
    /// </summary>
    protected virtual void GetEffectPose(out Vector3 origin, out Quaternion rotation)
    {
        origin = transform.position;
        rotation = transform.rotation;
    }
    protected abstract void UpdatePattern(float time);
    protected virtual void EndPattern() { }

    protected override void OnCancel()
    {
        HideBody(false);
        EndPattern();
        if (SettlesAtEnd) SettleAtMotionRoot();
        if (_effects) _effects.Stop();
    }
}
