using UnityEngine;

public sealed class CGamoriHeartAttack : CGamoriAttack
{
    [System.Serializable]
    private struct SHeartPath
    {
        public Vector3 origin;
        public Vector3 end;
        public Vector3 bend;
        public float launch;
        public float radius;

        /// <summary>
        /// 비행 시간에 따른 하트의 위치를 구합니다.
        /// </summary>
        public Vector3 Position(float time, float flight)
        {
            float t = Mathf.Clamp01((time - launch) / flight);
            return Vector3.Lerp(origin, end, t) + bend * Mathf.Sin(Mathf.PI * t);
        }
    }

    [SerializeField] private CGamoriEffectClip _effects;
    [SerializeField] private CStatusSO _slow;
    [SerializeField] private SHeartPath[] _paths;
    [SerializeField] private float _flightTime;
    [SerializeField] private float _duration;
    [SerializeField] private string _stateName;
    [SerializeField, Min(0f)] private float _aimLock = 0.12f;
    private bool _aimed;

    private CharacterController _body;
    private float _previous;
    private bool _hit;

    protected override bool TryStart(Transform target)
    {
        if (!_effects || !_effects.isActiveAndEnabled || !_effects.CanPlay || !_slow || _paths.Length == 0
            || !target.TryGetComponent(out _body) || !TryPlayMotion(_stateName)) return false;

        _aimed = false;
        Aim(target);
        _previous = 0f;
        _hit = false;
        _effects.Play(() => PatternTime, transform.position, transform.rotation, transform.lossyScale.y);
        return true;
    }

    private void LateUpdate()
    {
        if (!IsCasting) return;
        if (!Player || !_body || !Player.gameObject.activeInHierarchy)
        {
            CancelAttack();
            return;
        }
        float time = PatternTime;
        if (!_aimed)
        {
            Aim(Player.transform);
            _effects.Move(transform.position, transform.rotation);
            _aimed = time >= _paths[0].launch - _aimLock;
        }
        if (!_hit)
        {
            foreach (SHeartPath path in _paths)
            {
                if (time < path.launch || _previous > path.launch + _flightTime) continue;
                if (!TouchesPlayer(path)) continue;
                _hit = true;
                float hp = Player.HpCurrent;
                HitTarget();
                if (Player.HpCurrent < hp && Player.HpCurrent > 0f) Player.AddStatus(_slow.Id);
                break;
            }
        }
        _previous = time;
        if (time >= _duration) CancelAttack();
    }

    private void Aim(Transform target)
    {
        Vector3 aim = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
        Vector3 forward = Vector3.ProjectOnPlane(_paths[0].end - _paths[0].origin, Vector3.up);
        if (aim.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(aim) * Quaternion.Inverse(Quaternion.LookRotation(forward));
    }

    private bool TouchesPlayer(SHeartPath path)
    {
        Vector3 from = transform.TransformPoint(path.Position(_previous, _flightTime));
        Vector3 to = transform.TransformPoint(path.Position(PatternTime, _flightTime));
        Vector3 center = _body.bounds.center;
        Vector3 step = Vector3.ProjectOnPlane(to - from, Vector3.up);
        float t = step.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(center - from, step) / step.sqrMagnitude) : 0f;
        Vector3 contact = Vector3.Lerp(from, to, t);
        float half = Mathf.Max(0f, _body.bounds.extents.y - _body.bounds.extents.x);
        center.y = Mathf.Clamp(contact.y, center.y - half, center.y + half);
        float radius = path.radius + _body.bounds.extents.x;
        return (contact - center).sqrMagnitude <= radius * radius;
    }

    protected override void OnCancel()
    {
        if (_effects) _effects.Stop();
        _body = null;
    }
}
