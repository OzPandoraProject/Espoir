using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CPlayerMovement), typeof(CPlayerSkill))]
public class CPlayer : CUnit
{
    [Serializable]
    private struct SSkillSlot
    {
        public InputActionReference input;
        public CSkillSO skill;
    }

    [Header("디버그용 세트")]
    [SerializeField] private int _debugSet = 0;

    [Header("인벤토리 연결")]
    [SerializeField] private CInventory _inventory;

    [Header("카메라 설정")]
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private CTestCamera _testCamera;
    [SerializeField] private LayerMask _groundLayer;

    [Header("입력 설정")]
    [SerializeField] private InputActionReference _moveClick;
    [SerializeField] private InputActionReference _pointerPosition;
    [SerializeField] private InputActionReference _attackInput;
    [SerializeField] private InputActionReference _dashInput;
    [SerializeField] private InputActionReference _pradonaInput;
    [SerializeField] private SSkillSlot[] _skillSlots;
    [SerializeField] private InputActionReference[] _quickslotInputs = new InputActionReference[4];

    [Header("MP 설정")]
    [SerializeField, Min(1)] private int _maxMp = 150;
    [SerializeField, Min(0)] private int _mpPerBasicHit = 5;

    [Header("패시브 설정")]
    [SerializeField] private CPlayerPassive _passive = new CPlayerPassive();

    [Header("프라도나 권능 관련 정보")]
    [SerializeField] private float _pradonaCooldownSet = 60f;
    [SerializeField] private EPradonaPower _pradonaPowerDebug;

    private CPlayerMovement _movement;
    private CPlayerSkill _skill;
    private Dictionary<EEquipmentSlot, CItemEquipment> _equipment = new Dictionary<EEquipmentSlot, CItemEquipment>();
    private List<CStatus> _equipmentStatus = new List<CStatus>();
    private bool _actionsBlocked;
    private bool _isInvincible;
    private bool _isInputLocked;
    private EPradonaPower _pradonaPower;
    private EPradonaPower _temporaryPradonaPower;
    private float _pradonaCooldown;
    private bool _pauseMainStatusTimer;

    protected float _hpMaxBaseline = 1;
    protected float _attackBaseline = 0;
    protected float _defenceBaseline = 0;

    protected float _critRateBaseline = 0.05f;
    protected float _critDmgBaseline = 1.5f;

    protected float _movementBaseline = 1f;
    protected float _attackSpeedBaseline = 1f;

    public int CurrentMp { get; private set; }
    public float CrownWindowRemaining => _skill.CrownWindowRemaining;
    public float MovementSpeedMultiplier => _movementMultiply;
    public float AttackSpeedMultiplier => _attackSpeedMultiply;
    public float MaxMp => _maxMp;
    public float BaseHpMax => _baseHpMax;
    public float BaseAttack => _baseAttack;
    public float BaseDefence => _baseDefence;
    public float BaseCriticalRate => _baseCritRate;
    public float BaseCriticalDamage => _baseCritDmg;
    public CInventory Inventory => _inventory;
    protected override IReadOnlyList<CStatus> EquipmentStatusForEffects => _equipmentStatus;
    public CPlayerPassive Passive => _passive;

    /// <summary>
    /// 플레이어의 스킬 상태를 반환합니다.
    /// </summary>
    public CPlayerSkill Skill => _skill;
    public IReadOnlyList<CSkillSO> Skills => _skill.RegisteredSkills;
    internal bool EscapeActionsBlocked { get; set; }
    public bool IsInputLocked => _isInputLocked;
    internal bool WasPradonaPressed => _pradonaInput?.action?.WasPressedThisFrame() == true;

    public Dictionary<EEquipmentSlot, CItemEquipment> Equipment
    {
        get { return _equipment; }
        set { _equipment = value; }
    }

    public List<CStatus> EquipmentStatus
    {
        get { return _equipmentStatus; }
        set { _equipmentStatus = value; }
    }

    public float HpMaxBaseline
    {
        get { return _hpMaxBaseline; }
        set { _hpMaxBaseline = value; }
    }

    public float AttackBaseline
    {
        get { return _attackBaseline; }
        set { _attackBaseline = value; }
    }

    public float DefenceBaseline
    {
        get { return _defenceBaseline; }
        set { _defenceBaseline = value; }
    }

    public float CritRateBaseline
    {
        get { return _critRateBaseline; }
        set { _critRateBaseline = value; }
    }

    public float CritDmgBaseline
    {
        get { return _critDmgBaseline; }
        set { _critDmgBaseline = value; }
    }

    public EPradonaPower PradonaPower
    {
        get { return _pradonaPower; }
        set { 
            _pradonaPower = value; 
            CPlayerUI_Build script = FindFirstObjectByType<CPlayerUI_Build>();
            if (script != null)
            {
                script.InvokePradonaChanged(ActivePradonaPower);
            }
        }
    }

    /// <summary>
    /// 훈련 중 임시로 받은 권능을 포함해 사용할 수 있는 권능을 반환합니다.
    /// </summary>
    public EPradonaPower ActivePradonaPower => _pradonaPower | _temporaryPradonaPower;
    internal float PradonaCooldownDuration => _pradonaCooldownSet;

    public bool PauseMainStatusTimer
    {
        get { return _pauseMainStatusTimer; }
        set { _pauseMainStatusTimer = value; }
    }

    /// <summary>
    /// 훈련 중 사용할 임시 권능을 설정합니다.
    /// </summary>
    public void SetTemporaryPradonaPower(EPradonaPower power)
    {
        _temporaryPradonaPower = power;
        CPlayerUI_Build script = FindFirstObjectByType<CPlayerUI_Build>();
        if (script != null) script.InvokePradonaChanged(ActivePradonaPower);
    }

    /// <summary>
    /// 프라도나 권능의 재사용 대기시간을 초기화합니다.
    /// </summary>
    public void ResetPradonaCooldown()
    {
        _pradonaCooldown = 0f;
    }

    public void EquipmentResync()
    {
        float hpRatio = HpCurrent / HpMax;
        _equipmentStatus.Clear();

        _hpMaxBaseline = _baseHpMax;
        _attackBaseline = _baseAttack;
        _defenceBaseline = _baseDefence;
        _critRateBaseline = 0.05f;
        _critDmgBaseline = 1.5f;
        _movementBaseline = 1f;
        _attackSpeedBaseline = 1f;

        Dictionary<int, int> equipmentSet = new Dictionary<int, int>();
        List<CItemEquipment> equipments = new List<CItemEquipment>();
        foreach (CItemEquipment equipment in _equipment.Values)
        {
            if (equipment != null)
            {
                equipments.Add(equipment);
            }
        }
        for (int i = 0; i < equipments.Count; i++)
        {
            if (equipments[i].SetNumber != 0)
            {
                if (equipmentSet.ContainsKey(equipments[i].SetNumber))
                {
                    equipmentSet[equipments[i].SetNumber] += 1;
                }
                else
                {
                    equipmentSet[equipments[i].SetNumber] = 1;
                }
            }
        }

        List<CSet> sets = new List<CSet>();
        foreach (int i in equipmentSet.Keys)
        {
            if (equipmentSet[i] >= 2)
            {
                sets.Add(CSet.MakeSet(i, equipmentSet[i]));
            }
        }

        List<SStatusOnApply> effects = new List<SStatusOnApply>();
        for (int i = 0; i < equipments.Count; i++)
        {
            effects.AddRange(equipments[i].OnSync());
        }
        for (int i = 0; i < sets.Count; i++)
        {
            effects.AddRange(sets[i].OnSync());
        }

        for (int i = 0; i < equipments.Count; i++)
        {
            equipments[i].OnStatusSync(this);
        }
        for (int i = 0; i < sets.Count; i++)
        {
            sets[i].OnStatusSync(this);
        }

        for (int i = 0; i < _equipmentStatus.Count; i++)
        {
            if (_equipmentStatus[i].Trigger.HasFlag(EStatusTrigger.OnApply))
            {
                effects.AddRange(_equipmentStatus[i].OnApply);
            }
        }

        Dictionary<EStatusStats, float> statConstant = new Dictionary<EStatusStats, float>();
        Dictionary<EStatusStats, float> statMultiple = new Dictionary<EStatusStats, float>();
        Dictionary<EStatusStats, float> statDivide = new Dictionary<EStatusStats, float>();
        foreach(EStatusStats stat in Enum.GetValues(typeof(EStatusStats)))
        {
            statConstant.Add(stat, 0f);
            statMultiple.Add(stat, 1f);
            statDivide.Add(stat, 1f);
        }

        for(int i = 0; i < effects.Count; i++)
        {
            if (effects[i].isConstant)
            {
                statConstant[effects[i].target] += effects[i].value;
            }
            else
            {
                if (effects[i].value < 1f)
                {
                    statDivide[effects[i].target] *= effects[i].value;
                }
                else
                {
                    statMultiple[effects[i].target] += effects[i].value - 1f;
                }
            }
        }

        _hpMaxBaseline = (_baseHpMax + statConstant[EStatusStats.Hp])
            * statMultiple[EStatusStats.Hp] * statDivide[EStatusStats.Hp];
        _attackBaseline = (_baseAttack + statConstant[EStatusStats.Attack])
            * statMultiple[EStatusStats.Attack] * statDivide[EStatusStats.Attack];
        _defenceBaseline = (_baseDefence + statConstant[EStatusStats.Defence])
            * statMultiple[EStatusStats.Defence] * statDivide[EStatusStats.Defence];
        _critRateBaseline = (0.05f + statConstant[EStatusStats.CritRate])
            * statMultiple[EStatusStats.CritRate] * statDivide[EStatusStats.CritRate];
        _critDmgBaseline = (1.5f + statConstant[EStatusStats.CritDmg])
            * statMultiple[EStatusStats.CritDmg] * statDivide[EStatusStats.CritDmg];
        _movementBaseline = (1f + statConstant[EStatusStats.Movement])
            * statMultiple[EStatusStats.Movement] * statDivide[EStatusStats.Movement];
        _attackSpeedBaseline = (1f + statConstant[EStatusStats.AttackSpeed])
            * statMultiple[EStatusStats.AttackSpeed] * statDivide[EStatusStats.AttackSpeed];

        _hpMax = _hpMaxBaseline;
        _hpCurrent = _hpMax * hpRatio;
        _attack = _attackBaseline;
        _defence = _defenceBaseline;
        _critRate = _critRateBaseline;
        _critDmg = _critDmgBaseline;
        _movementMultiply = _movementBaseline;
        _attackSpeedMultiply = _attackSpeedBaseline;

        ResyncStatus();
        SyncStatusEffect();
    }

    public override void ResyncStatus()
    {
        List<SStatusOnApply> effects = new List<SStatusOnApply>();

        for (int i = 0; i < _status.Count; i++)
        {
            if (_status[i].Trigger.HasFlag(EStatusTrigger.OnApply))
            {
                for (int j = 0; j < _status[i].OnApply.Count; j++)
                {
                    SStatusOnApply targetEffect = _status[i].OnApply[j];
                    if (targetEffect.applyStack)
                    {
                        if (targetEffect.isConstant) targetEffect.value *= _status[i].Stack;
                        else targetEffect.value = 1f + ((targetEffect.value - 1f) * _status[i].Stack);
                        targetEffect.applyStack = false;
                    }
                    effects.Add(targetEffect);
                }
            }
        }

        Dictionary<EStatusStats, float> statConstant = new Dictionary<EStatusStats, float>();
        Dictionary<EStatusStats, float> statMultiple = new Dictionary<EStatusStats, float>();
        Dictionary<EStatusStats, float> statDivide = new Dictionary<EStatusStats, float>();
        foreach (EStatusStats stat in Enum.GetValues(typeof(EStatusStats)))
        {
            statConstant.Add(stat, 0f);
            statMultiple.Add(stat, 1f);
            statDivide.Add(stat, 1f);
        }

        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i].isConstant)
            {
                statConstant[effects[i].target] += effects[i].value;
            }
            else
            {
                if (effects[i].value < 1f)
                {
                    statDivide[effects[i].target] *= effects[i].value;
                }
                else
                {
                    statMultiple[effects[i].target] += effects[i].value - 1f;
                }
            }
        }

        _hpMax = (_hpMaxBaseline + statConstant[EStatusStats.Hp])
            * statMultiple[EStatusStats.Hp] * statDivide[EStatusStats.Hp];
        _hpCurrent = Mathf.Min(_hpCurrent, _hpMax);
        _attack = (_attackBaseline + statConstant[EStatusStats.Attack])
            * statMultiple[EStatusStats.Attack] * statDivide[EStatusStats.Attack];
        _defence = (_defenceBaseline + statConstant[EStatusStats.Defence])
            * statMultiple[EStatusStats.Defence] * statDivide[EStatusStats.Defence];
        _critRate = (_critRateBaseline + statConstant[EStatusStats.CritRate])
            * statMultiple[EStatusStats.CritRate] * statDivide[EStatusStats.CritRate];
        _critDmg = (_critDmgBaseline + statConstant[EStatusStats.CritDmg])
            * statMultiple[EStatusStats.CritDmg] * statDivide[EStatusStats.CritDmg];
        _movementMultiply = (_movementBaseline + statConstant[EStatusStats.Movement])
            * statMultiple[EStatusStats.Movement] * statDivide[EStatusStats.Movement];
        _attackSpeedMultiply = (_attackSpeedBaseline + statConstant[EStatusStats.AttackSpeed])
            * statMultiple[EStatusStats.AttackSpeed] * statDivide[EStatusStats.AttackSpeed];
    }

    public override void InvokeOnDelta(float t)
    {
        if (!(_equipmentStatus == null || _equipmentStatus.Count == 0))
        {
            for (int i = _equipmentStatus.Count - 1; i >= 0; i--)
            {
                if (_equipmentStatus[i].Trigger.HasFlag(EStatusTrigger.OnDelta))
                {
                    for (int j = 0; j < _equipmentStatus[i].OnDelta.Count; j++)
                    {
                        _equipmentStatus[i].OnDelta[j](this, t);
                    }
                }
            }
        }
        base.InvokeOnDelta(t);
    }

    public override void InvokeOnBattleStart(bool isBossBattle)
    {
        if (!(_equipmentStatus == null || _equipmentStatus.Count == 0))
        {
            for (int i = _equipmentStatus.Count - 1; i >= 0; i--)
            {
                if (_equipmentStatus[i].Trigger.HasFlag(EStatusTrigger.OnBattleStart))
                {
                    for (int j = 0; j < _equipmentStatus[i].OnBattleStart.Count; j++)
                    {
                        _equipmentStatus[i].OnBattleStart[j](this, isBossBattle);
                    }
                }
            }
        }
        base.InvokeOnBattleStart(isBossBattle);
    }

    public override void InvokeOnBattleEnd(bool isBossBattle)
    {
        if (!(_equipmentStatus == null || _equipmentStatus.Count == 0))
        {
            for (int i = _equipmentStatus.Count - 1; i >= 0; i--)
            {
                if (_equipmentStatus[i].Trigger.HasFlag(EStatusTrigger.OnBattleEnd))
                {
                    for (int j = 0; j < _equipmentStatus[i].OnBattleEnd.Count; j++)
                    {
                        _equipmentStatus[i].OnBattleEnd[j](this, isBossBattle);
                    }
                }
            }
        }
        base.InvokeOnBattleEnd(isBossBattle);
    }

    public void GetSkillCooldown(ESkillType type, out float ratio, out float remainCooldown)
    {
        _skill.GetSkillCooldown(type, out ratio, out remainCooldown);
    }

    public bool GetSkillAvailable(ESkillType type)
    {
        return _skill.GetSkillAvailable(type);
    }

    public float GetCrownRemaining => _skill.CrownWindowRemaining;

    protected override void Awake()
    {
        base.Awake();
        CStatusSO.Build(false);
        _movement = GetComponent<CPlayerMovement>();
        _skill = GetComponent<CPlayerSkill>();
        foreach(EEquipmentSlot slot in System.Enum.GetValues(typeof(EEquipmentSlot)))
        {
            _equipment[slot] = null;
        }
    }

    /// <summary>
    /// 플레이어의 무적 상태를 켜거나 끕니다.
    /// </summary>

    public void SetInvincible(bool isInvincible)
    {
        _isInvincible = isInvincible;
    }

    /// <summary>
    /// 대화·연출 중 공격, 스킬, 대쉬, 이동, 퀵슬롯 입력을 잠그거나 풉니다. 잠글 때 이동 목표를 멈춥니다.
    /// </summary>
    public void SetInputLocked(bool isLocked)
    {
        _isInputLocked = isLocked;

        if (isLocked && _movement != null)
        {
            _movement.Stop();
        }
    }

    /// <summary>
    /// 무적 상태가 아닐 때 체력 피해를 적용합니다.
    /// </summary>

    public override void GetDamage(float value, ERatioSwitch ratioSwitch = ERatioSwitch.Constant, bool penetrateInvincible = false)
    {
        if (_isInvincible)
        {
            return;
        }

        base.GetDamage(value, ratioSwitch, penetrateInvincible);
    }

    /// <summary>
    /// 기본 공격 한 번이 적중했을 때 MP를 회복합니다.
    /// </summary>

    public void RecoverBasicAttackMp()
    {
        CurrentMp = Mathf.Min(CurrentMp + _mpPerBasicHit, _maxMp);
    }

    /// <summary>
    /// 이어 받은 현재 MP 를 그대로 넣습니다 (맵 이동). 0 ~ 최대 MP 로 자릅니다.
    /// </summary>
    public void RestoreMp(int value)
    {
        CurrentMp = Mathf.Clamp(value, 0, _maxMp);
    }

    internal bool TrySpendMp(int cost)
    {
        if (cost > CurrentMp)
        {
            return false;
        }

        CurrentMp -= cost;
        return true;
    }

    /// <summary>
    /// 적의 공격으로 체력이 감소하면 전투의 흐름을 차감합니다.
    /// </summary>

    public override void OnHit(CUnit attacker, CUnit defender, SDamageInfo damage, bool penetrateInvincible = false)
    {
        float previousHp = HpCurrent;
        base.OnHit(attacker, defender, damage, penetrateInvincible);

        if (defender == this && HpCurrent < previousHp)
        {
            _passive.OnPlayerDamaged(this);
        }
    }

    private void OnEnable()
    {
        _moveClick?.action?.Enable();
        _pointerPosition?.action?.Enable();
        _attackInput?.action?.Enable();
        _dashInput?.action?.Enable();
        _pradonaInput?.action?.Enable();

        if (_skillSlots == null)
        {
            return;
        }

        foreach (SSkillSlot slot in _skillSlots)
        {
            slot.input?.action?.Enable();
        }

        if (_quickslotInputs != null)
        {
            foreach (InputActionReference input in _quickslotInputs)
            {
                input?.action?.Enable();
            }
        }
    }

    private void OnDisable()
    {
        _moveClick?.action?.Disable();
        _pointerPosition?.action?.Disable();
        _attackInput?.action?.Disable();
        _dashInput?.action?.Disable();
        _pradonaInput?.action?.Disable();

        if (_skill != null)
        {
            _skill.CancelSkill();
        }

        if (_movement != null)
        {
            _movement.Stop();
        }

        if (_skillSlots == null)
        {
            return;
        }

        foreach (SSkillSlot slot in _skillSlots)
        {
            slot.input?.action?.Disable();
        }
        if (_quickslotInputs != null)
        {
            foreach (InputActionReference input in _quickslotInputs)
            {
                input?.action?.Disable();
            }
        }
    }

    private void Start()
    {
        if (_mainCamera == null || _moveClick?.action == null || _pointerPosition?.action == null ||
            _attackInput?.action == null || _dashInput?.action == null || _movement == null ||
            !_movement.IsConfigured || _skill == null ||
            !_skill.Initialize(this))
        {
            CLogger.Error($"{name}: 카메라, 입력, 이동 컴포넌트와 스킬 설정을 확인해주세요.");
            enabled = false;
            return;
        }

        if (_skillSlots == null)
        {
            return;
        }

        foreach (SSkillSlot slot in _skillSlots)
        {
            if (slot.input?.action != null && _skill.RegisterSkill(slot.skill))
            {
                continue;
            }

            CLogger.Error($"{name}: 스킬 슬롯의 입력과 스킬 설정을 확인해주세요.");
            enabled = false;
            return;
        }

        EquipmentResync();
    }

    [ContextMenu("Status Debug")]
    public void SetStatusDebug()
    {
        EquipmentResync();
        CLogger.Success($"스테이터스 다시 받아옴");
    }

    [ContextMenu("Equip Debug sets")]
    public void GetDebugSet()
    {
        if (CSetSO.Contains(_debugSet))
        {
            List<EEquipmentSlot> slots = new List<EEquipmentSlot>(_equipment.Keys);
            foreach (EEquipmentSlot slot in slots)
            {
                if (_equipment[slot] == null) continue;
                _inventory.TryUnequipItem(slot, false);
            }
            CItemEquipment[] tempItems = new CItemEquipment[4];
            int firstItemId = _debugSet + (_debugSet % 100 - 1) * 15 + 12;
            for (int i = 0; i < tempItems.Length; i++)
            {
                tempItems[i] = (CItemEquipment)CItem.MakeItem(firstItemId + i);
                CLogger.Log($"디버그용 아이템 장착 -> {i} -> {tempItems[i].Name}");
                tempItems[i].OnEquip(this);
            }
            EquipmentResync();
        }
        else
        {
            CLogger.Error("CPlayer : _debugSet이 유효하지 않음.");
        }
    }

    [ContextMenu("Pradona Debug")]
    public void SetPradonaDebug()
    {
        CLogger.Success($"Pradona power -> {_pradonaPowerDebug}");
        PradonaPower = _pradonaPowerDebug;
    }

    protected override void Update()
    {
        base.Update();
        if (Time.timeScale <= 0f) return;
        _movement.ApplyGravity();
        _pradonaCooldown = Mathf.Max(0f, _pradonaCooldown - Time.deltaTime);

        if (HpCurrent <= 0f)
        {
            if (!_actionsBlocked)
            {
                _skill.CancelSkill();
            }

            _actionsBlocked = true;
            return;
        }

        _actionsBlocked = false;

        UpdateQuickslotInput();

        if (!_isInputLocked && !IsStunned && !EscapeActionsBlocked && _dashInput.action.WasPressedThisFrame() &&
            _skill.TryDash(GetPointerDirection()))
        {
            return;
        }

        bool isCameraBlocked = _testCamera != null && _testCamera.IsInputBlocked;
        bool canAct = !_isInputLocked && !isCameraBlocked;
        // UI 위에서 누른 마우 스 입력만 막는다. 같은 액션의 키보드 입력(C)은 그대로 둔다.
        bool isPointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (canAct && !EscapeActionsBlocked &&
            Time.timeScale > 0f &&
            ActivePradonaPower != EPradonaPower.None &&
            _pradonaCooldown <= 0f &&
            _pradonaInput?.action?.WasPressedThisFrame() == true)
        {
            CastPradonaPower();
        }

        if (canAct && !IsStunned)
        {
            if (_attackInput.action.WasPressedThisFrame() && !(isPointerOverUi && IsPointerControl(_attackInput.action)))
            {
                _skill.TryAttack(GetPointerDirection());
            }

            if (_skillSlots != null)
            {
                foreach (SSkillSlot slot in _skillSlots)
                {
                    if (EscapeActionsBlocked && slot.skill && slot.skill.Type == ESkillType.Rush) continue;
                    if (slot.input.action.WasPressedThisFrame() && _skill.TryUse(slot.skill, GetPointerDirection()))
                    {
                        break;
                    }
                }
            }
        }

        _skill.UpdateSkill();

        if (_skill.IsBusy)
        {
            return;
        }

        if (canAct && !isPointerOverUi && _moveClick.action.IsPressed() &&
            TryGetPointerGroundPosition(out Vector3 position))
        {
            _movement.SetMoveTarget(position);
        }
        if (!IsStunned) _movement.Move(_movementMultiply);
        else _movement.Stop();
    }

    private static bool IsPointerControl(InputAction action)
    {
        return action.activeControl != null && action.activeControl.device is Pointer;
    }

    internal bool WasSkillPressed(ESkillType type)
    {
        if (type == ESkillType.Dash) return _dashInput?.action?.WasPressedThisFrame() == true;
        if (_skillSlots == null) return false;

        foreach (SSkillSlot slot in _skillSlots)
        {
            if (slot.skill != null && slot.skill.Type == type && slot.input?.action?.WasPressedThisFrame() == true)
            {
                return true;
            }
        }

        return false;
    }

    internal Vector3 GetPointerDirection()
    {
        if (TryGetPointerGroundPosition(out Vector3 position))
        {
            Vector3 direction = position - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                return direction.normalized;
            }
        }

        return transform.forward;
    }

    private bool TryGetPointerGroundPosition(out Vector3 position)
    {
        Vector2 mousePosition = _pointerPosition.action.ReadValue<Vector2>();
        Ray ray = _mainCamera.ScreenPointToRay(mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, _groundLayer))
        {
            position = hit.point;
            return true;
        }

        position = default;
        return false;
    }

    private void UpdateQuickslotInput()
    {
        if (_isInputLocked || _inventory == null || _quickslotInputs == null || Time.timeScale <= 0f) return;

        bool isBattle = CGameManager.Instance.BattleState != EBattleState.None;
        int count = Mathf.Min(_quickslotInputs.Length, _inventory.QuickSlotLength);

        for (int i = 0; i < count; i++)
        {
            InputAction action = _quickslotInputs[i]?.action;
            if (action != null && action.WasPressedThisFrame())
            {
                _inventory.TryUseQuickSlot(i, isBattle);
            }
        }
    }

    private void CastPradonaPower()
    {
        _pradonaCooldown = _pradonaCooldownSet;
        EPradonaPower power = ActivePradonaPower;
        if (power.HasFlag(EPradonaPower.Invidia))
        {
            AddStatus(31);
        }
        if (power.HasFlag(EPradonaPower.Avaritia))
        {
            AddStatus(32);
        }
        if (power.HasFlag(EPradonaPower.Pigritia))
        {
            AddStatus(34);
        }
        if (power.HasFlag(EPradonaPower.Gula))
        {
            _skill.ResetCooldowns(true, true, false);
        }
        if (power.HasFlag(EPradonaPower.Luxuria))
        {
            for (int i = _status.Count - 1; i >= 0; i--)
            {
                CStatus status = _status[i];
                if (status.Trigger.HasFlag(EStatusTrigger.IsStun))
                {
                    RemoveStatus(status);
                }
            }
        }
        if (power.HasFlag(EPradonaPower.Ira))
        {
            AddStatus(35);
        }
    }

    public void GetPradonaCooldown(out float ratio, out float remainCooldown)
    {
        remainCooldown = _pradonaCooldown;
        ratio = _pradonaCooldown / _pradonaCooldownSet;
    }

    internal void Kill()
    {
        if (HpCurrent > 0f) base.GetDamage(HpCurrent, ERatioSwitch.Constant, true);
    }

    protected override void Die()
    {
        base.Die();
        if (_actionsBlocked) return;

        _actionsBlocked = true;
        _movement?.Stop();
        _skill?.CancelSkill();

        CGameManager.Instance?.SetGameState(EGameState.Gameover);
    }
    protected override bool ShouldAdvanceStatusDuration(CStatus status)
    {
        return !_pauseMainStatusTimer ||
            (
                status.Id != 25 && // 왕관
                status.Id != 29 && // 레비아탄
                status.Id != 10 // 질투
            );
    }
}
