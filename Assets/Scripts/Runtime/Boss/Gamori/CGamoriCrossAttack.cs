using UnityEngine;

public sealed class CGamoriCrossAttack : CGamoriAttack
{
    [SerializeField] private CGamoriEffectClip _effects;
    [SerializeField] private AnimationClip _clip;
    [SerializeField] private string _stateName;
    [SerializeField] private float _hitTime;
    [SerializeField] private Vector2 _halfSize;
    [SerializeField] private Vector2 _center;
    [SerializeField] private float _heightTolerance;
    private Transform _target;
    private CharacterController _body;
    private bool _hitChecked;

    protected override float MotionLength => _clip ? _clip.length : 0f;

    protected override bool TryStart(Transform target)
    {
        if (!_effects || !_effects.isActiveAndEnabled || !_effects.CanPlay || !_clip) return false;
        if (!TryPlayMotion(_stateName)) return false;
        Vector3 direction = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
        if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
        _target = target;
        target.TryGetComponent(out _body);
        _hitChecked = false;
        _effects.Play(() => PatternTime, transform.position, transform.rotation, transform.lossyScale.y);
        return true;
    }

    private void LateUpdate()
    {
        if (!IsCasting) return;
        if (!_target || !_target.gameObject.activeInHierarchy)
        {
            CancelAttack();
            return;
        }

        float time = PatternTime;
        if (!_hitChecked && time >= _hitTime)
        {
            _hitChecked = true;
            Vector3 point = _target.position;
            if (_body && _body.enabled) point.y = _body.bounds.min.y;
            Vector3 offset = Quaternion.Inverse(transform.rotation) * (point - transform.position);
            if (Mathf.Abs(offset.x - _center.x) <= _halfSize.x && Mathf.Abs(offset.z - _center.y) <= _halfSize.y
                && Mathf.Abs(offset.y) <= _heightTolerance) HitTarget();
        }
        if (time >= _clip.length) CancelAttack();
    }

    protected override void OnCancel()
    {
        if (_effects) _effects.Stop();
        _target = null;
        SettleAtMotionRoot();
    }
}
