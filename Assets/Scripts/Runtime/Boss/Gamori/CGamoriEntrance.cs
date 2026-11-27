using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 소환 후 등장 연출을 재생하고 가모리의 전투를 시작합니다.
/// </summary>
// 의상과 머리카락의 LateUpdate 전에 보스 위치를 보정한다.
[DefaultExecutionOrder(-200)]
public sealed class CGamoriEntrance : MonoBehaviour, IGamoriCine
{
    [SerializeField] private CBossSummoner _summoner;
    [SerializeField] private CGamoriBoss _boss;
    [SerializeField] private Animator _animator;
    [SerializeField] private Transform _motionRoot;
    [SerializeField] private CTalkNpc _npc;
    [Tooltip("NPC와 모델이 별도 오브젝트일 때 연결합니다.")]
    [SerializeField] private GameObject _npcModel;
    [SerializeField] private CPlayer _player;
    [Tooltip("모션, 카메라, 이펙트의 공통 기준점입니다.")]
    [SerializeField] private Transform _throneFront;
    [SerializeField] private CGamoriEntranceView _view;
    [SerializeField] private CGamoriEntranceCamera _cineCamera;
    [SerializeField] private CGamoriEffectClip _effects;
    [SerializeField] private CGamoriSeatDrape _drape;
    [SerializeField] private TextAsset _trackData;

    [Header("등장 모션")]
    [SerializeField] private AnimationClip[] _motions;
    [SerializeField] private float[] _motionStarts;
    [SerializeField] private string _readyState = "Base Layer.Ready";
    [SerializeField, Min(0f)] private float _readyBlend = 0.3f;

    [Header("연출 시간 (초)")]
    [SerializeField] private float _motionEnd = 13.49f;
    [SerializeField] private float _duration = 15.8f;
    [SerializeField] private float _seatRelease = 3.45f;
    [SerializeField] private float _cardTime = 11.67f;
    [SerializeField] private float _vignetteStart = 0.4f, _vignetteEnd = 14.5f;
    [SerializeField] private float _cameraOut = 14.87f;

    [SerializeField, Min(0.01f)] private float _retreatDuration = 1.4f;

    [Header("플레이어")]
    [Tooltip("왕좌 앞 기준점에서 플레이어까지의 거리(m)입니다.")]
    [SerializeField] private float _playerDistance = 9.5f;

    private readonly List<Renderer> _HIDDEN = new List<Renderer>();
    private readonly List<Renderer> _NPC_HIDDEN = new List<Renderer>();
    private CGamoriEntranceTrack _track;
    private CGamoriChase _chase;
    private int[] _states;
    private Vector3 _origin;
    private Quaternion _rotation;
    private float _time = -1f;
    private static readonly int _RETREAT_FADE = Shader.PropertyToID("_RetreatFade");
    private readonly List<Collider> _RETREAT_COLLIDERS = new List<Collider>();
    private readonly List<CEnvironmentGradeMask> _RETREAT_MASKS = new List<CEnvironmentGradeMask>();
    private Renderer[] _retreatRenderers;
    private MaterialPropertyBlock _retreatProperties;
    private float _retreatTime = -1f;
    private bool _pending;
    private bool _settled;
    private bool _cardShown;
    private bool _playerShown;

    public bool IsPlaying => _time >= 0f;

    public float PlayTime => _time;

    public float CameraOut => _cameraOut;

    public float ReturnTime => 0.9f;

    private float Unit => _boss.transform.lossyScale.y * CGamoriEffectClip.UNIT_SCALE;

    private void Awake()
    {
        _track = new CGamoriEntranceTrack(_trackData);
        _chase = _boss.GetComponent<CGamoriChase>();
        _states = new int[_motions.Length];
        for (int i = 0; i < _motions.Length; i++) _states[i] = Animator.StringToHash("Base Layer." + _motions[i].name);
    }

    private void OnEnable()
    {
        _summoner.OnSummonChanged += OnSummonChanged;
    }

    private void OnDisable()
    {
        _summoner.OnSummonChanged -= OnSummonChanged;
        _pending = false;
        if (IsPlaying) Finish();
        if (_retreatTime >= 0f) FinishRetreat();
    }

    private void OnSummonChanged(bool isSummoned)
    {
        if (isSummoned)
        {
            if (_retreatTime >= 0f) FinishRetreat();
            _pending = true;
            return;
        }
        _pending = false;
        if (IsPlaying) Finish();
        if (!_boss || _boss.HpCurrent <= 0f) return;
        _boss.enabled = false;
        if (_chase) _chase.enabled = false;
        foreach (Collider body in _boss.GetComponentsInChildren<Collider>())
        {
            if (!body.enabled) continue;
            _RETREAT_COLLIDERS.Add(body);
            body.enabled = false;
        }
        foreach (CEnvironmentGradeMask mask in _boss.GetComponentsInChildren<CEnvironmentGradeMask>())
        {
            if (!mask.enabled) continue;
            _RETREAT_MASKS.Add(mask);
            mask.enabled = false;
        }
        if (_retreatRenderers == null)
        {
            _retreatRenderers = _boss.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            _retreatProperties = new MaterialPropertyBlock();
        }
        _retreatTime = 0f;
    }

    private void SetRetreatFade(float amount)
    {
        foreach (Renderer part in _retreatRenderers)
        {
            if (!part) continue;
            part.GetPropertyBlock(_retreatProperties);
            _retreatProperties.SetFloat(_RETREAT_FADE, amount);
            part.SetPropertyBlock(_retreatProperties);
        }
    }

    private void FinishRetreat()
    {
        _retreatTime = -1f;
        _boss.gameObject.SetActive(false);
        SetRetreatFade(0f);
        foreach (Collider body in _RETREAT_COLLIDERS)
        {
            if (body) body.enabled = true;
        }
        foreach (CEnvironmentGradeMask mask in _RETREAT_MASKS)
        {
            if (mask) mask.enabled = true;
        }
        _RETREAT_COLLIDERS.Clear();
        _RETREAT_MASKS.Clear();
        _boss.enabled = true;
        ShowNpc(true);
    }

    /// <summary>
    /// 연출 위치를 월드 좌표로 변환합니다.
    /// </summary>
    public Vector3 ToWorld(Vector3 localPosition) =>
        _origin + _rotation * (new Vector3(-localPosition.x, localPosition.y, localPosition.z) * Unit);

    /// <summary>
    /// 연출 방향을 월드 방향으로 변환합니다.
    /// </summary>
    public Vector3 ToWorldDirection(Vector3 localDirection) =>
        _rotation * new Vector3(-localDirection.x, localDirection.y, localDirection.z);

    public CGamoriEntranceTrack Track => _track;

    private void Update()
    {
        if (_retreatTime >= 0f)
        {
            _retreatTime += Time.deltaTime;
            float amount = Mathf.Clamp01(_retreatTime / _retreatDuration);
            SetRetreatFade(CCssEase.Smooth(amount));
            if (amount >= 1f) FinishRetreat();
            return;
        }
        if (_pending && _boss.gameObject.activeInHierarchy) Begin();
        if (!IsPlaying) return;

        _time += Time.deltaTime;
        if (_time < _motionEnd) Sample();
        else if (!_settled) Settle();

        if (_drape) _drape.SetSeated(_time < _seatRelease);
        _view.SetVignette(_time >= _vignetteStart && _time < _vignetteEnd);
        if (!_cardShown && _time >= _cardTime)
        {
            _cardShown = true;
            _view.PlayCard();
        }
        if (!_playerShown && _time >= _cameraOut)
        {
            _playerShown = true;
            Show(_HIDDEN);
        }
        if (_time >= _duration)
        {
            Finish();
            _boss.BeginBattle(_player);
        }
    }

    private void Begin()
    {
        CGameManager.Instance.SetGameState(EGameState.Cinematic);
        _pending = false;
        _time = 0f;
        _settled = _cardShown = _playerShown = false;
        _origin = _throneFront.position;
        _rotation = Quaternion.Euler(0f, _throneFront.eulerAngles.y, 0f);

        ShowNpc(false);
        PlacePlayer();
        if (_chase) _chase.enabled = false;
        _boss.transform.SetPositionAndRotation(_origin, _rotation);
        _animator.speed = 0f;
        Sample();
        _animator.Update(0f);
        Align();
        if (_drape) _drape.SetSeated(true, 0f);
        if (_effects && _effects.CanPlay) _effects.Play(() => _time, _origin, _rotation, _boss.transform.lossyScale.y);
        _cineCamera.Play(this);
    }

    private void Sample()
    {
        int index = 0;
        while (index + 1 < _motions.Length && _time >= _motionStarts[index + 1]) index++;
        float phase = (_time - _motionStarts[index]) / _motions[index].length;
        _animator.Play(_states[index], 0, Mathf.Clamp(phase, 0f, 0.999f));
    }

    private void Settle()
    {
        _settled = true;
        Vector3 place = _motionRoot.position;
        place.y = _origin.y;
        _boss.transform.position = place;
        _animator.speed = 1f;
        _animator.CrossFadeInFixedTime(_readyState, _readyBlend, 0, 0f);
        if (!_boss.TryGetComponent(out CGamoriSettle settle)) settle = _boss.gameObject.AddComponent<CGamoriSettle>();
        settle.Hold(_motionRoot, place, _readyBlend);
    }

    private void LateUpdate()
    {
        if (IsPlaying && !_settled) Align();
    }

    private void Align()
    {
        _boss.transform.position += ToWorld(_track.Root(_time)) - _motionRoot.position;
    }

    private void Finish()
    {
        _time = -1f;
        if (_animator) _animator.speed = 1f;
        Show(_HIDDEN);
        if (_view) _view.SetVignette(false);
        if (_drape) _drape.SetSeated(false);
        if (_effects) _effects.Stop();
        if (CGameManager.Instance && CGameManager.Instance.GameState == EGameState.Cinematic)
            CGameManager.Instance.SetGameState(EGameState.Idle);
        if (_player) _player.SetInputLocked(false);
        if (_chase) _chase.enabled = true;
    }

    private void PlacePlayer()
    {
        if (!_player) return;
        Vector3 place = _origin + _rotation * Vector3.forward * (_playerDistance * Unit);
        place.y = _player.transform.position.y;
        _player.TryGetComponent(out CharacterController body);
        bool restoreController = body && body.enabled;
        if (restoreController) body.enabled = false;
        _player.transform.SetPositionAndRotation(place, _rotation * Quaternion.Euler(0f, 180f, 0f));
        if (restoreController) body.enabled = true;
        _player.SetInputLocked(true);
        Hide(_player.GetComponentsInChildren<Renderer>(), _HIDDEN);
        foreach (CCompanionFollow companion in FindObjectsByType<CCompanionFollow>(FindObjectsSortMode.None))
        {
            Hide(companion.GetComponentsInChildren<Renderer>(), _HIDDEN);
        }
    }

    internal void ShowNpc(bool isShown)
    {
        _npc.SetTalkable(isShown);
        if (isShown) Show(_NPC_HIDDEN);
        else
        {
            Hide(_npc.GetComponentsInChildren<Renderer>(), _NPC_HIDDEN);
            if (_npcModel) Hide(_npcModel.GetComponentsInChildren<Renderer>(), _NPC_HIDDEN);
        }
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
