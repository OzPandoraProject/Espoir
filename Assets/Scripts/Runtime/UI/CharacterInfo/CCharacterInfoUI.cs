using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 정보, 능력치, 기록과 권능 탭을 표시합니다.
/// </summary>
public partial class CCharacterInfoUI : MonoBehaviour
{
    private const int _TAB_COUNT = 4;
    private const int _PAGE_COUNT = 3;
    private const int _HELPER_TAB = 3;

    [Serializable]
    private struct STab
    {
        public CCharacterInfoPointerArea area;
        public RectTransform rect;
        public TMP_Text number;
        public TMP_Text label;
        [Tooltip("켜진 탭 글자 둘레의 빛")]
        public GameObject glow;
    }

    [Header("연결")]
    [SerializeField] private CCharacterInfoSettingsSO _settings;
    [Tooltip("3D 무대. 비워 두면 UI만 움직입니다.")]
    [SerializeField] private CCharacterInfoStage _stage;
    [Tooltip("비워 두면 처음 열 때 씬에서 한 번 찾습니다.")]
    [SerializeField] private CPlayer _player;

    [Header("입력")]
    [SerializeField] private InputAction _toggleAction =
        new InputAction("Toggle", InputActionType.Button, "<Keyboard>/tab");
    [SerializeField] private InputAction _tabAction = CreateAction("Tab",
        "<Keyboard>/1", "<Keyboard>/2", "<Keyboard>/3", "<Keyboard>/4");
    [SerializeField] private InputAction _previousAction = CreateAction("Previous",
        "<Keyboard>/q", "<Keyboard>/leftArrow");
    [SerializeField] private InputAction _nextAction = CreateAction("Next",
        "<Keyboard>/e", "<Keyboard>/rightArrow");

    [Header("화면 구성")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private GameObject _root;
    [SerializeField] private CanvasGroup _playerShade;
    [SerializeField] private CanvasGroup _helperShade;
    [SerializeField] private CanvasGroup _letterbox;
    [SerializeField] private RectTransform _letterboxTop;
    [SerializeField] private RectTransform _letterboxBottom;
    [SerializeField] private RectTransform _sweep;
    [SerializeField] private CanvasGroup _sweepGroup;
    [Tooltip("빛 띠가 지나가는 가로 범위 (띠 가운데의 x, px)")]
    [SerializeField] private Vector2 _sweepRange = new Vector2(-230f, 1290f);
    [SerializeField] private CanvasGroup _overlay;
    [SerializeField] private TMP_Text _hint;
    [SerializeField] private CanvasGroup _loading;
    [SerializeField] private CCharacterInfoPointerArea _closeArea;
    [SerializeField] private CHudBox _closeBox;

    [Header("권능")]
    [SerializeField] private GameObject _powerPanel;

    [Header("이름표")]
    [SerializeField] private RectTransform _side;
    [SerializeField] private CanvasGroup _sideGroup;
    [SerializeField] private CanvasGroup _numberGroup;
    [SerializeField] private RectTransform _numberRect;
    [SerializeField] private TMP_Text _numberText;
    [SerializeField] private TMP_Text _captionText;
    [Tooltip("머리말 숫자와 영문 머리말 사이 (px)")]
    [SerializeField] private float _captionGap = 16f;
    [SerializeField] private Material _numberMaterial;
    [SerializeField] private Material _numberHelperMaterial;
    [SerializeField] private RectTransform _nameRect;
    [SerializeField] private CanvasGroup _nameGroup;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _latinText;
    [SerializeField] private Material _nameMaterial;
    [SerializeField] private Material _nameHelperMaterial;
    [Tooltip("프라도나 이름 둘레의 빛 (프라도나 이름표일 때만 켬)")]
    [SerializeField] private GameObject _nameHelperGlow;

    [Header("탭")]
    [SerializeField] private STab[] _tabs = new STab[_TAB_COUNT];
    [SerializeField] private RectTransform _pill;
    [SerializeField] private CHudBox _pillBox;

    private readonly List<Canvas> _HIDDEN_CANVASES = new List<Canvas>();
    private readonly List<CMinimapHud> _PAUSED_MINIMAPS = new List<CMinimapHud>();
    private bool _isOpen;
    private int _tab;
    private bool _changedGameState;
    private bool _isRestorePending;
    private CInventory _inventory;
    private bool _couldOpenInventory;
    private CInventoryUI _inventoryUI;
    private CShopUI _shopUI;
    private bool _hasSearchedScene;

    public bool IsOpen => _isOpen;

    private void Awake()
    {
        CacheBasePositions();
        if (_powerPanel != null) _powerPanel.SetActive(false);
        if (_root != null) _root.SetActive(false);
    }

    private void OnEnable()
    {
        _toggleAction.Enable();
        _tabAction.Enable();
        _previousAction.Enable();
        _nextAction.Enable();

        for (int i = 0; i < _tabs.Length; i++)
        {
            if (_tabs[i].area != null) _tabs[i].area.Clicked += OnTabClicked;
        }

        if (_closeArea != null) _closeArea.Clicked += OnCloseClicked;
        if (_stage != null) _stage.HelperRevealed += OnHelperRevealed;
    }

    private void OnDisable()
    {
        Close();
        FinishRestore();

        _toggleAction.Disable();
        _tabAction.Disable();
        _previousAction.Disable();
        _nextAction.Disable();

        for (int i = 0; i < _tabs.Length; i++)
        {
            if (_tabs[i].area != null) _tabs[i].area.Clicked -= OnTabClicked;
        }

        if (_closeArea != null) _closeArea.Clicked -= OnCloseClicked;
        if (_stage != null) _stage.HelperRevealed -= OnHelperRevealed;
    }

    private void OnDestroy()
    {
        _toggleAction.Dispose();
        _tabAction.Dispose();
        _previousAction.Dispose();
        _nextAction.Dispose();
    }

    private void Update()
    {
        bool isTextInputFocused = IsTextInputFocused();
        if (!isTextInputFocused && _toggleAction.WasPressedThisFrame())
        {
            Toggle();
            return;
        }

        if (_isRestorePending && (_stage == null || !_stage.IsClosing)) FinishRestore();
        if (!_isOpen) return;

        // 사망이나 씬 이동으로 바뀐 게임 상태는 되돌리지 않는다.
        CGameManager manager = CGameManager.Instance;
        if (_player == null || (_changedGameState && (manager == null || manager.GameState != EGameState.UI)))
        {
            Close();
            return;
        }

        if (!isTextInputFocused) UpdateTabInput();

        float now = Time.unscaledTime;
        UpdatePage(now);
        RefreshValues(now);
        Paint(now);
    }

    /// <summary>
    /// 정보창을 열거나 닫습니다.
    /// </summary>
    public void Toggle()
    {
        if (_isOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    /// <summary>
    /// 다른 UI가 열려 있지 않은 Idle 상태에서 정보창을 엽니다. 성공 여부를 반환합니다.
    /// </summary>
    public bool Open()
    {
        if (_isOpen || _root == null || _settings == null) return false;

        FinishRestore();

        CGameManager manager = CGameManager.Instance;
        if (manager != null && manager.GameState != EGameState.Idle) return false;
        if (manager != null && manager.BattleState == EBattleState.BossBattle) return false;

        FindSceneObjects();
        if (_player == null || _player.HpCurrent <= 0f) return false;
        if (_inventoryUI != null && _inventoryUI.IsOpen) return false;
        if (_shopUI != null && _shopUI.IsOpen) return false;

        _isOpen = true;
        if (_tab >= _HELPER_TAB) _tab = 0;

        _inventory = _player.Inventory;
        if (_inventory != null)
        {
            _couldOpenInventory = _inventory.CanOpenInventory;
            _inventory.CanOpenInventory = false;
        }

        if (manager != null)
        {
            manager.SetGameState(EGameState.UI);
            _changedGameState = true;
        }

        HideOtherCanvases();
        _root.SetActive(true);
        BeginOpen(Time.unscaledTime);
        if (_stage != null) _stage.Open(_tab);
        return true;
    }

    /// <summary>
    /// 정보창을 닫고 게임 상태와 UI 표시를 복원합니다.
    /// </summary>
    public void Close()
    {
        if (!_isOpen) return;

        _isOpen = false;
        if (_powerPanel != null) _powerPanel.SetActive(false);
        if (_root != null) _root.SetActive(false);
        if (_stage != null) _stage.Close();

        // 카메라 복귀가 끝난 뒤 HUD를 다시 표시한다.
        _isRestorePending = true;
        if (_stage == null || !_stage.IsClosing) FinishRestore();

        if (_inventory != null) _inventory.CanOpenInventory = _couldOpenInventory;
        _inventory = null;

        CGameManager manager = CGameManager.Instance;
        if (_changedGameState && manager != null && manager.GameState == EGameState.UI)
        {
            manager.SetGameState(EGameState.Idle);
        }
        else if (_changedGameState && manager == null)
        {
            // 씬 종료 중 게임 매니저가 먼저 사라지면 일시정지를 직접 해제한다.
            Time.timeScale = 1f;
        }

        _changedGameState = false;
    }

    private void SelectTab(int tab)
    {
        if (!_isOpen) return;

        tab = (tab % _TAB_COUNT + _TAB_COUNT) % _TAB_COUNT;
        if (tab == _tab) return;

        int previous = _tab;
        _tab = tab;
        BeginTabChange(previous, Time.unscaledTime);
        if (_powerPanel != null)
        {
            bool showPower = _tab == _HELPER_TAB;
            _powerPanel.SetActive(showPower);

            if (showPower)
            {
                CPradonaPowerUI powerUI = _powerPanel.GetComponentInChildren<CPradonaPowerUI>();
                if (powerUI != null) powerUI.Show(_player);
            }
        }
        if (_stage != null) _stage.ChangeTab(_tab);
        else if (_tab == _HELPER_TAB) OnHelperRevealed();
    }

    private static bool IsTextInputFocused()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return false;

        GameObject selected = eventSystem.currentSelectedGameObject;
        if (selected == null) return false;
        if (selected.TryGetComponent<TMP_InputField>(out TMP_InputField textInput)) return textInput.isFocused;

        return selected.TryGetComponent<InputField>(out InputField input) && input.isFocused;
    }

    private void UpdateTabInput()
    {
        if (_tabAction.WasPressedThisFrame() && _tabAction.activeControl is KeyControl key)
        {
            SelectTab(key.keyCode - Key.Digit1);
            return;
        }

        if (_nextAction.WasPressedThisFrame())
        {
            SelectTab(_tab + 1);
            return;
        }

        if (_previousAction.WasPressedThisFrame()) SelectTab(_tab - 1);
    }

    private void OnTabClicked(CCharacterInfoPointerArea area)
    {
        for (int i = 0; i < _tabs.Length; i++)
        {
            if (_tabs[i].area == area)
            {
                SelectTab(i);
                return;
            }
        }
    }

    private void OnCloseClicked(CCharacterInfoPointerArea area)
    {
        Close();
    }

    private void OnHelperRevealed()
    {
        if (!_isOpen || _tab != _HELPER_TAB) return;

        SetIdentity(true, Time.unscaledTime, false);
    }

    private void FindSceneObjects()
    {
        if (_hasSearchedScene && _player != null) return;

        if (_player == null) _player = FindFirstObjectByType<CPlayer>();
        _inventoryUI = FindFirstObjectByType<CInventoryUI>();
        _shopUI = FindFirstObjectByType<CShopUI>();
        _hasSearchedScene = _player != null;
        if (_player == null) CLogger.Warn($"{name}: 플레이어를 찾을 수 없습니다.");
    }

    private void HideOtherCanvases()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == _canvas || !canvas.enabled || !canvas.isRootCanvas) continue;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            // CanvasScaler가 없는 색 공간 합성용 캔버스는 유지한다.
            if (canvas.GetComponent<CanvasScaler>() == null) continue;

            canvas.enabled = false;
            _HIDDEN_CANVASES.Add(canvas);

            foreach (CMinimapHud minimap in canvas.GetComponentsInChildren<CMinimapHud>(true))
            {
                if (!minimap.enabled) continue;

                minimap.enabled = false;
                _PAUSED_MINIMAPS.Add(minimap);
            }
        }
    }

    private void FinishRestore()
    {
        if (!_isRestorePending) return;

        _isRestorePending = false;
        RestoreOtherCanvases();
    }

    private void RestoreOtherCanvases()
    {
        for (int i = 0; i < _HIDDEN_CANVASES.Count; i++)
        {
            if (_HIDDEN_CANVASES[i] != null) _HIDDEN_CANVASES[i].enabled = true;
        }

        _HIDDEN_CANVASES.Clear();

        for (int i = 0; i < _PAUSED_MINIMAPS.Count; i++)
        {
            if (_PAUSED_MINIMAPS[i] != null) _PAUSED_MINIMAPS[i].enabled = true;
        }

        _PAUSED_MINIMAPS.Clear();
    }

    private static InputAction CreateAction(string name, params string[] bindings)
    {
        InputAction action = new InputAction(name, InputActionType.Button);

        foreach (string binding in bindings)
        {
            action.AddBinding(binding);
        }

        return action;
    }
}
