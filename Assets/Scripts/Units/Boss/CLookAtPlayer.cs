using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CLookAtPlayer : MonoBehaviour
{
    #region ¿ŒΩ∫∆—≈Õ
    [SerializeField] private CPlayer _player;

    [SerializeField] private float _moveSpeed = 5.0f;
    [SerializeField] private float _rotateSpeed = 10.0f;
    [SerializeField] private float _followRange = 12.0f;
    [SerializeField] private float _atkRange = 5.0f;
    [SerializeField] private bool _onTarget;
    #endregion

    private int _hashAtk = Animator.StringToHash("tAttack");
    private int _hashMove = Animator.StringToHash("bMove");



    private bool _isMove = true;
    private bool _isAttack = true;



    private bool _hasMove = false;

    private Rigidbody _rb;
    private Animator _animator;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();

    }


    void Start()
    {
        
    }


    void Update()
    {

        FollowPlayerAnime();

    }

    private void FixedUpdate()
    {
        FollowPlayer(_hasMove);

    }

    private void FollowPlayerAnime()
    {
        if(_player == null)
        {
            return;
        }

        Vector3 target = new Vector3(_player.transform.position.x, 0f, _player.transform.position.z);

        Vector3 lookPlayer = target - this.transform.position;

        float distance = lookPlayer.magnitude;

        _onTarget = distance > _followRange ? false : true;

        if(_onTarget == true)
        {
            _isMove = true;


            if(distance <= _atkRange)
            {
                _isAttack = true;

                _animator.SetTrigger(_hashAtk);
            }


            _animator.SetBool(_hashMove, _isMove);
        }

        else
        {
            _isMove = false;
            _isAttack = false;

            _animator.SetBool(_hashMove, _isMove);
        }


    }


    private void FollowPlayer(bool hasMove)
    {
        if(_player == null)
        {
            return;
        }

        Vector3 target = new Vector3(_player.transform.position.x , 0f, _player.transform.position.z);

        Vector3 lookPlayer = target - this.transform.position;

        float distance = lookPlayer.magnitude;

       
        if(hasMove == true)
        {

            _rb.velocity = new Vector3 (lookPlayer.normalized.x * _moveSpeed, 0f, lookPlayer.normalized.z * _moveSpeed );
            Quaternion toTarget = Quaternion.LookRotation(lookPlayer);
            Quaternion viewTarget = Quaternion.RotateTowards
                (
                    this.transform.rotation,
                    toTarget,
                    _rotateSpeed * Time.deltaTime

                );


        }

        else
        {
            _rb.velocity = Vector3.zero;

            Quaternion toTarget = Quaternion.LookRotation(lookPlayer);
            Quaternion viewTarget = Quaternion.RotateTowards
                (
                    this.transform.rotation,
                    toTarget,
                    _rotateSpeed * Time.deltaTime

                );

        }


    }



}
