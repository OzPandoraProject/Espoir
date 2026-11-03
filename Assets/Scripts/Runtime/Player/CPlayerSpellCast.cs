using System.Collections.Generic;
using UnityEngine;

internal class CPlayerSpellCast
{
    private readonly HashSet<CUnit> _RUSH_TARGETS = new HashSet<CUnit>();
    private Vector3 _origin;
    private Quaternion _rotation;
    private float _travel;
    private int _nextHit;
    private int _landedHits;
    private int _awardedFlow;
    private bool _reducedCooldown;

    internal void Begin(CPlayer player, Vector3 direction, CSkillSO skill, List<GameObject> effects)
    {
        _origin = player.transform.position;
        _rotation = Quaternion.LookRotation(direction);
        player.transform.rotation = _rotation;
        _travel = 0f;
        _nextHit = 0;
        _landedHits = 0;
        _awardedFlow = 0;
        _reducedCooldown = false;

        GameObject effect = skill.Steps[0].effectPrefab;
        if (effect != null)
        {
            effects.Add(Object.Instantiate(effect, _origin, _rotation));
        }

        if (skill.FollowEffect != null)
        {
            effects.Add(Object.Instantiate(skill.FollowEffect, _origin, _rotation, player.transform));
        }
    }

    internal bool UpdateRush(CPlayer player, CPlayerAttack attack, CPlayerMovement movement,
        CSkillSO skill, float time)
    {
        SSkillStep step = skill.Steps[0];
        if (time < step.moveStart || _travel >= 1f)
        {
            return false;
        }

        float travel = time >= step.moveEnd ? 1f : Mathf.Clamp01(skill.MovementCurve.Evaluate(time));
        Vector3 before = player.transform.position;
        float distance = Mathf.Max(0f, travel - _travel) * step.moveDistance;
        _travel = travel;

        if (distance > 0f)
        {
            movement.MoveOnGround(_rotation * Vector3.forward * distance);
        }

        Vector3 delta = player.transform.position - before;
        Vector3 local = Quaternion.Inverse(_rotation) * delta;
        SSkillHit hit = step.hits[0];
        hit.size += new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
        SPlayerHitResult result = attack.Attack(player, hit, before + delta * 0.5f, _rotation,
            EHitMark.Add, _RUSH_TARGETS);

        if (!result.hasHit)
        {
            return false;
        }

        int flow = player.Passive.FlowPerHit(result.hitFullyMarkedTarget);
        player.Passive.GainFlow(player, flow - _awardedFlow);
        _awardedFlow = Mathf.Max(flow, _awardedFlow);

        if (!result.hitFullyMarkedTarget || _reducedCooldown)
        {
            return false;
        }

        _reducedCooldown = true;
        
        return true;
    }

    internal void UpdateSlam(CPlayer player, CPlayerAttack attack, CSkillSO skill, float time)
    {
        SSkillHit[] hits = skill.Steps[0].hits;

        while (_nextHit < hits.Length && time >= hits[_nextHit].time)
        {
            EHitMark mark = _nextHit == 0 ? EHitMark.Fill : EHitMark.Refresh;
            SPlayerHitResult result = attack.Attack(player, hits[_nextHit], _origin, _rotation, mark);
            _nextHit++;

            if (!result.hasHit)
            {
                continue;
            }

            _landedHits++;
            int flow = player.Passive.FlowPerHit(result.hitFullyMarkedTarget);
           
            if (_landedHits == hits.Length)
            {
                flow = Mathf.Max(flow, skill.FullHitFlow - _awardedFlow);
            }

            player.Passive.GainFlow(player, flow);
            _awardedFlow += flow;
        }
    }

    internal void End()
    {
        _RUSH_TARGETS.Clear();
    }
}
