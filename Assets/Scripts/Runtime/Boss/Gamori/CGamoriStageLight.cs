using UnityEngine;

/// <summary>
/// 가모리 캐릭터 셰이더가 쓰는 조명 방향을 정합니다. 무대(왕좌 앞) 기준으로 고정된 방향에서 비춰,
/// 맵의 태양 방향과 상관없이 얼굴 · 머리가 일정한 밝기로 보이게 합니다.
/// 이 컴포넌트가 없는 씬에서는 셰이더가 맵의 주 조명을 그대로 씁니다.
/// </summary>
[ExecuteAlways]
public sealed class CGamoriStageLight : MonoBehaviour
{
    private static readonly int _LIGHT = Shader.PropertyToID("_GamoriLightDir");

    [Tooltip("왕좌 앞 기준점 (앞 = 무대 쪽)")]
    [SerializeField] private Transform _throneFront;
    // 왕좌 기준 광원 방향. 양의 X는 보스의 왼쪽, Z는 정면이다.
    [SerializeField] private Vector3 _stageLightDirection = new Vector3(0.4864f, 0.5299f, 0.6947f);

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        Apply();
    }

    private void OnDisable()
    {
        Shader.SetGlobalVector(_LIGHT, Vector4.zero);
    }

    private void Apply()
    {
        if (!_throneFront) return;
        Quaternion stage = Quaternion.Euler(0f, _throneFront.eulerAngles.y, 0f);
        Vector3 direction = (stage * new Vector3(
            -_stageLightDirection.x, _stageLightDirection.y, _stageLightDirection.z)).normalized;
        Shader.SetGlobalVector(_LIGHT, new Vector4(direction.x, direction.y, direction.z, 1f));
    }
}
