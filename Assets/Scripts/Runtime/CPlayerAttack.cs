using System.Collections.Generic;
using UnityEngine;

public class CPlayerAttack : MonoBehaviour
{
    [Header("공격 범위 설정")]
    [SerializeField] private Vector3 _attackOffset;
    [SerializeField] private Vector3 _attackSize = Vector3.one;
    [SerializeField] private LayerMask _targetLayer;

    private const int _INITIAL_HIT_CAPACITY = 5; 

    private Collider[] _hitColliders = new Collider[_INITIAL_HIT_CAPACITY];

    private readonly HashSet<CUnit> _hitTargets = new HashSet<CUnit>();

    private void Start()
    {
        if (_targetLayer.value == 0)
        {
            CLogger.Warn($"{name}: 공격 대상 레이어가 설정되지 않았습니다.");
        }
    }

    /// <summary>
    /// 공격 범위에 있는 대상에게 피해를 전달합니다.
    /// </summary>
    
    public void Attack(CUnit attacker)
    {
        _hitTargets.Clear();

        int hitCount = FindTargets();

        for (int i = 0; i < hitCount; i++)
        {
            CUnit target = _hitColliders[i].GetComponentInParent<CUnit>();

            if (target == null)
            {
                continue;
            }

            if (target.HpCurrent <= 0f)
            {
                continue;
            }

            if (!_hitTargets.Add(target))
            {
                continue;
            }

            SDamageInfo damageInfo = new SDamageInfo(attacker.Attack);
            target.OnHit(attacker, target, damageInfo);

            CLogger.Log($"{target.name} 현재 체력: {target.HpCurrent}");
        }
    }

    private int FindTargets()
    {
        Vector3 attackCenter =
            transform.position + transform.rotation * _attackOffset;

        while (true)
        {
            int hitCount = Physics.OverlapBoxNonAlloc(
                attackCenter,
                _attackSize * 0.5f,
                _hitColliders,
                transform.rotation,
                _targetLayer,
                QueryTriggerInteraction.Ignore
            );

            if (hitCount < _hitColliders.Length)
            {
                return hitCount;
            }

            _hitColliders = new Collider[_hitColliders.Length * 2];
        }
    }

    private void OnDrawGizmosSelected()
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.color = Color.yellow;
        Gizmos.matrix = Matrix4x4.TRS(
            transform.position,
            transform.rotation,
            Vector3.one
        );

        Gizmos.DrawWireCube(_attackOffset, _attackSize);

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}