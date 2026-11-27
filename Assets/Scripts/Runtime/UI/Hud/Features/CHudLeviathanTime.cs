using System.Globalization;
using TMPro;
using UnityEngine;

[DefaultExecutionOrder(262)]
public class CHudLeviathanTime : MonoBehaviour
{
    [SerializeField] private CPlayerHudView _hudView;
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private RectTransform _box;
    [SerializeField] private TMP_Text _valueText;
    [SerializeField] private RectTransform _fill;

    [Header("표시 전환")]
    [SerializeField] private float _showTime = 0.35f;
    [SerializeField] private float _hiddenOffset = 5f;

    [Header("종료 임박 표시")]
    [SerializeField] private float _lowTime = 5f;
    [SerializeField] private float _lowBlinkTime = 0.5f;
    [SerializeField] private Color _valueColor = new Color(1f, 210f / 255f, 122f / 255f);
    [SerializeField] private Color _lowColor = new Color(1f, 158f / 255f, 168f / 255f);

    private CPlayer _player;
    private CPlayerLeviathan _leviathan;
    private float _restY;
    private float _fillWidth;
    private float _shownAt;
    private bool _isShown;
    private int _shownSeconds = -1;

    private void Awake()
    {
        if (_box != null) _restY = _box.anchoredPosition.y;
        if (_fill != null) _fillWidth = ((RectTransform)_fill.parent).rect.width;
        if (_group != null) _group.alpha = 0f;
    }

    private void LateUpdate()
    {
        if (_leviathan == null && !TryConnect()) return;

        float now = Time.unscaledTime;
        bool show = _leviathan.State == ELeviathanState.Buff;
        if (show != _isShown)
        {
            _isShown = show;
            _shownAt = now;
            _shownSeconds = -1;
        }

        if (!show)
        {
            if (_group != null) _group.alpha = 0f;
            return;
        }

        // 버프 지속 시간에는 마무리 공격 시간도 포함된다.
        CLeviathanSkillSO settings = _leviathan.Settings;
        float total = settings.FinisherDelay;
        CStatus buff = _player.FindStatus(settings.BuffStatus.Id);
        float remain = buff != null ? buff.Duration - (settings.BuffStatus.Duration - total) : 0f;
        remain = Mathf.Clamp(remain, 0f, total);

        float elapsed = now - _shownAt;
        float alpha = _showTime > 0f ? CHudEase.Ease(Mathf.Clamp01(elapsed / _showTime)) : 1f;
        if (_group != null) _group.alpha = alpha;
        if (_box != null)
        {
            _box.anchoredPosition = new Vector2(_box.anchoredPosition.x, _restY - _hiddenOffset * (1f - alpha));
        }

        if (_fill != null)
        {
            float ratio = total > 0f ? remain / total : 0f;
            _fill.sizeDelta = new Vector2(_fillWidth * ratio, _fill.sizeDelta.y);
        }

        if (_valueText == null) return;

        int seconds = Mathf.CeilToInt(remain);
        if (seconds != _shownSeconds)
        {
            _shownSeconds = seconds;
            _valueText.text = seconds.ToString(CultureInfo.InvariantCulture);
        }

        float blink = remain <= _lowTime && _lowBlinkTime > 0f ? Mathf.PingPong(elapsed / _lowBlinkTime, 1f) : 0f;
        _valueText.color = Color.Lerp(_valueColor, _lowColor, CHudEase.Ease(blink));
    }

    private bool TryConnect()
    {
        if (_hudView == null || _hudView.Player == null) return false;

        CPlayerSkill skill = _hudView.Player.GetComponent<CPlayerSkill>();
        if (skill == null || skill.Leviathan.Settings == null) return false;

        _player = _hudView.Player;
        _leviathan = skill.Leviathan;
        return true;
    }
}
