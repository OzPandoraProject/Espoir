using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

/// <summary>
/// 해상도별 가우스 흐림을 합성해 밝은 영역의 번짐을 그립니다.
/// </summary>
public sealed class CChapter2BloomRenderer : PostProcessEffectRenderer<CChapter2Bloom>
{
    private const int _MIP_COUNT = 5;
    private const int _PASS_BRIGHT = 0;
    private const int _PASS_BLUR = 1;
    private const int _PASS_COMPOSITE = 2;
    private const int _PASS_ADD = 3;
    private const int _PASS_ADD_MASKED = 4;

    private static readonly int _THRESHOLD = Shader.PropertyToID("_Threshold");
    private static readonly int _BLUR = Shader.PropertyToID("_Blur");
    private static readonly int _FACTORS = Shader.PropertyToID("_Factors");
    private static readonly int _FACTOR_LAST = Shader.PropertyToID("_FactorLast");
    private static readonly int _ADD_SCALE = Shader.PropertyToID("_AddScale");
    private static readonly int _BLOOM_TEX = Shader.PropertyToID("_Chapter2BloomTex");
    private static readonly int _BRIGHT = Shader.PropertyToID("_Chapter2BloomBright");
    private static readonly int[] _KERNEL_RADIUS = { 3, 5, 7, 9, 11 };
    private static readonly float[] _BASE_FACTORS = { 1.0f, 0.8f, 0.6f, 0.4f, 0.2f };

    private readonly int[] _HORIZONTAL = new int[_MIP_COUNT];
    private readonly int[] _VERTICAL = new int[_MIP_COUNT];
    private readonly int[] _MIP_TEX = new int[_MIP_COUNT];
    private static readonly int _CHARACTER_MASK = Shader.PropertyToID("_Chapter2BloomCharacterMask");
    private static readonly int _CHARACTER_AMOUNT = Shader.PropertyToID("_CharacterAmount");

    private Shader _shader;
    private Material _maskMaterial;
    private static readonly int _WEIGHTS_ID = Shader.PropertyToID("_Weights");
    private readonly float[][] _WEIGHTS = new float[_MIP_COUNT][];

    /// <summary>
    /// 셰이더와 임시 텍스처 이름을 준비합니다.
    /// </summary>
    public override void Init()
    {
        _shader = Shader.Find("Hidden/Espoir/Chapter2Bloom");
        Shader maskShader = Shader.Find("Hidden/Espoir/PlayerPostMask");
        if (maskShader) _maskMaterial = new Material(maskShader) { hideFlags = HideFlags.HideAndDontSave };

        for (int i = 0; i < _MIP_COUNT; i++)
        {
            _WEIGHTS[i] = new float[11];
            float radius = _KERNEL_RADIUS[i];
            for (int k = 0; k < _KERNEL_RADIUS[i]; k++)
            {
                _WEIGHTS[i][k] = Mathf.Exp(-0.5f * k * k / (radius * radius));
            }
            _HORIZONTAL[i] = Shader.PropertyToID("_Chapter2BloomH" + i);
            _VERTICAL[i] = Shader.PropertyToID("_Chapter2BloomV" + i);
            _MIP_TEX[i] = Shader.PropertyToID("_Chapter2BloomMip" + i);
        }
    }

    /// <summary>
    /// 캐릭터 가림 판정에 사용할 깊이 텍스처를 요청합니다.
    /// </summary>
    public override DepthTextureMode GetCameraFlags()
    {
        return settings.characterAmount < 1f ? DepthTextureMode.Depth : DepthTextureMode.None;
    }

    /// <summary>
    /// 마스크 렌더링에 사용한 재질을 해제합니다.
    /// </summary>
    public override void Release()
    {
        RuntimeUtilities.Destroy(_maskMaterial);
    }

    /// <summary>
    /// 밝은 곳을 흐려 원본 화면에 더합니다. 캐릭터 위에는 정한 비율만큼만 얹습니다.
    /// </summary>
    public override void Render(PostProcessRenderContext context)
    {
        CommandBuffer cmd = context.command;
        cmd.BeginSample("Chapter2Bloom");
        PropertySheet sheet = context.propertySheets.Get(_shader);
        RenderTextureFormat format = context.sourceFormat;

        int width = Mathf.Max(1, Mathf.RoundToInt(context.width / 2f));
        int height = Mathf.Max(1, Mathf.RoundToInt(context.height / 2f));
        cmd.GetTemporaryRT(_BRIGHT, width, height, 0, FilterMode.Bilinear, format, RenderTextureReadWrite.Linear);
        sheet.properties.SetVector(_THRESHOLD, new Vector4(settings.threshold, settings.smoothWidth, 0f, 0f));
        cmd.BlitFullscreenTriangle(context.source, _BRIGHT, sheet, _PASS_BRIGHT);

        RenderTargetIdentifier input = _BRIGHT;

        for (int i = 0; i < _MIP_COUNT; i++)
        {
            cmd.GetTemporaryRT(_HORIZONTAL[i], width, height, 0, FilterMode.Bilinear, format,
                RenderTextureReadWrite.Linear);
            cmd.GetTemporaryRT(_VERTICAL[i], width, height, 0, FilterMode.Bilinear, format,
                RenderTextureReadWrite.Linear);
            sheet.properties.SetFloatArray(_WEIGHTS_ID, _WEIGHTS[i]);
            sheet.properties.SetVector(_BLUR, new Vector4(1f / width, 0f, _KERNEL_RADIUS[i], 0f));
            cmd.BlitFullscreenTriangle(input, _HORIZONTAL[i], sheet, _PASS_BLUR);
            sheet.properties.SetVector(_BLUR, new Vector4(0f, 1f / height, _KERNEL_RADIUS[i], 0f));
            cmd.BlitFullscreenTriangle(_HORIZONTAL[i], _VERTICAL[i], sheet, _PASS_BLUR);
            if (i > 0)
            {
                cmd.ReleaseTemporaryRT(_HORIZONTAL[i]);
            }
            cmd.SetGlobalTexture(_MIP_TEX[i], _VERTICAL[i]);
            input = _VERTICAL[i];
            width = Mathf.Max(1, Mathf.RoundToInt(width / 2f));
            height = Mathf.Max(1, Mathf.RoundToInt(height / 2f));
        }

        float radius = settings.radius;
        float strength = settings.strength;
        Vector4 factors = Vector4.zero;

        for (int i = 0; i < 4; i++)
        {
            factors[i] = Mathf.Lerp(_BASE_FACTORS[i], 1.2f - _BASE_FACTORS[i], radius) * strength;
        }

        float factorLast = Mathf.Lerp(_BASE_FACTORS[4], 1.2f - _BASE_FACTORS[4], radius) * strength;
        sheet.properties.SetVector(_FACTORS, factors);
        sheet.properties.SetFloat(_FACTOR_LAST, factorLast);
        sheet.properties.SetFloat(_ADD_SCALE, factors.x + factors.y + factors.z + factors.w + factorLast);
        cmd.BlitFullscreenTriangle(_VERTICAL[0], _HORIZONTAL[0], sheet, _PASS_COMPOSITE);

        cmd.SetGlobalTexture(_BLOOM_TEX, _HORIZONTAL[0]);

        bool hasMask = _maskMaterial && settings.characterAmount < 1f && CEnvironmentGradeMask.HasTargets;
        if (hasMask)
        {
            context.GetScreenSpaceTemporaryRT(cmd, _CHARACTER_MASK, 0, RenderTextureFormat.R8,
                RenderTextureReadWrite.Linear);
            cmd.SetRenderTarget(_CHARACTER_MASK);
            cmd.ClearRenderTarget(false, true, Color.clear);
            CEnvironmentGradeMask.DrawMasks(cmd, context.camera, _maskMaterial);
            cmd.SetGlobalTexture(_CHARACTER_MASK, _CHARACTER_MASK);
            sheet.properties.SetFloat(_CHARACTER_AMOUNT, settings.characterAmount);
        }
        cmd.BlitFullscreenTriangle(context.source, context.destination, sheet, hasMask ? _PASS_ADD_MASKED : _PASS_ADD);

        cmd.ReleaseTemporaryRT(_BRIGHT);
        if (hasMask) cmd.ReleaseTemporaryRT(_CHARACTER_MASK);

        for (int i = 0; i < _MIP_COUNT; i++)
        {
            if (i == 0)
            {
                cmd.ReleaseTemporaryRT(_HORIZONTAL[i]);
            }
            cmd.ReleaseTemporaryRT(_VERTICAL[i]);
        }

        cmd.EndSample("Chapter2Bloom");
    }
}
