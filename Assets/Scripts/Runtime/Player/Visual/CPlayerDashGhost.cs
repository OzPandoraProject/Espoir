using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CPlayerDashGhost : MonoBehaviour
{
    private static readonly int _DIRECTION = Shader.PropertyToID("_Dir");
    private static readonly int _GROUND_Y = Shader.PropertyToID("_GhostGroundY");
    private static readonly int _SEED = Shader.PropertyToID("_Seed");
    private static readonly int _PROGRESS = Shader.PropertyToID("_T");
    private static readonly int _ALPHA = Shader.PropertyToID("_AlphaNow");
    private static readonly int _SMEAR = Shader.PropertyToID("_SmearNow");

    private static CPlayerDashGhostPose _coatPose;
    private static Shader _depthShader;

    private readonly List<Mesh> _BAKED_MESHES = new List<Mesh>();
    private Material _colorMaterial;
    private Material _depthMaterial;
    private float _lifetime;
    private float _opacity;
    private float _smear;
    private float _hold;
    private float _age;

    public static int ActiveCount { get; private set; }

    /// <summary>
    /// 현재 자세를 복사해 이동 방향을 따라 사라지는 잔상을 생성합니다.
    /// </summary>

    public static CPlayerDashGhost Spawn(GameObject character, Material material, float life, float alpha,
        float smear, float hold, Vector3 direction, bool flare = true)
    {
        GameObject root = new GameObject("PlayerDashGhost");
        CPlayerDashGhost ghost = root.AddComponent<CPlayerDashGhost>();
        ghost._lifetime = life;
        ghost._opacity = alpha;
        ghost._smear = smear;
        ghost._hold = hold;
        ghost._colorMaterial = new Material(material);

        CharacterController controller = character.GetComponentInParent<CharacterController>();
        float ground = controller != null ? controller.bounds.min.y : character.transform.position.y;
        ghost._colorMaterial.SetVector(_DIRECTION, direction);
        ghost._colorMaterial.SetFloat(_GROUND_Y, ground);
        ghost._colorMaterial.SetFloat(_SEED, UnityEngine.Random.value * 7f);

        if (_depthShader == null)
        {
            _depthShader = Resources.Load<Shader>("Shaders/Player/Effects/PlayerGhostDepth");
        }

        ghost._depthMaterial = new Material(_depthShader);
        ghost._depthMaterial.CopyPropertiesFromMaterial(ghost._colorMaterial);
        ghost._depthMaterial.renderQueue = _depthShader.renderQueue;
        ghost.Capture(character, flare);
        ghost.UpdateMaterials();
        return ghost;
    }

    private void Capture(GameObject character, bool flare)
    {
        List<(Transform bone, Quaternion rotation)> original = null;

        if (flare)
        {
            if (_coatPose == null)
            {
                _coatPose = Resources.Load<CPlayerDashGhostPose>("Animations/Player/PlayerDashGhostPose");
            }

            if (_coatPose != null)
            {
                original = new List<(Transform, Quaternion)>();
                foreach (Transform bone in character.GetComponentsInChildren<Transform>())
                {
                    if (_coatPose.TryGetRotation(bone.name, out Quaternion rotation))
                    {
                        original.Add((bone, bone.localRotation));
                        bone.localRotation = rotation;
                    }
                }
            }
        }

        try
        {
            foreach (SkinnedMeshRenderer skin in character.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (skin.sharedMesh == null || !ShouldCapture(skin))
                {
                    continue;
                }

                Mesh mesh = new Mesh();
                _BAKED_MESHES.Add(mesh);
                skin.BakeMesh(mesh, true);
                AddPart(skin.transform, mesh, Vector3.one);
            }

            foreach (MeshFilter solid in character.GetComponentsInChildren<MeshFilter>())
            {
                if (solid.sharedMesh != null && solid.TryGetComponent(out MeshRenderer renderer) &&
                    ShouldCapture(renderer))
                {
                    AddPart(solid.transform, solid.sharedMesh, solid.transform.lossyScale);
                }
            }
        }
        finally
        {
            if (original != null)
            {
                foreach (var pose in original)
                {
                    pose.bone.localRotation = pose.rotation;
                }
            }
        }
    }

    private static bool ShouldCapture(Renderer renderer)
    {
        return renderer.enabled && renderer.gameObject.layer != CPlayerEffectRenderer.EFFECT_LAYER &&
            renderer.name.IndexOf("bag", StringComparison.OrdinalIgnoreCase) < 0;
    }

    private void AddPart(Transform source, Mesh mesh, Vector3 scale)
    {
        AddRenderer(source, mesh, scale, _depthMaterial);
        AddRenderer(source, mesh, scale, _colorMaterial);
    }

    private void AddRenderer(Transform source, Mesh mesh, Vector3 scale, Material material)
    {
        GameObject part = new GameObject(source.name);
        part.layer = CPlayerEffectRenderer.EFFECT_LAYER;
        part.transform.SetParent(transform, false);
        part.transform.SetPositionAndRotation(source.position, source.rotation);
        part.transform.localScale = scale;
        part.AddComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer renderer = part.AddComponent<MeshRenderer>();
        Material[] materials = new Material[Mathf.Max(1, mesh.subMeshCount)];

        for (int i = 0; i < materials.Length; i++)
        {
            materials[i] = material;
        }

        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private void OnEnable()
    {
        ActiveCount++;
    }

    private void OnDisable()
    {
        ActiveCount--;
    }

    private void Update()
    {
        _age += Time.unscaledDeltaTime;

        if (_age >= _lifetime)
        {
            Destroy(gameObject);
            return;
        }

        UpdateMaterials();
    }

    private void UpdateMaterials()
    {
        float t = Mathf.InverseLerp(0f, _lifetime, _age);
        float progress = Mathf.InverseLerp(_hold, 1f, t);
        float opacity = _opacity * Mathf.Min(1f, _age / 0.025f);
        float smear = _smear * (1f + t * 0.6f);

        _colorMaterial.SetFloat(_PROGRESS, progress);
        _depthMaterial.SetFloat(_PROGRESS, progress);
        _colorMaterial.SetFloat(_ALPHA, opacity);
        _depthMaterial.SetFloat(_ALPHA, opacity);
        _colorMaterial.SetFloat(_SMEAR, smear);
        _depthMaterial.SetFloat(_SMEAR, smear);
    }

    private void OnDestroy()
    {
        Destroy(_colorMaterial);
        Destroy(_depthMaterial);

        foreach (Mesh mesh in _BAKED_MESHES)
        {
            Destroy(mesh);
        }
    }
}
