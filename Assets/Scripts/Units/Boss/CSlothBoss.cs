using System.Collections;
using System.Collections.Generic;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

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

    [Header("Boss Info")]
    [SerializeField] private Animator _animator;
    [SerializeField] private float _atkTimer = 2.0f;
    [SerializeField] private float _normalAtkRange = 5.0f;
    [SerializeField] private float _detectedRange = 10.0f;


    [Header("훨윈드")]
    [SerializeField] private float _whirlRound = 10f;







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


    private SoundManager _soundManager;
    private Transform _target;



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

        if (_soundManager == null)
        {

            _soundManager = FindFirstObjectByType<SoundManager>();
            if (_soundManager == null)
            {
                CLogger.Warn($"사운드매니저 찾기 실패");
            }

        }



        _target = _player.transform;
        _previewPattern = this.GetComponent<CPreviewPattern>();
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


    private void NormalAttackPattern()
    {
        if (_currentState == ESlothBossState.NormalAttack)
        {
            _isNormal = true;

        }

        int normalAttack = Random.Range(0, 3);

        switch (normalAttack)
        {
            case 0:
                _animator.SetTrigger(_fisrtNorm);

                break;

            case 1:
                _animator.SetTrigger(_secondNorm);

                break;

            case 2:
                _animator.SetTrigger(_thirdNorm);

                break;

        }

    }


    private void SubAttackPattern()
    {
        if (_currentState == ESlothBossState.SubAttack)
        {
            _isSub = true;
        }

        int subAttack = Random.Range(0, 4);

        switch (subAttack)
        {
            case 0:
                _animator.SetTrigger(_firstSub);
                break;

            case 1:
                _animator.SetTrigger(_secondSub);
                break;

            case 2:
                _animator.SetTrigger(_thirdSub);
                break;

            case 3:
                _animator.SetTrigger(_forthSub);
                break;
        }
    }

    private void MainAttackPattern()
    {
        if (_currentState == ESlothBossState.MainAttack)
        {
            _isMain = true;
        }

        int mainAttack = Random.Range(0, 3);

        switch (mainAttack)
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






    private int FindPlayer()
    {
        int hit = Physics.OverlapSphereNonAlloc
                    (
                        transform.position,
                        _detectedRange,
                        _hitCollider,
                        _tartgetMask
                    );



        for (int i = 0; i < _hitCollider.Length; i++)
        {
            if (_hitCollider[i] == null)
            {
                continue;
            }


            Vector3 toPlayer = _hitCollider[i].transform.position - transform.position;
            float toPlayerDis = toPlayer.magnitude;

            if (_sightCheck)
            {
                Vector3 originPos = transform.position;
                Vector3 dir = toPlayer.normalized;

                float realDis = Mathf.Sqrt(toPlayerDis);

                if (Physics.Raycast(originPos, dir, realDis, _obtacleMask))
                {
                    continue;
                }

            }

            _target = _hitCollider[i].transform;

            break; // 단일 타겟 추적
        }

        return hit;

    }

    private void HitPlayer(CUnit attacker)
    {
        int hit = FindPlayer();

        for (int i = 0; i < hit; i++)
        {

            if (_hitCollider[i] == null)
            {
                continue;
            }

            CUnit player = _hitCollider[i].GetComponent<CUnit>();

            if (player == null)
            {
                continue;
            }

            if (player.HpCurrent <= 0f)
            {
                continue;
            }

            SDamageInfo damagePlayer = new SDamageInfo(attacker.Attack);

            float lastHP = player.HpCurrent;

            player.OnHit(attacker, player, damagePlayer);
            if (_soundManager != null)
            {
                _soundManager.PlaySE(EEffectSound.PlayerHit);
            }
            CLogger.Log($"{player.name}의 현재 체력 : {player.HpCurrent}");

        }





    }
}