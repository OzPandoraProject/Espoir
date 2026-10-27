using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;


public class CTestEnemy : CEnemy
{
    public enum EEnemyState
    {
        Idle = 0,
        Moving,
        Attack,
        
    }

    #region 인스팩터
    [Header("Enemy Info")]
    [SerializeField] private string _name = "Skeleton";
    [SerializeField] private LayerMask _tartgetMask;

    [SerializeField] private float _offsetDir = 1.5f;
    [SerializeField] private float _moveSpeed = 3.0f;
    [SerializeField] private float _rotateSpeed = 30.0f;
    [SerializeField] private float _sightView = 6.0f;
    
    [SerializeField] private float _forgetTime = 1.0f;
    [SerializeField] private float _patrolTime = 2.0f;

    [Header("공격 타겟")]
    [SerializeField] private CPlayer _player;

    [SerializeField] private float _atkRange = 2.0f;

    [SerializeField] float _attackTimer = 0.0f;
    [SerializeField] float _forgetTimer = 0.0f;
    [SerializeField] float _patrolTimer = 0.0f;
    
    [SerializeField] private bool _OnTarget = false;
    [SerializeField] private bool _sightCheck = false;
    [SerializeField] private LayerMask _obtacleMask;
    [SerializeField] private Collider[] _hitCollider = new Collider[2];

    #endregion 

    private CPlayer Player => _player;

    private SoundManager _soundManager;
    private Transform _target;
    private Vector3 _startPos;

    private Vector3 _movePos;
    private Rigidbody _rb;



    #region Animator
    [SerializeField] private Animator _animator;
    
    private int _hashMove = Animator.StringToHash("bMove");
    private int _hashAttack = Animator.StringToHash("tAttack");
    private int _hashDie = Animator.StringToHash("bDie");

    private bool _isAttack;
    private bool _isMove;
    private bool _isReturn;
    private bool _isPatrol;
    private bool _isDie;
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
            CLogger.Error("플레이어 찾을 수 없음");
        }
        _target = Player.transform;
        _startPos = transform.position;
        _animator = GetComponent<Animator>();
        _rb = this.GetComponent<Rigidbody>();
        _soundManager = FindFirstObjectByType<SoundManager>();
        if (_soundManager == null)
        {
            CLogger.Warn($"사운드매니저 찾기 실패");
        }
    }

    protected override void Update()
    {
        if (_hpCurrent <= 0)
        {
            _isDie = true;
            _animator.SetBool(_hashDie, true);
            Destroy(this.gameObject, 3f);
        }
        if (_isDie)
        {
            return;
        }
        // TODO : 플레이어를 쫓아가기
        OnTargetMove_Update();



        // TODO : 주기적으로 공격하기
       
        if(_attackTimer > 2.0f)
        {
            OnAttack();
        }

        _attackTimer += Time.deltaTime;

    }

    private void FixedUpdate()
    {
        OnTargetMove_Fixed();


    }

    #region No Rigidbody Move

    private void OnTargetMove()
    {
        if(_target == null && _isAttack == true)
        {
            return;
        }


        Vector3 target = new Vector3(_target.position.x , 0f , _target.position.z);

        Vector3 toTarget = target - transform.position;

        float distance = toTarget.magnitude;

        _OnTarget = distance <= _sightView ? true : false;
        _isPatrol = !_OnTarget;
                
        if(_OnTarget == true)
        {
            _isMove = true;

            FollowPlayer(target, distance);
            
            _forgetTimer = 0.0f;
        }

        else
        {
            if(_forgetTimer < _forgetTime)
            {
                _forgetTimer += Time.deltaTime;

                if (_isMove == true)
                {
                    FollowPlayer(target, distance);
                }
            }

            else
            {
                _isMove = false;

                // CLogger.Log("원래 위치로");
                Vector3 direction = _startPos - transform.position;
                direction.y = 0f;

                if (direction.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        targetRotation,
                        3600f * Time.deltaTime
                    );
                }

                transform.position = Vector3.MoveTowards(transform.position, _startPos, (1.5f * _moveSpeed) * Time.deltaTime);
                //EnemyPatrol();
                // TODO : 필요하면 패트롤 함수 넣어서 움직이게 해도 좋다.
            }

        }

            _animator.SetBool(_hashMove, _isMove);

    }

    private void FollowPlayer(Vector3 target, float dis)
    {
        Vector3 direction = target - this.transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(
                this.transform.rotation,
                targetRotation,
                3600f * Time.deltaTime
            );
        }

        if (dis > _offsetDir)
        {

            _isMove = true;

            Vector3 n_toTarget = target.normalized;

            Vector3 targetPos = target - (n_toTarget * _offsetDir);

            //transform.position = Vector3.Lerp(Vector3.forward, targetPos, _moveSpeed *Time.deltaTime);

            //_rb.MovePosition(this.transform.forward + targetPos * _moveSpeed * Time.deltaTime);

            transform.position = Vector3.MoveTowards(transform.position, targetPos, _moveSpeed * Time.deltaTime);

        }

        else
        {
            _isMove = false;

        }



    }

#endregion


    #region Rigidbody Move

    // 이동 외 애니메이션 + 회전
    private void OnTargetMove_Update()
    {
        if (_target == null && _isAttack == true)
        {
            _isMove = false;
            _movePos = Vector3.zero;
            _isReturn = false;

            return;
        }



        Vector3 target = new Vector3(_target.position.x, 0, _target.position.z);
               
        Vector3 toTarget = target - transform.position;

        float distance = toTarget.magnitude;

        _OnTarget = distance > _sightView ? false : true;
        
        if (_OnTarget == true)
        {
            CLogger.Log("타겟이 들어왔다.");
            
            _forgetTimer = 0.0f;

            _isReturn = false;

            if (distance < _sightView & distance > _offsetDir)
            {
                _isMove = true;

                _movePos = toTarget.normalized;

                Quaternion targetRotation = Quaternion.LookRotation(_movePos);
                transform.rotation = Quaternion.RotateTowards
                    (
                        transform.rotation,
                        targetRotation,
                        3600f * Time.deltaTime
                    );
            }

            else
            {
                _isMove = false;
                _movePos = Vector3.zero;
            }

        }

        else
        {
            if (_forgetTimer < _forgetTime)
            {
                _forgetTimer += Time.deltaTime;


                if (distance > _offsetDir)
                {
                    _isMove = true;

                    _movePos = toTarget.normalized;


                }


                else
                {
                    _isMove = false;

                    _movePos = Vector3.zero;

                }

            }

            else
            {
                _isMove = false;

                CLogger.Log("원래 위치로");
                Vector3 direction = _startPos - transform.position;
                direction.y = 0f;

                if (direction.sqrMagnitude > 0.001f)
                {
                    _isMove = true;
                    _isReturn = true;

                    _movePos = direction.normalized;

                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        targetRotation,
                        3600f * Time.deltaTime
                    );
                }

                else
                {
                    _isMove = false;
                    _isReturn = false;
                    _movePos = Vector3.zero;

                }

                //EnemyPatrol();
                // TODO : 필요하면 패트롤 함수 넣어서 움직이게 해도 좋다.
            }

        }

        _animator.SetBool(_hashMove, _isMove);




    }

    // 이동 전용
    private void OnTargetMove_Fixed()
    {
        if(_rb == null)
        {
            return;
        }

        if (_isMove == true)
        {
            float speed = _isReturn ? 1.5f * _moveSpeed : _moveSpeed;

            _rb.velocity = new Vector3(_movePos.x * speed , 0f , _movePos.z * speed);

        }

        else
        {
            _rb.velocity = new Vector3(0f, 0f, 0f);
        }

    }


    #endregion



    private void OnAttack()
    {


        if(_isMove == true)
        {
            return;
        }

        Vector3 toTarget = _player.transform.position - this.transform.position;
        
        if(toTarget.magnitude <= 2.0f)
        {
            _isAttack = true;
            if (_isAttack)
            {
                // TODO : TakeDamage 공격로직 추가 예정
                HitPlayer(this);
                _animator.SetTrigger(_hashAttack);
                if (_soundManager != null)
                {
                    _soundManager.PlaySE(EEffectSound.MonsterSlash);
                }


            }

        }

        else if(toTarget.magnitude > 2.0f)
        {
            _isAttack= false;
        }

        _attackTimer = 0f;

    }


    private int FindPlayer()
    {
        int hit = Physics.OverlapSphereNonAlloc
                    (
                        transform.position,
                        _sightView,
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
        
        for(int  i = 0; i < hit; i++)
        {

            if (_hitCollider[i] == null)
            {
                continue;
            }

            CUnit player = _hitCollider[i].GetComponent<CUnit>();

            if(player == null)
            {
                continue;
            }

            if(player.HpCurrent <= 0f)
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




    private void EnemyPatrol()
    {
        if(_OnTarget == false && _isPatrol == true)
        {
            float pingpong = Random.Range(-4.0f, 4.0f);

            Vector3 arrival = new Vector3(pingpong, 0f , pingpong);

            if (_patrolTimer > _patrolTime)
            {
                CLogger.Log("패트롤 하는 중");
                transform.position = Vector3.MoveTowards(transform.position, transform.position + arrival, _moveSpeed * Time.deltaTime);
                _patrolTimer = 0.0f;
                
            }

            _patrolTime += Time.deltaTime;
        }

        else
        {
         
            _isPatrol = true;
        }

    }

    private void TakeDamage(float damage)
    {
    
        this.GetDamage(damage);

        if(this.HpCurrent <= 0)
        {
            this.CallDie();
        }
    
    }



    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _atkRange);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, _sightView);

        if (_target != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _target.position);
        }


    }


}
