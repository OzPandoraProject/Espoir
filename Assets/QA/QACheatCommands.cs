using IngameDebugConsole;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

public class QACheatCommands : MonoBehaviour
{
    private static QACheatCommands _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        // 실행 명령어
        DebugLogConsole.AddCommand("god", "플레이어를 무적 상태로 만듭니다.", GodMode);
        DebugLogConsole.AddCommand("killall", "화면의 모든 적을 처치합니다.", KillAllEnemies);
        DebugLogConsole.AddCommand("heal", "플레이어의 체력을 즉시 100% 회복합니다.", Cheat_HealPlayer);
        DebugLogConsole.AddCommand("die", "플레이어를 즉시 사망 상태로 만듭니다.", Cheat_KillPlayer);

        DebugLogConsole.AddCommand<int>("gold", "입력한 만큼 골드를 추가합니다. (예: gold 5000)", AddGold);
        DebugLogConsole.AddCommand<float>("speed", "게임 배속을 조절합니다. (예: speed 2.0 / 기본값 1.0)", SetGameSpeed);

        DebugLogConsole.AddCommand("cool", "현재 스킬 쿨타임을 없앱니다.", Cheat_ResetCooldown);
        DebugLogConsole.AddCommand("mp", "플레이어 MP를 최대치로 채웁니다.", Cheat_FillMp);

        DebugLogConsole.AddCommand<int, int>("status", "상태이상을 부여합니다. (예: status 10 50)", Cheat_AddStatus);
        DebugLogConsole.AddCommand<int, int>("estatus", "적에게 상태이상을 부여합니다. (예: status 11 5)", Cheat_AddEnemyStatus);

        DebugLogConsole.AddCommand<int>("clear", "플레이어 상태이상을 제거합니다. (예: clear 10)", Cheat_ClearStatus);

        DebugLogConsole.AddCommand<int>("boss", "보스 최대 체력의 입력한 %만큼 깎습니다. (예: boss 10)", Cheat_DamageBossByPercent);

        DebugLogConsole.AddCommand("tutorial", "튜토리얼 퀘스트를 완료 처리합니다.", Cheat_SkipTutorial);
    }

    private InputActionMap _playerMap;
    private void Update()
    {
        // 쿨 초기화 ON / OFF
        if (_noCooldown)
        {
            CPlayer player = FindObjectOfType<CPlayer>();

            if (player != null)
            {
                CPlayerSkill skill = player.GetComponent<CPlayerSkill>();

                if (skill != null)
                {
                    skill.ResetCooldowns();
                }

                for (int i = 0; i < 40; i++)
                {
                    player.RecoverBasicAttackMp();
                }
            }
        }

        // 치트중 스킬 이동 키 입력 막기
        if (_playerMap == null)
        {
            InputActionAsset[] assets = Resources.FindObjectsOfTypeAll<InputActionAsset>();
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] != null && assets[i].name == "PlayerInput")
                {
                    _playerMap = assets[i].FindActionMap("Player");
                    break;
                }
            }
        }

        if (_playerMap == null || DebugLogManager.Instance == null)
        {
            return;
        }

        if (DebugLogManager.Instance.IsLogWindowVisible)
        {
            if (_playerMap.enabled)
            {
                _playerMap.Disable();
            }
        }

        else if (!_playerMap.enabled)
        {
            _playerMap.Enable();
        }
    }

    // P,I 키입력 막기
    private bool _inventoryWasOpen;
    private void LateUpdate()
    {
        bool cheatOpen = DebugLogManager.Instance != null && DebugLogManager.Instance.IsLogWindowVisible;
        GameObject panel = FindInventoryPanel();

        if (cheatOpen && Input.GetKeyDown(KeyCode.P))
        {
            CGameManager game = CGameManager.Instance;

            if (game != null)
            {
                if (game.GameState == EGameState.ForcePause)
                {
                    game.SetGameState(EGameState.Idle);
                }

                else if (game.GameState == EGameState.Idle)
                {
                    game.SetGameState(EGameState.ForcePause);
                }
            }
        }

        if (cheatOpen && Input.GetKeyUp(KeyCode.I) && panel != null)
        {
            panel.SetActive(_inventoryWasOpen);
        }

        if (panel != null)
        {
            _inventoryWasOpen = panel.activeSelf;
        }
    }

    private GameObject FindInventoryPanel()
    {
        CInventoryUI inventoryUi = FindObjectOfType<CInventoryUI>();

        if (inventoryUi == null)
        {
            return null;
        }

        FieldInfo panelField = typeof(CInventoryUI).GetField("_InventoryPanel", BindingFlags.NonPublic | BindingFlags.Instance);
        return panelField.GetValue(inventoryUi) as GameObject;
    }

    /// <summary>
    /// 무적 치트
    /// </summary>
    private bool _isGodMode;
    public void GodMode()
    {
        CPlayer player = FindPlayer();

        if (player == null)
        {
            return;
        }
        _isGodMode = !_isGodMode;
        player.SetInvincible(_isGodMode);
        CLogger.Log($"[QA] 무적 모드 {_isGodMode}");
    }

    /// <summary>
    /// 적 처치 치트
    /// </summary>
    public void KillAllEnemies()
    {
        CEnemy[] enemies = FindObjectsOfType<CEnemy>();
        int killedCount = 0;

        for (int i = 0; i < enemies.Length; i++)
        {
            CEnemy enemy = enemies[i];

            if (enemy.HpCurrent <= 0f)
            {
                continue;
            }

            enemy.GetDamage(enemy.HpCurrent);
            enemy.CallDie();
            killedCount++;
        }

        CLogger.Log($"[QA] 적 {killedCount}명을 처치했습니다.");
    }

    /// <summary>
    /// 보스 최대체력 비율 피해 치트
    /// </summary>
    public void Cheat_DamageBossByPercent(int percent)
    {
        if (percent <= 0)
        {
            CLogger.Log("[QA] 1 이상의 퍼센트를 입력하세요. (예: boss 10)");
            return;
        }

        CEnemy[] enemies = FindObjectsOfType<CEnemy>();
        float ratio = Mathf.Clamp(percent, 1, 100) / 100f;
        int count = 0;

        for (int i = 0; i < enemies.Length; i++)
        {
            CEnemy boss = enemies[i];
            if (boss is not IBoss || boss.HpCurrent <= 0f)
            {
                continue;
            }

            boss.GetDamage(ratio, ERatioSwitch.ByMax);
            count++;
            CLogger.Log($"[QA] {boss.name} 체력 {percent}% 감소. 현재 {boss.HpCurrent} / {boss.HpMax}");
        }

        if (count == 0)
        {
            CLogger.Log("[QA] 씬에서 보스를 찾지 못했습니다.");
        }
    }

    /// <summary>
    /// 골드 추가 치트
    /// </summary>
    public void AddGold(int amount)
    {
        CPlayer player = FindPlayer();
        if (player == null)
        {
            return;
        }

        if (player.Inventory == null)
        {
            CLogger.Log("[QA] 플레이어 인벤토리를 찾지 못했습니다.");
            return;
        }

        if (player.Inventory.TryAddGold(amount) == false)
        {
            CLogger.Log("[QA] 골드를 추가하지 못했습니다. 1 이상의 값을 입력하세요.");
            return;
        }

        CLogger.Log($"[QA] 골드가 {amount}만큼 추가되었습니다. 현재 골드: {player.Inventory.Gold}");
    }

    /// <summary>
    /// 플레이어 체력 회복 치트 기능
    /// </summary>
    public void Cheat_HealPlayer()
    {
        CPlayer player = FindPlayer();

        if (player == null)
        {
            return;
        }

        player.GetHeal(player.HpMax);
        CLogger.Log($"[QA] 플레이어 체력을 회복했습니다. 현재 체력: {player.HpCurrent}");
    }

    /// <summary>
    /// 플레이어 강제 사망 치트 기능
    /// </summary>
    public void Cheat_KillPlayer()
    {
        CPlayer player = FindPlayer();

        if (player == null)
        {
            return;
        }

        player.GetDamage(player.HpCurrent);
        CLogger.Log($"[QA] 플레이어를 사망 처리했습니다. 현재 체력: {player.HpCurrent}");
    }

    /// <summary>
    /// 게임 배속 제어 치트
    /// </summary>
    public void SetGameSpeed(float speedAmount)
    {
        Time.timeScale = speedAmount;
        CLogger.Log($"[QA] 게임 속도가 {speedAmount}배속으로 변경되었습니다.");
    }

    /// <summary>
    /// 스킬 쿨타임제거 치트
    /// </summary>
    private bool _noCooldown;
    public void Cheat_ResetCooldown()
    {
        CPlayer player = FindPlayer();

        if (player == null)
        {
            return;
        }

        CPlayerSkill skill = player.GetComponent<CPlayerSkill>();

        if (skill == null)
        {
            CLogger.Log("[QA] 플레이어 스킬 컴포넌트를 찾지 못했습니다.");
            return;
        }

        _noCooldown = !_noCooldown;
        if (_noCooldown)
        {
            skill.ResetCooldowns();

            for (int i = 0; i < 40; i++)
            {
                player.RecoverBasicAttackMp();
            }
        }
                
        CLogger.Log($"[QA] 쿨타임 제거: {_noCooldown}");
    }

    /// <summary>
    /// 마나채우는 치트
    /// </summary>
    public void Cheat_FillMp()
    {
        CPlayer player = FindPlayer();

        if (player == null)
        {
            return;
        }

        for (int i = 0; i < 40; i++)
        {
            player.RecoverBasicAttackMp();
        }

        CLogger.Log($"[QA] 플레이어 MP를 채웠습니다. 현재 MP: {player.CurrentMp}");
    }

    /// <summary>
    /// 플레이어 상태이상 치트
    /// </summary>
    public void Cheat_AddStatus(int id, int stack)
    {
        CPlayer player = FindPlayer();

        if (player == null)
        {
            return;
        }

        player.AddStatus(id, stack: stack);
        CLogger.Log($"[QA] 플레이어에게 상태이상 {id}를 {stack}스택 부여했습니다. 현재스택: {player.FindStatusStack(id)}");
    }

    /// <summary>
    /// 플레이어 상태이상 제거 치트
    /// </summary>
    public void Cheat_ClearStatus(int id)
    {
        CPlayer player = FindPlayer();

        if (player == null)
        {
            return;
        }

        player.RemoveStatus(id);
        CLogger.Log($"[QA] 플레이어의 상태이상{id}를 제거했습니다.");
    }

    /// <summary>
    /// 적에게 상태이상 치트
    /// </summary>
    public void Cheat_AddEnemyStatus(int id, int stack)
    {
        CEnemy[] enemies = FindObjectsOfType<CEnemy>();
        int count = 0;

        for (int i = 0; i < enemies.Length; i++)
        {
            CEnemy enemy = enemies[i];

            if (enemy.HpCurrent <= 0f)
            {
                continue;
            }

            enemy.AddStatus(id, stack: stack);
            count++;
        }

        CLogger.Log($"[QA] 적 {count}명에게 상태이상 {id}를 {stack}스택 부여합니다.");
    }

    /// <summary>
    /// 튜토리얼 스킵 치트
    /// </summary>
    private void Cheat_SkipTutorial()
    {
        const string questId = "tutorial_leaving_day";

        if (!CQuestManager.IsReady)
        {
            CLogger.Log("[QA] 퀘스트가 아직 준비되지 않았습니다.");
            return;
        }

        BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        System.Type manager = typeof(CQuestManager);
        var quests = (System.Collections.IDictionary)manager.GetField("_QUESTS", flags).GetValue(null);

        if (!quests.Contains(questId))
        {
            CLogger.Log("[QA] 튜토리얼 퀘스트를 찾지 못했습니다.");
            return;
        }

        CQuestSO quest = (CQuestSO)quests[questId];
        var records = (System.Collections.IDictionary)manager.GetField("_RECORDS", flags).GetValue(null);
        var active = (System.Collections.IList)manager.GetField("_ACTIVE", flags).GetValue(null);
        System.Type recordType = manager.GetNestedType("CRecord", BindingFlags.NonPublic);

        object record = records.Contains(questId) ? records[questId] : System.Activator.CreateInstance(recordType);
        recordType.GetField("quest").SetValue(record, quest);
        recordType.GetField("state").SetValue(record, EQuestState.Completed);
        recordType.GetField("index").SetValue(record, quest.Objectives.Count);
        records[questId] = record;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (recordType.GetField("quest").GetValue(active[i]) == quest)
            {
                active.RemoveAt(i);
            }
        }

        FieldInfo version = manager.GetField("_version", flags);
        version.SetValue(null, (int)version.GetValue(null) + 1);

        CTalkPresenter talk = FindObjectOfType<CTalkPresenter>();
        if (talk != null && talk.IsOpen)
        {
            talk.Close();
        }
        CLogger.Log("[QA] 튜토리얼을 건너뛰었습니다.");
    }

    private CPlayer FindPlayer()
    {
        CPlayer player = FindObjectOfType<CPlayer>();

        if (player == null)
        {
            CLogger.Log("[QA] 플레이어를 찾지 못했습니다.");
        }

        return player;
    }

}
