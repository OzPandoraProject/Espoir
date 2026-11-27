using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보스 연출 중 화면 가장자리와 이름판을 표시합니다.
/// </summary>
public class CBossSummonTitleView : MonoBehaviour
{
    private static readonly Vector4 _LINE_EASE = new Vector4(0.2f, 0.7f, 0.2f, 1f);

    [SerializeField] private CanvasGroup _vignette;
    [SerializeField] private RectTransform _card;
    [SerializeField] private CanvasGroup _cardGroup;
    [SerializeField] private CanvasGroup _veil;
    [SerializeField] private RectTransform _veilRect;
    [SerializeField] private RectMask2D _titleMask;
    [SerializeField] private RectMask2D _nameMask;
    [SerializeField] private RectTransform _line;
    [SerializeField] private CanvasGroup _lineGroup;

    [Header("연출 시간 (초)")]
    [SerializeField] private float _totalTime = 5.8f;
    [SerializeField] private float _vignetteTime = 5.6f;
    [SerializeField] private float _veilDelay = 2f;
    [SerializeField] private float _veilDuration = 0.8f;
    [SerializeField] private float _titleDelay = 2.15f;
    [SerializeField] private float _titleDuration = 1f;
    [SerializeField] private float _nameDelay = 2.4f;
    [SerializeField] private float _nameDuration = 1.3f;
    [SerializeField] private float _lineDelay = 2.8f;
    [SerializeField] private float _lineDuration = 1.2f;
    [SerializeField] private float _outDelay = 5f;
    [SerializeField] private float _outDuration = 0.6f;
    [SerializeField] private float _outOffset = 14f;

    [Header("자리 (화면 비율 · px)")]
    [SerializeField] private float _left = 0.07f;
    [SerializeField] private float _top = 0.3f;

    private float _startTime = -1f;
    private float _playDuration;

    /// <summary>
    /// 소환 연출 중 HUD, 이름표와 상호작용 표시를 숨길 때 사용합니다.
    /// </summary>
    public bool IsPlaying => _startTime >= 0f;

    /// <summary>
    /// 지금 재생 시각 (확인용, 재생 중이 아니면 -1).
    /// </summary>
    public float Elapsed => _startTime >= 0f ? Time.unscaledTime - _startTime : -1f;

    private void Awake()
    {
        if (_startTime < 0f) gameObject.SetActive(false);
    }

    /// <summary>
    /// 처음부터 다시 재생합니다.
    /// </summary>
    public void Play()
    {
        Play(_totalTime);
    }

    /// <summary>
    /// 지정한 시간 동안 타이틀을 재생합니다.
    /// </summary>
    public void Play(float duration)
    {
        _playDuration = duration;
        _startTime = Time.unscaledTime;
        gameObject.SetActive(true);
        Apply(0f);
    }

    private void Update()
    {
        if (_startTime < 0f) return;

        float t = Time.unscaledTime - _startTime;

        if (t >= _playDuration)
        {
            Stop();
            return;
        }

        Apply(t);
    }

    internal void Stop()
    {
        _startTime = -1f;
        gameObject.SetActive(false);
    }

    private void Apply(float t)
    {
        float v = t / _vignetteTime;
        float vignette;

        if (v < 0.15f) vignette = CCssEase.Evaluate(CCssEase.EASE, v / 0.15f);
        else if (v < 0.8f) vignette = 1f;
        else vignette = 1f - CCssEase.Evaluate(CCssEase.EASE, Mathf.Clamp01((v - 0.8f) / 0.2f));

        _vignette.alpha = vignette;

        RectTransform parent = (RectTransform)_card.parent;
        float outK = CCssEase.Evaluate(CCssEase.EASE_IN, Mathf.Clamp01((t - _outDelay) / _outDuration));
        _cardGroup.alpha = 1f - outK;
        float cardLeft = parent.rect.width * _left;
        _card.anchoredPosition = new Vector2(cardLeft - outK * _outOffset, -parent.rect.height * _top);
        _veilRect.anchoredPosition = new Vector2(-cardLeft, 40f);

        _veil.alpha = CCssEase.Evaluate(CCssEase.EASE_OUT, Mathf.Clamp01((t - _veilDelay) / _veilDuration));
        Wipe(_titleMask, CCssEase.Evaluate(CCssEase.EASE_OUT, Mathf.Clamp01((t - _titleDelay) / _titleDuration)));
        Wipe(_nameMask, CCssEase.Evaluate(CCssEase.EASE_OUT, Mathf.Clamp01((t - _nameDelay) / _nameDuration)));

        float lineK = CCssEase.Evaluate(_LINE_EASE, Mathf.Clamp01((t - _lineDelay) / _lineDuration));
        _lineGroup.alpha = lineK;
        _line.localScale = new Vector3(lineK, 1f, 1f);
    }

    private static void Wipe(RectMask2D mask, float progress)
    {
        float width = mask.rectTransform.rect.width;
        float position = 1f - progress;
        float edge = 1.43f * width - 1.6f * width * position;
        int softness = Mathf.Max(1, Mathf.RoundToInt(0.26f * width));
        // 글자가 나타날 때 왼쪽 가장자리의 번짐이 잘리지 않도록 여백을 둔다.
        mask.softness = new Vector2Int(softness, 0);
        mask.padding = new Vector4(-softness - 8f, -40f, width - edge, -40f);
    }
}
