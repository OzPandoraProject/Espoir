using System;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class CDynamicBoneClothCollision : MonoBehaviour
{
    [Serializable]
    private struct SContact
    {
        public DynamicBoneCollider source;
        public DynamicBoneCollider target;
    }

    private struct SContactSettings
    {
        public float idleRadius;
        public float runRadius;
        public float idleHeight;
        public float runHeight;
    }

    private struct SClothSettings
    {
        public float radius;
        public Vector3 force;
    }

    [Header("충돌 설정")]
    [SerializeField] private DynamicBone[] _targets;
    [SerializeField] private SContact[] _contacts;

    [Header("애니메이션 설정")]
    [SerializeField] private string _idleStateName = "Idle";
    [SerializeField] private string _runStateName = "Run";
    [SerializeField, Min(0f)] private float _blendSpeed = 5f;

    [Header("대기 상태 설정")]
    [SerializeField, Min(0f)] private float _idleSkirtRadius = 0.012f;
    [SerializeField] private Vector3 _idleSkirtForce = new Vector3(0f, -0.003f, 0f);

    private Animator _animator;
    private SContactSettings[] _contactSettings;
    private SClothSettings[] _clothSettings;
    private float _idleWeight;
    private float _runWeight;
    private float _previousIdleWeight = -1f;

    private void Start()
    {
        _animator = GetComponent<Animator>();
        _contactSettings = new SContactSettings[_contacts.Length];
        _clothSettings = new SClothSettings[_targets.Length];

        for (int i = 0; i < _contacts.Length; i++)
        {
            SContact contact = _contacts[i];
            _contactSettings[i] = new SContactSettings
            {
                idleRadius = contact.source.m_Radius,
                runRadius = contact.target.m_Radius,
                idleHeight = contact.source.m_Height,
                runHeight = contact.target.m_Height
            };
        }

        for (int i = 0; i < _targets.Length; i++)
        {
            _clothSettings[i] = new SClothSettings
            {
                radius = _targets[i].m_Radius,
                force = _targets[i].m_Force
            };
        }
    }

    private void LateUpdate()
    {
        AnimatorStateInfo state = _animator.IsInTransition(0)
            ? _animator.GetNextAnimatorStateInfo(0)
            : _animator.GetCurrentAnimatorStateInfo(0);

        float idleWeight = state.IsName(_idleStateName) ? 1f : 0f;
        float runWeight = state.IsName(_runStateName) ? 1f : 0f;
        _idleWeight = Mathf.MoveTowards(_idleWeight, idleWeight, _blendSpeed * Time.deltaTime);
        _runWeight = Mathf.MoveTowards(_runWeight, runWeight, _blendSpeed * Time.deltaTime);

        for (int i = 0; i < _contacts.Length; i++)
        {
            SContactSettings settings = _contactSettings[i];
            _contacts[i].target.m_Radius = Mathf.Lerp(settings.idleRadius, settings.runRadius, _runWeight);
            _contacts[i].target.m_Height = Mathf.Lerp(settings.idleHeight, settings.runHeight, _runWeight);
        }

        if (_previousIdleWeight == _idleWeight)
        {
            return;
        }

        for (int i = 0; i < _targets.Length; i++)
        {
            DynamicBone target = _targets[i];
            SClothSettings settings = _clothSettings[i];
            target.m_Radius = Mathf.Lerp(settings.radius, _idleSkirtRadius, _idleWeight);
            target.m_Force = Vector3.Lerp(settings.force, _idleSkirtForce, _idleWeight);
            target.UpdateParameters();
        }

        _previousIdleWeight = _idleWeight;
    }
}
