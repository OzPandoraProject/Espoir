using System.Collections.Generic;
using UnityEngine;

public struct SPlayerHitResult
{
    public bool hasHit;
    public bool hitFullyMarkedTarget;
}

internal enum EHitMark
{
    None,
    Add,
    Fill,
    Refresh
}

public class CPlayerAttack : MonoBehaviour
{
    [Header("공격 대상 설정")]
    [SerializeField] private LayerMask _targetLayer;
    [SerializeField] private CSkillSO _previewSkill;

    private const int _INITIAL_HIT_CAPACITY = 5;

    private Collider[] _hitColliders = new Collider[_INITIAL_HIT_CAPACITY];

    private readonly HashSet<CUnit> _HIT_TARGETS = new HashSet<CUnit>();

    private void Start()
    {
        if (_targetLayer.value == 0)
        {
            CLogger.Warn($"{name}: 공격 대상 레이어가 설정되지 않았습니다.");
        }
    }

    /// <summary>
    /// 범위 내 대상에게 피해를 주고 적중 여부와 타격 전 낙인 상태를 반환합니다.
    /// </summary>

    public SPlayerHitResult Attack(CPlayer attacker, SSkillHit hit, bool appliesMark)
    {
        return Attack(attacker, hit, transform.position, transform.rotation,
            appliesMark ? EHitMark.Add : EHitMark.None);
    }

    internal SPlayerHitResult Attack(CPlayer attacker, SSkillHit hit, Vector3 position, Quaternion rotation,
        EHitMark mark, HashSet<CUnit> castTargets = null)
    {
        HashSet<CUnit> targets = castTargets;
        if (targets == null)
        {
            _HIT_TARGETS.Clear();
            targets = _HIT_TARGETS;
        }

        int hitCount = FindTargets(position + rotation * hit.offset, hit.size, rotation);
        SPlayerHitResult result = new SPlayerHitResult();

        for (int i = 0; i < hitCount; i++)
        {
            CUnit target = _hitColliders[i].GetComponentInParent<CUnit>();

            if (target == null || target == attacker)
            {
                continue;
            }

            if (target.HpCurrent <= 0f)
            {
                continue;
            }

            if (!targets.Add(target))
            {
                continue;
            }

            if (mark == EHitMark.Fill)
            {
                attacker.Passive.ApplyFullMark(target);
            }

            bool hadFullMark = attacker.Passive.HasFullMark(target);
            SDamageInfo damageInfo = new SDamageInfo(attacker.Attack * hit.damageRatio);
            float previousHp = target.HpCurrent;
            target.OnHit(attacker, target, damageInfo);
            if (target.HpCurrent < previousHp)
            {
                result.hasHit = true;
                result.hitFullyMarkedTarget |= hadFullMark;

                if (mark == EHitMark.Add && target.HpCurrent > 0f)
                {
                    attacker.Passive.ApplyMark(target);
                }

                else if (mark == EHitMark.Refresh && target.HpCurrent > 0f)
                {
                    attacker.Passive.RefreshMark(target);
                }
            }

            CLogger.Log($"{target.name} 현재 체력: {target.HpCurrent}");
        }

        return result;
    }

    private int FindTargets(Vector3 attackCenter, Vector3 size, Quaternion rotation)
    {
        while (true)
        {
            int hitCount = Physics.OverlapBoxNonAlloc(
                attackCenter,
                size * 0.5f,
                _hitColliders,
                rotation,
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
        if (_previewSkill == null || _previewSkill.Steps == null)
        {
            return;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.color = Color.yellow;
        Gizmos.matrix = Matrix4x4.TRS(
            transform.position,
            transform.rotation,
            Vector3.one
        );

        foreach (SSkillStep step in _previewSkill.Steps)
        {
            if (step.hits == null)
            {
                continue;
            }

            foreach (SSkillHit hit in step.hits)
            {
                Gizmos.DrawWireCube(hit.offset, hit.size);
            }
        }

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
