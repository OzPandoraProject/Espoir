using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewStatus", menuName = "Espoir/StatusSO")]
public class CStatusSO : ScriptableObject
{
    [Tooltip("상태이상 ID")]
    [SerializeField] private int _id;
    [Tooltip("상태이상 이름")]
    [SerializeField] private string _name;
    [Tooltip("상태이상 아이콘")]
    [SerializeField] private Sprite _icon;

    [Tooltip("상태이상 설명")]
    [SerializeField][TextArea(1, 5)] private string _description;
    [Tooltip("상태이상 툴팁")]
    [SerializeField][TextArea(1, 5)] private string _tooltip;

    [Tooltip("상태이상 기본 지속시간")]
    [SerializeField] private float _duration;
    [Tooltip("상태이상 갱신 스타일")]
    [SerializeField] private EStatusStack _stack;
    [Tooltip("스택 제거 방식")]
    [SerializeField] private EStatusRemove _remove;
    [Tooltip("스택 제거 타이머")]
    [SerializeField] private float _stackDuration;
    [Tooltip("상태이상 효과")]
    [SerializeField] private List<SStatusAction> _actionList;

    #region Member Property
    public int Id => _id;
    public string Name => _name;
    public string Description => _description;
    public string Tooltip => _tooltip;
    public float Duration => _duration;
    public EStatusStack Stack => _stack;
    public EStatusRemove Remove => _remove;
    public float StackDuration => _stackDuration;
    public IReadOnlyList<SStatusAction> ActionList => _actionList;
    #endregion




    private static Dictionary<int, CStatusSO> _idToStatus = new Dictionary<int, CStatusSO>();
    private static Dictionary<string, CStatusSO> _stringToStatus = new Dictionary<string, CStatusSO>();
    private static bool _isBuilded = false;
    public static bool IsBuilded
    {
        get { return _isBuilded; }
    }

    public static void Build(bool force)
    {
        if (force || !_isBuilded) BuildInternal();
    }

    private static void BuildInternal()
    {
        _isBuilded = true;
        _idToStatus.Clear();
        _stringToStatus.Clear();
        CStatusSO[] statuses = Resources.LoadAll<CStatusSO>("StatusSO");
        if (statuses == null || statuses.Length == 0)
        {
            CLogger.Warn($"CStatusSO : 상태이상이 없음");
            return;
        }
        for (int i = 0; i < statuses.Length; i++)
        {
            if (statuses[i] == null)
            {
                CLogger.Error($"CStatusSO : 딕셔너리 생성 중 에러 발생 - {i}");
                continue;
            }
            _idToStatus.Add(statuses[i]._id, statuses[i]);
            _stringToStatus.Add(statuses[i]._name, statuses[i]);
        }

        return;
    }

    public static CStatusSO Find(int id)
    {
        return _idToStatus[id];
    }

    public static CStatusSO Find(string name)
    {
        return _stringToStatus[name];
    }
}
