using System.IO;
using UnityEngine;

/// <summary>
/// 가모리 연출의 카메라와 루트 본 트랙을 재생 시간에 맞춰 보간합니다.
/// </summary>
public sealed class CGamoriEntranceTrack
{
    // 프레임당 카메라 위치 3개, 방향 3개, 화각 1개, 루트 위치 3개.
    private const int _STRIDE = 10;

    private readonly int _COUNT;
    private readonly float _STEP;
    private readonly float[] _FRAMES;

    public float Duration => (_COUNT - 1) * _STEP;

    /// <summary>
    /// 바이너리 에셋에서 카메라와 루트 본의 프레임 데이터를 읽습니다.
    /// </summary>
    public CGamoriEntranceTrack(TextAsset data)
    {
        using var reader = new BinaryReader(new MemoryStream(data.bytes));
        reader.ReadBytes(4);
        _COUNT = reader.ReadInt32();
        _STEP = reader.ReadSingle();
        _FRAMES = new float[_COUNT * _STRIDE];
        for (int i = 0; i < _FRAMES.Length; i++) _FRAMES[i] = reader.ReadSingle();
    }

    /// <summary>
    /// 연출 로컬 좌표의 카메라 위치와 방향, 수직 시야각을 반환합니다.
    /// </summary>
    public void Camera(float time, out Vector3 position, out Vector3 forward, out float fov)
    {
        Find(time, out int frame, out float blend);
        position = Vector3.Lerp(Read(frame, 0), Read(frame + 1, 0), blend);
        forward = Vector3.Slerp(Read(frame, 3), Read(frame + 1, 3), blend);
        fov = Mathf.Lerp(_FRAMES[frame * _STRIDE + 6], _FRAMES[(frame + 1) * _STRIDE + 6], blend);
    }

    /// <summary>
    /// 연출 로컬 좌표의 루트 본 위치를 반환합니다.
    /// </summary>
    public Vector3 Root(float time)
    {
        Find(time, out int frame, out float blend);
        return Vector3.Lerp(Read(frame, 7), Read(frame + 1, 7), blend);
    }

    private void Find(float time, out int frame, out float blend)
    {
        float at = Mathf.Clamp(time / _STEP, 0f, _COUNT - 1);
        frame = Mathf.Min((int)at, _COUNT - 2);
        blend = at - frame;
    }

    private Vector3 Read(int frame, int offset)
    {
        int at = frame * _STRIDE + offset;
        return new Vector3(_FRAMES[at], _FRAMES[at + 1], _FRAMES[at + 2]);
    }
}
