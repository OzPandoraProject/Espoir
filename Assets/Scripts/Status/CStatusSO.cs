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
    [Tooltip("상태이상 최대 스택 수")]
    [SerializeField] private int _maxStack;
    [Tooltip("스택 제거 방식")]
    [SerializeField] private EStatusRemove _remove;
    [Tooltip("최초 스택 감소수")]
    [SerializeField] private int _initialRemoveStack;
    [Tooltip("스택 감소수")]
    [SerializeField] private int _removeStack;
    [Tooltip("스택 제거 타이머")]
    [SerializeField] private float _stackDuration;
    [Tooltip("상태이상 효과")]
    [SerializeField] private List<SStatusAction> _actionList;
    [Tooltip("커스텀 상태이상 (0이면 없음)")]
    [SerializeField] private int _customStatus = 0;

    #region Member Property
    public int Id => _id;
    public string Name => _name;
    public string Description => _description;
    public string Tooltip => _tooltip;
    public Sprite Icon => _icon;
    public float Duration => _duration;
    public EStatusStack Stack => _stack;
    public int MaxStack => _maxStack;
    public int InitialRemoveStack => _initialRemoveStack;
    public int RemoveStack => _removeStack;
    public EStatusRemove Remove => _remove;
    public float StackDuration => _stackDuration;
    public IReadOnlyList<SStatusAction> ActionList => _actionList;
    public int CustomStatus => _customStatus;
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
        if (_idToStatus.ContainsKey(id)) return _idToStatus[id];
        else
        {
            CLogger.Error($"CStatusSO : 상태이상 ID를 찾을 수 없음 - {id}");
            return null;
        }
    }

    public static CStatusSO Find(string name)
    {
        if (_stringToStatus.ContainsKey(name)) return _stringToStatus[name];
        else
        {
            CLogger.Error($"CStatusSO : 상태이상 이름을 찾을 수 없음 - {name}");
            return null;
        }
    }
}
