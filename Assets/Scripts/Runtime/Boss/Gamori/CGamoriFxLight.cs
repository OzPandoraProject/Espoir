using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 가모리 이펙트에 기록된 바닥 광원을 챕터 2 맵의 등불 칸 하나에 넣어 바닥과 벽을 비춥니다.
/// 등불 칸이 다 차 있으면 가장 먼 등불 자리를 잠시 빌립니다.
/// </summary>
[DefaultExecutionOrder(31000)]
public class CGamoriFxLight : MonoBehaviour
{
    // Chapter2Common.cginc의 CHAPTER2_LAMP_SLOTS와 일치해야 한다.
    private const int _SLOT_COUNT = 40;

    private static readonly int _POSITION_ID = Shader.PropertyToID("_Chapter2LampPos");
    private static readonly int _COLOR_ID = Shader.PropertyToID("_Chapter2LampColor");
    private static readonly int _COUNT_ID = Shader.PropertyToID("_Chapter2LampCount");
    private static readonly List<Vector4> _POSITIONS = new List<Vector4>(_SLOT_COUNT);
    private static readonly List<Vector4> _COLORS = new List<Vector4>(_SLOT_COUNT);

    private CEspoirBossFxPlayer _effect;

    private void Awake()
    {
        _effect = GetComponent<CEspoirBossFxPlayer>();
    }

    // CChapter2Lamps가 등불을 갱신한 뒤에 이펙트 광원을 추가한다.
    private void LateUpdate()
    {
        if (!_effect || _effect.Finished || !_effect.Stage) return;
        int light = CEspoirBossFxClipData.G_LIGHT;
        float strength = _effect.G(light);
        if (strength <= 0.0001f) return;

        Shader.GetGlobalVectorArray(_POSITION_ID, _POSITIONS);
        Shader.GetGlobalVectorArray(_COLOR_ID, _COLORS);
        while (_POSITIONS.Count < _SLOT_COUNT) _POSITIONS.Add(Vector4.zero);
        while (_COLORS.Count < _SLOT_COUNT) _COLORS.Add(Vector4.zero);
        int slot = Mathf.Clamp((int)Shader.GetGlobalFloat(_COUNT_ID), 0, _SLOT_COUNT - 1);
        Vector3 position = _effect.Stage.TransformPoint(
            new Vector3(_effect.G(light + 1), _effect.G(light + 2), _effect.G(light + 3)));
        _POSITIONS[slot] = new Vector4(position.x, position.y, position.z, _effect.G(light + 7));
        _COLORS[slot] = new Vector4(_effect.G(light + 4), _effect.G(light + 5), _effect.G(light + 6), 0f) * strength;
        Shader.SetGlobalVectorArray(_POSITION_ID, _POSITIONS);
        Shader.SetGlobalVectorArray(_COLOR_ID, _COLORS);
        Shader.SetGlobalFloat(_COUNT_ID, slot + 1);
    }
}
