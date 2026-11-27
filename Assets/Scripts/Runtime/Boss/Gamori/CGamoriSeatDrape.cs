using UnityEngine;

/// <summary>
/// 왕좌에 앉거나 일어설 때 의상 본을 앉은 자세와 보간합니다.
/// </summary>
// 의상 스프링의 LateUpdate가 끝난 뒤 앉은 자세를 적용한다.
[DefaultExecutionOrder(100)]
public sealed class CGamoriSeatDrape : MonoBehaviour
{
    [SerializeField] private Transform[] _bones;
    [SerializeField] private Quaternion[] _seated;
    [SerializeField, Min(0.01f)] private float _blend = 0.5f;

    private float _weight, _target, _speed;

    /// <summary>
    /// 앉은 자세로 전환하거나 원래 자세로 복귀합니다. 전환 시간이 0이면 즉시 적용합니다.
    /// </summary>
    public void SetSeated(bool isSeated, float blend = -1f)
    {
        _target = isSeated ? 1f : 0f;
        if (blend < 0f) blend = _blend;
        if (blend <= 0f) _weight = _target;
        else _speed = 1f / blend;
    }

    private void LateUpdate()
    {
        _weight = Mathf.MoveTowards(_weight, _target, _speed * Time.deltaTime);
        if (_weight <= 0f) return;
        float weight = _weight * _weight * (3f - 2f * _weight);
        for (int i = 0; i < _bones.Length; i++)
        {
            if (_bones[i]) _bones[i].localRotation = Quaternion.Slerp(_bones[i].localRotation, _seated[i], weight);
        }
    }
}
