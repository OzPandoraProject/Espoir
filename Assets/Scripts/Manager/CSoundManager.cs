using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public enum EBackgroundSound
{
    None,
    OasisOfGold,
    BaseBGM,
    BattleBGM,
    BossBGM,
    DialogueBGM,
    LocalNpcBGM,
    BossSummonerBGM
    // 여기서부터 배경음 리스트 만들기
}

public enum EEffectSound
{
    None,
    PlayerHit,
    MonsterHit,
    PlayerSlash,
    MonsterSlash,
    QuestionAppear,
    QuestionChoose,
    Success,
    Beep,
    Appear,
    Disappear
}

[Serializable]
class CBackgroundSound
{
    public EBackgroundSound soundType;
    public AudioSource audioClip;
    public float volume = 1.0f;
    public bool loop = true;
}

[Serializable]
class CEffectSound
{
    public EEffectSound soundType;
    public AudioSource audioClip;
    public float volume = 1.0f;
    public bool loop = false;
}

public class CSoundManager : MonoBehaviour
{
    [SerializeField] private List<CBackgroundSound> _backgroundSounds;
    [SerializeField] private List<CEffectSound> _effectSounds;
    [SerializeField] private AudioMixer _audioMixer;

    Dictionary<EBackgroundSound, AudioSource> _bgmDict;
    Dictionary<EEffectSound, AudioSource> _seDict;
    private EBackgroundSound _lastBackGround = EBackgroundSound.None;
    private EBackgroundSound _currentBackGround = EBackgroundSound.None;

    private float _fadeFactor = 1.0f;

    private static CSoundManager _instance;
    private float _titleBgmLevel = 1f;
    private float _titleSeLevel = 1f;

    public static CSoundManager Instance
    {
        get { return _instance; }
        protected set { _instance = value; }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        _instance = this;
        _bgmDict = new Dictionary<EBackgroundSound, AudioSource>();
        _seDict = new Dictionary<EEffectSound, AudioSource>();
        StopBGM();
        ResetDictionary();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
    void Update()
    {
        if (_lastBackGround == EBackgroundSound.None && _currentBackGround != EBackgroundSound.None)
        {
            _fadeFactor = 1.0f;
            _lastBackGround = _currentBackGround;
            _bgmDict[_currentBackGround].Play();
        }
        else if (_lastBackGround != _currentBackGround)
        {
            if (_fadeFactor > 0.0f)
            {
                _fadeFactor -= Time.deltaTime * 4;
            }
            if (_fadeFactor <= 0.0f)
            {
                _fadeFactor = 0.0f;
                _lastBackGround = _currentBackGround;
                StopBGMInternal();
                if (_currentBackGround != EBackgroundSound.None) _bgmDict[_currentBackGround].Play();
            }
        }
        else
        {
            if (_fadeFactor < 1.0f)
            {
                _fadeFactor += Time.deltaTime * 4;
            }
            if (_fadeFactor > 1.0f)
            {
                _fadeFactor = 1.0f;
            }
        }
        SetVolume();
    }

    public void SetTitleVolume(float bgm, float se)
    {
        _titleBgmLevel = Mathf.Clamp01(bgm);
        _titleSeLevel = Mathf.Clamp01(se);
    }
    private void ResetDictionary()
    {
        foreach (var bgm in _backgroundSounds)
        {
            if (!_bgmDict.ContainsKey(bgm.soundType))
            {
                _bgmDict.Add(bgm.soundType, bgm.audioClip);
                _bgmDict[bgm.soundType].volume = bgm.volume;
                _bgmDict[bgm.soundType].loop = bgm.loop;
            }
            else
            {
                CLogger.Error($"중복된 사운드 있음 - {bgm.soundType}");
            }
        }
        foreach (var se in _effectSounds)
        {
            if (!_seDict.ContainsKey(se.soundType))
            {
                _seDict.Add(se.soundType, se.audioClip);
                _seDict[se.soundType].volume = se.volume;
                _seDict[se.soundType].loop = se.loop;
            }
            else
            {
                CLogger.Error($"중복된 사운드 있음 - {se.soundType}");
            }
        }
    }

    private void SetVolume()
    {
        float bgmVolume = (CGameManager.Instance == null) ? _titleBgmLevel * _fadeFactor : CGameManager.Instance.BgmLevel * _fadeFactor;
        float seVolume = (CGameManager.Instance == null) ? _titleSeLevel : CGameManager.Instance.SeLevel;

        if (bgmVolume <= 0.0001f)
        {
            _audioMixer.SetFloat("BGMLevel", -80f);
        }
        else
        {
            _audioMixer.SetFloat("BGMLevel", Mathf.Log10(bgmVolume) * 20);
        }

        if (seVolume <= 0.0001f)
        {
            _audioMixer.SetFloat("SELevel", -80f);
        }
        else
        {
            _audioMixer.SetFloat("SELevel", Mathf.Log10(seVolume) * 20);
        }
    }
    public void PlaySE(EEffectSound soundType)
    {
        if (_seDict.ContainsKey(soundType))
        {
            _seDict[soundType].Play();
        }
        else
        {
            CLogger.Error($"사운드 없음 - {soundType}");
        }
    }

    public void PlayBGM(EBackgroundSound soundType)
    {
        if (soundType == EBackgroundSound.None)
        {
            _currentBackGround = EBackgroundSound.None;
        }
        else if (_bgmDict.ContainsKey(soundType))
        {
            _currentBackGround = soundType;
        }
        else
        {
            CLogger.Error($"사운드 없음 - {soundType}");
        }
    }

    public void StopBGM()
    {
        _fadeFactor = 0.0f;
        _currentBackGround = EBackgroundSound.None;
        StopBGMInternal();
    }

    private void StopBGMInternal()
    {
        foreach (var audioSource in _bgmDict.Values)
        {
            audioSource.Stop();
        }
    }
}
