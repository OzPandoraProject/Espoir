using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class CTestCamera : MonoBehaviour
{
    [Header("추적 설정")]
    [SerializeField] private Transform _target;
    [SerializeField, Min(0.01f)] private float _followSharpness = 12f;

    [Header("확대 설정")]
    [SerializeField] private Vector2 _distanceRange = new Vector2(3.5f, 100f);
    [SerializeField] private float _defaultDistance = 60f;
    [SerializeField] private Vector2 _pitchRange = new Vector2(15f, 36f);
    [SerializeField] private Vector2 _targetHeightRange = new Vector2(0.4f, 0f);
    [SerializeField, Min(0f)] private float _zoomSensitivity = 0.002f;
    [SerializeField, Min(0.01f)] private float _zoomSharpness = 12f;

    [Header("회전 설정")]
    [SerializeField] private float _defaultYaw = 225f;
    [SerializeField, Min(0f)] private float _rotationSensitivity = 0.15f;
    [SerializeField] private Vector2 _pitchLimits = new Vector2(10f, 75f);

    [Header("입력 설정")]
    [SerializeField] private InputAction _rotateModifier = new InputAction(
        "RotateModifier", InputActionType.Button, "<Keyboard>/shift"
    );
    [SerializeField] private InputAction _rotateButton = new InputAction(
        "RotateButton", InputActionType.Button, "<Mouse>/leftButton"
    );
    [SerializeField] private InputAction _look = new InputAction(
        "Look", InputActionType.Value, "<Mouse>/delta"
    );
    [SerializeField] private InputAction _zoom = new InputAction(
        "Zoom", InputActionType.Value, "<Mouse>/scroll/y"
    );

    public bool IsInputBlocked => isActiveAndEnabled && (_rotateModifier.IsPressed() || _isRotating);

    private Vector3 _followPosition;
    private float _distance;
    private float _desiredDistance;
    private float _yaw;
    private float _pitchOffset;
    private bool _isRotating;
    private bool _hasFocus = true;
    private Vector2 _cursorPosition;
    private CursorLockMode _previousLockMode;
    private bool _previousCursorVisible;

    private void OnEnable()
    {
        _rotateModifier.Enable();
        _rotateButton.Enable();
        _look.Enable();
        _zoom.Enable();
    }

    private void OnDisable()
    {
        StopRotation();
        _rotateModifier.Disable();
        _rotateButton.Disable();
        _look.Disable();
        _zoom.Disable();
    }

    private void OnDestroy()
    {
        _rotateModifier.Dispose();
        _rotateButton.Dispose();
        _look.Dispose();
        _zoom.Dispose();
    }

    private void Start()
    {
        if (_target == null)
        {
            CLogger.Error($"{name}: 카메라 추적 대상을 연결해주세요.");
            enabled = false;
            return;
        }

        _followPosition = _target.position;
        _distance = Mathf.Clamp(_defaultDistance, _distanceRange.x, _distanceRange.y);
        _desiredDistance = _distance;
        _yaw = _defaultYaw;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        _hasFocus = hasFocus;
        if (!hasFocus) StopRotation();
    }

    private void LateUpdate()
    {
        if (_target == null) return;

        UpdateRotation();

        if (_hasFocus)
        {
            float scroll = _zoom.ReadValue<float>();
            _desiredDistance *= Mathf.Exp(-scroll * _zoomSensitivity);
            _desiredDistance = Mathf.Clamp(_desiredDistance, _distanceRange.x, _distanceRange.y);
        }

        float zoomBlend = 1f - Mathf.Exp(-_zoomSharpness * Time.unscaledDeltaTime);
        _distance = Mathf.Lerp(_distance, _desiredDistance, zoomBlend);
        float zoomRatio = Mathf.InverseLerp(_distanceRange.x, _defaultDistance, _distance);
        float basePitch = Mathf.Lerp(_pitchRange.x, _pitchRange.y, zoomRatio);
        float pitch = Mathf.Clamp(basePitch + _pitchOffset, _pitchLimits.x, _pitchLimits.y);
        _pitchOffset = pitch - basePitch;

        float followBlend = 1f - Mathf.Exp(-_followSharpness * Time.unscaledDeltaTime);
        _followPosition = Vector3.Lerp(_followPosition, _target.position, followBlend);
        float height = Mathf.Lerp(_targetHeightRange.x, _targetHeightRange.y, zoomRatio);
        Vector3 pivot = _followPosition + Vector3.up * height;
        Quaternion rotation = Quaternion.Euler(pitch, _yaw, 0f);

        transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * _distance, rotation);
    }

    private void UpdateRotation()
    {
        bool isRotating = _hasFocus && _rotateModifier.IsPressed() && _rotateButton.IsPressed();

        if (!isRotating)
        {
            StopRotation();
            return;
        }

        if (!_isRotating)
        {
            _isRotating = true;
            _cursorPosition = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            _previousLockMode = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            return;
        }

        Vector2 delta = _look.ReadValue<Vector2>() * _rotationSensitivity;
        _yaw = Mathf.Repeat(_yaw + delta.x, 360f);
        _pitchOffset -= delta.y;
    }

    private void StopRotation()
    {
        if (!_isRotating) return;

        _isRotating = false;
        Cursor.lockState = _previousLockMode;
        Cursor.visible = _previousCursorVisible;

        if (_hasFocus && Mouse.current != null && _previousLockMode == CursorLockMode.None)
        {
            Mouse.current.WarpCursorPosition(_cursorPosition);
        }
    }
}
