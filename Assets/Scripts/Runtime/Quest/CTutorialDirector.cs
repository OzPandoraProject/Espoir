using System.Collections.Generic;
using UnityEngine;

// 훈련용 MP와 쿨타임을 플레이어가 입력을 처리하기 전에 반영한다.
[DefaultExecutionOrder(-20)]
public class CTutorialDirector : MonoBehaviour
{
    private enum EPhase
    {
        Waiting = 0,
        Talk = 1,
        Do = 2,
        Done = 3,
    }

    private enum EDiscardPhase
    {
        None,
        Warning,
        Reward
    }

    private struct SHeldStatus
    {
        public CUnit owner;
        public CStatus status;
        public float duration;
        public float baseTimer;
    }

    [Header("퀘스트")]
    [SerializeField] private CQuestSO _quest;
    [SerializeField] private CTutorialStep[] _steps;
    [SerializeField] private CTutorialPrompts _prompts = new CTutorialPrompts();
    [Tooltip("모든 목표를 완료했을 때 재생할 대사 ID")]
    [SerializeField] private string _reportIntroNode = "depart";
    [SerializeField] private string _discardWarningNode = "discard_warning";
    [SerializeField] private string _speakerName = "프라도나";
    [SerializeField] private string _offerText = "프라도나에게 말 걸기";
    [SerializeField] private string _offerHint = "가까이 가서 F";

    [Header("연결")]
    [SerializeField] private CPlayer _player;
    [SerializeField] private CTalkPresenter _talk;
    [SerializeField] private CTalkNpc _pradona;
    [SerializeField] private CCompanionFollow _pradonaFollow;
    [SerializeField] private CTalkNpc _fluffy;
    [SerializeField] private CEnemy _scarecrow;
    [SerializeField] private CTestCamera _camera;
    [SerializeField] private CInventoryUI _inventoryUI;
    [SerializeField] private RectTransform _hudRoot;
    [SerializeField] private CTutorialGuideView _guide;
    [SerializeField] private CQuestWaypointView _waypoint;
    [SerializeField] private CQuestPanelView _panel;

    [Header("훈련장 복귀 위치")]
    [SerializeField] private Transform _trainingSpot;
    [SerializeField] private Transform _pradonaTrainingSpot;

    [Header("이동 중 자막")]
    [SerializeField] private float[] _walkLineAt = { 0.02f, 0.3f, 0.62f, 0.86f };
    [SerializeField, TextArea] private string[] _walkLines;

    [Header("레비아탄 자막")]
    [Tooltip("시전 전, 중첩 획득, 최대 중첩 순서입니다. {0}은 현재 중첩입니다.")]
    [SerializeField, TextArea] private string[] _leviathanReminds;

    [Header("상태 효과")]
    [SerializeField] private CStatusSO _crownStatus;
    [SerializeField] private CStatusSO _flowStatus;
    [SerializeField] private CStatusSO _leviathanStatus;
    [SerializeField] private CStatusSO _powerStatus;

    [Header("훈련 안내 설정")]
    [SerializeField, Min(0f)] private float _remindInterval = 7f;
    [SerializeField, Min(0f)] private float _nextStepDelay = 1.2f;
    [SerializeField, Min(0.5f)] private float _talkDistance = 3.2f;
    [SerializeField, Min(0.5f)] private float _trainingRange = 6f;
    [SerializeField, Min(0.1f)] private float _cameraReturnTime = 1.1f;

    [SerializeField, Min(1)] private int _missesBeforeHint = 3;
    [SerializeField, Min(0f)] private float _missHintInterval = 6f;

    private const float _CAMERA_YAW_STEP = 20f;
    private const float _CAMERA_ZOOM_STEP = 0.15f;

    private readonly List<STutorialKeyStep> _KEYS = new List<STutorialKeyStep>();
    private readonly List<SHeldStatus> _HELD = new List<SHeldStatus>();
    private EPhase _phase;
    private EDiscardPhase _discardPhase;
    private bool _isRestored;
    private int _index = -2;
    private EQuestState _state;
    private bool _canOffer;
    private CTutorialStep _step;
    private float _phaseTime;
    private float _idleTime;
    private CPlayerSkill _playerSkill;
    private RectTransform _stepHighlight;
    private bool _potionSlotPrepared;
    private bool _checkSupply;
    private bool _wasTalking;
    private bool _hasReportTalk;
    private float _cameraYaw;
    private float _cameraDistance;
    private float _cameraReturnAt = -1f;
    private Vector3 _walkLast;
    private float _walkDone;
    private float _walkTotal = 1f;
    private int _walkLine;
    private int _missCount;
    private int _swingFrame = -1;
    private int _hitFrame = -1;
    private float _missToastUntil;
    private ELeviathanState _leviathanState;
    private bool _isPowerGranted;
    private string _remindShown;

    private void OnEnable()
    {
        CUnit.OnDamageResolved += HandleDamage;
        if (_player != null && _player.TryGetComponent(out _playerSkill))
        {
            _playerSkill.BasicAttackHitProcessed += HandleSwing;
        }
        if (_player != null && _player.Inventory != null)
        {
            _player.Inventory.OnInventoryChanged += HandleInventoryChanged;
        }
        if (_inventoryUI != null) _inventoryUI.ItemDiscarded += HandleItemDiscarded;
        CQuestManager.SupplyFailed += HandleSupplyFailed;
    }

    private void OnDisable()
    {
        CUnit.OnDamageResolved -= HandleDamage;
        if (_playerSkill != null)
        {
            _playerSkill.BasicAttackHitProcessed -= HandleSwing;
            _playerSkill.ProtectEnhancedFinisher = false;
        }
        if (_player != null && _player.Inventory != null)
        {
            _player.Inventory.OnInventoryChanged -= HandleInventoryChanged;
        }
        if (_inventoryUI != null) _inventoryUI.ItemDiscarded -= HandleItemDiscarded;
        CQuestManager.SupplyFailed -= HandleSupplyFailed;

        CQuestManager.IsSupplyHeld = false;
        ReleaseHeld();
        SetPower(false);
        if (_pradonaFollow != null) _pradonaFollow.SetHold(false);
        if (_guide != null) _guide.Highlight(null);
        if (_waypoint != null) _waypoint.ClearOverride();
    }

    private void Update()
    {
        if (!CQuestManager.IsReady || _quest == null || _player == null) return;

        if (!_isRestored)
        {
            _isRestored = true;
            RestoreLayout();
            if (CQuestManager.GetPendingReplacement(_quest) != null) _discardPhase = EDiscardPhase.Warning;
        }

        if (_playerSkill != null)
        {
            _playerSkill.ProtectEnhancedFinisher = _state == EQuestState.Active &&
                _quest.GetObjective(_index)?.kind == EQuestObjectiveKind.LeviathanFinisher;
        }
        CQuestManager.IsSupplyHeld = _inventoryUI != null && _inventoryUI.IsOpen;

        if (_phase == EPhase.Do && _step != null) CheckFailedInput();
    }

    private void LateUpdate()
    {
        if (!_isRestored || _player.HpCurrent <= 0f) return;

        float dt = Time.unscaledDeltaTime;
        _phaseTime += dt;

        UpdateStepChange();
        UpdateTalk();
        UpdateHeld();

        if (_discardPhase == EDiscardPhase.None && _phase == EPhase.Do) UpdateDo(dt);
        UpdateMiss();
        UpdateCameraReturn();
        UpdateGuide();
    }

    private void UpdateStepChange()
    {
        EQuestState state = CQuestManager.GetState(_quest);
        CQuestManager.TryGetCurrent(_quest, out int index, out _);
        if (state == EQuestState.Reportable) index = _quest.Objectives.Count;
        bool canOffer = state == EQuestState.None && CQuestManager.CanAccept(_quest);
        if (state == _state && index == _index && canOffer == _canOffer) return;
        _canOffer = canOffer;

        bool isFirst = _index == -2;
        bool hasAdvanced = !isFirst && _state == EQuestState.Active && index != _index;
        EndStep();

        int walkIndex = _quest.FindObjectiveIndex("walk");

        bool reachedCheckpoint = !isFirst && _state == EQuestState.Active &&
            (state == EQuestState.Reportable ||
             (state == EQuestState.Active &&
              walkIndex >= 0 && _index <= walkIndex && index > walkIndex));

        _state = state;
        _index = index;
        _step = state == EQuestState.Active ? FindStep(_quest.GetObjective(index)) : null;
        _stepHighlight = _step != null && _hudRoot != null && !string.IsNullOrEmpty(_step.highlightPath)
            ? _hudRoot.Find(_step.highlightPath) as RectTransform : null;
        _potionSlotPrepared = false;
        _checkSupply = true;

        if (hasAdvanced)
        {
            _phase = EPhase.Done;
            _phaseTime = 0f;
            if (_guide != null) _guide.ClearSubtitle();
            CSceneFlowManager flow = CSceneFlowManager.Instance;
            if (reachedCheckpoint && flow != null &&
                flow.TargetSaveData >= 0 && !flow.IsTraveling)
            {
                CGameManager.Instance?.InvokeAutosave();
            }
            return;
        }

        BeginStep();
    }

    private void BeginStep()
    {
        _phaseTime = 0f;
        _idleTime = 0f;
        _remindShown = null;

        if (_pradona != null) _pradona.SetTalkable(_canOffer);
        UpdateFluffyTalk();

        if (_panel != null) _panel.SetOffer(_canOffer ? _quest : null, _offerText, _offerHint);

        if (_state == EQuestState.None)
        {
            if (_pradonaFollow != null) _pradonaFollow.SetHold(true);
            _phase = EPhase.Do;
            return;
        }

        if (_pradonaFollow != null) _pradonaFollow.SetHold(false);

        if (_state == EQuestState.Reportable)
        {
            _phase = _hasReportTalk ? EPhase.Do : EPhase.Talk;
            return;
        }

        if (_state != EQuestState.Active || _step == null)
        {
            _phase = EPhase.Waiting;
            return;
        }

        if (_step.grantsPower) SetPower(true);

        CQuestObjective objective = _quest.GetObjective(_index);
        if (objective != null && objective.kind == EQuestObjectiveKind.Arrive) BeginWalk();
        if (objective != null && objective.kind == EQuestObjectiveKind.Camera) BeginCamera();
        _phase = string.IsNullOrEmpty(_step.introNode) || _talk.IsOpen ? EPhase.Do : EPhase.Talk;
    }

    private void UpdateFluffyTalk()
    {
        bool canLeave = _state == EQuestState.Reportable || _state == EQuestState.Completed;
        if (_fluffy != null) _fluffy.SetTalkable(canLeave && _discardPhase == EDiscardPhase.None);
    }

    private void EndStep()
    {
        if (_step != null && _step.grantsPower) SetPower(false);
        if (_state == EQuestState.Active && _quest.GetObjective(_index)?.kind == EQuestObjectiveKind.Camera)
        {
            _cameraReturnAt = Time.unscaledTime + 0.5f;
        }

        ReleaseHeld();
        if (_waypoint != null) _waypoint.ClearOverride();
    }

    private void UpdateTalk()
    {
        bool isTalking = _talk != null && _talk.IsOpen;
        if (isTalking && !_wasTalking) HoldForTalk();
        if (!isTalking && _wasTalking)
        {
            ReleaseHeld();
            if (_pradonaFollow != null) _pradonaFollow.SetHold(_state == EQuestState.None);
            if (_discardPhase == EDiscardPhase.None && _phase == EPhase.Talk && _state != EQuestState.None) StartDo();
        }

        _wasTalking = isTalking;

        if (_discardPhase != EDiscardPhase.None)
        {
            if (isTalking || IsWindowOpen()) return;

            if (_discardPhase == EDiscardPhase.Warning)
            {
                if (CanOpenTalk() && TryOpenPradonaTalk(_discardWarningNode))
                {
                    _discardPhase = EDiscardPhase.Reward;
                    _checkSupply = true;
                }
            }
            else if (_checkSupply)
            {
                _checkSupply = false;
                if (CQuestManager.TryGiveReplacement(_quest, _player.Inventory))
                {
                    _discardPhase = EDiscardPhase.None;
                    UpdateFluffyTalk();
                    _potionSlotPrepared = false;
                }
            }
            return;
        }

        if (_phase == EPhase.Done && _phaseTime >= _nextStepDelay) BeginStep();
        if (_phase != EPhase.Talk || isTalking) return;

        if (!CanOpenTalk()) return;

        string node = _state == EQuestState.Reportable ? _reportIntroNode : _step.introNode;
        if (_state == EQuestState.Active && !string.IsNullOrEmpty(_step.equippedIntroNode))
        {
            CQuestObjective equip = _quest.GetObjective(_quest.FindObjectiveIndex(_step.equipObjectiveId));
            if (equip != null && CQuestManager.IsEquipped(_player, equip)) node = _step.equippedIntroNode;
        }
        if (TryOpenPradonaTalk(node) && _state == EQuestState.Reportable) _hasReportTalk = true;
    }

    private bool TryOpenPradonaTalk(string node)
    {
        Vector3 offset = _pradona.FeetPosition - _player.transform.position;
        offset.y = 0f;
        if (offset.magnitude > _talkDistance && _pradonaFollow != null)
        {
            Vector3 back = -_player.transform.forward;
            Vector3 side = _player.transform.right;
            _pradonaFollow.PlaceAt(_player.transform.position + back * 2f + side * 0.8f,
                Quaternion.LookRotation(_player.transform.position - _pradona.FeetPosition));
        }

        if (_pradonaFollow != null) _pradonaFollow.SetHold(true);
        if (_talk.TryOpen(_pradona, node))
        {
            if (_guide != null) _guide.ClearSubtitle();
            return true;
        }

        if (_pradonaFollow != null) _pradonaFollow.SetHold(false);
        return false;
    }

    private bool CanOpenTalk()
    {
        if (_pradona == null || _talk == null || _talk.IsOpen) return false;
        if (_inventoryUI != null && _inventoryUI.IsOpen) return false;
        if (CGameManager.Instance != null && CGameManager.Instance.GameState != EGameState.Idle) return false;
        if (_player.IsInputLocked) return false;

        return _player.Skill == null || !_player.Skill.IsBusy;
    }

    private void StartDo()
    {
        _phase = EPhase.Do;
        _phaseTime = 0f;
        _idleTime = 0f;
        if (_state == EQuestState.Active && _quest.GetObjective(_index)?.kind == EQuestObjectiveKind.Camera)
        {
            BeginCamera();
        }
    }

    private void UpdateDo(float dt)
    {
        CQuestObjective objective = _state == EQuestState.Active ? _quest.GetObjective(_index) : null;

        if (_checkSupply && !IsWindowOpen() && !_talk.IsOpen)
        {
            _checkSupply = false;
            CQuestManager.ReplaceMissingSupply(_quest, _player);
        }

        if (objective != null && objective.kind == EQuestObjectiveKind.Arrive) UpdateWalk();
        if (objective != null && objective.kind == EQuestObjectiveKind.Camera) UpdateCamera();
        if (objective != null && objective.kind == EQuestObjectiveKind.UseItem && !_potionSlotPrepared)
        {
            _potionSlotPrepared = PreparePotionSlot(objective);
        }
        if (objective != null && objective.kind == EQuestObjectiveKind.LeviathanFinisher) UpdateLeviathan();

        if (_waypoint != null)
        {
            if (_step != null && _step.isTraining && IsFarFromScarecrow())
            {
                _waypoint.SetOverride(_scarecrow.transform.position, _scarecrow.DisplayName, 3f);
            }
            else _waypoint.ClearOverride();
        }

        UpdateRemind(dt);
    }

    private void UpdateRemind(float dt)
    {
        if (_guide == null || _talk.IsOpen || IsWindowOpen()) return;

        if (_guide.IsSubtitleOn)
        {
            _idleTime = 0f;
            return;
        }

        _idleTime += dt;
        if (_idleTime < _remindInterval) return;

        _idleTime = 0f;
        string remind = GetRemind();
        if (string.IsNullOrEmpty(remind)) return;

        _remindShown = remind;
        _guide.Say(_speakerName, remind);
    }

    private string GetRemind()
    {
        if (_state == EQuestState.None || _state == EQuestState.Reportable || _step == null) return null;

        CQuestObjective objective = _quest.GetObjective(_index);
        if (objective != null && objective.kind == EQuestObjectiveKind.LeviathanFinisher &&
            _leviathanReminds != null && _leviathanReminds.Length >= 3)
        {
            CPlayerLeviathan leviathan = _player.Skill.Leviathan;
            if (leviathan.State == ELeviathanState.Finisher) return null;
            int stack = leviathan.Stack;
            if (leviathan.State == ELeviathanState.None) return _leviathanReminds[0];
            if (stack >= leviathan.MaxStack) return _leviathanReminds[2];
            return string.Format(_leviathanReminds[1], stack);
        }

        if (UsePotionFromBag(objective)) return objective.text;
        if (string.IsNullOrEmpty(_step.remind)) return null;
        return string.Format(_step.remind, CQuestManager.GetLiveProgress(_quest));
    }

    private void UpdateGuide()
    {
        if (_guide == null) return;

        bool finishing = _playerSkill != null && _playerSkill.ProtectEnhancedFinisher &&
            _playerSkill.Leviathan.State == ELeviathanState.Finisher;
        bool isHidden = finishing || (_talk != null && _talk.IsOpen) ||
            (CGameManager.Instance != null && CGameManager.Instance.GameState != EGameState.Idle &&
            !(_inventoryUI != null && _inventoryUI.IsOpen));
        _guide.SetHidden(isHidden);
        _guide.AvoidRect(_inventoryUI != null && _inventoryUI.IsOpen ? _inventoryUI.PanelRect : null);

        int current = BuildKeys();
        if (finishing) _KEYS.Clear();
        _guide.ShowKeys(_KEYS, current);

        bool isLearning = _phase == EPhase.Do || _phase == EPhase.Talk;
        CQuestObjective objective = _state == EQuestState.Active ? _quest.GetObjective(_index) : null;
        RectTransform highlight = !finishing && isLearning && !UsePotionFromBag(objective) ? _stepHighlight : null;
        RectTransform itemSlot = FindObjectiveItemSlot(objective);
        if (itemSlot != null) highlight = itemSlot;

        _guide.Highlight(highlight);
    }

    private int BuildKeys()
    {
        _KEYS.Clear();
        bool isBagOpen = _inventoryUI != null && _inventoryUI.IsOpen;

        if (_talk != null && _talk.IsOpen) return 0;

        CQuestObjective objective = _state == EQuestState.Active ? _quest.GetObjective(_index) : null;
        if (UsePotionFromBag(objective) && _phase == EPhase.Do)
        {
            if (isBagOpen) AddKey("RMB", ETutorialKeyIcon.RightMouse, objective.text);
            else AddKey("I", ETutorialKeyIcon.Key, _prompts.openBag);
            return 0;
        }

        if (isBagOpen)
        {
            if (objective != null && objective.kind == EQuestObjectiveKind.Equip && _phase == EPhase.Do)
            {
                string caption = objective.item != null ? _prompts.equipItem : objective.text;
                AddKey("RMB", ETutorialKeyIcon.RightMouse, caption);
            }
            else if (_phase != EPhase.Do || _state == EQuestState.Active)
            {
                AddKey("I", ETutorialKeyIcon.Key, _prompts.closeBag, "Esc");
            }

            return 0;
        }

        if (_state == EQuestState.None)
        {
            if (!_canOffer) return 0;
            bool isNear = _talk != null && _talk.IsInRange && _talk.CurrentNpc == _pradona;
            if (isNear) AddKey("F", ETutorialKeyIcon.Key, _prompts.talkToPradona);
            else AddKey("RMB", ETutorialKeyIcon.RightMouse, _prompts.approachPradona);
            return 0;
        }

        if (_state == EQuestState.Reportable)
        {
            if (_phase != EPhase.Do) return 0;
            bool isNear = _talk != null && _talk.IsInRange && _talk.CurrentNpc == _fluffy;
            if (isNear) AddKey("F", ETutorialKeyIcon.Key, _prompts.talkToFluffy);
            else AddKey("RMB", ETutorialKeyIcon.RightMouse, _prompts.approachFluffy);
            return 0;
        }

        if (_phase != EPhase.Do || _step == null || _step.keys == null) return 0;

        if (_step.isTraining && IsFarFromScarecrow())
        {
            AddKey("RMB", ETutorialKeyIcon.RightMouse, _prompts.approachScarecrow);
            return 0;
        }

        int current = -1;
        int firstOpen = -1;

        foreach (CTutorialKey key in _step.keys)
        {
            bool isDone = IsKeyDone(key.doneWhen);
            bool isNow = IsKeyNow(key.nowWhen);
            _KEYS.Add(new STutorialKeyStep
            {
                key = key.key, icon = key.icon, plusKey = key.plusKey, plusIcon = key.plusIcon, altKey = key.altKey,
                caption = key.caption, isDone = isDone, isNow = isNow,
            });

            int index = _KEYS.Count - 1;
            if (isNow && current < 0) current = index;
            if (!isDone && firstOpen < 0) firstOpen = index;
        }

        return current >= 0 ? current : Mathf.Max(0, firstOpen);
    }

    private void AddKey(string key, ETutorialKeyIcon icon, string caption, string altKey = null)
    {
        _KEYS.Add(new STutorialKeyStep { key = key, icon = icon, caption = caption, altKey = altKey });
    }

    private bool IsKeyNow(ETutorialKeyNow condition)
    {
        CPlayerSkill skill = _player.Skill;

        switch (condition)
        {
            case ETutorialKeyNow.LinkOpen:
                return skill.IsStrikeLinkOpen;
            case ETutorialKeyNow.CrownOpen:
                bool isFinisher = _quest.GetObjective(_index)?.kind == EQuestObjectiveKind.LeviathanFinisher;
                return skill.CrownWindowRemaining > 0f &&
                    (!isFinisher || skill.Leviathan.State != ELeviathanState.None);
            case ETutorialKeyNow.LeviathanFull:
                return skill.Leviathan.State == ELeviathanState.Buff &&
                    skill.Leviathan.Stack >= skill.Leviathan.MaxStack;
            case ETutorialKeyNow.MarksFull:
                return _scarecrow != null && _player.Passive.HasFullMark(_scarecrow);
            default:
                return false;
        }
    }

    private bool IsKeyDone(ETutorialKeyDone condition)
    {
        switch (condition)
        {
            case ETutorialKeyDone.CameraRotated:
                CQuestManager.GetCameraProgress(_quest, out bool rotated, out _);
                return rotated;
            case ETutorialKeyDone.CameraZoomed:
                CQuestManager.GetCameraProgress(_quest, out _, out bool zoomed);
                return zoomed;
            case ETutorialKeyDone.LeviathanAwake:
                return _player.Skill.Leviathan.State != ELeviathanState.None;
            case ETutorialKeyDone.PowerReceived:
                return _powerStatus != null && _player.FindStatus(_powerStatus.Id) != null;
            default:
                return false;
        }
    }

    private void CheckFailedInput()
    {
        if (_player.IsInputLocked || _talk.IsOpen || IsWindowOpen()) return;

        CPlayerSkill skill = _player.Skill;
        CQuestObjective objective = _quest.GetObjective(_index);

        if (objective != null && objective.kind == EQuestObjectiveKind.SkillHit && objective.requireLink &&
            _player.WasSkillPressed(ESkillType.Strike) && !skill.IsStrikeLinkOpen && !skill.IsBusy)
        {
            Toast(_prompts.missedLink);
        }

        if (objective != null && objective.kind == EQuestObjectiveKind.CooldownReduced &&
            _player.WasSkillPressed(ESkillType.Rush) && _scarecrow != null &&
            !_player.Passive.HasFullMark(_scarecrow))
        {
            Toast(_prompts.needsMarks);
        }

        if (_step.grantsPower && _player.WasPradonaPressed)
        {
            _player.GetPradonaCooldown(out _, out float remain);
            if (remain > 0f)
            {
                _player.ResetPradonaCooldown();
                Toast(_prompts.powerReady);
            }
        }

        if (_step.assistSkills == null) return;

        foreach (CSkillSO assist in _step.assistSkills)
        {
            if (assist == null || !_player.WasSkillPressed(assist.Type)) continue;

            if (assist.Type == ESkillType.Crown && skill.CrownWindowRemaining <= 0f)
            {
                Toast(_prompts.crownWindowClosed);
                continue;
            }

            skill.GetSkillCooldown(assist.Type, out _, out float remain);
            bool isLink = assist.Type == ESkillType.Strike && skill.IsStrikeLinkOpen;
            int cost = isLink ? assist.FinisherMpCost : assist.MpCost;
            bool isLeviathanAwake = assist.Type == ESkillType.Slam && skill.Leviathan.State != ELeviathanState.None;

            if (remain > 0f && !isLeviathanAwake)
            {
                skill.ResetCooldown(assist);
                Toast(_prompts.skillReady);
            }

            if (_player.CurrentMp < cost && !isLeviathanAwake)
            {
                _player.RestoreMp(cost);
                Toast(_prompts.manaRestored);
            }
        }
    }

    private void UpdateLeviathan()
    {
        CPlayerLeviathan leviathan = _player.Skill.Leviathan;
        ELeviathanState state = leviathan.State;

        if (state == ELeviathanState.Buff && _leviathanStatus != null) Hold(_player, _leviathanStatus.Id);
        else ReleaseHeld();

        if (state == ELeviathanState.Finisher && _leviathanState != state && _guide != null) _guide.ClearSubtitle();

        if (_leviathanState == ELeviathanState.Buff && state == ELeviathanState.Buff &&
            leviathan.Stack >= leviathan.MaxStack && _remindShown != GetRemind())
        {
            SayNow(GetRemind());
        }

        if (_leviathanState == ELeviathanState.Finisher && state == ELeviathanState.None)
        {
            if (!leviathan.WasFinisherEnhanced) Toast(_prompts.finisherTooEarly);
            _player.Skill.ResetCooldowns(false, false, true);
            _player.RestoreMp(Mathf.RoundToInt(_player.MaxMp));
        }

        _leviathanState = state;
    }

    private void SayNow(string text)
    {
        if (_guide == null || string.IsNullOrEmpty(text)) return;

        _guide.Say(_speakerName, text);
        _remindShown = text;
        _idleTime = 0f;
    }

    private void Toast(string text)
    {
        if (_guide != null) _guide.Toast(text);
    }

    private void BeginWalk()
    {
        CQuestObjective objective = _quest.GetObjective(_index);
        _walkLast = _player.transform.position;
        _walkDone = 0f;
        _walkLine = 0;
        Vector3 offset = objective.destinationPoint - _walkLast;
        offset.y = 0f;
        _walkTotal = Mathf.Max(10f, offset.magnitude * 1.35f);
    }

    private void UpdateWalk()
    {
        Vector3 position = _player.transform.position;
        Vector3 step = position - _walkLast;
        step.y = 0f;
        _walkLast = position;
        if (step.magnitude < 1f) _walkDone += step.magnitude;

        if (_guide == null || _walkLines == null || _guide.IsSubtitleOn) return;
        if (_walkLine >= _walkLines.Length || _walkLine >= _walkLineAt.Length) return;
        if (_walkDone / _walkTotal < _walkLineAt[_walkLine]) return;

        _guide.Say(_speakerName, _walkLines[_walkLine]);
        _remindShown = _walkLines[_walkLine];
        _walkLine++;
    }

    private void BeginCamera()
    {
        if (_camera == null) return;

        _cameraYaw = _camera.Yaw;
        _cameraDistance = _camera.DesiredDistance;
    }

    private void UpdateCamera()
    {
        if (_camera == null) return;

        bool rotated = Mathf.Abs(Mathf.DeltaAngle(_cameraYaw, _camera.Yaw)) > _CAMERA_YAW_STEP;
        float distanceRatio = _camera.DesiredDistance / Mathf.Max(0.01f, _cameraDistance);
        bool zoomed = Mathf.Abs(Mathf.Log(distanceRatio)) > _CAMERA_ZOOM_STEP;
        if (rotated || zoomed) CQuestManager.ReportCamera(rotated, zoomed);
    }

    private void UpdateCameraReturn()
    {
        if (_cameraReturnAt < 0f || Time.unscaledTime < _cameraReturnAt) return;

        _cameraReturnAt = -1f;
        if (_camera != null) _camera.ReturnToDefault(_cameraReturnTime);
    }

    private bool UsePotionFromBag(CQuestObjective objective)
    {
        if (!_potionSlotPrepared || objective == null || objective.kind != EQuestObjectiveKind.UseItem) return false;

        CInventory inventory = _player.Inventory;
        int itemId = inventory.GetQuickSlotId(0);
        return !objective.MatchesItem(itemId) || inventory.GetTotalQuantity(itemId) == 0;
    }

    private RectTransform FindObjectiveItemSlot(CQuestObjective objective)
    {
        if (_inventoryUI == null || !_inventoryUI.IsOpen || _phase != EPhase.Do) return null;
        if (objective == null || objective.item == null) return null;
        if (objective.kind != EQuestObjectiveKind.Equip && !UsePotionFromBag(objective)) return null;

        CInventory inventory = _player.Inventory;
        if (inventory == null) return null;

        for (int i = 0; i < inventory.MaxItemSlot; i++)
        {
            CItem item = inventory.GetItem(i);
            if (item != null && objective.MatchesItem(item.Id)) return _inventoryUI.GetSlotRect(i);
        }

        return null;
    }

    private bool PreparePotionSlot(CQuestObjective objective)
    {
        CInventory inventory = _player.Inventory;
        if (inventory == null || objective.item == null) return false;
        int itemId = objective.item.Id;
        if (inventory.GetTotalQuantity(itemId) == 0)
        {
            if (objective.replacementItem == null) return false;
            itemId = objective.replacementItem.Id;
            if (inventory.GetTotalQuantity(itemId) == 0) return false;
        }

        if (inventory.GetQuickSlotId(0) >= 0) return true;

        for (int i = 1; i < inventory.QuickSlotLength; i++)
        {
            if (inventory.GetQuickSlotId(i) == itemId) return true;
        }

        return inventory.RegisterQuickSlot(0, itemId);
    }

    private void HandleSwing(CSkillSO skill, int step, int hit)
    {
        _swingFrame = Time.frameCount;
    }

    private void HandleDamage(SDamageResult result)
    {
        if (result.attacker is CPlayer) _hitFrame = Time.frameCount;
    }

    private void UpdateMiss()
    {
        if (_swingFrame < 0 || _swingFrame == Time.frameCount) return;

        bool isMiss = _hitFrame < _swingFrame;
        _swingFrame = -1;
        if (_phase != EPhase.Do || _step == null || !_step.isTraining) return;

        if (!isMiss)
        {
            _missCount = 0;
            return;
        }

        if (++_missCount < _missesBeforeHint || Time.unscaledTime < _missToastUntil) return;

        _missCount = 0;
        _missToastUntil = Time.unscaledTime + _missHintInterval;
        Toast(IsFarFromScarecrow() ? _prompts.outOfRange
            : _prompts.missedAttack);
    }

    private void HandleItemDiscarded(int itemId)
    {
        if (_player == null || _player.HpCurrent <= 0f) return;
        if (!CQuestManager.TryDiscardSupply(_quest, itemId, out bool repeated)) return;

        _inventoryUI.SetInventoryOpen(false);
        if (repeated) _player.Kill();
        else
        {
            _discardPhase = EDiscardPhase.Warning;
            UpdateFluffyTalk();
        }
    }

    private void HandleInventoryChanged()
    {
        _checkSupply = true;
    }

    private void HandleSupplyFailed(CQuestSO quest, CItemSO item)
    {
        if (quest == _quest) Toast(_prompts.bagFull);
    }

    private bool IsFarFromScarecrow()
    {
        if (_scarecrow == null) return false;

        Vector3 offset = _scarecrow.transform.position - _player.transform.position;
        offset.y = 0f;
        return offset.magnitude > _trainingRange;
    }

    private bool IsWindowOpen()
    {
        return (_inventoryUI != null && _inventoryUI.IsOpen) ||
            (CGameManager.Instance != null && CGameManager.Instance.GameState != EGameState.Idle);
    }

    private void HoldForTalk()
    {
        ReleaseHeld();
        if (_crownStatus != null) Hold(_player, _crownStatus.Id);
        if (_flowStatus != null) Hold(_player, _flowStatus.Id);
        if (_leviathanStatus != null) Hold(_player, _leviathanStatus.Id);
        if (_scarecrow != null && _player.Passive.Mark != null) Hold(_scarecrow, _player.Passive.Mark.Id);
    }

    private void Hold(CUnit owner, int statusId)
    {
        CStatus status = owner.FindStatus(statusId);
        if (status == null) return;

        foreach (SHeldStatus held in _HELD)
        {
            if (held.status == status) return;
        }

        _HELD.Add(new SHeldStatus
        {
            owner = owner,
            status = status,
            duration = status.Duration,
            baseTimer = status.BaseTimer
        });
    }

    private void UpdateHeld()
    {
        for (int i = _HELD.Count - 1; i >= 0; i--)
        {
            SHeldStatus held = _HELD[i];
            if (held.owner == null || held.owner.FindStatus(held.status.Id) != held.status)
            {
                _HELD.RemoveAt(i);
                continue;
            }

            held.status.RestoreTimer(held.duration, held.baseTimer);
        }
    }

    private void ReleaseHeld()
    {
        _HELD.Clear();
    }

    private void SetPower(bool isGranted)
    {
        if (_player == null || _isPowerGranted == isGranted) return;

        _isPowerGranted = isGranted;
        _player.SetTemporaryPradonaPower(isGranted ? EPradonaPower.Invidia : EPradonaPower.None);

        if (!isGranted && _powerStatus != null && (_player.PradonaPower & EPradonaPower.Invidia) == 0)
        {
            _player.RemoveStatus(_powerStatus.Id);
        }
    }

    private void RestoreLayout()
    {
        EQuestState state = CQuestManager.GetState(_quest);
        CQuestManager.TryGetCurrent(_quest, out int index, out _);
        int walkIndex = -1;

        for (int i = 0; i < _quest.Objectives.Count; i++)
        {
            if (_quest.Objectives[i].kind == EQuestObjectiveKind.Arrive) walkIndex = i;
        }

        bool isPastWalk = state == EQuestState.Reportable || state == EQuestState.Completed ||
            (state == EQuestState.Active && index > walkIndex);
        if (walkIndex < 0 || !isPastWalk || _trainingSpot == null) return;

        CharacterController controller = _player.GetComponent<CharacterController>();
        bool wasEnabled = controller != null && controller.enabled;
        if (wasEnabled) controller.enabled = false;
        _player.transform.SetPositionAndRotation(_trainingSpot.position, _trainingSpot.rotation);
        if (wasEnabled) controller.enabled = true;

        if (_player.TryGetComponent(out CPlayerMovement movement)) movement.Stop();

        if (_pradonaFollow != null && _pradonaTrainingSpot != null)
        {
            _pradonaFollow.PlaceAt(_pradonaTrainingSpot.position, _pradonaTrainingSpot.rotation);
        }
    }

    private CTutorialStep FindStep(CQuestObjective objective)
    {
        if (objective == null || _steps == null) return null;

        foreach (CTutorialStep step in _steps)
        {
            if (step != null && step.objectiveId == objective.id) return step;
        }

        return null;
    }
}
