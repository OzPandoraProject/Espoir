
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;
using static UnityEngine.GraphicsBuffer;
using System;
using Random = UnityEngine.Random;

public class CSlothBoss : CEnemy
{


    public enum ESlothBossState
    {
        Idle = 0,
        NormalAttack,
        SubAttack,
        MainAttack,
        Dead
    }



    #region 인스팩터
    [SerializeField] private CPlayer _player;

    [Header("Attack Player")]
    [SerializeField] private LayerMask _tartgetMask;
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
    [SerializeField] private float _normalAtkRange = 7.0f;
    [SerializeField] private float _detectedRange = 15.0f;
    [SerializeField] private float _returnDelayTime = 2f;


    [Header("Pattern Range")]
    [SerializeField] private float _whirlRange = 15f;
    [SerializeField] private float _whirlInner = 7f;
    [SerializeField] private float _donutRange = 12f;
    [SerializeField] private float _donutInner = 7f;
    [SerializeField] private float _areaAngle = 30f;
    [SerializeField] private int _divideArea = 6;





    #endregion

    #region Animator 변수

    private int _fisrtNorm = Animator.StringToHash("nAttack01");
    private int _secondNorm = Animator.StringToHash("nAttack02");
    private int _thirdNorm = Animator.StringToHash("nAttack03");

    private int _firstSub = Animator.StringToHash("bAttack01");
    private int _secondSub = Animator.StringToHash("bAttack02");
    private int _thirdSub = Animator.StringToHash("bAttack03");
    private int _forthSub = Animator.StringToHash("bAttack04");

    private int _firstMain = Animator.StringToHash("pAttack01");
    private int _secondMain = Animator.StringToHash("pAttack02");
    private int _thirdMain = Animator.StringToHash("pAttack03");



    #endregion



    #region 내부변수

    private ESlothBossState _startState;
    private ESlothBossState _currentState;


    private CSoundManager _soundManager;
    private Transform _target;
    private Vector3 _patternPos;

    private float _atkTimer;
    private float _delayTimer;


    private int normalAtk;
    private int subAtk;
    private int mainAtk;
    private bool _onAttack;


    private bool _isStop;
    private bool _isNormal;
    private bool _isSub;
    private bool _isMain;
    private bool _isDead;



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

        if(_soundManager == null)
        {
            _soundManager = FindFirstObjectByType<CSoundManager>();
            if(_soundManager == null)
            {
                CLogger.Warn("사운드 매니저 찾을 수 없음");
            }

        }


        _patternPos = _previewPattern.transform.position;
        _target = _player.transform;
        _previewPattern = this.GetComponentInChildren<CPreviewPattern>();
        _animator = this.GetComponent<Animator>();

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
        LookAtPlayer();
        
        if(_atkTimer > _atkTime)
        {
            BossState();

        }
        else
        {
            
            _atkTimer += Time.fixedDeltaTime;        
        }
    }


    private void LookAtPlayer()
    {
        Vector3 targer = new Vector3(_target.position.x, this.transform.position.y, _target.position.z);

        Vector3 lookPlayer = targer - this.transform.position;

        float distance = lookPlayer.magnitude;

        if (distance <= _detectedRange)
        {
            if (_onAttack == false)
            {
                Vector3 dir = lookPlayer.normalized;
                Quaternion lookRotation = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRotation, _rotateSpeed * Time.deltaTime);

                float currentHP = this.HpCurrent;

                if(currentHP < this.HpMax * 0.7f && _mainCount == 0)
                {
                    _isSub = false;
                    _isMain = false;
                    if (_delayTimer > _returnDelayTime)
                    {
                        _isMain = true;
                        mainAtk = 1;
                        _currentState = ESlothBossState.MainAttack;
                        _delayTimer = 0;

                    }


                }


                else if(currentHP < this.HpMax * 0.4f && _mainCount == 1)
                {
                    _isSub = false;
                    _isMain = false;
                    if (_delayTimer > _returnDelayTime)
                    {
                        _isMain = true;
                        mainAtk = 2;
                        _currentState = ESlothBossState.MainAttack;
                        _delayTimer = 0;

                    }

                }

                else if(currentHP < this.HpMax * 0.2f && _mainCount == 2)
                {
                    if (_delayTimer > _returnDelayTime)
                    {
                        _isMain = true;
                        mainAtk = 0;
                        _currentState = ESlothBossState.MainAttack;
                        _delayTimer = 0;

                    }

                }

                else
                {

                    if (distance <= _normalAtkRange)
                    {
                        _isSub = false;
                        _isMain = false;

                        if (_delayTimer > _returnDelayTime)
                        {
                            _isNormal = true;

                            _currentState = ESlothBossState.NormalAttack;
                            _delayTimer = 0;

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
                        }

                        else
                        {
                            _isNormal = false;
                            _isMain = false;
                            _delayTimer += Time.deltaTime;
                        }



                    }

                }

                
            }

            else
            {
                CLogger.Log("Boss Animator Moving");
            }

        }





    }




    private void NormalAttackPattern()
    {
        if(_isNormal == false)
        {
            return;
        }

        _isSub = false;
        _isMain = false;

        normalAtk = Random.Range(0, 4);
        _onAttack = true;

        normalAtk = 1;
        
        CLogger.Log($"Normal Attack Pattern : {normalAtk}");

        switch (normalAtk)
        {
            case 0:
                _animator.SetTrigger(_fisrtNorm);
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
                TwiceAttack().Forget();

                break;

        }
    }

    private void SubAttackPattern()
    {
        if(_isSub == false)
        {
            return;
        }

        _isMain = false;
        _isNormal = false;

        subAtk = UnityEngine.Random.Range(1, 4);

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
    }

    private void MainAttackPattern()
    {
        if (_isMain == false)
        {
            return;
        }

        _isNormal = false;
        _isSub = false;

        _onAttack = true;

        CLogger.Log($"Main Attack Pattern : {mainAtk}");

        switch (mainAtk)
        {
            case 0:
                _animator.SetTrigger(_firstMain);
                break;

            case 1:
                _animator.SetTrigger(_secondMain);
                break;

            case 2:
                _animator.SetTrigger(_thirdMain);
                break;
        }


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
                break;
        }


    }



    #region NormalAttack Patterns
    private async UniTask NormalAttackDelay()
    {

        await UniTask.Delay(3000);
        _atkTime = 0;

        _onAttack = false;

    }

    private void DoubleHookAttack()
    {

        int hit = NormalFindPlayer(4f);

        for (int i = 0; i < hit; i++)
        {
            if(_hitCollider == null)
            {
                continue;
            }

            CPlayer player = _hitCollider[i].GetComponent<CPlayer>();

            if (player == null || player.HpCurrent <= 0)
            {
                continue;
            }

           
            SDamageInfo damagePlayer = new SDamageInfo(0.1f, ERatioSwitch.ByMax);
            float lastHp = player.HpCurrent;

            player.OnHit(this, damagePlayer);
            
            CLogger.Log($"{player.name}의 현재 체력 : {player.HpCurrent}");

        }



    }

    private void BackSpinAttack()
    {
        int hit = NormalFindPlayer(4f);

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

            float area = Vector3.Dot(spinAreaDir,damageDir);

            if(area >= 0f)
            {
                SDamageInfo damagePlayer = new SDamageInfo(0.2f, ERatioSwitch.ByMax);
                float lastHp = player.HpCurrent;

                player.OnHit(this, damagePlayer);

                CLogger.Log($"{player.name}의 현재 체력 : {player.HpCurrent}");

            }




        }

    }

    private void SlamUpperAttack()
    {

        int hit = NormalFindPlayer(4f);

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

            SDamageInfo damagePlayer = new SDamageInfo(0.15f, ERatioSwitch.ByMax);
           
            player.OnHit(this, damagePlayer);

            CLogger.Log($"{player.name}의 현재 체력 : {player.HpCurrent}");
        }

    }
    #endregion


    #region SubPattern
    private async UniTask WhirlWindAttack()
    {

        if(_previewPattern != null)
        {
            _previewPattern.ShowDonutPattern(20, 15f, 3f);
        }
        else
        {
            CLogger.Error("PatternView 비어있음");
        }

        await UniTask.Delay(2300);
        _atkTimer = -0.8f;

        if (_previewPattern != null)
        {
            _previewPattern.ClearPreview();
        }
        
        
        _animator.SetTrigger(_secondSub);

        await UniTask.Delay(2500);

        _onAttack = false;

    }

    private void WhirlWindDamage()
    {

        int hit = SubFindPlayer(_whirlRange);

        for (int i = 0; i < hit; i++)
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

            float distance = Vector3.Distance
                (
                    new Vector3 (this.transform.position.x , 0f , this.transform.position.z),
                    new Vector3( player.transform.position.x , 0f, player.transform.position.z)
                );

            if(distance >= 3f && distance <= _whirlRange)
            {
                SDamageInfo damagePlayer = new SDamageInfo(0.1f, true, ERatioSwitch.ByMax);
                
                player.OnHit(this, damagePlayer);


                CLogger.Log($"{player.name}의 현재 체력 : {player.HpCurrent}");
            }

        }
    }
    
    private async UniTask DonutAttack()
    {
        if(_previewPattern != null)
        {
            _previewPattern.ShowDonutPattern(20, 18f, 10f);
        }

        else
        {
            CLogger.Error("PatternView 비어있음");
        }

        await UniTask.Delay(2000);
        _atkTimer = 0;

        if (_previewPattern != null)
        {
            _previewPattern.ClearPreview();
        }

        _animator.SetTrigger(_forthSub);

        _onAttack = true;
        await UniTask.Delay(2000);

        _onAttack = false;
    }

    private void DonutDamage()
    {
        int hit = SubFindPlayer(_donutRange);

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

            if (distance >= 10f && distance <= 18f)
            {
                SDamageInfo damagePlayer = new SDamageInfo(0.25f, true, ERatioSwitch.ByMax);

                player.OnHit(this, damagePlayer);

                CLogger.Log($"{player.name}의 현재 체력 : {player.HpCurrent}");
            }
        }
    }

    private async UniTask TwiceAttack()
    {
        if (_previewPattern != null)
        {
            _previewPattern.transform.position = _player.transform.position;
            _previewPattern.ShowCirclePattern(20, 8f);

        }

        else
        {
            CLogger.Error("PatternView 비어있음");
        }

        await UniTask.Delay(1500);
        _atkTimer = 0;

        if (_previewPattern != null)
        {
            _previewPattern.ClearPreview();
            _previewPattern.transform.position = _patternPos;
        }
        _animator.SetTrigger(_firstSub);

        await UniTask.Delay(3000);
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

            SDamageInfo damagePlayer = new SDamageInfo(0.1f, ERatioSwitch.ByMax);
            float lastHp = player.HpCurrent;

            player.OnHit(this, damagePlayer);

            CLogger.Log($"{player.name}의 현재 체력 : {player.HpCurrent}");
        }



    }

    private void OuterExplosionDamage()
    {
        int hit = SubFindPlayer(13f);

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

            if (distance >= 7f && distance <= 13f)
            {
                SDamageInfo damagePlayer = new SDamageInfo(0.30f, true, ERatioSwitch.ByMax);

                player.OnHit(this, damagePlayer);

                CLogger.Log($"{player.name}의 현재 체력 : {player.HpCurrent}");
            }
        }
    }

    private async UniTask AlternationAttack()
    {
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

        if (_previewPattern != null)
        {
            _previewPattern.ClearPreview();
        }

        _atkTimer = 0f;

        _animator.SetTrigger(_thirdSub);

        await UniTask.Delay(3100);
        _onAttack = false;
    }

    private void AlternationDamage(float offsetAngle)
    {
        int hit = SubFindPlayer(15f);
 
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

            if(distance > 15f)
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
                float standardAngle = offsetAngle +(j * angleStep);
                float tickAngle = Mathf.DeltaAngle(standardAngle, playerAngle);
                
                if(Mathf.Abs(tickAngle) <= _areaAngle * 0.5f)
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


    private int NormalFindPlayer(float radius)
    {
        Vector3 offset = this.transform.forward * 3f;
                     
        int hit = Physics.OverlapSphereNonAlloc(this.transform.position + offset, radius, _hitCollider, _tartgetMask);

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


    private int SubFindPlayer(float radius)
    {
        

        int hit = Physics.OverlapSphereNonAlloc(this.transform.position, radius, _hitCollider, _tartgetMask);

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

    private void OnValidate()
    {
        if (_player == null) return;
        Vector3 targetPos = _player.transform.position - this.transform.position; 

        float distance = targetPos.magnitude;

        distance = Mathf.Max(3.0f, distance);
                
    }

}