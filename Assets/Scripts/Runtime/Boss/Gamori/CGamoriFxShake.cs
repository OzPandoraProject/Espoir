using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 여러 공격이 겹치면 가장 강한 카메라 흔들림을 적용합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(30000)]
public class CGamoriFxShake : MonoBehaviour
{
    private const float _AMPLITUDE = 0.025f;

    private readonly List<CEspoirBossFxPlayer> _EFFECTS = new List<CEspoirBossFxPlayer>();
    private Vector3 _offset;
    private bool _moved;

    /// <summary>
    /// 메인 카메라에 흔들림을 적용할 이펙트를 등록합니다.
    /// </summary>
    public static void Follow(CEspoirBossFxPlayer effect)
    {
        if (!effect || effect.Finished) return;
        Camera camera = Camera.main;
        if (!camera) return;
        if (!camera.TryGetComponent(out CGamoriFxShake shake))
        {
            shake = camera.gameObject.AddComponent<CGamoriFxShake>();
            shake.hideFlags = HideFlags.DontSave;
        }
        if (!shake._EFFECTS.Contains(effect)) shake._EFFECTS.Add(effect);
        shake.enabled = true;
    }

    private void LateUpdate()
    {
        Restore();
        float shake = 0f;
        for (int i = _EFFECTS.Count - 1; i >= 0; i--)
        {
            CEspoirBossFxPlayer effect = _EFFECTS[i];
            if (!effect || !effect.isActiveAndEnabled || effect.Finished)
            {
                _EFFECTS.RemoveAt(i);
                continue;
            }
            shake = Mathf.Max(shake, effect.G(CEspoirBossFxClipData.G_SHAKE));
        }
        if (_EFFECTS.Count == 0)
        {
            enabled = false;
            return;
        }
        if (shake <= 0.0005f) return;
        float time = Time.unscaledTime;
        float amount = shake * _AMPLITUDE;
        _offset = transform.rotation * new Vector3(
            (Mathf.Sin(time * 91.7f) + Mathf.Sin(time * 47.3f)) * amount,
            (Mathf.Sin(time * 83.1f + 1.3f) + Mathf.Sin(time * 39.9f)) * amount,
            0f);
        transform.position += _offset;
        _moved = true;
    }

    private void OnPostRender()
    {
        Restore();
    }

    private void OnDisable()
    {
        Restore();
    }

    private void Restore()
    {
        if (!_moved) return;
        transform.position -= _offset;
        _moved = false;
    }
}
