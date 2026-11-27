using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class CPlayerUI_Build : MonoBehaviour
{
    #region 인스팩터
    [SerializeField] private CPlayer _player;
    
    [SerializeField] private Image _hpFill = null;
    [SerializeField] private Image _mpFill = null;

    [SerializeField] private Image _hpFillEffect = null;
    [SerializeField] private float _hpEffectDelayField = 0.2f;

    [SerializeField] private TMP_Text _hpText = null;
    [SerializeField] private TMP_Text _mpText = null;

    [SerializeField] private TMP_Text _damageText = null;

    [SerializeField] private RectTransform _statusRoot = null;
    [SerializeField] private CStatusUI _statusPrefab = null;
    [SerializeField] private CTooltipUI _tooltip = null;

    [SerializeField] private TMP_Text _debugText = null;

    //[SerializeField] private CSkillUI _skillP1 = null;
    //[SerializeField] private CSkillUI _skillP2 = null;
    [SerializeField] private CSkillUI _skillQ = null;
    [SerializeField] private CSkillUI _skillW = null;
    [SerializeField] private CSkillUI _skillE = null;
    [SerializeField] private CSkillUI _skillR = null;
    [SerializeField] private CSkillUI _skillD = null;
    [SerializeField] private CSkillUI _skillPradona = null;
    [SerializeField] private Image _crownRemaining = null;
    [SerializeField] private CanvasGroup _skillDCanvas = null;

    [SerializeField] private List<CQuickslotUI> _quickslot = null;

    [SerializeField] private TMP_Text _jealousStack = null;
    [SerializeField] private List<Image> _crownStack = new List<Image>();
    [SerializeField] private List<Image> _pradonaStack = new List<Image>();
    #endregion

    #region 내부변수
    private readonly Dictionary<CStatus, CStatusUI> _statusViews = new();
    private readonly HashSet<CStatus> _currentStatus = new();
    private readonly List<CStatus> _removedStatus = new();
    private int _debuffViewCount;

    private bool _hpEffectInitialized = false;
    private float _hpEffectDelay = 0f;
    private bool _quickslotDirty;
    #endregion


    private void Start()
    {
        if(_player == null)
        {
            CLogger.Warn("CPlayerUI의 인스팩터에 플레이어 연결이 안되어 있음");
            return;
        }
        InvokePradonaChanged(_player.ActivePradonaPower);
    }

    private void OnEnable()
    {
        _player.Inventory.OnInventoryChanged += MarkQuickslotDirty;
        _quickslotDirty = true;
    }

    private void OnDisable()
    {
        _player.Inventory.OnInventoryChanged -= MarkQuickslotDirty;
    }

    private void LateUpdate()
    {
        UpdateHp();
        UpdateMp();
        if (_debugText != null) UpdateDebug();
        UpdateSkill();
        UpdateQuickslot();
        UpdateStatus();
    }

    private void MarkQuickslotDirty() => _quickslotDirty = true;

    public void UpdateHp()
    {
        if(_hpFill == null)
        {
            return;
        }

        float ratio = _player.HpMax > 0 ? _player.HpCurrent / _player.HpMax : 0f;
        float previousRatio = _hpFill.fillAmount;
        _hpFill.fillAmount = ratio;

        if (_hpText != null)
        {
            _hpText.text = 
                $"<size=75%>HP</size> {((_player.HpCurrent <= 0f) ? 0 : _player.HpCurrent):F0} <size=65%>/ {_player.HpMax:F0}</size>";
        }

        if (!_hpEffectInitialized)
        {
            _hpEffectInitialized = true;
            _hpFillEffect.fillAmount = ratio;
            _hpEffectDelay = 0f;
            return;
        }

        if (ratio > previousRatio)
        {
            _hpFillEffect.fillAmount = Mathf.Max(_hpFillEffect.fillAmount, ratio);
        }

        if (ratio < previousRatio)
        {
            _hpEffectDelay = _hpEffectDelayField;
            return;
        }
        else if (_hpEffectDelay > 0)
        {
            _hpEffectDelay -= Time.deltaTime;
            return;
        }

        if ((_hpFillEffect.fillAmount - _hpFill.fillAmount) < 0.001)
        {
            _hpFillEffect.fillAmount = _hpFill.fillAmount;
            return;
        }
        else
        {
            _hpFillEffect.fillAmount = Mathf.Lerp(_hpFillEffect.fillAmount, ratio, 8f * Time.deltaTime);
        }
    }


    public void UpdateMp()
    {
        if (_mpFill == null)
        {
            return;
        }

        float ratio = _player.MaxMp > 0 ? _player.CurrentMp / _player.MaxMp : 0f;

        if (_mpText != null)
        {
            _mpText.text =
                $"<size=75%>MP</size> {((_player.CurrentMp <= 0f) ? 0 : _player.CurrentMp):F0} <size=65%>/ {_player.MaxMp:F0}</size>";
        }

        _mpFill.fillAmount = ratio;
    }

    public void UpdateDebug()
    {
        _debugText.text = $"ATK {_player.Attack:F1}\nDEF {_player.Defence:F1}\nCRI {_player.CriticalRate*100:F1}%\n {_player.CriticalDamage*100:F1}%\nA.spd {_player.AttackSpeedMultiplier:F2}\nS.spd {_player.MovementSpeedMultiplier:F2}";
    }

    public void UpdateStatus()
    {
        if (_statusRoot == null || _statusPrefab == null)
        {
            return;
        }

        _currentStatus.Clear();
        _removedStatus.Clear();


        if (_player.EquipmentStatus != null)
        {
            for (int i = 0; i < _player.EquipmentStatus.Count; i++)
            {
                CStatus target = _player.EquipmentStatus[i];
                if (target == null) continue;
                _currentStatus.Add(target);
                if (_statusViews.ContainsKey(target)) continue;
                CStatusUI view = Instantiate(_statusPrefab, _statusRoot);
                view.Bind(target, _tooltip);
                if (target.IsDebuff)
                {
                    view.transform.SetSiblingIndex(_debuffViewCount);
                    _debuffViewCount++;
                }

                _statusViews.Add(target, view);
            }
        }

        if (_player.Status != null)
        {
            for (int i = 0; i < _player.Status.Count; i++)
            {
                CStatus target = _player.Status[i];
                if (target == null) continue;
                _currentStatus.Add(target);
                if (_statusViews.ContainsKey(target)) continue;
                CStatusUI view = Instantiate(_statusPrefab, _statusRoot);
                view.Bind(target, _tooltip);
                if (target.IsDebuff)
                {
                    view.transform.SetSiblingIndex(_debuffViewCount);
                    _debuffViewCount++;
                }

                _statusViews.Add(target, view);
            }
        }

        foreach (KeyValuePair<CStatus, CStatusUI> pair in _statusViews)
        {
            if (_currentStatus.Contains(pair.Key))
            {
                pair.Value.Refresh();
            }
            else
            {
                _removedStatus.Add(pair.Key);
            }
        }

        if (_removedStatus.Count > 0)
        {
            for (int i = 0; i < _removedStatus.Count; i++)
            {
                CStatus targetStatus = _removedStatus[i];
                CStatusUI targetUi = _statusViews[_removedStatus[i]];
                _statusViews.Remove(_removedStatus[i]);

                if (targetStatus.IsDebuff) _debuffViewCount--;

                targetUi.gameObject.SetActive(false);
                targetUi.transform.SetAsLastSibling();
                Destroy(targetUi.gameObject);
            }
        }
    }

    public void UpdateSkill()
    {
        float ratio;
        float remainCooldown;

        int jealousStack = _player.FindStatusStack(10);
        int crownStack = _player.FindStatusStack(25);
        _jealousStack.text = jealousStack == 0 ? "" : jealousStack.ToString();
        for (int i = 0; i < _crownStack.Count; i++)
        {
            _crownStack[i].enabled = i < crownStack;
        }
        _crownRemaining.fillAmount = _player.CrownWindowRemaining / 2;
        _player.GetSkillCooldown(ESkillType.Strike, out ratio, out remainCooldown);
        _skillQ.Refresh(ratio, remainCooldown, _player.GetSkillAvailable(ESkillType.Strike));
        _player.GetSkillCooldown(ESkillType.Crown, out ratio, out remainCooldown);
        _skillW.Refresh(ratio, remainCooldown, _player.GetSkillAvailable(ESkillType.Crown));
        _player.GetSkillCooldown(ESkillType.Rush, out ratio, out remainCooldown);
        _skillE.Refresh(ratio, remainCooldown, _player.GetSkillAvailable(ESkillType.Rush));
        _player.GetSkillCooldown(ESkillType.Slam, out ratio, out remainCooldown);
        _skillR.Refresh(ratio, remainCooldown, _player.GetSkillAvailable(ESkillType.Slam));
        _player.GetSkillCooldown(ESkillType.Dash, out ratio, out remainCooldown);
        _skillD.Refresh(ratio, remainCooldown, _player.GetSkillAvailable(ESkillType.Dash));

        if (_skillDCanvas != null)
        {
            bool isCooldown = remainCooldown > 0f;
            _skillDCanvas.alpha = isCooldown ? 1f : 0f;
            _skillDCanvas.interactable = isCooldown;
            _skillDCanvas.blocksRaycasts = isCooldown;
        }

        _player.GetPradonaCooldown(out ratio, out remainCooldown);
        bool canUse = _player.ActivePradonaPower != EPradonaPower.None && remainCooldown <= 0f;
        _skillPradona.Refresh(ratio, remainCooldown, canUse);
    }

    public void UpdateQuickslot()
    {
        for (int i = 0; i < 4; i++)
        {
            int id = _player.Inventory.GetQuickSlotId(i);

            if (_quickslotDirty)
            {
                int quantity = (id == -1) ? 0 : _player.Inventory.GetTotalQuantity(id);
                _quickslot[i].RefreshIteminfo(id, quantity);
            }

            _player.Inventory.GetItemCooldown(id, out float ratio, out float remainCooldown);
            _quickslot[i].RefreshCooldown(ratio, remainCooldown);
        }

        _quickslotDirty = false;
    }

    public void InvokePradonaChanged(EPradonaPower pradonaPower)
    {
        for (int i = 0; i < _pradonaStack.Count; i++)
        {
            _pradonaStack[i].enabled = false;
        }
        int index = 0;
        string temp = "";
        if (pradonaPower.HasFlag(EPradonaPower.Invidia))
        {
            if (index < _pradonaStack.Count)
            {
                if (ColorUtility.TryParseHtmlString("#B61834", out Color color))
                {
                    _pradonaStack[index].color = color;
                }
                _pradonaStack[index].enabled = true;
                index++;
            }
            temp += "질투 : 적중 시 체력 회복\n";
        }
        if (pradonaPower.HasFlag(EPradonaPower.Avaritia))
        {
            if (index < _pradonaStack.Count)
            {
                if (ColorUtility.TryParseHtmlString("#FFD65A", out Color color))
                {
                    _pradonaStack[index].color = color;
                }
                _pradonaStack[index].enabled = true;
                index++;
            }
            temp += "탐욕 : 적중 시 적이 받는 피해 증가\n";
        }
        if (pradonaPower.HasFlag(EPradonaPower.Pigritia))
        {
            if (index < _pradonaStack.Count)
            {
                if (ColorUtility.TryParseHtmlString("#BBA1DA", out Color color))
                {
                    _pradonaStack[index].color = color;
                }
                _pradonaStack[index].enabled = true;
                index++;
            }
            temp += "나태 : 다음 받는 공격 피해 99% 감소\n";
        }
        if (pradonaPower.HasFlag(EPradonaPower.Gula))
        {
            if (index < _pradonaStack.Count)
            {
                if (ColorUtility.TryParseHtmlString("#558701", out Color color))
                {
                    _pradonaStack[index].color = color;
                }
                _pradonaStack[index].enabled = true;
                index++;
            }
            temp += "폭식 : 일반 기술 재사용 대기시간 초기화\n";
        }
        if (pradonaPower.HasFlag(EPradonaPower.Luxuria))
        {
            if (index < _pradonaStack.Count)
            {
                if (ColorUtility.TryParseHtmlString("#FF82B1", out Color color))
                {
                    _pradonaStack[index].color = color;
                }
                _pradonaStack[index].enabled = true;
                index++;
            }
            temp += "색욕 : 기절 효과 해제\n";
        }
        if (pradonaPower.HasFlag(EPradonaPower.Ira))
        {
            if (index < _pradonaStack.Count)
            {
                if (ColorUtility.TryParseHtmlString("#F95502", out Color color))
                {
                    _pradonaStack[index].color = color;
                }
                _pradonaStack[index].enabled = true;
                index++;
            }
            temp += "분노 : 치명타 확률 크게 증가\n";
        }

        temp = temp.TrimEnd('\n');

        if (string.IsNullOrWhiteSpace(temp)) temp = "선택한 권능이 없어 지금은 사용할 수 없습니다";
        string cooldown = _player.PradonaCooldownDuration.ToString("0.##", CultureInfo.InvariantCulture);
        _skillPradona.TooltipText = $"재사용 대기시간 {cooldown}초\n선택한 권능을 발동합니다\n기절 상태에서도 사용할 수 있습니다\n" + temp;
    }
}
