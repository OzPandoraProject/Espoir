using TMPro;
using UnityEngine;

/// <summary>
/// 가모리 등장 연출의 비네트와 보스 이름을 표시합니다.
/// </summary>
public sealed class CGamoriEntranceView : MonoBehaviour
{
    private static readonly Vector4 _EASE_OUT = new Vector4(0.2f, 0.7f, 0.2f, 1f);
    private static readonly Color _HALO = new Color(0.039f, 0.016f, 0.047f, 0.95f);
    private static readonly Color _GLOW = new Color(1f, 0.353f, 0.51f, 0.8f);
    // 폰트 아틀라스의 글리프 경계가 보이지 않도록 번짐 폭을 제한한다.
    private const float _HALO_DILATE = 0.18f;
    private const float _HALO_SOFTNESS = 0.3f;
    private const float _GLOW_DILATE = 0.2f;
    private const float _GLOW_SOFTNESS = 0.38f;

    [SerializeField] private CanvasGroup _vignette;
    [SerializeField] private CanvasGroup _card;
    [SerializeField] private CanvasGroup _veil;
    [SerializeField] private RectTransform _title;
    [SerializeField] private CanvasGroup _titleGroup;
    [SerializeField] private TMP_Text[] _letters;
    [SerializeField] private TMP_Text[] _leftGhosts;
    [SerializeField] private TMP_Text[] _rightGhosts;

    [Header("표시 시간 및 거리 (초, px)")]
    [SerializeField] private float _vignetteFade = 0.9f;
    [SerializeField] private float _cardTime = 3.6f;
    [SerializeField, Range(0f, 1f)] private float _cardFadeFrom = 0.84f;
    [SerializeField, Range(0f, 1f)] private float _veilFadeUntil = 0.14f;
    [SerializeField] private float _titleDelay = 0.8f;
    [SerializeField] private float _titleDuration = 0.9f;
    [SerializeField] private float _titleRise = 8f;
    [SerializeField] private float _letterDelay = 0.3f;
    [SerializeField] private float _letterStagger = 0.1f;
    [SerializeField] private float _letterDuration = 1.25f;
    [SerializeField] private float _ghostSpread = 70f;
    [SerializeField] private float _ghostNear = 3f;
    [SerializeField] private Color _leftColor = new Color(1f, 0.275f, 0.431f, 0.75f);
    [SerializeField] private Color _rightColor = new Color(1f, 0.745f, 0.627f, 0.6f);

    private Material[] _letterMaterials;
    private Vector2 _titleHome;
    private float _vignetteTarget;
    private float _cardStart = -1f;

    private void Awake()
    {
        _titleHome = _title.anchoredPosition;
        _vignette.alpha = 0f;
        _card.alpha = 0f;
        foreach (TMP_Text title in _title.GetComponentsInChildren<TMP_Text>(true))
            Shade(title, _HALO, _HALO_DILATE, _HALO_SOFTNESS);
        _letterMaterials = new Material[_letters.Length];
        for (int i = 0; i < _letters.Length; i++)
        {
            _letterMaterials[i] = Shade(_letters[i], _HALO, _HALO_DILATE, _HALO_SOFTNESS);
        }
    }

    private static Material Shade(TMP_Text text, Color color, float dilate, float softness)
    {
        Material material = text.fontMaterial;
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.1f);
        Tint(material, color, dilate, softness);
        text.extraPadding = true;
        text.UpdateMeshPadding();
        return material;
    }

    private static void Tint(Material material, Color color, float dilate, float softness)
    {
        material.SetColor(ShaderUtilities.ID_UnderlayColor, color);
        material.SetFloat(ShaderUtilities.ID_UnderlayDilate, dilate);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, softness);
    }

    /// <summary>
    /// 등장 연출의 비네트를 전환합니다.
    /// </summary>
    public void SetVignette(bool isOn)
    {
        _vignetteTarget = isOn ? 1f : 0f;
    }

    /// <summary>
    /// 이름 카드를 처음부터 띄웁니다.
    /// </summary>
    public void PlayCard()
    {
        _cardStart = Time.time;
        ApplyCard(0f);
    }

    private void Update()
    {
        _vignette.alpha = Mathf.MoveTowards(_vignette.alpha, _vignetteTarget, Time.deltaTime / _vignetteFade);
        if (_cardStart < 0f) return;

        float time = Time.time - _cardStart;
        if (time >= _cardTime)
        {
            _cardStart = -1f;
            _card.alpha = 0f;
            return;
        }
        ApplyCard(time);
    }

    private void ApplyCard(float time)
    {
        float whole = time / _cardTime;
        _card.alpha = 1f - Mathf.Clamp01((whole - _cardFadeFrom) / (1f - _cardFadeFrom));
        _veil.alpha = CCssEase.Evaluate(CCssEase.EASE, Mathf.Clamp01(whole / _veilFadeUntil));

        float title = CCssEase.Evaluate(CCssEase.EASE, Mathf.Clamp01((time - _titleDelay) / _titleDuration));
        _titleGroup.alpha = title;
        _title.anchoredPosition = _titleHome - new Vector2(0f, _titleRise * (1f - title));

        for (int i = 0; i < _letters.Length; i++)
        {
            float k = CCssEase.Evaluate(_EASE_OUT,
                Mathf.Clamp01((time - _letterDelay - _letterStagger * i) / _letterDuration));
            float alpha = k < 0.35f ? Mathf.Lerp(0f, 0.9f, k / 0.35f) : Mathf.Lerp(0.9f,
                1f, Mathf.Clamp01((k - 0.35f) / 0.35f));
            float gather = Mathf.Clamp01(k / 0.7f), settle = Mathf.Clamp01((k - 0.7f) / 0.3f);
            float offset = Mathf.Lerp(Mathf.Lerp(_ghostSpread, _ghostNear, gather), 0f, settle);
            _letters[i].alpha = alpha;
            Color glow = _GLOW;
            glow.a *= gather;
            Tint(_letterMaterials[i], Color.Lerp(glow, _HALO, settle), Mathf.Lerp(_GLOW_DILATE, _HALO_DILATE, settle),
                Mathf.Lerp(_GLOW_SOFTNESS, _HALO_SOFTNESS, settle));
            Place(_leftGhosts[i], _letters[i], -offset, _leftColor, alpha * (1f - settle));
            Place(_rightGhosts[i], _letters[i], offset, _rightColor, alpha * (1f - settle));
        }
    }

    private static void Place(TMP_Text ghost, TMP_Text letter, float offset, Color color, float alpha)
    {
        ghost.rectTransform.anchoredPosition = letter.rectTransform.anchoredPosition + new Vector2(offset, 0f);
        color.a *= alpha;
        ghost.color = color;
    }
}
