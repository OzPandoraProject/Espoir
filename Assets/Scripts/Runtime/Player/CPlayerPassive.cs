using System;
using UnityEngine;

[Serializable]
public class CPlayerPassive
{
    [SerializeField] private CStatusSO _flow;
    [SerializeField] private CStatusSO _mark;
    [SerializeField, Min(1)] private int _flowPerHit = 1;
    [SerializeField, Min(1)] private int _markedHitMultiplier = 2;
    [SerializeField, Min(1)] private int _flowLostOnDamage = 6;
    [SerializeField, Min(0f)] private float _damageInterval = 1.5f;

    private float _nextFlowLossTime;

    internal bool HasFullMark(CUnit target)
    {
        return _mark != null && _mark.MaxStack > 0 && target.FindStatusStack(_mark.Id) >= _mark.MaxStack;
    }

    internal void ApplyMark(CUnit target)
    {
        AddStacks(target, _mark, 1);
    }

    internal void ApplyFullMark(CUnit target)
    {
        if (_mark != null)
        {
            AddStacks(target, _mark, _mark.MaxStack);
        }
    }

    internal void RefreshMark(CUnit target)
    {
        if (_mark != null)
        {
            target.FindStatus(_mark.Id)?.ResetTimer();
        }
    }

    internal int FlowPerHit(bool fullyMarked)
    {
        return fullyMarked ? _flowPerHit * _markedHitMultiplier : _flowPerHit;
    }

    internal void GainFlow(CPlayer player, int amount)
    {
        AddStacks(player, _flow, amount);
    }

    internal void OnPlayerDamaged(CPlayer player)
    {
        if (_flow == null || Time.time < _nextFlowLossTime)
        {
            return;
        }

        CStatus flow = player.FindStatus(_flow.Id);

        if (flow == null)
        {
            return;
        }

        _nextFlowLossTime = Time.time + _damageInterval;

        if (flow.Stack <= _flowLostOnDamage)
        {
            player.RemoveStatus(flow);
            return;
        }

        flow.Stack -= _flowLostOnDamage;
        player.ResyncStatus();
    }

    private static void AddStacks(CUnit target, CStatusSO status, int amount)
    {
        if (status == null || amount <= 0)
        {
            return;
        }

        CStatus current = target.FindStatus(status.Id);
        if (current != null && current.StackSwitch == EStatusStack.StackAndRefresh && current.Stack == current.MaxStack)
        {
            current.ResetTimer();
            return;
        }

        target.AddStatus(status.Id, stack: amount);
    }
}
