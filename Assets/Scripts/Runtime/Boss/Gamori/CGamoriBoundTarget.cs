using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 속박된 플레이어의 자세와 피격 재질을 제어합니다.
/// </summary>
// Animator가 갱신한 자세를 의상과 머리카락의 LateUpdate 전에 보정한다.
[DefaultExecutionOrder(-200)]
public sealed class CGamoriBoundTarget : MonoBehaviour
{
    private static readonly int _MAIN_TEX = Shader.PropertyToID("_MainTex");
    private static readonly int _RAMP = Shader.PropertyToID("_Ramp");
    private static readonly int _HOT = Shader.PropertyToID("_Hot");
    private static readonly int _DIS = Shader.PropertyToID("_Dis");
    private static readonly int _LEAN = Shader.PropertyToID("_Lean");
    private static readonly int _HEIGHT = Shader.PropertyToID("_Height");
    private static readonly int _BASE = Shader.PropertyToID("_Base");
    private static readonly int _PULL_DIR = Shader.PropertyToID("_PullDir");
    private static readonly int _GROW = Shader.PropertyToID("_Grow");
    private const float _BLEND = 0.12f, _PUSH = 0.16f, _GONE = 0.7f;
    private const float _MODEL_HEIGHT = 1.667f;

    private readonly List<Renderer> _HIDDEN = new List<Renderer>();
    private readonly List<Material> _INSTANCE_MATERIALS = new List<Material>();
    private readonly List<CEnvironmentGradeMask> _HIDDEN_MASKS = new List<CEnvironmentGradeMask>();
    private Renderer[] _body;
    private Material[][] _original;
    private CGamoriBoundPose _pose;
    private TextAsset _poseData;
    private Shader _shader;
    private CPlayer _player;
    private Transform _model;
    private Quaternion _modelRest, _facing;
    private Vector3 _pushDirection;
    private Vector3 _anchor;
    private Func<float> _clock;
    private float _stab, _pull;
    private bool _swapped, _gone;

    /// <summary>
    /// 속박 자세를 적용하고 모델 크기 배율을 반환합니다. 타격 시각은 속박 시작 기준 초 단위입니다.
    /// </summary>
    public float Begin(CPlayer player, TextAsset pose, Shader shader, Vector3 toBoss,
        Func<float> clock, float stab, float pull)
    {
        End();
        var animator = player.GetComponentInChildren<Animator>();
        if (!animator) return 1f;
        _player = player;
        _anchor = player.transform.position;
        _model = animator.transform;
        _modelRest = _model.localRotation;
        toBoss = Vector3.ProjectOnPlane(toBoss, Vector3.up);
        _facing = toBoss.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toBoss) : _model.rotation;
        _pushDirection = -(_facing * Vector3.forward);
        _clock = clock;
        _stab = stab;
        _pull = pull;
        _shader = shader;
        if (_pose == null || _poseData != pose)
        {
            _poseData = pose;
            _pose = new CGamoriBoundPose(pose);
        }
        _pose.Bind(_model);

        var body = new List<Renderer>();
        foreach (Renderer part in player.GetComponentsInChildren<Renderer>())
        {
            if (!part.enabled) continue;
            if (part.name == "Sword" || (part.transform.parent && part.transform.parent.name == "Sword"))
            {
                part.enabled = false;
                _HIDDEN.Add(part);
            }
            else if (part is SkinnedMeshRenderer && part.transform.IsChildOf(_model))
            {
                body.Add(part);
            }
        }
        _body = body.ToArray();
        return _model.lossyScale.y;
    }

    private void LateUpdate()
    {
        if (!_player || !_model) return;
        float time = _clock();
        _model.rotation = _facing;
        _pose.Apply(time, Mathf.Clamp01(time / _BLEND), _anchor);
        if (!_shader || time < _stab || _gone) return;

        if (!_swapped) Swap();
        float after = time - _stab;
        float hot = 1.3f * Mathf.Exp(-after / 0.3f) + 0.25f;
        float push = 1f - Mathf.Pow(1f - Mathf.Clamp01(after / 0.08f), 3f);
        float dissolve = time >= _pull ? 0.34f + 0.74f * Mathf.SmoothStep(0f, 1f, (time - _pull) / 0.55f)
            : 0.04f + 0.3f * Mathf.SmoothStep(0f, 1f, after / (_pull - _stab));
        float scale = _model.lossyScale.y;
        foreach (Material material in _INSTANCE_MATERIALS)
        {
            material.SetFloat(_HOT, hot);
            material.SetFloat(_DIS, dissolve);
            material.SetFloat(_LEAN, _PUSH * scale * push);
            material.SetFloat(_HEIGHT, _MODEL_HEIGHT * scale);
            material.SetFloat(_GROW, 0.0035f * scale);
            material.SetVector(_BASE, _model.position);
            material.SetVector(_PULL_DIR, _pushDirection);
        }
        if (time < _pull + _GONE) return;

        _gone = true;
        foreach (Renderer part in _player.GetComponentsInChildren<Renderer>())
        {
            if (!part.enabled) continue;
            part.enabled = false;
            _HIDDEN.Add(part);
        }
    }

    private void Swap()
    {
        _swapped = true;
        _original = new Material[_body.Length][];
        for (int i = 0; i < _body.Length; i++)
        {
            Material[] source = _body[i].sharedMaterials;
            var burning = new Material[source.Length];
            for (int k = 0; k < source.Length; k++)
            {
                var material = new Material(_shader);
                if (source[k] && source[k].HasProperty(_MAIN_TEX))
                    material.SetTexture(_MAIN_TEX, source[k].GetTexture(_MAIN_TEX));
                if (source[k] && source[k].HasProperty(_RAMP)) material.SetTexture(_RAMP, source[k].GetTexture(_RAMP));
                burning[k] = material;
                _INSTANCE_MATERIALS.Add(material);
            }
            _original[i] = source;
            _body[i].sharedMaterials = burning;
        }
        foreach (CEnvironmentGradeMask mask in _player.GetComponentsInChildren<CEnvironmentGradeMask>(true))
        {
            if (!mask.enabled) continue;
            mask.enabled = false;
            _HIDDEN_MASKS.Add(mask);
        }
    }

    /// <summary>
    /// 묶인 모습을 끝내고 플레이어를 원래대로 되돌립니다.
    /// </summary>
    public void End()
    {
        if (_clock == null) return;
        if (_swapped)
        {
            for (int i = 0; i < _body.Length; i++)
            {
                if (_body[i]) _body[i].sharedMaterials = _original[i];
            }
            foreach (Material material in _INSTANCE_MATERIALS) Destroy(material);
            _INSTANCE_MATERIALS.Clear();
            foreach (CEnvironmentGradeMask mask in _HIDDEN_MASKS)
            {
                if (mask) mask.enabled = true;
            }
            _HIDDEN_MASKS.Clear();
            _original = null;
        }
        foreach (Renderer part in _HIDDEN)
        {
            if (part) part.enabled = true;
        }
        _HIDDEN.Clear();
        if (_model) _model.localRotation = _modelRest;
        _pose.Release();
        _swapped = _gone = false;
        _clock = null;
        _body = null;
        _player = null;
        _model = null;
    }

    private void OnDisable()
    {
        End();
    }
}
