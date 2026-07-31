using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// BGM
public enum EBgm
{
    TITLE,
    GAME,
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
    public static SoundManager Instance { get; private set; }

    [Header("Audio Clips")]
    [SerializeField] private BGMSoundData[] bgmClips;
    [SerializeField] private SFXSoundData[] sfxClips;

    [Header("Pool Settings")]
    [SerializeField] private int poolSize = 10;

    private Dictionary<EBgm, AudioClip> bgmDict;
    private Dictionary<ESfx, AudioClip> sfxDict;
    private Queue<AudioSource> audioSourcePool;

    private AudioSource bgmPlayer;
    private AudioSource sfxPlayer;

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
        for (int i = 0; i < bgmClips.Length; i++)
        {
            bgmDict[bgmClips[i].bgmType] = bgmClips[i].bgmClip;
        }

        sfxDict = new Dictionary<ESfx, AudioClip>();
        for (int i = 0; i < sfxClips.Length; i++)
        {
            sfxDict[sfxClips[i].sfxType] = sfxClips[i].sfxClip;
        }

        bgmPlayer = gameObject.AddComponent<AudioSource>();
        bgmPlayer.loop = true;

        InitPool();
    }

    private void InitPool()
    {
        audioSourcePool = new Queue<AudioSource>();
        for (int i = 0; i < poolSize; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.enabled = false;
            audioSourcePool.Enqueue(source);
        }
    }

    public void PlaySFX(ESfx sfxType, bool randomPitch = true, float minPitch = 0.8f, float maxPitch = 1.2f)
    {
        if (sfxDict.TryGetValue(sfxType, out var clip))
        {
            if (audioSourcePool.Count > 0)
            {
                AudioSource source = audioSourcePool.Dequeue();
                source.clip = clip;
                source.enabled = true;
                if (randomPitch)
                {
                    source.pitch = Random.Range(minPitch, maxPitch);
                }
                else source.pitch = 1f;
                source.Play();

                StartCoroutine(ReturnToPool(source, clip.length));
            }
            else
            {
                AudioSource newSource = gameObject.AddComponent<AudioSource>();
                newSource.clip = clip;
                newSource.playOnAwake = false;
                newSource.enabled = true;
                newSource.Play();

                StartCoroutine(ReturnToPool(newSource, clip.length));
            }
        }
        else Debug.LogWarning("SFX not found: " + sfxType.ToString());
    }

    public void PlayBGM(EBgm bgmType)
    {
        if (bgmDict.TryGetValue(bgmType, out var clip))
        {
            if (bgmPlayer.clip != clip)
            {
                bgmPlayer.clip = clip;
                bgmPlayer.Play();
            }
        }
        else Debug.LogWarning("BGM not found: " + bgmType.ToString());
        
    }

    private IEnumerator ReturnToPool(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);
        source.enabled = false;
        audioSourcePool.Enqueue(source);
    }
}
