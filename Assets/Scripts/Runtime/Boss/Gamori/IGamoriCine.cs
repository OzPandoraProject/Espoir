using UnityEngine;

/// <summary>
/// 가모리 연출 카메라에 재생 시간과 월드 좌표를 제공합니다.
/// </summary>
public interface IGamoriCine
{
    bool IsPlaying { get; }

    /// <summary>
    /// 연출의 현재 재생 시간(초)입니다.
    /// </summary>
    float PlayTime { get; }

    /// <summary>
    /// 카메라가 게임 시점으로 돌아가기 시작하는 시각입니다.
    /// </summary>
    float CameraOut { get; }

    /// <summary>
    /// 게임 시점으로 돌아가는 데 걸리는 시간(초)입니다.
    /// </summary>
    float ReturnTime { get; }

    CGamoriEntranceTrack Track { get; }

    /// <summary>
    /// 연출 위치를 월드 좌표로 변환합니다.
    /// </summary>
    Vector3 ToWorld(Vector3 localPosition);

    /// <summary>
    /// 연출 방향을 월드 방향으로 변환합니다.
    /// </summary>
    Vector3 ToWorldDirection(Vector3 localDirection);
}
