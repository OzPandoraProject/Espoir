using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct SDashGhostStyle
{
    [Min(0f)] public float lifetime;
    [Range(0f, 1f)] public float alpha;
    [Min(0f)] public float smear;
    [Range(0f, 1f)] public float hold;
}

[Serializable]
public struct SDashTrailSettings
{
    public GameObject startShock;
    public GameObject endShock;
    public float startHeight;
    public float endHeight;
    public Material ghostMaterial;
    [Min(0.01f)] public float firstDistance;
    [Min(0.01f)] public float spacing;
    [Range(0f, 1f)] public float lateProgress;
    [Min(0f)] public float earlyLifetimeStep;
    [Min(0f)] public float alphaStep;
    [Range(0f, 1f)] public float minAlpha;
    public Vector2 ghostWindow;
    public SDashGhostStyle departure;
    public SDashGhostStyle early;
    public SDashGhostStyle late;
}

internal class CPlayerDashTrail
{
    private Vector3 _origin;
    private float _nextDistance;
    private int _ghostCount;
    private bool _spawnedRushGhost;

    internal void Begin(SDashTrailSettings settings, Transform player, Animator animator, Vector3 direction,
        List<GameObject> effects)
    {
        Reset(player.position, settings.firstDistance);
        SpawnShock(settings.startShock, player.position, settings.startHeight, effects);

        if (settings.ghostMaterial != null)
        {
            SpawnGhost(animator, settings.ghostMaterial, settings.departure, direction, false, effects);
        }
    }

    internal void Update(SDashTrailSettings settings, Transform player, Animator animator, Vector3 direction,
        float progress, List<GameObject> effects)
    {
        if (progress >= 1f)
        {
            SpawnShock(settings.endShock, player.position, settings.endHeight, effects);
            return;
        }

        UpdateGhosts(settings, player, animator, direction, progress, effects);
    }

    internal void Reset(Vector3 origin, float firstDistance)
    {
        _origin = origin;
        _ghostCount = 0;
        _nextDistance = firstDistance;
        _spawnedRushGhost = false;
    }

    internal void UpdateRush(SDashTrailSettings settings, Transform player, Animator animator, Vector3 direction,
        float time, List<GameObject> effects)
    {
        if (settings.ghostMaterial == null || time < settings.ghostWindow.x || time >= settings.ghostWindow.y)
        {
            return;
        }

        if (!_spawnedRushGhost)
        {
            SpawnGhost(animator, settings.ghostMaterial, settings.departure, direction, true, effects);
            _spawnedRushGhost = true;
        }

        UpdateGhosts(settings, player, animator, direction, time, effects);
    }

    private void UpdateGhosts(SDashTrailSettings settings, Transform player, Animator animator, Vector3 direction,
        float progress, List<GameObject> effects)
    {
        float distance = Vector3.Dot(player.position - _origin, direction);

        if (settings.ghostMaterial == null || progress <= 0f || distance < _nextDistance)
        {
            return;
        }

        _nextDistance = distance + settings.spacing;
        _ghostCount++;
        bool isLate = progress > settings.lateProgress;
        SDashGhostStyle style = isLate ? settings.late : settings.early;

        if (!isLate)
        {
            style.lifetime += _ghostCount * settings.earlyLifetimeStep;
        }

        style.alpha = Mathf.Clamp(style.alpha - _ghostCount * settings.alphaStep, settings.minAlpha, 1f);
        SpawnGhost(animator, settings.ghostMaterial, style, direction, true, effects);
    }

    private void SpawnGhost(Animator animator, Material material, SDashGhostStyle style, Vector3 direction,
        bool flare, List<GameObject> effects)
    {
        CPlayerDashGhost ghost = CPlayerDashGhost.Spawn(animator.gameObject, material, style.lifetime,
            style.alpha, style.smear, style.hold, direction, flare);
        if (ghost != null)
        {
            effects.Add(ghost.gameObject);
        }
    }

    private void SpawnShock(GameObject prefab, Vector3 position, float height, List<GameObject> effects)
    {
        if (prefab != null)
        {
            effects.Add(UnityEngine.Object.Instantiate(prefab, position + Vector3.up * height, Quaternion.identity));
        }
    }
}
