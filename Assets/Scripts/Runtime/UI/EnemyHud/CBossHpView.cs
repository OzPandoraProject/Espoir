using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 화면 상단 중앙에 보스 초상화, 이름, 체력 막대와 남은 체력 비율을 표시합니다.
/// 실제 보스(CSlothBoss)의 HP 를 읽고, 맞은 순간 · 치명은 CUnit.OnDamageResolved 로 받는다. 게임 수치는 읽기만 한다.
/// 배치 단위는 1080 높이 기준 px이며, x는 화면 중앙 기준이고 y는 위에서 아래로 증가합니다.
/// </summary>
[DefaultExecutionOrder(100)]
public class CBossHpView : MonoBehaviour
{
    [Serializable]
    private struct SBossLayout
    {
        public float top;
        public float portraitSize;
        public float portraitGap;
        public float barWidth;
        public float barHeight;
        public float tagGap;
        public float tagPaddingX;
        public float tagHeight;
        public float overhangRight;
        public float rowBaseline;
        public float barTop;
        public float tagTop;
        public float tagBaseline;
        public float nameGap;
        public float valueGap;
        public Vector4 veilOutset;
        public float lineOutsetX;
        public float lineBelow;
    }

    [Header("대상")]
    [SerializeField] private CEnemy _boss;
    [SerializeField] private CPlayer _player;
    [SerializeField] private string _bossName = "벨페고르";
    [SerializeField] private string _bossTitle = "나태의 군주";

    [Header("보이기")]
    [Tooltip("이 거리 안에 들어오면 보임 (m)")]
    [SerializeField] private float _showRange = 15f;
    [Tooltip("보이는 중에 이 거리 밖으로 나가면 숨김 (m)")]
    [SerializeField] private float _hideRange = 22f;
    [Tooltip("보스가 맞은 뒤 거리와 상관없이 보이는 시간 (s)")]
    [SerializeField] private float _engageHold = 10f;
    [Tooltip("보스 HP가 0이 된 뒤 사망 표시를 유지하는 시간 (초)")]
    [SerializeField] private float _deadHold = 4.2f;

    [Header("요소")]
    [SerializeField] private GameObject _content;
    [SerializeField] private CCssBoxGraphic _veil;
    [SerializeField] private CCssBoxGraphic _line;
    [SerializeField] private CCssBoxGraphic _portrait;
    [SerializeField] private CCssBoxGraphic _bar;
    [SerializeField] private CCssBoxGraphic _tag;
    [SerializeField] private TextMeshProUGUI _name;
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private TextMeshProUGUI _hpLabel;
    [SerializeField] private TextMeshProUGUI _hpValue;
    [SerializeField] private TextMeshProUGUI _hpMax;
    [SerializeField] private TextMeshProUGUI _tagText;
    [SerializeField] private CHudGlowFx _fx;

    [Header("요소 배치")]
    [SerializeField] private SBossLayout _layout;

    [Header("글자 투명도")]
    [SerializeField] private float _titleAlpha = 0.8f;
    [SerializeField] private float _labelAlpha = 0.8f;
    [SerializeField] private float _lowThreshold = 0.25f;

    [Header("맞음 효과")]
    [SerializeField] private float _trailDelay = 0.45f;
    [SerializeField] private float _trailSpeed = 0.35f;
    [SerializeField] private float _minFlashWidth = 3f;
    [SerializeField] private Vector3 _sparkColorA = new Vector3(1.28f, 1.0f, 0.72f);
    [SerializeField] private Vector3 _sparkColorB = new Vector3(1.3f, 1.02f, 0.62f);
    [SerializeField] private Vector3 _flareColor = new Vector3(1.58f, 1.22f, 0.86f);

    [Header("상태이상 보여주는 부분")]
    [SerializeField] private RectTransform _statusRoot;
    [SerializeField] private CStatusUI _statusPrefab;
    [SerializeField] private CTooltipUI _tooltip;

    [Header("타이머 보여주는 부분")]
    [SerializeField] private TMP_Text _currentTimer;
    [SerializeField] private TMP_Text _bestTimer;

    [Header("무력화 게이지")]
    [SerializeField] private CCssBoxGraphic _groggyBar;
    [SerializeField] private TMP_Text _groggyRemainHp;
    [SerializeField] private TMP_Text _groggyRemainTime;

    // 피격과 치명타 흔들림의 정규화 시간별 위치입니다.
    private static readonly float[] _SHAKE_T = { 0f, 0.2f, 0.5f, 0.8f, 1f };
    private static readonly Vector2[] _SHAKE_V = { Vector2.zero, new Vector2(-3f, 1f), new Vector2(2f, -1f),
        new Vector2(-1f, 0f), Vector2.zero };
    private static readonly float[] _SHAKE_CRIT_T = { 0f, 0.15f, 0.4f, 0.65f, 0.85f, 1f };
    private static readonly Vector2[] _SHAKE_CRIT_V = { Vector2.zero, new Vector2(-6f, 1f), new Vector2(5f, -1f),
        new Vector2(-3f, 1f), new Vector2(1f, 0f), Vector2.zero };
    // x = 투명도, y = 밝기
    private static readonly float[] _FLASH_T = { 0f, 0.4f, 1f };
    private static readonly Vector2[] _FLASH_V = { new Vector2(1f, 2.6f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
    private static readonly float[] _FLASH_CRIT_T = { 0f, 0.25f, 1f };
    private static readonly Vector2[] _FLASH_CRIT_V = { new Vector2(1f, 3f), new Vector2(1f, 1.4f),
        new Vector2(0f, 1f) };
    private const float _SHAKE_TIME = 0.28f;
    private const float _SHAKE_CRIT_TIME = 0.4f;
    private const float _FLASH_TIME = 0.5f;
    private const float _FLASH_CRIT_TIME = 0.75f;
    private const float _BOSS_SEARCH_INTERVAL = 0.5f;

    private readonly char[] _HP_CHARS = new char[16];
    private readonly char[] _MAX_CHARS = new char[16];
    private readonly char[] _PCT_CHARS = new char[8];

    private bool _isShown;
    private bool _isDead;
    private float _deadAt;
    private float _spawnAt = -99f;
    private float _lastHitAt = -99f;
    private float _hitAt = -99f;
    private bool _isCritHit;
    private float _flashLeft;
    private float _flashRight;
    private float _t = 1f;
    private float _td = 1f;
    private float _lastPct = -1f;
    private int _lastHp = -1;
    private int _hpCount;
    private int _maxCount;
    private int _pctCount;
    private int _lastMax = -1;
    private float _nextSearch;
    private Vector2 _barBase;
    private float _barLeftX;
    private float _phase;

    private SCssTransition _fill;
    private SCssTransition _trailAlpha;
    private SCssTransition _glow;
    private SCssTransition _low;
    private SCssTransition _valueAlpha;
    private SCssTransition _nameAlpha;
    private SCssTransition _titleTween;
    private SCssTransition _tagAlpha;
    private SCssTransition _portraitAlpha;
    private SCssTransition _portraitGray;
    private SCssTransition _portraitBright;
    private SCssTransition _portraitGlow;
    private SCssTransition _portraitShadow;
    private SCssTransition _veilAlpha;
    private SCssTransition _lineAlpha;

    // 상태이상 부분
    private readonly Dictionary<CStatus, CStatusUI> _statusViews = new();
    private readonly HashSet<CStatus> _currentStatus = new();
    private readonly List<CStatus> _removedStatus = new();
    private int _debuffViewCount;

    // 그로기 부분
    private bool _wasGroggyCheckActive;
    private float _groggyHideAt = -1f;

    /// <summary>
    /// 지금 화면에 보이는지 (검증 · 다른 UI 참고용).
    /// </summary>
    public bool IsShown => _isShown;

    /// <summary>
    /// 지금 연결된 보스.
    /// </summary>
    public CEnemy Boss => _boss;

    private void Awake()
    {
        _phase = UnityEngine.Random.value * 12f;
        _name.text = _bossName;
        _title.text = _bossTitle;
        _hpLabel.text = "HP";
        SetShown(false);
    }

    private void Start()
    {
        if (_boss != null)
        {
            _name.text = _boss.Name;
            _title.text = _boss.Nickname;
        }
    }

    private void OnEnable()
    {
        CUnit.OnDamageResolved += HandleDamageResolved;
    }

    private void OnDisable()
    {
        CUnit.OnDamageResolved -= HandleDamageResolved;
        SetShown(false);
    }

    private void HandleDamageResolved(SDamageResult result)
    {
        if (_boss == null || result.target != _boss || result.hpLost <= 0f) return;

        float now = Time.time;
        _lastHitAt = now;
        if (!_isShown) return;

        float prev = _t;
        _t = Ratio();
        _hitAt = now;
        _isCritHit = result.isCritical;

        // 현재 체력 끝부터 피격 전 체력 끝까지 표시하며 최소 폭은 3px입니다.
        float dmg = Mathf.Clamp01(prev - _t);
        _flashLeft = _t;
        _flashRight = _t + Mathf.Max(_minFlashWidth / _layout.barWidth, dmg);

        Vector2 tip = WebToLocal(_barLeftX + _layout.barWidth * _t, _layout.barTop + _layout.barHeight * 0.5f);
        if (_fx != null)
        {
            Vector3 c = UnityEngine.Random.value < 0.5f ? _sparkColorA : _sparkColorB;
            _fx.Spark(tip, c, 80f, -Mathf.PI * 0.5f, 2.1f, 1.1f, 0.32f, 3.4f);
            if (_isCritHit) _fx.Flare(tip, 26f, _flareColor, 0.38f, 0.3f);
        }
    }

    private void LateUpdate()
    {
        float now = Time.time;
        FindBoss(now);

        bool shouldShow = ShouldShow(now);
        if (shouldShow != _isShown)
        {
            SetShown(shouldShow);
            if (shouldShow) Spawn(now);
        }

        if (!_isShown) return;

        UpdateState(now);
        UpdateTexts();
        Layout(UpdateGroggy());
        Paint(now);
        UpdateStatus();
        UpdateTimerText();
    }

    private void FindBoss(float now)
    {
        if (_boss != null || now < _nextSearch) return;

        _nextSearch = now + _BOSS_SEARCH_INTERVAL;
        _boss = FindFirstObjectByType<CSlothBoss>();
        if (_player == null) _player = FindFirstObjectByType<CPlayer>();
    }

    private bool ShouldShow(float now)
    {
        if (_boss == null || !_boss.isActiveAndEnabled) return false;
        if (IsTalking()) return false;
        // HP 0: 보이던 중이면 사망 표시를 _deadHold 동안 유지 (사망 판정은 UpdateState 에서)
        if (_boss.HpCurrent <= 0f) return _isShown && (!_isDead || now - _deadAt < _deadHold);

        if (now - _lastHitAt < _engageHold) return true;
        CGameManager gm = CGameManager.Instance;
        if (gm != null && gm.BattleState == EBattleState.BossBattle) return true;
        if (_player == null) return false;

        float d = Vector3.Distance(_player.transform.position, _boss.transform.position);
        return d <= (_isShown ? _hideRange : _showRange);
    }

    // 대화와 소환 연출 중에는 체력 UI를 숨깁니다.
    private bool IsTalking()
    {
        if (_player != null && _player.IsInputLocked) return true;
        CGameManager gm = CGameManager.Instance;
        return gm != null && gm.GameState == EGameState.Story;
    }

    private void SetShown(bool isShown)
    {
        _isShown = isShown;
        if (_content != null && _content.activeSelf != isShown) _content.SetActive(isShown);
        if (!isShown)
        {
            _wasGroggyCheckActive = false;
            _groggyHideAt = -1;
        }
    }

    // 등장 시 막대를 왼쪽부터 채우며 이름과 배경을 표시합니다.
    private void Spawn(float now)
    {
        _spawnAt = now;
        _isDead = false;
        _t = Ratio();
        _td = _t;
        _hitAt = -99f;
        _lastPct = -1f;
        _lastHp = -1;
        _lastMax = -1;
        _fill.Snap(_t);
        _trailAlpha.Snap(1f);
        _glow.Snap(1f);
        _low.Snap(_t < _lowThreshold ? 1f : 0f);
        _valueAlpha.Snap(1f);
        _nameAlpha.Snap(1f);
        _titleTween.Snap(_titleAlpha);
        _tagAlpha.Snap(1f);
        _portraitAlpha.Snap(1f);
        _portraitGray.Snap(0f);
        _portraitBright.Snap(1f);
        _portraitGlow.Snap(1f);
        _portraitShadow.Snap(_low.Target);
        _veilAlpha.Snap(1f);
        _lineAlpha.Snap(1f);
        SetBestTimer();
    }

    private float Ratio()
    {
        if (_boss == null || _boss.HpMax <= 0f) return 0f;
        return Mathf.Clamp01(_boss.HpCurrent / _boss.HpMax);
    }

    private void UpdateState(float now)
    {
        _t = Ratio();

        if (!_isDead && _t <= 0f)
        {
            _isDead = true;
            _deadAt = now;
            _fill.Set(0f, now, 0.9f, 0f, new Vector4(0.3f, 0f, 0.2f, 1f));
            _trailAlpha.Set(0f, now, 0.8f, 0.45f, CCssEasing.EASE);
            _glow.Set(0f, now, 0.9f, 0.5f, CCssEasing.EASE);
            _valueAlpha.Set(0.55f, now, 0.8f, 0.3f, CCssEasing.EASE);
            _nameAlpha.Set(0.32f, now, 0.8f, 0.3f, CCssEasing.EASE);
            _titleTween.Set(0.32f, now, 0.8f, 0.3f, CCssEasing.EASE);
            _tagAlpha.Set(0.4f, now, 0.8f, 0.3f, CCssEasing.EASE);
            _portraitAlpha.Set(0.5f, now, 0.8f, 0.3f, CCssEasing.EASE);
            _portraitGray.Set(0.7f, now, 0.8f, 0.3f, CCssEasing.EASE);
            _portraitBright.Set(0.8f, now, 0.8f, 0.3f, CCssEasing.EASE);
            _portraitGlow.Set(0f, now, 0.5f, 0f, CCssEasing.EASE);
            _veilAlpha.Set(0.55f, now, 0.9f, 0.4f, CCssEasing.EASE);
            _lineAlpha.Set(0.3f, now, 0.9f, 0.4f, CCssEasing.EASE);
        }

        else if (_isDead && _t > 0f)
        {
            Spawn(now);
        }

        if (!_isDead)
        {
            _fill.Set(_t, now, 0.18f, 0f, CCssEasing.EASE_OUT);
        }

        // 늦게 따라오는 막대: 마지막으로 맞고 0.45초 뒤부터 초당 0.35
        if (now - _hitAt > _trailDelay && _td > _t)
        {
            _td = Mathf.Max(_t, _td - Time.deltaTime * _trailSpeed);
        }

        else if (_td < _t)
        {
            _td = _t;
        }

        bool isLow = !_isDead && _t < _lowThreshold;
        _low.Set(isLow ? 1f : 0f, now, 0.5f, 0f, CCssEasing.EASE);
        _portraitShadow.Set(isLow ? 1f : 0f, now, 0.5f, 0f, CCssEasing.EASE);
    }

    private void UpdateTexts()
    {
        int hp = Mathf.RoundToInt(_t * _boss.HpMax);
        int max = Mathf.RoundToInt(_boss.HpMax);
        // 비율을 배정밀도로 계산한 뒤 정수 퍼센트로 올림합니다.
        float pct = (float)Math.Ceiling((double)_boss.HpCurrent / _boss.HpMax * 100.0);

        if (hp != _lastHp)
        {
            _lastHp = hp;
            _hpCount = CHudNumberFormat.Write(_HP_CHARS, hp);
            _hpValue.SetCharArray(_HP_CHARS, 0, _hpCount);
        }

        if (max != _lastMax)
        {
            _lastMax = max;
            _MAX_CHARS[0] = '/';
            _MAX_CHARS[1] = ' ';
            _maxCount = CHudNumberFormat.Write(_MAX_CHARS, max, 2);
            _hpMax.SetCharArray(_MAX_CHARS, 0, _maxCount);
        }

        if (!Mathf.Approximately(pct, _lastPct))
        {
            _lastPct = pct;
            _pctCount = CHudNumberFormat.Write(_PCT_CHARS, Mathf.Max(0, (int)pct));
            _PCT_CHARS[_pctCount++] = '%';
            _tagText.SetCharArray(_PCT_CHARS, 0, _pctCount);
        }
    }

    // 퍼센트 글자 폭에 맞춰 전체 폭을 계산하고 화면 중앙에 배치합니다.
    private void Layout(bool showGroggy)
    {
        SBossLayout l = _layout;
        float tagW = CHudNumberFormat.CssWidth(_tagText, _PCT_CHARS, _pctCount) + l.tagPaddingX * 2f;
        float columnW = l.barWidth + l.tagGap + tagW;
        float boxW = l.portraitSize + l.portraitGap + columnW - l.overhangRight;
        float left = -boxW * 0.5f;
        float columnX = left + l.portraitSize + l.portraitGap;
        float columnRight = columnX + columnW;

        PlaceBox(_veil.rectTransform, left - l.veilOutset.x, l.top - l.veilOutset.y,
            boxW + l.veilOutset.x + l.veilOutset.z, l.portraitSize + l.veilOutset.y + l.veilOutset.w);
        PlaceBox(_line.rectTransform, left - l.lineOutsetX, l.top + l.portraitSize + l.lineBelow - 1f, boxW +
            l.lineOutsetX * 2f, 1f);
        PlaceBox(_portrait.rectTransform, left, l.top, l.portraitSize, l.portraitSize);

        _barLeftX = columnX;
        _barBase = new Vector2(columnX, l.barTop);
        PlaceBox(_tag.rectTransform, columnX + l.barWidth + l.tagGap, l.tagTop, tagW, l.tagHeight);
        PlaceText(_tagText, columnX + l.barWidth + l.tagGap + l.tagPaddingX, l.tagBaseline);

        float nameW = CHudNumberFormat.CssWidth(_name, _name.text) - _name.characterSpacing * 0.01f * _name.fontSize;
        PlaceText(_name, columnX, l.rowBaseline);
        PlaceText(_title, columnX + nameW + l.nameGap, l.rowBaseline);

        float maxW = CHudNumberFormat.CssWidth(_hpMax, _MAX_CHARS, _maxCount);
        float valueW = CHudNumberFormat.CssWidth(_hpValue, _HP_CHARS, _hpCount);
        float labelW = CHudNumberFormat.CssWidth(_hpLabel, "HP");
        float maxX = columnRight - maxW;
        float valueX = maxX - l.valueGap - valueW;
        PlaceText(_hpMax, maxX, l.rowBaseline);
        PlaceText(_hpValue, valueX, l.rowBaseline);
        PlaceText(_hpLabel, valueX - l.valueGap - labelW, l.rowBaseline);

        float barBottom = l.barTop + l.barHeight;
        float lineBottom = l.top + l.portraitSize + l.lineBelow;
        float statusTop = Mathf.Max(barBottom, lineBottom) + 6f;
        float timerBaseline = Mathf.Max(barBottom, l.tagTop + l.tagHeight) + 20f;

        if (showGroggy)
        {
            float groggyTop = statusTop;
            float groggyBaseline = groggyTop + 26f;

            PlaceBox(_groggyBar.rectTransform, columnX, groggyTop, l.barWidth, 8f);
            PlaceText(_groggyRemainHp, columnX, groggyBaseline);
            PlaceText(_groggyRemainTime, columnX + l.barWidth - 75f, groggyBaseline);

            statusTop += 42f;
            timerBaseline = statusTop + 16f;
        }

        if (_statusRoot != null)
            PlaceBox(_statusRoot, columnX, statusTop, l.barWidth, 30f);

        if (_currentTimer != null)
            PlaceText(_currentTimer, columnRight, timerBaseline);

        if (_bestTimer != null)
            PlaceText(_bestTimer, columnRight, timerBaseline + 19f);
    }

    private void Paint(float now)
    {
        // 맞음 흔들림 (.gh)
        Vector2 shake = Vector2.zero;
        float flashAlpha = 0f;
        float flashBright = 1f;
        float critMix = 0f;
        float age = now - _hitAt;
        if (_isCritHit)
        {
            if (age < _SHAKE_CRIT_TIME) shake = CCssEasing.Keyframes(_SHAKE_CRIT_T, _SHAKE_CRIT_V, CCssEasing.EASE,
                age / _SHAKE_CRIT_TIME);
            if (age < _FLASH_CRIT_TIME)
            {
                Vector2 f = CCssEasing.Keyframes(_FLASH_CRIT_T, _FLASH_CRIT_V, CCssEasing.EASE_OUT,
                    age / _FLASH_CRIT_TIME);
                flashAlpha = f.x;
                flashBright = f.y;
                critMix = age / _FLASH_CRIT_TIME < 0.25f ? 1f - CCssEasing.Evaluate(CCssEasing.EASE_OUT,
                    age / _FLASH_CRIT_TIME / 0.25f) : 0f;
            }
        }

        else
        {
            if (age < _SHAKE_TIME) shake = CCssEasing.Keyframes(_SHAKE_T, _SHAKE_V, CCssEasing.EASE, age / _SHAKE_TIME);
            if (age < _FLASH_TIME)
            {
                Vector2 f = CCssEasing.Keyframes(_FLASH_T, _FLASH_V, CCssEasing.EASE_OUT, age / _FLASH_TIME);
                flashAlpha = f.x;
                flashBright = f.y;
            }
        }

        // 다시 나옴: 폭 0 → 값 1s cubic-bezier(.2,.7,.2,1) 지연 .15s
        float spawnAge = now - _spawnAt;
        float spawnWidth = CCssEasing.Evaluate(new Vector4(0.2f, 0.7f, 0.2f, 1f), (spawnAge - 0.15f) / 1f);
        float fadeName = CCssEasing.Evaluate(CCssEasing.EASE, (spawnAge - 0.1f) / 0.8f);
        float fadeVeil = CCssEasing.Evaluate(CCssEasing.EASE, spawnAge / 0.9f);

        float fill = _fill.Evaluate(now) * spawnWidth;
        float trail = _td * spawnWidth;
        PlaceBox(_bar.rectTransform, _barBase.x + shake.x, _barBase.y + shake.y, _layout.barWidth, _layout.barHeight);
        _bar.SetDynamic(new Vector4(fill, trail, _flashLeft, _flashRight),
            new Vector4(flashAlpha, flashBright, 0f, critMix),
            new Vector3(_phase, _glow.Evaluate(now), _low.Evaluate(now)),
            new Vector4(_trailAlpha.Evaluate(now), 0f, 0f, 0f));

        _portrait.SetDynamic(new Vector4(_portraitGlow.Evaluate(now), _portraitGray.Evaluate(now),
            _portraitBright.Evaluate(now) - 1f, _portraitShadow.Evaluate(now)),
            Vector4.zero, Vector3.zero, Vector4.zero);
        SetAlpha(_portrait, _portraitAlpha.Evaluate(now));
        SetAlpha(_veil, _veilAlpha.Evaluate(now) * fadeVeil);
        SetAlpha(_line, _lineAlpha.Evaluate(now) * fadeVeil);

        float tagAlpha = _tagAlpha.Evaluate(now);
        SetAlpha(_tag, tagAlpha);
        _tagText.alpha = tagAlpha;

        float nameAlpha = _nameAlpha.Evaluate(now);
        _name.alpha = nameAlpha * fadeName;
        _title.alpha = _titleTween.Evaluate(now) * fadeName;

        float valueAlpha = _valueAlpha.Evaluate(now);
        _hpValue.alpha = valueAlpha;
        _hpLabel.alpha = valueAlpha * _labelAlpha;
        _hpMax.alpha = valueAlpha * _labelAlpha;
    }

    private static void SetAlpha(CCssBoxGraphic g, float alpha)
    {
        Color c = g.color;
        if (Mathf.Approximately(c.a, alpha)) return;
        c.a = alpha;
        g.color = c;
    }

    // 화면 중앙 기준 x와 위쪽 기준 y를 RectTransform 로컬 좌표로 변환합니다.
    private Vector2 WebToLocal(float x, float y)
    {
        Rect r = ((RectTransform)transform).rect;
        return new Vector2(r.width * 0.5f + x, r.height - y);
    }

    private static void PlaceBox(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    // TMP 는 BaselineLeft 정렬 · 높이 0 상자 → 상자 y = 기준선
    private static void PlaceText(TMP_Text text, float x, float baseline)
    {
        text.rectTransform.anchoredPosition = new Vector2(x, -baseline);
    }

    private void UpdateStatus()
    {
        if (_statusRoot == null || _statusPrefab == null)
        {
            return;
        }

        _currentStatus.Clear();
        _removedStatus.Clear();

        if (_boss.Status != null)
        {
            for (int i = 0; i < _boss.Status.Count; i++)
            {
                CStatus target = _boss.Status[i];
                if (target == null) continue;
                _currentStatus.Add(target);
                if (_statusViews.ContainsKey(target)) continue;
                CStatusUI view = Instantiate(_statusPrefab, _statusRoot);
                //((RectTransform)view.transform).sizeDelta = new Vector2(30f, 30f);
                view.Bind(target, _tooltip);
                if (target.IsDebuff)
                {
                    view.transform.SetSiblingIndex(_debuffViewCount);
                    _debuffViewCount++;
                }

                _statusViews.Add(target, view);
            }
        }

        foreach (KeyValuePair<CStatus, CStatusUI> pair in _statusViews)
        {
            if (_currentStatus.Contains(pair.Key))
            {
                pair.Value.Refresh();
            }
            else
            {
                _removedStatus.Add(pair.Key);
            }
        }

        if (_removedStatus.Count > 0)
        {
            for (int i = 0; i < _removedStatus.Count; i++)
            {
                CStatus targetStatus = _removedStatus[i];
                CStatusUI targetUi = _statusViews[_removedStatus[i]];
                _statusViews.Remove(_removedStatus[i]);

                if (targetStatus.IsDebuff) _debuffViewCount--;

                targetUi.gameObject.SetActive(false);
                targetUi.transform.SetAsLastSibling();
                Destroy(targetUi.gameObject);
            }
        }
    }

    private void SetBestTimer()
    {
        if (_bestTimer == null) return;
        if (_boss == null || _boss.BossId == EBossClear.None || CGameManager.Instance == null ||
            !CGameManager.Instance.TryGetBossClearTime(_boss.BossId, out float bestTime))
        {
            _bestTimer.gameObject.SetActive(false);
            return;
        }

        _bestTimer.text = bestTime > 0f ? "최고 " + FormatTimer(bestTime) : "최고기록 없음";
        _bestTimer.gameObject.SetActive(true);
    }

    private void UpdateTimerText()
    {
        if (_currentTimer == null) return;
        if (_boss == null || CGameManager.Instance == null ||
            !CGameManager.Instance.TryGetBossTimer(_boss.BossId, out float currentTimer, out _))
        {
            _currentTimer.gameObject.SetActive(false);
            return;
        }

        _currentTimer.text = FormatTimer(currentTimer);
        _currentTimer.gameObject.SetActive(true);
    }

    private static string FormatTimer(float time)
    {
        int seconds = (int)(time * 100f);
        return $"{seconds / 6000:D2}:{seconds / 100 % 60:D2}<size=72%>.{seconds % 100:D2}</size>";
    }

    private bool UpdateGroggy()
    {
        if (_groggyBar == null || _groggyRemainHp == null || _groggyRemainTime == null)
            return false;

        if (!(_boss is IBoss boss) ||
            !boss.TryGetGroggyInfo(out float remainHp, out float maxHp,
                                  out float remainTime, out _))
        {
            return UpdateGroggyResult();
        }

        float ratio = Mathf.Clamp01(remainHp / maxHp);
        _groggyBar.SetDynamic(
            new Vector4(ratio, ratio, 0f, 0f),
            Vector4.zero,
            Vector3.zero,
            Vector4.zero
        );

        _groggyRemainHp.SetText($"{Mathf.CeilToInt(remainHp):0} / {Mathf.CeilToInt(maxHp):0}");
        _groggyRemainTime.SetText($"{remainTime:0.0}s");

        _wasGroggyCheckActive = true;
        _groggyHideAt = -1f;
        SetGroggyVisible(true);
        return true;
    }

    private void SetGroggyVisible(bool visible)
    {
        if (_groggyBar.gameObject.activeSelf == visible) return;
        else if (_groggyBar.gameObject.activeSelf)
        {
            _groggyBar.gameObject.SetActive(false);
            _groggyRemainHp.gameObject.SetActive(false);
            _groggyRemainTime.gameObject.SetActive(false);
        }
        else
        {
            _groggyBar.gameObject.SetActive(true);
            _groggyRemainHp.gameObject.SetActive(true);
            _groggyRemainTime.gameObject.SetActive(true);
        }
    }

    private bool UpdateGroggyResult()
    {
        bool succeeded = _boss is IBoss boss && boss.IsGroggy;

        if (_wasGroggyCheckActive)
        {
            _wasGroggyCheckActive = false;

            if (succeeded)
            {
                _groggyHideAt = Time.unscaledTime + 1.2f;

                _groggyBar.SetDynamic(
                    Vector4.zero, Vector4.zero, Vector3.zero, Vector4.zero);

                _groggyRemainHp.SetText("무력화 성공");
                _groggyRemainTime.SetText("");
            }
        }

        bool visible = succeeded && Time.unscaledTime < _groggyHideAt;
        SetGroggyVisible(visible);
        return visible;
    }
}
