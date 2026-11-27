
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Triggers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

public class CSlothBoss : CEnemy, IBoss
{
    public enum ESlothBossState
    {
        Idle = 0,
        NormalAttack,
        SubAttack,
        MainAttack,
        Dead,
        Grogi
    }

    #region 인스팩터
    [SerializeField] private CPlayer _player;

    [Header("Attack Player")]
    [SerializeField] private LayerMask _targetMask;
    [SerializeField] private bool _sightCheck = false;
    [SerializeField] private LayerMask _obtacleMask;
    [SerializeField] private Collider[] _hitCollider = new Collider[2];


    [Header("Boss State")]
    [SerializeField] private ESlothBossState _bossState;
    [SerializeField] private CPreviewPattern _previewPattern;
    [SerializeField] private int _mainCount = 0;

    [Header("Boss Info")]
    [SerializeField] private Animator _animator;
    [SerializeField] private float _rotateSpeed = 1000.0f;
    [SerializeField] private float _atkTime = 5.0f;
    [SerializeField] private float _normalAtkRange = 9.0f;
    [SerializeField] private float _detectedRange = 15.0f;
    [SerializeField] private float _returnDelayTime = 3f;

    [Header("Boss GrogiState")]
    [SerializeField] private float _grogiTime = 5.0f;

    [Header("Pattern Range")]
    [SerializeField] private float _whirlRange = 15f;
    [SerializeField] private float _whirlInner = 7f;
    [SerializeField] private float _donutRange = 18f;
    [SerializeField] private float _donutInner = 10f;
    [SerializeField] private CPreviewPattern _previewInnerPattern;

    [Header("AlternationAttack")]
    [SerializeField] private float _areaAngle = 30f;
    [SerializeField] private int _divideArea = 6;

    [Header("Thunder Voltalge Cylinder")]
    [SerializeField] private float _thunderOrbitRadius = 11f;
    [SerializeField] private float _thunderSpeed = 90f;
    [SerializeField] private float _thunderHitRadius = 2.5f;


    #endregion

    #region Animator 변수

    private int _firstNorm = Animator.StringToHash("nAttack01");
    private int _secondNorm = Animator.StringToHash("nAttack02");
    private int _thirdNorm = Animator.StringToHash("nAttack03");

    private int _firstSub = Animator.StringToHash("sAttack01");
    private int _secondSub = Animator.StringToHash("sAttack02");
    private int _thirdSub = Animator.StringToHash("sAttack03");
    private int _forthSub = Animator.StringToHash("sAttack04");

    private int _firstMain = Animator.StringToHash("mAttack01");
    private int _secondMain = Animator.StringToHash("mAttack02");
    private int _thirdMain = Animator.StringToHash("mAttack03");
    private int _finalMain = Animator.StringToHash("mAttack04");

    private int _grogiState = Animator.StringToHash("tGrogi");

    #endregion



    #region 내부변수

    private ESlothBossState _startState;
    private ESlothBossState _currentState;


    private CSoundManager _soundManager;
    private Transform _target;
    private Vector3 _patternPos;
    private Vector3 _savePos;

    private float _atkTimer;
    private float _delayTimer;
    private float _grogiTimer;

    private int normalAtk;
    private int normalCount;
    private int subAtk;
    private int mainAtk;
    private bool _onAttack;

    private bool _isNormal;
    private bool _isSub;
    private bool _isMain;
    private bool _isDead;
    private bool _onGrogi;

    private const float GroggyDamageRatio = 0.035f;
    private float _groggyStartHp;
    private float _groggyDuration;
    private float _groggyEndTime;
    private CEspoirBossFxDriver _fxDriver;
    private CancellationTokenSource _cts;

    public bool IsGroggy => _onGrogi;
    #endregion


    protected override void Awake()
    {
        base.Awake();

        if (_player == null)
        {
            _player = FindFirstObjectByType<CPlayer>();

            if (_player == null)
            {
                CLogger.Error("플레이어 찾을 수 없음");
            }

        }

        if (_soundManager == null)
        {
            _soundManager = FindFirstObjectByType<CSoundManager>();
            if (_soundManager == null)
            {
                CLogger.Warn("사운드 매니저 찾을 수 없음");
            }

        }



        _target = _player.transform;
        _previewPattern = this.GetComponentInChildren<CPreviewPattern>();
        _previewInnerPattern = this.GetComponentInChildren<CPreviewPattern>();
        _animator = this.GetComponent<Animator>();
        _patternPos = _previewPattern.transform.position;

        _fxDriver = GetComponentInChildren<CEspoirBossFxDriver>();

    }

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    private void OnDisable()
    {
        _battleVersion++;
    }

    protected override void Start()
    {
        base.Start();
        _startState = ESlothBossState.Idle;
        _currentState = _bossState;

    }


    protected override void Update()
    {
        base.Update();



    }

    private void FixedUpdate()
    {
        if (_isDead || HpCurrent <= 0f)
        {

            _currentState = ESlothBossState.Dead;
            return;
        }
        if (_onGrogi == true)
        {

            _currentState = ESlothBossState.Grogi;
            BossState();


            return;
        }
        if (_groggyDuration > 0f) return;

        LookAtPlayer();

        if (_atkTimer > _atkTime)
        {
            BossState();

        }
        else
        {
            _atkTime = _isMain ? 7.3f : 5f;
            _atkTimer += Time.fixedDeltaTime;
        }
    }


    private void LookAtPlayer()
    {
        Vector3 target = new Vector3(_target.position.x, this.transform.position.y, _target.position.z);

        Vector3 lookPlayer = target - this.transform.position;

        float distance = lookPlayer.magnitude;

        if (distance <= _detectedRange)
        {
            if (_onAttack == false)
            {
                if (MainPatternExecute())
                {
                    return;
                }

                if (SNPatternExecute(distance))
                {
                    return;
                }


                Vector3 dir = lookPlayer.normalized;
                Quaternion lookRotation = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRotation, _rotateSpeed * Time.deltaTime);


            }


            else
            {
                if (_isMain == true)
                _returnDelayTime = 7f;

                else
                _returnDelayTime = (_mainCount == 4) ? 1.5f : 2.5f;
                                
            }

        }





    }

    private bool MainPatternExecute()
    {
        float currentHP = this.HpCurrent;

        if (currentHP < this.HpMax * 0.8f && _mainCount == 0)
        {
            CallingMainPattern(2);

            return true;
        }

        else if (currentHP < this.HpMax * 0.6f && _mainCount == 1)
        {
            CallingMainPattern(0);
            return true;
        }

        else if (currentHP < this.HpMax * 0.4f && _mainCount == 2)
        {
            CallingMainPattern(1);
            return true;
        }

        else if (currentHP < this.HpMax * 0.2f && _mainCount == 3)
        {
            CallingMainPattern(3);
            return true;
        }

        return false;
    }

    private void CallingMainPattern(int mainIndex)
    {
        _isSub = false;
        _isNormal = false;

        if (_delayTimer > _returnDelayTime)
        {
            _isMain = true;
            mainAtk = mainIndex;
            _currentState = ESlothBossState.MainAttack;
            _delayTimer = 0;

        }

        else
        {
            _delayTimer += Time.deltaTime;
        }
    }


    private bool SNPatternExecute(float distance)
    {

        if (distance <= _normalAtkRange && normalCount <= 3)
        {
            _isSub = false;
            _isMain = false;

            if (_delayTimer > _returnDelayTime)
            {
                _isNormal = true;

                _currentState = ESlothBossState.NormalAttack;
                _delayTimer = 0;
                normalCount++;
                return true;
            }

            else
            {
                _delayTimer += Time.deltaTime;
            }

        }

        else
        {

            if (_delayTimer > _returnDelayTime)
            {
                _isSub = true;
                _currentState = ESlothBossState.SubAttack;
                _delayTimer = 0;
                normalCount = 0;
                return true;
            }

            else
            {
                _isNormal = false;
                _isMain = false;
                _delayTimer += Time.deltaTime;
            }

        }

        return false;
    }



    private void NormalAttackPattern()
    {
        if (_isNormal == false)
        {
            return;
        }

        int prevAtk = normalAtk;
        _isSub = false;
        _isMain = false;

        normalAtk = Random.Range(0, 4);
        _onAttack = true;

        if(normalAtk == prevAtk)
        {
            normalAtk = (prevAtk + 1) % 4;

        }


        CLogger.Log($"Normal Attack Pattern : {normalAtk}");

        switch (normalAtk)
        {
            case 0:
                _animator.SetTrigger(_firstNorm);
                NormalAttackDelay().Forget();

                break;

            case 1:
                _animator.SetTrigger(_secondNorm);
                NormalAttackDelay().Forget();
                break;

            case 2:
                _animator.SetTrigger(_thirdNorm);
                NormalAttackDelay().Forget();
                break;

            case 3:
                _animator.SetTrigger(_firstSub);
                TwiceAttack().Forget();

                break;

        }
        _delayTimer = 0f;
    }

    private void SubAttackPattern()
    {
        if (_isSub == false)
        {
            return;
        }

        int prevSub = subAtk;

        _isMain = false;
        _isNormal = false;

        subAtk = (normalCount >= 3) ? 1 : Random.Range(1, 4);
        
        if(prevSub == subAtk)
        {
            subAtk++;
            if(subAtk > 3)
            {
                subAtk = (prevSub % 3) + 1;
            }

        }


        _onAttack = true;

        CLogger.Log($"Sub Attack Pattern : {subAtk}");
        switch (subAtk)
        {
            case 1:
                WhirlWindAttack().Forget();

                break;

            case 2:
                AlternationAttack().Forget();

                break;

            case 3:
                DonutAttack().Forget();

                break;
        }
        _delayTimer = 0f;
    }

    private void MainAttackPattern()
    {
        if (_isMain == false)
        {
            return;
        }
        _mainCount++;
        _isNormal = false;
        _isSub = false;

        _onAttack = true;

        CLogger.Log($"Main Attack Pattern : {mainAtk}");

        switch (mainAtk)
        {
            case 0:
                _animator.SetTrigger(_firstMain);
                LikeOneShotAttack().Forget();
                break;

            case 1:

                FindSafeZoneAttack().Forget();
                break;

            case 2:
                _animator.SetTrigger(_thirdMain);
                LightningAttack().Forget();

                break;

            case 3:
                _animator.SetTrigger(_finalMain);
                LikeOneShotAttack().Forget();
                break;


        }

        _delayTimer = 0f;

    }



    private void BossState()
    {
        if (_player == null)
        {
            return;
        }

        switch (_currentState)
        {
            case ESlothBossState.Idle:
                break;

            case ESlothBossState.NormalAttack:
                NormalAttackPattern();

                _isNormal = false;
                break;

            case ESlothBossState.SubAttack:
                SubAttackPattern();

                _isSub = false;
                break;

            case ESlothBossState.MainAttack:
                MainAttackPattern();

                _isMain = false;
                break;

            case ESlothBossState.Dead:
                _animator.SetTrigger("tDie");
                break;

            case ESlothBossState.Grogi:
                _onAttack = true;
                GrogiState();

                break;
        }


    }



    #region NormalAttack Patterns
    private async UniTask NormalAttackDelay()
    {
        int battleVersion = _battleVersion;

        await UniTask.Delay(3000);
        if (battleVersion != _battleVersion) return;
        _atkTime = 0;

        _onAttack = false;

    }

    private void DoubleHookAttack()
    {

        int hit = NormalFindPlayer(3f);

        for (int i = 0; i < hit; i++)
        {
            if (_hitCollider == null)
            {
                continue;
            }

            CPlayer player = _hitCollider[i].GetComponent<CPlayer>();

            if (player == null || player.HpCurrent <= 0)
            {
                continue;
            }


            SDamageInfo damageToPlayer = new SDamageInfo(0.1f, ERatioSwitch.ByMax);
            float lastHp = player.HpCurrent;

            player.OnHit(this, damageToPlayer);

            CLogger.Log($"{player.name}의 현재 체력 : {player.HpCurrent}");

        }



    }

    private void BackSpinAttack()
    {
        int hit = NormalFindPlayer(7f);

        Vector3 spinAreaDir = -this.transform.right;

        for (int i = 0; i < hit; i++)
        {
            if (_hitCollider == null)
            {
                continue;
            }

            CPlayer player = _hitCollider[i].GetComponent<CPlayer>();

            if (player == null || player.HpCurrent <= 0)
            {
                continue;
            }

            Vector3 damageDir = player.transform.position - this.transform.position;

            float area = Vector3.Dot(spinAreaDir, damageDir);

            if (area >= 0f)
            {
                SDamageInfo damageToPlayer = new SDamageInfo(0.2f, ERatioSwitch.ByMax);
                float lastHp = player.HpCurrent;

                player.OnHit(this, damageToPlayer);

                CLogger.Log($"{player.name}의 현재 체력 : {player.HpCurrent}");

            }


        }

    }

    private void SlamUpperAttack()
    {

        int hit = NormalFindPlayer(5f);

        for (int i = 0; i < hit; i++)
        {
            if (_hitCollider == null)
            {
                continue;
            }

            CPlayer player = _hitCollider[i].GetComponent<CPlayer>();

            if (player == null || player.HpCurrent <= 0)
            {
                continue;
            }

            SDamageInfo damageToPlayer = new SDamageInfo(0.15f, ERatioSwitch.ByMax);

            player.OnHit(this, damageToPlayer);

            CLogger.Log($"{player.name}의 현재 체력 : {player.HpCurrent}");
        }

    }
    #endregion


    #region SubPattern
    private async UniTask WhirlWindAttack()
    {
        int battleVersion = _battleVersion;

        if (_previewPattern != null)
        {
            _previewPattern.ShowDonutPattern(20, 15f, 3f);
        }
        else
        {
            CLogger.Error("PatternView 비어있음");
        }

        await UniTask.Delay(2300);
        if (battleVersion != _battleVersion) return;
        _atkTimer = -0.8f;

        if (_previewPattern != null)
        {
            _previewPattern.ClearPreview();
        }


        _animator.SetTrigger(_secondSub);

        await UniTask.Delay(2500);
        if (battleVersion != _battleVersion) return;

        _onAttack = false;

    }

    private void WhirlWindDamage()
    {

        int hit = FindPlayer(_whirlRange);

        for (int i = 0; i < hit; i++)
        {
            if (_hitCollider[i] == null)
            {
                continue;
            }

            CPlayer player = _hitCollider[i].GetComponent<CPlayer>();
            if (player == null || player.HpCurrent <= 0)
            {
                continue;
            }

            float distance = Vector3.Distance
                (
                    new Vector3(this.transform.position.x, 0f, this.transform.position.z),
                    new Vector3(player.transform.position.x, 0f, player.transform.position.z)
                );

            if (distance >= 3f && distance <= _whirlRange)
            {
                SDamageInfo damageToPlayer = new SDamageInfo(0.1f, true, ERatioSwitch.ByMax);

                player.OnHit(this, damageToPlayer);


                CLogger.Log($"{player.name}의 현재 체력 : {player.HpCurrent}");
            }

        }
    }

    private async UniTask DonutAttack()
    {
        int battleVersion = _battleVersion;
        if (_previewPattern != null)
        {
            _previewPattern.ShowDoubleDonutPattern(20, 18f, 10f, 7f, 3f);
        }

        else
        {
            CLogger.Error("PatternView 비어있음");
        }

        await UniTask.Delay(2000);
        if (battleVersion != _battleVersion) return;
        _atkTimer = 0;

        if (_previewPattern != null)
        {
            _previewPattern.ClearPreview();
        }

        _animator.SetTrigger(_forthSub);

        _onAttack = true;
        await UniTask.Delay(2000);
        if (battleVersion != _battleVersion) return;

        _onAttack = false;
    }

    private void DonutDamage()
    {
        int hit = FindPlayer(_donutRange);

        for (int i = 0; i < hit; i++)
        {
            if (_hitCollider[i] == null)
            {
                continue;
            }
            CPlayer player = _hitCollider[i].GetComponent<CPlayer>();
            if (player == null || player.HpCurrent <= 0)
            {
                continue;
            }

            float distance = Vector3.Distance
            (
                new Vector3(this.transform.position.x, this.transform.position.y, this.transform.position.z),
                new Vector3(player.transform.position.x, this.transform.position.y, player.transform.position.z)
            );

            if ((distance >= 10f && distance <= 18f) || (distance >= 3f && distance <= 7f))
            {
                SDamageInfo damageToPlayer = new SDamageInfo(0.25f, true, ERatioSwitch.ByMax);

                player.OnHit(this, damageToPlayer);

                CLogger.Log($"{player.name}의 현재 체력 : {player.HpCurrent}");
            }
        }
    }

    private async UniTask TwiceAttack()
    {
        int battleVersion = _battleVersion;
        if (_previewPattern != null)
        {
            _previewPattern.transform.position = _player.transform.position;
            _previewPattern.ShowCirclePattern(20, 8.5f);
            _savePos = _previewPattern.transform.position;
        }

        else
        {
            CLogger.Error("PatternView 비어있음");
        }

        await UniTask.Delay(1500);
        if (battleVersion != _battleVersion) return;
        _atkTimer = 0;

        if (_previewPattern != null)
        {
            _previewPattern.ClearPreview();
            _previewPattern.transform.position = _patternPos;
        }


        await UniTask.Delay(3200);
        if (battleVersion != _battleVersion) return;
        _onAttack = false;
    }

    private void DoubleHookDamage()
    {

        int hit = NormalFindPlayer(3.5f);

        for (int i = 0; i < hit; i++)
        {
            if (_hitCollider == null)
            {
                continue;
            }

            CPlayer player = _hitCollider[i].GetComponent<CPlayer>();

            if (player == null || player.HpCurrent <= 0)
            {
                continue;
            }

            SDamageInfo damageToPlayer = new SDamageInfo(0.15f, ERatioSwitch.ByMax);
            float lastHp = player.HpCurrent;

            player.OnHit(this, damageToPlayer);

            CLogger.Log($"{player.name}의 현재 체력 : {player.HpCurrent}");
        }



    }

    private void DoubleHandStampDamage()
    {
        int hit = FindPlayer(13f);

        for (int i = 0; i < hit; i++)
        {
            if (_hitCollider[i] == null)
            {
                continue;
            }
            CPlayer player = _hitCollider[i].GetComponent<CPlayer>();
            if (player == null || player.HpCurrent <= 0)
            {
                continue;
            }


            float atkRange = Vector3.Distance(_savePos, player.transform.position);

            if (atkRange <= 8.0f)
            {
                SDamageInfo damagePlayer = new SDamageInfo(0.30f, true, ERatioSwitch.ByMax);

                player.OnHit(this, damagePlayer);

                CLogger.Log($"{player.name}의 현재 체력 : {player.HpCurrent}");

            }


        }
    }

    private async UniTask AlternationAttack()
    {
        int battleVersion = _battleVersion;
        _onAttack = true;
        if (_previewPattern != null)
        {
            _previewPattern.ShowMultiArcPattern(_divideArea, 15f, _areaAngle, 30f);
        }

        else
        {
            CLogger.Error("PatternView �������");
        }

        await UniTask.Delay(1000);
        if (battleVersion != _battleVersion) return;

        if (_previewPattern != null)
        {
            _previewPattern.ClearPreview();
        }

        if (_previewPattern != null)
        {
            _previewPattern.ShowMultiArcPattern(_divideArea, 15f, _areaAngle, 0f);
        }

        else
        {
            CLogger.Error("PatternView �������");
        }

        await UniTask.Delay(1000); ;
        if (battleVersion != _battleVersion) return;

        if (_previewPattern != null)
        {
            _previewPattern.ClearPreview();
        }

        _atkTimer = 0f;

        _animator.SetTrigger(_thirdSub);

        await UniTask.Delay(3100);
        if (battleVersion != _battleVersion) return;
        _onAttack = false;
    }

    private void AlternationDamage(float offsetAngle)
    {
        int hit = FindPlayer(15f);

        for (int i = 0; i < hit; i++)
        {
            if (_hitCollider[i] == null)
            {
                continue;
            }

            CPlayer player = _hitCollider[i].GetComponent<CPlayer>();
            if (player == null || player.HpCurrent <= 0)
            {
                continue;
            }

            float distance = Vector3.Distance
                (
                    new Vector3(this.transform.position.x, 0f, this.transform.position.z),
                    new Vector3(player.transform.position.x, 0f, player.transform.position.z)
                );

            if (distance > 15f)
            {
                continue;
            }

            Vector3 playerDir = player.transform.position - this.transform.position;
            playerDir.y = 0;

            float playerAngle = Vector3.SignedAngle(this.transform.forward, playerDir, Vector3.up);

            bool isHit = false;
            float angleStep = 360f / _divideArea;

            for (int j = 0; j < _divideArea; j++)
            {
                float standardAngle = offsetAngle + (j * angleStep);
                float tickAngle = Mathf.DeltaAngle(standardAngle, playerAngle);

                if (Mathf.Abs(tickAngle) <= _areaAngle * 0.5f)
                {
                    isHit = true;
                    break;
                }
            }


            if (isHit)
            {
                SDamageInfo damagePlayer = new SDamageInfo(0.2f, true, ERatioSwitch.ByMax);
                player.OnHit(this, damagePlayer);

                CLogger.Log($"{player.name}의 현재 체력 : {player.HpCurrent}");
            }

        }


    }

    private void AlternationDamage_First()
    {
        AlternationDamage(0f);


    }

    private void AlternationDamage_Second()
    {
        AlternationDamage(30f);
    }

    #endregion

    #region MainPattern

    private async UniTask LikeOneShotAttack()
    {
        int battleVersion = _battleVersion;
        _onAttack = true;

        if (_previewPattern != null)
        {
            _previewInnerPattern.ShowCirclePattern(100, 30f);
        }


        await UniTask.Delay(4000);
        if (battleVersion != _battleVersion) return;
        if (_previewPattern != null)
        {
            _previewPattern.ClearPreview();
        }

        _atkTimer = 0;

        _onAttack = false;

    }

    private void LikeOneShotDamage(float damage)
    {
        int hits = FindPlayer(30);

        for (int i = 0; i < hits; i++)
        {
            if (_hitCollider[i] == null)
            {
                continue;
            }

            CPlayer player = _hitCollider[i].GetComponent<CPlayer>();
            if (player == null || player.HpCurrent <= 0)
            {
                continue;
            }

            SDamageInfo damageToPlayer = new SDamageInfo(damage, true, ERatioSwitch.ByCurrent);
            float lastHp = player.HpCurrent;

            player.OnHit(this, damageToPlayer);

            CLogger.Log($"{player.name}의 현재 체력 : {player.HpCurrent}");

        }

    }

    private void LikeOneShotAttack_damage()
    {
        float damage = _mainCount == 1 ? 0.6f : 0.95f;
        LikeOneShotDamage(damage);
    }

    private void LikeOneShotAttack_SmallDamage()
    {
        float damage = 0.4f;
        LikeOneShotDamage(damage);
    }

    private void LikeOneShotAttack_BigDamage()
    {
        float damage = 0.95f;
        LikeOneShotDamage(damage);
    }


    private void FreezeAnime()
    {
        if (_mainCount == 3)
        {
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        FreezeAnimation(6f, _cts.Token).Forget();

    }

    private async UniTaskVoid FreezeAnimation(float duration, CancellationToken cancelAnima)
    {
        CLogger.Warn("애니메이션 이벤트 실행됨");
        _animator.speed = 0.2f;

        if (_player == null)
        {
            CLogger.Warn("플레이어를 참조하지 않았습니다.");
            return;
        }

        CancellationTokenSource source = _cts;
        float patternStartHP = this.HpCurrent;

        _groggyStartHp = patternStartHP;
        _groggyDuration = duration;
        _groggyEndTime = Time.unscaledTime + duration;
        _animator.speed = 0.2f;

        try
        {
            await UniTask.WaitUntil(
                () => GetGroggyRemainHp() <= 0f ||
                Time.unscaledTime >= _groggyEndTime,
                cancellationToken: cancelAnima
                );

            if (isActiveAndEnabled && HpCurrent > 0f && GetGroggyRemainHp() <= 0f)
            {
                _animator.speed = 1f;
                _onGrogi = true;
                _fxDriver?.StopAll();
            }
        }

        catch (OperationCanceledException)
        {
            _fxDriver?.StopAll();
            return;
        }

        finally
        {
            if (this != null && ReferenceEquals(_cts, source))
            {
                _groggyDuration = 0f;
                _animator.speed = 1f;
            }
        }
    }


    private async UniTask FindSafeZoneAttack()
    {
        int battleVersion = _battleVersion;
        _onAttack = true;
        _animator.SetTrigger(_secondMain);

        _atkTimer = 0;

        await UniTask.Delay(TimeSpan.FromSeconds(7.2f));
        if (battleVersion != _battleVersion) return;

        _atkTimer = 0;
        _onAttack = false;

    }



    private void FindSafeZoneDamage()
    {
        int hit = FindPlayer(25);

        List<Vector3> safeZones = new List<Vector3>();

        for (int i = 0; i < 4; i++)
        {
            float safeAngle = 45f + (i * 90f);

            Quaternion rot = Quaternion.Euler(0f, safeAngle, 0f);

            Vector3 safeDir = rot * this.transform.forward;

            Vector3 safePos = this.transform.position + (safeDir * 18f);

            safeZones.Add(safePos);
        }


        for (int i = 0; i < hit; i++)
        {
            if (_hitCollider[i] == null)
            {
                continue;
            }

            CPlayer player = _hitCollider[i].GetComponent<CPlayer>();
            if (player == null || player.HpCurrent <= 0)
            {
                continue;
            }

            bool isHit = true;

            Vector3 playerPos = new Vector3(player.transform.position.x, this.transform.position.y, player.transform.position.z);

            for (int j = 0; j < safeZones.Count; j++)
            {
                Vector3 szPos = new Vector3(safeZones[j].x, this.transform.position.y, safeZones[j].z);
                float distance = Vector3.Distance(szPos, playerPos);

                if (distance <= 4f)
                {
                    isHit = false;

                    break;
                }


            }

            if (isHit == true)
            {
                SDamageInfo playerToDamage = new SDamageInfo(0.6f, ERatioSwitch.ByMax);
                float currentHP = player.HpCurrent;

                player.OnHit(this, playerToDamage);

                CLogger.Log($"현재 플레이어 체력 : {player.HpCurrent}");
            }

        }


    }


    private async UniTask LightningAttack()
    {
        int battleVersion = _battleVersion;
        _onAttack = true;
        if (_previewPattern != null)
        {
            _previewPattern.ShowDonutPattern(100, 14f, 10f);
        }
        _atkTimer = 0;

        await UniTask.Delay(2000);
        if (battleVersion != _battleVersion) return;

        if (_previewPattern != null)
        {
            _previewPattern.ClearPreview();
        }
        _atkTimer = 0;

        await UniTask.Delay(5000);
        if (battleVersion != _battleVersion) return;

        _onAttack = false;

    }
       
    private void EarthShaker()
    {
        int hit = FindPlayer(20);

        List<Vector3> damageZones = new List<Vector3>();

        for (int i = 0; i < 4; i++)
        {
            float shakerAngle = 45f + (i * 90f);

            Quaternion rot = Quaternion.Euler(0f, shakerAngle, 0f);

            Vector3 damagePosDir = rot * this.transform.forward;

            Vector3 damagePos = this.transform.position + (damagePosDir * 11f);
            
            damageZones.Add(damagePos);
        }


        for (int i = 0; i < hit; i++)
        {
            if (_hitCollider[i] == null)
            {
                continue;
            }

            CPlayer player = _hitCollider[i].GetComponent<CPlayer>();
            if (player == null || player.HpCurrent <= 0)
            {
                continue;
            }

            Vector3 playerPos = player.transform.position - this.transform.position;

            playerPos.y = 0;

            bool isHit = false;

            for(int j = 0; j < damageZones.Count; j++)
            {
                float damageRange = Vector3.Distance(damageZones[i], playerPos);

                if(Mathf.Abs(damageRange) <= 4f)
                {
                    isHit = true;
                    break;
                }


            }

            if(isHit == true)
            {
                SDamageInfo damageToPlayer = new SDamageInfo(0.3f, true, ERatioSwitch.ByMax);
                float lastHp = player.HpCurrent;

                player.OnHit(this, damageToPlayer);

                CLogger.Log($"{player.name}의 현재 체력 : {player.HpCurrent}");
            }


        }

    }


    private void ThunderVoltage()
    {
        List<Vector3> circleCenter = new List<Vector3>();

        for (int i = 0; i < 4; i++)
        {
            float startAngle = 45f + (i * 90f);

            Quaternion rot = Quaternion.Euler(0f, startAngle, 0f);

            Vector3 dir = rot * this.transform.forward;

            Vector3 currentPos = this.transform.position + (dir * _thunderOrbitRadius);

            circleCenter.Add(currentPos);
        }

        int hitCount = 0;

        for (int i = 0; i < circleCenter.Count; i++)
        {
            int hit = Physics.OverlapSphereNonAlloc(circleCenter[i], _thunderHitRadius,_hitCollider,_targetMask);
            
            hitCount += hit;

        }



        for (int i = 0; i < hitCount; i++)
        {
            if (_hitCollider[i] == null)
            {
                continue;
            }

            CPlayer player = _hitCollider[i].GetComponent<CPlayer>();
            if (player == null || player.HpCurrent <= 0)
            {
                continue;
            }

           
            bool isHit = false;

            for (int j = 0; j < 4; j++)
            {
                Vector3 voltagePos = circleCenter[j];

                Vector2 voltageXZ = new Vector2(voltagePos.x, voltagePos.z);
                Vector2 playerXZ = new Vector2(player.transform.position.x, player.transform.position.z);

                float distance = Vector3.Distance(voltageXZ, playerXZ);
                            
                if(distance <= _thunderHitRadius)
                {
                    isHit = true;

                    break;
                }

            }

            if (isHit == true)
            {
                SDamageInfo damageToPlayer = new SDamageInfo(0.2f, true, ERatioSwitch.ByCurrent);
                float lastHp = player.HpCurrent;

                player.OnHit(this, damageToPlayer);

                CLogger.Log($"{player.name}의 현재 체력 : {player.HpCurrent}");
            }



        }


    }

    private void ShootLightningBeam()
    {
        Vector3 boxSize = new Vector3(6f, 6f, 25f);

        Vector3 startPos = this.transform.forward * 1f;

        Vector3 boxCenter = (this.transform.position + startPos) + (this.transform.forward * (boxSize.z * 0.5f));

        Quaternion boxRot = this.transform.rotation;

        int hitCount = Physics.OverlapBoxNonAlloc(boxCenter, boxSize * 0.5f, _hitCollider, boxRot , _targetMask);

        for (int i = 0; i < hitCount; i++) 
        {
            if (_hitCollider[i] == null)
            {
                continue;
            }

            CPlayer player = _hitCollider[i].GetComponent<CPlayer>();
            if(player == null || player.HpCurrent <= 0)
            {
                continue;
            }

            SDamageInfo damageToPlayer = new SDamageInfo(0.6f, true ,ERatioSwitch.ByMax);
            player.OnHit(this, damageToPlayer);

            CLogger.Log($"{player.name}의 현재 체력 : {player.HpCurrent}");


        }

    }

    private void GrogiState()
    {
        if (_grogiTimer == 0f)
        {
            _isMain = false;
            _isSub = false;

            _animator.ResetTrigger(_firstNorm);
            _animator.ResetTrigger(_secondNorm);
            _animator.ResetTrigger(_thirdNorm);
            _animator.ResetTrigger(_firstSub);
            _animator.ResetTrigger(_secondSub);
            _animator.ResetTrigger(_thirdSub);
            _animator.ResetTrigger(_forthSub);
            _animator.ResetTrigger(_firstMain);
            _animator.ResetTrigger(_secondMain);
            _animator.ResetTrigger(_thirdMain);
            _animator.ResetTrigger(_grogiState);

            CLogger.Warn("GrogiState 함수야!!");

            _atkTimer = 0f;
            _delayTimer = 0f;
            _fxDriver?.SetLocked(true);
            _animator.SetTrigger(_grogiState);

        }

        _grogiTimer += Time.fixedDeltaTime;
        if (_grogiTimer < _grogiTime) return;

        _isMain = false;
        _isSub = false;

        _grogiTimer = 0f;
        _onGrogi = false;
        _onAttack = false;
        _currentState = ESlothBossState.Idle;
        _fxDriver?.SetLocked(false);

    }


    #endregion


    private int NormalFindPlayer(float radius)
    {
        Vector3 offset = (normalAtk == 1)? this.transform.forward : this.transform.forward * 3f;
                     
        int hit = Physics.OverlapSphereNonAlloc(this.transform.position + offset, radius, _hitCollider, _targetMask);

        for (int i = 0; i < _hitCollider.Length; i++)
        {
            if (_hitCollider[i] == null)
            {
                continue;
            }

            _target = _hitCollider[i].transform;


            break;
        }

        return hit;
    }


    private int FindPlayer(float radius)
    {
        int hit = Physics.OverlapSphereNonAlloc(this.transform.position, radius, _hitCollider, _targetMask);

        for (int i = 0; i < _hitCollider.Length; i++)
        {
            if (_hitCollider[i] == null)
            {
                continue;
            }

            _target = _hitCollider[i].transform;


            break;
        }

        return hit;
    }


    private Vector3 RotateBox(Vector3 startPos, Vector3 pivot, Vector3 axis, float angle)
    {
        Vector3 dir = startPos - pivot;
        dir = Quaternion.AngleAxis(angle, axis) * dir;


        return (pivot + dir);
    }


    private void OnValidate()
    {
        if (_player == null) return;
        Vector3 targetPos = _player.transform.position - this.transform.position; 

        float distance = targetPos.magnitude;

        distance = Mathf.Max(3.0f, distance);
                
    }

    private float _initialAtkTime = -1f;
    private int _battleVersion;

    /// <summary>
    /// 다시 소환할 때 AI · 공격 상태를 처음(Start 직후)과 같게 되돌립니다. 체력 · 상태이상은 부르는 쪽(CBossSummoner)이 되돌립니다.
    /// </summary>
    public void ResetBattleState()
    {
        if (_isDead && HpCurrent > 0f)
        {
            _isDead = false;

            if (_animator != null)
            {
                _animator.speed = 1f;
                _animator.enabled = true;
            }
        }

        _battleVersion++;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _groggyDuration = 0f;

        _onGrogi = false;
        _grogiTimer = 0f;
        normalCount = 0;

        if (_animator != null && _animator.isActiveAndEnabled)  
        {
            _animator.speed = 1f;
            _animator.ResetTrigger(_grogiState);
        }

        // 공격 대기 시간(_atkTime)은 공격 중에 0 으로 바뀌므로 첫 소환 때(전투 전) 값을 기억해 두고 되돌림
        if (_initialAtkTime < 0f) _initialAtkTime = _atkTime;
        _atkTime = _initialAtkTime;

        _currentState = _bossState;
        _atkTimer = 0f;
        _delayTimer = 0f;
        if (_onAttack && _previewPattern != null)
        {
            _previewPattern.ClearPreview();
            _previewPattern.transform.position = _patternPos;
        }
        _onAttack = false;
        _isNormal = false;
        _isSub = false;
        _isMain = false;
        normalAtk = 0;
        subAtk = 0;
        mainAtk = 0;
        _mainCount = 0;
        if (_player != null) _target = _player.transform;
    }

    public override EBossClear BossId => EBossClear.Pigritia;

    protected override void Die()
    {
        if (_isDead) return;

        ResetBattleState();
        _isDead = true;
        _currentState = ESlothBossState.Dead;
        _onGrogi = false;

        _cts?.Cancel();

        if (_animator != null) _animator.enabled = false;
        if (_previewPattern != null) _previewPattern.ClearPreview();

        if (_previewInnerPattern != null && _previewInnerPattern != _previewPattern)
            _previewInnerPattern.ClearPreview();

        GetComponentInChildren<CEspoirBossFxDriver>(true)?.StopAll();

        CGameManager.Instance?.EndBossTimer(EBossClear.Pigritia, true);
        
        base.Die();
    }

    private float GetGroggyRemainHp()
    {
        float maxHp = _groggyStartHp * GroggyDamageRatio;
        float damageTaken = _groggyStartHp - HpCurrent;
        return Mathf.Clamp(maxHp - damageTaken, 0f, maxHp);
    }

    public bool TryGetGroggyInfo(out float remainHp, out float maxHp, out float remainTime, out float maxTime)
    {
        remainHp = 0f;
        maxHp = 1f;
        remainTime = 0f;
        maxTime = 1f;

        if (!isActiveAndEnabled || HpCurrent <= 0f || _groggyDuration <= 0f)
        {
            return false;
        }

        maxHp = _groggyStartHp * GroggyDamageRatio;
        float damageTaken = _groggyStartHp - HpCurrent;
        remainHp = GetGroggyRemainHp();

        maxTime = _groggyDuration;
        remainTime = Mathf.Max(0f, _groggyEndTime - Time.unscaledTime);
        return true;
    }
}