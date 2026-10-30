using System;

public class CItemEtc: CItem
{
    public override bool AvailableInLobby() => false;
    public override bool AvailableInBattle() => false;

    public override int Quantity
    {
        get { return _quantity; }
        set { _quantity = value; }
    }

    public CItemEtc(CItemEtcSO targetItem)
    {
        Setup(targetItem);
    }

    public void OnUse(CPlayer player)
    {
        return;
    }

    public override bool OnDiscard(CPlayer player, int quantity)
    {
        _quantity -= quantity;
        if (_quantity <= 0) return true;
        else return false;
    }

    public override bool Setup(CItemSO targetItem)
    {
        if(targetItem is CItemEtcSO targetEtc)
        {
            this._id = targetEtc.Id;
            this._name = targetEtc.Name;
            this._itemIcon = targetEtc.ItemIcon;
            this._description = targetEtc.Description;
            return true;
        }
        else
        {
            CLogger.Error("CItemEtc : 재료가 아닌 아이템이 들어옴!");
            return false;
        }
    }


}
