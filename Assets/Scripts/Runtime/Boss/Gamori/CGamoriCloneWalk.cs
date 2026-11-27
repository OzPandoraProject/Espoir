using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 분신의 등장 시각에 해당하는 골반 위치를 문 기준 좌표에서 읽습니다.
/// </summary>
public sealed class CGamoriCloneWalk
{
    private const string _HIPS_NAME = "hips";
    // 뼈마다 위치 3개와 회전값 4개.
    private const int _BONE_BYTES = 7 * sizeof(float);

    private readonly int _START;
    private readonly float _STEP;
    private readonly Vector3[] _HIPS;

    public float StartTime => _START * _STEP;

    /// <summary>
    /// 등장 동작의 프레임별 골반 위치를 읽습니다.
    /// </summary>
    public CGamoriCloneWalk(TextAsset data)
    {
        using var reader = new BinaryReader(new MemoryStream(data.bytes));
        reader.ReadBytes(4);
        int boneCount = reader.ReadInt32();
        int hips = 0;
        for (int i = 0; i < boneCount; i++)
        {
            if (Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32())) == _HIPS_NAME) hips = i;
        }
        _START = reader.ReadInt32();
        int count = reader.ReadInt32();
        _STEP = reader.ReadSingle();
        _HIPS = new Vector3[count];
        long first = reader.BaseStream.Position + hips * _BONE_BYTES;
        for (int i = 0; i < count; i++)
        {
            reader.BaseStream.Position = first + (long)i * boneCount * _BONE_BYTES;
            _HIPS[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }

    /// <summary>
    /// 등장 시각에 맞춰 분신 골반의 월드 위치를 반환합니다.
    /// </summary>
    public Vector3 Hips(Transform stage, float time)
    {
        float at = Mathf.Clamp(time / _STEP - _START, 0f, _HIPS.Length - 1);
        int frame = Mathf.Min((int)at, _HIPS.Length - 2);
        return stage.TransformPoint(Vector3.Lerp(_HIPS[frame], _HIPS[frame + 1], at - frame));
    }
}
