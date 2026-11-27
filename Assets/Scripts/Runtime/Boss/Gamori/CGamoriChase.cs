using UnityEngine;

/// <summary>
/// 공격과 공격 사이에 플레이어를 따라 걸어갑니다. 가까워지면 멈춰 서서 플레이어를 바라봅니다.
/// </summary>
[RequireComponent(typeof(CGamoriCombatant))]
public sealed class CGamoriChase : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private string _moveState = "Base Layer.Run";
    [SerializeField] private string _readyState = "Base Layer.Ready";
    [SerializeField, Min(0f)] private float _speed = 1.3f;
    [SerializeField, Min(0f)] private float _stopDistance = 3.3f, _resumeDistance = 4f;
    [SerializeField, Min(0f)] private float _turnSpeed = 240f;
    [SerializeField, Min(0f)] private float _blend = 0.25f;
    [SerializeField, Min(0f)] private float _restAfterAttack = 0.5f;

    private CGamoriCombatant _owner;
    private CPlayer _player;
    private float _rest;
    private bool _moving;

    /// <summary>
    /// 지금 플레이어에게 걸어가는 중인지 알려줍니다.
    /// </summary>
    public bool IsMoving => _moving;

    private float Unit => transform.lossyScale.y * CGamoriEffectClip.UNIT_SCALE;

    private void Awake()
    {
        _owner = GetComponent<CGamoriCombatant>();
    }

    private void OnDisable()
    {
        _moving = false;
    }

    private void Update()
    {
        if (_owner.IsCasting)
        {
            _moving = false;
            _rest = _restAfterAttack;
            return;
        }
        if (!_player) _player = FindFirstObjectByType<CPlayer>();
        _rest -= Time.deltaTime;
        bool canAct = _rest <= 0f && _player && _player.HpCurrent > 0f && _owner.HpCurrent > 0f && !_owner.IsStunned
            && CGameManager.Instance.GameState == EGameState.Idle;
        if (!canAct)
        {
            if (_rest <= 0f) SetMoving(false);
            return;
        }

        Vector3 toward = Vector3.ProjectOnPlane(_player.transform.position - transform.position, Vector3.up);
        float distance = toward.magnitude;
        if (distance > 0.01f)
        {
            Quaternion facing = Quaternion.LookRotation(toward);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, _turnSpeed * Time.deltaTime);
        }
        SetMoving(distance > (_moving ? _stopDistance : _resumeDistance) * Unit);
        if (_moving) transform.position += toward / distance * (_speed * Unit * Time.deltaTime);
    }

    private void SetMoving(bool moving)
    {
        if (_moving == moving) return;
        _moving = moving;
        if (_animator) _animator.CrossFadeInFixedTime(moving ? _moveState : _readyState, _blend, 0);
    }
}
