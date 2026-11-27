using UnityEngine;

/// <summary>
/// 가모리 연출의 카메라 트랙을 재생하고 전투 시점으로 복귀합니다.
/// </summary>
// 전투와 대화 카메라가 위치를 갱신한 뒤 연출 시점을 적용한다.
[DefaultExecutionOrder(95)]
public sealed class CGamoriEntranceCamera : MonoBehaviour
{
    private const float _FOCUS_DISTANCE = 5f;

    [SerializeField] private Camera _camera;

    private IGamoriCine _source;
    private float _gameFov;
    private float _appliedFov;
    private bool _active;
    private CPlayer _player;

    /// <summary>
    /// 지정한 연출의 카메라 트랙을 재생합니다.
    /// </summary>
    public void Play(IGamoriCine source)
    {
        _source = source;
    }

    private void LateUpdate()
    {
        if (!_camera || _source == null || !_source.IsPlaying)
        {
            if (_active) Release();
            _source = null;
            return;
        }
        if (!_active || !Mathf.Approximately(_camera.fieldOfView, _appliedFov)) _gameFov = _camera.fieldOfView;
        _active = true;

        float time = _source.PlayTime;
        float back = Mathf.Clamp01((time - _source.CameraOut) / Mathf.Max(_source.ReturnTime, 0.01f));
        float weight = 1f - back * back * back * (back * (back * 6f - 15f) + 10f);
        _source.Track.Camera(Mathf.Min(time, _source.CameraOut), out Vector3 position,
            out Vector3 forward, out float fov);
        Transform view = _camera.transform;
        Vector3 cinePlace = _source.ToWorld(position), cineForward = _source.ToWorldDirection(forward).normalized;
        if (weight >= 1f)
        {
            view.SetPositionAndRotation(cinePlace, Quaternion.LookRotation(cineForward, Vector3.up));
        }
        else
        {
            BlendToGameView(view, cinePlace, cineForward, weight);
        }
        _appliedFov = Mathf.Lerp(_gameFov, fov, weight);
        _camera.fieldOfView = _appliedFov;
    }

    // 복귀 중 플레이어가 화면을 벗어나지 않도록 카메라 위치와 주시점을 함께 보간한다.
    private void BlendToGameView(Transform view, Vector3 cinePlace, Vector3 cineForward, float weight)
    {
        Vector3 gamePlace = view.position, gameForward = view.forward;
        if (!_player) _player = FindFirstObjectByType<CPlayer>();
        float gameReach = _player ? Vector3.Dot(_player.transform.position - gamePlace, gameForward) : _FOCUS_DISTANCE;
        Vector3 gameFocus = gamePlace + gameForward * Mathf.Max(gameReach, 1f);
        float cineReach = Vector3.Dot(gameFocus - cinePlace, cineForward);
        Vector3 cineFocus = cinePlace + cineForward * (cineReach > 1f ? cineReach : _FOCUS_DISTANCE);

        Vector3 place = Vector3.Lerp(gamePlace, cinePlace, weight);
        Vector3 toward = Vector3.Lerp(gameFocus, cineFocus, weight) - place;
        Quaternion look = toward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toward, Vector3.up) : view.rotation;
        view.SetPositionAndRotation(place, look);
    }

    private void Release()
    {
        _active = false;
        if (_camera) _camera.fieldOfView = _gameFov;
    }

    private void OnDisable()
    {
        if (_active) Release();
    }
}
