using UnityEngine;

public class CDynamicBoneHairWind : MonoBehaviour
{
    [SerializeField] private DynamicBone[] _targets;
    [Tooltip("대상별 힘과 강성 완화 배율")]
    [SerializeField] private float[] _scales;
    [Tooltip("최고 속도일 때 추가할 힘")]
    [SerializeField] private float _maxForce = 0.02f;
    [Tooltip("최고 속도일 때 강성 배율")]
    [SerializeField, Range(0.2f, 1f)] private float _movingStiffness = 0.5f;
    [Tooltip("대상별 배율을 적용한 뒤 유지할 최소 강성 비율")]
    [SerializeField, Range(0f, 1f)] private float _minStiffnessRatio = 0.15f;
    [Tooltip("최대 힘을 적용할 이동 속도")]
    [SerializeField, Min(0.01f)] private float _fullSpeed = 5f;
    [Tooltip("뒤로 미는 힘에 더할 위쪽 비율")]
    [SerializeField] private float _lift = 0.15f;
    [SerializeField] private float _response = 6f;

    private Vector3 _previousPosition;
    private Vector3 _force;
    private float _amount;
    private float[] _baseStiffness;
    private Vector3[] _baseForces;
    private bool _hasPrevious;

    private void OnEnable()
    {
        _hasPrevious = false;
        _force = Vector3.zero;
        _amount = 0f;

        if (_targets == null)
        {
            return;
        }

        _baseStiffness = new float[_targets.Length];
        _baseForces = new Vector3[_targets.Length];

        for (int i = 0; i < _targets.Length; i++)
        {
            DynamicBone bone = _targets[i];

            if (bone == null)
            {
                continue;
            }

            _baseStiffness[i] = bone.m_Stiffness;
            _baseForces[i] = bone.m_Force;
        }
    }

    private void OnDisable()
    {
        if (_targets == null || _baseStiffness == null)
        {
            return;
        }

        int count = Mathf.Min(_targets.Length, _baseStiffness.Length);

        for (int i = 0; i < count; i++)
        {
            DynamicBone bone = _targets[i];

            if (bone == null)
            {
                continue;
            }

            bone.m_Force = _baseForces[i];
            bone.m_Stiffness = _baseStiffness[i];
            bone.UpdateParameters();
        }
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f || _targets == null)
        {
            return;
        }

        Vector3 position = transform.position;

        if (!_hasPrevious)
        {
            _previousPosition = position;
            _hasPrevious = true;
        }

        Vector3 velocity = (position - _previousPosition) / deltaTime;
        _previousPosition = position;
        velocity.y = 0f;

        float response = 1f - Mathf.Exp(-_response * deltaTime);
        float targetAmount = Mathf.Clamp01(velocity.magnitude / _fullSpeed);
        _amount = Mathf.Lerp(_amount, targetAmount, response);

        Vector3 targetForce = _force.normalized * _maxForce;

        if (velocity.sqrMagnitude > 1e-6f)
        {
            targetForce = (-velocity.normalized + Vector3.up * _lift) * _maxForce;
        }

        _force = Vector3.Lerp(_force, targetForce * targetAmount, response);

        for (int i = 0; i < _targets.Length; i++)
        {
            DynamicBone bone = _targets[i];

            if (bone == null)
            {
                continue;
            }

            float scale = _scales != null && i < _scales.Length ? _scales[i] : 1f;
            bone.m_Force = _baseForces[i] + _force * scale;

            float stiffnessRatio = Mathf.LerpUnclamped(1f, _movingStiffness, _amount * scale);
            float stiffness = _baseStiffness[i] * Mathf.Clamp(stiffnessRatio, _minStiffnessRatio, 1f);

            if (Mathf.Abs(bone.m_Stiffness - stiffness) > 1e-3f)
            {
                bone.m_Stiffness = stiffness;
                bone.UpdateParameters();
            }
        }
    }
}
