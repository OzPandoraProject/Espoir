using UnityEngine;
using UnityEngine.InputSystem;

public class CPlayer : CUnit
{
    [Header("이동 설정")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _rotationSpeed = 720f;

    [SerializeField] private Camera _mainCamera;
    [SerializeField] private LayerMask _groundLayer;

    [Header("대쉬 설정")]
    [SerializeField] private float _dashDistance = 3f;
    [SerializeField, Min(0.01f)] private float _dashDuration = 0.3f;
    [SerializeField] private float _dashCooldown = 1f;

    [Header("공격 설정")]
    [SerializeField] private CPlayerAttack _playerAttack;
    [SerializeField, Range(0f, 1f)] private float _attackHitTime = 0.3f;

    [Header("입력 설정")]
    [SerializeField] private InputActionReference _moveClick;
    [SerializeField] private InputActionReference _pointerPosition;
    [SerializeField] private InputActionReference _attackInput;
    [SerializeField] private InputActionReference _dashInput;

    [Header("애니메이션 설정")]
    [SerializeField] private Animator _animator;

    private static readonly int _IS_MOVING_HASH = Animator.StringToHash("IsMoving");
    private static readonly int _ATTACK_HASH = Animator.StringToHash("Attack");
    private static readonly int _DASH_HASH = Animator.StringToHash("Dash");

    private Vector3 _targetPosition;
    private bool _isAttacking;
    private bool _hasEnteredAttack;
    private bool _hasAppliedAttack;

    private Vector3 _dashTargetPosition;
    private float _dashCooldownRemaining;
    private bool _isDashing;
    private bool _hasEnteredDash;

    private void OnEnable()
    {
        _moveClick.action.Enable();
        _pointerPosition.action.Enable();
        _attackInput.action.Enable();
        _dashInput.action.Enable();
    }

    private void OnDisable()
    {
        _moveClick.action.Disable();
        _pointerPosition.action.Disable();
        _attackInput.action.Disable();
        _dashInput.action.Disable();
    }

    private void Start()
    {
        _targetPosition = transform.position;
    }

    private void Update()
    {
        _dashCooldownRemaining = Mathf.Max(_dashCooldownRemaining - Time.deltaTime, 0f);

        if (_isAttacking)
        {
            UpdateAttackState();
            return;
        }

        if (_isDashing)
        {
            UpdateDashState();
            return;
        }

        if (_dashInput.action.WasPressedThisFrame() &&
            _dashCooldownRemaining <= 0f)
        {
            StartDash();
            return;
        }

        if (_attackInput.action.WasPressedThisFrame())
        {
            StartAttack();
            return;
        }

        if (_moveClick.action.WasPressedThisFrame())
        {
            SetMoveTarget();
        }

        Move();
    }

    private void StartAttack()
    {
        _isAttacking = true;
        _hasEnteredAttack = false;
        _hasAppliedAttack = false;
        _targetPosition = transform.position;

        _animator.SetBool(_IS_MOVING_HASH, false);
        _animator.SetTrigger(_ATTACK_HASH);
    }

    private void UpdateAttackState()
    {
        AnimatorStateInfo currentState = _animator.GetCurrentAnimatorStateInfo(0);

        if (currentState.shortNameHash == _ATTACK_HASH)
        {
            _hasEnteredAttack = true;

            if (!_hasAppliedAttack &&
                currentState.normalizedTime >= _attackHitTime)
            {
                _hasAppliedAttack = true;
                _playerAttack.Attack(this);
            }
        }
        
        else if (_hasEnteredAttack && !_animator.IsInTransition(0))
        {
            _isAttacking = false;
        }
    }

    private void StartDash()
    {
        _isDashing = true;
        _hasEnteredDash = false;
        _dashCooldownRemaining = _dashCooldown;
        _targetPosition = transform.position;

        _dashTargetPosition =
            transform.position + transform.forward * _dashDistance;

        _animator.SetBool(_IS_MOVING_HASH, false);
        _animator.SetTrigger(_DASH_HASH);
    }

    private void UpdateDashState()
    {
        float dashSpeed = _dashDistance / _dashDuration;

        transform.position = Vector3.MoveTowards(
            transform.position,
            _dashTargetPosition,
            dashSpeed * Time.deltaTime
        );

        AnimatorStateInfo currentState = _animator.GetCurrentAnimatorStateInfo(0);

        if (currentState.shortNameHash == _DASH_HASH)
        {
            _hasEnteredDash = true;
        }
        
        else if (_hasEnteredDash &&
                 !_animator.IsInTransition(0) &&
                 transform.position == _dashTargetPosition)
        {
            _isDashing = false;
            _targetPosition = transform.position;
        }
    }

    private void SetMoveTarget()
    {
        Vector2 mousePosition = _pointerPosition.action.ReadValue<Vector2>();
        Ray ray = _mainCamera.ScreenPointToRay(mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, _groundLayer))
        {
            _targetPosition = hit.point;

            _targetPosition.y = transform.position.y;
        }
    }

    private void Move()
    {
        Vector3 previousPosition = transform.position;

        transform.position = Vector3.MoveTowards(
            previousPosition,
            _targetPosition,
            _moveSpeed * Time.deltaTime
        );

        Vector3 moveDirection = transform.position - previousPosition;
        bool isMoving = transform.position != previousPosition;

        if (isMoving)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _rotationSpeed * Time.deltaTime
            );
        }

        _animator.SetBool(_IS_MOVING_HASH, isMoving);
    }
}
