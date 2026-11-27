using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public enum EGameState
{
    Idle = 0,
    Story = 1,
    UI = 2,
    Setting = 3,
    Cinematic = 4,
    ForcePause = 9,
    Gameover = 10,
}

public enum ESettingState
{
    None = 0,
    BaseMenu = 1,
    //Save = 1,
    //Load = 2,
    //Sound = 3,
    Confirm = 9
}

public enum EBattleState
{
    None = 0,
    NormalBattle = 1,
    BossBattle = 2,
}

[Flags]
public enum EBossClear
{
    None = 0,
    [InspectorName("질투")]
    Invidia     = 1 << 0,
    [InspectorName("탐욕")]
    Avaritia    = 1 << 1,
    [InspectorName("나태")]
    Pigritia    = 1 << 2,
    [InspectorName("폭식")]
    Gula        = 1 << 3,
    [InspectorName("색욕")]
    Luxuria     = 1 << 4,
    [InspectorName("분노")]
    Ira         = 1 << 5,
    [InspectorName("오만")]
    Superbia    = 1 << 6,
    [InspectorName("프라도나")]
    Pradona     = 1 << 7,
    [InspectorName("보스들(미사용)")]
    AvailableBoss = Pigritia | Luxuria | Superbia | Pradona,
}

[Flags]
public enum EPradonaPower
{
    None        = 0,
    Invidia     = 1 << 0,
    Avaritia    = 1 << 1,
    Pigritia    = 1 << 2,
    Gula        = 1 << 3,
    Luxuria     = 1 << 4,
    Ira         = 1 << 5,
    Superbia    = 1 << 6,
}

public class CGameManager : MonoBehaviour
{
    [SerializeField] private AudioMixer _audioMixer;
    [Tooltip("반드시 작성!!!")]
    [SerializeField] private int _currentStage;

    [SerializeField] Button _gameShutdown;
    [SerializeField] Button _screenChange;
    [SerializeField] TMP_Text _screenChangeText;
    [SerializeField] Button _gameGotoTitle;
    [SerializeField] Button _gameSave;
    [SerializeField] Toggle _gameAutosave;

    [SerializeField] CanvasGroup _menuGameover;
    [SerializeField] Button _gameGameoverTitle;

    [SerializeField] GameObject _forcePauseSet;
    [SerializeField] CanvasGroup _menuBackground;
    [SerializeField] CanvasGroup _menuBase;

    [SerializeField] List<Button> _quilitySetting;
    [SerializeField] Slider _bgmSlider;
    [SerializeField] Slider _seSlider;
    [SerializeField] TMP_InputField _bgmInput;
    [SerializeField] TMP_InputField _seInput;

    [SerializeField] CanvasGroup _menuConfirm;
    [SerializeField] TMP_Text _confirmTitle;
    [SerializeField] TMP_Text _confirmDescription;
    [SerializeField] Button _confirmYes;
    [SerializeField] Button _confirmNo;

    [SerializeField] CanvasGroup _popupScreen;
    [SerializeField] TMP_Text _popupTitle;
    [SerializeField] TMP_Text _popupDescription;

    [SerializeField] CanvasGroup _saveScreen;

    [SerializeField] GameObject _errorPrefab;

    [Header("디버그용")]
    [SerializeField] TMP_Text _zoneText;

    private const string BgmVolumeKey = "Espoir.Settings.BgmVolume";
    private const string SeVolumeKey = "Espoir.Settings.SeVolume";
    private const string AutosaveKey = "Espoir.Settings.Autosave";

    private EGameState _gameState;
    private EBattleState _battleState;
    private ESettingState _settingState;
    private ESettingState _lastSettingState;
    private EBossClear _bossClearState;
    private bool _isInLobby = false;
    private bool _isPaused = false;
    private float _timeScaleBeforePause = 1f;
    private bool _isFullscreen = false;
    private int _tempWidth;
    private int _tempHeight;

    private CSoundManager _soundManager;
    private CPlayer _player;
    private CInventoryUI _inventoryUI;
    private CShopUI _shopUI;
    private CCharacterInfoUI _characterInfoUI;
    private Action _confirmAction;
    private Dictionary<EBossClear, float> _bossTime;
    
    // 보스 타이머용
    private EBossClear _attempBoss = EBossClear.None;
    private static EBossClear _bossFoundState;
    // 씬 이동 중에도 플레이시간을 유지하며 새 게임과 불러오기에서 초기화한다.
    private static float _playTime;
    private float _attempStartTime;

    private int _currentActiveSpawner = 0;

    // 팝업용
    private Coroutine _popupCoroutine;
    private Coroutine _saveCoroutine;

    public EGameState GameState => _gameState;
    public EBattleState BattleState => _battleState;
    public EBossClear BossClearState => _bossClearState;
    public bool IsInLobby => _isInLobby;
    public bool IsPaused => _isPaused;

    private static CGameManager _instance;
    public static CGameManager Instance => _instance;
    public float BgmLevel => _bgmSlider.value;
    public float SeLevel => _seSlider.value;
    public Dictionary<EBossClear, float> BossTime => _bossTime;
    public EBossClear BossFoundState => _bossFoundState;
    public float PlayTime => _playTime;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        _instance = this;

        _bossTime = new Dictionary<EBossClear, float>();
        _bossTime.Add(EBossClear.Pigritia, 0f);
        _bossTime.Add(EBossClear.Luxuria, 0f);
        _bossTime.Add(EBossClear.Superbia, 0f);
        _bossTime.Add(EBossClear.Pradona, 0f);
    }

    void Start()
    {
        _gameShutdown.onClick.AddListener(() =>
            ShowQuestion(
                "게임 종료",
                "게임을 종료합니까?",
                () =>
                {
                    _soundManager.PlaySE(EEffectSound.Success);
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                }));
        _gameGotoTitle.onClick.AddListener(() =>
            ShowQuestion(
                "타이틀로",
                "타이틀로 돌아갑니까?",
                () =>
                {
                    _soundManager.PlaySE(EEffectSound.Success);
                    CSceneFlowManager.Instance.LoadScene(ESceneId.Title);
                }));
        _gameSave.onClick.AddListener(() =>
            ShowQuestion(
                "저장",
                "게임을 저장합니까?",
                () =>
                {
                    SaveData();
                }));
        _confirmYes.onClick.AddListener(() =>
            {
                _confirmAction();
                HideQuestion();
            });
        _confirmNo.onClick.AddListener(() =>
            {
                _soundManager.PlaySE(EEffectSound.Beep);
                HideQuestion();
            });
        _gameGameoverTitle.onClick.AddListener(() =>
            CSceneFlowManager.Instance?.LoadScene(ESceneId.Title));
        _screenChange.onClick.AddListener(() => OnGameResolutionChange());
        _player = FindFirstObjectByType<CPlayer>();
        _soundManager = FindFirstObjectByType<CSoundManager>();
        _inventoryUI = FindFirstObjectByType<CInventoryUI>();
        _shopUI = FindFirstObjectByType<CShopUI>();
        _characterInfoUI = FindFirstObjectByType<CCharacterInfoUI>();
        _soundManager.PlayBGM(EBackgroundSound.BaseBGM);
        SetGameState(EGameState.Idle);
        SetBattleState(EBattleState.None);
        _settingState = ESettingState.None;
        _tempWidth = Screen.width;
        _tempHeight = Screen.height;

        if (CMapInfo.Current == null)
        {
            CLogger.Error("현재 맵 정보를 찾을 수 없어 저장하지 못했습니다.");
            return;
        }

        _currentStage = CMapInfo.Current.SceneId switch
        {
            ESceneId.PradonaCamp => 0,
            ESceneId.Desert => 1,
            ESceneId.Garden => 2,
            ESceneId.Tower => 3,
            _ => -1
        };

        if (_currentStage < 0)
        {
            CLogger.Error($"맵 정보가 잘못되어있습니다.");
            return;
        }

        for (int i = 0; i < _quilitySetting.Count; i++)
        {
            int target = i;
            _quilitySetting[i].onClick.AddListener(() => OnQualityChanged(target));
        }
        OnQualityChanged(_quilitySetting.Count - 1);
        _bgmSlider.onValueChanged.AddListener((float _) =>
        {
            SyncSoundBySlider(_bgmInput, _bgmSlider);
            SetVolume();
            StoreVolumeSettings();
        });
        _seSlider.onValueChanged.AddListener((float _) =>
        {
            SyncSoundBySlider(_seInput, _seSlider);
            SetVolume();
            StoreVolumeSettings();
        });
        _bgmInput.onValueChanged.AddListener((string _) =>
        {
            SyncSoundByText(_bgmInput, _bgmSlider);
            SetVolume();
            StoreVolumeSettings();
        });
        _seInput.onValueChanged.AddListener((string _) =>
        {
            SyncSoundByText(_seInput, _seSlider);
            SetVolume();
            StoreVolumeSettings();
        });

        _menuBackground.alpha = 0;
        _menuBackground.interactable = false;
        _menuBackground.blocksRaycasts = false;
        _menuBase.alpha = 0;
        _menuBase.interactable = false;
        _menuBase.blocksRaycasts = false;

        LoadVolumeSettings();

        _gameAutosave.SetIsOnWithoutNotify(PlayerPrefs.GetInt(AutosaveKey, 1) == 1);
        _gameAutosave.onValueChanged.AddListener(isOn =>
        {
            PlayerPrefs.SetInt(AutosaveKey, isOn ? 1 : 0);
            PlayerPrefs.Save();
        });

        if (!CQuestManager.IsReady &&
            (CSceneFlowManager.Instance == null || CSceneFlowManager.Instance.TargetSaveData < 0))
        {
            CQuestManager.Restore(null);
        }
    }

    void Update()
    {
        // 메뉴, 대화, 일시정지 시간은 플레이시간에 넣지 않는다.
        if (_gameState == EGameState.Idle) _playTime += Time.unscaledDeltaTime;
        CQuestManager.Tick(_player);

        if (Input.GetKeyDown(KeyCode.P))
        {
            if (_gameState == EGameState.Idle)
            {
                SetGameState(EGameState.ForcePause);
            }
            else if (_gameState == EGameState.ForcePause)
            {
                SetGameState(EGameState.Idle);
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_shopUI != null && _shopUI.IsOpen)
            {
                _shopUI.Close();
            }
            else if (_inventoryUI != null && _inventoryUI.IsOpen)
            {
                _inventoryUI.SetInventoryOpen(false);
            }
            else if (_characterInfoUI != null && _characterInfoUI.IsOpen)
            {
                _characterInfoUI.Close();
            }
            else
            {
                ToggleBaseMenu();
            }
        }

        if (_zoneText != null)
        {
            _zoneText.text = $"Game: {_gameState}\nBattle: {_battleState}\nLobby: {_isInLobby}";
        }
    }

    private void LoadVolumeSettings()
    {
        float bgm = PlayerPrefs.GetFloat(BgmVolumeKey, _bgmSlider.value);
        float se = PlayerPrefs.GetFloat(SeVolumeKey, _seSlider.value);

        _bgmSlider.SetValueWithoutNotify(Mathf.Clamp01(bgm));
        _seSlider.SetValueWithoutNotify(Mathf.Clamp01(se));

        SyncSoundBySlider(_bgmInput, _bgmSlider);
        SyncSoundBySlider(_seInput, _seSlider);
    }

    private void StoreVolumeSettings()
    {
        PlayerPrefs.SetFloat(BgmVolumeKey, _bgmSlider.value);
        PlayerPrefs.SetFloat(SeVolumeKey, _seSlider.value);
    }

    private void SetPaused(bool paused)
    {
        if (_isPaused == paused) return;
        _isPaused = paused;
        if (paused)
        {
            _timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0;
        }
        else
        {
            Time.timeScale = _timeScaleBeforePause;
        }
    }

    private void OnGameResolutionChange()
    {
        _isFullscreen = !_isFullscreen;
        if (_isFullscreen)
        {
            Screen.SetResolution(1920, 1080, FullScreenMode.FullScreenWindow);
            _screenChangeText.text = "창모드";
        }
        else
        {
            Screen.SetResolution(_tempWidth, _tempHeight, FullScreenMode.Windowed);
            _screenChangeText.text = "전체화면";
        }
    }

    public void SetGameState(EGameState gameState)
    {
        switch (_gameState)
        {
            case EGameState.Idle:
                break;
            case EGameState.Story:
                _soundManager.PlayBGM(EBackgroundSound.BaseBGM);
                break;
            case EGameState.UI:
                break;
            case EGameState.Setting:
                _menuBackground.alpha = 0;
                _menuBackground.interactable = false;
                _menuBackground.blocksRaycasts = false;
                SetPaused(false);
                PlayerPrefs.Save();
                break;
            case EGameState.ForcePause:
                _forcePauseSet.SetActive(false);
                SetPaused(false);
                break;
            default:
                break;
        }
        _gameState = gameState;
        switch (_gameState)
        {
            case EGameState.Idle:
                break;
            case EGameState.Story:
                break;
            case EGameState.UI:
                _soundManager.PlaySE(EEffectSound.QuestionAppear);
                break;
            case EGameState.Setting:
                _soundManager.PlaySE(EEffectSound.QuestionAppear);
                _menuBackground.alpha = 1;
                _menuBackground.interactable = true;
                _menuBackground.blocksRaycasts = true;
                SetPaused(true);
                break;
            case EGameState.ForcePause:
                _soundManager.PlaySE(EEffectSound.QuestionAppear);
                _forcePauseSet.SetActive(true);
                SetPaused(true);
                break;
            case EGameState.Gameover:
                CloseGameplayPanels();
                _soundManager.PlaySE(EEffectSound.Beep);
                EndBossTimer(_attempBoss, false);
                _menuBackground.alpha = 1;
                _menuBackground.interactable = true;
                _menuBackground.blocksRaycasts = true;
                _menuGameover.alpha = 1;
                _menuGameover.interactable = true;
                _menuGameover.blocksRaycasts = true;
                break;
        }
    }

    public void SetBattleState(EBattleState battleState)
    {
        switch (_battleState)
        {
            case EBattleState.None:
                _player.RemoveStatus(27);
                break;
            case EBattleState.NormalBattle:
                _player.InvokeOnBattleEnd(false);
                break;
            case EBattleState.BossBattle:
                _player.InvokeOnBattleEnd(true);
                break;
        }
        _battleState = battleState;
        switch (_battleState)
        {
            case EBattleState.None:
                _player.AddStatus(27);
                _soundManager.PlayBGM(EBackgroundSound.BaseBGM);
                break;
            case EBattleState.NormalBattle:
                CloseGameplayPanels();
                _player.InvokeOnBattleStart(false);
                _soundManager.PlayBGM(EBackgroundSound.BattleBGM);
                break;

            case EBattleState.BossBattle:
                CloseGameplayPanels();
                _player.InvokeOnBattleStart(true);
                _soundManager.PlayBGM(EBackgroundSound.BossBGM);
                break;
        }
    }

    /// <summary>
    /// ESC 와 같은 규칙으로 기본 메뉴를 열거나 닫습니다. HUD 메뉴 버튼도 이 메서드를 씁니다.
    /// </summary>
    public void ToggleBaseMenu()
    {
        if (_gameState == EGameState.Idle)
        {
            SetGameState(EGameState.Setting);
            SetMenuState(ESettingState.BaseMenu);
        }
        else if (_gameState == EGameState.Setting)
        {
            SetGameState(EGameState.Idle);
            SetMenuState(ESettingState.None);
        }
    }

    public void SetMenuState(ESettingState state)
    {
        switch (_settingState)
        {
            case ESettingState.None:
                break;
            case ESettingState.BaseMenu:
                _menuBase.alpha = 0;
                _menuBase.interactable = false;
                _menuBase.blocksRaycasts = false;
                break;
            case ESettingState.Confirm:
                _menuConfirm.alpha = 0;
                _menuConfirm.interactable = false;
                _menuConfirm.blocksRaycasts = false;
                break;
        }
        _settingState = state;
        switch (_settingState)
        {
            case ESettingState.None:
                break;
            case ESettingState.BaseMenu:
                _menuBase.alpha = 1;
                _menuBase.interactable = true;
                _menuBase.blocksRaycasts = true;
                break;
            case ESettingState.Confirm:
                _menuConfirm.alpha = 1;
                _menuConfirm.interactable = true;
                _menuConfirm.blocksRaycasts = true;
                break;
        }
    }

    public void OnQualityChanged(int index)
    {
        switch (index)
        {
            case 0:
                QualitySettings.SetQualityLevel(0, true);
                break;
            case 1:
                QualitySettings.SetQualityLevel(1, true);
                break;
            case 2:
                QualitySettings.SetQualityLevel(2, true);
                break;
            case 3:
                QualitySettings.SetQualityLevel(3, true);
                break;
            case 4:
                QualitySettings.SetQualityLevel(5, true);
                break;
            default:
                break;
        }

        RefreshQualityButtons(index);
    }

    public void SetLobbyIn(bool target)
    {
        if (target != _isInLobby)
        {
            _isInLobby = target;
            if (target)
            {
                _player.AddStatus(26, 0, 1);
            }
            else
            {
                _player.RemoveStatus(26, true);
            }
        }
    }

    private void CloseGameplayPanels()
    {
        if (_shopUI != null && _shopUI.IsOpen)
        {
            _shopUI.Close();
        }

        if (_inventoryUI != null && _inventoryUI.IsOpen)
        {
            _inventoryUI.SetInventoryOpen(false);
        }

        if (_characterInfoUI != null && _characterInfoUI.IsOpen)
        {
            _characterInfoUI.Close();
        }
    }

    private void RefreshQualityButtons(int index)
    {
        for (int i = 0; i < _quilitySetting.Count; i++)
        {
            _quilitySetting[i].interactable = !(i == index);
        }
    }

    private void SetVolume()
    {
        // SoundManager에서 처리함
    }

    private void SyncSoundByText(TMP_InputField input, Slider slider)
    {
        if (!int.TryParse(input.text, out int volume))
        {
            return;
        }

        volume = Mathf.Clamp(volume, 0, 100);
        slider.SetValueWithoutNotify(volume / 100f);
        input.SetTextWithoutNotify(volume.ToString());
    }
    private void SyncSoundBySlider(TMP_InputField input, Slider slider)
    {
        input.SetTextWithoutNotify(Mathf.RoundToInt(slider.value * 100f).ToString());
    }

    public void InvokeSpawnerActivation(bool isActive)
    {
        if (isActive)
        {
            if (_currentActiveSpawner == 0 && _battleState == EBattleState.None) SetBattleState(EBattleState.NormalBattle);
            _currentActiveSpawner += 1;
        }
        else
        {
            _currentActiveSpawner -= 1;
            if (_currentActiveSpawner == 0 && _battleState == EBattleState.NormalBattle) SetBattleState(EBattleState.None);
        }
    }

    public void InvokeBossBattleActivation(bool isActive)
    {
        if (isActive)
        {
            SetBattleState(EBattleState.BossBattle);
        }
        else
        {
            if (_currentActiveSpawner != 0) SetBattleState(EBattleState.NormalBattle);
            else SetBattleState(EBattleState.None);
        }
    }

    public void InvokeAutosave()
    {
        if (_gameAutosave.isOn)
        {
            SaveData(false);
        }
    }
    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public bool InitializeSaveslot(int slot, bool useSaveData)
    {
        if (_player == null ||
            _player.Inventory == null ||
            slot < 0 || slot >= CSaveManager.MaxSaveSlot) return false;

        CSaveManager.RefreshData(slot);
        if (useSaveData)
        {
            if (!CSaveManager.GetData(slot, out CSaveData data)) return false;
            LoadData(data);
            return true;
        }

        // 새 시작인데 데이터가 이미 있을경우
        if (CSaveManager.Available(slot)) return false;

        _playTime = 0f;
        _bossFoundState = EBossClear.None;
        CQuestManager.Restore(null);
        return SaveData(slot);
    }

    public bool SaveData(bool isManual = true)
    {
        bool result = SaveData(CSceneFlowManager.Instance.TargetSaveData);
        if (result)
        {
            if (isManual) _soundManager.PlaySE(EEffectSound.Success);
            StartCoroutine(ShowSaveNotify());
        }
        else
        {
            _soundManager.PlaySE(EEffectSound.Beep);
            ShowError("저장 실패. 다시 시도해주세요.", Color.red, true);
        }
        return result;
    }

    private bool SaveData(int slot)
    {
        if (_player == null || _player.Inventory == null) return false;

        List<CItem> items = new List<CItem>(_player.Inventory.MaxItemSlot);
        int[] quickSlots = new int[_player.Inventory.QuickSlotLength];

        _player.Inventory.ExportState(items, quickSlots, new Dictionary<(EConsumableCooldownGroup group, int itemId), float>(), out int gold);

        CSaveData data = new CSaveData
            (
                CSaveManager.Version,
                _currentStage,
                gold,
                items,
                _player.Equipment,
                quickSlots,
                _bossClearState,
                _player.PradonaPower,
                _bossTime,
                _playTime,
                _bossFoundState,
                CQuestManager.Export());

        return CSaveManager.SaveData(slot, data);
    }

    public void LoadData()
    {
        LoadData(CSceneFlowManager.Instance.TargetSaveData);
    }

    private void LoadData(int slot)
    {
        if (!CSaveManager.GetData(slot, out CSaveData data)) return;
        LoadData(data);
    }

    private void LoadData(CSaveData data)
    {
        _player.Inventory.ImportState(data.Inventory, data.Quickslot.ToArray(), new Dictionary<(EConsumableCooldownGroup group, int itemId), float>(), data.Money);
        _bossClearState = data.BossClear;
        _player.PradonaPower = data.PradonaPower;
        _bossTime = data.BossTime;
        _playTime = data.PlayTime;
        _bossFoundState = data.BossFound;
        CQuestManager.Restore(data.Quests);
        foreach (KeyValuePair<EEquipmentSlot, CItemEquipment> pair in data.Equipment)
        {
            if (_player.Equipment.TryGetValue(pair.Key, out CItemEquipment previous) &&
                previous != null)
            {
                previous.OnUnequip(_player);
            }

            _player.Equipment[pair.Key] = null;
            pair.Value?.OnEquip(_player);
        }
        _player.EquipmentResync();
    }

    private void ShowQuestion(string shownTitle, string shownText, Action action)
    {
        _soundManager?.PlaySE(EEffectSound.QuestionAppear);
        _confirmTitle.text = shownTitle;
        _confirmDescription.text = shownText;
        _confirmAction = action;
        _lastSettingState = _settingState;
        SetMenuState(ESettingState.Confirm);
    }

    private void HideQuestion()
    {
        _confirmTitle.text = "";
        _confirmDescription.text = "";
        _confirmAction = null;
        SetMenuState(_lastSettingState);
        _lastSettingState = ESettingState.None;
    }

    public int GetAvailableStage()
    {
        // int count = 0;
        // if (_bossClearState.HasFlag(EBossClear.Invidia)) count += 1;
        int count = 1;
        if (_bossClearState.HasFlag(EBossClear.Pigritia)) count += 1;
        if (_bossClearState.HasFlag(EBossClear.Luxuria)) count += 1;
        return count;
    }

    public int GetCompleteNumber()
    {
        int count = 0;
        if (_bossClearState.HasFlag(EBossClear.Invidia)) count += 1;
        if (_bossClearState.HasFlag(EBossClear.Avaritia)) count += 1;
        if (_bossClearState.HasFlag(EBossClear.Pigritia)) count += 1;
        if (_bossClearState.HasFlag(EBossClear.Gula)) count += 1;
        if (_bossClearState.HasFlag(EBossClear.Luxuria)) count += 1;
        if (_bossClearState.HasFlag(EBossClear.Ira)) count += 1;
        if (_bossClearState.HasFlag(EBossClear.Superbia)) count += 1;
        if (_bossClearState.HasFlag(EBossClear.Pradona)) count += 1;
        return count;
    }

    public static int GetCompleteNumber(EBossClear target)
    {
        int count = 0;
        if (target.HasFlag(EBossClear.Invidia)) count += 1;
        if (target.HasFlag(EBossClear.Avaritia)) count += 1;
        if (target.HasFlag(EBossClear.Pigritia)) count += 1;
        if (target.HasFlag(EBossClear.Gula)) count += 1;
        if (target.HasFlag(EBossClear.Luxuria)) count += 1;
        if (target.HasFlag(EBossClear.Ira)) count += 1;
        if (target.HasFlag(EBossClear.Superbia)) count += 1;
        if (target.HasFlag(EBossClear.Pradona)) count += 1;
        return count;
    }

    public int GetMaxCompleteNumber()
    {
        return 8;
    }

    public static int GetMaxCompleteNumberStatic()
    {
        return System.Enum.GetValues(typeof(EBossClear)).Length - 1;
    }

    /// <summary>
    /// 보스를 클리어했을 때 사용하는 함수입니다.
    /// </summary>
    /// <param name="target"></param>
    public void SetBossCleared(EBossClear target)
    {
        if (target == EBossClear.None || (target & (target - 1)) != 0)
        {
            CLogger.Error("[CGameManager] SetBossCleared의 target이 하나가 아님!");
            return;
        }
        _bossClearState |= target;
    }

    /// <summary>
    /// 보스를 클리어했을 때 사용하는 함수입니다. time은 0 초과 시간입니다.
    /// </summary>
    /// <param name="target"></param>
    public void SetBossCleared(EBossClear target, float time)
    {
        if (target == EBossClear.None || (target & (target - 1)) != 0)
        {
            CLogger.Error("[CGameManager] SetBossCleared의 target이 하나가 아님!");
            return;
        }
        if ((target & EBossClear.AvailableBoss) == 0)
        {
            CLogger.Error($"[CGameManager] GetBossClearTime의 target은 토벌할 수 없음 : {target}");
            return;
        }
        if (time <= 0)
        {
            CLogger.Error("[CGameManager] SetBossCleared의 시간이 0 이하임");
            return;
        }
        _bossClearState |= target;
        bool newRecord = !_bossTime.TryGetValue(target, out float best) || best <= 0f || time < best;
        if (newRecord)
        {
            _bossTime[target] = time;
        }
        else
        {
            CLogger.Log($"[CGameManager] {target} 처치 기록 갱신실패 -> 데이터 저장되지 않음.");
        }

        if (target != EBossClear.Pigritia && target != EBossClear.Luxuria) return;

        string title = target == EBossClear.Luxuria ? "가모리 처치" : "벨페고르 처치";
        string description = "시간 : " + FormatTimer(time);
        if (newRecord) description += "\n최고기록 갱신!";
        StartCoroutine(ShowPopup(title, description));
    }

    public bool TryGetBossClearTime(EBossClear target, out float time)
    {
        time = 0f;
        if (target == EBossClear.None || (target & (target - 1)) != 0)
        {
            CLogger.Error("[CGameManager] GetBossClearTime의 target이 하나가 아님!");
            return false;
        }
        if ((target & EBossClear.AvailableBoss) == 0)
        {
            CLogger.Error($"[CGameManager] GetBossClearTime의 target은 토벌할 수 없음 : {target}");
            return false;
        }
        time = _bossTime[target];
        return true;
    }

    /// <summary>
    /// 맵 이동 전에 보관한 보스 처치 여부와 최단 기록을 복원합니다.
    /// </summary>
    public void RestoreBossRecords(EBossClear cleared, IReadOnlyDictionary<EBossClear, float> times)
    {
        _bossClearState = cleared;
        _bossTime.Clear();
        foreach (KeyValuePair<EBossClear, float> pair in times)
        {
            _bossTime.Add(pair.Key, pair.Value);
        }
    }

    public void BeginBossTimer(EBossClear boss)
    {
        if (boss == EBossClear.None || (boss & (boss - 1)) != 0 ||
            _gameState == EGameState.Gameover || (boss & EBossClear.AvailableBoss) == 0) return;
        _attempBoss = boss;
        _attempStartTime = Time.time;
        _bossFoundState |= boss;
    }

    public bool TryGetBossTimer(EBossClear boss, out float currentTimer, out bool timeOvered)
    {
        currentTimer = 0f;
        timeOvered = false;
        if (boss == EBossClear.None || (boss & (boss - 1)) != 0 ||
            _attempBoss != boss || _gameState == EGameState.Gameover) return false;
        
        currentTimer = Time.time - _attempStartTime;
        timeOvered = (_bossTime[boss] > 0) && (currentTimer > _bossTime[boss]);

        return true;
    }

    public void EndBossTimer(EBossClear boss, bool cleared)
    {
        if (boss == EBossClear.None || (boss & (boss - 1)) != 0 || _attempBoss != boss) return;
        if (cleared && TryGetBossTimer(boss, out float currentTimer, out _))
        {
            if (currentTimer > 0f) SetBossCleared(boss, currentTimer);
            else SetBossCleared(boss);
        }

        _attempBoss = EBossClear.None;
    }

    public IEnumerator ShowPopup(string title, string description)
    {
        if (_popupScreen == null || _popupTitle == null || _popupDescription == null)
        {
            yield break;
        }

        if (_popupCoroutine != null)
        {
            StopCoroutine(_popupCoroutine);
            _popupCoroutine = null;
        }

        _popupCoroutine = StartCoroutine(ShowPopupInternal(title, description));
        yield return _popupCoroutine;
        _popupCoroutine = null;
    }

    private IEnumerator ShowPopupInternal(string title, string description)
    {
        _popupScreen.alpha = 0f;
        _popupTitle.text = title;
        _popupDescription.text = description;

        float t = 0f;
        while (t < 0.5f)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            float lerp = Mathf.Clamp01(t / 0.5f);
            _popupScreen.alpha = Mathf.Lerp(0f, 1f, lerp);
            yield return null;
        }

        yield return new WaitForSecondsRealtime(2f);

        t = 0f;
        while (t < 0.5f)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            float lerp = Mathf.Clamp01(t / 0.5f);
            _popupScreen.alpha = Mathf.Lerp(1f, 0f, lerp);
            yield return null;
        }
    }

    public void ShowError(string text, Color? color = null, bool trackMouse = false)
    {
        GameObject go = Instantiate(_errorPrefab, this.transform);
        if (color == null) color = Color.red;
        if (go.TryGetComponent<TMP_Text>(out TMP_Text goText))
        {
            StartCoroutine(CreatePopupCoroutine(goText, text, trackMouse, color.Value));
        }
        else
        {
            CLogger.Error("에러 프리팹 잘못됨");
        }
    }

    private IEnumerator CreatePopupCoroutine(TMP_Text go, string text, bool trackMouse, Color color)
    {
        go.text = text;
        go.color = color;
        RectTransform rectTransform = go.GetComponent<RectTransform>();
        Vector2 basePoint;

        if (trackMouse)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle
                (
                    this.GetComponent<RectTransform>(),
                    Input.mousePosition,
                    null,
                    out Vector2 localPoint)
                )
            {
                rectTransform.anchoredPosition = localPoint;
                basePoint = localPoint;
            }
            else
            {
                rectTransform.anchoredPosition = Vector2.zero;
                basePoint = Vector2.zero;
            }
        }
        else
        {
            rectTransform.anchoredPosition = Vector2.zero;
            basePoint = Vector2.zero;
        }
        go.alpha = 0f;
        rectTransform.localScale = Vector3.one * 1.5f;
        float timer = 0f;
        while (timer < 0.125f)
        {
            timer += Time.unscaledDeltaTime;
            rectTransform.localScale = Vector3.one * (1.5f - timer * 4f);
            go.alpha = timer * 8f;
            yield return null;
        }
        go.alpha = 1f;
        rectTransform.localScale = Vector3.one;
        timer = 0f;
        yield return new WaitForSecondsRealtime(0.5f);
        while (timer < 0.5f)
        {
            timer += Time.unscaledDeltaTime;
            rectTransform.anchoredPosition = basePoint + Vector2.up * timer * 2 * (rectTransform.rect.height * 0.1f);
            go.alpha = 1 - timer * 2;
            yield return null;
        }
        Destroy(go.gameObject);
    }

    private static string FormatTimer(float time)
    {
        int seconds = (int)(time * 100f);
        return $"{seconds / 6000:D2}:{seconds / 100 % 60:D2}<size=72%>.{seconds % 100:D2}</size>";
    }
    public IEnumerator ShowSaveNotify()
    {
        if (_saveScreen == null)
        {
            yield break;
        }

        if (_saveCoroutine != null)
        {
            StopCoroutine(_saveCoroutine);
            _saveCoroutine = null;
        }

        _saveCoroutine = StartCoroutine(ShowSaveNotifyInternal());
        yield return _saveCoroutine;
        _saveCoroutine = null;
    }

    private IEnumerator ShowSaveNotifyInternal()
    {
        _saveScreen.alpha = 0f;

        float t = 0f;
        while (t < 0.25f)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            float lerp = Mathf.Clamp01(t / 0.25f);
            _saveScreen.alpha = Mathf.Lerp(0f, 1f, lerp);
            yield return null;
        }

        yield return new WaitForSecondsRealtime(1f);

        t = 0f;
        while (t < 0.25f)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            float lerp = Mathf.Clamp01(t / 0.25f);
            _saveScreen.alpha = Mathf.Lerp(1f, 0f, lerp);
            yield return null;
        }
    }

}
