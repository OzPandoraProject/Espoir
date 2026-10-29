using System.Collections.Generic;
using UnityEngine;

public class CItemSO : ScriptableObject
{
    [Tooltip("아이템 id")]
    [SerializeField] protected int _id;
    [Tooltip("아이템 이름")]
    [SerializeField] protected string _name;
    [Tooltip("아이템 아이콘")]
    [SerializeField] protected Sprite _itemIcon;
    [Tooltip("아이템 설명")]
    [SerializeField] protected string _description;

    private static Dictionary<int, CItemSO> _idToItem = new Dictionary<int, CItemSO>();
    private static Dictionary<string, CItemSO> _stringToItem = new Dictionary<string, CItemSO>();
    private static bool _isBuilded = false;

    public static bool IsBuilded => _isBuilded;

    public int Id => _id;
    public string Name => _name;
    public Sprite ItemIcon => _itemIcon;
    public string Description => _description;

    public static void Build(bool force = false)
    {
        if (force || !_isBuilded) BuildInternal();
    }

    private static void BuildInternal()
    {
        _isBuilded = true;
        _idToItem.Clear();
        _stringToItem.Clear();
        CItemSO[] consumables = Resources.LoadAll<CItemSO>("ItemSO");
        if (consumables == null || consumables.Length == 0)
        {
            CLogger.Warn($"CItemSO : 소모품이 없음");
        }
        else
        {
            for (int i = 0; i < consumables.Length; i++)
            {
                if (consumables[i] == null)
                {
                    CLogger.Error($"CStatusSO : 딕셔너리 생성 중 에러 발생 - {i}");
                    continue;
                }
                _idToItem.Add(consumables[i]._id, consumables[i]);
                _stringToItem.Add(consumables[i]._name, consumables[i]);
            }
        }

        return;
    }

    public static CItemSO Find(int id)
    {
        return _idToItem[id];
    }

    public static CItemSO Find(string name)
    {
        return _stringToItem[name];
    }
}