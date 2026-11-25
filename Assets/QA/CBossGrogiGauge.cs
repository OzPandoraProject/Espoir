using UnityEngine;
using UnityEngine.UI;

public class CBossGrogiGauge : MonoBehaviour
{
    [SerializeField] CSlothBoss _boss;
    [SerializeField] GameObject _gaugeRoot;
    [SerializeField] Image _fill;
    [SerializeField] float _needRatio = 0.035f;
    [SerializeField] float _slowSpeed = 0.2f;

    float _startHp;
    float _lastHp;
    bool _showing;

    private void Awake()
    {
        if (_boss == null)
        {
            if (_boss == null)
            {
                _boss = GetComponentInParent<CSlothBoss>();
            }
            Hide();
        }
    }

    private void Update()
    {
        if (_boss == null || _boss.HpCurrent <= 0f)
        {
            Hide();
            return;
        }

        Animator animator = _boss.GetComponent<Animator>();
        bool slowing = animator != null && Mathf.Abs(animator.speed - _slowSpeed) < 0.02f;

        if (!slowing)
        {
            _lastHp = _boss.HpCurrent;
            Hide();
            return;
        }

        if (!_showing)
        {
            _startHp = _lastHp > 0f ? _lastHp : _boss.HpCurrent;
            _showing = true;
            if (_gaugeRoot != null)
            {
                _gaugeRoot.SetActive(true);
            }
        }

        float need = _startHp * _needRatio;
        float dealt = Mathf.Max(0f, _startHp - _boss.HpCurrent);
        float ratio = need <= 0f ? 1f : Mathf.Clamp01(dealt / need);
        _fill.fillAmount = 1f - ratio;
    }

    private void Hide()
    {
        _showing = false;
        if (_fill != null)
        {
            _fill.fillAmount = 0f;
        }

        if (_gaugeRoot != null)
        {
            _gaugeRoot.SetActive(false);
        }
    }

}
