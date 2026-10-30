using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewSet", menuName = "Espoir/SetSO")]
public class CSetSO : ScriptableObject
{
    [Tooltip("세트 id")]
    [SerializeField] protected int _id;
    [Tooltip("세트 이름")]
    [SerializeField] protected string _name;
    [Tooltip("세트 설명")]
    [SerializeField][TextArea(1, 5)] protected string _description;

    [Tooltip("세트 2개 상태의 능력치 변화")]
    [SerializeField] private List<SEquipmentAction> _actionSet2;
    [Tooltip("세트 2개 상태의 추가 상태")]
    [SerializeField] private List<SStatusApply> _equipmentStatusSet2;

    [Tooltip("세트 3개 상태의 능력치 변화")]
    [SerializeField] private List<SEquipmentAction> _actionSet3;
    [Tooltip("세트 3개 상태의 추가 상태")]
    [SerializeField] private List<SStatusApply> _equipmentStatusSet3;

    [Tooltip("세트 4개 상태의 능력치 변화")]
    [SerializeField] private List<SEquipmentAction> _actionSet4;
    [Tooltip("세트 4개 상태의 추가 상태")]
    [SerializeField] private List<SStatusApply> _equipmentStatusSet4;



    private static Dictionary<int, CSetSO> _idToSet = new Dictionary<int, CSetSO>();
    private static Dictionary<string, CSetSO> _stringToSet = new Dictionary<string, CSetSO>();
    private static bool _isBuilded = false;

    public static bool IsBuilded => _isBuilded;

    public int Id => _id;
    public string Name => _name;
    public string Description => _description;

    public IReadOnlyList<SEquipmentAction> ActionSet2 => _actionSet2;
    public IReadOnlyList<SStatusApply> EquipmentStatusSet2 => _equipmentStatusSet2;

    public IReadOnlyList<SEquipmentAction> ActionSet3 => _actionSet3;
    public IReadOnlyList<SStatusApply> EquipmentStatusSet3 => _equipmentStatusSet3;

    public IReadOnlyList<SEquipmentAction> ActionSet4 => _actionSet4;
    public IReadOnlyList<SStatusApply> EquipmentStatusSet4 => _equipmentStatusSet4;

    public static void Build(bool force = false)
    {
        if (force || !_isBuilded) BuildInternal();
    }

    private static void BuildInternal()
    {
        _isBuilded = true;
        _idToSet.Clear();
        _stringToSet.Clear();
        CSetSO[] sets = Resources.LoadAll<CSetSO>("SetSO");
        if (sets == null || sets.Length == 0)
        {
            CLogger.Warn($"CSetSO : 세트효과가 없음");
        }
        else
        {
            for (int i = 0; i < sets.Length; i++)
            {
                if (sets[i] == null)
                {
                    CLogger.Error($"CStatusSO : 딕셔너리 생성 중 에러 발생 - {i}");
                    continue;
                }
                _idToSet.Add(sets[i]._id, sets[i]);
                _stringToSet.Add(sets[i]._name, sets[i]);
            }
        }
        return;
    }
    public static CSetSO Find(int id)
    {
        return _idToSet[id];
    }

    public static CSetSO Find(string name)
    {
        return _stringToSet[name];
    }

}