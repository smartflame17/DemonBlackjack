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
}


public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Audio Clips")]
    [Tooltip("Clip order must match the order of the EBgm enum.")]
    [SerializeField] private AudioClip[] bgmClips;
    [Tooltip("Clip order must match the order of the ESfx enum.")]
    [SerializeField] private AudioClip[] sfxClips;

    [Header("Pool Settings")]
    [SerializeField] private int poolSize = 10;

    private Dictionary<EBgm, AudioClip> bgmDict;
    private Dictionary<ESfx, AudioClip> sfxDict;
    private Queue<AudioSource> audioSourcePool;

    private AudioSource bgmPlayer;

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
            bgmDict[(EBgm)i] = bgmClips[i];
        }

        sfxDict = new Dictionary<ESfx, AudioClip>();
        for (int i = 0; i < sfxClips.Length; i++)
        {
            sfxDict[(ESfx)i] = sfxClips[i];
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

    public void PlaySFX(ESfx sfxType)
    {
        if (sfxDict.TryGetValue(sfxType, out var clip))
        {
            if (audioSourcePool.Count > 0)
            {
                AudioSource source = audioSourcePool.Dequeue();
                source.clip = clip;
                source.enabled = true;
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
        else Debug.LogWarning("SFX not found");
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
        else Debug.LogWarning("BGM not found");
        
    }

    private IEnumerator ReturnToPool(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);
        source.enabled = false;
        audioSourcePool.Enqueue(source);
    }
}
