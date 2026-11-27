using UnityEngine;

/// <summary>
/// 기본 자세로 전환하는 동안 루트 본의 수평 위치를 유지합니다.
/// </summary>
// 의상과 머리카락의 LateUpdate 전에 루트 위치를 보정한다.
[DefaultExecutionOrder(-200)]
public sealed class CGamoriSettle : MonoBehaviour
{
    private Transform _root;
    private Vector3 _place;
    private float _duration, _elapsed;

    /// <summary>
    /// 루트 본의 수평 위치를 지정한 시간 동안 고정합니다.
    /// </summary>
    public void Hold(Transform root, Vector3 place, float duration)
    {
        _root = root;
        _place = place;
        _duration = Mathf.Max(duration, 0.01f);
        _elapsed = 0f;
        enabled = true;
        // 공격이 LateUpdate에서 끝나는 프레임에도 보정을 적용한다.
        Apply();
    }

    private void LateUpdate()
    {
        _elapsed += Time.deltaTime;
        if (!_root || _elapsed >= _duration)
        {
            enabled = false;
            return;
        }
        Apply();
    }

    private void Apply()
    {
        Vector3 place = _root.position;
        place.x = _place.x;
        place.z = _place.z;
        _root.position = place;
    }
}
