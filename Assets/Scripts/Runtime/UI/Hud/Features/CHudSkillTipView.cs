using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(271)]
public class CHudSkillTipView : MonoBehaviour
{
    private class CRow
    {
        public TMP_Text key;
        public TMP_Text value;
        public TMP_Text note;
        public bool isLongNote;
    }

    [SerializeField] private RectTransform _box;
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private RectTransform _content;
    [SerializeField] private CHudBox _keyBox;
    [SerializeField] private TMP_Text _keyText;
    [SerializeField] private TMP_Text _nameText;

    [Header("글꼴")]
    [SerializeField] private TMP_FontAsset _sans;
    [SerializeField] private TMP_FontAsset _sansMedium;
    [SerializeField] private TMP_FontAsset _sansSemiBold;
    [SerializeField] private TMP_FontAsset _number;

    [Header("배치")]
    [SerializeField] private float _width = 430f;
    [SerializeField] private float _minWidth = 400f;
    [SerializeField] private Vector2 _padding = new Vector2(19f, 17f);
    [SerializeField] private float _paddingBottom = 16f;
    [SerializeField] private float _slotGap = 34f;
    [SerializeField] private float _screenMargin = 12f;
    [SerializeField] private float _transition = 0.12f;

    [Header("색상")]
    [SerializeField] private Color _tagFill = new Color(1f, 240f / 255f, 220f / 255f);
    [SerializeField] private Color _tagText = new Color(24f / 255f, 26f / 255f, 44f / 255f);
    [SerializeField] private Color _plainTagFill = new Color(1f, 240f / 255f, 220f / 255f, 0.14f);
    [SerializeField] private Color _cream = new Color(1f, 228f / 255f, 196f / 255f);
    [SerializeField] private Color _blockTitle = new Color(1f, 192f / 255f, 112f / 255f);
    [SerializeField] private Color _valueText = new Color(1f, 246f / 255f, 232f / 255f);
    [SerializeField] private Color _bodyText = new Color(1f, 244f / 255f, 232f / 255f, 0.8f);
    [SerializeField] private Color _soft = new Color(1f, 240f / 255f, 220f / 255f);
    [SerializeField] private Color _line = new Color(1f, 1f, 1f, 0.16f);

    private const float _HEAD_HEIGHT = 28f;
    private const float _TAG_HEIGHT = 20.5f;
    private const float _SUMMARY_GAP = 48f;
    private const float _COLUMN_GAP = 16f;
    private const float _ROW_HEIGHT = 21.5f;
    private const float _ROW_GAP = 5f;
    private const float _NOTE_HEIGHT = 17.4f;
    private const float _BLOCK_HEAD = 16.25f;
    private const float _TEXT_LINE = 20.25f;
    private const int _LONG_NOTE = 12;

    private static readonly Regex _DAMAGE = new Regex(@"^<color=[^>]+>-?\d[^<>]*</color>$");
    private static readonly Regex _COOLDOWN = new Regex(@"^재사용 대기시간 (.+?)(?: \((.+)\))?$");
    private static readonly Regex _UNIT = new Regex(@"^([0-9.,]+)(\D.*)$");

    private readonly List<TMP_Text> _TEXTS = new List<TMP_Text>();
    private readonly List<Image> _IMAGES = new List<Image>();
    private readonly List<CRow> _ROWS = new List<CRow>();
    private readonly List<string> _DAMAGE_NAMES = new List<string>();
    private readonly List<string> _DAMAGE_VALUES = new List<string>();
    private readonly Vector3[] _CORNERS = new Vector3[4];
    private int _usedTexts;
    private int _usedImages;
    private RectTransform _target;
    private float _from;
    private float _to;
    private float _start;

    private void Awake()
    {
        if (_group != null) _group.alpha = 0f;
    }

    /// <summary>
    /// 슬롯 위에 상세 설명을 표시합니다.
    /// </summary>
    public void Show(RectTransform slot, string keyLabel, string title, string text, bool isFixedWidth)
    {
        _target = slot;
        BuildContent(keyLabel, title, text, isFixedWidth);
        FadeTo(1f);
    }

    /// <summary>
    /// 해당 슬롯의 상세 설명을 닫습니다.
    /// </summary>
    public void Hide(RectTransform slot)
    {
        if (_target != slot) return;

        FadeTo(0f);
    }

    private void FadeTo(float to)
    {
        if (Mathf.Approximately(_to, to)) return;

        float now = Time.unscaledTime;
        _from = GetAlpha(now);
        _to = to;
        _start = now;
    }

    private float GetAlpha(float now)
    {
        if (_transition <= 0f) return _to;

        return Mathf.Lerp(_from, _to, (now - _start) / _transition);
    }

    private void LateUpdate()
    {
        float alpha = GetAlpha(Time.unscaledTime);
        if (_group != null) _group.alpha = alpha;
        if (alpha > 0f && _target != null) UpdatePosition();
    }

    private void UpdatePosition()
    {
        _target.GetWorldCorners(_CORNERS);
        RectTransform parent = (RectTransform)_box.parent;
        Vector3 a = parent.InverseTransformPoint(_CORNERS[1]);
        Vector3 b = parent.InverseTransformPoint(_CORNERS[2]);
        float half = _box.sizeDelta.x * 0.5f;
        float limit = parent.rect.width * 0.5f - _screenMargin - half;
        float x = Mathf.Clamp((a.x + b.x) * 0.5f, parent.rect.center.x - limit, parent.rect.center.x + limit);

        Vector2 anchorOffset = (Vector2)_box.localPosition - _box.anchoredPosition;
        _box.anchoredPosition = new Vector2(x, a.y + _slotGap) - anchorOffset;
    }

    private void BuildContent(string keyLabel, string title, string text, bool isFixedWidth)
    {
        _usedTexts = 0;
        _usedImages = 0;
        _ROWS.Clear();
        _DAMAGE_NAMES.Clear();
        _DAMAGE_VALUES.Clear();

        string[] lines = (text ?? "").Replace("\r", "").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].Trim();
        }

        float x = _padding.x;
        float y = _padding.y;
        float inner = _width - 2f * _padding.x;

        _keyText.text = keyLabel;
        float keyWidth = GetTextWidth(_keyText) + 16f;
        RectTransform keyRect = (RectTransform)_keyBox.transform;
        keyRect.anchoredPosition = new Vector2(x, -(y + 4f));
        keyRect.sizeDelta = new Vector2(keyWidth, 22f);
        _nameText.text = title;
        ((RectTransform)_nameText.transform).anchoredPosition = new Vector2(x + keyWidth + 10f, -y);
        float used = keyWidth + 10f + GetTextWidth(_nameText);
        y += _HEAD_HEIGHT;

        int first = 0;
        if (lines.Length > 0 && IsHeader(lines[0]))
        {
            y += 8f;
            used = Mathf.Max(used, BuildTags(lines[0], x, y));
            y += _TAG_HEIGHT;
            first = 1;
        }

        for (int i = first; i < lines.Length; i++)
        {
            if (!lines[i].StartsWith("#") && lines[i].Contains("|")) AddRow(lines[i]);
        }

        if (_DAMAGE_VALUES.Count > 0)
        {
            BuildSummary(x, y + 12f, inner);
            y += 76f;
        }

        y += 11f;
        y = BuildBody(lines, first, x, y, inner);

        if (_ROWS.Count == 0 && !isFixedWidth)
        {
            float widest = 0f;
            for (int i = 0; i < _usedTexts; i++)
            {
                RectTransform rt = (RectTransform)_TEXTS[i].transform;
                widest = Mathf.Max(widest, rt.anchoredPosition.x + rt.sizeDelta.x - x);
            }

            inner = Mathf.Clamp(Mathf.Max(used, widest), _minWidth - 2f * _padding.x, inner);
        }

        _box.sizeDelta = new Vector2(inner + 2f * _padding.x, y + _paddingBottom);
        for (int i = _usedTexts; i < _TEXTS.Count; i++) _TEXTS[i].gameObject.SetActive(false);
        for (int i = _usedImages; i < _IMAGES.Count; i++) _IMAGES[i].gameObject.SetActive(false);
    }

    private static bool IsHeader(string line)
    {
        return line.StartsWith("MP ") || line.StartsWith("재사용 대기시간") || line.StartsWith("(");
    }

    private float BuildTags(string head, float x, float y)
    {
        float start = x;
        if (head.StartsWith("(") && head.EndsWith(")")) head = head.Substring(1, head.Length - 2);
        string[] parts = head.Split(new[] { " / " }, System.StringSplitOptions.None);
        foreach (string part in parts)
        {
            Match cooldown = _COOLDOWN.Match(part);
            if (part.StartsWith("MP ")) x += AddTag("MP", part.Substring(3), x, y) + 5f;
            else if (cooldown.Success)
            {
                x += AddTag("재사용", cooldown.Groups[1].Value, x, y) + 5f;
                if (cooldown.Groups[2].Success) x += AddTag("", cooldown.Groups[2].Value, x, y) + 5f;
            }
            else x += AddTag("", part, x, y) + 5f;
        }

        return x - 5f - start;
    }

    private float AddTag(string label, string value, float x, float y)
    {
        Image back = AddImage(string.IsNullOrEmpty(label) ? _plainTagFill : _tagFill);
        float w = 7f;
        if (string.IsNullOrEmpty(label))
        {
            TMP_Text plain = AddText(_sansMedium, 12f, _cream, value, x + w, y, _TAG_HEIGHT);
            w += GetTextWidth(plain) + 7f;
        }
        else
        {
            Color faint = new Color(_tagText.r, _tagText.g, _tagText.b, 0.7f);
            TMP_Text name = AddText(_sansMedium, 11f, faint, label, x + w, y + 1f, _TAG_HEIGHT, 6f);
            w += GetTextWidth(name) + 5f;

            Match unit = _UNIT.Match(value);
            string number = unit.Success ? unit.Groups[1].Value : value;
            TMP_Text main = AddText(_number, 13.5f, _tagText, number, x + w, y + 0.5f, _TAG_HEIGHT, 2f);
            w += GetTextWidth(main);
            if (unit.Success)
            {
                Color soft = new Color(_tagText.r, _tagText.g, _tagText.b, 0.8f);
                TMP_Text tail = AddText(_sansMedium, 11f, soft, unit.Groups[2].Value,
                    x + w + 2f, y + 0.5f, _TAG_HEIGHT);
                w += 2f + GetTextWidth(tail);
            }

            w += 7f;
        }

        SetRect(back.rectTransform, x, y, w, _TAG_HEIGHT);
        return w;
    }

    private void BuildSummary(float x, float y, float width)
    {
        float start = x;
        Color label = new Color(_cream.r, _cream.g, _cream.b, 0.65f);
        int count = Mathf.Min(3, _DAMAGE_VALUES.Count);
        for (int i = 0; i < count; i++)
        {
            TMP_Text name = AddText(_sansMedium, 11f, label, _DAMAGE_NAMES[i], x, y + 10f, 11f, 10f);
            TMP_Text value = AddText(_number, 30f, Color.white, _DAMAGE_VALUES[i], x, y + 25f, 30f, 1f);
            x += Mathf.Max(GetTextWidth(name), GetTextWidth(value)) + _SUMMARY_GAP;
        }

        SetRect(AddImage(_line).rectTransform, start, y, width, 1f);
        SetRect(AddImage(_line).rectTransform, start, y + 63f, width, 1f);
    }

    private void AddRow(string line)
    {
        string[] cells = line.Split('|');
        string key = cells[0].Trim();
        string value = cells[1].Trim();
        string note = cells.Length > 2 ? cells[2].Trim() : "";
        string summary = cells.Length > 3 ? cells[3].Trim() : key;

        CRow row = new CRow();
        Color faint = new Color(_soft.r, _soft.g, _soft.b, 0.7f);
        row.key = AddText(_sans, 13.5f, faint, key, 0f, 0f, _ROW_HEIGHT);

        if (_DAMAGE.IsMatch(value))
        {
            _DAMAGE_NAMES.Add(summary);
            _DAMAGE_VALUES.Add(value);
            row.value = AddText(_number, 17f, Color.white, value, 0f, 0f, _ROW_HEIGHT, 2f);
        }
        else row.value = AddText(_sansSemiBold, 14.5f, _valueText, value, 0f, 0f, _ROW_HEIGHT);

        if (note.Length > 0)
        {
            Color dim = new Color(_soft.r, _soft.g, _soft.b, 0.55f);
            row.note = AddText(_sans, 12f, dim, note, 0f, 0f, _NOTE_HEIGHT);
            row.isLongNote = note.Length > _LONG_NOTE;
        }

        _ROWS.Add(row);
    }

    private float BuildBody(string[] lines, int first, float x, float y, float inner)
    {
        float keyWidth = 0f;
        float valueWidth = 0f;
        foreach (CRow row in _ROWS)
        {
            keyWidth = Mathf.Max(keyWidth, GetTextWidth(row.key));
            valueWidth = Mathf.Max(valueWidth, GetTextWidth(row.value));
        }

        float valueX = x + keyWidth + _COLUMN_GAP;
        float noteX = valueX + valueWidth + _COLUMN_GAP;
        int next = 0;
        bool isFirst = true;
        bool wasRow = false;
        for (int i = first; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Length == 0) continue;

            if (_ROWS.Count > 0 && line.StartsWith("#"))
            {
                if (!isFirst)
                {
                    y += 11f;
                    SetRect(AddImage(new Color(1f, 1f, 1f, 0.13f)).rectTransform, x, y, inner, 1f);
                    y += 11f;
                }

                int cut = line.IndexOf(" : ");
                string title = (cut >= 0 ? line.Substring(1, cut - 1) : line.Substring(1)).Trim();
                TMP_Text head = AddText(_sansSemiBold, 12.5f, _blockTitle, title, x, y, _BLOCK_HEAD, 8f);
                if (cut >= 0)
                {
                    Color dim = new Color(_soft.r, _soft.g, _soft.b, 0.62f);
                    AddText(_sans, 12.5f, dim, line.Substring(cut + 3).Trim(),
                        x + GetTextWidth(head) + 9f, y, _BLOCK_HEAD);
                }

                y += _BLOCK_HEAD + 5f;
                wasRow = false;
            }
            else if (_ROWS.Count > 0 && line.Contains("|"))
            {
                if (wasRow) y += _ROW_GAP;
                CRow row = _ROWS[next++];
                SetTextPosition(row.key, x, y);
                SetTextPosition(row.value, valueX, y);
                y += _ROW_HEIGHT;
                if (row.note != null && row.isLongNote)
                {
                    SetTextPosition(row.note, valueX, y + _ROW_GAP - 4f);
                    y += _ROW_GAP - 4f + _NOTE_HEIGHT;
                }
                else if (row.note != null) SetTextPosition(row.note, noteX, y - _ROW_HEIGHT + 2.6f);

                wasRow = true;
            }
            else
            {
                if (!isFirst) y += _ROW_GAP;
                y += AddBodyText(line, x, y, inner);
                wasRow = false;
            }

            isFirst = false;
        }

        return y;
    }

    private float AddBodyText(string line, float x, float y, float width)
    {
        bool isNote = line.StartsWith("<i>") && line.EndsWith("</i>");
        if (isNote) line = line.Substring(3, line.Length - 7);

        Color color = isNote ? new Color(_cream.r, _cream.g, _cream.b, 0.6f) : _bodyText;
        TMP_Text text = AddText(_sans, isNote ? 12.5f : 13.5f, color, line, x, y, _TEXT_LINE);
        if (text.preferredWidth <= width) return _TEXT_LINE;

        text.enableWordWrapping = true;
        RectTransform rect = text.rectTransform;
        rect.sizeDelta = new Vector2(width, text.GetPreferredValues(line, width, 0f).y);
        return rect.sizeDelta.y;
    }

    private TMP_Text AddText(TMP_FontAsset font, float size, Color color, string value, float x, float y,
        float lineHeight, float spacing = 0f)
    {
        TMP_Text text;
        if (_usedTexts < _TEXTS.Count) text = _TEXTS[_usedTexts];
        else
        {
            GameObject go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(_content, false);
            text = go.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            RectTransform created = (RectTransform)go.transform;
            created.anchorMin = new Vector2(0f, 1f);
            created.anchorMax = new Vector2(0f, 1f);
            created.pivot = new Vector2(0f, 1f);
            _TEXTS.Add(text);
        }

        _usedTexts++;
        text.gameObject.SetActive(true);
        text.font = font;
        text.fontSharedMaterial = font.material;
        text.fontSize = size;
        text.color = color;
        text.characterSpacing = spacing;
        text.enableWordWrapping = false;
        text.text = value;

        RectTransform rt = (RectTransform)text.transform;
        rt.sizeDelta = new Vector2(Mathf.Ceil(text.preferredWidth) + 1f, lineHeight);
        rt.anchoredPosition = new Vector2(x, -y);
        return text;
    }

    private Image AddImage(Color color)
    {
        Image image;
        if (_usedImages < _IMAGES.Count) image = _IMAGES[_usedImages];
        else
        {
            GameObject go = new GameObject("Rect", typeof(RectTransform));
            go.transform.SetParent(_content, false);
            go.transform.SetAsFirstSibling();
            image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.rectTransform.anchorMin = new Vector2(0f, 1f);
            image.rectTransform.anchorMax = new Vector2(0f, 1f);
            image.rectTransform.pivot = new Vector2(0f, 1f);
            _IMAGES.Add(image);
        }

        _usedImages++;
        image.gameObject.SetActive(true);
        image.color = color;
        return image;
    }

    private static float GetTextWidth(TMP_Text text)
    {
        return text.preferredWidth + text.fontSize * text.characterSpacing / 100f;
    }

    private static void SetTextPosition(TMP_Text text, float x, float y)
    {
        ((RectTransform)text.transform).anchoredPosition = new Vector2(x, -y);
    }

    private static void SetRect(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(width, height);
    }
}
