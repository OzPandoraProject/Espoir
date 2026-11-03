using TMPro;
using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(TextMeshProUGUI))]

public class CTestStackLabel : MonoBehaviour
{
    [SerializeField] private CUnit _unit;
    [SerializeField] private CStatusSO _status;
    [SerializeField] private Camera _camera;
    [SerializeField] private float _height;
    [SerializeField] private Vector2 _offset;

    private TextMeshProUGUI _label;
    private int _previousStack = -1;

    private void Awake()
    {
        _label = GetComponent<TextMeshProUGUI>();
    }

    private void LateUpdate()
    {
        if (_unit == null || !_unit.isActiveAndEnabled || _unit.HpCurrent <= 0f || _status == null || _camera == null)
        {
            _label.enabled = false;
            
            return;
        }

        Vector3 anchor = _unit.transform.position + Vector3.up * _height;
        Vector3 screen = _camera.WorldToScreenPoint(anchor);
        _label.enabled = screen.z > 0f && _camera.pixelRect.Contains((Vector2)screen);

        if (!_label.enabled)
        {
            return;
        }

        _label.rectTransform.position = (Vector2)screen + _offset;
        int stack = _unit.FindStatusStack(_status.Id);

        if (stack != _previousStack)
        {
            _label.text = stack.ToString();
            _previousStack = stack;
        }
    }
}
