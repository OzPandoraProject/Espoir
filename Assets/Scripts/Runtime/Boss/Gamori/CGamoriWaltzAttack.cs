using System;
using UnityEngine;

public class CGamoriWaltzAttack : CGamoriAttack
{
    [SerializeField] private float _finalMultiplier = 6.4f;
    [Serializable]
    private struct SBeat
    {
        [Min(0f)] public float warningStart;
        [Min(0f)] public float impactTime;
        public Vector2 origin;
        public float direction;
        [Min(0f)] public float radius;
        [Range(0f, 180f)] public float halfAngle;
    }

    [SerializeField] private CGamoriEffectClip _effects;
    [SerializeField] private string _stateName = "Base Layer.Waltz";
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField, Min(0f)] private float _heightTolerance = 1.5f;
    [SerializeField, Min(0f)] private float _duration;
    [SerializeField] private SBeat[] _beats;

    private Transform _target;
    private CharacterController _body;
    private Quaternion _facing;
    private Vector3 _origin;
    private int _nextImpact;

    protected override bool TryStart(Transform target)
    {
        if (_effects == null || !_effects.isActiveAndEnabled) return false;
        if (_beats == null || _beats.Length == 0 || !_effects.CanPlay) return false;
        float previousImpact = 0f;
        foreach (SBeat beat in _beats)
        {
            if (beat.warningStart < 0f || beat.warningStart >= beat.impactTime || beat.impactTime <= previousImpact
                || beat.impactTime >= _duration || beat.radius <= 0f || beat.halfAngle <= 0f || beat.halfAngle > 180f)
                return false;
            previousImpact = beat.impactTime;
        }

        if (!TryPlayMotion(_stateName)) return false;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0f) transform.rotation = Quaternion.LookRotation(direction);
        _facing = transform.rotation;
        _origin = transform.position;
        if (Physics.Raycast(_origin + Vector3.up * _heightTolerance, Vector3.down, out RaycastHit hit,
            _heightTolerance * 2f, _groundLayer, QueryTriggerInteraction.Ignore))
            _origin.y = hit.point.y;

        _target = target;
        _target.TryGetComponent(out _body);
        _nextImpact = 0;
        _effects.Play(() => PatternTime, _origin, _facing, transform.lossyScale.y);
        return true;
    }

    private void LateUpdate()
    {
        if (!IsCasting) return;
        if (_target == null || !_target.gameObject.activeInHierarchy
            || _effects == null || !_effects.isActiveAndEnabled || !CanAnimate)
        {
            CancelAttack();
            return;
        }

        float time = PatternTime;
        while (_nextImpact < _beats.Length && time >= _beats[_nextImpact].impactTime)
        {
            SBeat beat = _beats[_nextImpact++];
            Vector3 apex = _origin + _facing * (new Vector3(beat.origin.x, 0f, beat.origin.y) * Scale);
            Vector3 direction = _facing * Quaternion.Euler(0f, beat.direction, 0f) * Vector3.forward;
            Vector3 point = _target.position;
            if (_body != null && _body.enabled) point.y = _body.bounds.min.y;
            Vector3 offset = point - apex;
            if (Mathf.Abs(offset.y) > _heightTolerance) continue;
            offset.y = 0f;
            if (offset.sqrMagnitude > beat.radius * beat.radius * Scale * Scale) continue;
            if (Vector3.Angle(offset, direction) > beat.halfAngle) continue;
            if (_nextImpact == _beats.Length) HitTarget(_finalMultiplier);
            else HitTarget();
            if (!IsCasting || _target == null || !_target.gameObject.activeInHierarchy) return;
        }
        if (time >= _duration) CancelAttack();
    }

    protected override void OnCancel()
    {
        _target = null;
        _body = null;
        if (_effects != null) _effects.Stop();
    }
}
