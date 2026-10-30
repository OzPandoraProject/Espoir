using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewConsumable", menuName = "Espoir/ConsumableSO")]
public class CItemConsumableSO : CItemSO
{
    [Tooltip("아이템 사용시 나타나는 효과")]
    [SerializeField] private List<CConsumableAction> _action;
    [Tooltip("아이템 사용시 적용되는 상태이상")]
    [SerializeField] private List<SStatusApply> _itemStatus;
    [Tooltip("던지는 아이템인지 유무 (던지는 아이템이면 위의 것들이 맞으면 적용되는거로 처리됨)")]
    [SerializeField] private bool _isThrow = false;

    public IReadOnlyList<CConsumableAction> Action => _action;
    public IReadOnlyList<SStatusApply> ItemStatus => _itemStatus;
    public bool IsThrow => _isThrow;
}