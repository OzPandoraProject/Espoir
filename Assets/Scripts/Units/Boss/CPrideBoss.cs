using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class CPrideBoss : CEnemy, IBoss
{

    private enum EPridePhase
    {
        Phase1,
        RecoveryPending,
        Phase2,
        FinalJudgmentPending,
        Finisher
    }

    [Header("연결")]
    [SerializeField] private CPlayer _player;
    [SerializeField] private CPreviewPattern _previewPattern;
    [SerializeField] private CLinePreview _linePreviewPattern;
    [SerializeField] private GameObject _swordPrefab;

    [Header("평타 이펙트")]
    [SerializeField] private CEspoirBossFxPlayer _slamImpactPrefab;

    [Header("순간이동 이펙트")]
    [SerializeField] private GameObject _warpSwordPrefab;
    [SerializeField] private GameObject _warpArrivalFxPrefab;

    [Header("페이즈 이펙트")]
    [SerializeField] private CEspoirBossFxPlayer _phaseRingFxPrefab;
    [SerializeField] private CEspoirBossFxPlayer _judgmentOrbFxPrefab;
    [SerializeField] private ParticleSystem _judgmentOrbBreakFxPrefab;
    [SerializeField] private Transform _judgmentOrbAnchor;
    [SerializeField, Min(0.01f)] private float _judgmentOrbScale = 0.2f;

    [Header("인식범위 (확인용)")]
    [SerializeField] private float _battleStartRadius = 20f;
    [SerializeField] private float _battleEndRadius = 25f;
    [SerializeField] private float _attackRadius = 1.5f; // 기본 평타 검찍기 공격범위
    [SerializeField] private float _attackDelay = 1.5f; // 기본 평타 검찍기 내려오는 시간

    [Header("평타 이동용")]
    [SerializeField] private LayerMask _groundMask = 1 << 0;
    [SerializeField] private LayerMask _moveObstacleMask = (1 << 0) | (1 << 6) | (1 << 7);

    [Header("메인 패턴용")]
    [SerializeField] private Material _mainBeamMaterial;
    [SerializeField, Min(1f)] private float _crossLaserRadius = 14f;
    [SerializeField] private int _attacksPerMainPattern = 10;

    [Header("디버그 로그")]
    [SerializeField] private bool _enableLog = false;

    [Header("회복 패턴 무력화 게이지")]
    [SerializeField, Min(1f)] private float _recoveryGroggyHpMax = 2000f;

    [Header("최종 심판")]
    [SerializeField, Min(1f)] private float _judgmentGroggyHpMax = 2000f;

    [Header("전투영역")]
    [SerializeField] private CBossArena _arena;

    [Header("사운드")]
    [SerializeField] private AudioSource _SE_ShowPattern;
    [SerializeField] private AudioSource _SE_FallingSword;
    [SerializeField] private AudioSource _SE_ShootingSword;
    [SerializeField] private AudioSource _SE_ChargingLaser;
    [SerializeField] private AudioSource _SE_ShootingLaser;
    [SerializeField] private AudioSource _SE_Groggy;
    [SerializeField] private AudioSource _SE_HealPattern;
    [SerializeField] private AudioSource _SE_JudgementPattern;
    [SerializeField] private AudioSource _SE_JudgementFailed;

    private const float RecoveryDuration = 8f;
    private const float RecoveryHealRatio = 0.5f;

    private const float JudgmentDuration = 6f;

    private float _groggyRemainHp;
    private float _groggyRemainTime;

    private CEspoirBossFxPlayer _phaseFx;

    private Rigidbody _rb;
    private CapsuleCollider _collider;

    private CancellationTokenSource _cts;
    private readonly List<CEspoirBossFxPlayer> _activeSlamImpacts = new List<CEspoirBossFxPlayer>();
    private Animator _animator;
    private float _realAttackDelay;
    private const float MainLaserSwordScale = 1.2f;

    private int _mainCounter = 0;

    private enum EPrideMainPattern
    {
        TripleLaser,
        SwordRain,
        CrossLaser,
    }

    private EPrideMainPattern _nextMainPattern;

    private EPridePhase _phase = EPridePhase.Phase1;
    private Vector3 _arenaCenter;
    public bool IsGroggy { get; private set; }
    private bool IsRage => _phase == EPridePhase.Phase2;

    private bool CanFight => _player != null && _previewPattern != null && HpCurrent > 0f && _player.HpCurrent > 0f;
    private bool IsGroggyPhase => _phase == EPridePhase.RecoveryPending ||
                    _phase == EPridePhase.FinalJudgmentPending;
    private bool CanContinueAttack => CanFight && !IsGroggyPhase && IsPlayerInBattleArea(_battleEndRadius);
    public override EBossClear BossId => EBossClear.Superbia;

    #region Animator Hash
    private static readonly int IdleState = Animator.StringToHash("BaseLayer.Idle");

    // 백스텝
    private static readonly int BackstepTrigger = Animator.StringToHash("tBackstep");

    // 검 소환 및 발사
    private static readonly int SwordSummonTrigger = Animator.StringToHash("tSwordSummon");
    private static readonly int SwordLaunchTrigger = Animator.StringToHash("tSwordLaunch");

    // 비행 및 착지
    private static readonly int RiseTrigger = Animator.StringToHash("tRise");
    private static readonly int LandTrigger = Animator.StringToHash("tLand");

    // 회복
    private static readonly int HealStartTrigger = Animator.StringToHash("tHealStart");
    private static readonly int HealEndTrigger = Animator.StringToHash("tHealEnd");

    // 무력화
    private static readonly int KneelTrigger = Animator.StringToHash("tKneel");
    private static readonly int KneelEndTrigger = Animator.StringToHash("tKneelEnd");

    // 사망
    private static readonly int DeathTrigger = Animator.StringToHash("tDeath");

    // 손 계속 들고있기
    private static readonly int SwordHoldParameter = Animator.StringToHash("bSwordHold");
    #endregion

    protected override void Awake()
    {
        base.Awake();

        if (_player == null)
        {
            _player = FindFirstObjectByType<CPlayer>();
        }

        if (_player == null)
        {
            CLogger.Error("CPrideBoss : 플레이어를 찾을 수 없음");
            enabled = false;
        }

        _rb = GetComponent<Rigidbody>();
        _arenaCenter = _rb.position;
        _collider = GetComponent<CapsuleCollider>();
        _animator = GetComponentInChildren<Animator>(true);
        _realAttackDelay = _attackDelay;
    }

    protected override void Update()
    {
        base.Update();

        if (_phase == EPridePhase.Finisher) return;

        if (!CanFight)
        {
            if (_cts != null) ResetBattleState();
            return;
        }

        if (_cts != null)
        {
            if (!IsPlayerInBattleArea(_battleEndRadius))
            {
                ResetBattleState();
            }
            return;
        }

        if (IsPlayerInBattleArea(_battleStartRadius))
        {
            _cts = new CancellationTokenSource();
            RunBattleLoop(_cts.Token).Forget();
        }
    }

    private async UniTask RunBattleLoop(CancellationToken token)
    {
        _realAttackDelay = _attackDelay; // 나중에 2페되면 이거 줄이면 될듯!
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (!IsGroggyPhase)
                {
                    await UniTask.Delay(1500, delayTiming: PlayerLoopTiming.LastUpdate,
                        cancellationToken: token);
                }

                if (!CanFight ||
                    !IsPlayerInBattleArea(_battleEndRadius))
                {
                    return;
                }

                if (_phase == EPridePhase.RecoveryPending)
                {
                    await RunRecoveryPattern(token);
                    continue;
                }

                if (_phase == EPridePhase.FinalJudgmentPending)
                {
                    await RunFinalJudgmentPattern(token);
                    return;
                }

                if (_mainCounter < _attacksPerMainPattern)
                {
                    int subAttackRatio = IsRage ? 3 : 4;
                    if (Random.Range(0, subAttackRatio) == 0)
                    {
                        await RunSubAttack(token);
                    }
                    else
                    {
                        await RunNormalAttack(token);
                    }

                    token.ThrowIfCancellationRequested();
                    _mainCounter++;
                }
                else
                {
                    EPrideMainPattern nextPattern = EPrideMainPattern.TripleLaser;

                    switch (_nextMainPattern)
                    {
                        case EPrideMainPattern.TripleLaser:
                            await RunMainLaserAttack(token);
                            nextPattern = EPrideMainPattern.SwordRain;
                            break;

                        case EPrideMainPattern.SwordRain:
                            await RunSwordRain(token);
                            if (IsRage) nextPattern = EPrideMainPattern.CrossLaser;
                            break;

                        case EPrideMainPattern.CrossLaser:
                            await RunRotatingCrossLaserAttack(token);
                            break;
                    }

                    token.ThrowIfCancellationRequested();
                    _mainCounter = 0;
                    _nextMainPattern = nextPattern;
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // 전투 종료로 진행 중인 공격이 취소
        }
        finally
        {
            if (!token.IsCancellationRequested) ResetBattleState();
        }
    }

    private async UniTask RunNormalAttack(CancellationToken token)
    {
        Vector3 toPlayer = _player.transform.position - _rb.position;
        toPlayer.y = 0f;

        float distanceSquared = toPlayer.sqrMagnitude;


        // 2번: 4m 이내에서 50%.
        if (distanceSquared > 0.0001f && distanceSquared <= 4f * 4f && Random.value < 0.25f)
        {
            Vector3 forward = toPlayer.normalized;
            Vector3 backstepPoint = _rb.position - forward * 5f;

            if (TryGetMoveDestination(backstepPoint, out _)
                && !IsBackstepPathBlocked(-forward, 5f))
            {
                _rb.rotation = Quaternion.LookRotation(forward);
                Vector3 attackPoint = _rb.position + forward * 5f;

                if (_animator != null && _animator.isActiveAndEnabled)
                {
                    _animator.SetTrigger(BackstepTrigger);
                }

                await UniTask.WhenAll(
                    SlamTask(attackPoint, _realAttackDelay * 0.75f, 0.15f, _attackRadius * 0.65f, token,
                        new SDamageInfo(100f, false, ERatioSwitch.Constant, false, null)),
                    BackstepTask(-forward, token));
                return;
            }
        }

        // 3번: 10m 이상에서 50%, 15m 이상에서 75%.
        if (_warpSwordPrefab != null && distanceSquared >= 10f * 10f)
        {
            float chaseChance = distanceSquared >= 15f * 15f ? 0.75f : 0.5f;
            Vector3 forward = toPlayer.normalized;
            Vector3 behindPlayer = _player.transform.position + forward * 2.5f;
            behindPlayer.y = _rb.position.y;
            Vector3 sideOffset = Vector3.Cross(Vector3.up, forward) * 2f;

            if (Random.value < chaseChance &&
                (TryGetMoveDestination(behindPlayer, out Vector3 destination) ||
                TryGetMoveDestination(behindPlayer + sideOffset, out destination)))
            {
                _animator?.SetTrigger(SwordSummonTrigger);
                await FlyWarpSwordTask(destination, token);
                token.ThrowIfCancellationRequested();

                if (!CanFight || !IsPlayerInBattleArea(_battleEndRadius))
                {
                    return;
                }

                Vector3 landingPoint = destination;
                if (TryGetMoveDestination(landingPoint, out destination) ||
                    TryGetMoveDestination(landingPoint + sideOffset, out destination))
                {
                    _rb.velocity = Vector3.zero;
                    _rb.position = destination;

                    Vector3 facing = _player.transform.position - destination;
                    facing.y = 0f;
                    if (facing.sqrMagnitude > 0.0001f)
                    {
                        _rb.rotation = Quaternion.LookRotation(facing);
                    }

                    if (_warpArrivalFxPrefab != null)
                    {
                        Instantiate(_warpArrivalFxPrefab, destination, Quaternion.identity);
                    }
                }
                await SlamTask(_player.transform.position, _realAttackDelay * 0.75f, 0.15f, _attackRadius * 0.65f, token,
                        new SDamageInfo(100f, false, ERatioSwitch.Constant, false, null));
                return;
            }
        }

        if (distanceSquared > 4f * 4f && distanceSquared < 10f * 10f
            && _linePreviewPattern != null && _warpSwordPrefab != null
            && Random.value < 0.5f)
        {
            await RunAimedLineSword(token);
            return;
        }

        // 1번: 기본 낙하와 이동 불가 시 대체 공격.
        _animator?.SetTrigger(SwordSummonTrigger);
        await SlamTask(_player.transform.position, _realAttackDelay * 1f, 0.2f, _attackRadius, token,
                        new SDamageInfo(100f, false, ERatioSwitch.Constant, false, null));
    }

    private async UniTask RunSubAttack(CancellationToken token)
    {
        if (GetPlayerDistanceSquared() <= 4f * 4f
            && _linePreviewPattern != null && _warpSwordPrefab != null)
        {
            await RunBackstepSwordVolley(token);
            return;
        }

        switch (Random.Range(0, 2))
        {
            case 0:
                await RunGiantSlam(token);
                break;

            case 1:
                await RunSeveralSlam(true, token);
                break;
        }
    }

    #region MainPattern
    private async UniTask RunMainLaserAttack(CancellationToken token)
    {
        if (!CanContinueAttack || _linePreviewPattern == null ||
            _warpSwordPrefab == null || _mainBeamMaterial == null) return;

        Rigidbody body = _rb;
        if (!TryGetMoveDestination(_arenaCenter, out Vector3 restPosition)) return;
        Vector3 groundOrigin = restPosition;

        if (TryGetGround(groundOrigin, out RaycastHit ground))
        {
            groundOrigin.y = ground.point.y;
        }

        bool arrivedAtCenter = false;
        bool wasKinematic = body.isKinematic;
        GameObject[] swords = new GameObject[3];

        try
        {
            if (!wasKinematic) body.velocity = Vector3.zero;
            body.isKinematic = true;

            await MoveBossTask(restPosition, 0.45f, token);
            if (!CanContinueAttack) return;

            arrivedAtCenter = true;

            Vector3 forward = AimFrom(groundOrigin, Vector3.forward);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            body.rotation = Quaternion.LookRotation(forward);

            for (int i = 0; i < swords.Length; i++)
            {
                Vector3 offset = right * ((i - 1) * 2.5f) + Vector3.up * (i == 1 ? 3.2f : 2.4f);

                swords[i] = Instantiate(_warpSwordPrefab, restPosition + offset, Quaternion.LookRotation(Vector3.down, forward));
                swords[i].transform.localScale *= MainLaserSwordScale;
                swords[i].transform.SetParent(body.transform, true);

                if (swords[i].TryGetComponent(out TrailRenderer trail))
                {
                    trail.emitting = false;
                }
            }

            //_animator?.SetTrigger(RiseTrigger);
            //await MoveBossTask(restPosition + Vector3.up * 3f, 0.45f, token);

            if (!CanContinueAttack) return;

            foreach (GameObject sword in swords)
            {
                sword.transform.SetParent(null, true);
            }

            Vector3[] spreadStarts = new Vector3[swords.Length];
            Vector3[] spreadDirections = new Vector3[swords.Length];

            for (int i = 0; i < swords.Length; i++)
            {
                spreadStarts[i] = groundOrigin + forward * 3f + right * ((i - 1) * 4.5f);
            }

            for (int volley = 0; volley < 2; volley++)
            {
                for (int i = 0; i < swords.Length; i++)
                {
                    float yaw;

                    switch (i)
                    {
                        case 0:
                            yaw = Random.Range(-45f, -20f);
                            break;
                        case 1:
                            yaw = Random.Range(-8f, 8f);
                            break;
                        default:
                            yaw = Random.Range(20f, 45f);
                            break;
                    }

                    Vector3 aim = AimFrom(spreadStarts[i], forward);
                    spreadDirections[i] = Quaternion.Euler(0f, yaw, 0f) * aim;
                }

                await RunMainLaserVolley(swords, spreadStarts, spreadDirections, false, token);

                if (!CanContinueAttack) return;
            }

            forward = AimFrom(groundOrigin, forward);
            body.rotation = Quaternion.LookRotation(forward);

            Vector3[] finalStarts = new Vector3[1] { groundOrigin + forward * 3f };
            Vector3[] finalDirections = new Vector3[1] { AimFrom(finalStarts[0], forward) };

            await RunMainLaserVolley(swords, finalStarts, finalDirections, true, token);

            if (!CanContinueAttack) return;

            //_animator?.SetTrigger(LandTrigger);
            //await MoveBossTask(restPosition, 0.8f, token);
        }
        finally
        {
            foreach (GameObject sword in swords)
            {
                if (sword != null) Destroy(sword);
            }

            if (body != null)
            {
                if (arrivedAtCenter) body.position = restPosition;
                body.isKinematic = wasKinematic;
            }
        }

        if (CanContinueAttack)
        {
            await UniTask.Delay(1000, delayTiming: PlayerLoopTiming.LastUpdate,
                cancellationToken: token);
        }

        Vector3 AimFrom(Vector3 start, Vector3 fallback)
        {
            Vector3 direction = _player.transform.position - start;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : fallback;
        }
    }

    private async UniTask RunSwordRain(CancellationToken token)
    {
        if (_arena == null || !CanContinueAttack) return;
        if (!TryGetMoveDestination(_arenaCenter, out Vector3 center)) return;

        const int waveCount = 15;
        const int swordsPerWave = 7;
        const float readyTime = 1.3f;
        const float fallingTime = 0.25f;
        const float radius = 3f;
        const float minSpacing = radius * 2f;

        SDamageInfo damage = new SDamageInfo(
            180f, false, ERatioSwitch.Constant, false, null);
        List<Vector3> points = new List<Vector3>(swordsPerWave);
        List<UniTask> slams = new List<UniTask>(swordsPerWave);

        await MoveBossTask(center, 0.45f, token);
        if (!CanContinueAttack) return;

        BeginSwordHold();

        try
        {
            for (int wave = 0; wave < waveCount; wave++)
            {
                token.ThrowIfCancellationRequested();
                if (!CanContinueAttack) return;

                points.Clear();

                bool hitApplied = false;
                bool TryConsumeHit()
                {
                    if (hitApplied) return false;
                    hitApplied = true;
                    return true;
                }

                Vector3 aimedPoint = _player.transform.position;
                aimedPoint.y = center.y;

                points.Add(aimedPoint);
                slams.Add(SlamTask(aimedPoint, readyTime, fallingTime, radius,
                    token, damage, 1.5f, TryConsumeHit,
                    playSound: true, playLaunchAnimation: false));

                for (int attempt = 0; attempt < 36 && points.Count < swordsPerWave; attempt++)
                {
                    Vector3 point = _arena.GetRandomGroundCandidate(radius);
                    if (!TryGetGround(point, out RaycastHit ground)) continue;

                    point.y = ground.point.y;
                    if (!_arena.Contains(point)) continue;

                    bool overlaps = false;
                    for (int i = 0; i < points.Count; i++)
                    {
                        Vector3 offset = point - points[i];
                        offset.y = 0f;

                        if (offset.sqrMagnitude >= minSpacing * minSpacing) continue;

                        overlaps = true;
                        break;
                    }

                    if (overlaps) continue;

                    bool playSound = points.Count == 0;
                    points.Add(point);
                    slams.Add(SlamTask(point, readyTime, fallingTime, radius,
                        token, damage, 1.5f, TryConsumeHit,
                        playSound, playLaunchAnimation: false));
                }

                await UniTask.Delay(TimeSpan.FromSeconds(readyTime * 0.33));

                if (wave + 1 < waveCount && CanContinueAttack)
                {
                    await UniTask.Delay(slams.Count == 0 ? 1650 : 150,
                        delayTiming: PlayerLoopTiming.LastUpdate, cancellationToken: token);
                }
            }

            await UniTask.WhenAll(slams);
        }
        finally
        {
            EndSwordHold(token);
        }
    }

    private async UniTask RunRotatingCrossLaserAttack(CancellationToken token)
    {
        if (!CanFight || _linePreviewPattern == null ||
            _warpSwordPrefab == null || _mainBeamMaterial == null) return;

        if (!TryGetMoveDestination(_arenaCenter, out Vector3 center)) return;

        const float moveTime = 0.45f;
        const float warningTime = 1f;
        const float warningAngle = 15f;
        const float fireTime = 5f;
        const float fireAngle = 135f;
        const float width = 3f;
        const float fadeInTime = 0.15f;
        const float fadeOutTime = 0.18f;
        const float returnTime = 0.3f;
        const float tipDistance = 3.3125f * MainLaserSwordScale;
        const float hitInterval = 1f;

        float radius = _crossLaserRadius;
        float halfWidth = width * 0.5f;
        Vector3 beamHeight = Vector3.up * 2.2f;
        const int invincibleStatusId = 38;
        CStatus invincible = null;
        bool startedHovering = false;
        float startYaw = _rb.rotation.eulerAngles.y;
        bool wasKinematic = _rb.isKinematic;
        GameObject crossRoot = null;

        Vector3[] axes = new Vector3[4]
        {
            Vector3.forward, Vector3.right, Vector3.back, Vector3.left
        };

        CLinePreview[] previews = new CLinePreview[axes.Length];
        LineRenderer[] beams = new LineRenderer[axes.Length];
        Transform[] swords = new Transform[axes.Length];
        Vector3[] swordPositions = new Vector3[axes.Length];
        SDamageInfo damage = new SDamageInfo(300f, false, ERatioSwitch.Constant, false, null);

        try
        {
            if (!wasKinematic) _rb.velocity = Vector3.zero;
            _rb.isKinematic = true;

            // 보스를 아레나 중앙으로 이동
            await MoveBossTask(center, moveTime, token);
            if (!CanFight) return;

            _player.PauseMainStatusTimer = true;
            if (FindStatus(invincibleStatusId) == null)
            {
                AddStatus(invincibleStatusId, duration: 0f);
                invincible = FindStatus(invincibleStatusId);
            }

            startedHovering = true;
            _animator?.SetTrigger(RiseTrigger);
            await MoveBossTask(center + Vector3.up * 5f, 0.45f, token);
            if (!CanFight) return;

            float elapsed;

            crossRoot = new GameObject("Pride Rotating Cross");
            Transform pivot = crossRoot.transform;
            pivot.SetPositionAndRotation(center, Quaternion.Euler(0f, startYaw, 0f));

            for (int i = 0; i < axes.Length; i++)
            {
                Vector3 outward = pivot.TransformDirection(axes[i]);
                Vector3 groundStart = center + outward * radius;
                Vector3 beamStart = groundStart + beamHeight;
                Vector3 beamEnd = center + beamHeight;
                Vector3 swordPosition = beamStart + outward * tipDistance;

                GameObject sword = Instantiate(_warpSwordPrefab,
                    swordPosition, Quaternion.LookRotation(-outward), pivot);
                sword.transform.localScale *= MainLaserSwordScale;

                if (sword.TryGetComponent(out TrailRenderer trail))
                {
                    trail.emitting = false;
                }

                swords[i] = sword.transform;
                swordPositions[i] = sword.transform.localPosition;

                previews[i] = Instantiate(_linePreviewPattern, pivot);
                previews[i].Show(groundStart, center, width);

                // 빔의 두 끝점을 회전 중심 기준 로컬 좌표로 저장
                LineRenderer beam = ShowMainBeam(pivot, beamStart, beamEnd, width);
                beam.useWorldSpace = false;
                beam.SetPosition(0, pivot.InverseTransformPoint(beamStart));
                beam.SetPosition(1, pivot.InverseTransformPoint(beamEnd));
                beams[i] = beam;
            }

            if (_SE_ShowPattern != null) _SE_ShowPattern.Play();
            if (_SE_ChargingLaser != null) _SE_ChargingLaser.Play();

            elapsed = 0f;
            while (elapsed < warningTime)
            {
                await UniTask.NextFrame(PlayerLoopTiming.LastUpdate, token);
                if (!CanFight) return;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / warningTime);
                pivot.rotation = Quaternion.Euler(0f, startYaw + warningAngle * t, 0f);

                float alpha = Mathf.SmoothStep(0f, 1f,
                    (elapsed - (warningTime - fadeInTime)) / fadeInTime);
                SetCrossLaserAlpha(beams, alpha);
            }

            for (int i = 0; i < previews.Length; i++)
            {
                previews[i].ClearPreview();
            }

            
            if (_SE_ShootingLaser != null) _SE_ShootingLaser.Play();
            float nextHitTime = 0f;

            elapsed = 0f;

            while (elapsed < fireTime && CanFight)
            {
                float angle = startYaw + warningAngle + fireAngle * (elapsed / fireTime);
                pivot.rotation = Quaternion.Euler(0f, angle, 0f);

                if (elapsed >= nextHitTime)
                {
                    Vector3 local = pivot.InverseTransformPoint(_player.transform.position);
                    float x = Mathf.Abs(local.x);
                    float z = Mathf.Abs(local.z);

                    if ((x <= halfWidth && z <= radius) || (z <= halfWidth && x <= radius))
                    {
                        nextHitTime = elapsed + hitInterval;
                        _player.OnHit(this, damage);
                    }
                }

                await UniTask.NextFrame(PlayerLoopTiming.LastUpdate, token);
                elapsed += Time.deltaTime;
            }

            if (!CanFight) return;
            pivot.rotation = Quaternion.Euler(0f, startYaw + warningAngle + fireAngle, 0f);

            // 검 회수
            elapsed = 0f;
            while (elapsed < returnTime)
            {
                await UniTask.NextFrame(PlayerLoopTiming.LastUpdate, token);
                if (!CanFight) return;

                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / returnTime);
                SetCrossLaserAlpha(beams, 1f - Mathf.Clamp01(elapsed / fadeOutTime));

                for (int i = 0; i < swords.Length; i++)
                {
                    swords[i].localPosition = Vector3.Lerp(swordPositions[i], beamHeight, t);
                }
            }

            _animator?.SetTrigger(LandTrigger);
            await MoveBossTask(center, 0.8f, token);
        }
        finally
        {
            if (_player != null) _player.PauseMainStatusTimer = false;
            if (crossRoot != null) Destroy(crossRoot);

            if (_rb != null)
            {
                if (startedHovering) _rb.position = center;
                _rb.isKinematic = wasKinematic;
            }

            if (this != null && invincible != null) RemoveStatus(invincible);
        }

        if (CanFight)
        {
            await UniTask.Delay(800, delayTiming: PlayerLoopTiming.LastUpdate,
                cancellationToken: token);
        }
    }

    private async UniTask RunRecoveryPattern(CancellationToken token)
    {
        if (_animator != null && _animator.isActiveAndEnabled)
        {
            _animator.ResetTrigger(SwordSummonTrigger);
            _animator.ResetTrigger(SwordLaunchTrigger);
            _animator.Play(IdleState, 0, 0f);
            _animator.SetTrigger(HealStartTrigger);
        }

        // 손을 드는 동작을 보여준 뒤 회복과 무력화 판정을 시작
        await UniTask.Delay(1000, delayTiming: PlayerLoopTiming.LastUpdate,
            cancellationToken: token);
        if (!CanFight) return;

        _groggyRemainHp = _recoveryGroggyHpMax;
        _groggyRemainTime = RecoveryDuration;
        PlayPhaseRingFx(recovery: true);
        if (_SE_HealPattern != null) _SE_HealPattern.Play();

        while (_groggyRemainTime > 0f && _groggyRemainHp > 0f)
        {
            await UniTask.NextFrame(PlayerLoopTiming.LastUpdate, token);
            if (!CanFight) return;
            if (_groggyRemainHp <= 0f) break;

            float step = Mathf.Min(Time.deltaTime, _groggyRemainTime);
            _groggyRemainTime -= step;
            GetHeal(RecoveryHealRatio * step / RecoveryDuration, ERatioSwitch.ByMax);
        }

        if (_SE_HealPattern != null) _SE_HealPattern.Stop();
        StopPhaseFx();
        _groggyRemainTime = 0f;
        _phase = EPridePhase.Phase2;
        _mainCounter = 0;
        _nextMainPattern = EPrideMainPattern.CrossLaser;

        if (_groggyRemainHp <= 0f)
        {
            IsGroggy = true;
            if (_SE_Groggy != null) _SE_Groggy.Play();
            PlayRecoveryBreakFlash();
            _animator?.SetTrigger(KneelTrigger);
            await UniTask.Delay(2500, delayTiming: PlayerLoopTiming.LastUpdate,
                cancellationToken: token);

            IsGroggy = false;
            _animator?.SetTrigger(KneelEndTrigger);
            await UniTask.Delay(1250, delayTiming: PlayerLoopTiming.LastUpdate,
                cancellationToken: token);
        }
        else
        {
            _animator?.SetTrigger(HealEndTrigger);
            await UniTask.Delay(900, delayTiming: PlayerLoopTiming.LastUpdate,
                cancellationToken: token);
        }
        if (CanFight) AddStatus(40);
    }

    private void PlayGroggyStartAnimation()
    {
        if (_animator == null || !_animator.isActiveAndEnabled) return;

        _animator.ResetTrigger(SwordSummonTrigger);
        _animator.ResetTrigger(SwordLaunchTrigger);
        _animator.Play(IdleState, 0, 0f);
        _animator.SetTrigger(HealStartTrigger);
    }

    private async UniTask RunFinalJudgmentPattern(CancellationToken token)
    {
        PlayGroggyStartAnimation();

        await UniTask.Delay(1000, delayTiming: PlayerLoopTiming.LastUpdate,
            cancellationToken: token);
        if (!CanFight) return;

        _groggyRemainHp = _judgmentGroggyHpMax;
        _groggyRemainTime = JudgmentDuration;
        Transform orbCore = PlayJudgmentOrbFx();
        if (_SE_JudgementPattern != null) _SE_JudgementPattern.Play();

        while (_groggyRemainTime > 0f && _groggyRemainHp > 0f)
        {
            await UniTask.NextFrame(PlayerLoopTiming.LastUpdate, token);
            if (!CanFight) return;

            _groggyRemainTime = Mathf.Max(0f, _groggyRemainTime - Time.deltaTime);

            if (_phaseFx != null && orbCore != null)
            {
                _phaseFx.Advance(0f);
                AlignJudgmentOrb(orbCore);
            }
        }

        Vector3? orbBreakPosition = orbCore != null ? orbCore.position : (Vector3?)null;
        if (_SE_JudgementPattern != null) _SE_JudgementPattern.Stop();
        StopPhaseFx();
        _groggyRemainTime = 0f;
        _phase = EPridePhase.Finisher;

        if (_groggyRemainHp > 0f)
        {
            PlayPhaseRingFx(recovery: false);
            if (_phaseFx != null)
            {
                await UniTask.Delay(300, delayTiming: PlayerLoopTiming.LastUpdate,
                    cancellationToken: token);
            }

            if (CanFight)
            {
                if (_SE_JudgementFailed != null) _SE_JudgementFailed.Play();
                _player.Kill();
            }
            return;
        }

        IsGroggy = true;
        if (_SE_Groggy != null) _SE_Groggy.Play();
        if (orbBreakPosition.HasValue)
        {
            PlayJudgmentOrbBreakFx(orbBreakPosition.Value);
        }
        _animator?.SetTrigger(DeathTrigger);

        // 3.8초 사망 클립과 전환 시간을 확보
        await UniTask.Delay(4000, delayTiming: PlayerLoopTiming.LastUpdate,
            cancellationToken: token);
        if (!CanFight) return;

        base.GetDamage(HpCurrent, ERatioSwitch.Constant, true);
    }

    private async UniTask RunMainLaserVolley(GameObject[] swords, Vector3[] starts, Vector3[] directions,
        bool isFinale, CancellationToken token)
    {
        int beamCount = starts.Length;
        float width = isFinale ? 12f : 3f;
        const float length = 22f;
        const float tipDistance = 3.3125f * MainLaserSwordScale;

        float activeTime = isFinale ? 2.5f : 0.75f;
        Vector3 beamHeight = Vector3.up * 2.2f;

        CLinePreview[] previews = new CLinePreview[beamCount];
        LineRenderer[] beams = new LineRenderer[beamCount];
        Transform[] firingSwords = new Transform[beamCount];
        Vector3[] initialPositions = new Vector3[beamCount];
        Quaternion[] initialRotations = new Quaternion[beamCount];

        SDamageInfo damage = new SDamageInfo(isFinale ? 0.95f : 300f, false, isFinale ? ERatioSwitch.ByMax : ERatioSwitch.Constant, false, null);

        try
        {
            for (int i = 0; i < beamCount; i++)
            {
                Transform sword = swords[isFinale ? 1 : i].transform;
                firingSwords[i] = sword;
                initialPositions[i] = sword.position;
                initialRotations[i] = sword.rotation;

                previews[i] = Instantiate(_linePreviewPattern);
                previews[i].Show(starts[i], starts[i] + directions[i] * length, width);
            }

            if (_SE_ShowPattern != null) _SE_ShowPattern.Play();
            if (!isFinale && _SE_ChargingLaser != null) _SE_ChargingLaser.Play();

            const float turnTime = 0.25f;
            const float fadeInTime = 0.15f;
            float warningTime = isFinale ? 2f : 1.25f;
            float waitTime = warningTime - turnTime - fadeInTime;

            float turnElapsed = 0f;

            while (turnElapsed < turnTime)
            {
                await UniTask.NextFrame(PlayerLoopTiming.LastUpdate, token);
                if (!CanContinueAttack) return;

                turnElapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, turnElapsed / turnTime);

                for (int i = 0; i < beamCount; i++)
                {
                    Vector3 position = starts[i] + beamHeight - directions[i] * tipDistance;
                    Quaternion rotation = Quaternion.LookRotation(directions[i]);

                    firingSwords[i].SetPositionAndRotation(
                        Vector3.Lerp(initialPositions[i], position, t),
                        Quaternion.Slerp(initialRotations[i], rotation, t));
                }
            }

            if (isFinale)
            {
                await GatherMainLaserCharge(swords, starts[0] + beamHeight, tipDistance, waitTime, token);
            }
            else
            {
                await UniTask.Delay(TimeSpan.FromSeconds(waitTime),
                    delayTiming: PlayerLoopTiming.LastUpdate, cancellationToken: token);
            }

            if (!CanContinueAttack) return;

            for (int i = 0; i < beamCount; i++)
            {
                Vector3 beamStart = starts[i] + beamHeight;
                beams[i] = ShowMainBeam(
                    firingSwords[i], beamStart, beamStart + directions[i] * length, width);
            }

            float fadeInElapsed = 0f;

            while (fadeInElapsed < fadeInTime)
            {
                await UniTask.NextFrame(PlayerLoopTiming.LastUpdate, token);
                if (!CanContinueAttack) return;

                fadeInElapsed += Time.deltaTime;
                float alpha = Mathf.SmoothStep(0f, 1f, fadeInElapsed / fadeInTime);
                Color color = new Color(1f, 1f, 1f, alpha);

                foreach (LineRenderer beam in beams)
                {
                    beam.startColor = color;
                    beam.endColor = color;
                }
            }

            foreach (CLinePreview preview in previews)
            {
                preview.ClearPreview();
            }

            if (_SE_ShootingLaser != null) _SE_ShootingLaser.Play();
            bool hit = false;
            float elapsed = 0f;

            while (elapsed < activeTime && CanContinueAttack)
            {
                if (!hit)
                {
                    for (int i = 0; i < beamCount; i++)
                    {
                        Vector3 offset = _player.transform.position - starts[i];
                        Vector3 direction = directions[i];
                        Vector3 right = Vector3.Cross(Vector3.up, direction);

                        float along = Vector3.Dot(offset, direction);
                        float across = Mathf.Abs(Vector3.Dot(offset, right));

                        if (along >= 0f && along <= length && across <= width * 0.5f)
                        {
                            hit = true;
                            _player.OnHit(this, damage);
                            break;
                        }
                    }
                }

                await UniTask.NextFrame(PlayerLoopTiming.LastUpdate, token);
                elapsed += Time.deltaTime;
            }
            const float fadeTime = 0.18f;
            float fadeElapsed = 0f;

            while (fadeElapsed < fadeTime)
            {
                await UniTask.NextFrame(PlayerLoopTiming.LastUpdate, token);
                fadeElapsed += Time.deltaTime;

                float alpha = 1f - Mathf.Clamp01(fadeElapsed / fadeTime);
                Color color = new Color(1f, 1f, 1f, alpha);

                foreach (LineRenderer beam in beams)
                {
                    beam.startColor = color;
                    beam.endColor = color;
                }
            }
        }
        finally
        {
            for (int i = 0; i < beamCount; i++)
            {
                if (previews[i] != null) Destroy(previews[i].gameObject);
                if (beams[i] != null) Destroy(beams[i].gameObject);
            }
        }
    }

    private static void SetCrossLaserAlpha(LineRenderer[] beams, float alpha)
    {
        Color color = new Color(1f, 1f, 1f, alpha);

        for (int i = 0; i < beams.Length; i++)
        {
            beams[i].startColor = color;
            beams[i].endColor = color;
        }
    }

    private LineRenderer ShowMainBeam(Transform parent, Vector3 start, Vector3 end, float width)
    {
        GameObject beamObject = new GameObject("Pride Main Beam");
        beamObject.transform.SetParent(parent, false);

        LineRenderer beam = beamObject.AddComponent<LineRenderer>();
        beam.sharedMaterial = _mainBeamMaterial;
        beam.useWorldSpace = true;
        beam.positionCount = 2;
        beam.startWidth = width;
        beam.endWidth = width;
        beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        beam.receiveShadows = false;
        beam.SetPosition(0, start);
        beam.SetPosition(1, end);

        beam.textureMode = LineTextureMode.Stretch;
        Color transparent = new Color(1f, 1f, 1f, 0f);
        beam.startColor = transparent;
        beam.endColor = transparent;

        return beam;
    }

    private async UniTask GatherMainLaserCharge(GameObject[] swords, Vector3 centerTip, float tipDistance,
        float waitTime, CancellationToken token)
    {
        Transform center = swords[1].transform;
        Transform[] sideSwords = new Transform[2]
        {
            swords[0].transform,
            swords[2].transform
        };

        Vector3[] initialPositions = new Vector3[2];
        Quaternion[] initialRotations = new Quaternion[2];
        Vector3[] chargePositions = new Vector3[2];
        Quaternion[] chargeRotations = new Quaternion[2];

        for (int i = 0; i < sideSwords.Length; i++)
        {
            initialPositions[i] = sideSwords[i].position;
            initialRotations[i] = sideSwords[i].rotation;

            float side = i == 0 ? -1f : 1f;
            chargePositions[i] = centerTip + center.right * (side * (tipDistance + 2f)) - center.forward * 1.5f;

            chargeRotations[i] = Quaternion.LookRotation(centerTip - chargePositions[i]);
        }

        const float aimTime = 0.25f;
        float aimElapsed = 0f;

        while (aimElapsed < aimTime)
        {
            await UniTask.NextFrame(PlayerLoopTiming.LastUpdate, token);
            if (!CanContinueAttack) return;

            aimElapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, aimElapsed / aimTime);

            for (int i = 0; i < sideSwords.Length; i++)
            {
                sideSwords[i].SetPositionAndRotation(
                    Vector3.Lerp(initialPositions[i], chargePositions[i], t),
                    Quaternion.Slerp(initialRotations[i], chargeRotations[i], t));
            }
        }

        Vector3[] starts = new Vector3[sideSwords.Length];
        for (int i = 0; i < sideSwords.Length; i++)
        {
            starts[i] = sideSwords[i].position + sideSwords[i].forward * tipDistance;
        }

        LineRenderer[] streaks = new LineRenderer[starts.Length];

        try
        {
            for (int i = 0; i < streaks.Length; i++)
            {
                streaks[i] = ShowMainBeam(center, starts[i], centerTip, 0.24f);
            }

            if (_SE_ChargingLaser != null) _SE_ChargingLaser.Play();
            float chargeTime = waitTime - aimTime;
            float elapsed = 0f;

            while (elapsed < chargeTime)
            {
                await UniTask.NextFrame(PlayerLoopTiming.LastUpdate, token);
                if (!CanContinueAttack) return;

                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / chargeTime);
                float head = Mathf.SmoothStep(0f, 1f, progress);
                float tail = Mathf.Max(0f, head - 0.35f);
                float alpha = Mathf.Clamp01(Mathf.Sin(Mathf.PI * progress));
                Color color = new Color(1f, 1f, 1f, alpha);

                for (int i = 0; i < streaks.Length; i++)
                {
                    LineRenderer streak = streaks[i];
                    streak.SetPosition(0, Vector3.Lerp(starts[i], centerTip, tail));
                    streak.SetPosition(1, Vector3.Lerp(starts[i], centerTip, head));
                    streak.startColor = color;
                    streak.endColor = color;
                }
            }
        }
        finally
        {
            foreach (LineRenderer streak in streaks)
            {
                if (streak != null) Destroy(streak.gameObject);
            }
        }
    }
    #endregion

    #region SubPattern
    private async UniTask RunGiantSlam(CancellationToken token)
    {
        const float fallingTime = 0.25f;
        const float escapeGap = 1f; // 도망칠 수 있게 적용하는 최소 회피간격
        Vector3 attackPoint = _player.transform.position;
        

        float aimedRadius = _attackRadius * 1.5f;
        float spreadRadius = _attackRadius * 1f;
        float innerDistance = aimedRadius + spreadRadius + escapeGap;

        BeginSwordHold();

        try
        {
            for (int i = 0; i < 2; i++)
            {
                token.ThrowIfCancellationRequested();

                if (!CanFight || !IsPlayerInBattleArea(_battleEndRadius))
                {
                    return;
                }

                float additionalTime = i % 2 == 1 ? 1.5f : 1.5f;

                float readyTime = Mathf.Max(fallingTime, _realAttackDelay * 0.75f * additionalTime);
                Vector3 center = _player.transform.position;
                int swordCount = i == 0 ? 1 : 5;
                UniTask[] slams = new UniTask[swordCount];

                bool hitApplied = false;

                bool TryConsumeHit()
                {
                    if (hitApplied) return false;
                    hitApplied = true;
                    return true;
                }

                slams[0] = SlamTask(center, readyTime, fallingTime, aimedRadius, token,
                                new SDamageInfo(250f, false, ERatioSwitch.Constant, false, null),
                                3f, TryConsumeHit, playLaunchAnimation: false);
                int spreadCount = slams.Length - 1;
                float angleOffset = Random.Range(0f, 360f);

                for (int j = 0; j < spreadCount; j++)
                {
                    float angle = (angleOffset + j * (360f / spreadCount)
                        + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
                    float distance = Random.Range(innerDistance, innerDistance + 3f);
                    Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                    Vector3 point = center + direction * distance;

                    slams[j + 1] = SlamTask(point, readyTime, fallingTime, spreadRadius, token,
                                        new SDamageInfo(150f, false, ERatioSwitch.Constant, false, null),
                                        2f, TryConsumeHit, playSound: false, playLaunchAnimation: false);
                }

                await UniTask.WhenAll(slams);
            }
        }
        finally
        {
            EndSwordHold(token);
        }
        
    }

    private async UniTask RunSeveralSlam(bool special, CancellationToken token)
    {
        List<UniTask> slams = new List<UniTask>();
        BeginSwordHold();

        try
        {
            for (int i = 0; i < 5; i++)
            {
                token.ThrowIfCancellationRequested();
                if (!CanContinueAttack) return;

                bool hitApplied = false;
                bool TryConsumeHit()
                {
                    if (hitApplied) return false;
                    hitApplied = true;
                    return true;
                }

                SDamageInfo damage = new SDamageInfo(
                    150f, false, ERatioSwitch.Constant, false, null);

                Vector3 center = _player.transform.position;
                slams.Add(SlamTask(center, _realAttackDelay, 0.2f, _attackRadius,
                    token, damage, 1.5f, TryConsumeHit, playLaunchAnimation: false));

                if (special)
                {
                    float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    float distance = Random.Range(_attackRadius * 2f + 0.5f, _attackRadius * 2f + 3f);
                    Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                    Vector3 point = center + direction * distance;

                    slams.Add(SlamTask(point, _realAttackDelay, 0.2f, _attackRadius,
                        token, damage, 1.5f, TryConsumeHit,
                        playSound: false, playLaunchAnimation: false));
                }

                if (i < 4)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(_realAttackDelay * 2f / 3f),
                        delayTiming: PlayerLoopTiming.LastUpdate, cancellationToken: token);
                }
            }

            await UniTask.WhenAll(slams);
        }
        finally
        {
            EndSwordHold(token);
        }
    }

    private async UniTask RunBackstepSwordVolley(CancellationToken token)
    {
        if (_linePreviewPattern == null || _warpSwordPrefab == null || !CanFight) return;

        const float backstepDistance = 5f;
        const int swordCount = 3;

        Vector3 target = _rb.position;
        Vector3 forward = _player.transform.position - target;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f) return;
        forward.Normalize();

        Vector3 retreatPoint = target - forward * backstepDistance;
        if (!TryGetMoveDestination(retreatPoint, out _) ||
            IsBackstepPathBlocked(-forward, backstepDistance))
        {
            await RunAimedLineSword(token);
            return;
        }

        _rb.rotation = Quaternion.LookRotation(forward);

        if (_animator != null && _animator.isActiveAndEnabled)
        {
            _animator.SetTrigger(BackstepTrigger);
        }

        await BackstepTask(-forward, token);
        token.ThrowIfCancellationRequested();

        if (!CanFight || !IsPlayerInBattleArea(_battleEndRadius))
        {
            return;
        }

        Vector3 launchCenter = _rb.position;
        SDamageInfo damage = new SDamageInfo(100f, false, ERatioSwitch.Constant, false, null);
        UniTask[] swords = new UniTask[swordCount];
        bool hitApplied = false;

        bool TryConsumeHit()
        {
            if (hitApplied) return false;
            hitApplied = true;
            return true;
        }

        for (int i = 0; i < swordCount; i++)
        {
            float sectorStart = -90f + i * 60f;
            float angle = Random.Range(sectorStart, sectorStart + 60f);
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * -forward;
            Vector3 start = launchCenter + direction * Random.Range(6f, 8f);

            if (TryGetGround(start, out RaycastHit ground))
            {
                start.y = ground.point.y;
            }

            const float beyondCrossing = 5f;
            const float timeToCrossing = 0.25f;

            Vector3 toCrossing = target - start;
            toCrossing.y = 0f;

            float distanceToCrossing = toCrossing.magnitude;
            float speed = distanceToCrossing / timeToCrossing;

            float pathLength = distanceToCrossing + beyondCrossing;
            Vector3 end = start + toCrossing.normalized * pathLength;
            float flightTime = pathLength / speed;

            swords[i] = LineSwordTask(start, end, 1.5f, 1f, flightTime, damage, token,
                TryConsumeHit, playSound: i == 0);
        }

        await UniTask.WhenAll(swords);
    }
    #endregion

    #region VFX
    private CEspoirBossFxPlayer CreatePhaseFx(CEspoirBossFxPlayer prefab,
    Vector3 position)
    {
        if (prefab == null) return null;

        CEspoirBossFxPlayer fx = Instantiate(prefab, position, Quaternion.identity);
        if (fx.Stage == null)
        {
            fx.Stop();
            return null;
        }

        fx.screenEffects = false;
        return fx;
    }

    private void PlayPhaseRingFx(bool recovery)
    {
        Vector3 center = _rb.position;
        if (TryGetGround(center, out RaycastHit ground)) center.y = ground.point.y;
        center += Vector3.up * 0.05f;

        _phaseFx = CreatePhaseFx(_phaseRingFxPrefab, center);
        if (_phaseFx == null) return;

        CEspoirBossFxPlayer fx = _phaseFx;
        fx.SetElements(new CEspoirBossFxPlayer.CElementSetting[]
        {
        new CEspoirBossFxPlayer.CElementSetting
        {
            isGroup = true,
            group = CEspoirBossFxPlayer.EEffectGroup.Ground,
            hidden = !recovery,
            overrideRadius = recovery,
            innerRadius = 2.8f,
            outerRadius = 3.5f
        }
        });

        if (recovery)
        {
            fx.PreviewGroup = (int)CEspoirBossFxPlayer.EEffectGroup.Ground;
            fx.clock = () => 0.74f;
        }
        else
        {
            float startedAt = Time.time;
            fx.clock = () => 0.8f + (Time.time - startedAt);
        }

        fx.Advance(0f);
        Transform ring = fx.Stage.Find("Ground/fx0_RingField");
        if (ring != null) fx.transform.position += center - ring.position;
    }

    private Transform PlayJudgmentOrbFx()
    {
        if (_judgmentOrbAnchor == null) return null;

        _phaseFx = CreatePhaseFx(_judgmentOrbFxPrefab, _judgmentOrbAnchor.position);
        if (_phaseFx == null) return null;

        CEspoirBossFxPlayer fx = _phaseFx;
        fx.PreviewGroup = (int)CEspoirBossFxPlayer.EEffectGroup.Orb;
        fx.stageScale *= _judgmentOrbScale;
        fx.ApplyStageScale();
        fx.clock = () => Mathf.Lerp(1.12f, 3.55f,
            1f - Mathf.Clamp01(_groggyRemainTime / JudgmentDuration));

        fx.Advance(0f);
        Transform orbCore = fx.Stage.Find("fx0_OrbCore");
        if (orbCore == null)
        {
            StopPhaseFx();
            return null;
        }

        AlignJudgmentOrb(orbCore);
        return orbCore;
    }

    private void AlignJudgmentOrb(Transform orbCore)
    {
        Vector3 center = _judgmentOrbAnchor.position + Vector3.up * 0.25f;
        _phaseFx.transform.position += center - orbCore.position;
    }

    private void PlayRecoveryBreakFlash()
    {
        Bounds body = _collider.bounds;
        Vector3 chest = body.center + Vector3.up * (body.extents.y * 0.3f);
        CEspoirBossFxPlayer fx = CreatePhaseFx(_phaseRingFxPrefab, chest);
        if (fx == null) return;

        const float flashSourceTime = 0.8f;
        const float flashHoldTime = 0.08f;
        const float flashDuration = 0.35f;

        fx.OnlyObjectIndex = 3;
        fx.stageScale *= 0.55f;
        fx.ApplyStageScale();

        fx.clock = () => flashSourceTime;
        fx.Advance(0f);

        Transform flash = fx.Stage.Find("fx3_PunchFlash");
        if (flash == null || !flash.TryGetComponent(out MeshRenderer flashRenderer))
        {
            fx.Stop();
            return;
        }

        fx.transform.position += chest - flash.position;

        // 원본 프레임은 고정하고, 섬광의 밝기만 별도로 줄인다.
        Material flashMaterial = flashRenderer.sharedMaterial;
        int fadeProperty = Shader.PropertyToID("uT");
        float startedAt = Time.time;
        flashMaterial.SetFloat(fadeProperty, 0f);
        fx.clock = () =>
        {
            float fade = Mathf.InverseLerp(flashHoldTime, flashDuration, Time.time - startedAt);
            flashMaterial.SetFloat(fadeProperty, fade);
            return flashSourceTime;
        };

        Destroy(fx.gameObject, flashDuration);
    }

    private void PlayJudgmentOrbBreakFx(Vector3 position)
    {
        if (_judgmentOrbBreakFxPrefab == null) return;

        Instantiate(_judgmentOrbBreakFxPrefab, position, Quaternion.identity);
    }

    private void StopPhaseFx()
    {
        if (_phaseFx != null) _phaseFx.Stop();
        _phaseFx = null;
    }
    #endregion
    private async UniTask BackstepTask(Vector3 direction, CancellationToken token)
    {
        // 0.25초 동안 뒤로 이동
        _rb.velocity = direction * 20f + Vector3.up * _rb.velocity.y;

        try
        {
            await UniTask.Delay(250, delayTiming: PlayerLoopTiming.LastUpdate,
                cancellationToken: token);
        }
        finally
        {
            if (_rb != null)
            {
                _rb.velocity = Vector3.up * _rb.velocity.y;
            }
        }
    }

    private async UniTask FlyWarpSwordTask(Vector3 destination, CancellationToken token)
    {
        const float flightTime = 0.25f;

        Vector3 height = Vector3.up * 1.6f;
        Vector3 from = _rb.position;
        Vector3 forward = Vector3.ProjectOnPlane(destination - from, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        Vector3 start = from + height + right * 1.4f;
        Vector3 end = destination + height;
        Vector3 control = (start + end) * 0.5f + right * 4f;

        GameObject sword = Instantiate(
            _warpSwordPrefab, start, Quaternion.LookRotation(control - start));

        try
        {
            await UniTask.Delay(250, delayTiming: PlayerLoopTiming.LastUpdate,
                cancellationToken: token);

            float elapsed = 0f;
            while (elapsed < flightTime)
            {
                await UniTask.NextFrame(PlayerLoopTiming.LastUpdate, token);
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / flightTime);
                Vector3 a = Vector3.Lerp(start, control, t);
                Vector3 b = Vector3.Lerp(control, end, t);

                sword.transform.SetPositionAndRotation(
                    Vector3.Lerp(a, b, t),
                    Quaternion.LookRotation(b - a));
            }
        }
        finally
        {
            if (sword != null) Destroy(sword);
        }
    }
    private UniTask RunAimedLineSword(CancellationToken token)
    {
        if (!CanFight) return UniTask.CompletedTask;

        Vector3 start = _rb.position;
        Vector3 direction = _player.transform.position - start;
        direction.y = 0f;

        if (TryGetGround(start, out RaycastHit ground))
        {
            start.y = ground.point.y;
        }

        Vector3 end = start + direction.normalized * 15f;
        SDamageInfo damage = new SDamageInfo(100f, false, ERatioSwitch.Constant, false, null);

        _animator?.SetTrigger(SwordSummonTrigger);
        return LineSwordTask(start, end, width: 1.5f, warningTime: 1.5f,
            flightTime:  0.15f, damage: damage, token: token);
    }



    private bool TryGetMoveDestination(Vector3 point, out Vector3 destination)
    {
        destination = point;

        if (!TryGetGround(point, out RaycastHit ground))
        {
            if (_enableLog)
            {
                CLogger.Warn($"[PrideBoss] 바닥 탐색 실패 : 목적지 {point}, Ground Mask {_groundMask.value}");
            }

            return false;
        }

        destination.y = ground.point.y + 0.02f;

        if (_arena != null && !_arena.Contains(destination))
        {
            return false;
        }

        Bounds bounds = _collider.bounds;
        float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        float halfSegment = Mathf.Max(0f, bounds.extents.y - radius);
        Vector3 center = destination + (bounds.center - _rb.position);
        Vector3 verticalOffset = Vector3.up * halfSegment;
        Vector3 bottom = center - verticalOffset;
        Vector3 top = center + verticalOffset;

        bool blocked = Physics.CheckCapsule(bottom, top,
            radius, _moveObstacleMask, QueryTriggerInteraction.Ignore);

        if (blocked && _enableLog)
        {
            CLogger.Warn($"[PrideBoss] 목적지 겹침 : 위치 {destination}, 바닥 {ground.collider.name}");

            Collider[] blockers = Physics.OverlapCapsule(bottom, top,
                radius, _moveObstacleMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < blockers.Length; i++)
            {
                Collider obstacle = blockers[i];
                string layerName = LayerMask.LayerToName(obstacle.gameObject.layer);
                CLogger.Warn($"[PrideBoss] 목적지를 막은 콜라이더 {obstacle.name} ({layerName})");
            }
        }

        return !blocked;
    }

    private bool IsBackstepPathBlocked(Vector3 direction, float distance)
    {
        bool blocked = _rb.SweepTest(direction, out RaycastHit hit,
            distance, QueryTriggerInteraction.Ignore);

        if (blocked && _enableLog && hit.collider != null)
        {
            Collider obstacle = hit.collider;
            string layerName = LayerMask.LayerToName(obstacle.gameObject.layer);
            CLogger.Warn(
                $"[PrideBoss] 백스텝 경로 충돌 : {obstacle.name} ({layerName}), {hit.distance:F2}m / {distance:F2}m");
        }

        return blocked;
    }

    private bool TryGetGround(Vector3 point, out RaycastHit hit)
    {
        return Physics.Raycast(point + Vector3.up * 2f, Vector3.down,
            out hit, 4f, _groundMask, QueryTriggerInteraction.Ignore);
    }

    private float GetPlayerDistanceSquared()
    {
        Vector3 toPlayer = _player.transform.position - transform.position;
        toPlayer.y = 0f;
        return toPlayer.sqrMagnitude;
    }

    private void BeginSwordHold()
    {
        if (_animator == null || !_animator.isActiveAndEnabled) return;

        _animator.ResetTrigger(SwordLaunchTrigger);
        _animator.SetBool(SwordHoldParameter, true);
        _animator.SetTrigger(SwordSummonTrigger);
    }

    private void EndSwordHold(CancellationToken token)
    {
        if (_animator == null || !_animator.isActiveAndEnabled) return;

        _animator.SetBool(SwordHoldParameter, false);
        _animator.ResetTrigger(SwordLaunchTrigger);

        if (!token.IsCancellationRequested && CanContinueAttack)
        {
            _animator.CrossFadeInFixedTime(IdleState, 0.2f, 0);
        }
    }

    private async UniTask SlamTask(Vector3 attackPoint, float readyTime, float fallingTime, float radius, CancellationToken token,
        SDamageInfo damage, float swordScale = 1f, Func<bool> tryConsumeHit = null, bool playSound = true, bool playLaunchAnimation = true)
    {
        if (!CanContinueAttack) return;

        if (TryGetGround(attackPoint, out RaycastHit ground))
        {
            attackPoint.y = ground.point.y;
        }
        else
        {
            attackPoint.y = _rb.position.y;
        }

        CPreviewPattern preview = Instantiate(
            _previewPattern,
            attackPoint + Vector3.up * 0.05f,
            Quaternion.identity);

        GameObject sword = null;

        try
        {
            preview.ShowCirclePattern(64, radius);
            if (playSound && _SE_ShowPattern != null) _SE_ShowPattern.Play();

            Vector3 airPoint = attackPoint + Vector3.up * 12f;
            if (_swordPrefab != null)
            {
                sword = Instantiate(_swordPrefab, airPoint, Quaternion.identity);
                sword.transform.localScale *= swordScale;
            }

            await UniTask.Delay(TimeSpan.FromSeconds(readyTime - fallingTime),
                delayTiming: PlayerLoopTiming.LastUpdate,
                cancellationToken: token);

            if (!CanContinueAttack) return;

            if (playLaunchAnimation)
            {
                _animator?.SetTrigger(SwordLaunchTrigger);
            }

            float deltaT = 0f;
            while (deltaT < fallingTime)
            {
                await UniTask.NextFrame(PlayerLoopTiming.LastUpdate, token);
                deltaT += Time.deltaTime;

                float t = Mathf.Clamp01(deltaT / fallingTime);
                if (sword != null)
                {
                    sword.transform.position = Vector3.Lerp(airPoint, attackPoint, t * t);
                }
            }

            preview.ClearPreview();

            if (HpCurrent <= 0f || _player == null || _player.HpCurrent <= 0f) return;

            PlaySlamImpact(preview.transform.position, radius);
            if (playSound && _SE_FallingSword != null) _SE_FallingSword.Play();

            Vector3 offset = _player.transform.position - attackPoint;
            offset.y = 0f;

            if (!CanContinueAttack) return;
            if (offset.sqrMagnitude <= radius * radius &&
                (tryConsumeHit == null || tryConsumeHit()))
            {
                _player.OnHit(this, damage);
            }

            await UniTask.Delay(200, cancellationToken: token); 
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // 패턴 취소.
        }
        finally
        {
            if (preview != null) Destroy(preview.gameObject);
            if (sword != null) Destroy(sword);
        }
    }

    private void PlaySlamImpact(Vector3 previewCenter, float radius)
    {
        if (_slamImpactPrefab == null) return;

        _activeSlamImpacts.RemoveAll(impact => impact == null || impact.Finished);

        CEspoirBossFxPlayer fx = Instantiate(
            _slamImpactPrefab, previewCenter, Quaternion.identity);

        if (fx.Stage == null)
        {
            CLogger.Warn("착지 이펙트의 Data와 Library 연결 확인필요");
            Destroy(fx.gameObject);
            return;
        }

        fx.screenEffects = false;

        const float referenceRadius = 3.2f;

        fx.stageScale *= radius / referenceRadius;
        fx.ApplyStageScale();

        Vector3 flashOffset = new Vector3(0.810f, -2.445f, 3.099f);
        fx.SetElements(new CEspoirBossFxPlayer.CElementSetting[]
        {
        // 0번 GroundShockDistort는 섬광과 같은 높이에서 시작함
        new CEspoirBossFxPlayer.CElementSetting
        {
            objectIndex = 0,
            offset = flashOffset
        },
        new CEspoirBossFxPlayer.CElementSetting
        {
            isGroup = true,
            group = CEspoirBossFxPlayer.EEffectGroup.Shockwave,
            offset = new Vector3(0.810f, 0f, 3.099f)
        },
        new CEspoirBossFxPlayer.CElementSetting
        {
            isGroup = true,
            group = CEspoirBossFxPlayer.EEffectGroup.Flash,
            offset = flashOffset
        }
        });

        const float impactStart = 0.9f;
        const float impactDuration = 0.5f;

        float spawnedAt = Time.time;
        fx.clock = () => impactStart
            + Mathf.Min(Time.time - spawnedAt, impactDuration);

        Destroy(fx.gameObject, impactDuration);
        fx.Advance(0f);

        Transform groundRing = fx.Stage.Find("fx3_SubBurstField");
        if (groundRing == null)
        {
            CLogger.Warn($"{_slamImpactPrefab.name} : 착지 장판 없음");
            fx.Stop();
            return;
        }

        fx.transform.position += previewCenter - groundRing.position;
        _activeSlamImpacts.Add(fx);
    }

    private async UniTask LineSwordTask(Vector3 start, Vector3 end, float width, float warningTime, float flightTime,
        SDamageInfo damage, CancellationToken token, Func<bool> tryConsumeHit = null, bool playSound = true)
    {
        if (_linePreviewPattern == null || _warpSwordPrefab == null || !CanFight) return;
        if (width <= 0f || warningTime < 0f || flightTime <= 0f) return;

        const float referenceWidth = 1.5f;
        const float referenceTipOffset = 3.3125f;
        const float referenceSwordLength = 7.71f;

        Vector3 direction = end - start;
        direction.y = 0f;

        float length = direction.magnitude;
        if (length < 0.001f) return;

        Vector3 forward = direction / length;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        float swordScale = width / referenceWidth;
        float travelLength = length + referenceSwordLength * swordScale * 0.25f;
        float speed = length / flightTime;
        float tipOffset = referenceTipOffset * swordScale;
        Vector3 swordOffset = Vector3.up * 1.6f - forward * tipOffset;

        CLinePreview preview = Instantiate(_linePreviewPattern);
        GameObject sword = null;

        try
        {
            preview.Show(start, end, width);
            if (playSound && _SE_ShowPattern != null) _SE_ShowPattern.Play();
            await UniTask.Delay(TimeSpan.FromSeconds(warningTime),
                                delayTiming: PlayerLoopTiming.LastUpdate,
                                cancellationToken: token);

            if (!CanFight) return;

            preview.ClearPreview();
            _animator?.SetTrigger(SwordLaunchTrigger);
            sword = Instantiate(_warpSwordPrefab, start + swordOffset,
                                Quaternion.LookRotation(forward));
            if (playSound && _SE_ShootingSword != null) _SE_ShootingSword.Play();
            
            TrailRenderer trail = sword.GetComponent<TrailRenderer>();
            if (trail != null)
            {
                trail.time = 25f / speed;
                trail.widthMultiplier = 2f;
            }

            sword.transform.localScale *= swordScale;

            float elapsed = 0f;
            float distance = 0f;
            bool hit = false;
            Vector3 previousPlayerPosition = _player.transform.position;

            while (distance < travelLength)
            {
                await UniTask.NextFrame(PlayerLoopTiming.LastUpdate, token);
                if (!CanFight) return;

                float previousDistance = distance;
                elapsed += Time.deltaTime;
                distance = Mathf.Min(elapsed * speed, travelLength);
                sword.transform.position = start + forward * distance + swordOffset;

                Vector3 playerPosition = _player.transform.position;
                float before = Vector3.Dot(previousPlayerPosition - start, forward) - previousDistance;
                float after = Vector3.Dot(playerPosition - start, forward) - distance;

                if (!hit && before * after <= 0f)
                {
                    float crossingTime = Mathf.InverseLerp(before, after, 0f);
                    float crossingDistance = Mathf.Lerp(previousDistance, distance, crossingTime);
                    Vector3 crossingPoint = Vector3.Lerp(
                        previousPlayerPosition, playerPosition, crossingTime);
                    float sideDistance = Mathf.Abs(Vector3.Dot(crossingPoint - start, right));

                    if (crossingDistance <= length && sideDistance <= width * 0.5f)
                    {
                        if (crossingDistance <= length && sideDistance <= width * 0.5f)
                        {
                            hit = true;

                            if (tryConsumeHit == null || tryConsumeHit())
                            {
                                _player.OnHit(this, damage);
                            }
                        }
                    }
                }

                previousPlayerPosition = playerPosition;
            }

            if (trail != null)
            {
                trail.emitting = false;

                foreach (MeshRenderer blade in sword.GetComponentsInChildren<MeshRenderer>())
                {
                    blade.enabled = false;
                }

                await UniTask.Delay(TimeSpan.FromSeconds(trail.time),
                    delayTiming: PlayerLoopTiming.LastUpdate, cancellationToken: token);
            }
        }
        finally
        {
            if (preview != null) Destroy(preview.gameObject);
            if (sword != null) Destroy(sword);
        }
    }

    private async UniTask MoveBossTask(Vector3 destination, float duration, CancellationToken token)
    {
        Vector3 start = _rb.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            await UniTask.NextFrame(PlayerLoopTiming.LastUpdate, token);
            if (!CanFight) return;

            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            _rb.position = Vector3.Lerp(start, destination, t);
        }
    }

    private bool IsPlayerInBattleArea(float fallbackRadius)
    {
        if (_arena != null)
        {
            return _arena.Contains(_player.transform.position);
        }

        return GetPlayerDistanceSquared() <= fallbackRadius * fallbackRadius;
    }

    private void OnDisable()
    {
        ResetBattleState();
    }

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    public void ResetBattleState()
    {
        StopPhaseFx();
        _groggyRemainTime = 0f;
        IsGroggy = false;

        if (_phase == EPridePhase.Finisher && _hpCurrent > 0)
        {
            _phase = EPridePhase.FinalJudgmentPending;
        }

        foreach (CEspoirBossFxPlayer fx in _activeSlamImpacts)
        {
            if (fx != null) fx.Stop();
        }

        if (_animator != null && _animator.isActiveAndEnabled)
        {
            _animator.ResetTrigger(BackstepTrigger);
            _animator.ResetTrigger(SwordSummonTrigger);
            _animator.ResetTrigger(SwordLaunchTrigger);
            _animator.ResetTrigger(RiseTrigger);
            _animator.ResetTrigger(LandTrigger);
            _animator.ResetTrigger(HealStartTrigger);
            _animator.ResetTrigger(HealEndTrigger);
            _animator.ResetTrigger(KneelTrigger);
            _animator.ResetTrigger(KneelEndTrigger);
            _animator.ResetTrigger(DeathTrigger);
            _animator.SetBool(SwordHoldParameter, false);

            if (HpCurrent > 0f && (_animator.IsInTransition(0) ||
                _animator.GetCurrentAnimatorStateInfo(0).fullPathHash != IdleState))
            {
                _animator.Play(IdleState, 0, 0f);
            }
        }

        _activeSlamImpacts.Clear();
        CancellationTokenSource source = _cts;
        _cts = null;

        if (source == null) return;

        source.Cancel();
        source.Dispose();

        if (_SE_ShowPattern != null) _SE_ShowPattern.Stop();
        if (_SE_FallingSword != null) _SE_FallingSword.Stop();
        if (_SE_ShootingSword != null) _SE_ShootingSword.Stop();
        if (_SE_ChargingLaser != null) _SE_ChargingLaser.Stop();
        if (_SE_ShootingLaser != null) _SE_ShootingLaser.Stop();
        if (_SE_Groggy != null) _SE_Groggy.Stop();
        if (_SE_HealPattern != null) _SE_HealPattern.Stop();
        if (_SE_JudgementPattern != null) _SE_JudgementPattern.Stop();
    }
    public void ResetPhaseState()
    {
        _phase = EPridePhase.Phase1;
        _mainCounter = 0;
        _nextMainPattern = EPrideMainPattern.TripleLaser;
        if (_SE_JudgementFailed != null) _SE_JudgementFailed.Stop();
    }

    public override void GetDamage(float value, ERatioSwitch ratioSwitch = ERatioSwitch.Constant, bool PenetrateInvincible = false)
    {
        if (HpCurrent <= 0f || (IsInvincible && !PenetrateInvincible) || _phase == EPridePhase.Finisher) return;

        if (value <= 0f)
        {
            base.GetDamage(value, ratioSwitch, PenetrateInvincible);
            return;
        }

        float incomingDamage = value;

        switch (ratioSwitch)
        {
            case ERatioSwitch.Constant:
                break;
            case ERatioSwitch.ByMax:
                incomingDamage *= HpMax;
                break;
            case ERatioSwitch.ByCurrent:
                incomingDamage *= HpCurrent;
                break;
            case ERatioSwitch.ByLose:
                incomingDamage *= HpMax - HpCurrent;
                break;
            default:
                base.GetDamage(value, ratioSwitch, PenetrateInvincible);
                return;
        }

        if (IsGroggyPhase)
        {
            if (_groggyRemainTime > 0f)
            {
                _groggyRemainHp = Mathf.Max(0f, _groggyRemainHp - incomingDamage);
            }
            return;
        }

        bool isFirstPhase = _phase == EPridePhase.Phase1;
        float thresholdHp = HpMax * (isFirstPhase ? 0.5f : 0.025f);
        float damageToThreshold = Mathf.Max(0f, HpCurrent - thresholdHp);

        if (incomingDamage < damageToThreshold)
        {
            base.GetDamage(value, ratioSwitch, PenetrateInvincible);
            return;
        }

        base.GetDamage(damageToThreshold, ERatioSwitch.Constant, PenetrateInvincible);
        _phase = isFirstPhase ? EPridePhase.RecoveryPending : EPridePhase.FinalJudgmentPending;
    }

    protected override void Die()
    {
        base.Die();
        if (HpCurrent > 0f) return;

        CGameManager.Instance?.SetBossCleared(BossId);
        CGameManager.Instance?.EndBossTimer(BossId, true);
    }

    public bool TryGetGroggyInfo(out float remainHp, out float maxHp,
    out float remainTime, out float maxTime)
    {
        bool isJudgment = _phase == EPridePhase.FinalJudgmentPending;

        remainHp = _groggyRemainHp;
        maxHp = isJudgment ? _judgmentGroggyHpMax : _recoveryGroggyHpMax;
        remainTime = _groggyRemainTime;
        maxTime = isJudgment ? JudgmentDuration : RecoveryDuration;

        return IsGroggyPhase && _groggyRemainTime > 0f;
    }
}