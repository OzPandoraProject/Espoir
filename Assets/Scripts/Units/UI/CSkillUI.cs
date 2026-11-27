using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CSkillUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TMP_Text _cooldownText;
    [SerializeField] private Image _durationFill;
    [SerializeField] private Image _skillIcon;
    [SerializeField] private string _name;
    [SerializeField] private CTooltipUI _tooltipUI;
    [SerializeField][TextArea(1, 5)] private string _tooltipText;

    [SerializeField] private bool _showingAvailable;
    [Tooltip("켜면 사용 불가 아이콘을 붉게 칠하지 않고 투명도만 낮춥니다.")]
    [SerializeField] private bool _useAlphaForUnavailable = false;
    [SerializeField, Range(0f, 1f)] private float _unavailableAlpha = 0.5f;
    [Tooltip("켜면 남은 쿨타임을 1초 이상은 올림 정수, 1초 미만만 소수 1자리로 씁니다.")]
    [SerializeField] private bool _useWholeSeconds = false;
    [Tooltip("켜면 마우스를 올려도 설명을 띄우지 않습니다(꾹 누름 등 다른 컴포넌트가 Tooltip UI 로 띄움).")]
    [SerializeField] private bool _tooltipOnHold = false;

    private static readonly Regex _attackToken = new Regex(
        @"(?<![A-Za-z0-9_.])([0-9]+(?:\.[0-9]+)?)attack(?![A-Za-z0-9_])");

    private const string _DamageColor = "#FFA64D";
    private CPlayer _player;

    public string Name => _name;
    public string Description => _player != null ? ReplaceRealDamage(_tooltipText, _player.Attack) : _tooltipText;
    public string Tooltip => $"{Name}\n<size=75%>{Description}</size>";

    public string TooltipText
    {
        get { return _tooltipText; }
        set { _tooltipText = value; }
    }

    private void Start()
    {
        _player = FindAnyObjectByType<CPlayer>();
    }

    public void Refresh(float ratio, float remainCooldown, bool enableUse = true)
    {
        _durationFill.fillAmount = Mathf.Clamp01(ratio);

        if (remainCooldown <= 0) _cooldownText.gameObject.SetActive(false);
        else
        {
            _cooldownText.text = _useWholeSeconds && remainCooldown >= 1f
                ? Mathf.CeilToInt(remainCooldown).ToString()
                : remainCooldown.ToString("F1");
            _cooldownText.gameObject.SetActive(true);
        }

        if (enableUse || !_showingAvailable)
        {
            _skillIcon.color = new Color(1f, 1f, 1f, 1f);
        }
        else if (_useAlphaForUnavailable)
        {
            _skillIcon.color = new Color(1f, 1f, 1f, _unavailableAlpha);
        }
        else
        {
            _skillIcon.color = new Color(0.5f, 0f, 0f, 1f);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_tooltipOnHold) return;
        _tooltipUI.Enable(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _tooltipUI.Disable();
    }

    private static string ReplaceRealDamage(string text, float attack)
    {
        if (string.IsNullOrEmpty(text)) return text;

        return _attackToken.Replace(text, match =>
        {
            if (!float.TryParse(match.Groups[1].Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out float multiplier))
            {
                return match.Value;
            }

            float damage = attack * multiplier;
            return $"<color={_DamageColor}>{damage:N0}</color>";
        });
    }
}
