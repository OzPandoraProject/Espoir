using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IBoss
{
    bool IsGroggy { get; }
    public void ResetBattleState();
    public bool TryGetGroggyInfo(out float remainHp, out float maxHp, out float remainTime, out float maxTime);
}

public class CEnemy : CUnit
{
    [Header("몬스터 이름 및 설명")]
    [SerializeField] protected string _name;
    [SerializeField] protected string _nickname;

    [Header("몬스터 정보 (테스트 전용, 우선시됨)")]
    [SerializeField] private CEnemyStatSO _targetStatSO;

    [Header("드롭하는 아이템 범위")]
    [SerializeField] private int _dropCount = 1;
    [SerializeField] private List<SDropTable> _drops;

    [Header("퀘스트 대상 판정")]
    [Tooltip("직접 배치한 몬스터의 원본 프리팹입니다. 스포너는 소환할 때 자동으로 지정합니다.")]
    [SerializeField] private CEnemy _sourcePrefab;

    private bool _hasReportedKill;
    private bool _isDropped = false;
    protected bool _isChasingPlayer = false;

    public string Name => _name;
    public string Nickname => _nickname;
    public virtual bool IsChasingPlayer => _isChasingPlayer;
    public virtual string DisplayName => string.Empty;

    /// <summary>
    /// 퀘스트의 대상을 판별할 때 사용하는 원본 프리팹입니다.
    /// </summary>
    public CEnemy SourcePrefab => _sourcePrefab;

    /// <summary>
    /// 보스의 식별값. 일반 몬스터는 None.
    /// </summary>
    public virtual EBossClear BossId => EBossClear.None;

    public List<SDropTable> Drops
    {
        get { return _drops; }
        set { _drops = value; }
    }

    protected override void Awake()
    {
        base.Awake();
        _baseCritRate = 0f;
        _critRate = _baseCritRate;
        if (_targetStatSO != null)
        {
            Setup(_targetStatSO);
        }
    }

    public void Setup(CEnemyStatSO target)
    {
        this._baseHpMax = target.Hp;
        this._baseAttack = target.Attack;
        this._baseDefence = target.Defence;

        this._hpMax = _baseHpMax;
        this._hpCurrent = _hpMax;
        this._attack = _baseAttack;
        this._defence = _baseDefence;
    }

    protected virtual void Start()
    {
        
    }

    protected override void Update()
    {
        base.Update();
    }

    /// <summary>
    /// 스포너가 소환한 몬스터의 원본 프리팹을 기록합니다.
    /// </summary>
    public void SetSourcePrefab(CEnemy prefab)
    {
        _sourcePrefab = prefab;
    }

    // 디버그용
    public void CallDie()
    {
        this.Die();
    }

    /// <summary>
    /// 체력을 회복하고 되살아난 몬스터의 처치 판정을 초기화합니다.
    /// </summary>
    public override void GetHeal(float value, ERatioSwitch ratioSwitch = ERatioSwitch.Constant,
        bool forceHeal = false)
    {
        base.GetHeal(value, ratioSwitch, forceHeal);
        if (HpCurrent > 0f) _hasReportedKill = false;
    }

    protected override void Die()
    {
        base.Die();
        if (HpCurrent <= 0f && !_hasReportedKill)
        {
            _hasReportedKill = true;
            CQuestManager.ReportKill(this);
        }

        CPlayer player = FindFirstObjectByType<CPlayer>();
        if (!_isDropped)
        {
            _isDropped = true;
            if (player != null && player.Inventory != null && _dropCount > 0)
            {
                bool inventoryFull = false;

                for (int i = 0; i < _dropCount; i++)
                {
                    if (!CItemSO.TryMakeDropItem(_drops, out CItemSO targetItem))
                    {
                        continue;
                    }

                    if (player.Inventory.AddItem(targetItem.Id) == 0 &&
                        player.Inventory.FindEmptySlot() < 0)
                    {
                        inventoryFull = true;
                    }
                }

                if (inventoryFull)
                {
                    CGameManager.Instance?.ShowError("가방이 가득 차 아이템을 받을 수 없습니다.", Color.red, true);
                }
            }
        }
    }

    public void ResetDropState()
    {
        _isDropped = false;
    }
}
