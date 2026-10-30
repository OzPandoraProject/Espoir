using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
public class CEnvironmentGradeMask : MonoBehaviour
{
    private class CMaskRenderer
    {
        public Renderer renderer;
        public Material[] materials;
        public int subMeshCount;
        public int[] maskPasses;
        public int[] outlineMaskPasses;
        public int[] aoExcludePasses;
    }

    private static readonly List<CEnvironmentGradeMask> _TARGETS = new List<CEnvironmentGradeMask>();
    private static readonly Dictionary<Camera, CommandBuffer> _AO_EXCLUDE_BUFFERS =
        new Dictionary<Camera, CommandBuffer>();
    private static readonly List<Camera> _UNUSED_AO_CAMERAS = new List<Camera>();
    private const CameraEvent _AO_EXCLUDE_EVENT = CameraEvent.AfterImageEffectsOpaque;
    private static readonly int _OUTLINE_WIDTH = Shader.PropertyToID("_OutlineWidth");
    private static readonly int _OUTLINE_Z = Shader.PropertyToID("_OutlineZ");
    private static readonly int _MASK_OUTLINE_WIDTH = Shader.PropertyToID("_PlayerMaskOutlineWidth");
    private static readonly int _MASK_OUTLINE_Z = Shader.PropertyToID("_PlayerMaskOutlineZ");

    private readonly List<CMaskRenderer> _RENDERERS = new List<CMaskRenderer>();

    /// <summary>
    /// 환경 색 보정에서 제외할 캐릭터가 등록되어 있는지 반환합니다.
    /// </summary>

    public static bool HasTargets => _TARGETS.Count > 0;

    private void OnEnable()
    {
        _RENDERERS.Clear();

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = null;

            if (renderer is SkinnedMeshRenderer skinnedRenderer)
            {
                mesh = skinnedRenderer.sharedMesh;
            }

            else if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter meshFilter))
            {
                mesh = meshFilter.sharedMesh;
            }

            if (mesh == null)
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            int subMeshCount = Mathf.Min(mesh.subMeshCount, materials.Length);
            int[] maskPasses = new int[subMeshCount];
            int[] outlineMaskPasses = new int[subMeshCount];
            int[] aoExcludePasses = new int[subMeshCount];

            for (int i = 0; i < subMeshCount; i++)
            {
                Material material = materials[i];
                maskPasses[i] = material != null ? material.FindPass("ENVIRONMENT_MASK") : -1;
                outlineMaskPasses[i] = material != null ? material.FindPass("ENVIRONMENT_OUTLINE_MASK") : -1;
                aoExcludePasses[i] = material != null ? material.FindPass("AO_EXCLUDE") : -1;
            }

            _RENDERERS.Add(new CMaskRenderer
            {
                renderer = renderer,
                materials = materials,
                subMeshCount = subMeshCount,
                maskPasses = maskPasses,
                outlineMaskPasses = outlineMaskPasses,
                aoExcludePasses = aoExcludePasses
            });
        }

        if (_RENDERERS.Count > 0)
        {
            _TARGETS.Add(this);

            if (_TARGETS.Count == 1)
            {
                Camera.onPreCull += DrawAOExclude;
            }
        }
    }

    private void OnDisable()
    {
        _TARGETS.Remove(this);
        _RENDERERS.Clear();

        if (_TARGETS.Count == 0)
        {
            Camera.onPreCull -= DrawAOExclude;
        }

        ReleaseUnusedAOExcludeBuffers();
    }

    /// <summary>
    /// 화면 AO가 적용된 직후 AO_EXCLUDE 패스가 있는 재질(보스)을 원래 색으로 다시 그립니다.
    /// 반투명 이펙트보다 먼저 그려지므로 이펙트를 가리지 않습니다.
    /// </summary>

    private static void DrawAOExclude(Camera camera)
    {
        ReleaseUnusedAOExcludeBuffers();
        _AO_EXCLUDE_BUFFERS.TryGetValue(camera, out CommandBuffer command);

        if (command != null)
        {
            command.Clear();
        }

        bool hasDraws = false;

        foreach (CEnvironmentGradeMask target in _TARGETS)
        {
            foreach (CMaskRenderer item in target._RENDERERS)
            {
                Renderer renderer = item.renderer;

                if (renderer == null || !renderer.enabled || renderer.forceRenderingOff ||
                    !renderer.gameObject.activeInHierarchy ||
                    (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0)
                {
                    continue;
                }

                for (int i = 0; i < item.subMeshCount; i++)
                {
                    if (item.materials[i] != null && item.aoExcludePasses[i] >= 0)
                    {
                        if (command == null)
                        {
                            command = new CommandBuffer
                            {
                                name = "Espoir AO Exclude"
                            };
                            camera.AddCommandBuffer(_AO_EXCLUDE_EVENT, command);
                            _AO_EXCLUDE_BUFFERS.Add(camera, command);
                        }

                        command.DrawRenderer(renderer, item.materials[i], i, item.aoExcludePasses[i]);
                        hasDraws = true;
                    }
                }
            }
        }

        if (!hasDraws && command != null)
        {
            camera.RemoveCommandBuffer(_AO_EXCLUDE_EVENT, command);
            command.Release();
            _AO_EXCLUDE_BUFFERS.Remove(camera);
        }
    }

    private static void ReleaseUnusedAOExcludeBuffers()
    {
        foreach (KeyValuePair<Camera, CommandBuffer> pair in _AO_EXCLUDE_BUFFERS)
        {
            if (pair.Key == null || !pair.Key.isActiveAndEnabled || _TARGETS.Count == 0)
            {
                _UNUSED_AO_CAMERAS.Add(pair.Key);
            }
        }

        foreach (Camera camera in _UNUSED_AO_CAMERAS)
        {
            CommandBuffer command = _AO_EXCLUDE_BUFFERS[camera];

            if (camera != null)
            {
                camera.RemoveCommandBuffer(_AO_EXCLUDE_EVENT, command);
            }

            command.Release();
            _AO_EXCLUDE_BUFFERS.Remove(camera);
        }

        _UNUSED_AO_CAMERAS.Clear();
    }

    /// <summary>
    /// 현재 카메라에 표시되는 캐릭터와 검의 본체 및 외곽선을 마스크 대상에 그립니다.
    /// </summary>

    public static void DrawMasks(CommandBuffer command, Camera camera, Material maskMaterial)
    {
        foreach (CEnvironmentGradeMask target in _TARGETS)
        {
            foreach (CMaskRenderer item in target._RENDERERS)
            {
                Renderer renderer = item.renderer;

                if (renderer == null || !renderer.enabled || renderer.forceRenderingOff ||
                    !renderer.gameObject.activeInHierarchy ||
                    (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0)
                {
                    continue;
                }

                for (int i = 0; i < item.subMeshCount; i++)
                {
                    Material source = item.materials[i];

                    if (source == null)
                    {
                        continue;
                    }

                    if (item.maskPasses[i] >= 0)
                    {
                        command.DrawRenderer(renderer, source, i, item.maskPasses[i]);

                        if (item.outlineMaskPasses[i] >= 0)
                        {
                            command.DrawRenderer(renderer, source, i, item.outlineMaskPasses[i]);
                        }

                        continue;
                    }

                    command.DrawRenderer(renderer, maskMaterial, i, 0);

                    if (!source.HasProperty(_OUTLINE_WIDTH) || !source.HasProperty(_OUTLINE_Z))
                    {
                        continue;
                    }

                    float width = source.GetFloat(_OUTLINE_WIDTH);

                    if (width <= 0.01f)
                    {
                        continue;
                    }

                    command.SetGlobalFloat(_MASK_OUTLINE_WIDTH, width);
                    command.SetGlobalFloat(_MASK_OUTLINE_Z, source.GetFloat(_OUTLINE_Z));
                    command.DrawRenderer(renderer, maskMaterial, i, 1);
                }
            }
        }
    }
}
