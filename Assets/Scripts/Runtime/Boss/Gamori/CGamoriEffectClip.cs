using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 가모리 공격 시간에 맞춰 위치별 이펙트를 재생합니다.
/// </summary>
[DefaultExecutionOrder(29000)]
public class CGamoriEffectClip : MonoBehaviour
{
    /// <summary>
    /// 0.969배 모델에서 기록한 이펙트 좌표를 캐릭터 스케일로 환산합니다.
    /// </summary>
    public const float UNIT_SCALE = 1.032f;

    private static readonly int _PATH_ADD = Shader.PropertyToID("_GamoriPathAdd");
    private readonly List<Transform> _HOLDERS = new List<Transform>();
    private static readonly List<KeyValuePair<Renderer, Material>> _RENDERERS =
        new List<KeyValuePair<Renderer, Material>>();

    [SerializeField] private CEspoirBossFxPlayer[] _effectPrefabs;

    private CEspoirBossFxPlayer[] _effects;
    private float?[] _pathAdd;
    private float _scale;
    private int _framesUntilStop;

    public bool CanPlay
    {
        get
        {
            if (_effectPrefabs == null || _effectPrefabs.Length == 0) return false;
            foreach (CEspoirBossFxPlayer prefab in _effectPrefabs)
            {
                if (!prefab) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 보스 크기와 공격 시간에 맞춰 이펙트를 재생합니다.
    /// </summary>
    public void Play(Func<float> clock, Vector3 origin, Quaternion rotation, float scale)
    {
        Stop();
        if (!CanPlay) return;
        _framesUntilStop = 0;
        _scale = scale * UNIT_SCALE;
        _effects = new CEspoirBossFxPlayer[_effectPrefabs.Length];
        _pathAdd = new float?[_effectPrefabs.Length];
        for (int i = 0; i < _effects.Length; i++)
        {
            CEspoirBossFxPlayer effect = Instantiate(_effectPrefabs[i], origin, rotation);
            effect.stageScale = _scale;
            effect.ApplyStageScale();
            effect.clock = clock;
            effect.gameObject.AddComponent<CGamoriFxLight>();
            _effects[i] = effect;
        }
        CGamoriFxShake.Follow(_effects[0]);
    }

    /// <summary>
    /// 첫 번째 이펙트의 위치와 회전을 설정합니다.
    /// </summary>
    public void Move(Vector3 origin, Quaternion rotation) => Move(0, origin, rotation);

    /// <summary>
    /// 지정한 이펙트의 위치와 회전을 설정합니다.
    /// </summary>
    public void Move(int index, Vector3 origin, Quaternion rotation)
    {
        if (!Has(index)) return;
        _effects[index].transform.SetPositionAndRotation(origin, rotation);
    }

    /// <summary>
    /// 이펙트의 전방 길이를 조절합니다.
    /// </summary>
    public void Stretch(int index, float forward)
    {
        if (!Has(index) || !_effects[index].Stage) return;
        _effects[index].Stage.localScale = new Vector3(-_scale, _scale, _scale * forward);
    }

    /// <summary>
    /// 꽃잎 크기를 유지하면서 이동 경로의 길이를 조절합니다.
    /// </summary>
    public void SetPathLength(int index, float ratio)
    {
        if (Has(index)) _pathAdd[index] = ratio - 1f;
    }

    /// <summary>
    /// 지정한 이펙트의 로컬 좌표 기준을 반환합니다.
    /// </summary>
    public Transform Stage(int index) => Has(index) ? _effects[index].Stage : null;

    /// <summary>
    /// 이펙트의 원점을 기준으로 크기를 조절합니다.
    /// </summary>
    public void Resize(int index, float ratio)
    {
        if (!Has(index) || !_effects[index].Stage) return;
        _effects[index].Stage.localScale = new Vector3(-_scale * ratio, _scale * ratio, _scale * ratio);
    }

    /// <summary>
    /// 기준점에서 목표까지 이펙트의 방향과 길이를 맞춥니다. 폭 변화는 지정한 배율로 제한합니다.
    /// </summary>
    public void Aim(int index, Vector3 pivot, Vector3 sourceOffset, Vector3 targetOffset, float maxWidthScale)
    {
        if (!Has(index) || sourceOffset.sqrMagnitude < 0.0001f || targetOffset.sqrMagnitude < 0.0001f) return;
        Transform holder = new GameObject("Aim").transform;
        holder.SetPositionAndRotation(pivot, Quaternion.LookRotation(sourceOffset, Vector3.up));
        _effects[index].transform.SetParent(holder, true);
        float lengthScale = targetOffset.magnitude / sourceOffset.magnitude;
        float widthScale = Mathf.Clamp(lengthScale, 1f / maxWidthScale, maxWidthScale);
        holder.rotation = Quaternion.LookRotation(targetOffset, Vector3.up);
        holder.localScale = new Vector3(widthScale, widthScale, lengthScale);
        _HOLDERS.Add(holder);
    }

    /// <summary>
    /// 지정한 이펙트의 재생을 중단합니다.
    /// </summary>
    public void StopOne(int index)
    {
        if (!Has(index)) return;
        _effects[index].Stop();
        _effects[index] = null;
    }

    private bool Has(int index) => _effects != null && index >= 0 && index < _effects.Length && _effects[index];

    /// <summary>
    /// 다음 이펙트의 첫 프레임이 그려질 때까지 종료를 두 프레임 늦춥니다.
    /// </summary>
    public void StopSoon()
    {
        if (_effects != null) _framesUntilStop = 2;
    }

    private void LateUpdate()
    {
        if (_effects == null) return;
        if (_framesUntilStop > 0 && --_framesUntilStop == 0)
        {
            Stop();
            return;
        }
        for (int i = 0; i < _effects.Length; i++)
        {
            if (!_effects[i] || !_pathAdd[i].HasValue) continue;
            _effects[i].GetGlowRenderers(_RENDERERS);
            foreach (KeyValuePair<Renderer, Material> pair in _RENDERERS)
            {
                if (pair.Value) pair.Value.SetFloat(_PATH_ADD, _pathAdd[i].Value);
            }
            _RENDERERS.Clear();
        }
    }

    /// <summary>
    /// 재생 중인 이펙트를 중단하고 기준 오브젝트를 정리합니다.
    /// </summary>
    public void Stop()
    {
        if (_effects == null) return;
        foreach (CEspoirBossFxPlayer effect in _effects)
            if (effect) effect.Stop();
        _effects = null;
        _pathAdd = null;
        foreach (Transform holder in _HOLDERS)
            if (holder) Destroy(holder.gameObject);
        _HOLDERS.Clear();
    }

    private void OnDisable()
    {
        Stop();
    }
}
