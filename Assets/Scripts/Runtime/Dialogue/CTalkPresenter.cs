using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// NPC 선택, 대사와 선택지 진행, 대화 종료 후 이동 및 소환을 처리합니다.
/// 대화 중에는 플레이어 입력, 인벤토리와 기본 메뉴를 막고 HUD와 카메라를 전환합니다.
/// 입력이 중복되지 않도록 CDialogueManager와 함께 사용하지 않습니다.
/// </summary>
[DefaultExecutionOrder(50)]
public class CTalkPresenter : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private CPlayer _player;
    [SerializeField] private CTalkCamera _talkCamera;
    [SerializeField] private CBossSummoner _summoner;
    [SerializeField] private Camera _camera;
    [SerializeField] private GameObject _hudRoot;
    [Tooltip("대화 · 소환 글씨 동안 HUD 와 같이 숨길 미니맵")]
    [SerializeField] private CMinimapHud _minimap;

    [Header("화면")]
    [SerializeField] private RectTransform _canvasRect;
    [SerializeField] private RectTransform _tagRoot;
    [SerializeField] private CNpcNameTagView _tagTemplate;
    [SerializeField] private CTalkPromptView _prompt;
    [SerializeField] private CTalkDialogView _dialog;
    [SerializeField] private CBossSummonTitleView _summonTitle;
    [SerializeField] private CTalkTravelFadeView _travelFade;

    [Header("현재 맵")]
    [SerializeField] private string _mapKey = "m51";

    [Header("대화 표시")]
    [SerializeField, Min(1f)] private float _typingSpeed = 30f;
    [SerializeField, Min(0f)] private float _nameRadius = 16f;
    [SerializeField] private float _headLift = 0.3f;
    [SerializeField, Min(0f)] private float _hudFadeTime = 0.35f;
    [SerializeField, Min(0f)] private float _summonHideTime = 0.4f;
    [SerializeField, Min(0f)] private float _travelDelay = 0.75f;
    [SerializeField, Min(0f)] private float _travelFadeOutDelay = 0.5f;

    [SerializeField] private string _topicPrompt = "무슨 이야기를 할까?";
    [SerializeField] private string _normalTopic = "일반 대화";

    [Header("입력 (New Input System)")]
    [SerializeField] private InputAction _talkAction = new InputAction("Talk", InputActionType.Button, "<Keyboard>/f");
    [SerializeField] private InputAction _advanceAction = CreateAction("Advance",
        "<Keyboard>/space", "<Keyboard>/enter", "<Keyboard>/numpadEnter");
    [SerializeField] private InputAction _closeAction =
        new InputAction("Close", InputActionType.Button, "<Keyboard>/escape");
    [SerializeField] private InputAction _choiceAction = CreateAction("Choice",
        "<Keyboard>/1", "<Keyboard>/2", "<Keyboard>/3",
        "<Keyboard>/4", "<Keyboard>/5", "<Keyboard>/6", "<Keyboard>/7", "<Keyboard>/8", "<Keyboard>/9");

    private readonly Dictionary<CTalkNpc, CNpcNameTagView> _TAGS = new Dictionary<CTalkNpc, CNpcNameTagView>();
    private readonly List<CTalkNpc> _STALE_TAGS = new List<CTalkNpc>();
    private readonly List<string> _HISTORY = new List<string>();
    private readonly List<bool> _HISTORY_IS_MINE = new List<bool>();
    private CanvasGroup _hudGroup;
    private SCssTween _hudAlpha;
    private CTalkNpc _current;
    private bool _isOpen;
    private bool _isBusy;
    private CTalkNode _node;
    private CTalkScriptSO _questTopic;
    private readonly List<CTalkChoice> _CHOICES = new List<CTalkChoice>();
    private int _lineIndex;
    private string _text = string.Empty;
    private float _shown;
    private bool _isTyping;
    private bool _isPicking;
    private string _pendingChoice;
    private bool _wasLocked;
    private CInventory _talkInventory;
    private CInventoryUI _inventoryUI;
    private bool _couldOpenInventory;
    private bool _changedGameState;
    private float _travelTime = -1f;
    private string _travelDestination;
    private bool _isInRange;

    private CShopUI _targetShop;

    /// <summary>
    /// 지금 이름표 · 말 걸기 · 대화 대상인 NPC.
    /// </summary>
    public CTalkNpc CurrentNpc => _current;

    public bool IsOpen => _isOpen;

    public bool IsInRange => _isInRange;

    public bool IsPicking => _isPicking && !_isTyping;

    public bool IsTyping => _isTyping;

    public string CurrentNodeId => _node != null ? _node.id : null;

    public string CurrentText => _text;

    public CTalkDialogView Dialog => _dialog;

    public string MapKey => _mapKey;

    private void OnEnable()
    {
        _talkAction.Enable();
        _advanceAction.Enable();
        _closeAction.Enable();
        _choiceAction.Enable();

        if (_dialog != null)
        {
            _dialog.AdvanceRequested += Advance;
            _dialog.ChoiceRequested += Choose;
        }
    }

    private void OnDisable()
    {
        Close();
        _hudAlpha.Snap(1f);

        if (_hudGroup != null)
        {
            _hudGroup.alpha = 1f;
            _hudGroup.blocksRaycasts = true;
        }

        if (_minimap != null) _minimap.SetVisible(true);

        _talkAction.Disable();
        _advanceAction.Disable();
        _closeAction.Disable();
        _choiceAction.Disable();

        if (_dialog != null)
        {
            _dialog.AdvanceRequested -= Advance;
            _dialog.ChoiceRequested -= Choose;
        }
    }

    private void OnDestroy()
    {
        _talkAction.Dispose();
        _advanceAction.Dispose();
        _closeAction.Dispose();
        _choiceAction.Dispose();
    }

    private void Start()
    {
        if (_camera == null) _camera = Camera.main;

        if (_hudRoot != null)
        {
            _hudGroup = _hudRoot.GetComponent<CanvasGroup>();
            if (_hudGroup == null) _hudGroup = _hudRoot.AddComponent<CanvasGroup>();
        }

        _hudAlpha.Snap(1f);
        if (_tagTemplate != null) _tagTemplate.gameObject.SetActive(false);
        if (_dialog != null) _dialog.Close();
    }

    private void Update()
    {
        float now = Time.unscaledTime;

        UpdateCurrentNpc();
        UpdateTravel(now);
        UpdateInput();

        if (_isOpen && _isTyping)
        {
            _shown += Mathf.Min(0.1f, Time.unscaledDeltaTime) * _typingSpeed;

            if (_shown >= _text.Length)
            {
                _shown = _text.Length;
                _isTyping = false;
                if (_CHOICES.Count > 0 && _lineIndex == _node.LineCount - 1) _isPicking = true;
            }

            PaintProgress(now);
        }

        if (_isOpen && _current == null && !_isBusy) Close();
        if (_dialog != null) _dialog.Tick(now);

        // 대화와 소환 안내에 각각 설정한 전환 시간으로 HUD를 숨긴다.
        bool isSummoning = _summonTitle != null && _summonTitle.IsPlaying;
        float hudTarget = _isOpen || isSummoning ? 0f : 1f;
        _hudAlpha.To(hudTarget, now, isSummoning && !_isOpen ? _summonHideTime : _hudFadeTime, 0f, CCssEase.EASE);
        if (_minimap != null) _minimap.SetVisible(hudTarget > 0f);

        if (_hudGroup != null)
        {
            _hudGroup.alpha = _hudAlpha.Value(now);
            _hudGroup.blocksRaycasts = !_isOpen;
        }
    }

    // 카메라 위치가 정해진 뒤 NPC 이름표의 화면 좌표를 구한다.
    private void LateUpdate()
    {
        float now = Time.unscaledTime;
        bool isSummoning = _summonTitle != null && _summonTitle.IsPlaying;
        bool showNpcUI = !_isOpen && !isSummoning && (_targetShop == null || !_targetShop.IsOpen);
        bool isCinematic = _talkCamera != null && _talkCamera.IsCinematic;
        Vector3 feet = _player != null ? _player.transform.position : Vector3.zero;

        foreach (CTalkNpc npc in CTalkNpc.All)
        {
            if (!_TAGS.ContainsKey(npc) && _tagTemplate != null)
            {
                CNpcNameTagView tag = Instantiate(_tagTemplate, _tagRoot);
                tag.name = "Tag_" + npc.NpcId;
                tag.gameObject.SetActive(true);
                tag.Setup(npc);
                _TAGS.Add(npc, tag);
            }
        }

        _STALE_TAGS.Clear();

        foreach (KeyValuePair<CTalkNpc, CNpcNameTagView> pair in _TAGS)
        {
            CTalkNpc npc = pair.Key;

            if (npc == null || !npc.isActiveAndEnabled)
            {
                _STALE_TAGS.Add(npc);
                continue;
            }

            bool isVisible = TryGetHeadScreen(npc, out Vector2 head);
            float distance = FlatDistance(feet, npc.FeetPosition);
            bool isOn = distance < _nameRadius && isVisible && showNpcUI && npc.IsTalkable;
            pair.Value.Tick(head, isOn, now);
        }

        foreach (CTalkNpc npc in _STALE_TAGS)
        {
            if (_TAGS.TryGetValue(npc, out CNpcNameTagView tag) && tag != null) Destroy(tag.gameObject);
            _TAGS.Remove(npc);
        }

        if (_prompt != null)
        {
            Vector2 head = Vector2.zero;
            bool isHeadVisible = _current != null && TryGetHeadScreen(_current, out head);
            bool isOn = _isInRange && isHeadVisible && showNpcUI && !isCinematic;
            _prompt.Tick(head, isOn, now);
        }
    }

    /// <summary>
    /// 지금 NPC 와 대화를 시작합니다 (F 키와 같은 경로). 시작하지 못하면 false.
    /// </summary>
    public bool TryOpen()
    {
        if (!_isInRange) return false;
        return TryOpen(_current, null);
    }

    /// <summary>
    /// 거리와 상관없이 지정한 NPC 와 대화를 엽니다 (튜토리얼처럼 NPC 가 먼저 말을 거는 경우).
    /// 지정한 대사부터 대화를 엽니다. 시작 대사를 생략하면 기본 대사로 시작합니다.
    /// </summary>
    public bool TryOpen(CTalkNpc npc, string startNode)
    {
        if (_isOpen || _isBusy || npc == null || _dialog == null) return false;
        if (_targetShop != null && _targetShop.IsOpen) return false;

        CGameManager manager = CGameManager.Instance;
        if (manager != null && manager.GameState != EGameState.Idle) return false;

        if (_inventoryUI == null) _inventoryUI = FindFirstObjectByType<CInventoryUI>();
        if (_inventoryUI != null && _inventoryUI.IsOpen) return false;

        _current = npc;
        _isOpen = true;
        _HISTORY.Clear();
        _HISTORY_IS_MINE.Clear();
        _pendingChoice = null;
        _text = string.Empty;

        if (_player != null)
        {
            _wasLocked = _player.IsInputLocked;
            _player.SetInputLocked(true);
            _talkInventory = _player.Inventory;

            if (_talkInventory != null)
            {
                _couldOpenInventory = _talkInventory.CanOpenInventory;
                _talkInventory.CanOpenInventory = false;
            }
        }

        if (manager != null)
        {
            manager.SetGameState(EGameState.Story);
            _changedGameState = true;
        }

        _current.SetTalker(_player != null ? _player.transform : null);
        if (_talkCamera != null) _talkCamera.BeginTalk(_current);
        _current.PlayDialogueBGM();

        _dialog.Open(_current.DisplayName, _current.Title, Time.unscaledTime);
        CQuestManager.ReportTalk(_current.NpcId);
        bool isSummoned = _summoner != null && _summoner.IsSummoned;
        string first = _current.GetStartNode(isSummoned);
        _questTopic = string.IsNullOrEmpty(startNode) ? _current.GetQuestTalk() : null;
        if (_questTopic == null) Enter(string.IsNullOrEmpty(startNode) ? first : startNode);
        else if (_current.FindNode(first, _mapKey) == null)
        {
            Enter(_current.SelectQuestTalk(_questTopic));
            _questTopic = null;
        }
        else
        {
            ShowNode(new CTalkNode
            {
                ask = _topicPrompt,
                choices = new[]
                {
                    new CTalkChoice { text = _questTopic.QuestLink.quest.Title },
                    new CTalkChoice { text = _normalTopic, to = first },
                },
            });
        }
        return true;
    }

    /// <summary>
    /// 대화를 닫고 입력 · 카메라 · 게임 상태 · HUD 를 되돌립니다. 열려 있지 않으면 아무것도 하지 않습니다.
    /// </summary>
    public void Close()
    {
        if (!_isOpen) return;

        _isOpen = false;
        _isBusy = false;
        _isTyping = false;
        _isPicking = false;
        _node = null;

        if (_dialog != null) _dialog.Close();
        if (_talkCamera != null) _talkCamera.EndTalk();
        if (_current != null) _current.SetTalker(null);
        if (_player != null) _player.SetInputLocked(_wasLocked);
        if (_talkInventory != null) _talkInventory.CanOpenInventory = _couldOpenInventory;
        _talkInventory = null;

        CGameManager manager = CGameManager.Instance;

        if (_changedGameState && manager != null && manager.GameState == EGameState.Story)
        {
            manager.SetGameState(EGameState.Idle);
        }

        _changedGameState = false;
    }

    /// <summary>
    /// 다음으로 넘깁니다: 글자 중이면 다 보여 주고, 아니면 다음 줄 · 다음 묶음 · 끝 동작.
    /// </summary>
    public void Advance()
    {
        if (!_isOpen || _isBusy || _node == null) return;

        if (_isTyping)
        {
            _shown = _text.Length;
            _isTyping = false;
            if (_CHOICES.Count > 0 && _lineIndex == _node.LineCount - 1) _isPicking = true;
            PaintProgress(Time.unscaledTime);
            return;
        }

        if (_isPicking) return;

        if (_node.lines != null && ++_lineIndex < _node.lines.Length)
        {
            ShowLine(_node.lines[_lineIndex]);
            return;
        }

        if (_node.HasChoices && _CHOICES.Count == 0)
        {
            Close();
            return;
        }

        if (!string.IsNullOrEmpty(_node.next))
        {
            Enter(_node.next);
            return;
        }

        switch (_node.end)
        {
            case ETalkEnd.Travel:
                StartTravel(_node.dest);
                return;
            case ETalkEnd.Summon:
                Close();
                if (_summoner != null) _summoner.Summon();
                return;
            case ETalkEnd.Unsummon:
                Close();
                if (_summoner != null) _summoner.Unsummon();
                return;
            case ETalkEnd.Shop:
                _targetShop = _current != null ? _current.ShopUI : null;
                Close();
                if (_targetShop == null)
                {
                    CLogger.Error("CTalkPresenter : 상점 UI 미연결");
                    return;
                }
                _targetShop.Open();
                return;
            default:
                Close();
                return;
        }
    }

    /// <summary>
    /// index 번째(0부터) 선택지를 고릅니다. 고름 상태가 아니면 무시합니다.
    /// </summary>
    public void Choose(int index)
    {
        if (!_isOpen || _isBusy || _isTyping || !_isPicking || _node == null || !_node.HasChoices) return;
        if (index < 0 || index >= _CHOICES.Count) return;

        CTalkChoice choice = _CHOICES[index];
        if (_current == null || !_current.CanChoose(choice))
        {
            Close();
            return;
        }

        _pendingChoice = choice.text;
        if (_questTopic != null)
        {
            CTalkScriptSO topic = _questTopic;
            _questTopic = null;
            if (index == 0)
            {
                if (_current.GetQuestTalk() != topic) Close();
                else Enter(_current.SelectQuestTalk(topic));
                return;
            }
        }
        _current.ConfirmChoice(choice);
        Enter(choice.to);
    }

    private void Enter(string id)
    {
        CTalkNode node = _current != null ? _current.FindNode(id, _mapKey) : null;

        if (node == null)
        {
            CLogger.Warn($"CTalkPresenter: '{id}' 대사 묶음을 찾지 못해 대화를 닫습니다.");
            Close();
            return;
        }

        ShowNode(node);
    }

    private void ShowNode(CTalkNode node)
    {
        _CHOICES.Clear();
        if (node.HasChoices)
        {
            foreach (CTalkChoice choice in node.choices)
            {
                if (!_current.CanChoose(choice)) continue;
                if (_summoner == null && choice.questAction == ETalkQuestAction.None &&
                    !string.IsNullOrEmpty(choice.to))
                {
                    CTalkNode target = _current.FindNode(choice.to, _mapKey);
                    if (target?.end == ETalkEnd.Summon && !target.HasChoices && string.IsNullOrEmpty(target.next))
                        continue;
                }
                _CHOICES.Add(choice);
            }
        }

        _node = node;
        _lineIndex = 0;
        ShowLine(node.GetLine(0));
    }

    private void ShowLine(string text)
    {
        if (!string.IsNullOrEmpty(_text))
        {
            _HISTORY.Add(_text);
            _HISTORY_IS_MINE.Add(false);
        }

        if (_pendingChoice != null)
        {
            _HISTORY.Add(_pendingChoice);
            _HISTORY_IS_MINE.Add(true);
            _pendingChoice = null;
        }

        _text = text ?? string.Empty;
        _shown = 0f;
        _isTyping = true;
        _isPicking = false;

        if (_current != null) _current.Gesture();

        float now = Time.unscaledTime;
        _dialog.ShowLine(_text, _HISTORY, _HISTORY_IS_MINE, now);
        PaintProgress(now);
    }

    private void PaintProgress(float now)
    {
        bool isPicking = _isPicking && !_isTyping;
        bool isWaiting = !_isTyping && !isPicking;
        _dialog.SetProgress(Mathf.FloorToInt(_shown), isWaiting, isPicking, _CHOICES, now);
    }

    // 대화 전용 페이드가 있으면 암전 후 이동을 요청하고, 없으면 씬 전환의 페이드를 사용한다.
    private void StartTravel(string destination)
    {
        // 씬 이동이 화면 전환을 맡는 경우 암전을 중복 적용하지 않는다.
        if (_travelFade == null)
        {
            Close();
            if (!CTalkTravel.Request(destination))
            {
                CGameManager.Instance.ShowError("해당 지역으로 이동할 수 없습니다.", Color.red, true);
            }
            return;
        }

        _isBusy = true;
        _travelTime = 0f;
        _travelDestination = destination;
        string name = destination;
        CTravelGuideSO.CPlace place = _current != null && _current.TravelGuide != null
            ? _current.TravelGuide.FindPlace(destination) : null;
        if (place != null) name = place.areaName;
        if (_travelFade != null) _travelFade.Set(true, name, Time.unscaledTime);
    }

    private void UpdateTravel(float now)
    {
        if (_travelTime < 0f) return;

        float previous = _travelTime;
        _travelTime += Time.unscaledDeltaTime;

        if (previous < _travelDelay && _travelTime >= _travelDelay)
        {
            Close();
            CTalkTravel.Request(_travelDestination);
        }

        if (_travelTime >= _travelDelay + _travelFadeOutDelay)
        {
            _travelTime = -1f;
            if (_travelFade != null) _travelFade.Set(false, null, now);
        }
    }

    private void UpdateInput()
    {
        if (!_isOpen)
        {
            if (_talkAction.WasPressedThisFrame()) TryOpen();
            return;
        }

        if (_closeAction.WasPressedThisFrame())
        {
            if (!_isBusy) Close();
            return;
        }

        if (_choiceAction.WasPressedThisFrame() && _choiceAction.activeControl is KeyControl key)
        {
            Choose(key.keyCode - Key.Digit1);
            return;
        }

        if (_talkAction.WasPressedThisFrame() || _advanceAction.WasPressedThisFrame()) Advance();
    }

    private void UpdateCurrentNpc()
    {
        if (_player == null)
        {
            _isInRange = false;
            return;
        }

        Vector3 feet = _player.transform.position;

        // 대화 중에는 바뀌지 않음, 아니면 가장 가까운 NPC
        if (!_isOpen)
        {
            CTalkNpc best = null;
            float bestDistance = float.MaxValue;

            foreach (CTalkNpc npc in CTalkNpc.All)
            {
                if (!npc.IsTalkable) continue;

                float distance = FlatDistance(feet, npc.FeetPosition);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = npc;
                }
            }

            _current = best;
        }

        bool isCinematic = _talkCamera != null && _talkCamera.IsCinematic;
        float blend = _talkCamera != null ? _talkCamera.Blend : 0f;
        _isInRange = _current != null && FlatDistance(feet, _current.FeetPosition) < _current.TalkRadius &&
            !_isOpen && !isCinematic && blend < 0.05f;
    }

    private bool TryGetHeadScreen(CTalkNpc npc, out Vector2 css)
    {
        css = Vector2.zero;
        if (_camera == null || _canvasRect == null) return false;

        Vector3 viewport = _camera.WorldToViewportPoint(npc.GetHeadPosition() + Vector3.up * _headLift);
        Rect rect = _canvasRect.rect;
        css = new Vector2(viewport.x * rect.width, (1f - viewport.y) * rect.height);
        return viewport.z > 0f && viewport.x > -0.1f && viewport.x < 1.1f && viewport.y > -0.1f && viewport.y < 1.1f;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
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
