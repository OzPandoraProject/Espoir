using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public sealed class CEnvironmentGradeRenderer : PostProcessEffectRenderer<CEnvironmentGrade>
{
    private static readonly int _GRADE = Shader.PropertyToID("_Grade");
    private static readonly int _SHADOW_TONE = Shader.PropertyToID("_ShadowTone");
    private static readonly int _HIGHLIGHT_TONE = Shader.PropertyToID("_HighlightTone");
    private static readonly int _VIGNETTE_COLOR = Shader.PropertyToID("_VignetteColor");
    private static readonly int _DETAIL = Shader.PropertyToID("_Detail");
    private static readonly int _CHARACTER_MASK = Shader.PropertyToID("_CharacterMask");
    private static readonly int _HEAT_HAZE = Shader.PropertyToID("_HeatHaze");
    private static readonly int _HEAT_HAZE_TIME = Shader.PropertyToID("_HeatHazeTime");

    private Shader _shader;
    private Material _maskMaterial;
    private Camera _camera;
    private PostProcessLayer _layer;

    /// <summary>
    /// 색 보정에 사용할 셰이더를 저장합니다.
    /// </summary>

    public override void Init()
    {
        _shader = Shader.Find("Hidden/Espoir/EnvironmentGrade");
        _maskMaterial = new Material(Shader.Find("Hidden/Espoir/PlayerPostMask"))
        {
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    /// <summary>
    /// 캐릭터 가림 판정에 사용할 깊이 텍스처를 요청합니다.
    /// </summary>

    public override DepthTextureMode GetCameraFlags()
    {
        return DepthTextureMode.Depth;
    }

    /// <summary>
    /// 마스크 렌더링에 사용한 재질을 해제합니다.
    /// </summary>

    public override void Release()
    {
        RuntimeUtilities.Destroy(_maskMaterial);
    }

    /// <summary>
    /// 빛 번짐 이후 선형 공간에서 맵의 색감과 밝기를 보정합니다.
    /// </summary>

    public override void Render(PostProcessRenderContext context)
    {
        PropertySheet sheet = context.propertySheets.Get(_shader);
        sheet.properties.SetVector(
            _GRADE,
            new Vector4(settings.exposure, settings.saturation, settings.contrast, settings.toneAmount)
        );
        sheet.properties.SetColor(_SHADOW_TONE, settings.shadowTone.value.linear);
        sheet.properties.SetColor(_HIGHLIGHT_TONE, settings.highlightTone.value.linear);
        sheet.properties.SetColor(_VIGNETTE_COLOR, settings.vignetteColor.value.linear);
        sheet.properties.SetVector(
            _DETAIL,
            new Vector4(settings.vignette, settings.grain, Time.time, settings.shadowProtection)
        );

        bool hasMask = CEnvironmentGradeMask.HasTargets;

        if (hasMask)
        {
            context.GetScreenSpaceTemporaryRT(
                context.command, _CHARACTER_MASK, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear
            );
            context.command.SetRenderTarget(_CHARACTER_MASK);
            context.command.ClearRenderTarget(false, true, Color.clear);
            CEnvironmentGradeMask.DrawMasks(context.command, context.camera, _maskMaterial);
            context.command.SetGlobalTexture(_CHARACTER_MASK, _CHARACTER_MASK);
        }

        else
        {
            context.command.SetGlobalTexture(_CHARACTER_MASK, Texture2D.blackTexture);
        }

        if (hasMask)
        {
            SetMaskCoordinates(context, sheet);
        }

        else
        {
            sheet.properties.SetVector(_HEAT_HAZE, Vector4.zero);
        }

        context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);

        if (hasMask)
        {
            context.command.ReleaseTemporaryRT(_CHARACTER_MASK);
        }
    }

    private void SetMaskCoordinates(PostProcessRenderContext context, PropertySheet sheet)
    {
        if (_camera != context.camera)
        {
            _camera = context.camera;
            _layer = _camera.GetComponent<PostProcessLayer>();
        }

        CToonPost toon = _layer != null ? _layer.GetSettings<CToonPost>() : null;
        Vector4 haze = Vector4.zero;

        if (toon != null && toon.IsEnabledAndSupported(context) && toon.heatHazeStrength.value > 0f)
        {
            Vector2 distance = toon.heatHazeDistance.value;
            float startDistance = Mathf.Max(0f, distance.x);
            float endDistance = Mathf.Max(startDistance + 0.01f, distance.y);
            haze = new Vector4(toon.heatHazeStrength, startDistance, endDistance, _camera.aspect);
        }

        sheet.properties.SetVector(_HEAT_HAZE, haze);
        sheet.properties.SetFloat(_HEAT_HAZE_TIME, Time.time);
    }
}
