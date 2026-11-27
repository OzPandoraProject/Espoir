using System;
using UnityEngine;

/// <summary>
/// 문에서 나오는 분신의 위치를 등장 동작에 맞춥니다.
/// </summary>
// 의상과 머리카락의 LateUpdate 전에 분신 위치를 보정한다.
[DefaultExecutionOrder(-200)]
public sealed class CGamoriCloneEntrance : MonoBehaviour
{
    private Animator _animator;
    private Transform _hips, _stage;
    private CGamoriCloneWalk _walk;
    private Func<float> _clock;
    private float _startTime, _clipStart, _clipLength, _blend;
    private int _motionState, _idleState;
    private bool _idling;

    /// <summary>
    /// 문을 기준으로 등장 클립을 재생하고 대기 동작으로 전환합니다.
    /// </summary>
    public void Begin(CGamoriCloneWalk walk, Transform stage, Func<float> clock, float startTime, AnimationClip motion,
        float clipStart, string idleState, float blend)
    {
        foreach (Animator animator in GetComponentsInChildren<Animator>())
        {
            if (animator.enabled) _animator = animator;
        }
        foreach (Transform bone in _animator.GetComponentsInChildren<Transform>())
        {
            if (bone.name == "hips") _hips = bone;
        }
        _walk = walk;
        _stage = stage;
        _clock = clock;
        _startTime = startTime;
        _clipStart = clipStart;
        _clipLength = motion.length;
        _blend = blend;
        _motionState = Animator.StringToHash("Base Layer." + motion.name);
        _idleState = Animator.StringToHash(idleState);
        Aim();
        Sample();
        _animator.Update(0f);
        Place();
    }

    public Transform Hips => _hips;

    private void Aim()
    {
        Vector3 forward = Vector3.ProjectOnPlane(_stage.forward, Vector3.up);
        if (forward.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(forward);
    }

    private void Update()
    {
        if (_clock != null) Sample();
    }

    private void Sample()
    {
        float clipTime = _clipStart + (_clock() - _startTime);
        if (clipTime < _clipLength)
        {
            _animator.speed = 0f;
            _animator.Play(_motionState, 0, clipTime / _clipLength);
            return;
        }
        if (_idling) return;
        _idling = true;
        _animator.speed = 1f;
        _animator.CrossFadeInFixedTime(_idleState, _blend, 0, 0f);
    }

    private void LateUpdate()
    {
        if (_clock != null) Place();
    }

    private void Place()
    {
        Vector3 shift = _walk.Hips(_stage, _clock()) - _hips.position;
        shift.y = 0f;
        transform.position += shift;
    }

    /// <summary>
    /// 분신의 위치 보정을 끝내고 애니메이션 재생 속도를 복원합니다.
    /// </summary>
    public void Finish()
    {
        if (_animator) _animator.speed = 1f;
        _clock = null;
        Destroy(this);
    }
}
