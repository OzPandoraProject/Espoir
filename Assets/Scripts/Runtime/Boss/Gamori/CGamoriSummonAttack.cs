using System.Collections.Generic;
using UnityEngine;

public sealed class CGamoriSummonAttack : CGamoriSequenceAttack
{
    [SerializeField] private CGamoriDouble _double;
    [SerializeField] private float _summonTime;
    [SerializeField] private Vector3 _offset;
    [SerializeField, Range(0f, 1f)] private float _hpRatio;
    [SerializeField, Range(0f, 1f)] private float _attackRatio = 0.5f;
    [SerializeField, Min(0f)] private float _gateLockTime = 2.2f;
    [SerializeField] private TextAsset _walkData;
    [SerializeField] private AnimationClip _cloneMotion;
    [SerializeField] private float _cloneStart = 2.87f, _cloneClipStart = 2.4f, _cloneBlend = 0.5f;
    [SerializeField] private string _cloneIdleState = "Base Layer.Ready";
    [SerializeField] private Shader _crossShader;
    // 포털 위치는 분신, 통과 거리는 골반을 기준으로 측정한다.
    [SerializeField] private float _gateBack = 0.832f, _crossOut = 0.42f;

    private static readonly int _GATE_PLANE = Shader.PropertyToID("_GatePlane");
    private static readonly int _HIDE = Shader.PropertyToID("_Hide");
    private static readonly int _MAIN_TEX = Shader.PropertyToID("_MainTex");
    private readonly List<Material> _CROSS_MATERIALS = new List<Material>();
    private Renderer[] _walkerRenderers;
    private Material[][] _walkerMaterials;
    private Transform _walkerHips;
    private bool _crossing;
    private CGamoriBoss _owner;
    private CGamoriDouble _clonePrefab;
    private CGamoriCloneWalk _walk;
    private CGamoriDouble _walker;
    private CGamoriCloneEntrance _entrance;
    private bool _summoned;
    private bool _gateLocked;
    private Quaternion _gateFacing;

    protected override bool CanBegin()
    {
        _owner = Caster is CGamoriDouble clone ? clone.Owner : Caster as CGamoriBoss;
        if (!_owner || !_owner.CanSummonClone()) return false;
        _clonePrefab = _double;
        if (!_clonePrefab && _owner.TryGetComponent(out CGamoriSummonAttack summon)) _clonePrefab = summon._double;
        return _clonePrefab;
    }

    protected override void BeginPattern()
    {
        _owner.ReserveClone(this);
        Face(Player.transform.position);
        _summoned = false;
        _gateLocked = false;
        _gateFacing = transform.rotation;
        if (_walk == null && _walkData) _walk = new CGamoriCloneWalk(_walkData);
    }

    private Vector3 Spot => transform.TransformPoint(_offset * CGamoriEffectClip.UNIT_SCALE);

    protected override void UpdatePattern(float time)
    {
        if (!_gateLocked)
        {
            Vector3 spot = Spot;
            Camera view = Camera.main;
            Vector3 toward = view ? Vector3.ProjectOnPlane(view.transform.position - spot, Vector3.up) : Vector3.zero;
            if (toward.sqrMagnitude > 0.0001f) _gateFacing = Quaternion.LookRotation(toward);
            Effects.Move(1, spot, _gateFacing);
            _gateLocked = time >= _gateLockTime;
        }
        if (_summoned) return;

        if (_walk != null && !_walker && time >= _walk.StartTime)
        {
            _walker = Instantiate(_clonePrefab, Spot, _gateFacing);

            if (_walker.TryGetComponent(out CGamoriChase chase)) chase.enabled = false;
            _entrance = _walker.gameObject.AddComponent<CGamoriCloneEntrance>();
            _entrance.Begin(_walk, Effects.Stage(1), () => PatternTime, _cloneStart, _cloneMotion, _cloneClipStart,
                _cloneIdleState, _cloneBlend);
            BeginCross();
        }
        if (_walker) UpdateCross();

        if (time < _summonTime) return;
        _summoned = true;
        EndCross();
        CGamoriDouble unit = _walker ? _walker : Instantiate(_clonePrefab, Spot, transform.rotation);
        if (_entrance) _entrance.Finish();
        _walker = null;
        _entrance = null;
        if (unit.TryGetComponent(out CGamoriChase follow)) follow.enabled = true;
        unit.Summon(Player, _owner, Caster is CGamoriBoss, _owner.HpMax * _hpRatio, _owner.Attack * _attackRatio);
        _owner.RegisterClone(unit);
    }

    private void BeginCross()
    {
        if (!_crossShader) return;
        _walkerRenderers = _walker.GetComponentsInChildren<Renderer>();
        _walkerMaterials = new Material[_walkerRenderers.Length][];
        for (int i = 0; i < _walkerRenderers.Length; i++)
        {
            Material[] originals = _walkerRenderers[i].sharedMaterials;
            _walkerMaterials[i] = originals;
            var swapped = new Material[originals.Length];
            for (int k = 0; k < originals.Length; k++)
            {
                var material = new Material(_crossShader);
                Texture texture = originals[k] && originals[k].HasProperty(_MAIN_TEX)
                    ? originals[k].GetTexture(_MAIN_TEX) : null;
                material.SetTexture(_MAIN_TEX, texture);
                material.SetFloat(_HIDE, texture ? 0f : 1f);
                _CROSS_MATERIALS.Add(material);
                swapped[k] = material;
            }
            _walkerRenderers[i].sharedMaterials = swapped;
        }
        _walkerHips = _entrance.Hips;
        _crossing = true;
    }

    private void UpdateCross()
    {
        Transform stage = Effects.Stage(1);
        if (!_crossing || !stage) return;
        float unit = Scale * CGamoriEffectClip.UNIT_SCALE;
        Vector3 normal = stage.forward;
        float distance = Vector3.Dot(stage.position - normal * (_gateBack * unit), normal);
        var plane = new Vector4(normal.x, normal.y, normal.z, distance);
        foreach (Material material in _CROSS_MATERIALS) material.SetVector(_GATE_PLANE, plane);
        if (_walkerHips && Vector3.Dot(_walkerHips.position, normal) - distance > _crossOut * unit) EndCross();
    }

    private void EndCross()
    {
        if (_walkerRenderers != null)
        {
            for (int i = 0; i < _walkerRenderers.Length; i++)
            {
                if (_walkerRenderers[i]) _walkerRenderers[i].sharedMaterials = _walkerMaterials[i];
            }
        }
        foreach (Material material in _CROSS_MATERIALS) Destroy(material);
        _CROSS_MATERIALS.Clear();
        _walkerRenderers = null;
        _walkerMaterials = null;
        _walkerHips = null;
        _crossing = false;
    }

    protected override void EndPattern()
    {
        EndCross();
        if (_owner) _owner.ReleaseClone(this);
        if (_walker) Destroy(_walker.gameObject);
        _walker = null;
        _entrance = null;
    }
}
