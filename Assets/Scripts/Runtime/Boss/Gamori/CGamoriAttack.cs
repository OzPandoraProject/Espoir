using System.Collections.Generic;
using UnityEngine;

public abstract class CGamoriAttack : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private string _readyState = "Base Layer.Ready";
    [SerializeField, Min(0f)] private float _recoveryBlend = 0.3f;
    [Tooltip("본체가 이 공격을 마친 뒤 다음 공격까지 기다릴 최소 시간(초)")]
    [SerializeField, Min(0f)] private float _minNextAttackDelay;
    [SerializeField, Min(0f)] private float _damageMultiplier = 1f;

    [System.Serializable]
    private struct SPlaybackRange
    {
        public float start, end;
        [Min(0.01f)] public float speed;
        public bool warningOnly;
    }

    [SerializeField] private SPlaybackRange[] _playbackRanges;
    private float _animatorSpeed;
    protected bool UsesPatternClock => _playbackRanges != null && _playbackRanges.Length > 0;

    internal float PatternTime
    {
        get
        {
            float time = Elapsed + _timeOffset;
            if (!UsesPatternClock) return time;
            foreach (SPlaybackRange range in _playbackRanges)
            {
                if (time <= range.start) break;
                float span = (range.end - range.start) / range.speed;
                if (time < range.start + span) return range.start + (time - range.start) * range.speed;
                time -= span - (range.end - range.start);
            }
            return time;
        }
    }

    protected float MotionTimeAt(float patternTime)
    {
        if (!UsesPatternClock) return patternTime;
        foreach (SPlaybackRange range in _playbackRanges)
        {
            if (patternTime < range.start) break;
            if (!range.warningOnly || patternTime >= range.end) continue;

            float remaining = (range.end - patternTime) / range.speed;
            return Mathf.Max(range.start, range.end - remaining);
        }
        return patternTime;
    }

    private float RealTimeAt(float time)
    {
        float result = time;
        if (UsesPatternClock)
        {
            foreach (SPlaybackRange range in _playbackRanges)
                result += Mathf.Clamp(time - range.start, 0f, range.end - range.start) * (1f / range.speed - 1f);
        }
        return result;
    }

    protected void SeekPattern(float time) => _timeOffset = RealTimeAt(time) - Elapsed;

    private readonly List<AnimatorClipInfo> _MOTION_CLIPS = new List<AnimatorClipInfo>(1);
    private float _sampleLength;
    private int _motionState;
    private float _startedAt;
    private float _timeOffset;
    private float _elapsed;
    private Transform _rootBone;

    internal float MinNextAttackDelay => _minNextAttackDelay;
    public bool IsCasting { get; private set; }
    public float Elapsed => IsCasting ? Time.time - _startedAt : _elapsed;
    protected CGamoriCombatant Caster { get; private set; }
    protected CPlayer Player { get; private set; }
    protected Animator MotionAnimator => _animator;
    protected float Scale => transform.lossyScale.y;
    protected bool CanAnimate => _animator != null && _animator.isActiveAndEnabled
        && _animator.runtimeAnimatorController != null;

    internal bool StartCast(CGamoriCombatant boss, CPlayer player)
    {
        if (!isActiveAndEnabled || IsCasting || !CanAnimate) return false;
        if (!_animator.HasState(0, Animator.StringToHash(_readyState))) return false;

        Caster = boss;
        Player = player;
        _startedAt = Time.time;
        _timeOffset = 0f;
        _animatorSpeed = _animator.speed;
        if (UsesPatternClock) _animator.speed = 0f;
        IsCasting = true;
        if (TryStart(player.transform)) return true;

        EndCast();
        return false;
    }

    protected abstract bool TryStart(Transform target);
    protected abstract void OnCancel();

    protected bool TryPlayMotion(string stateName)
    {
        int state = Animator.StringToHash(stateName);
        if (!_animator.HasState(0, state)) return false;
        _motionState = state;
        _sampleLength = 0f;
        _animator.Play(state, 0, 0f);
        return true;
    }

    private void Update()
    {
        if (IsCasting && UsesPatternClock) SamplePatternMotion(MotionTimeAt(PatternTime));
    }

    /// <summary>
    /// 재생 중인 동작의 길이(초)입니다. 타격 순간 느려짐을 쓰는 공격은 이 값으로 동작의 시각을 맞춥니다.
    /// </summary>
    protected virtual float MotionLength => 0f;

    protected virtual void SamplePatternMotion(float time)
    {
        float length = MotionLength;
        if (length <= 0f)
        {
            if (_sampleLength <= 0f)
            {
                if (_animator.GetCurrentAnimatorStateInfo(0).fullPathHash != _motionState) return;
                _animator.GetCurrentAnimatorClipInfo(0, _MOTION_CLIPS);
                if (_MOTION_CLIPS.Count == 0) return;
                _sampleLength = _MOTION_CLIPS[0].clip.length;
            }
            length = _sampleLength;
        }
        if (length > 0f) _animator.Play(_motionState, 0, Mathf.Clamp(time / length, 0f, 0.999f));
    }

    /// <summary>
    /// 공격 종료 위치에 보스를 옮기고 기본 자세로 전환하는 동안 루트 위치를 유지합니다.
    /// </summary>
    protected void SettleAtMotionRoot()
    {
        if (!_animator) return;
        if (!_rootBone)
        {
            foreach (Transform bone in _animator.GetComponentsInChildren<Transform>())
            {
                if (bone.name != "root") continue;
                _rootBone = bone;
                break;
            }
        }
        if (!_rootBone) return;
        Vector3 place = _rootBone.position;
        place.y = transform.position.y;
        if ((place - transform.position).sqrMagnitude < 0.0004f) return;
        transform.position = place;
        if (!TryGetComponent(out CGamoriSettle settle)) settle = gameObject.AddComponent<CGamoriSettle>();
        settle.Hold(_rootBone, place, _recoveryBlend);
    }

    protected void HitTarget() => HitTarget(_damageMultiplier);

    protected void HitTarget(float multiplier)
    {
        if (IsCasting && Caster && Player) Caster.DamagePlayer(Player, multiplier);
    }

    internal void CancelAttack()
    {
        if (!IsCasting) return;
        EndCast(true);
        if (CanAnimate && _animator.HasState(0, Animator.StringToHash(_readyState)))
            _animator.CrossFadeInFixedTime(_readyState, _recoveryBlend, 0, 0f);
    }

    private void EndCast(bool cancelPattern = false)
    {
        if (UsesPatternClock && _animator) _animator.speed = _animatorSpeed;
        _elapsed = Elapsed;
        IsCasting = false;
        if (cancelPattern) OnCancel();
        Player = null;
        Caster = null;
    }

    protected void OnDisable()
    {
        CancelAttack();
    }
}
