using UnityEngine;

public class CGamoriClawAttack : CGamoriAttack
{
    [SerializeField] private CGamoriEffectClip _effects;
    [SerializeField] private AnimationClip _clip;
    [SerializeField] private string _stateName = "Base Layer.Claw";
    [SerializeField, Min(0f)] private float _warningStart;
    [SerializeField, Min(0f)] private float _firstHit;
    [SerializeField, Min(0f)] private float _secondHit;
    [SerializeField] private Vector2 _origin;
    [SerializeField] private float _angle;
    [SerializeField, Min(0f)] private float _radius;
    [SerializeField, Range(0f, 180f)] private float _halfAngle;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField, Min(0f)] private float _heightTolerance = 1.5f;

    private Transform _target;
    private CharacterController _body;
    private Vector3 _castOrigin;
    private Quaternion _facing;
    private Vector3 _apex;
    private Vector3 _direction;
    private int _nextHit;

    protected override float MotionLength => _clip ? _clip.length : 0f;

    protected override bool TryStart(Transform target)
    {
        if (!_effects || !_effects.isActiveAndEnabled || !_effects.CanPlay || !_clip) return false;
        if (_firstHit <= _warningStart || _secondHit <= _firstHit || _secondHit >= _clip.length) return false;
        if (_radius <= 0f || _halfAngle <= 0f || _halfAngle > 180f || _warningStart < 0f) return false;

        if (!TryPlayMotion(_stateName)) return false;

        Vector3 forward = target.position - transform.position;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0f) transform.rotation = Quaternion.LookRotation(forward);
        _facing = transform.rotation;
        _castOrigin = transform.position;
        if (Physics.Raycast(_castOrigin + Vector3.up * _heightTolerance, Vector3.down, out RaycastHit ground,
            _heightTolerance * 2f, _groundLayer, QueryTriggerInteraction.Ignore))
            _castOrigin.y = ground.point.y;
        _apex = _castOrigin + _facing * (new Vector3(_origin.x, 0f, _origin.y) * Scale);
        _direction = _facing * Quaternion.Euler(0f, _angle, 0f) * Vector3.forward;

        _target = target;
        _target.TryGetComponent(out _body);
        _nextHit = 0;
        _effects.Play(() => PatternTime, _castOrigin, _facing, transform.lossyScale.y);
        return true;
    }

    private void LateUpdate()
    {
        if (!IsCasting) return;
        if (!_target || !_target.gameObject.activeInHierarchy || !CanAnimate
            || !_effects || !_effects.isActiveAndEnabled)
        {
            CancelAttack();
            return;
        }

        while (_nextHit < 2 && PatternTime >= (_nextHit == 0 ? _firstHit : _secondHit))
        {
            _nextHit++;
            if (IsTargetInSector()) HitTarget();
            if (!IsCasting || !_target || !_target.gameObject.activeInHierarchy) return;
        }

        if (PatternTime >= _clip.length) CancelAttack();
    }

    private bool IsTargetInSector()
    {
        Vector3 point = _target.position;
        if (_body && _body.enabled) point.y = _body.bounds.min.y;
        Vector3 offset = point - _apex;
        if (Mathf.Abs(offset.y) > _heightTolerance) return false;
        offset.y = 0f;
        float reach = _radius * Scale;
        return offset.sqrMagnitude <= reach * reach && Vector3.Angle(offset, _direction) <= _halfAngle;
    }

    protected override void OnCancel()
    {
        _target = null;
        _body = null;
        if (_effects) _effects.Stop();
        SettleAtMotionRoot();
    }
}
