using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class CPlayerMovement : MonoBehaviour
{
    [Header("이동 설정")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _rotationSpeed = 720f;
    [SerializeField, Min(0f)] private float _groundSnapDistance = 0.6f;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private Animator _animator;

    private static readonly int _IS_MOVING_HASH = Animator.StringToHash("IsMoving");

    private CharacterController _characterController;
    private Vector3 _targetPosition;
    private float _verticalSpeed;

    public bool IsConfigured => _characterController != null && _animator != null;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        _targetPosition = transform.position;
        _verticalSpeed = 0f;
    }

    /// <summary>
    /// 이동할 지면 위치를 지정합니다.
    /// </summary>

    public void SetMoveTarget(Vector3 position)
    {
        _targetPosition = position;
    }

    /// <summary>
    /// 기존 목적지를 취소하고 이동 애니메이션을 멈춥니다.
    /// </summary>

    public void Stop()
    {
        _targetPosition = transform.position;

        if (_animator != null)
        {
            _animator.SetBool(_IS_MOVING_HASH, false);
        }
    }

    /// <summary>
    /// 중력을 적용하고 바닥에 닿으면 낙하 속도를 초기화합니다.
    /// </summary>

    public void ApplyGravity()
    {
        _verticalSpeed += Physics.gravity.y * Time.deltaTime;
        CollisionFlags collision = _characterController.Move(Vector3.up * (_verticalSpeed * Time.deltaTime));

        if ((collision & CollisionFlags.Below) != 0 && _verticalSpeed < 0f)
        {
            _verticalSpeed = 0f;
        }
    }

    /// <summary>
    /// 목적지로 이동하며 실제 이동 방향으로 회전합니다.
    /// </summary>

    public void Move()
    {
        Vector3 previousPosition = transform.position;

        Vector3 targetDirection = _targetPosition - previousPosition;
        targetDirection.y = 0f;

        MoveOnGround(Vector3.ClampMagnitude(targetDirection, _moveSpeed * Time.deltaTime));

        Vector3 moveDirection = transform.position - previousPosition;
        moveDirection.y = 0f;
        bool isMoving = moveDirection != Vector3.zero;

        if (isMoving)
        {
            Rotate(moveDirection, _rotationSpeed);
        }

        _animator.SetBool(_IS_MOVING_HASH, isMoving);
    }

    /// <summary>
    /// 충돌과 경사면 접지를 처리하며 지정한 거리만큼 이동합니다.
    /// </summary>

    public void MoveOnGround(Vector3 motion)
    {
        int moveCount = Mathf.Max(1, Mathf.CeilToInt(motion.magnitude / _characterController.radius));
        Vector3 moveStep = motion / moveCount;
        float slopeAngle = _characterController.slopeLimit * Mathf.Deg2Rad;
        float slope = Mathf.Tan(slopeAngle);
        float minGroundNormal = Mathf.Cos(slopeAngle);

        for (int i = 0; i < moveCount; i++)
        {
            bool wasGrounded = _characterController.isGrounded;
            Vector3 previousPosition = transform.position;
            _characterController.Move(moveStep);

            if (!wasGrounded || _verticalSpeed > 0f)
            {
                continue;
            }

            Vector3 moveDirection = transform.position - previousPosition;
            moveDirection.y = 0f;
            float snapDistance = Mathf.Min(moveDirection.magnitude * slope, _groundSnapDistance);

            if (snapDistance <= 0f)
            {
                continue;
            }

            Bounds bounds = _characterController.bounds;
            float groundDistance = bounds.extents.y + _characterController.skinWidth;
            float rayDistance = groundDistance + _groundSnapDistance;

            if (!Physics.Raycast(bounds.center, Vector3.down, out RaycastHit hit,
                rayDistance, _groundLayer, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            if (Vector3.Dot(hit.normal, Vector3.up) < minGroundNormal)
            {
                continue;
            }

            float distance = Mathf.Clamp(hit.distance - groundDistance, 0f, snapDistance);
            _characterController.Move(Vector3.down * distance);
        }
    }

    /// <summary>
    /// 지정한 방향으로 초당 회전 속도에 맞춰 회전합니다.
    /// </summary>

    public void Rotate(Vector3 direction, float rotationSpeed)
    {
        if (direction == Vector3.zero)
        {
            return;
        }

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            Quaternion.LookRotation(direction),
            rotationSpeed * Time.deltaTime
        );
    }
}
