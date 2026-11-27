using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CQuestSaveData
{
    public string questId;
    public int state;
    public string objectiveId;
    public int count;
    public bool supplied;
    public int suppliedCount;
    public bool hasSupplyHistory;
    public List<int> suppliedItemIds;
    public bool warnedForDiscard;
    public int pendingReplacementId;
    public List<int> usedItemIds;
    public bool cameraRotated;
    public bool cameraZoomed;
}

public static class CQuestManager
{
    private class CRecord
    {
        public CQuestSO quest;
        public EQuestState state;
        public int index;
        public int count;
        public int suppliedCount;
        public float nextSupplyTime;
        public bool warnedForDiscard;
        public int pendingReplacementId;
        public readonly HashSet<int> suppliedItems = new HashSet<int>();
        public CUnit statusUnit;
        public bool cameraRotated;
        public bool cameraZoomed;
        public readonly HashSet<int> usedItems = new HashSet<int>();
    }

    private const float _SUPPLY_RETRY_SECONDS = 3f;

    private static readonly Dictionary<string, CQuestSO> _QUESTS = new Dictionary<string, CQuestSO>();
    private static readonly Dictionary<string, CRecord> _RECORDS = new Dictionary<string, CRecord>();
    private static readonly List<CRecord> _ACTIVE = new List<CRecord>();
    private static bool _isBuilt;
    private static bool _isReady;
    private static int _version;
    private static CPlayer _player;

    public static bool IsReady => _isReady;
    internal static bool HasTrackedQuests => _isReady && _ACTIVE.Count > 0;

    public static int Version => _version;

    public static int MaxFinisherStack => Leviathan?.MaxStack ?? 0;

    private static CPlayerLeviathan Leviathan =>
        _player != null && _player.Skill != null ? _player.Skill.Leviathan : null;

    public static event Action<CQuestSO, CItemSO> SupplyFailed;

    public static bool IsSupplyHeld { get; set; }

    /// <summary>
    /// 퀘스트 진행 기록을 초기화합니다.
    /// </summary>
    public static void Clear()
    {
        _RECORDS.Clear();
        _ACTIVE.Clear();
        _isReady = false;
        _version++;
    }

    /// <summary>
    /// 저장 슬롯의 퀘스트 진행 기록을 복원합니다.
    /// </summary>
    public static void Restore(List<CQuestSaveData> saved)
    {
        Build();
        _RECORDS.Clear();
        _ACTIVE.Clear();

        if (saved != null)
        {
            foreach (CQuestSaveData data in saved)
            {
                if (data == null || !_QUESTS.TryGetValue(data.questId ?? string.Empty, out CQuestSO quest)) continue;

                CRecord record = new CRecord { quest = quest, state = (EQuestState)data.state };
                if (record.state == EQuestState.Active)
                {
                    record.index = quest.FindObjectiveIndex(data.objectiveId);

                    if (record.index < 0)
                    {
                        CLogger.Warn($"CQuestManager : '{quest.Title}' 의 저장된 목표를 찾지 못해 첫 목표부터 엽니다.");
                        record.index = 0;
                    }
                    else
                    {
                        record.count = Mathf.Max(0, data.count);
                        CQuestObjective objective = quest.GetObjective(record.index);
                        record.suppliedCount = data.supplied ? objective.supplyCount : Mathf.Max(0, data.suppliedCount);
                        if (data.usedItemIds != null) record.usedItems.UnionWith(data.usedItemIds);
                        if (objective.kind == EQuestObjectiveKind.Camera)
                        {
                            record.cameraRotated = data.cameraRotated;
                            record.cameraZoomed = data.cameraZoomed;
                        }
                    }
                }

                if (record.state == EQuestState.Active || record.state == EQuestState.Reportable)
                {
                    RestoreSupplyHistory(record, data);
                }
                _RECORDS[quest.QuestId] = record;
            }
        }

        RebuildActive();
        _isReady = true;
        _version++;
    }

    private static void RestoreSupplyHistory(CRecord record, CQuestSaveData data)
    {
        if (data.hasSupplyHistory)
        {
            if (data.suppliedItemIds != null) record.suppliedItems.UnionWith(data.suppliedItemIds);
            record.warnedForDiscard = data.warnedForDiscard;
            record.pendingReplacementId = data.pendingReplacementId;
            return;
        }

        int stepCount = record.state == EQuestState.Reportable ? record.quest.Objectives.Count : record.index;
        if (record.state == EQuestState.Active && record.suppliedCount > 0) stepCount++;
        for (int i = 0; i < stepCount; i++)
        {
            CQuestObjective objective = record.quest.GetObjective(i);
            if (objective == null || objective.supplyItem == null || objective.replacementItem == null ||
                objective.supplyItem != objective.item) continue;
            record.suppliedItems.Add(objective.supplyItem.Id);
        }
    }

    /// <summary>
    /// 저장할 퀘스트 진행 기록을 반환합니다.
    /// </summary>
    public static List<CQuestSaveData> Export()
    {
        List<CQuestSaveData> result = new List<CQuestSaveData>();

        foreach (CRecord record in _RECORDS.Values)
        {
            if (record.state == EQuestState.None) continue;

            CQuestObjective objective = record.state == EQuestState.Active
                ? record.quest.GetObjective(record.index) : null;
            result.Add(new CQuestSaveData
            {
                questId = record.quest.QuestId,
                state = (int)record.state,
                objectiveId = objective != null ? objective.id : string.Empty,
                count = objective != null ? record.count : 0,
                supplied = objective != null && objective.supplyItem != null &&
                    record.suppliedCount >= objective.supplyCount,
                suppliedCount = record.suppliedCount,
                hasSupplyHistory = true,
                suppliedItemIds = record.suppliedItems.Count > 0 ? new List<int>(record.suppliedItems) : null,
                warnedForDiscard = record.warnedForDiscard,
                pendingReplacementId = record.pendingReplacementId,
                cameraRotated = record.cameraRotated,
                cameraZoomed = record.cameraZoomed,
                usedItemIds = objective != null && record.usedItems.Count > 0 ? new List<int>(record.usedItems) : null,
            });
        }

        return result;
    }

    /// <summary>
    /// 퀘스트의 진행 상태를 반환합니다.
    /// </summary>
    public static EQuestState GetState(CQuestSO quest)
    {
        return TryGetRecord(quest, out CRecord record) ? record.state : EQuestState.None;
    }

    /// <summary>
    /// 현재 목표의 순번과 진행 수량을 반환합니다.
    /// </summary>
    public static bool TryGetCurrent(CQuestSO quest, out int index, out int count)
    {
        index = -1;
        count = 0;
        if (!TryGetRecord(quest, out CRecord record) ||
            record.state != EQuestState.Active) return false;

        index = record.index;
        count = record.count;
        return true;
    }

    /// <summary>
    /// 진행 중이거나 보고할 수 있는 퀘스트를 목록에 채웁니다.
    /// </summary>
    public static void GetTracked(List<CQuestSO> result)
    {
        result.Clear();

        foreach (CRecord record in _ACTIVE)
        {
            result.Add(record.quest);
        }
    }

    /// <summary>
    /// 선행 퀘스트와 보스 처치 조건을 충족해 수락할 수 있는지 확인합니다.
    /// </summary>
    public static bool CanAccept(CQuestSO quest)
    {
        if (!_isReady || quest == null || string.IsNullOrEmpty(quest.QuestId) || quest.Objectives.Count == 0 ||
            !_QUESTS.TryGetValue(quest.QuestId, out CQuestSO registered) || registered != quest) return false;
        if (GetState(quest) != EQuestState.None) return false;
        if (quest.Prerequisite != null && GetState(quest.Prerequisite) != EQuestState.Completed) return false;

        EBossClear cleared = CGameManager.Instance?.BossClearState ?? EBossClear.None;
        return (cleared & quest.RequiredBosses) == quest.RequiredBosses;
    }

    /// <summary>
    /// 새 퀘스트를 수락하면 true를 반환합니다.
    /// </summary>
    public static bool Accept(CQuestSO quest)
    {
        if (!CanAccept(quest)) return false;

        CRecord record = new CRecord { quest = quest, state = EQuestState.Active };
        _RECORDS[quest.QuestId] = record;
        _ACTIVE.Add(record);
        _version++;
        return true;
    }

    /// <summary>
    /// 달성한 퀘스트를 보고하면 true를 반환합니다.
    /// </summary>
    public static bool Report(CQuestSO quest)
    {
        if (!_isReady || !TryGetRecord(quest, out CRecord record)) return false;
        if (record.state != EQuestState.Reportable) return false;

        EBossClear reward = quest.BossClearReward;
        if (reward != EBossClear.None)
        {
            if (CGameManager.Instance == null)
            {
                CLogger.Error("CGameManager가 없음!");
                return false;
            }

            foreach (EBossClear boss in Enum.GetValues(typeof(EBossClear)))
            {
                // None과 AvailableBoss 같은 복합 값 건너뛰기
                if (boss == EBossClear.None || (boss & (boss - 1)) != 0) continue;

                if ((reward & boss) != 0)
                {
                    CGameManager.Instance.SetBossCleared(boss);
                }
            }
        }

        record.state = EQuestState.Completed;
        record.pendingReplacementId = 0;
        record.warnedForDiscard = false;
        record.suppliedItems.Clear();
        _ACTIVE.Remove(record);
        _version++;
        return true;
    }

    /// <summary>
    /// 진행 중인 목표의 보급, 장착, 도착 조건을 확인합니다.
    /// </summary>
    public static void Tick(CPlayer player)
    {
        _player = player;
        if (!_isReady || player == null || _ACTIVE.Count == 0) return;

        for (int i = 0; i < _ACTIVE.Count; i++)
        {
            CRecord record = _ACTIVE[i];
            if (record.state != EQuestState.Active) continue;

            CQuestObjective objective = record.quest.GetObjective(record.index);
            if (objective == null) continue;

            if (objective.kind == EQuestObjectiveKind.UseItem && objective.countEarlierUse && objective.count <= 1 &&
                ((objective.item != null && record.usedItems.Remove(objective.item.Id)) ||
                 (objective.replacementItem != null && record.usedItems.Remove(objective.replacementItem.Id))))
            {
                Advance(record);
                continue;
            }

            if (!IsSupplyHeld) TrySupply(record, objective, player);

            if (objective.kind == EQuestObjectiveKind.Equip && IsEquipped(player, objective))
            {
                Advance(record);
            }
            else if (objective.kind == EQuestObjectiveKind.Arrive && IsArrived(objective, player.transform.position))
            {
                Advance(record);
            }
        }
    }

    /// <summary>
    /// 처치한 적과 일치하는 목표의 수량을 올립니다.
    /// </summary>
    public static void ReportKill(CEnemy enemy)
    {
        if (enemy == null) return;

        ForEachCurrent(EQuestObjectiveKind.Kill, (record, objective) => IsTarget(objective, enemy));
    }

    /// <summary>
    /// 스킬 적중 시 대상과 연계 조건을 확인합니다.
    /// </summary>
    public static void ReportSkillHit(CSkillSO skill, CUnit target, bool isLink, bool isFinisher)
    {
        if (skill == null) return;

        ForEachCurrent(EQuestObjectiveKind.SkillHit, (record, objective) =>
            objective.HasSkill(skill) && (!objective.requireLink || isLink) &&
            (!objective.requireFinisher || isFinisher) && IsTarget(objective, target as CEnemy));
    }

    /// <summary>
    /// 시전에 성공한 스킬을 사용 목표에 반영합니다.
    /// </summary>
    public static void ReportSkillUse(CSkillSO skill)
    {
        if (skill == null) return;

        ForEachCurrent(EQuestObjectiveKind.SkillUse, (record, objective) => objective.HasSkill(skill));
    }

    /// <summary>
    /// 대상의 현재 상태 중첩이 목표에 도달했는지 확인합니다.
    /// </summary>
    public static void ReportStatusStack(CUnit owner, int statusId, int stack)
    {
        if (owner == null) return;

        ForEachCurrent(EQuestObjectiveKind.StatusStack, (record, objective) =>
        {
            if (objective.status == null || objective.status.Id != statusId) return false;

            bool isOwner = objective.statusOwner == EQuestStatusOwner.Player
                ? owner is CPlayer
                : IsTarget(objective, owner as CEnemy);
            if (!isOwner) return false;

            if (record.statusUnit != owner)
            {
                record.statusUnit = owner;
                _version++;
            }

            return stack >= objective.stack;
        });
    }

    /// <summary>
    /// 현재 목표의 누적 수량이나 중첩 수를 반환합니다.
    /// </summary>
    public static int GetLiveProgress(CQuestSO quest)
    {
        if (!TryGetRecord(quest, out CRecord record) ||
            record.state != EQuestState.Active) return 0;

        CQuestObjective objective = quest.GetObjective(record.index);
        if (objective == null) return record.count;

        if (objective.kind == EQuestObjectiveKind.StatusStack && objective.status != null)
        {
            CUnit unit = objective.statusOwner == EQuestStatusOwner.Player ? _player : record.statusUnit;
            return unit != null ? unit.FindStatusStack(objective.status.Id) : 0;
        }

        if (objective.kind == EQuestObjectiveKind.LeviathanFinisher)
        {
            return Leviathan?.Stack ?? 0;
        }

        return record.count;
    }

    /// <summary>
    /// 쿨타임이 줄어든 스킬을 목표에 반영합니다.
    /// </summary>
    public static void ReportCooldownReduced(CSkillSO skill)
    {
        if (skill == null) return;

        ForEachCurrent(EQuestObjectiveKind.CooldownReduced, (record, objective) => objective.HasSkill(skill));
    }

    /// <summary>
    /// 사용한 소모품을 퀘스트 진행에 반영합니다.
    /// </summary>
    public static void ReportItemUsed(int itemId)
    {
        foreach (CRecord record in _ACTIVE)
        {
            if (record.state != EQuestState.Active) continue;
            CQuestObjective objective = record.quest.GetObjective(record.index);
            if (objective != null && objective.kind == EQuestObjectiveKind.UseItem && objective.MatchesItem(itemId))
            {
                AddProgress(record, objective);
                continue;
            }

            for (int i = record.index + 1; i < record.quest.Objectives.Count; i++)
            {
                CQuestObjective next = record.quest.GetObjective(i);
                if (next == null || next.kind != EQuestObjectiveKind.UseItem || !next.countEarlierUse ||
                    next.count != 1 || !next.MatchesItem(itemId)) continue;

                record.usedItems.Add(itemId);
                break;
            }
        }
    }

    /// <summary>
    /// 대화를 시작한 NPC를 목표에 반영합니다.
    /// </summary>
    public static void ReportTalk(string npcId)
    {
        if (string.IsNullOrEmpty(npcId)) return;

        ForEachCurrent(EQuestObjectiveKind.Talk, (record, objective) => objective.npcId == npcId);
    }

    /// <summary>
    /// 카메라 회전과 확대 또는 축소를 모두 조작하면 목표를 달성합니다.
    /// </summary>
    public static void ReportCamera(bool rotated, bool zoomed)
    {
        if (!_isReady || (!rotated && !zoomed)) return;

        foreach (CRecord record in _ACTIVE)
        {
            if (record.state != EQuestState.Active) continue;
            CQuestObjective objective = record.quest.GetObjective(record.index);
            if (objective == null || objective.kind != EQuestObjectiveKind.Camera) continue;

            record.cameraRotated |= rotated;
            record.cameraZoomed |= zoomed;
            if (record.cameraRotated && record.cameraZoomed) AddProgress(record, objective);
        }
    }

    /// <summary>
    /// 카메라 회전과 확대 또는 축소 조작 여부를 반환합니다.
    /// </summary>
    public static void GetCameraProgress(CQuestSO quest, out bool rotated, out bool zoomed)
    {
        rotated = false;
        zoomed = false;
        if (!TryGetRecord(quest, out CRecord record)) return;

        rotated = record.cameraRotated;
        zoomed = record.cameraZoomed;
    }

    /// <summary>
    /// 피니쉬 중첩과 수동 발동 여부를 확인합니다.
    /// </summary>
    public static void ReportLeviathanFinisher(int stack, int maxStack, bool isManual)
    {
        ForEachCurrent(EQuestObjectiveKind.LeviathanFinisher, (record, objective) =>
            !objective.requireFullStack || (isManual && stack >= maxStack));
    }

    /// <summary>
    /// 프라도나 권능으로 회복한 결과를 목표에 반영합니다.
    /// </summary>
    public static void ReportPradonaPower()
    {
        ForEachCurrent(EQuestObjectiveKind.PradonaPower, (record, objective) => true);
    }

    private static bool TryGetRecord(CQuestSO quest, out CRecord record)
    {
        record = null;
        return quest != null && !string.IsNullOrEmpty(quest.QuestId) &&
            _RECORDS.TryGetValue(quest.QuestId, out record) && record.quest == quest;
    }

    private static void ForEachCurrent(EQuestObjectiveKind kind, Func<CRecord, CQuestObjective, bool> isMatch)
    {
        if (!_isReady) return;

        for (int i = _ACTIVE.Count - 1; i >= 0; i--)
        {
            if (i >= _ACTIVE.Count) continue;

            CRecord record = _ACTIVE[i];
            if (record.state != EQuestState.Active) continue;

            CQuestObjective objective = record.quest.GetObjective(record.index);
            if (objective == null || objective.kind != kind || !isMatch(record, objective)) continue;

            AddProgress(record, objective);
        }
    }

    private static void AddProgress(CRecord record, CQuestObjective objective)
    {
        record.count++;
        _version++;
        if (record.count >= objective.RequiredCount) Advance(record);
    }

    private static void Advance(CRecord record)
    {
        record.index++;
        record.count = 0;
        record.suppliedCount = 0;
        record.nextSupplyTime = 0f;
        record.cameraRotated = false;
        record.cameraZoomed = false;

        if (record.index >= record.quest.Objectives.Count)
        {
            record.index = record.quest.Objectives.Count;
            record.state = EQuestState.Reportable;
        }

        _version++;
    }

    internal static bool TryDiscardSupply(CQuestSO quest, int itemId, out bool repeated)
    {
        repeated = false;
        if (!TryGetRecord(quest, out CRecord record) || !record.suppliedItems.Contains(itemId)) return false;
        if (record.state != EQuestState.Active && record.state != EQuestState.Reportable) return false;

        foreach (CQuestObjective objective in quest.Objectives)
        {
            if (objective == null || objective.replacementItem == null || objective.supplyItem != objective.item ||
                !objective.MatchesItem(itemId)) continue;

            repeated = record.warnedForDiscard;
            record.warnedForDiscard = true;
            record.pendingReplacementId = repeated ? 0 : objective.replacementItem.Id;
            return true;
        }

        return false;
    }

    internal static CItemSO GetPendingReplacement(CQuestSO quest)
    {
        if (!TryGetRecord(quest, out CRecord record) || record.pendingReplacementId == 0) return null;

        foreach (CQuestObjective objective in quest.Objectives)
        {
            CItemSO replacement = objective?.replacementItem;
            if (replacement != null && replacement.Id == record.pendingReplacementId) return replacement;
        }

        return null;
    }

    internal static bool TryGiveReplacement(CQuestSO quest, CInventory inventory)
    {
        if (inventory == null) return false;
        CItemSO item = GetPendingReplacement(quest);
        if (item == null) return false;

        CRecord record = _RECORDS[quest.QuestId];
        record.pendingReplacementId = 0;
        bool added = record.suppliedItems.Add(item.Id);
        if (inventory.AddItem(item.Id, 1) > 0) return true;

        if (added) record.suppliedItems.Remove(item.Id);
        record.pendingReplacementId = item.Id;
        SupplyFailed?.Invoke(quest, item);
        return false;
    }

    private static bool IsReplacementPending(CRecord record, CQuestObjective objective)
    {
        return objective.replacementItem != null && objective.replacementItem.Id == record.pendingReplacementId;
    }

    internal static void ReplaceMissingSupply(CQuestSO quest, CPlayer player)
    {
        if (player == null || player.Inventory == null || !TryGetRecord(quest, out CRecord record) ||
            record.state != EQuestState.Active) return;

        CQuestObjective objective = quest.GetObjective(record.index);
        if (objective == null || objective.item == null || objective.supplyItem != objective.item ||
            record.suppliedCount < objective.supplyCount || IsReplacementPending(record, objective)) return;
        if (objective.kind != EQuestObjectiveKind.UseItem && objective.kind != EQuestObjectiveKind.Equip) return;
        if (player.Inventory.GetTotalQuantity(objective.item.Id) > 0 || IsEquipped(player, objective)) return;
        if (objective.replacementItem != null &&
            player.Inventory.GetTotalQuantity(objective.replacementItem.Id) > 0) return;

        if (player.Inventory.AddItem(objective.item.Id, 1) == 0) SupplyFailed?.Invoke(quest, objective.item);
    }

    private static void TrySupply(CRecord record, CQuestObjective objective, CPlayer player)
    {
        if (objective.supplyItem == null || player.Inventory == null || IsReplacementPending(record, objective)) return;
        if (record.warnedForDiscard && player.HpCurrent <= 0f) return;
        int remaining = objective.supplyCount - record.suppliedCount;
        if (remaining <= 0 || Time.unscaledTime < record.nextSupplyTime) return;

        int added = player.Inventory.AddItem(objective.supplyItem.Id, remaining);
        if (added > 0)
        {
            record.suppliedCount += added;
            if (objective.replacementItem != null) record.suppliedItems.Add(objective.supplyItem.Id);
            _version++;
        }

        if (added == remaining) return;
        record.nextSupplyTime = Time.unscaledTime + _SUPPLY_RETRY_SECONDS;
        SupplyFailed?.Invoke(record.quest, objective.supplyItem);
    }

    private static bool IsTarget(CQuestObjective objective, CEnemy enemy)
    {
        if (enemy == null) return false;
        if (objective.IsBossTarget) return enemy.BossId == objective.boss;
        if (enemy.BossId != EBossClear.None) return false;

        return objective.enemy == null || enemy.SourcePrefab == objective.enemy;
    }

    internal static bool IsEquipped(CPlayer player, CQuestObjective objective)
    {
        if (player.Equipment == null) return false;

        foreach (CItemEquipment equipment in player.Equipment.Values)
        {
            if (equipment != null && objective.MatchesItem(equipment.Id)) return true;
        }

        return false;
    }

    /// <summary>
    /// 현재 맵에서 목표가 가리키는 좌표와 NPC를 반환합니다.
    /// </summary>
    public static bool TryGetDestination(CQuestObjective objective, out Vector3 point, out CTalkNpc npc)
    {
        point = Vector3.zero;
        npc = null;
        if (objective == null || !objective.useDestination || CMapInfo.Current == null ||
            CMapInfo.Current.SceneId != objective.destinationScene) return false;

        if (string.IsNullOrEmpty(objective.destinationNpcId))
        {
            point = objective.destinationPoint;
            return true;
        }

        npc = FindNpc(objective.destinationNpcId);
        if (npc == null) return false;
        point = npc.FeetPosition;
        return true;
    }

    /// <summary>
    /// 현재 맵에서 지정한 NPC를 찾습니다.
    /// </summary>
    public static CTalkNpc FindNpc(string npcId)
    {
        if (string.IsNullOrEmpty(npcId)) return null;
        foreach (CTalkNpc npc in CTalkNpc.All)
        {
            if (npc.NpcId == npcId) return npc;
        }

        return null;
    }

    private static bool IsArrived(CQuestObjective objective, Vector3 position)
    {
        if (!TryGetDestination(objective, out Vector3 point, out _)) return false;
        Vector3 offset = position - point;
        offset.y = 0f;
        return offset.sqrMagnitude <= objective.arriveRadius * objective.arriveRadius;
    }

    private static void RebuildActive()
    {
        foreach (CRecord record in _RECORDS.Values)
        {
            if (record.state == EQuestState.Active || record.state == EQuestState.Reportable) _ACTIVE.Add(record);
        }
    }

    private static void Build()
    {
        if (_isBuilt) return;

        _isBuilt = true;
        _QUESTS.Clear();

        foreach (CQuestSO quest in Resources.LoadAll<CQuestSO>(CQuestSO.RESOURCES_FOLDER))
        {
            if (quest == null || string.IsNullOrEmpty(quest.QuestId)) continue;
            if (_QUESTS.ContainsKey(quest.QuestId))
            {
                CLogger.Error($"CQuestManager : 퀘스트 식별값이 겹칩니다 - {quest.name}");
                continue;
            }

            _QUESTS.Add(quest.QuestId, quest);
        }
    }
}
