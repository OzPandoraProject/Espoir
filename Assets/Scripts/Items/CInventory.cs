/*  지금 생각하는 구조
 *    - CInventory는 그냥 스크립트로 CItem을 담고, 정상적인 상태인지만 확인
 *    - CInventoryUI로 인벤토리 띄웠을 때 보여지는거 관리
 *    
 *    - CItem은 아이템 정보랑 갯수 담기 (CItemSO의 데이터 복사)
 *    - CItemShown은 인벤토리를 띄웠을 때 보여주는 용도로 사용하기
 */

using System.Collections.Generic;

public class CInventory
{
    private List<CItem> _items = new List<CItem>();
    private static readonly int _maxItemSlot = 64;

    public CInventory()
    {
        
    }

    public void AddItem(int id)
    {
        _items.Add(CItem.MakeItem(id));
    }
}