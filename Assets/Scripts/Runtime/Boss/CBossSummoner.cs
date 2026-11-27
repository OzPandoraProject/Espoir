using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 나태의 조각상 대화로 기존 보스(벨페고르)를 제단 가운데에 페이드 인으로 소환하고, 다시 말 걸면 페이드 아웃으로 잠재운다.
/// 보스 AI · 전투 코드는 그대로 두고, 켜고 끄기와 재질 투명도만 다룬다. 공용 재질은 건드리지 않고 재질 사본을 잠깐 바꿔 끼운다.
/// 보스 소환·해제의 페이드와 카메라 연출을 제어합니다.
/// </summary>
public class CBossSummoner : MonoBehaviour
{
    private const string _OPAQUE_SHADER_NAME = "Espoir/BossUnlitOutline";
    private static readonly int _FADE = Shader.PropertyToID("_Fade");

    [Header("연결")]
    [SerializeField] private CEnemy _boss;
    [Tooltip("제단 가운데 (보스가 서는 자리 · 방향)")]
    [SerializeField] private Transform _altar;
    [Tooltip("제단 입구의 나태의 조각상 (소환 장면 카메라 기준)")]
    [SerializeField] private Transform _statue;
    [SerializeField] private CTalkCamera _talkCamera;
    [SerializeField] private CBossSummonTitleView _titleView;
    [SerializeField] private CPlayer _player;
    [SerializeField] private Shader _fadeShader;
    [SerializeField] private LayerMask _groundMask = 1 << 3;

    [Header("시간 (웹 값)")]
    [SerializeField, Min(0f)] private float _fadeInDelay = 0.8f;
    [SerializeField, Min(0.01f)] private float _fadeInDuration = 2f;
    [SerializeField, Min(0.01f)] private float _fadeOutDuration = 1.4f;
    [SerializeField, Min(0f)] private float _summonSceneDuration = 3.4f;
    [SerializeField, Min(0f)] private float _cinematicDuration = 5.2f;
    [SerializeField, Min(0f)] private float _shakeStrength = 0.12f;

    [Header("애니메이션 (웹: 보스 대기 애니는 소환 · 잠재움 내내 재생)")]
    [Tooltip("보스 Animator 첫 레이어의 대기 상태")]
    [SerializeField] private string _idleStateName = "Idle";
    [Tooltip("공격 레이어(두 번째부터)의 빈 상태")]
    [SerializeField] private string _emptyStateName = "New State";
    [SerializeField, Min(0f)] private float _idleBlend = 0.25f;

    [Header("자리")]
    [SerializeField] private float _altarLift = 0.05f;
    [SerializeField] private float _cineForward = 5f;
    [SerializeField] private float _cineRight = 4.5f;
    [SerializeField] private float _cineHeight = 4.2f;
    [SerializeField] private float _cineFov = 42f;

    [Header("보스 소환해제 거리 (영역없으면 이거씀)")]
    [SerializeField] private float _unsummonDistance = 25f;

    [Header("아레나 영역 (거리보다 우선시함)")]
    [SerializeField] private CBossArena _arena;

    private readonly List<Renderer> _RENDERERS = new List<Renderer>();
    private readonly List<Material[]> _ORIGINAL_MATERIALS = new List<Material[]>();
    private readonly List<Material[]> _FADE_MATERIALS = new List<Material[]>();
    private CEnvironmentGradeMask _gradeMask;
    private bool _isFadeMaterial;
    private float _summonTime = -1f;
    private float _dismissTime = -1f;
    private float _cineTime = -1f;
    private bool _isSummoned;
    private float _alpha = 1f;

    /// <summary>
    /// 소환 상태가 바뀔 때 (true = 소환, false = 잠재움). 보스 UI 등이 구독한다.
    /// </summary>
    public event Action<bool> OnSummonChanged;

    /// <summary>
    /// 보스가 소환돼 있는지 (페이드 인 중 포함, 페이드 아웃 시작부터 false).
    /// </summary>
    public bool IsSummoned => _isSummoned;

    /// <summary>
    /// 지금 보스에 적용된 투명도 (확인용).
    /// </summary>
    public float CurrentAlpha => _alpha;

    public CEnemy Boss => _boss;

    private void Awake()
    {
        if (_boss == null) return;
        if (_boss is not IBoss) return;

        foreach (Renderer target in _boss.GetComponentsInChildren<Renderer>(true))
        {
            Material[] shared = target.sharedMaterials;
            bool usesBossShader = false;

            foreach (Material material in shared)
            {
                if (material != null && material.shader != null
                    && material.shader.name == _OPAQUE_SHADER_NAME) usesBossShader = true;
            }

            if (!usesBossShader) continue;

            Material[] fades = new Material[shared.Length];

            for (int i = 0; i < shared.Length; i++)
            {
                if (shared[i] == null || shared[i].shader == null || shared[i].shader.name != _OPAQUE_SHADER_NAME
                    || _fadeShader == null)
                {
                    fades[i] = shared[i];
                    continue;
                }

                // 재질 사본: 공용 재질 값은 그대로 복사하고 셰이더만 투명판으로
                Material fade = new Material(_fadeShader);
                fade.CopyPropertiesFromMaterial(shared[i]);
                fade.name = shared[i].name + " (Fade)";
                fade.SetFloat(_FADE, 1f);
                fades[i] = fade;
            }

            _RENDERERS.Add(target);
            _ORIGINAL_MATERIALS.Add(shared);
            _FADE_MATERIALS.Add(fades);
        }

        _gradeMask = _boss.GetComponentInChildren<CEnvironmentGradeMask>(true);
        _isSummoned = _boss.gameObject.activeSelf;
    }

    private void OnDestroy()
    {
        foreach (Material[] fades in _FADE_MATERIALS)
        {
            foreach (Material fade in fades)
            {
                if (fade != null && fade.shader == _fadeShader) Destroy(fade);
            }
        }
    }

    /// <summary>
    /// 보스를 소환합니다. 이미 소환돼 있으면 아무것도 하지 않고 false 를 반환합니다.
    /// </summary>
    public bool Summon()
    {
        if (_isSummoned || _boss == null) return false;
        if (_boss is not IBoss) return false;
        CGameManager.Instance.InvokeAutosave();
        _isSummoned = true;
        CGameManager.Instance.InvokeBossBattleActivation(true);
        _summonTime = 0f;
        _dismissTime = -1f;
        _cineTime = 0f;

        // 소환 전엔 숨김 → 제단 가운데 원판 윗면에 세움 (처음 켤 때 Awake 가 이 자리에서 돌도록 켜기 전에 옮김)
        _boss.gameObject.SetActive(false);
        ResetBoss();
        PlaceOnAltar();
        UseFadeMaterials(true);
        SetAlpha(0f);

        if (_player != null) _player.SetInputLocked(true);
        if (_talkCamera != null) _talkCamera.BeginCinematic(GetCinePosition(), GetCineLook(), _cineFov,
            _cinematicDuration);
        if (_titleView != null) _titleView.Play();
        if (_boss is CSlothBoss)
        {
            CGameManager.Instance?.BeginBossTimer(EBossClear.Pigritia);
        }
        if (_boss is CPrideBoss)
        {
            CGameManager.Instance?.BeginBossTimer(EBossClear.Superbia);
        }
        OnSummonChanged?.Invoke(true);
        return true;
    }

    /// <summary>
    /// 소환한 보스를 1.4초 동안 사라지게 합니다. 소환돼 있지 않으면 false 를 반환합니다.
    /// </summary>
    public bool Unsummon()
    {
        if (_boss is CSlothBoss)
        {
            CGameManager.Instance?.EndBossTimer(EBossClear.Pigritia, false);
        }
        if (_boss is CPrideBoss)
        {
            CGameManager.Instance?.EndBossTimer(EBossClear.Superbia, false);
        }

        if (!_isSummoned || _boss == null) return false;

        _isSummoned = false;
        CGameManager.Instance.InvokeBossBattleActivation(false);
        _summonTime = -1f;
        _dismissTime = 0f;

        if (!_boss.gameObject.activeSelf)
        {
            FinishDismiss();
            OnSummonChanged?.Invoke(false);
            return true;
        }

        // 사라지는 동안 대기 애니메이션을 유지하고, AI가 공격 상태로 바뀌지 않도록 고정합니다.
        ReturnToIdle();
        UseFadeMaterials(true);
        SetAlpha(1f);
        OnSummonChanged?.Invoke(false);
        return true;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        float shake = 0f;

        if (_summonTime >= 0f)
        {
            _summonTime += dt;
            float k = Mathf.Clamp01((_summonTime - _fadeInDelay) / _fadeInDuration);

            if (_summonTime > _fadeInDelay && !_boss.gameObject.activeSelf)
            {
                _boss.gameObject.SetActive(true);
                RefreshGradeMask();
            }

            SetAlpha(CCssEase.Smooth(k));

            if (k >= 1f && _isFadeMaterial) UseFadeMaterials(false);

            if (_summonTime < _fadeInDelay) shake = _summonTime / _fadeInDelay * _shakeStrength;
            else if (_summonTime < _fadeInDelay +
                _fadeInDuration) shake = (1f - (_summonTime - _fadeInDelay) / _fadeInDuration) * _shakeStrength;

            if (_summonTime > _summonSceneDuration) _summonTime = -1f;
        }

        else if (_dismissTime >= 0f)
        {
            _dismissTime += dt;
            float k = Mathf.Min(1f, _dismissTime / _fadeOutDuration);
            SetAlpha(1f - CCssEase.Smooth(k));

            if (k >= 1f) FinishDismiss();
        }

        if (_talkCamera != null) _talkCamera.SetShake(shake);

        if (_cineTime >= 0f)
        {
            _cineTime += dt;

            if (_cineTime > _cinematicDuration)
            {
                _cineTime = -1f;
                if (_player != null) _player.SetInputLocked(false);
            }
        }

        if (_isSummoned && _boss != null &&
            _boss.gameObject.activeInHierarchy && _boss.HpCurrent <= 0f)
        {
            Unsummon();
            return;
        }

        if (_isSummoned && _player != null)
        {
            Vector3 playerPosition = _player.transform.position;

            if (_arena != null)
            {
                if (!_arena.Contains(playerPosition)) Unsummon();
            }
            else if (_altar != null)
            {
                Vector3 offset = playerPosition - _altar.position;
                offset.y = 0f;

                if (offset.sqrMagnitude > _unsummonDistance * _unsummonDistance)
                {
                    Unsummon();
                }
            }
        }

        // 페이드 인 시작부터 소환 장면이 끝나 플레이어가 움직일 수 있을 때까지, 그리고 잠재우는 동안은 AI 를 대기 상태로 묶음
        // (공격 타이머 · 상태를 매 프레임 처음 값으로). 애니메이터는 그대로 돌아 대기 애니가 재생된다.
        bool isHolding = _summonTime >= 0f || _cineTime >= 0f || _dismissTime >= 0f;
        if (isHolding && _boss != null && _boss.gameObject.activeSelf)
        {
            if (_boss is IBoss targetBoss) targetBoss.ResetBattleState();
        }
    }

    // 공격 모션 중이면 대기 상태로 섞어 돌림 (첫 레이어 = 대기, 나머지 공격 레이어 = 빈 상태)
    private void ReturnToIdle()
    {
        Animator animator = _boss.GetComponent<Animator>();
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;

        int idle = Animator.StringToHash(_idleStateName);
        int empty = Animator.StringToHash(_emptyStateName);

        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            int target = layer == 0 ? idle : empty;
            if (!animator.HasState(layer, target)) continue;
            if (animator.GetCurrentAnimatorStateInfo(layer).shortNameHash == target
                && !animator.IsInTransition(layer)) continue;

            animator.CrossFadeInFixedTime(target, _idleBlend, layer);
        }
    }

    private void FinishDismiss()
    {
        _dismissTime = -1f;
        _boss.gameObject.SetActive(false);
        UseFadeMaterials(false);
        SetAlpha(1f);
    }

    // 다시 소환(잠재운 뒤 · 쓰러뜨린 뒤) = 새 보스: 상태이상 전부 지우고 체력 최대, AI · 공격 상태는 처음(대기)으로.
    //  기존 API 만 씀: CUnit.RemoveStatus(id) · GetHeal(1, ByMax) (최대 체력까지 채우고 넘치면 잘림) · CSlothBoss.ResetBattleState
    private void ResetBoss()
    {
        IReadOnlyList<CStatus> status = _boss.Status;

        if (status != null)
        {
            List<int> ids = new List<int>();
            foreach (CStatus s in status) if (!ids.Contains(s.Id)) ids.Add(s.Id);
            foreach (int id in ids) _boss.RemoveStatus(id, true);
        }

        if (_boss.HpCurrent < _boss.HpMax) _boss.GetHeal(1f, ERatioSwitch.ByMax, true);
        if (_boss is IBoss targetBoss) targetBoss.ResetBattleState();
        if (_boss is CPrideBoss prideBoss) prideBoss.ResetPhaseState();
        _boss.ResetDropState();
    }

    private void PlaceOnAltar()
    {
        if (_altar == null) return;

        Vector3 position = _altar.position + Vector3.up * _altarLift;
        _boss.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, _altar.eulerAngles.y, 0f));
    }

    // 소환 장면: 석상 옆 조금 높은 곳에서 제단 가운데(보스)를 봄
    private Vector3 GetCinePosition()
    {
        Transform basis = _altar != null ? _altar : transform;
        Vector3 origin = _statue != null ? _statue.position : basis.position;
        Vector3 forward = Vector3.ProjectOnPlane(basis.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(basis.right, Vector3.up).normalized;
        Vector3 position = origin + forward * _cineForward + right * _cineRight;
        Vector3 rayOrigin = new Vector3(position.x, position.y + 50f, position.z);
        float ground = origin.y;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 200f, _groundMask,
            QueryTriggerInteraction.Ignore))
        {
            ground = hit.point.y;
        }

        position.y = ground + _cineHeight;
        return position;
    }

    private Vector3 GetCineLook()
    {
        Vector3 center = _altar != null ? _altar.position : transform.position;
        return center + Vector3.up * _cineHeight;
    }

    private void UseFadeMaterials(bool useFade)
    {
        if (_isFadeMaterial == useFade) return;

        _isFadeMaterial = useFade;

        for (int i = 0; i < _RENDERERS.Count; i++)
        {
            if (_RENDERERS[i] != null) _RENDERERS[i].sharedMaterials = useFade ? _FADE_MATERIALS[i] :
                _ORIGINAL_MATERIALS[i];
        }

        RefreshGradeMask();
    }

    // 원색 덮개(CEnvironmentGradeMask)는 켜질 때 재질 목록을 기억하므로 바꿔 끼운 뒤 다시 읽힘
    private void RefreshGradeMask()
    {
        if (_gradeMask == null || !_gradeMask.isActiveAndEnabled) return;

        _gradeMask.enabled = false;
        _gradeMask.enabled = true;
    }

    private void SetAlpha(float alpha)
    {
        _alpha = alpha;

        foreach (Material[] fades in _FADE_MATERIALS)
        {
            foreach (Material fade in fades)
            {
                if (fade != null && fade.HasProperty(_FADE)) fade.SetFloat(_FADE, alpha);
            }
        }
    }
}
