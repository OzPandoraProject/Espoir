using UnityEngine;

public sealed class CGamoriRushAttack : CGamoriSequenceAttack
{
    [SerializeField] private float _finalMultiplier = 4f;
    [System.Serializable]
    private struct SStrike
    {
        public float warning, impact, distance, reach, width;
        public bool circular;
    }
    [SerializeField] private SStrike[] _strikes;
    [SerializeField] private LayerMask _groundLayer;
    // 이동 거리(m)는 이펙트 클립과 같은 1/60초 간격이다.
    [SerializeField] private float[] _travel;
    private Vector3 _center;
    private int _index;
    private bool _aimed;
    private Vector3 _origin;
    private Vector3 _direction;
    private float _aimTime;
    private bool _moving;

    protected override void BeginPattern()
    {
        _index = 0;
        _aimed = false;
        _moving = false;
    }

    protected override void UpdatePattern(float time)
    {
        Strike(time);
        if (!_moving) return;
        float advance = (Travel(MotionTimeAt(time)) - Travel(MotionTimeAt(_aimTime)))
            * Scale * CGamoriEffectClip.UNIT_SCALE;
        Vector3 next = _origin + _direction * advance;
        if (Physics.Raycast(next + Vector3.up, Vector3.down, out RaycastHit hit, 2f, _groundLayer,
            QueryTriggerInteraction.Ignore)) transform.position = hit.point;
    }

    private void Strike(float time)
    {
        while (_index < _strikes.Length)
        {
            SStrike strike = _strikes[_index];
            if (time < strike.warning) return;
            if (!_aimed)
            {
                Face(Player.transform.position);
                _origin = transform.position;
                _direction = transform.forward;
                _center = _origin + _direction * ((strike.circular ? strike.distance : strike.reach * 0.5f) * Scale);
                if (Physics.Raycast(_center + Vector3.up, Vector3.down, out RaycastHit ground, 2f,
                    _groundLayer, QueryTriggerInteraction.Ignore)) _center.y = ground.point.y;
                _aimed = true;
                _aimTime = time;
                _moving = _travel != null && _travel.Length > 1;
                Effects.Move(_index, _origin, transform.rotation);
            }
            if (!_moving)
            {
                float t = Mathf.InverseLerp(strike.warning, strike.impact, time);
                Vector3 step = _origin + _direction * (t * strike.distance * Scale);
                if (Physics.Raycast(step + Vector3.up, Vector3.down, out RaycastHit floor, 2f, _groundLayer,
                    QueryTriggerInteraction.Ignore)) transform.position = floor.point;
            }
            if (time < strike.impact) return;
            Vector3 offset = Player.transform.position - (strike.circular ? _center : _origin);
            float height = Mathf.Abs(offset.y);
            offset.y = 0f;
            float along = Vector3.Dot(offset, _direction);
            float width = strike.width * Scale;
            bool inside = strike.circular ? offset.sqrMagnitude <= width * width
                : along >= 0f && along <= strike.reach * Scale
                    && Mathf.Abs(Vector3.Dot(offset, transform.right)) <= width;
            if (height < 2f && inside)
            {
                if (_index == _strikes.Length - 1) HitTarget(_finalMultiplier);
                else HitTarget();
            }
            _index++;
            _aimed = false;
        }
    }

    private float Travel(float time)
    {
        float frame = Mathf.Clamp(time * 60f, 0f, _travel.Length - 1);
        int index = Mathf.Min((int)frame, _travel.Length - 2);
        return Mathf.Lerp(_travel[index], _travel[index + 1], frame - index);
    }
}
