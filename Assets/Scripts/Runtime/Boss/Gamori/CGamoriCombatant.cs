using System.Collections.Generic;
using UnityEngine;

public class CGamoriCombatant : CEnemy
{
    private CGamoriAttack _currentAttack;
    private Collider _playerCollider;
    private Collider[] _bodyColliders;
    private readonly List<Collider> _IGNORED_COLLIDERS = new List<Collider>();

    public bool IsCasting => _currentAttack && _currentAttack.IsCasting;

    /// <summary>
    /// 선택한 공격을 플레이어에게 시전합니다.
    /// </summary>
    public bool TryCast(CGamoriAttack attack, CPlayer player)
    {
        if (!isActiveAndEnabled || IsCasting || IsStunned || HpCurrent <= 0f) return false;
        if (!player || !player.gameObject.activeInHierarchy || player.HpCurrent <= 0f) return false;
        if (!attack || attack.gameObject != gameObject) return false;

        _currentAttack = attack;
        if (!attack.StartCast(this, player))
        {
            _currentAttack = null;
            return false;
        }

        if (player.TryGetComponent(out Collider body) && body.enabled && !body.isTrigger && body != _playerCollider)
        {
            RestoreCollision();
            _playerCollider = body;
            if (_bodyColliders == null) _bodyColliders = GetComponentsInChildren<Collider>();
            foreach (Collider collider in _bodyColliders)
            {
                if (!collider || !collider.enabled || collider.isTrigger) continue;
                if (!collider.gameObject.activeInHierarchy) continue;
                if (Physics.GetIgnoreCollision(collider, body)) continue;
                Physics.IgnoreCollision(collider, body, true);
                _IGNORED_COLLIDERS.Add(collider);
            }
            if (_IGNORED_COLLIDERS.Count == 0) _playerCollider = null;
        }
        return true;
    }

    protected override void Update()
    {
        base.Update();
        if (IsCasting || _IGNORED_COLLIDERS.Count == 0) return;
        if (!_playerCollider || !_playerCollider.enabled || !_playerCollider.gameObject.activeInHierarchy)
        {
            RestoreCollision();
            return;
        }

        // 공격이 끝나도 두 몸이 겹쳐 있으면 떨어진 뒤에 충돌을 복원한다.
        foreach (Collider collider in _IGNORED_COLLIDERS)
        {
            if (collider && collider.enabled && collider.bounds.Intersects(_playerCollider.bounds)) return;
        }
        RestoreCollision();
    }

    private void RestoreCollision()
    {
        if (_playerCollider && _playerCollider.enabled && _playerCollider.gameObject.activeInHierarchy)
        {
            foreach (Collider collider in _IGNORED_COLLIDERS)
            {
                if (collider && collider.enabled && collider.gameObject.activeInHierarchy)
                {
                    Physics.IgnoreCollision(collider, _playerCollider, false);
                }
            }
        }
        _IGNORED_COLLIDERS.Clear();
        _playerCollider = null;
    }

    internal CGamoriAttack CurrentAttack => IsCasting ? _currentAttack : null;

    internal void DamagePlayer(CPlayer player, float multiplier)
    {
        if (HpCurrent <= 0f || player.HpCurrent <= 0f) return;
        player.OnHit(this, new SDamageInfo(Attack * multiplier, false, false, null));
    }

    protected override void Die()
    {
        ResetBattleState();
        base.Die();
    }

    /// <summary>
    /// 진행 중인 공격을 중단합니다.
    /// </summary>
    public void ResetBattleState()
    {
        if (_currentAttack) _currentAttack.CancelAttack();
        _currentAttack = null;
    }

    protected virtual void OnDisable()
    {
        ResetBattleState();
        RestoreCollision();
    }
}
