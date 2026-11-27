using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 속박된 플레이어의 골반 위치와 뼈 회전을 보간합니다.
/// </summary>
public sealed class CGamoriBoundPose
{
    private readonly int _COUNT, _BONE_COUNT;
    private readonly float _STEP, _LOOP_START;
    private readonly string[] _NAMES;
    private readonly float[] _FRAMES;
    private Transform[] _bones;
    private Vector3 _hipsRest;

    // 프레임마다 골반 위치 3개와 각 뼈의 회전값 4개를 읽는다.
    private int Stride => 3 + _BONE_COUNT * 4;

    /// <summary>
    /// 골반 위치, 뼈 회전과 반복 시작 시각을 읽습니다.
    /// </summary>
    public CGamoriBoundPose(TextAsset data)
    {
        using var reader = new BinaryReader(new MemoryStream(data.bytes));
        reader.ReadBytes(4);
        _COUNT = reader.ReadInt32();
        _STEP = reader.ReadSingle();
        _LOOP_START = reader.ReadSingle();
        _BONE_COUNT = reader.ReadInt32();
        _NAMES = new string[_BONE_COUNT];
        for (int i = 0; i < _BONE_COUNT; i++) _NAMES[i] = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadByte()));
        _FRAMES = new float[_COUNT * Stride];
        for (int i = 0; i < _FRAMES.Length; i++) _FRAMES[i] = reader.ReadSingle();
    }

    /// <summary>
    /// 모델의 뼈를 자세 데이터의 순서에 맞춰 연결합니다.
    /// </summary>
    public void Bind(Transform model)
    {
        _bones = new Transform[_BONE_COUNT];
        foreach (Transform bone in model.GetComponentsInChildren<Transform>(true))
        {
            int index = System.Array.IndexOf(_NAMES, bone.name);
            if (index >= 0 && !_bones[index]) _bones[index] = bone;
        }
        if (_bones[0]) _hipsRest = _bones[0].localPosition;
    }

    /// <summary>
    /// 현재 동작과 속박 자세를 보간하고 골반의 수평 위치를 고정합니다.
    /// </summary>
    public void Apply(float time, float weight, Vector3 anchor)
    {
        if (_bones == null) return;
        float length = (_COUNT - 1) * _STEP;
        if (time > _LOOP_START) time = _LOOP_START + Mathf.Repeat(time - _LOOP_START, length - _LOOP_START);
        float at = Mathf.Clamp(time / _STEP, 0f, _COUNT - 1);
        int frame = Mathf.Min((int)at, _COUNT - 2);
        float blend = at - frame;
        int a = frame * Stride, b = a + Stride;
        if (_bones[0])
        {
            Vector3 hips = Vector3.Lerp(new Vector3(_FRAMES[a], _FRAMES[a + 1], _FRAMES[a + 2]),
                new Vector3(_FRAMES[b], _FRAMES[b + 1], _FRAMES[b + 2]), blend);
            _bones[0].localPosition = Vector3.Lerp(_bones[0].localPosition, hips, weight);
            Vector3 position = _bones[0].position;
            position.x = anchor.x;
            position.z = anchor.z;
            _bones[0].position = position;
        }
        for (int i = 0; i < _BONE_COUNT; i++)
        {
            if (!_bones[i]) continue;
            int p = a + 3 + i * 4, q = b + 3 + i * 4;
            Quaternion from = new Quaternion(_FRAMES[p], _FRAMES[p + 1], _FRAMES[p + 2], _FRAMES[p + 3]);
            Quaternion to = new Quaternion(_FRAMES[q], _FRAMES[q + 1], _FRAMES[q + 2], _FRAMES[q + 3]);
            Quaternion rotation = Quaternion.Slerp(from, to, blend);
            _bones[i].localRotation = Quaternion.Slerp(_bones[i].localRotation, rotation, weight);
        }
    }

    /// <summary>
    /// 원래 골반 위치를 복원하고 뼈 연결을 해제합니다.
    /// </summary>
    public void Release()
    {
        if (_bones != null && _bones[0]) _bones[0].localPosition = _hipsRest;
        _bones = null;
    }
}
