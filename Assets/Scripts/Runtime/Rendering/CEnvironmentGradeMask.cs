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
    }

    private static readonly List<CEnvironmentGradeMask> _TARGETS = new List<CEnvironmentGradeMask>();
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
            _RENDERERS.Add(new CMaskRenderer
            {
                renderer = renderer,
                materials = materials,
                subMeshCount = Mathf.Min(mesh.subMeshCount, materials.Length)
            });
        }

        if (_RENDERERS.Count > 0)
        {
            _TARGETS.Add(this);
        }
    }

    private void OnDisable()
    {
        _TARGETS.Remove(this);
        _RENDERERS.Clear();
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
