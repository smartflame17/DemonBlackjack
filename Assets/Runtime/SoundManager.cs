using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// BGM
public enum EBgm
{
    TITLE,
    GAME,
    NONE,
}

// SFX
public enum ESfx
{
    CARD_PLAY,
    CARD_DRAW,
    MONEY_CHANGE_LOW,
    MONEY_CHANGE_MID,
    MONEY_CHANGE_HIGH,
    SHOP_PURCHASE,
    UI_BUTTON_CLICK,
    NONE,
}

[System.Serializable]
public struct BGMSoundData
{
    public EBgm bgmType;
    public AudioClip bgmClip;
    public string description;
}

[System.Serializable]
public struct SFXSoundData
{
    public ESfx sfxType;
    public AudioClip sfxClip;
    public string description;
}


public class SoundManager : MonoBehaviour
{
    private const string MasterVolumeKey = "DemonBlackjack.Audio.MasterVolume";
    private const string BGMVolumeKey = "DemonBlackjack.Audio.BGMVolume";
    private const string SFXVolumeKey = "DemonBlackjack.Audio.SFXVolume";
    private const string MasterMutedKey = "DemonBlackjack.Audio.MasterMuted";
    private const string BGMMutedKey = "DemonBlackjack.Audio.BGMMuted";
    private const string SFXMutedKey = "DemonBlackjack.Audio.SFXMuted";

    public static SoundManager Instance { get; private set; }

    [Header("Audio Clips")]
    [SerializeField] private BGMSoundData[] bgmClips;
    [SerializeField] private SFXSoundData[] sfxClips;

    [Header("Pool Settings")]
    [SerializeField] private int poolSize = 10;

    private Dictionary<EBgm, AudioClip> bgmDict;
    private Dictionary<ESfx, AudioClip> sfxDict;
    private Queue<AudioSource> audioSourcePool;
    private readonly List<AudioSource> sfxPlayers = new List<AudioSource>();

    private AudioSource bgmPlayer;
    private Coroutine bgmFadeCoroutine;
    private float bgmFadeMultiplier = 1f;
    private float masterVolume = 1f;
    private float bgmVolume = 1f;
    private float sfxVolume = 1f;
    private bool isMasterMuted;
    private bool isBGMMuted;
    private bool isSFXMuted;

    public EBgm CurrentBGM { get; private set; } = EBgm.NONE;
    public ESfx CurrentSFX { get; private set; } = ESfx.NONE;
    public float MasterVolume => masterVolume;
    public float BGMVolume => bgmVolume;
    public float SFXVolume => sfxVolume;
    public bool IsMasterMuted => isMasterMuted;
    public bool IsBGMMuted => isBGMMuted;
    public bool IsSFXMuted => isSFXMuted;

    public event Action VolumeSettingsChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Init();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Init()
    {
        bgmDict = new Dictionary<EBgm, AudioClip>();
        for (int i = 0; bgmClips != null && i < bgmClips.Length; i++)
        {
            bgmDict[bgmClips[i].bgmType] = bgmClips[i].bgmClip;
        }

        sfxDict = new Dictionary<ESfx, AudioClip>();
        for (int i = 0; sfxClips != null && i < sfxClips.Length; i++)
        {
            sfxDict[sfxClips[i].sfxType] = sfxClips[i].sfxClip;
        }

        LoadVolumeSettings();

        bgmPlayer = gameObject.AddComponent<AudioSource>();
        bgmPlayer.loop = true;
        bgmPlayer.playOnAwake = false;
        ApplyBGMVolume();

        InitPool();
    }

    private void InitPool()
    {
        audioSourcePool = new Queue<AudioSource>();
        for (int i = 0; i < poolSize; i++)
        {
            AudioSource source = CreateSFXPlayer();
            source.enabled = false;
            audioSourcePool.Enqueue(source);
        }
    }

    private AudioSource CreateSFXPlayer()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        sfxPlayers.Add(source);
        ApplySFXVolume(source);
        return source;
    }

    public void PlaySFX(ESfx sfxType, bool randomPitch = true, float minPitch = 0.8f, float maxPitch = 1.2f)
    {
        if (sfxDict.TryGetValue(sfxType, out var clip))
        {
            AudioSource source = audioSourcePool.Count > 0
                ? audioSourcePool.Dequeue()
                : CreateSFXPlayer();

            source.clip = clip;
            source.enabled = true;
            if (randomPitch)
            {
                source.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
            }
            else
            {
                source.pitch = 1f;
            }

            ApplySFXVolume(source);
            source.Play();
            CurrentSFX = sfxType;

            float playbackDuration = clip.length / Mathf.Max(Mathf.Abs(source.pitch), 0.01f);
            StartCoroutine(ReturnToPool(source, playbackDuration));
        }
        else Debug.LogWarning("SFX not found: " + sfxType.ToString());
    }

    public void PlayBGM(EBgm bgmType)
    {
        if (bgmDict.TryGetValue(bgmType, out var clip))
        {
            if (bgmPlayer.clip != clip || !bgmPlayer.isPlaying)
            {
                CancelBGMFade();
                bgmFadeMultiplier = 1f;
                bgmPlayer.clip = clip;
                ApplyBGMVolume();
                bgmPlayer.Play();
                CurrentBGM = bgmType;
            }
        }
        else Debug.LogWarning("BGM not found: " + bgmType.ToString());
        
    }

    public void SetBGMVolume(float volume)
    {
        float clampedVolume = Mathf.Clamp01(volume);
        if (Mathf.Approximately(bgmVolume, clampedVolume))
            return;

        bgmVolume = clampedVolume;
        PlayerPrefs.SetFloat(BGMVolumeKey, bgmVolume);
        ApplyBGMVolume();
        VolumeSettingsChanged?.Invoke();
    }

    public void SetMasterVolume(float volume)
    {
        float clampedVolume = Mathf.Clamp01(volume);
        if (Mathf.Approximately(masterVolume, clampedVolume))
            return;

        masterVolume = clampedVolume;
        PlayerPrefs.SetFloat(MasterVolumeKey, masterVolume);
        ApplyAllVolumes();
        VolumeSettingsChanged?.Invoke();
    }

    public void SetMasterMuted(bool muted)
    {
        if (isMasterMuted == muted)
            return;

        isMasterMuted = muted;
        PlayerPrefs.SetInt(MasterMutedKey, muted ? 1 : 0);
        ApplyAllVolumes();
        VolumeSettingsChanged?.Invoke();
    }

    public void SetBGMMuted(bool muted)
    {
        if (isBGMMuted == muted)
            return;

        isBGMMuted = muted;
        PlayerPrefs.SetInt(BGMMutedKey, muted ? 1 : 0);
        ApplyBGMVolume();
        VolumeSettingsChanged?.Invoke();
    }

    public void SetSFXMuted(bool muted)
    {
        if (isSFXMuted == muted)
            return;

        isSFXMuted = muted;
        PlayerPrefs.SetInt(SFXMutedKey, muted ? 1 : 0);
        ApplyAllSFXVolumes();
        VolumeSettingsChanged?.Invoke();
    }

    public void StopBGM()
    {
        CancelBGMFade();
        bgmPlayer.Stop();
        bgmFadeMultiplier = 1f;
        ApplyBGMVolume();
        CurrentBGM = EBgm.NONE; // Reset to default BGM type
    }

    public void FadeOutBGM(float duration = 1.0f)
    {
        CancelBGMFade();
        bgmFadeMultiplier = 1f;
        ApplyBGMVolume();

        if (duration <= 0f)
        {
            StopBGM();
            return;
        }

        bgmFadeCoroutine = StartCoroutine(FadeOutCoroutine(duration));
    }

    private IEnumerator FadeOutCoroutine(float duration)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            bgmFadeMultiplier = Mathf.Lerp(1f, 0f, elapsedTime / duration);
            ApplyBGMVolume();
            yield return null;
        }

        bgmPlayer.Stop();
        bgmFadeMultiplier = 1f;
        ApplyBGMVolume();
        CurrentBGM = EBgm.NONE; // Reset to default BGM type
        bgmFadeCoroutine = null;
    }

    public void SetSFXVolume(float volume)
    {
        float clampedVolume = Mathf.Clamp01(volume);
        if (Mathf.Approximately(sfxVolume, clampedVolume))
            return;

        sfxVolume = clampedVolume;
        PlayerPrefs.SetFloat(SFXVolumeKey, sfxVolume);
        ApplyAllSFXVolumes();
        VolumeSettingsChanged?.Invoke();
    }

    private IEnumerator ReturnToPool(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);
        source.Stop();
        source.clip = null;
        source.enabled = false;
        audioSourcePool.Enqueue(source);
    }

    private void LoadVolumeSettings()
    {
        masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
        bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BGMVolumeKey, 1f));
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SFXVolumeKey, 1f));
        isMasterMuted = PlayerPrefs.GetInt(MasterMutedKey, 0) != 0;
        isBGMMuted = PlayerPrefs.GetInt(BGMMutedKey, 0) != 0;
        isSFXMuted = PlayerPrefs.GetInt(SFXMutedKey, 0) != 0;
    }

    private void ApplyAllVolumes()
    {
        ApplyBGMVolume();
        ApplyAllSFXVolumes();
    }

    private void ApplyBGMVolume()
    {
        if (bgmPlayer == null)
            return;

        bgmPlayer.volume = isMasterMuted || isBGMMuted
            ? 0f
            : masterVolume * bgmVolume * bgmFadeMultiplier;
    }

    private void ApplyAllSFXVolumes()
    {
        for (int i = 0; i < sfxPlayers.Count; i++)
        {
            ApplySFXVolume(sfxPlayers[i]);
        }
    }

    private void ApplySFXVolume(AudioSource source)
    {
        if (source == null)
            return;

        source.volume = isMasterMuted || isSFXMuted
            ? 0f
            : masterVolume * sfxVolume;
    }

    private void CancelBGMFade()
    {
        if (bgmFadeCoroutine == null)
            return;

        StopCoroutine(bgmFadeCoroutine);
        bgmFadeCoroutine = null;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            PlayerPrefs.Save();
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }
}
