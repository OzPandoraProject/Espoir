using System;
using System.Collections.Generic;
using UnityEngine;
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

    [Header("카메라 설정")]
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private CTestCamera _testCamera;
    [SerializeField] private LayerMask _groundLayer;

    [Header("입력 설정")]
    [SerializeField] private InputActionReference _moveClick;
    [SerializeField] private InputActionReference _pointerPosition;
    [SerializeField] private InputActionReference _attackInput;
    [SerializeField] private InputActionReference _dashInput;
    [SerializeField] private SSkillSlot[] _skillSlots;

    private CPlayerMovement _movement;
    private CPlayerSkill _skill;
    private Dictionary<EEquipmentSlot, CItemEquipment> _equipment = new Dictionary<EEquipmentSlot, CItemEquipment>();
    private List<CStatus> _equipmentStatus = new List<CStatus>();
    private bool _actionsBlocked;

    protected float _hpMaxBaseline = 1;
    protected float _attackBaseline = 0;
    protected float _defenceBaseline = 0;

    protected float _critRateBaseline = 0.05f;
    protected float _critDmgBaseline = 1.5f;

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


    public void EquipmentResync()
    {
        if (_equipmentStatus != null && _equipmentStatus.Count > 0)
        {
            for (int i = 0; i < _equipmentStatus.Count; i++)
            {
                if (!_equipmentStatus[i].Trigger.HasFlag(EStatusTrigger.OnRemove)) continue;
                for (int j = 0; j < _equipmentStatus[i].OnRemove.Count; j++)
                {
                    _equipmentStatus[i].OnRemove[j](this);
                }
            }
        }
        if (_status != null && _status.Count > 0)
        {
            for (int i = 0; i < _status.Count; i++)
            {
                if (!_status[i].Trigger.HasFlag(EStatusTrigger.OnRemove)) continue;
                for (int j = 0; j < _status[i].OnRemove.Count; j++)
                {
                    _status[i].OnRemove[j](this);
                }
            }
        }

        _equipmentStatus.Clear();

        _hpMaxBaseline = _baseHpMax;
        _attackBaseline = _baseAttack;
        _defenceBaseline = _baseDefence;
        _critRateBaseline = 0.05f;
        _critDmgBaseline = 1.5f;

        Dictionary<int, int> equipmentSet = new Dictionary<int, int>();
        List<CItemEquipment> equipments = new List<CItemEquipment>(_equipment.Values);
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
            equipments[i].OnEarlySync(this);
        }

        List<CSet> sets = new List<CSet>();
        foreach (int i in equipmentSet.Keys)
        {
            if (equipmentSet[i] >= 2)
            {
                sets.Add(CSet.MakeSet(i, equipmentSet[i]));
            }
        }

        for (int i = 0; i < sets.Count; i++)
        {
            sets[i].OnEarlySync(this);
        }
        for (int i = 0; i < equipments.Count; i++)
        {
            equipments[i].OnLateSync(this);
        }
        for (int i = 0; i < sets.Count; i++)
        {
            sets[i].OnLateSync(this);
        }
        for (int i = 0; i < equipments.Count; i++)
        {
            equipments[i].OnStatusSync(this);
        }
        for (int i = 0; i < sets.Count; i++)
        {
            sets[i].OnStatusSync(this);
        }
        _hpMax = _hpMaxBaseline;
        _attack = _attackBaseline;
        _defence = _defenceBaseline;
        _critRate = _critRateBaseline;
        _critDmg = _critDmgBaseline;
        if (_equipmentStatus != null && _equipmentStatus.Count > 0)
        {
            for (int i = 0; i < _equipmentStatus.Count; i++)
            {
                if (!_equipmentStatus[i].Trigger.HasFlag(EStatusTrigger.OnApply)) continue;
                for (int j = 0; j < _equipmentStatus[i].OnApply.Count; j++)
                {
                    _equipmentStatus[i].OnApply[j](this);
                }
            }
        }

        if (_status != null && _status.Count > 0)
        {
            for (int i = 0; i < _status.Count; i++)
            {
                if (!_status[i].Trigger.HasFlag(EStatusTrigger.OnApply)) continue;
                for (int j = 0; j < _status[i].OnRemove.Count; j++)
                {
                    _status[i].OnApply[j](this);
                }
            }
        }
    }

    protected override void Awake()
    {
        base.Awake();
        _movement = GetComponent<CPlayerMovement>();
        _skill = GetComponent<CPlayerSkill>();
    }

    private void OnEnable()
    {
        _moveClick?.action?.Enable();
        _pointerPosition?.action?.Enable();
        _attackInput?.action?.Enable();
        _dashInput?.action?.Enable();

        if (_skillSlots == null)
        {
            return;
        }

        foreach (SSkillSlot slot in _skillSlots)
        {
            slot.input?.action?.Enable();
        }
    }

    private void OnDisable()
    {
        _moveClick?.action?.Disable();
        _pointerPosition?.action?.Disable();
        _attackInput?.action?.Disable();
        _dashInput?.action?.Disable();

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
    }

    private void Update()
    {
        _movement.ApplyGravity();

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

        if (_dashInput.action.WasPressedThisFrame() && _skill.TryDash())
        {
            return;
        }

        bool isCameraBlocked = _testCamera != null && _testCamera.IsInputBlocked;

        if (!isCameraBlocked)
        {
            if (_attackInput.action.WasPressedThisFrame())
            {
                _skill.TryAttack(GetAttackDirection());
            }

            if (_skillSlots != null)
            {
                foreach (SSkillSlot slot in _skillSlots)
                {
                    if (slot.input.action.WasPressedThisFrame() && _skill.TryUse(slot.skill, GetAttackDirection()))
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

        if (!isCameraBlocked && _moveClick.action.IsPressed() && TryGetPointerGroundPosition(out Vector3 position))
        {
            _movement.SetMoveTarget(position);
        }

        _movement.Move();
    }

    private Vector3 GetAttackDirection()
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
}
