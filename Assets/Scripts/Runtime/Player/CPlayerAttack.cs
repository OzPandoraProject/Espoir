using System.Collections.Generic;
using UnityEngine;

public class CPlayerAttack : MonoBehaviour
{
    [Header("공격 대상 설정")]
    [SerializeField] private LayerMask _targetLayer;
    [SerializeField] private CSkillSO _previewSkill;

    private SoundManager _soundManager;
    private const int _INITIAL_HIT_CAPACITY = 5;

    private Collider[] _hitColliders = new Collider[_INITIAL_HIT_CAPACITY];

    private readonly HashSet<CUnit> _HIT_TARGETS = new HashSet<CUnit>();

    private void Start()
    {
        if (_targetLayer.value == 0)
        {
            CLogger.Warn($"{name}: 공격 대상 레이어가 설정되지 않았습니다.");
        }
        _soundManager = FindFirstObjectByType<SoundManager>();
        
        if (_soundManager == null)
        {
            CLogger.Warn($"사운드매니저 찾기 실패");
        }
    }

    /// <summary>
    /// 공격 범위에 피해를 전달하고 체력이 감소한 대상이 있는지 반환합니다.
    /// </summary>

    public bool Attack(CUnit attacker, SSkillHit hit)
    {
        _HIT_TARGETS.Clear();

        int hitCount = FindTargets(hit.offset, hit.size);
        bool hasHit = false;

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

            if (!_HIT_TARGETS.Add(target))
            {
                continue;
            }

            SDamageInfo damageInfo = new SDamageInfo(attacker.Attack * hit.damageRatio);
            float previousHp = target.HpCurrent;
            target.OnHit(attacker, target, damageInfo);
            if (_soundManager != null)
            {
                _soundManager.PlaySE(EEffectSound.MonsterHit);
            }
            hasHit |= target.HpCurrent < previousHp;

            CLogger.Log($"{target.name} 현재 체력: {target.HpCurrent}");
        }

        return hasHit;
    }

    private int FindTargets(Vector3 offset, Vector3 size)
    {
        Vector3 attackCenter =
            transform.position + transform.rotation * offset;

        while (true)
        {
            int hitCount = Physics.OverlapBoxNonAlloc(
                attackCenter,
                size * 0.5f,
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
