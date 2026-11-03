using System;
using System.Collections.Generic;
using UnityEngine;

public class CPlayerEffectStart : MonoBehaviour
{
    private const float _FIRST_STEP = 1f / 120f;
    private const float _SORT_SCALE = 1000f;

    private static readonly List<CPlayerEffectStart> _PLAYING = new List<CPlayerEffectStart>();

    private enum EScreenMode
    {
        Fade,
        Hold,
        Invert,
        Flash
    }

    [Serializable]
    private struct SCameraPunch
    {
        public float time;
        public float zoom;
        public float shake;
    }

    [Serializable]
    private struct SScreenCue
    {
        public float time;
        public float duration;
        public float strength;
        public EScreenMode mode;
    }

    [SerializeField] private SCameraPunch[] _punch = Array.Empty<SCameraPunch>();
    [SerializeField] private SScreenCue[] _screens = Array.Empty<SScreenCue>();

    private ParticleSystem _particles;
    private ParticleSystemRenderer[] _ribbons;
    private Vector3[] _centers;
    private bool _distorts;

    internal static int ActiveCount => _PLAYING.Count;
    internal static int DistortionCount { get; private set; }

    private void Awake()
    {
        ParticleSystemRenderer[] renderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
        List<ParticleSystemRenderer> ribbons = new List<ParticleSystemRenderer>();

        foreach (ParticleSystemRenderer particle in renderers)
        {
            particle.gameObject.layer = CPlayerEffectRenderer.EFFECT_LAYER;

            if (!particle.enabled || particle.sharedMaterial == null)
            {
                continue;
            }

            if (particle.sharedMaterial.GetTag("EspoirDistort", false) == "On")
            {
                _distorts = true;
            }

            if (particle.renderMode == ParticleSystemRenderMode.Mesh && particle.mesh != null)
            {
                ribbons.Add(particle);
            }
        }

        _ribbons = ribbons.ToArray();
        _centers = new Vector3[_ribbons.Length];

        for (int i = 0; i < _ribbons.Length; i++)
        {
            _centers[i] = _ribbons[i].mesh.bounds.center;
        }

        _particles = GetComponent<ParticleSystem>();
        _particles.Simulate(_FIRST_STEP, true, true, false);
        _particles.Play(true);
    }

    private void OnEnable()
    {
        _PLAYING.Add(this);

        if (_distorts)
        {
            DistortionCount++;
        }
    }

    private void OnDisable()
    {
        _PLAYING.Remove(this);

        if (_distorts)
        {
            DistortionCount--;
        }
    }

    internal static void GetCameraCues(out Vector2 punch, out Vector3 screen)
    {
        punch = Vector2.zero;
        screen = Vector3.zero;

        foreach (CPlayerEffectStart effect in _PLAYING)
        {
            if (effect._punch.Length == 0 && effect._screens.Length == 0)
            {
                continue;
            }

            float age = effect._particles.time;

            foreach (SCameraPunch cue in effect._punch)
            {
                float elapsed = age - cue.time;
                if (elapsed < 0f) break;

                punch.x = Mathf.Max(punch.x, cue.zoom * Mathf.Pow(0.002f, elapsed));
                punch.y = Mathf.Max(punch.y, cue.shake * Mathf.Pow(0.0008f, elapsed));
            }

            foreach (SScreenCue cue in effect._screens)
            {
                float elapsed = age - cue.time;
                if (elapsed < 0f) break;
                if (elapsed >= cue.duration) continue;

                float fade = 1f - elapsed / cue.duration;

                if (cue.mode == EScreenMode.Flash)
                {
                    screen.z = Mathf.Max(screen.z, cue.strength * fade);
                    continue;
                }

                float strength = cue.mode == EScreenMode.Fade ? cue.strength * fade * fade : cue.strength;
                if (strength <= screen.x) continue;

                screen.x = strength;
                screen.y = cue.mode == EScreenMode.Invert ? 1f : 0f;
            }
        }
    }

    internal static void Sort(Camera camera)
    {
        Vector3 eye = camera.transform.position;
        Vector3 forward = camera.transform.forward;

        foreach (CPlayerEffectStart effect in _PLAYING)
        {
            for (int i = 0; i < effect._ribbons.Length; i++)
            {
                ParticleSystemRenderer ribbon = effect._ribbons[i];
                Vector3 center = ribbon.transform.TransformPoint(effect._centers[i]);
                float depth = Vector3.Dot(center - eye, forward);
                ribbon.sortingFudge = depth * _SORT_SCALE - i * 0.01f;
            }
        }
    }
}
