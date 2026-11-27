using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 가모리 사망 시 동작과 카메라를 재생하고 플레이어의 시점을 복원합니다.
/// </summary>
// 의상과 머리카락의 LateUpdate 전에 보스 위치를 보정한다.
[DefaultExecutionOrder(-200)]
public sealed class CGamoriDeath : MonoBehaviour, IGamoriCine
{
    [SerializeField] private CBossSummoner _summoner;
    [SerializeField] private CGamoriBoss _boss;
    [SerializeField] private CGamoriEntrance _entrance;
    [SerializeField] private Animator _animator;
    [SerializeField] private Transform _motionRoot;
    [SerializeField] private CPlayer _player;
    [SerializeField] private CGamoriEntranceCamera _cineCamera;
    [SerializeField] private CGamoriEffectClip _effects;
    [SerializeField] private TextAsset _trackData;
    [SerializeField] private AnimationClip _motion;
    [SerializeField] private CBossSummonTitleView _hudHider;

    [Header("연출 시간 (초)")]
    [SerializeField] private float _bodyHide = 4.05f;
    [SerializeField] private float _cameraOut = 6.4f;
    [SerializeField] private float _duration = 7.3f;
    [Tooltip("X는 시작 시간(초), Y는 종료 시간(초), Z는 재생 속도 배율입니다.")]
    [SerializeField] private Vector3 _slow = new Vector3(3.42f, 3.72f, 0.5f);

    private readonly List<Renderer> _HIDDEN = new List<Renderer>();
    private readonly List<Renderer> _BODY_HIDDEN = new List<Renderer>();
    private CGamoriEntranceTrack _track;
    private Vector3 _origin;
    private Quaternion _rotation;
    private float _unit;
    private int _state;
    private float _time = -1f;
    private bool _bodyGone;

    public bool IsPlaying => _time >= 0f;

    public float PlayTime => _time;

    public float CameraOut => _cameraOut;

    public float ReturnTime => 0.9f;

    public CGamoriEntranceTrack Track => _track;

    private bool CanPose => _boss && _motionRoot && _animator && _animator.isActiveAndEnabled;

    /// <inheritdoc/>
    public Vector3 ToWorld(Vector3 localPosition) =>
        _origin + _rotation * (new Vector3(-localPosition.x, localPosition.y, localPosition.z) * _unit);

    /// <inheritdoc/>
    public Vector3 ToWorldDirection(Vector3 localDirection) =>
        _rotation * new Vector3(-localDirection.x, localDirection.y, localDirection.z);

    private void Awake()
    {
        _track = new CGamoriEntranceTrack(_trackData);
        _state = Animator.StringToHash("Base Layer." + _motion.name);
    }

    private void OnEnable()
    {
        _summoner.OnSummonChanged += OnSummonChanged;
    }

    private void OnDisable()
    {
        _summoner.OnSummonChanged -= OnSummonChanged;
        if (IsPlaying) Finish();
    }

    private void OnSummonChanged(bool isSummoned)
    {
        if (!isSummoned && _boss && _boss.gameObject.activeInHierarchy && _boss.HpCurrent <= 0f) Begin();
    }

    private void Begin()
    {
        if (!CanPose) return;
        CGameManager.Instance.SetGameState(EGameState.Cinematic);
        _time = 0f;
        _bodyGone = false;
        Transform boss = _boss.transform;
        _origin = boss.position;
        _rotation = Quaternion.Euler(0f, boss.eulerAngles.y, 0f);
        _unit = boss.lossyScale.y * CGamoriEffectClip.UNIT_SCALE;

        if (_boss.TryGetComponent(out CGamoriSettle settle)) settle.enabled = false;
        if (_player)
        {
            _player.SetInputLocked(true);
            Hide(_player.GetComponentsInChildren<Renderer>(), _HIDDEN);
        }
        foreach (CCompanionFollow companion in FindObjectsByType<CCompanionFollow>(FindObjectsSortMode.None))
        {
            Hide(companion.GetComponentsInChildren<Renderer>(), _HIDDEN);
        }
        if (_hudHider) _hudHider.Play(_duration);

        _animator.speed = 0f;
        Sample();
        _animator.Update(0f);
        Align();
        if (_effects && _effects.CanPlay) _effects.Play(() => _time, _origin, _rotation, boss.lossyScale.y);
        _cineCamera.Play(this);
    }

    private void Update()
    {
        if (!IsPlaying) return;
        if (!_boss || !_animator || (!_bodyGone && !CanPose))
        {
            Finish();
            return;
        }
        bool isSlow = _time >= _slow.x && _time < _slow.y;
        _time += Time.deltaTime * (isSlow ? _slow.z : 1f);
        if (!_bodyGone)
        {
            if (_time < _bodyHide) Sample();
            else
            {
                _bodyGone = true;
                Hide(_boss.GetComponentsInChildren<Renderer>(), _BODY_HIDDEN);
            }
        }
        if (_time >= _duration) Finish();
    }

    private void Sample()
    {
        _animator.Play(_state, 0, Mathf.Clamp(_time / _motion.length, 0f, 0.999f));
    }

    private void LateUpdate()
    {
        if (IsPlaying && !_bodyGone && CanPose) Align();
    }

    private void Align()
    {
        _boss.transform.position += ToWorld(_track.Root(_time)) - _motionRoot.position;
    }

    private void Finish()
    {
        _time = -1f;
        if (_animator) _animator.speed = 1f;
        if (_effects) _effects.Stop();
        if (_hudHider && _hudHider.IsPlaying) _hudHider.Stop();
        Show(_HIDDEN);
        if (_boss) _boss.gameObject.SetActive(false);
        Show(_BODY_HIDDEN);
        if (_entrance) _entrance.ShowNpc(true);
        if (CGameManager.Instance && CGameManager.Instance.GameState == EGameState.Cinematic)
            CGameManager.Instance.SetGameState(EGameState.Idle);
        if (_player) _player.SetInputLocked(false);
    }

    private static void Hide(Renderer[] targets, List<Renderer> hidden)
    {
        foreach (Renderer target in targets)
        {
            if (!target.enabled) continue;
            target.enabled = false;
            hidden.Add(target);
        }
    }

    private static void Show(List<Renderer> hidden)
    {
        foreach (Renderer target in hidden)
        {
            if (target) target.enabled = true;
        }
        hidden.Clear();
    }
}
