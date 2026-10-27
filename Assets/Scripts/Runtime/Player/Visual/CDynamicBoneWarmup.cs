using UnityEngine;

public class CDynamicBoneWarmup : MonoBehaviour
{
    private const int _WARMUP_FRAMES = 2;

    private DynamicBone[] _bones;
    private float[] _blendWeights;
    private int _frames;

    private void OnEnable()
    {
        _bones = GetComponentsInChildren<DynamicBone>(true);
        _blendWeights = new float[_bones.Length];
        _frames = 0;

        for (int i = 0; i < _bones.Length; i++)
        {
            _blendWeights[i] = _bones[i].m_BlendWeight;
            _bones[i].m_BlendWeight = 0f;
        }
    }

    private void OnDisable()
    {
        if (_bones == null)
        {
            return;
        }

        for (int i = 0; i < _bones.Length; i++)
        {
            if (_bones[i] != null)
            {
                _bones[i].m_BlendWeight = _blendWeights[i];
            }
        }
    }

    private void LateUpdate()
    {
        _frames++;

        if (_frames >= _WARMUP_FRAMES)
        {
            enabled = false;
        }
    }
}
