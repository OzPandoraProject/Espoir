using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CEquipmentDebug : MonoBehaviour
{
    [SerializeField] private CPlayer _player;
    [SerializeField] private GameObject _window;
    [SerializeField] private Transform _buttonRoot;
    [SerializeField] private Button _buttonPrefab;
    [SerializeField] private CItemEquipmentSO[] _equipments;
    [SerializeField] private CItemConsumableSO[] _potions;

    private void Awake()
    {
        _window.SetActive(false);
    }

    public void OpenWindow()
    {
        _window.SetActive(true);
        CreateButtons();
    }

    public void CloseWindow()
    {
        _window.SetActive(false);
    }

    private void CreateButtons()
    {
        for (int i = _buttonRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(_buttonRoot.GetChild(i).gameObject);
        }

        for (int i = 0; i < _equipments.Length; i++)
        {
            CItemEquipmentSO equipment = _equipments[i];

            if (equipment == null)
            {
                continue;
            }

            Button button = Instantiate(_buttonPrefab, _buttonRoot);
            button.gameObject.SetActive(true);
            button.GetComponentInChildren<TMP_Text>().text = equipment.Name;
            button.onClick.AddListener(() => GiveEquipment(equipment));
        }

        for (int i = 0; i < _potions.Length; i++)
        {
            CItemConsumableSO potion = _potions[i];

            if (potion == null)
            {
                continue;
            }

            Button button = Instantiate(_buttonPrefab, _buttonRoot);
            button.gameObject.SetActive(true);
            button.GetComponentInChildren<TMP_Text>().text = potion.Name;
            button.onClick.AddListener(() => GiveItem(potion));
        }
    }

    private void GiveEquipment(CItemEquipmentSO equipment)
    {
        if (_player == null)
        {
            _player = FindObjectOfType<CPlayer>();
        }

        if (_player == null || _player.Inventory == null)
        {
            return;
        }

        int added = _player.Inventory.AddItem(equipment.Id);

        if (added > 0)
        {
            CLogger.Log($"[QA] {equipment.Name} 인벤토리에 추가");
        }
    }

    private void GiveItem(CItemSO item)
    {
        if (_player == null)
        {
            _player = FindObjectOfType<CPlayer>();
        }

        if (_player == null || _player.Inventory == null || item == null)
        {
            return;
        }

        int added = _player.Inventory.AddItem(item.Id);

        if (added > 0)
        {
            CLogger.Log($"[QA] {item.Name} 인벤토리에 추가");
        }
    }

}
