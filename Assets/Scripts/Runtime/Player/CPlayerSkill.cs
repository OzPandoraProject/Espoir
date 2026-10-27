using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CPlayerMovement))]
public class CPlayerSkill : MonoBehaviour
{
    private class CSkillRuntime
    {
        public CSkillSO data;
        public int startTriggerHash;
        public int comboTriggerHash;
        public int[] stateHashes;
        public int[] recoverStateHashes;
        public float readyTime;
    }

    [Header("스킬 설정")]
    [SerializeField] private CSkillSO _basicAttack;
    [SerializeField] private CSkillSO _dash;
    [SerializeField] private CPlayerAttack _playerAttack;
    [SerializeField] private Animator _animator;
    [SerializeField, Min(0f)] private float _cancelTransitionDuration = 0.05f;

    private static readonly int _IDLE_HASH = Animator.StringToHash("Idle");
    private readonly Dictionary<CSkillSO, CSkillRuntime> _SKILLS = new Dictionary<CSkillSO, CSkillRuntime>();
    private readonly List<ParticleSystem> _EFFECT_PARTICLES = new List<ParticleSystem>();

    private CUnit _owner;
    private CPlayerMovement _movement;
    private CSkillRuntime _currentSkill;
    private int _stepIndex = -1;
    private int _stateCheckFrame;
    private int _nextHitIndex;
    private bool _hasQueuedCombo;
    private Vector3 _direction;
    private Vector3 _queuedDirection;
    private float _moveProgress;
    private float _dashTimeRemaining;
    private bool _hasSpawnedEffect;
    private float _hitStopRemaining;
    private float _animatorSpeed;

    public bool IsBusy => _currentSkill != null;

    private void OnDisable()
    {
        CancelSkill();
    }

    /// <summary>
    /// 사용 주체와 기본 스킬 연결을 확인합니다.
    /// </summary>

    public bool Initialize(CUnit owner)
    {
        _owner = owner;
        _movement = GetComponent<CPlayerMovement>();

        return _animator != null && _playerAttack != null &&
            _basicAttack != null && _basicAttack.Type == ESkillType.Melee &&
            _dash != null && _dash.Type == ESkillType.Dash &&
            _animator.HasState(0, _IDLE_HASH) && RegisterSkill(_basicAttack) && RegisterSkill(_dash);
    }

    /// <summary>
    /// 스킬 설정을 검사하고 캐릭터별 실행 정보를 준비합니다.
    /// </summary>

    public bool RegisterSkill(CSkillSO skill)
    {
        if (skill == null)
        {
            return false;
        }

        if (_SKILLS.ContainsKey(skill))
        {
            return true;
        }

        if (!HasValidSettings(skill))
        {
            return false;
        }

        CSkillRuntime runtime = new CSkillRuntime
        {
            data = skill,
            startTriggerHash = Animator.StringToHash(skill.StartTrigger),
            comboTriggerHash = string.IsNullOrWhiteSpace(skill.ComboTrigger) ? 0 :
                Animator.StringToHash(skill.ComboTrigger),
            stateHashes = new int[skill.Steps.Length],
            recoverStateHashes = new int[skill.Steps.Length]
        };

        for (int i = 0; i < skill.Steps.Length; i++)
        {
            runtime.stateHashes[i] = Animator.StringToHash(skill.Steps[i].stateName);

            if (!string.IsNullOrWhiteSpace(skill.Steps[i].recoverStateName))
            {
                runtime.recoverStateHashes[i] = Animator.StringToHash(skill.Steps[i].recoverStateName);
            }
        }

        _SKILLS.Add(skill, runtime);
        return true;
    }

    /// <summary>
    /// 기본 공격을 시작하거나 다음 연격의 방향을 예약합니다.
    /// </summary>

    public bool TryAttack(Vector3 direction)
    {
        return TryUse(_basicAttack, direction);
    }

    /// <summary>
    /// 대쉬를 사용할 수 있을 때 현재 행동을 취소하고 전방으로 대쉬합니다.
    /// </summary>

    public bool TryDash()
    {
        return TryUse(_dash, transform.forward);
    }

    /// <summary>
    /// 재사용 시간을 확인한 뒤 스킬을 시작합니다.
    /// </summary>

    public bool TryUse(CSkillSO skill, Vector3 direction)
    {
        if (_owner == null || _owner.HpCurrent <= 0f || !isActiveAndEnabled ||
            skill == null || !_SKILLS.TryGetValue(skill, out CSkillRuntime runtime))
        {
            return false;
        }

        if (_currentSkill == runtime && skill.Type == ESkillType.Melee)
        {
            return QueueCombo(direction);
        }

        if (Time.time < runtime.readyTime)
        {
            return false;
        }

        bool isCanceling = IsBusy;

        if (isCanceling && (skill.Type != ESkillType.Dash || _currentSkill.data.Type == ESkillType.Dash))
        {
            return false;
        }

        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
        bool canContinue = skill.Type == ESkillType.Melee && runtime.comboTriggerHash != 0 &&
            GetStateIndex(runtime.recoverStateHashes, state.shortNameHash) >= 0 &&
            state.normalizedTime * state.length <= skill.ComboGraceTime;

        FinishSkill();
        _currentSkill = runtime;
        _stateCheckFrame = Time.frameCount + 1;
        runtime.readyTime = Time.time + skill.Cooldown;
        _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        _dashTimeRemaining = skill.DashDuration;

        if (isCanceling)
        {
            _animator.CrossFadeInFixedTime(runtime.stateHashes[0], _cancelTransitionDuration);
        }

        else
        {
            _animator.SetTrigger(canContinue ? runtime.comboTriggerHash : runtime.startTriggerHash);
        }

        return true;
    }

    /// <summary>
    /// 현재 스킬의 이동과 타격 시점을 진행합니다.
    /// </summary>

    public void UpdateSkill()
    {
        if (!IsBusy)
        {
            return;
        }

        if (_stepIndex < 0 && _animator.updateMode == AnimatorUpdateMode.Normal &&
            _animator.cullingMode == AnimatorCullingMode.AlwaysAnimate)
        {
            if (!_animator.isActiveAndEnabled || _animator.speed <= 0f || Time.deltaTime <= 0f ||
                _animator.IsInTransition(0))
            {
                _stateCheckFrame = Time.frameCount + 1;
            }

            else if (Time.frameCount > _stateCheckFrame &&
                GetStateIndex(_currentSkill.stateHashes, _animator.GetCurrentAnimatorStateInfo(0).shortNameHash) < 0)
            {
                CLogger.Error($"{name}: 스킬 애니메이션의 진입 전환을 확인해주세요.");
                CancelSkill();
                return;
            }
        }

        if (_hitStopRemaining > 0f)
        {
            _hitStopRemaining = Mathf.Max(_hitStopRemaining - Time.unscaledDeltaTime, 0f);

            if (_hitStopRemaining > 0f)
            {
                return;
            }

            _animator.speed = _animatorSpeed;
        }

        if (_currentSkill.data.Type == ESkillType.Dash)
        {
            UpdateDash();
        }

        else
        {
            UpdateAttack();
        }
    }

    /// <summary>
    /// 예약 타격과 히트스톱을 취소하고 대기 자세로 돌아갑니다.
    /// </summary>

    public void CancelSkill()
    {
        bool wasBusy = IsBusy;
        FinishSkill();

        if (wasBusy && _animator != null && _animator.isActiveAndEnabled)
        {
            _animator.CrossFadeInFixedTime(_IDLE_HASH, _cancelTransitionDuration);
        }
    }

    private bool QueueCombo(Vector3 direction)
    {
        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
        int index = GetStateIndex(_currentSkill.stateHashes, state.shortNameHash);

        if (_currentSkill.comboTriggerHash == 0 || index < 0 || index >= _currentSkill.data.Steps.Length - 1 ||
            _animator.IsInTransition(0))
        {
            return false;
        }

        if (index != _stepIndex)
        {
            EnterStep(index);
        }

        _queuedDirection = direction;
        _hasQueuedCombo = true;
        _animator.SetTrigger(_currentSkill.comboTriggerHash);
        return true;
    }

    private void UpdateAttack()
    {
        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
        int index = GetStateIndex(_currentSkill.stateHashes, state.shortNameHash);

        if (index < 0)
        {
            if (_stepIndex >= 0 && !_animator.IsInTransition(0))
            {
                FinishSkill();
            }

            else
            {
                _movement.Rotate(_direction, _currentSkill.data.RotationSpeed);
            }

            return;
        }

        if (index != _stepIndex)
        {
            EnterStep(index);
        }

        _movement.Rotate(_direction, _currentSkill.data.RotationSpeed);
        SSkillStep step = _currentSkill.data.Steps[index];
        float progress = Mathf.InverseLerp(step.moveStart, step.moveEnd, state.normalizedTime);
        float distance = Mathf.Max(progress - _moveProgress, 0f) * step.moveDistance;
        _movement.MoveOnGround(transform.forward * distance);
        _moveProgress = progress;
        UpdateEffect(step, state);

        while (_nextHitIndex < step.hits.Length && state.normalizedTime >= step.hits[_nextHitIndex].time)
        {
            SSkillHit hit = step.hits[_nextHitIndex];
            _nextHitIndex++;

            if (_playerAttack.Attack(_owner, hit) && hit.hitStopDuration > 0f)
            {
                HitStop(hit.hitStopDuration);
                break;
            }
        }
    }

    private void UpdateDash()
    {
        CSkillSO skill = _currentSkill.data;
        float moveTime = Mathf.Min(Time.deltaTime, _dashTimeRemaining);
        float speed = skill.Steps[0].moveDistance / skill.DashDuration;
        _movement.MoveOnGround(_direction * (speed * moveTime));
        _dashTimeRemaining = Mathf.Max(_dashTimeRemaining - moveTime, 0f);

        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);

        if (state.shortNameHash == _currentSkill.stateHashes[0])
        {
            if (_stepIndex < 0)
            {
                EnterStep(0);
            }

            UpdateEffect(skill.Steps[0], state);
        }

        else if (_stepIndex >= 0 && !_animator.IsInTransition(0) && _dashTimeRemaining <= 0f)
        {
            FinishSkill();
        }
    }

    private void EnterStep(int index)
    {
        if (_hasQueuedCombo)
        {
            _direction = _queuedDirection;
        }

        _hasQueuedCombo = false;
        _stepIndex = index;
        _nextHitIndex = 0;
        _moveProgress = 0f;
        _hasSpawnedEffect = false;
    }

    private void UpdateEffect(SSkillStep step, AnimatorStateInfo state)
    {
        if (_hasSpawnedEffect || step.effectPrefab == null || state.normalizedTime < step.effectTime)
        {
            return;
        }

        _hasSpawnedEffect = true;
        float elapsed = (state.normalizedTime - step.effectTime) * state.length;

        if (elapsed >= step.effectLifetime)
        {
            return;
        }

        GameObject effect = Instantiate(step.effectPrefab, transform.position, transform.rotation);

        if (elapsed > 0f)
        {
            effect.GetComponentsInChildren(false, _EFFECT_PARTICLES);

            foreach (ParticleSystem particle in _EFFECT_PARTICLES)
            {
                particle.Simulate(elapsed, false, true, false);
                particle.Play(false);
            }

            _EFFECT_PARTICLES.Clear();
        }

        Destroy(effect, step.effectLifetime - elapsed);
    }

    private void FinishSkill()
    {
        if (_hitStopRemaining > 0f)
        {
            _animator.speed = _animatorSpeed;
            _hitStopRemaining = 0f;
        }

        if (_currentSkill != null)
        {
            _animator.ResetTrigger(_currentSkill.startTriggerHash);

            if (_currentSkill.comboTriggerHash != 0)
            {
                _animator.ResetTrigger(_currentSkill.comboTriggerHash);
            }
        }

        _hasSpawnedEffect = false;
        _currentSkill = null;
        _stepIndex = -1;
        _stateCheckFrame = 0;
        _nextHitIndex = 0;
        _hasQueuedCombo = false;
        _moveProgress = 0f;
        _dashTimeRemaining = 0f;

        if (_movement != null)
        {
            _movement.Stop();
        }
    }

    private void HitStop(float duration)
    {
        _hitStopRemaining = duration;
        _animatorSpeed = _animator.speed;
        _animator.speed = 0f;
    }

    private int GetStateIndex(int[] hashes, int hash)
    {
        for (int i = 0; i < hashes.Length; i++)
        {
            if (hashes[i] != 0 && hashes[i] == hash)
            {
                return i;
            }
        }

        return -1;
    }

    private bool HasValidSettings(CSkillSO skill)
    {
        if (skill.Steps == null || skill.Steps.Length == 0 || skill.Cooldown < 0f || skill.RotationSpeed < 0f ||
            skill.ComboGraceTime < 0f || !HasTrigger(skill.StartTrigger))
        {
            return false;
        }

        if (skill.Type == ESkillType.Dash && (skill.Steps.Length != 1 || skill.DashDuration <= 0f))
        {
            return false;
        }

        if (skill.Steps.Length > 1 && !HasTrigger(skill.ComboTrigger))
        {
            return false;
        }

        foreach (SSkillStep step in skill.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.stateName) ||
                !_animator.HasState(0, Animator.StringToHash(step.stateName)) || step.moveDistance < 0f)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(step.recoverStateName) &&
                !_animator.HasState(0, Animator.StringToHash(step.recoverStateName)))
            {
                return false;
            }

            if (step.effectPrefab != null &&
                (step.effectTime < 0f || step.effectTime > 1f || step.effectLifetime <= 0f))
            {
                return false;
            }

            if (skill.Type == ESkillType.Dash)
            {
                continue;
            }

            if (step.moveStart < 0f || step.moveEnd > 1f || step.moveStart >= step.moveEnd ||
                step.hits == null || step.hits.Length == 0)
            {
                return false;
            }

            float previousTime = -1f;

            foreach (SSkillHit hit in step.hits)
            {
                if (hit.time < 0f || hit.time > 1f || hit.time <= previousTime || hit.damageRatio < 0f ||
                    hit.hitStopDuration < 0f || hit.size.x <= 0f || hit.size.y <= 0f || hit.size.z <= 0f)
                {
                    return false;
                }

                previousTime = hit.time;
            }
        }

        return true;
    }

    private bool HasTrigger(string triggerName)
    {
        if (string.IsNullOrWhiteSpace(triggerName))
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in _animator.parameters)
        {
            if (parameter.name == triggerName && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                return true;
            }
        }

        return false;
    }
}
