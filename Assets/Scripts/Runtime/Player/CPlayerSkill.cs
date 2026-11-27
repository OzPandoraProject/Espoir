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
    private readonly CPlayerSkillEffects _EFFECTS = new CPlayerSkillEffects();
    private readonly CPlayerDashTrail _DASH_TRAIL = new CPlayerDashTrail();
    private readonly CPlayerSpellCast _SPELL = new CPlayerSpellCast();
    private readonly CPlayerCrown _CROWN = new CPlayerCrown();
    private readonly CPlayerLeviathan _LEVIATHAN = new CPlayerLeviathan();

    private CPlayerSkillAudio _audio;
    private CPlayer _owner;
    private CPlayerMovement _movement;
    private CSkillRuntime _currentSkill;
    private CSkillRuntime _lastBasicAttack;
    private int _stepIndex = -1;
    private int _stateCheckFrame;
    private int _nextHitIndex;
    private bool _hasQueuedCombo;
    private Vector3 _direction;
    private Vector3 _queuedDirection;
    private float _lastFinisherTime = float.NegativeInfinity;
    private float _moveProgress;
    private Vector3 _stepMovement;
    private Vector3 _stepPosition;
    private GameObject _stepEffect;
    private float _dashElapsed;
    private float _dashDistance;
    private bool _hasSpawnedEffect;
    private bool _hasAddedStrikeStack;
    private bool _isCurrentLink;
    private bool _isFinisherHit;
    private float _baseAttackSpeed;

    private readonly List<CSkillSO> _registeredSkills = new();
    public IReadOnlyList<CSkillSO> RegisteredSkills => _registeredSkills;

    public bool IsBusy => _currentSkill != null;
    public float CrownWindowRemaining => _CROWN.RemainingTime;
    public float DashCooldownDuration { get; set; }
    public CPlayerLeviathan Leviathan => _LEVIATHAN;

    /// <summary>
    /// W(왕관)를 쓴 직후 호출됩니다. 인자 = 사용 뒤 왕관 중첩 수.
    /// </summary>
    public event System.Action<int> CrownUsed;

    /// <summary>
    /// 스킬 쿨타임이 줄어든 순간 호출됩니다(E 가 낙인 가득 대상에 적중). 인자 = 스킬, 줄어든 초.
    /// </summary>
    public event System.Action<CSkillSO, float> CooldownReduced;

    /// <summary>
    /// 평타 콤보의 타격 판정 하나를 처리할 때 호출됩니다(적중 여부와 무관). 인자 = 평타 스킬, 단계 번호, 그 단계의 타격 번호.
    /// </summary>
    public event System.Action<CSkillSO, int, int> BasicAttackHitProcessed;

    /// <summary>
    /// 평타 콤보가 진행 중이거나 다음 단계로 이어질 수 있는 동안 참입니다.
    /// </summary>
    public bool IsBasicComboAlive
    {
        get
        {
            if (_currentSkill != null)
            {
                return _currentSkill.data.Type == ESkillType.Melee;
            }

            if (_lastBasicAttack == null || _animator == null)
            {
                return false;
            }

            return CanContinue(_lastBasicAttack, _animator.GetCurrentAnimatorStateInfo(0));
        }
    }

    /// <summary>
    /// 지금 실행 중인 스킬. 없으면 null.
    /// </summary>
    public CSkillSO CurrentSkill => _currentSkill != null ? _currentSkill.data : null;

    /// <summary>
    /// 지금 실행 중인 Q 가 막타 직후 연계로 시작됐는지 여부.
    /// </summary>
    public bool IsCurrentLink => _isCurrentLink;

    /// <summary>
    /// 지금 처리 중인 타격이 콤보 마지막 단계의 마지막 타격인지 여부.
    /// </summary>
    public bool IsFinisherHit => _isFinisherHit;

    /// <summary>
    /// 현재 왕관 상태에 맞는 평타 설정을 반환합니다.
    /// </summary>
    public CSkillSO BasicAttackData => _owner != null ? _CROWN.GetAttack(_owner, _basicAttack) : _basicAttack;

    /// <summary>
    /// 현재 실행 중인 평타가 마지막 단계인지 반환합니다.
    /// </summary>
    public bool IsBasicFinisherActive => _currentSkill != null &&
        _currentSkill.data.Type == ESkillType.Melee &&
        _stepIndex == _currentSkill.data.Steps.Length - 1;

    /// <summary>
    /// Q 막타 연계가 남은 시간(초)입니다. 마지막 단계에서 취소 가능해져 아직 막타가 끝나지 않았으면 연계 시간 전체를 반환합니다.
    /// </summary>
    public float StrikeLinkRemaining
    {
        get
        {
            if (!IsStrikeLinkOpen)
            {
                return 0f;
            }

            foreach (CSkillRuntime runtime in _SKILLS.Values)
            {
                if (runtime.data.Type != ESkillType.Strike)
                {
                    continue;
                }

                float window = runtime.data.FinisherWindow;
                float remaining = _lastFinisherTime + window - Time.time;
                return remaining > 0f ? Mathf.Min(remaining, window) : window;
            }

            return 0f;
        }
    }

    /// <summary>
    /// 지금 Q(Strike)를 쓰면 막타 연계(MP · 선딜 감소)가 되는지 알려 줍니다. Q 가 쿨타임이면 거짓입니다.
    /// </summary>
    public bool IsStrikeLinkOpen
    {
        get
        {
            if (_owner == null || _animator == null)
            {
                return false;
            }

            foreach (CSkillRuntime runtime in _SKILLS.Values)
            {
                if (runtime.data.Type != ESkillType.Strike)
                {
                    continue;
                }

                if (Time.time < runtime.readyTime)
                {
                    return false;
                }

                return Time.time <= _lastFinisherTime + runtime.data.FinisherWindow ||
                    (IsBusy && _currentSkill.data.Type == ESkillType.Melee && CanCancelInto(ESkillType.Strike));
            }

            return false;
        }
    }

    private void OnDisable()
    {
        CancelSkill();
        _CROWN.Clear();
        _lastBasicAttack = null;
        _lastFinisherTime = float.NegativeInfinity;
    }

    /// <summary>
    /// 지정한 스킬의 재사용 대기시간과 그 비율을 반환합니다.
    /// </summary>
    public void GetSkillCooldown(ESkillType type, out float ratio, out float remainCooldown)
    {
        ratio = 0f;
        remainCooldown = 0f;

        foreach (KeyValuePair<CSkillSO, CSkillRuntime> pair in _SKILLS)
        {
            if (pair.Key.Type != type) continue;
            remainCooldown = Mathf.Max(0f,pair.Value.readyTime - Time.time);
            if (pair.Key.Type == ESkillType.Dash)
            {
                if (DashCooldownDuration == 0) DashCooldownDuration = pair.Key.Cooldown;
                ratio = pair.Key.Cooldown > 0f ? Mathf.Clamp01(remainCooldown / DashCooldownDuration) : 0f;
            }
            else
            {
                ratio = pair.Key.Cooldown > 0f ? Mathf.Clamp01(remainCooldown / pair.Key.Cooldown) : 0f;
            }
            return;
        }
    }
    /// <summary>
    /// 스킬이 사용가능한지를 반환합니다.
    /// </summary>
    public bool GetSkillAvailable(ESkillType type)
    {
        if (_owner == null || _owner.HpCurrent <= 0f || !isActiveAndEnabled)
        {
            return false;
        }

        CSkillRuntime runtime = null;
        if (type == ESkillType.Melee)
        {
            if (_animator == null || _basicAttack == null)
            {
                return false;
            }

            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            if (IsBusy)
            {
                int index = GetStateIndex(_currentSkill.stateHashes, state.shortNameHash);
                return _currentSkill.data.Type == type && _currentSkill.comboTriggerHash != 0 &&
                    index >= 0 && index < _currentSkill.data.Steps.Length - 1 && !_animator.IsInTransition(0);
            }

            CSkillSO attack = _lastBasicAttack != null && CanContinue(_lastBasicAttack, state) ?
                _lastBasicAttack.data : _CROWN.GetAttack(_owner, _basicAttack);
            _SKILLS.TryGetValue(attack, out runtime);
        }

        else
        {
            foreach (CSkillRuntime registered in _SKILLS.Values)
            {
                if (registered.data.Type == type)
                {
                    runtime = registered;
                    break;
                }
            }
        }

        // 활성 R(레비아탄 버프)의 재사용은 쿨타임과 MP 를 보지 않는다.
        if (runtime != null && runtime.data is CLeviathanSkillSO && !_LEVIATHAN.IsIdle)
        {
            return _LEVIATHAN.IsBuffActive;
        }

        if (runtime == null || Time.time < runtime.readyTime)
        {
            return false;
        }

        CSkillSO skill = runtime.data;
        if (skill is CLeviathanSkillSO)
        {
            return _owner.CurrentMp >= skill.MpCost;
        }

        if (type == ESkillType.Crown)
        {
            return _CROWN.CanUse() && (!IsBusy || _currentSkill.data.Type == ESkillType.Melee) &&
                _owner.CurrentMp >= skill.MpCost;
        }

        if (IsBusy && !CanCancelInto(type))
        {
            return false;
        }

        bool isFinisherLink = type == ESkillType.Strike &&
            (Time.time <= _lastFinisherTime + skill.FinisherWindow ||
                (IsBusy && _currentSkill.data.Type == ESkillType.Melee));
        int mpCost = isFinisherLink ? skill.FinisherMpCost : skill.MpCost;
        return _owner.CurrentMp >= mpCost;
    }

    /// <summary>
    /// 사용 주체와 기본 스킬 연결을 확인합니다.
    /// </summary>
    public bool Initialize(CPlayer owner)
    {
        _owner = owner;
        _audio = GetComponentInChildren<CPlayerSkillAudio>();
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

        if (skill.Type == ESkillType.Crown)
        {
            foreach (CSkillSO attack in skill.EmpoweredAttacks)
            {
                if (!RegisterSkill(attack))
                {
                    return false;
                }
            }

            _CROWN.Initialize(skill);
        }

        if (skill is CLeviathanSkillSO leviathan)
        {
            if (!CPlayerLeviathan.HasValidSettings(leviathan))
            {
                return false;
            }

            _LEVIATHAN.Initialize(leviathan);
        }

        int stepCount = skill.Steps != null ? skill.Steps.Length : 0;
        CSkillRuntime runtime = new CSkillRuntime
        {
            data = skill,
            startTriggerHash = string.IsNullOrWhiteSpace(skill.StartTrigger) ? 0 :
                Animator.StringToHash(skill.StartTrigger),
            comboTriggerHash = string.IsNullOrWhiteSpace(skill.ComboTrigger) ? 0 :
                Animator.StringToHash(skill.ComboTrigger),
            stateHashes = new int[stepCount],
            recoverStateHashes = new int[stepCount]
        };

        for (int i = 0; i < stepCount; i++)
        {
            runtime.stateHashes[i] = Animator.StringToHash(skill.Steps[i].stateName);

            if (!string.IsNullOrWhiteSpace(skill.Steps[i].recoverStateName))
            {
                runtime.recoverStateHashes[i] = Animator.StringToHash(skill.Steps[i].recoverStateName);
            }
        }

        _SKILLS.Add(skill, runtime);
        _registeredSkills.Add(skill);
        return true;
    }

    /// <summary>
    /// 지정한 스킬의 쿨타임을 초기화합니다.
    /// </summary>
    public void ResetCooldown(CSkillSO skill)
    {
        if (skill != null && _SKILLS.TryGetValue(skill, out CSkillRuntime runtime))
        {
            runtime.readyTime = 0f;
        }
    }

    /// <summary>
    /// 등록된 모든 스킬의 쿨타임을 초기화합니다.
    /// </summary>

    public void ResetCooldowns()
    {
        foreach (CSkillRuntime runtime in _SKILLS.Values)
        {
            runtime.readyTime = 0f;
        }
    }

    /// <summary>
    /// 맵 이동용: 스킬(SO)별 재사용 가능 시각을 꺼내고, W 사용 기회 종료 시각을 돌려줍니다 (Time.time 기준).
    /// </summary>
    public float ExportTimers(Dictionary<CSkillSO, float> readyTimes)
    {
        readyTimes.Clear();
        foreach (KeyValuePair<CSkillSO, CSkillRuntime> pair in _SKILLS)
        {
            readyTimes[pair.Key] = pair.Value.readyTime;
        }

        return _CROWN.WindowEnd;
    }

    /// <summary>
    /// 맵 이동용: ExportTimers 로 꺼낸 시각을 같은 스킬(SO)에 그대로 넣습니다. 넣은 스킬 수를 돌려줍니다.
    /// </summary>
    public int ImportTimers(IReadOnlyDictionary<CSkillSO, float> readyTimes, float crownWindowEnd)
    {
        int restored = 0;
        foreach (KeyValuePair<CSkillSO, float> pair in readyTimes)
        {
            if (_SKILLS.TryGetValue(pair.Key, out CSkillRuntime runtime))
            {
                runtime.readyTime = pair.Value;
                restored++;
            }
        }

        _CROWN.WindowEnd = crownWindowEnd;
        return restored;
    }

    /// <summary>
    /// 기본 공격을 시작하거나 다음 연격의 방향을 예약합니다.
    /// </summary>

    public bool TryAttack(Vector3 direction)
    {
        if (IsBusy)
        {
            return _currentSkill.data.Type == ESkillType.Melee && TryUse(_currentSkill.data, direction);
        }

        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
        if (_lastBasicAttack != null && CanContinue(_lastBasicAttack, state))
        {
            return TryUse(_lastBasicAttack.data, direction);
        }

        return TryUse(_CROWN.GetAttack(_owner, _basicAttack), direction);
    }

    /// <summary>
    /// 대쉬를 사용할 수 있을 때 현재 행동을 취소하고 지정한 방향으로 대쉬합니다.
    /// </summary>

    public bool TryDash(Vector3 direction)
    {
        return TryUse(_dash, direction);
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

        if (skill is CLeviathanSkillSO && !_LEVIATHAN.IsIdle)
        {
            return _LEVIATHAN.IsBuffActive && TryStartLeviathanFinisher(runtime, direction, true);
        }

        if (Time.time < runtime.readyTime)
        {
            return false;
        }

        // R 레비아탄 시전은 모션 · 이펙트 없이 버프만 건다. 내려찍기 모션은 피니쉬 때 한 번만 나온다.
        if (skill is CLeviathanSkillSO)
        {
            return TryBeginLeviathanBuff(runtime);
        }

        if (skill.Type == ESkillType.Crown)
        {
            if (!_CROWN.CanUse() || (IsBusy && _currentSkill.data.Type != ESkillType.Melee) ||
                !_owner.TrySpendMp(skill.MpCost))
            {
                return false;
            }

            CQuestManager.ReportSkillUse(skill);
            _CROWN.Use(_owner);
            runtime.readyTime = Time.time + skill.Cooldown;
            int crownStack = _owner.FindStatusStack(skill.CrownStatus.Id);
            CrownUsed?.Invoke(crownStack);
            _audio?.PlayCrown(crownStack);
            return true;
        }

        bool isCanceling = IsBusy;

        if (isCanceling && !CanCancelInto(skill.Type))
        {
            return false;
        }

        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
        bool canContinue = skill.Type == ESkillType.Melee && CanContinue(runtime, state);

        bool isFinisherLink = skill.Type == ESkillType.Strike &&
            (Time.time <= _lastFinisherTime + skill.FinisherWindow ||
                (isCanceling && _currentSkill.data.Type == ESkillType.Melee));
        int mpCost = isFinisherLink ? skill.FinisherMpCost : skill.MpCost;

        if (!_owner.TrySpendMp(mpCost))
        {
            return false;
        }

        if (isCanceling && skill.Type == ESkillType.Strike && _currentSkill.data.Type == ESkillType.Melee)
        {
            int index = GetStateIndex(_currentSkill.stateHashes, state.shortNameHash);
            if (index != _stepIndex)
            {
                EnterStep(index);
            }

            UpdateStepHits(_currentSkill.data, _currentSkill.data.Steps[index], state.normalizedTime, false);
        }

        bool keepEffects = isCanceling && _currentSkill.data.Type == ESkillType.Rush &&
            skill.Type == ESkillType.Strike;
        FinishSkill(isCanceling, keepEffects);
        _currentSkill = runtime;
        _isCurrentLink = isFinisherLink;
        _stateCheckFrame = Time.frameCount + 1;
        CQuestManager.ReportSkillUse(skill);

        // 대시만 보스배틀일 때 쿨 길어지게
        float cooldown = skill.Cooldown;
        if (skill.Type == ESkillType.Dash)
        {
            if ((CGameManager.Instance != null) &&
                (CGameManager.Instance.BattleState == EBattleState.BossBattle))
            {
                cooldown = skill.CooldownInBossbattle;
            }
            DashCooldownDuration = cooldown;
        }

        runtime.readyTime = Time.time + cooldown;
        _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;

        if (skill.Type == ESkillType.Dash)
        {
            _owner.AddStatus(28, 0f);
            _audio?.PlayDash();
            transform.rotation = Quaternion.LookRotation(_direction);
            _DASH_TRAIL.Begin(skill.DashTrail, transform, _animator, _direction, _EFFECTS);
        }

        if (skill.Type == ESkillType.Rush || skill.Type == ESkillType.Slam)
        {
            _SPELL.Begin(_owner, _direction, skill, _EFFECTS);
            _DASH_TRAIL.Reset(transform.position, skill.DashTrail.firstDistance);
        }

        if (skill.Type == ESkillType.Strike)
        {
            transform.rotation = Quaternion.LookRotation(_direction);
            _baseAttackSpeed = _animator.speed;
            float windupSpeed = isFinisherLink ? 1f / skill.FinisherWindupRatio : 1f;
            _animator.speed = _baseAttackSpeed * windupSpeed;
        }

        if (skill.Type == ESkillType.Melee)
        {
            _baseAttackSpeed = _animator.speed;
            _lastBasicAttack = runtime;
            AnimationCurve moveCurve = skill.Steps[0].moveCurve;
            if (moveCurve != null && moveCurve.length > 0)
            {
                transform.rotation = Quaternion.LookRotation(_direction);
            }
        }

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
        UpdateLeviathan();

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

        if (_EFFECTS.UpdateHitStop())
        {
            return;
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
        FinishSkill(true);
        _LEVIATHAN.Clear(_owner);

        if (wasBusy && _animator != null && _animator.isActiveAndEnabled)
        {
            _animator.CrossFadeInFixedTime(_IDLE_HASH, _cancelTransitionDuration);
        }
    }

    private void UpdateLeviathan()
    {
        if (!_LEVIATHAN.IsBuffActive)
        {
            return;
        }

        _LEVIATHAN.CheckExpired(_owner);

        // 버프가 끝나면 현재 행동을 취소하고 마우스 방향으로 피니쉬를 시작한다.
        if (_LEVIATHAN.IsFinisherPending && !_owner.IsInputLocked &&
            _SKILLS.TryGetValue(_LEVIATHAN.Settings, out CSkillRuntime runtime))
        {
            TryStartLeviathanFinisher(runtime, _owner.GetPointerDirection(), false);
        }
    }

    private bool TryBeginLeviathanBuff(CSkillRuntime runtime)
    {
        // 버프만 켜므로 진행 중인 행동과 이동 목표는 유지한다.
        if (!_owner.TrySpendMp(runtime.data.MpCost))
        {
            return false;
        }

        runtime.readyTime = Time.time + runtime.data.Cooldown;
        _LEVIATHAN.BeginBuff(_owner);
        _audio?.PlayLeviathanBuff();
        CQuestManager.ReportSkillUse(runtime.data);
        return true;
    }

    private bool TryStartLeviathanFinisher(CSkillRuntime runtime, Vector3 direction, bool isManual)
    {
        bool isCanceling = IsBusy;
        FinishSkill(isCanceling);
        _currentSkill = runtime;
        _isCurrentLink = false;
        _stateCheckFrame = Time.frameCount + 1;
        _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        _SPELL.Begin(_owner, _direction, runtime.data, _EFFECTS);
        _DASH_TRAIL.Reset(transform.position, runtime.data.DashTrail.firstDistance);
        _LEVIATHAN.BeginFinisher(_owner, isManual);
        _audio?.PlayLeviathanFinisher();

        if (isCanceling)
        {
            _animator.CrossFadeInFixedTime(runtime.stateHashes[0], _cancelTransitionDuration);
        }

        else
        {
            _animator.SetTrigger(runtime.startTriggerHash);
        }

        return true;
    }

    internal bool ProtectEnhancedFinisher { get; set; }

    private bool CanCancelInto(ESkillType type)
    {
        CSkillSO current = _currentSkill.data;
        if (type == ESkillType.Rush)
        {
            if (ProtectEnhancedFinisher && _LEVIATHAN.State == ELeviathanState.Finisher && _LEVIATHAN.IsEnhanced)
                return false;
            return current.Type != ESkillType.Rush;
        }

        float cancelTime;

        if (type == ESkillType.Dash && current.Type != ESkillType.Dash)
        {
            cancelTime = current.DashCancelTime;
            if (cancelTime <= 0f)
            {
                return true;
            }
        }

        else if (type == ESkillType.Strike && current.StrikeCancelTime > 0f)
        {
            cancelTime = current.StrikeCancelTime;
        }

        else
        {
            return false;
        }

        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
        int index = GetStateIndex(_currentSkill.stateHashes, state.shortNameHash);
        return index == current.Steps.Length - 1 && state.normalizedTime >= cancelTime;
    }

    private bool CanContinue(CSkillRuntime runtime, AnimatorStateInfo state)
    {
        int index = GetStateIndex(runtime.recoverStateHashes, state.shortNameHash);
        return runtime.comboTriggerHash != 0 && index >= 0 && index < runtime.data.Steps.Length - 1 &&
            state.normalizedTime * state.length <= runtime.data.ComboGraceTime;
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
        if (_currentSkill.data.Type == ESkillType.Melee)
        {
            _animator.speed = _baseAttackSpeed * Mathf.Max(0f, _owner.AttackSpeedMultiplier);
        }

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

        CSkillSO skill = _currentSkill.data;
        _audio?.UpdateStep(skill, state, _animator.speed, _nextHitIndex);
        if (skill.Type == ESkillType.Strike && state.normalizedTime >= skill.Steps[index].hits[0].time)
        {
            _animator.speed = _baseAttackSpeed;
        }

        if (skill.Type == ESkillType.Rush)
        {
            if (_SPELL.UpdateRush(_owner, _playerAttack, _movement, skill, state.normalizedTime))
            {
                _currentSkill.readyTime -= skill.MarkedCooldownReduction;
                CooldownReduced?.Invoke(skill, skill.MarkedCooldownReduction);
                CQuestManager.ReportCooldownReduced(skill);
            }

            _DASH_TRAIL.UpdateRush(skill.DashTrail, transform, _animator, _direction,
                state.normalizedTime, _EFFECTS);
            return;
        }

        if (skill.Type == ESkillType.Slam)
        {
            if (skill is CLeviathanSkillSO)
            {
                _LEVIATHAN.UpdateFinisher(_owner, _playerAttack, state.normalizedTime);
            }

            else
            {
                _SPELL.UpdateSlam(_owner, _playerAttack, skill, state.normalizedTime);
            }

            return;
        }

        _movement.Rotate(_direction, _currentSkill.data.RotationSpeed);
        SSkillStep step = _currentSkill.data.Steps[index];
        UpdateStepMovement(step, state.normalizedTime);
        UpdateEffect(step, state);

        if (_stepEffect != null && step.moveCurve != null && step.moveCurve.length > 0)
        {
            _stepEffect.transform.position = transform.position - transform.rotation * _stepMovement;
        }

        if (skill.SyncEffectSpeed)
        {
            _EFFECTS.SetSpeed(state.speed * state.speedMultiplier * _animator.speed);
        }

        UpdateStepHits(skill, step, state.normalizedTime);
    }

    private void UpdateStepMovement(SSkillStep step, float time)
    {
        if (step.moveCurve != null && step.moveCurve.length > 0)
        {
            time = Mathf.Clamp01(time);
            float side = step.sideMoveCurve != null ? step.sideMoveCurve.Evaluate(time) : 0f;
            Vector3 motion = new Vector3(side, 0f, step.moveCurve.Evaluate(time));
            Vector3 delta = motion - _stepMovement;
            delta.z *= 1f - step.forwardMoveReduction;
            _movement.MoveOnGround(transform.rotation * delta);
            _stepMovement = motion;
        }

        else
        {
            float progress = Mathf.InverseLerp(step.moveStart, step.moveEnd, time);
            float distance = Mathf.Max(progress - _moveProgress, 0f) * step.moveDistance;
            _movement.MoveOnGround(transform.forward * distance);
            _moveProgress = progress;
        }
    }

    private void UpdateStepHits(CSkillSO skill, SSkillStep step, float time, bool applyHitStop = true)
    {
        while (_nextHitIndex < step.hits.Length && time >= step.hits[_nextHitIndex].time)
        {
            SSkillHit hit = step.hits[_nextHitIndex];
            _nextHitIndex++;

            bool isBasicAttack = skill.Type == ESkillType.Melee;
            if (isBasicAttack)
            {
                hit.damageRatio *= _CROWN.GetDamageMultiplier(_owner);
                BasicAttackHitProcessed?.Invoke(skill, _stepIndex, _nextHitIndex - 1);
            }

            _isFinisherHit = isBasicAttack && _stepIndex == skill.Steps.Length - 1 && _nextHitIndex == step.hits.Length;
            SPlayerHitResult result = _playerAttack.Attack(_owner, hit, hit.appliesMark);
            _isFinisherHit = false;

            if (!result.hasHit)
            {
                continue;
            }

            if (isBasicAttack)
            {
                _owner.RecoverBasicAttackMp();
                if (hit.opensCrownWindow)
                {
                    _CROWN.OpenWindow();
                }

                // 콤보 마지막 단계의 마지막 피해(일반·W1 3타, W2·W3 4타)만 막타로 센다. 광역이어도 한 번이다.
                if (_stepIndex == skill.Steps.Length - 1 && _nextHitIndex == step.hits.Length)
                {
                    _LEVIATHAN.AddStack();
                }
            }

            if (skill.Type == ESkillType.Strike && !_hasAddedStrikeStack)
            {
                _LEVIATHAN.AddStack();
                _hasAddedStrikeStack = true;
            }

            _owner.Passive.GainFlow(_owner, _owner.Passive.FlowPerHit(result.hitFullyMarkedTarget));

            if (applyHitStop && hit.hitStopDuration > 0f)
            {
                _EFFECTS.HitStop(hit.hitStopDuration, _animator);
                break;
            }
        }
    }

    private void UpdateDash()
    {
        CSkillSO skill = _currentSkill.data;
        float endTime = skill.DashStartDelay + skill.DashDuration;

        if (_dashElapsed < endTime)
        {
            _dashElapsed = Mathf.Min(_dashElapsed + Time.deltaTime, endTime);
            float progress = _dashElapsed >= endTime ? 1f :
                Mathf.Clamp01((_dashElapsed - skill.DashStartDelay) / skill.DashDuration);
            float distance = skill.Steps[0].moveDistance * (1f - Mathf.Pow(1f - progress, skill.DashEasePower));
            _movement.MoveOnGround(_direction * (distance - _dashDistance));
            _dashDistance = distance;
            _DASH_TRAIL.Update(skill.DashTrail, transform, _animator, _direction, progress, _EFFECTS);
        }

        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);

        if (state.shortNameHash == _currentSkill.stateHashes[0])
        {
            if (_stepIndex < 0)
            {
                EnterStep(0);
            }

            UpdateEffect(skill.Steps[0], state);
        }

        if (_dashElapsed >= endTime)
        {
            FinishSkill();
            _animator.CrossFadeInFixedTime(_IDLE_HASH, _cancelTransitionDuration);
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
        _audio?.BeginStep(_currentSkill.data, index);
        _nextHitIndex = 0;
        _hasAddedStrikeStack = false;
        _moveProgress = 0f;
        _stepMovement = Vector3.zero;
        _stepPosition = transform.position;
        _stepEffect = null;
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

        Vector3 position = step.moveCurve != null && step.moveCurve.length > 0 ?
            _stepPosition : transform.position;
        GameObject effect = _EFFECTS.Spawn(step.effectPrefab, position, transform.rotation);
        _stepEffect = effect;
        if (_currentSkill.data.SyncEffectSpeed)
        {
            float stateSpeed = state.speed * state.speedMultiplier;
            _EFFECTS.PlaySynced(effect, elapsed * stateSpeed, stateSpeed * _animator.speed);
        }
    }

    private void FinishSkill(bool canceled = false, bool keepEffects = false)
    {
        _audio?.EndStep(canceled);
        // 대시무적 제거
        if (_owner != null &&
            _currentSkill != null &&
            _currentSkill.data.Type == ESkillType.Dash)
        {
            _owner.RemoveStatus(28);
        }

        _EFFECTS.End(canceled, keepEffects);
        if (canceled)
        {
            _lastBasicAttack = null;
        }

        _SPELL.End();
        _LEVIATHAN.End(canceled);

        if (_currentSkill != null)
        {
            if (!canceled && _currentSkill.data.Type == ESkillType.Melee &&
                _stepIndex == _currentSkill.data.Steps.Length - 1 &&
                _nextHitIndex >= _currentSkill.data.Steps[_stepIndex].hits.Length)
            {
                _lastFinisherTime = Time.time;
            }

            if (_currentSkill.data.Type == ESkillType.Melee || _currentSkill.data.Type == ESkillType.Strike)
            {
                _animator.speed = _baseAttackSpeed;
            }

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
        _hasAddedStrikeStack = false;
        _hasQueuedCombo = false;
        _moveProgress = 0f;
        _stepMovement = Vector3.zero;
        _dashElapsed = 0f;
        _dashDistance = 0f;

        if (_movement != null)
        {
            _movement.Stop();
        }
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
        if (skill.Type == ESkillType.Crown)
        {
            if (skill.CrownStatus == null || skill.CrownStatus.MaxStack <= 0 ||
                skill.EmpoweredAttacks == null || skill.EmpoweredAttacks.Length != skill.CrownStatus.MaxStack ||
                skill.CrownEffects == null || skill.CrownEffects.Length != skill.CrownStatus.MaxStack ||
                skill.CrownWindow <= 0f || skill.EmpoweredDamageMultiplier < 1f ||
                skill.Cooldown < 0f || skill.MpCost < 0)
            {
                return false;
            }

            for (int i = 0; i < skill.EmpoweredAttacks.Length; i++)
            {
                if (skill.EmpoweredAttacks[i] == null || skill.EmpoweredAttacks[i].Type != ESkillType.Melee ||
                    skill.CrownEffects[i] == null)
                {
                    return false;
                }
            }

            return true;
        }

        if (skill.Steps == null || skill.Steps.Length == 0 || skill.Cooldown < 0f || skill.RotationSpeed < 0f ||
            skill.ComboGraceTime < 0f || !HasTrigger(skill.StartTrigger))
        {
            return false;
        }

        if (skill.Type == ESkillType.Dash && (skill.Steps.Length != 1 || skill.DashDuration <= 0f))
        {
            return false;
        }

        if (skill.Type == ESkillType.Rush && (skill.Steps.Length != 1 || skill.Steps[0].hits?.Length != 1 ||
            skill.MovementCurve == null || skill.MovementCurve.length == 0))
        {
            return false;
        }

        if (skill.Type == ESkillType.Strike && (skill.Steps.Length != 1 || skill.FinisherWindow < 0f ||
            skill.FinisherMpCost < 0 || skill.FinisherWindupRatio <= 0f || skill.FinisherWindupRatio > 1f))
        {
            return false;
        }

        if (skill.Type == ESkillType.Slam && skill.Steps.Length != 1)
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
                (step.effectTime < 0f || step.effectTime > 1f))
            {
                return false;
            }

            if (step.effectPrefab != null &&
                (!step.effectPrefab.TryGetComponent(out ParticleSystem particles) ||
                    particles.main.stopAction != ParticleSystemStopAction.Destroy))
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
                    hit.markedDamageRatio < 0f || hit.hitStopDuration < 0f ||
                    hit.size.x <= 0f || hit.size.y <= 0f || hit.size.z <= 0f)
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

    public void ResetCooldowns(bool resetNormal, bool resetDash, bool resetUltimate)
    {
        foreach (CSkillRuntime runtime in _SKILLS.Values)
        {
            switch (runtime.data.Type)
            {
                case ESkillType.Strike: // Q
                    if (resetNormal) runtime.readyTime = 0f;
                    break;
                case ESkillType.Crown:  // W
                    if (resetNormal) runtime.readyTime = 0f;
                    break;
                case ESkillType.Rush:   // E
                    if (resetNormal) runtime.readyTime = 0f;
                    break;
                case ESkillType.Slam:   // R
                    if (resetUltimate) runtime.readyTime = 0f;
                    break;
                case ESkillType.Dash:   // Space
                    if (resetDash) runtime.readyTime = 0f;
                    break;
            }
        }
    }
}
