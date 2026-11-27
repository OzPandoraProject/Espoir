using UnityEngine;

public class CGamoriRingAttack : CGamoriAttack
{
    [SerializeField] private string _stateName;
    [SerializeField] private GameObject _ringPrefab;
    [SerializeField] private Vector2 _offset;
    [SerializeField, Min(0.1f)] private float _radius;
    [SerializeField, Min(0f)] private float _impactTime;
    [SerializeField, Min(0f)] private float _damageInterval;
    [SerializeField, Min(0f)] private float _releaseTime;
    [SerializeField, Min(0.1f)] private float _duration;
    [SerializeField] private LayerMask _groundLayer;

    internal bool DealsPeriodicDamage => _damageInterval > 0f;

    private Transform _target;
    private CharacterController _body;
    private CGamoriThornRing _ring;
    private float _nextHit;

    protected override bool TryStart(Transform target)
    {
        if (!_ringPrefab || _releaseTime <= _impactTime || _duration < _releaseTime) return false;
        Vector3 direction = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
        Vector3 local = new Vector3(_offset.x, 0f, _offset.y);
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction)
                * Quaternion.Inverse(Quaternion.LookRotation(local));
        Vector3 center = transform.position + transform.rotation * (local * Scale);
        if (!Physics.Raycast(center + Vector3.up * 2f, Vector3.down, out var ground, 4f,
            _groundLayer, QueryTriggerInteraction.Ignore) || !TryPlayMotion(_stateName)) return false;
        if (!_ring) _ring = Instantiate(_ringPrefab).GetComponent<CGamoriThornRing>();
        _ring.transform.SetPositionAndRotation(ground.point, transform.rotation);
        _ring.transform.localScale = Vector3.one * (_radius * Scale);
        _ring.gameObject.SetActive(true);
        _target = target;
        target.TryGetComponent(out _body);
        _nextHit = _impactTime;
        _ring.SetBlocked(false);
        _ring.Play(() => PatternTime, new Vector3(transform.position.x, ground.point.y, transform.position.z),
            transform.rotation, transform.lossyScale.y);
        return true;
    }

    private void LateUpdate()
    {
        if (!IsCasting) return;
        float time = PatternTime;
        if (!_target || !_target.gameObject.activeInHierarchy || time >= _duration)
        {
            CancelAttack();
            return;
        }
        bool active = time >= _impactTime && time < _releaseTime;
        _ring.SetBlocked(active);
        while (_nextHit <= time && _nextHit < _releaseTime)
        {
            _nextHit = _damageInterval > 0f ? _nextHit + _damageInterval : float.PositiveInfinity;
            Vector3 offset = _target.position - _ring.transform.position;
            float feet = _body && _body.enabled ? _body.bounds.min.y : _target.position.y;
            if (Mathf.Abs(feet - _ring.transform.position.y) < 1.5f &&
                offset.x * offset.x + offset.z * offset.z <= _radius * _radius * Scale * Scale) HitTarget();
            if (!IsCasting) return;
        }
    }

    protected override void OnCancel()
    {
        if (_ring) _ring.gameObject.SetActive(false);
        _target = null;
    }

    private void OnDestroy()
    {
        if (_ring) Destroy(_ring.gameObject);
    }
}
