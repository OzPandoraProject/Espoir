using System;
using UnityEngine;

public class CGamoriThornRing : MonoBehaviour
{
    [SerializeField] private CEspoirBossFxPlayer _effectPrefab;
    [SerializeField] private GameObject _rim;

    private CEspoirBossFxPlayer _effect;

    /// <summary>
    /// 보스 발밑을 원점으로 이펙트를 처음부터 재생합니다. 시각은 공격의 진행 시각을 그대로 따르고, 크기는 보스 배율을 따릅니다.
    /// </summary>
    public void Play(Func<float> clock, Vector3 origin, Quaternion rotation, float scale)
    {
        StopEffect();
        _effect = Instantiate(_effectPrefab, origin, rotation);
        _effect.stageScale = scale;
        _effect.ApplyStageScale();
        _effect.clock = clock;
        _effect.gameObject.AddComponent<CGamoriFxLight>();
        CGamoriFxShake.Follow(_effect);
    }

    /// <summary>
    /// 테두리 충돌을 켜거나 끕니다.
    /// </summary>
    public void SetBlocked(bool blocked)
    {
        if (_rim.activeSelf != blocked) _rim.SetActive(blocked);
    }

    private void StopEffect()
    {
        if (_effect) _effect.Stop();
        _effect = null;
    }

    private void OnDisable()
    {
        StopEffect();
    }
}
