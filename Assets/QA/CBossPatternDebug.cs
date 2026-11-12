using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CBossPatternDebug : MonoBehaviour
{
    [System.Serializable]
    public struct SPattern
    {
        public string patternName;
        public string triggerName;
    }

    [SerializeField] private Animator _bossAnimator;
    [SerializeField] private GameObject _window;
    [SerializeField] private Transform _buttonRoot;
    [SerializeField] private Button _buttonPrefab;
    [SerializeField] private SPattern[] _patterns;
    [SerializeField] private float _repeatInterval = 5f;
    [SerializeField] private Transform _player;

    private Coroutine _repeat;

    private void Awake()
    {
        _window.SetActive(false);
    }

    public void OpenWindow()
    {
        _window.SetActive(true);
        CreateButtons();
    }

    private void CreateButtons()
    {
        for (int i = _buttonRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(_buttonRoot.GetChild(i).gameObject);
        }

        for (int i = 0; i < _patterns.Length; i++)
        {
            int index = i;
            Button button = Instantiate(_buttonPrefab, _buttonRoot);
            button.gameObject.SetActive(true);
            button.GetComponentInChildren<TMP_Text>().text = _patterns[i].patternName;
            button.onClick.AddListener(() => PlayOnly(index));
        }
    }

    private void PlayOnly(int index)
    {
        _window.SetActive(false);

        if (_repeat != null)
        {
            StopCoroutine(_repeat);
        }

        _repeat = StartCoroutine(RepeatPattern(index));
    }

    private IEnumerator RepeatPattern(int index)
    {
        CPreviewPattern preview = _bossAnimator.GetComponentInChildren<CPreviewPattern>();

        while (true)
        {
            for (int i = 0; i < _patterns.Length; i++)
            {
                _bossAnimator.ResetTrigger(_patterns[i].triggerName);
            }

            string trigger = _patterns[index].triggerName;

            if (trigger == "bAttack02")
            {
                preview.ShowDonutPattern(20, 15f, 3f);
                yield return new WaitForSeconds(2.3f);
                preview.ClearPreview();
            }

            else if (trigger == "bAttack04")
            {
                preview.ShowDonutPattern(20, 18f, 10f);
                yield return new WaitForSeconds(2f);
                preview.ClearPreview();
            }

            else if (trigger == "bAttack03")
            {
                preview.ShowMultiArcPattern(6, 15f, 60f, 0f);
                yield return new WaitForSeconds(1f);
                preview.ClearPreview();
                preview.ShowMultiArcPattern(6, 15f, 60f, 30f);
                yield return new WaitForSeconds(1f);
                preview.ClearPreview();
            }

            else if (trigger == "bAttack01")
            {
                _bossAnimator.SetTrigger(trigger);

                Vector3 origin = preview.transform.position;
                preview.transform.position = _player.position;
                preview.ShowCirclePattern(20, 8f);
                yield return new WaitForSeconds(1.5f);
                preview.ClearPreview();
                preview.transform.position = origin;

                yield return new WaitForSeconds(_repeatInterval);
                continue;
            }

            _bossAnimator.SetTrigger(trigger);
            yield return new WaitForSeconds(_repeatInterval);
        }
    }
}