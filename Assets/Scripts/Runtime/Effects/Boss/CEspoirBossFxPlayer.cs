using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CEspoirBossFxPlayer : MonoBehaviour
{
    public enum EEffectGroup
    {
        Ground,
        Trail,
        Flash,
        Debris,
        Shockwave,
        Orb,
        Whirlwind,
        Beam,
        Shield
    }

    [Serializable]
    public class CElementSetting
    {
        public int objectIndex;
        public bool isGroup;
        public EEffectGroup group;
        public bool hidden;
        public float rotation;
        [Min(0.01f)] public float scale = 1f;
        public bool keepAfterAttack;
        public Vector3 offset;
        public bool overrideRadius;
        public bool overrideDuration;
        [Min(0.05f)] public float duration = 1f;
        [Min(0f)] public float innerRadius;
        [Min(0.01f)] public float outerRadius = 1f;
    }

    [SerializeField] private CElementSetting[] _elements;

    /// <summary>선택한 요소의 위치와 도넛 반지름 설정을 적용합니다.</summary>
    public void SetElements(CElementSetting[] elements)
    {
        if (elements != null && elements.Length > 0)
        {
            _elements = elements;
        }
    }

    public int PreviewGroup { get; set; } = -1;
    public int OnlyObjectIndex { get; set; } = -1;
    private bool _retainedGround;
    private readonly Dictionary<CElementSetting, Transform> _groupRoots = new Dictionary<CElementSetting, Transform>();

    /// <summary>같은 시각 효과를 구성하는 메시를 하나의 조절 묶음으로 분류합니다.</summary>
    public static EEffectGroup GetGroup(string shader)
    {
        switch (shader)
        {
            case "RearField": case "RingField": case "SafeField": case "SafeRing":
            case "SealField": case "SubBurstField": case "Trench": return EEffectGroup.Ground;
            case "Hook": return EEffectGroup.Trail;
            case "PunchFlash": return EEffectGroup.Flash;
            case "Rock": case "RockLine": return EEffectGroup.Debris;
            case "OrbCore": case "OrbHalo": return EEffectGroup.Orb;
            case "CylWall": case "CylRibbon": return EEffectGroup.Whirlwind;
            case "LbPillar": case "LbGlow": case "LbRing": case "Bolt":
            case "Basic": case "Beam": case "BeamDistort": return EEffectGroup.Beam;
            case "Shield": return EEffectGroup.Shield;
            default: return EEffectGroup.Shockwave;
        }
    }

    public TextAsset data;
    public CEspoirBossFxLibrary library;
    public float stageScale = 1f;
    public bool screenEffects = true;
    public bool effectLights = false;
    public float speed = 1f;

    [Min(0f)]
    public float holdTime;

    [Min(0f)]
    public float holdDuration;
    private float _holdElapsed;
    private bool _holdComplete;

    public Func<float> clock;

    public Renderer[] inkRenderers;

    public bool Finished { get; private set; }

    public bool HoldNext { get; private set; }
    public int Frame => _cur < 0 ? 0 : _cur;
    public CEspoirBossFxClipData Clip => _clip;
    public Transform Stage => _stage;
    public float Duration =>
        _clip == null ? 0f : _clip.G(_clip.frames - 1, CEspoirBossFxClipData.G_T);

    public float G(int ch) => _clip == null || _cur < 0 ? 0f : _clip.G(_cur, ch);

    public void GetGlowRenderers(List<KeyValuePair<Renderer, Material>> into)
    {
        foreach (var r in _live)
            if (r.go != null && r.go.activeSelf && r.mat != null && !r.src.distort)
                into.Add(new KeyValuePair<Renderer, Material>(r.mr, r.mat));
    }

    public bool ClockAlive => clock == null || !_clockLost;
    public IEnumerable<Renderer> InkEffectRenderers
    {
        get
        {
            foreach (var r in _live)
                if (r.go != null && r.ink && r.go.activeSelf && r.mat.GetFloat("uOuter") < 0.5f)
                    yield return r.mr;
        }
    }

    CEspoirBossFxClipData _clip;
    Transform _stage;
    int _cur = -1;
    float _t,
        _acc;
    bool _clockLost,
        _detached;

    public void DetachClock()
    {
        _detached = true;
    }

    readonly List<CEffectInstance> _live = new List<CEffectInstance>();
    readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
    Mesh[] _geoms;
    Light _orbLight,
        _fxLight;

    class CEffectInstance
    {
        public CEspoirBossFxClipData.CEffectObject src;
        public GameObject go;
        public MeshRenderer mr;
        public MeshFilter mf;
        public Material mat;
        public Mesh boltMesh;
        public int key;
        public bool vis,
            ink;
        public int bb;
        public float boltW;
        public float[] boltPts;
        public CElementSetting setting;
        public EEffectGroup group;
        public Transform adjustment;
        public bool supportsRadius;
        public float originalOuterRadius;
        public float originalInnerRadius;
        public Mesh sourceMesh;
        public Mesh adjustedMesh;
        public Vector3[] vertices;
        public Vector3[] sourceVertices;
        public float radiusScale = -1f;
        public bool customDuration;
        public float startTime;
        public float endTime;
        public float holdAt;
        public float duration;
        public float timeOffset;
    }

    void Awake()
    {
        if (data == null || library == null)
        {
            enabled = false;
            return;
        }
        _clip = library.GetClip(data);
        _stage = new GameObject("Stage").transform;
        _stage.SetParent(transform, false);
        ApplyStageScale();
        _geoms = new Mesh[_clip.geoms.Length];
    }

    void Start()
    {
        if (_clip == null)
            return;
        if (screenEffects)
            CEspoirBossFxScreen.Register(this);
        if (effectLights)
        {
            _orbLight = MakeLight("OrbLight");
            _fxLight = MakeLight("FxLight");
        }
    }

    Light MakeLight(string n)
    {
        var go = new GameObject(n);
        go.transform.SetParent(_stage, false);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.shadows = LightShadows.None;
        l.intensity = 0;
        l.enabled = false;
        return l;
    }

    public void ApplyStageScale()
    {
        if (_stage != null)
            _stage.localScale = new Vector3(-stageScale, stageScale, stageScale);
    }

    Mesh GeomMesh(int gi)
    {
        if (gi < 0)
            return null;
        if (_geoms[gi] != null)
            return _geoms[gi];
        var g = _clip.geoms[gi];
        var m = new Mesh { name = "EspoirBossFx_g" + gi };
        if (g.vcount > 65000)
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(g.pos);
        if (g.nrm != null)
            m.SetNormals(g.nrm);
        if (g.uv != null)
            m.SetUVs(0, g.uv);
        for (int s = 0; s < g.slots.Length; s++)
            m.SetUVs(s + 1, g.slots[s]);
        int start = Mathf.Max(0, g.drawStart);
        int total = g.idx != null ? g.idx.Length : g.vcount;
        int count = g.drawCount < 0 ? total - start : Mathf.Min(g.drawCount, total - start);
        var tri = new int[Mathf.Max(0, count - count % 3)];
        if (g.idx != null)
            Array.Copy(g.idx, start, tri, 0, tri.Length);

        else
            for (int i = 0; i < tri.Length; i++)
                tri[i] = start + i;
        m.SetTriangles(tri, 0);
        m.RecalculateBounds();

        m.bounds = new Bounds(m.bounds.center, m.bounds.size * 3f + Vector3.one * 20f);
        _geoms[gi] = m;
        _owned.Add(m);
        return m;
    }

    void Spawn(int oi)
    {
        if (_retainedGround || (OnlyObjectIndex >= 0 && oi != OnlyObjectIndex))
            return;
        var o = _clip.objs[oi];
        var sdef = _clip.shaders[o.shader];
        var sh = library.Get(sdef.name);
        if (sh == null)
            return;
        var r = new CEffectInstance
        {
            src = o,
            group = GetGroup(sdef.name),
            key = 0,
            ink = sdef.name == "Beam",
        };
        r.go = new GameObject("fx" + oi + "_" + sdef.name);
        r.go.layer = gameObject.layer;
        r.go.transform.SetParent(_stage, false);
        if (_elements != null)
        {
            foreach (CElementSetting setting in _elements)
            {
                if (setting != null && (setting.isGroup ? setting.group == r.group : setting.objectIndex == oi))
                {
                    r.setting = setting;
                    r.supportsRadius = Array.Exists(sdef.uniforms, u => u.name == "uR0");
                    if (setting.isGroup)
                    {
                        if (!_groupRoots.TryGetValue(setting, out r.adjustment))
                        {
                            r.adjustment = new GameObject(setting.group.ToString()).transform;
                            r.adjustment.SetParent(_stage, false);
                            _groupRoots.Add(setting, r.adjustment);
                        }
                    }

                    else
                    {
                        r.adjustment = new GameObject("ElementOffset").transform;
                        r.adjustment.SetParent(_stage, false);
                    }
                    r.go.transform.SetParent(r.adjustment, false);
                    _owned.Add(r.adjustment.gameObject);
                    break;
                }
            }
        }
        if (r.setting != null && r.setting.overrideDuration && r.supportsRadius)
        {
            _clip.GetElementTiming(oi, out r.startTime, out r.endTime, out r.holdAt);
            r.duration = Mathf.Max(0.05f, r.setting.duration);
            r.customDuration = true;
        }
        r.mf = r.go.AddComponent<MeshFilter>();
        r.mr = r.go.AddComponent<MeshRenderer>();
        r.mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.mr.receiveShadows = false;
        r.mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        r.mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        r.mat = new Material(sh) { name = "EspoirBossFx_" + sdef.name };

        r.mat.SetFloat("_SrcBlend", o.distort ? 5 : o.src);
        r.mat.SetFloat("_DstBlend", o.distort ? 10 : o.dst);
        r.mat.SetFloat("_ZWrite", o.distort ? 0 : o.zwrite);
        r.mat.SetFloat("_ZTest", o.ztest);
        r.mat.SetFloat("_Cull", o.cull);
        r.mat.renderQueue = o.queue;
        if (library.noise != null)
            r.mat.SetTexture("uNoise", library.noise);
        _owned.Add(r.mat);
        r.mr.sharedMaterial = r.mat;
        if (o.geom >= 0)
            r.mf.sharedMesh = GeomMesh(o.geom);

        else
        {
            r.boltMesh = new Mesh { name = "EspoirBossFx_bolt" + oi };
            r.boltMesh.MarkDynamic();
            _owned.Add(r.boltMesh);
            r.mf.sharedMesh = r.boltMesh;
        }
        r.go.SetActive(false);
        _live.Add(r);
    }

    void ApplyKey(CEffectInstance r, CEspoirBossFxClipData.CFrameKey k)
    {
        if ((k.flags & CEspoirBossFxClipData.F_GONE) != 0)
        {
            Kill(r.go);
            r.go = null;
            return;
        }
        if ((k.flags & CEspoirBossFxClipData.F_VIS) != 0)
            r.vis = k.vis;
        if ((k.flags & CEspoirBossFxClipData.F_MAT) != 0)
        {
            var m = k.matrix;
            Vector3 c0 = m.GetColumn(0),
                c1 = m.GetColumn(1),
                c2 = m.GetColumn(2);
            float det = Vector3.Dot(Vector3.Cross(c0, c1), c2);
            var t = r.go.transform;
            t.localPosition = m.GetColumn(3);
            t.localRotation =
                (c2.sqrMagnitude > 1e-14f && c1.sqrMagnitude > 1e-14f)
                    ? Quaternion.LookRotation(c2, c1)
                    : Quaternion.identity;
            t.localScale = new Vector3(
                c0.magnitude * (det < 0 ? -1f : 1f),
                c1.magnitude,
                c2.magnitude
            );
        }
        if ((k.flags & CEspoirBossFxClipData.F_BB) != 0)
            r.bb = k.bb;
        if ((k.flags & CEspoirBossFxClipData.F_UNI) != 0)
        {
            var sh = _clip.shaders[r.src.shader];
            foreach (var u in k.uni)
            {
                var un = sh.uniforms[u.index];
                var v = u.v;
                if (r.setting != null && un.name == "uR")
                {
                    r.originalOuterRadius = v[0];
                }

                else if (r.setting != null && un.name == "uR0")
                {
                    r.originalInnerRadius = v[0];
                }
                if (un.kind == CEspoirBossFxClipData.U_FLOAT)
                    r.mat.SetFloat(un.id, v[0]);

                else if (un.kind == CEspoirBossFxClipData.U_VEC)
                    r.mat.SetVector(
                        un.id,
                        new Vector4(
                            v[0],
                            v.Length > 1 ? v[1] : 0,
                            v.Length > 2 ? v[2] : 0,
                            v.Length > 3 ? v[3] : 0
                        )
                    );

                else
                {
                    int c = Mathf.Max(1, un.comps),
                        n = v.Length / c;
                    var arr = new Vector4[Mathf.Max(n, un.arrayLen)];
                    for (int j = 0; j < n; j++)
                        arr[j] = new Vector4(
                            v[j * c],
                            c > 1 ? v[j * c + 1] : 0,
                            c > 2 ? v[j * c + 2] : 0,
                            c > 3 ? v[j * c + 3] : 0
                        );
                    r.mat.SetVectorArray(un.id, arr);
                }
            }
        }
        if ((k.flags & CEspoirBossFxClipData.F_BOLT) != 0)
        {
            r.boltW = k.boltW;
            r.boltPts = k.boltPts;
        }
        if ((k.flags & CEspoirBossFxClipData.F_GEO) != 0 && k.geo >= 0)
            r.mf.sharedMesh = GeomMesh(k.geo);
    }

    void StepTo(int f)
    {
        for (int fi = _cur + 1; fi <= f; fi++)
        {
            var starts = _clip.startsAt[fi];
            if (starts != null)
                foreach (int oi in starts)
                    Spawn(oi);
            for (int li = _live.Count - 1; li >= 0; li--)
            {
                var r = _live[li];
                if (r.customDuration)
                    continue;
                var keys = r.src.keys;
                while (r.go != null && r.key < keys.Length && keys[r.key].frame <= fi)
                    ApplyKey(r, keys[r.key++]);
                if (r.go == null)
                    _live.RemoveAt(li);
            }
        }
        _cur = f;
    }

    bool IsHold(int f) =>
        f > 0
        && _clip.G(f, CEspoirBossFxClipData.G_T)
            <= _clip.G(f - 1, CEspoirBossFxClipData.G_T) + 1e-5f;

    void LateUpdate() => Advance(Time.deltaTime);

    public void Advance(float dt)
    {
        if (_clip == null || Finished)
            return;
        float ct = float.NaN;
        if (clock != null && !_clockLost && !_detached)
        {
            ct = clock();
            if (float.IsNaN(ct))
                _clockLost = true;
        }
        int last = _clip.frames - 1;
        if (_cur < 0)
            StepTo(0);
        int target = _cur;
        _acc += dt * speed;
        bool held = false;
        float now = float.IsNaN(ct) ? _t + dt * speed : ct;
        if (!_holdComplete && holdDuration > 0f && now >= holdTime)
        {
            _detached = true;
            _holdElapsed += dt;
            now = holdTime;
            if (_holdElapsed >= holdDuration)
            {
                _holdComplete = true;
            }
        }
        while (target < last)
        {
            int n = target + 1;
            if (IsHold(n))
            {
                if (now > _clip.G(n, CEspoirBossFxClipData.G_T) + _clip.dt * 1.5f)
                {
                    target = n;
                    continue;
                }
                if (_acc >= _clip.dt)
                {
                    _acc -= _clip.dt;
                    target = n;
                    continue;
                }
                held = true;
                break;
            }
            if (_clip.G(n, CEspoirBossFxClipData.G_T) <= now + _clip.dt * 0.5f)
            {
                target = n;
                _acc = 0;
                continue;
            }
            break;
        }
        if (!held)
        {
            _acc = 0;
            _t = Mathf.Max(_t, now);
        }
        if (target != _cur)
            StepTo(target);
        HoldNext = _cur < last && IsHold(_cur + 1);

        UpdateElementTimes();
        foreach (KeyValuePair<CElementSetting, Transform> group in _groupRoots)
        {
            CElementSetting setting = group.Key;
            group.Value.localPosition = new Vector3(-setting.offset.x, setting.offset.y, setting.offset.z);
            group.Value.localRotation = Quaternion.Euler(0f, -setting.rotation, 0f);
            group.Value.localScale = Vector3.one * Mathf.Max(0.01f, setting.scale);
        }
        var s2w = _stage.localToWorldMatrix;
        var cam = Camera.main;
        foreach (var r in _live)
        {
            bool on = r.vis && (r.setting == null || !r.setting.hidden)
                && (PreviewGroup < 0 || PreviewGroup == (int)r.group);
            if (r.go.activeSelf != on)
                r.go.SetActive(on);
            if (!on)
                continue;
            ApplyElement(r);
            if (r.customDuration && r.timeOffset > 0f)
                r.mat.SetFloat("uT", _t);
            r.mat.SetMatrix("_StageToWorld", r.adjustment == null ? s2w : r.adjustment.localToWorldMatrix);
            if (cam == null)
                continue;
            if (r.bb != 0)
                Billboard(r, cam);
            if (r.boltMesh != null)
                BuildBolt(r, cam);
        }
        if (effectLights)
            UpdateLights();
        if (_cur >= last && _t >= Duration && !HasPendingElements())
            Stop();
    }

    private void UpdateElementTimes()
    {
        for (int i = _live.Count - 1; i >= 0; i--)
        {
            CEffectInstance instance = _live[i];
            if (!instance.customDuration)
                continue;

            float originalDuration = Mathf.Max(0.05f, instance.endTime - instance.startTime);
            float time = _t;
            float extra = instance.duration - originalDuration;
            if (extra > 0f && time > instance.holdAt)
            {
                time = Mathf.Max(instance.holdAt, time - extra);
            }

            else if (extra < 0f && _t >= instance.startTime)
            {
                time = instance.startTime + (_t - instance.startTime) * originalDuration / instance.duration;
            }

            instance.timeOffset = _t - time;
            CEspoirBossFxClipData.CFrameKey[] keys = instance.src.keys;
            while (instance.go != null && instance.key < keys.Length
                && _clip.G(keys[instance.key].frame, CEspoirBossFxClipData.G_T) <= time)
            {
                ApplyKey(instance, keys[instance.key++]);
            }

            if (instance.go != null && time >= instance.endTime)
            {
                Kill(instance.go);
                instance.go = null;
            }

            if (instance.go == null)
                _live.RemoveAt(i);
        }
    }

    private bool HasPendingElements()
    {
        foreach (CEffectInstance instance in _live)
        {
            if (instance.customDuration && instance.go != null)
                return true;
        }

        return false;
    }

    private void ApplyElement(CEffectInstance instance)
    {
        CElementSetting setting = instance.setting;
        if (setting == null)
        {
            return;
        }

        if (!setting.isGroup)
            instance.adjustment.localPosition = new Vector3(-setting.offset.x, setting.offset.y, setting.offset.z);
        if (!instance.supportsRadius || instance.originalOuterRadius <= 0f)
        {
            return;
        }

        if (!setting.overrideRadius)
        {
            instance.mat.SetFloat("uR", instance.originalOuterRadius);
            instance.mat.SetFloat("uR0", instance.originalInnerRadius);
            if (instance.adjustedMesh != null && instance.mf.sharedMesh == instance.adjustedMesh)
            {
                instance.mf.sharedMesh = instance.sourceMesh;
            }

            return;
        }

        float outerRadius = Mathf.Max(0.01f, setting.outerRadius);
        float innerRadius = Mathf.Clamp(setting.innerRadius, 0f, outerRadius - 0.01f);
        instance.mat.SetFloat("uR", outerRadius);
        instance.mat.SetFloat("uR0", innerRadius);
        float scale = Mathf.Max(1f, outerRadius / instance.originalOuterRadius);
        if (scale <= 1f)
        {
            if (instance.adjustedMesh != null && instance.mf.sharedMesh == instance.adjustedMesh)
            {
                instance.mf.sharedMesh = instance.sourceMesh;
            }

            return;
        }

        Mesh mesh = instance.mf.sharedMesh;
        if (mesh != instance.adjustedMesh)
        {
            instance.sourceMesh = mesh;
            instance.sourceVertices = mesh.vertices;
            instance.vertices = new Vector3[instance.sourceVertices.Length];
            if (instance.adjustedMesh != null)
            {
                Kill(instance.adjustedMesh);
            }

            instance.adjustedMesh = Instantiate(mesh);
            _owned.Add(instance.adjustedMesh);
            instance.mf.sharedMesh = instance.adjustedMesh;
            instance.radiusScale = -1f;
        }

        if (Mathf.Approximately(instance.radiusScale, scale))
        {
            return;
        }

        Vector3[] source = instance.sourceVertices;
        for (int i = 0; i < source.Length; i++)
        {
            instance.vertices[i] = new Vector3(source[i].x * scale, source[i].y * scale, source[i].z);
        }

        instance.adjustedMesh.vertices = instance.vertices;
        instance.adjustedMesh.RecalculateBounds();
        instance.radiusScale = scale;
    }

    void UpdateLights()
    {
        float s = stageScale;
        int O = CEspoirBossFxClipData.G_ORB,
            L = CEspoirBossFxClipData.G_LIGHT;
        SetLight(
            _orbLight,
            G(O),
            G(O + 1),
            G(O + 2),
            G(O + 3),
            new Color(1f, 0.706f, 0.353f),
            45f * s
        );
        float dist = G(L + 7);
        SetLight(
            _fxLight,
            G(L),
            G(L + 1),
            G(L + 2),
            G(L + 3),
            new Color(G(L + 4), G(L + 5), G(L + 6)),
            (dist > 0 ? dist : 40f) * s
        );
    }

    static void SetLight(Light l, float i, float x, float y, float z, Color c, float range)
    {
        if (l == null)
            return;
        l.enabled = i > 0.01f;
        if (!l.enabled)
            return;
        l.transform.localPosition = new Vector3(x, y, z);
        l.color = c;
        l.range = range;
        l.intensity = Mathf.Min(8f, i * 0.05f);
    }

    void Billboard(CEffectInstance r, Camera cam)
    {
        var t = r.go.transform;
        var ct = cam.transform;
        Vector3 fwd,
            up;
        if (r.bb == 1)
        {
            fwd = -ct.forward;
            up = ct.up;
        }

        else
        {
            fwd = ct.position - t.position;
            fwd.y = 0;
            if (fwd.sqrMagnitude < 1e-6f)
                return;
            up = Vector3.up;
        }
        var fl = t.parent.InverseTransformVector(fwd);
        var ul = t.parent.InverseTransformVector(up);
        if (fl.sqrMagnitude < 1e-10f || ul.sqrMagnitude < 1e-10f)
            return;
        t.localRotation = Quaternion.LookRotation(fl.normalized, ul.normalized);
    }

    static readonly List<Vector3> _bv = new List<Vector3>();
    static readonly List<Vector2> _bu = new List<Vector2>();
    static readonly List<int> _bi = new List<int>();

    void BuildBolt(CEffectInstance r, Camera cam)
    {
        var pts = r.boltPts;
        var m = r.boltMesh;
        m.Clear();
        if (pts == null || pts.Length < 6)
            return;
        int n = pts.Length / 3;
        var camL = r.go.transform.parent.InverseTransformPoint(cam.transform.position);
        _bv.Clear();
        _bu.Clear();
        _bi.Clear();
        for (int i = 0; i < n; i++)
        {
            var p = new Vector3(pts[i * 3], pts[i * 3 + 1], pts[i * 3 + 2]);
            int a = Mathf.Max(0, i - 1),
                b = Mathf.Min(n - 1, i + 1);
            var tan = new Vector3(
                pts[b * 3] - pts[a * 3],
                pts[b * 3 + 1] - pts[a * 3 + 1],
                pts[b * 3 + 2] - pts[a * 3 + 2]
            );
            var side = Vector3.Cross(tan, camL - p);
            float l = side.magnitude;
            side = l < 1e-6f ? new Vector3(r.boltW * 0.5f, 0, 0) : side * (r.boltW * 0.5f / l);
            _bv.Add(p - side);
            _bv.Add(p + side);
            float s = n > 1 ? (float)i / (n - 1) : 0f;
            _bu.Add(new Vector2(-1, s));
            _bu.Add(new Vector2(1, s));
            if (i < n - 1)
            {
                int k = i * 2;
                _bi.Add(k);
                _bi.Add(k + 1);
                _bi.Add(k + 2);
                _bi.Add(k + 1);
                _bi.Add(k + 3);
                _bi.Add(k + 2);
            }
        }
        m.SetVertices(_bv);
        m.SetUVs(1, _bu);
        m.SetTriangles(_bi, 0);
        m.bounds = new Bounds(Vector3.zero, Vector3.one * 4000f);
        var t = r.go.transform;
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;
    }

    /// <summary>유지 설정된 장판만 남기고 현재 공격의 나머지 연출을 종료합니다.</summary>
    public void FinishAttack()
    {
        for (int i = _live.Count - 1; i >= 0; i--)
        {
            CEffectInstance instance = _live[i];
            if (instance.group == EEffectGroup.Ground && instance.setting != null
                && instance.setting.keepAfterAttack && !instance.setting.hidden)
                continue;
            Kill(instance.go);
            _live.RemoveAt(i);
        }
        if (_live.Count == 0)
        {
            Stop();
            return;
        }
        _retainedGround = true;
        DetachClock();
        CEspoirBossFxScreen.Unregister(this);
        effectLights = false;
        if (_orbLight != null) _orbLight.enabled = false;
        if (_fxLight != null) _fxLight.enabled = false;
    }

    public void Stop()
    {
        if (Finished)
            return;
        Finished = true;
        Cleanup();
        Kill(gameObject);
    }

    void Cleanup()
    {
        foreach (var r in _live)
            if (r.go != null)
                Kill(r.go);
        _live.Clear();
        foreach (var o in _owned)
            if (o != null)
                Kill(o);
        _owned.Clear();
        CEspoirBossFxScreen.Unregister(this);
    }

    static void Kill(UnityEngine.Object o)
    {
        if (Application.isPlaying)
            Destroy(o);

        else
            DestroyImmediate(o);
    }

    void OnDestroy()
    {
        if (!Finished)
        {
            Finished = true;
            Cleanup();
        }
    }
}
