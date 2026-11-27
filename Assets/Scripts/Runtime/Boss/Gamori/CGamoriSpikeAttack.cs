using UnityEngine;

public class CGamoriSpikeAttack : CGamoriAttack
{
    [SerializeField] private CGamoriEffectClip _effects;
    [SerializeField] private string _stateName = "Base Layer.ThornSpike";
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField, Min(0f)] private float _radius = 3.15f;
    [SerializeField, Min(0f)] private float _heightTolerance = 1.5f;
    [SerializeField, Min(0f)] private float _warningStart = 0.743f;
    [SerializeField, Min(0f)] private float _eruptionTime = 1.743f;
    [SerializeField, Min(0f)] private float _duration = 4.067f;

    private Transform _target;
    private CharacterController _body;
    private bool _locked;
    private bool _erupted;

    private Vector3 _center;

    protected override bool TryStart(Transform target)
    {
        if (_effects == null || !_effects.isActiveAndEnabled || !_effects.CanPlay) return false;
        if (_warningStart >= _eruptionTime || _eruptionTime >= _duration || _radius <= 0f) return false;

        if (!TryPlayMotion(_stateName)) return false;

        _target = target;
        _target.TryGetComponent(out _body);
        _locked = false;
        _erupted = false;
        AimAtTarget();
        _effects.Play(() => PatternTime, transform.position, transform.rotation, transform.lossyScale.y);
        PlaceEffects();
        return true;
    }

    private void LateUpdate()
    {
        if (!IsCasting) return;
        if (_target == null || !_target.gameObject.activeInHierarchy || _effects == null || !_effects.isActiveAndEnabled
            || !CanAnimate)
        {
            CancelAttack();
            return;
        }

        if (!_locked) AimAtTarget();
        PlaceEffects();
        if (!_erupted && PatternTime >= _eruptionTime)
        {
            _erupted = true;
            Vector3 offset = TargetFeet() - _center;
            float distanceSquared = offset.x * offset.x + offset.z * offset.z;
            if (Mathf.Abs(offset.y) <= _heightTolerance && distanceSquared <= _radius * _radius * Scale * Scale)
                HitTarget();
        }
        if (PatternTime >= _duration) CancelAttack();
    }

    // 첫 클립은 보스 손을, 두 번째 클립은 지면의 타격 위치를 따른다.
    private void PlaceEffects()
    {
        _effects.Move(0, transform.position, transform.rotation);
        _effects.Move(1, _center, transform.rotation);
    }

    private void AimAtTarget()
    {
        _center = TargetFeet();
        Vector3 direction = _center - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0f) transform.rotation = Quaternion.LookRotation(direction);
        if (PatternTime < _warningStart) return;

        _locked = true;
        Vector3 origin = _center + Vector3.up * _heightTolerance;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, _heightTolerance * 2f,
            _groundLayer, QueryTriggerInteraction.Ignore))
            _center = hit.point;
    }

    private Vector3 TargetFeet()
    {
        Vector3 point = _target.position;
        if (_body != null && _body.enabled) point.y = _body.bounds.min.y;
        return point;
    }

    protected override void OnCancel()
    {
        _target = null;
        _body = null;
        if (_effects != null) _effects.Stop();
    }
}
