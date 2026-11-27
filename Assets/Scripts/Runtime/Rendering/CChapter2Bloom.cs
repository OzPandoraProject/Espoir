using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

/// <summary>
/// 밝은 영역의 번짐 강도와 범위를 설정합니다.
/// </summary>
[Serializable]
[PostProcess(typeof(CChapter2BloomRenderer), PostProcessEvent.BeforeStack, "Espoir/Chapter2 Bloom")]
public sealed class CChapter2Bloom : PostProcessEffectSettings
{
    [Tooltip("번짐 세기")]
    [Range(0f, 3f)] public FloatParameter strength = new FloatParameter { value = 0.9f };

    [Tooltip("넓은 번짐 비중")]
    [Range(0f, 1f)] public FloatParameter radius = new FloatParameter { value = 0.8f };

    [Tooltip("번짐이 시작되는 밝기")]
    [Range(0f, 4f)] public FloatParameter threshold = new FloatParameter { value = 0.68f };

    [Tooltip("시작 밝기 경계의 부드러운 폭")]
    [Range(0.001f, 1f)] public FloatParameter smoothWidth = new FloatParameter { value = 0.01f };

    [Tooltip("캐릭터에 적용할 번짐 비율")]
    [Range(0f, 1f)] public FloatParameter characterAmount = new FloatParameter { value = 1f };

    /// <summary>
    /// 세기가 0 이면 효과를 건너뜁니다.
    /// </summary>
    public override bool IsEnabledAndSupported(PostProcessRenderContext context)
    {
        return enabled.value && strength.value > 0f;
    }
}
