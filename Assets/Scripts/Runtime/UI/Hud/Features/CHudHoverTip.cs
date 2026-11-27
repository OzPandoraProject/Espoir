using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 스킬 칸에 마우스를 올리면 이름표를, 길게 누르면 상세 설명 창을 표시합니다.
/// 선택한 칸의 위치와 아이콘 크기도 함께 전환합니다.
/// </summary>
[DefaultExecutionOrder(265)]
public class CHudHoverTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    /// <summary>
    /// 슬롯 이름표에 표시할 내용의 종류입니다.
    /// </summary>
    public enum EKind
    {
        Skill = 0,
        Fixed = 1,
        Quickslot = 2,
    }

    [SerializeField] private CHudTipView _view;
    [SerializeField] private CTooltipUI _tooltip;
    [SerializeField] private CHudSkillTipView _detail;
    [SerializeField] private string _keyLabel;
    [SerializeField] private CPlayerHudView _hudView;
    [SerializeField] private EKind _kind;
    [SerializeField] private ESkillType _skillType;
    [SerializeField] private string _fixedText;
    [SerializeField] private string _emptyText = "비어 있음";
    [SerializeField] private float _holdTime = 0.35f;

    [Header("마우스 반응")]
    [SerializeField] private CHudBox _box;
    [SerializeField] private CHudSlotStyle _style;
    [SerializeField] private RectTransform _icon;
    [SerializeField] private CHudCastPress _press;
    [SerializeField] private bool _hasHoverShadow;
    [SerializeField] private float _lift = 3f;
    [SerializeField] private float _iconScale = 1.12f;
    [SerializeField] private Color _hoverFill = new Color(120f / 255f, 126f / 255f, 170f / 255f, 0.34f);

    private CSkillUI _skillUI;
    private CQuickslotUI _quickUI;
    private bool _isOver;
    private bool _isDown;
    private bool _isHolding;
    private float _downAt;
    private float _liftApplied;
    private readonly float[] _FROM = new float[3];
    private readonly float[] _TO = new float[3];
    private readonly float[] _START = { -10f, -10f, -10f };

    /// <summary>
    /// 슬롯의 상세 설명을 표시 중인지 반환합니다.
    /// </summary>
    public bool IsHolding => _isHolding;

    private void Awake()
    {
        _skillUI = GetComponent<CSkillUI>();
        _quickUI = GetComponent<CQuickslotUI>();
    }

    /// <summary>
    /// 가리킨 슬롯의 이름표와 강조를 표시합니다.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        _isOver = true;
        SetHoverTarget(1f);
        if (!_isHolding && _view != null) _view.Show((RectTransform)transform, BuildText());
    }

    /// <summary>
    /// 슬롯 강조와 이름표를 숨깁니다.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        _isOver = false;
        SetHoverTarget(0f);
        if (_view != null) _view.Hide((RectTransform)transform);
    }

    /// <summary>
    /// 상세 설명을 열기 위한 누름 시간을 기록합니다.
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        _isDown = true;
        _downAt = Time.unscaledTime;
    }

    /// <summary>
    /// 상세 설명을 닫고 이름표로 돌아갑니다.
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        _isDown = false;
        EndHold();
        if (_isOver && _view != null) _view.Show((RectTransform)transform, BuildText());
    }

    private void SetHoverTarget(float to)
    {
        float now = Time.unscaledTime;
        for (int i = 0; i < 3; i++)
        {
            _FROM[i] = GetHoverWeight(i, now);
            _TO[i] = to;
            _START[i] = now;
        }
    }

    private float GetHoverWeight(int i, float now)
    {
        float dur = i == 2 ? 0.25f : 0.2f;
        float k = Mathf.Clamp01((now - _START[i]) / dur);
        float e = i == 0 ? CHudEase.Bezier(0.2f, 0.9f, 0.3f, 1.3f, k) : CHudEase.Ease(k);
        return Mathf.LerpUnclamped(_FROM[i], _TO[i], e);
    }

    private void LateUpdate()
    {
        float now = Time.unscaledTime;
        RectTransform slot = (RectTransform)transform;
        float lift = _lift * GetHoverWeight(0, now);
        slot.anchoredPosition += new Vector2(0f, lift);
        _liftApplied = lift;

        float h = Mathf.Clamp01(GetHoverWeight(1, now));
        float crimson = _style != null ? _style.CrimsonWeight : 0f;
        float w = h * (1f - crimson);
        if (_box != null && w > 0f)
        {
            bool isEmpty = _quickUI != null && !_quickUI.HasItem;
            if (!isEmpty) _box.Fill = LerpPremul(_box.Fill, _hoverFill, w);
            if (_hasHoverShadow)
            {
                _box.SetShadow(20f * w, new Color(1f, 1f, 1f, 0.35f * w));
                _box.SetInset(14f * w, new Color(1f, 1f, 1f, 0.16f * w));
            }
        }

        float si = Mathf.LerpUnclamped(1f, _iconScale, GetHoverWeight(2, now));
        if (_press != null) _press.IconHoverScale = si;
        if (_icon != null && (_press == null || !_press.IsPlaying)) _icon.localScale = Vector3.one * si;
    }

    private static Color LerpPremul(Color a, Color b, float t)
    {
        float alpha = Mathf.Lerp(a.a, b.a, t);
        if (alpha <= 1e-5f) return new Color(b.r, b.g, b.b, 0f);

        return new Color(Mathf.Lerp(a.r * a.a, b.r * b.a, t) / alpha,
            Mathf.Lerp(a.g * a.a, b.g * b.a, t) / alpha,
            Mathf.Lerp(a.b * a.a, b.b * b.a, t) / alpha, alpha);
    }

    private void Update()
    {
        // 다른 HUD가 위치를 갱신하기 전에 이전 프레임의 강조 이동량을 되돌린다.
        if (_liftApplied != 0f)
        {
            ((RectTransform)transform).anchoredPosition -= new Vector2(0f, _liftApplied);
            _liftApplied = 0f;
        }

        if (!_isDown || _isHolding || Time.unscaledTime - _downAt < _holdTime) return;

        _isHolding = true;
        if (_view != null) _view.Hide((RectTransform)transform);
        if (_detail != null)
        {
            ShowDetail();
            return;
        }

        if (_tooltip == null) return;

        if (_skillUI != null) _tooltip.Enable(_skillUI);
        else if (_quickUI != null && _quickUI.HasItem) _tooltip.Enable(_quickUI);
    }

    private void ShowDetail()
    {
        RectTransform slot = (RectTransform)transform;
        if (_skillUI != null) _detail.Show(slot, _keyLabel, _skillUI.Name, _skillUI.Description, false);
        else if (_quickUI != null && _quickUI.HasItem)
        {
            _detail.Show(slot, _keyLabel, _quickUI.Name, _quickUI.Description, true);
        }
    }

    private void OnDisable()
    {
        _isOver = false;
        _isDown = false;
        EndHold();
        if (_view != null) _view.Hide((RectTransform)transform);

        ((RectTransform)transform).anchoredPosition -= new Vector2(0f, _liftApplied);
        _liftApplied = 0f;
        for (int i = 0; i < _FROM.Length; i++)
        {
            _FROM[i] = 0f;
            _TO[i] = 0f;
        }

        if (_press != null) _press.IconHoverScale = 1f;
        if (_icon != null && (_press == null || !_press.IsPlaying)) _icon.localScale = Vector3.one;
    }

    private void EndHold()
    {
        if (!_isHolding) return;

        _isHolding = false;
        if (_detail != null) _detail.Hide((RectTransform)transform);
        else if (_tooltip != null) _tooltip.Disable();
    }

    private string BuildText()
    {
        switch (_kind)
        {
            case EKind.Fixed:
                return _fixedText;
            case EKind.Quickslot:
            {
                string slotName = _quickUI != null ? _quickUI.Name : "";
                return string.IsNullOrEmpty(slotName) ? _emptyText : slotName;
            }
            default:
            {
                string slotName = _skillUI != null ? _skillUI.Name : "";
                CSkillSO skill = FindSkill();
                if (skill == null) return slotName;

                string cooldown = skill.Cooldown.ToString("0.##", CultureInfo.InvariantCulture);
                string cost = skill.MpCost.ToString(CultureInfo.InvariantCulture);
                return slotName + " · MP " + cost + " · " + cooldown + "초";
            }
        }
    }

    private CSkillSO FindSkill()
    {
        if (_hudView == null || _hudView.Player == null) return null;

        CPlayerSkill skill = _hudView.Player.GetComponent<CPlayerSkill>();
        if (skill == null) return null;

        foreach (CSkillSO settings in skill.RegisteredSkills)
        {
            if (settings != null && settings.Type == _skillType) return settings;
        }

        return null;
    }
}
