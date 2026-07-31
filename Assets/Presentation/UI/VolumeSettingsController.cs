using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class VolumeSettingsController : MonoBehaviour
{
    [Header("Volume Sliders")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("Mute Toggles")]
    [SerializeField] private Toggle masterMuteToggle;
    [SerializeField] private Toggle bgmMuteToggle;
    [SerializeField] private Toggle sfxMuteToggle;

    private SoundManager soundManager;
    private Coroutine bindCoroutine;
    private bool listenersRegistered;

    private void OnEnable()
    {
        if (!HasRequiredReferences())
        {
            Debug.LogError(
                "VolumeSettingsController requires three volume sliders and three mute toggles.",
                this);
            return;
        }

        ConfigureSliders();
        AddListeners();
        SetControlsInteractable(false);
        bindCoroutine = StartCoroutine(BindWhenSoundManagerIsReady());
    }

    private void OnDisable()
    {
        if (bindCoroutine != null)
        {
            StopCoroutine(bindCoroutine);
            bindCoroutine = null;
        }

        UnbindSoundManager();
        RemoveListeners();
    }

    public void RefreshFromSoundManager()
    {
        if (soundManager == null)
            return;

        masterVolumeSlider.SetValueWithoutNotify(soundManager.MasterVolume);
        bgmVolumeSlider.SetValueWithoutNotify(soundManager.BGMVolume);
        sfxVolumeSlider.SetValueWithoutNotify(soundManager.SFXVolume);
        masterMuteToggle.SetIsOnWithoutNotify(soundManager.IsMasterMuted);
        bgmMuteToggle.SetIsOnWithoutNotify(soundManager.IsBGMMuted);
        sfxMuteToggle.SetIsOnWithoutNotify(soundManager.IsSFXMuted);
    }

    private IEnumerator BindWhenSoundManagerIsReady()
    {
        while (isActiveAndEnabled && SoundManager.Instance == null)
            yield return null;

        bindCoroutine = null;
        if (isActiveAndEnabled)
            BindSoundManager(SoundManager.Instance);
    }

    private void BindSoundManager(SoundManager manager)
    {
        if (soundManager == manager)
            return;

        UnbindSoundManager();
        soundManager = manager;

        if (soundManager == null)
            return;

        soundManager.VolumeSettingsChanged += RefreshFromSoundManager;
        RefreshFromSoundManager();
        SetControlsInteractable(true);
    }

    private void UnbindSoundManager()
    {
        if (soundManager != null)
            soundManager.VolumeSettingsChanged -= RefreshFromSoundManager;

        soundManager = null;
    }

    private void ConfigureSliders()
    {
        ConfigureSlider(masterVolumeSlider);
        ConfigureSlider(bgmVolumeSlider);
        ConfigureSlider(sfxVolumeSlider);
    }

    private static void ConfigureSlider(Slider slider)
    {
        if (slider == null)
            return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
    }

    private void AddListeners()
    {
        if (listenersRegistered)
            return;

        masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        bgmVolumeSlider.onValueChanged.AddListener(SetBGMVolume);
        sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolume);
        masterMuteToggle.onValueChanged.AddListener(SetMasterMuted);
        bgmMuteToggle.onValueChanged.AddListener(SetBGMMuted);
        sfxMuteToggle.onValueChanged.AddListener(SetSFXMuted);
        listenersRegistered = true;
    }

    private void RemoveListeners()
    {
        if (!listenersRegistered)
            return;

        masterVolumeSlider.onValueChanged.RemoveListener(SetMasterVolume);
        bgmVolumeSlider.onValueChanged.RemoveListener(SetBGMVolume);
        sfxVolumeSlider.onValueChanged.RemoveListener(SetSFXVolume);
        masterMuteToggle.onValueChanged.RemoveListener(SetMasterMuted);
        bgmMuteToggle.onValueChanged.RemoveListener(SetBGMMuted);
        sfxMuteToggle.onValueChanged.RemoveListener(SetSFXMuted);
        listenersRegistered = false;
    }

    private void SetControlsInteractable(bool interactable)
    {
        masterVolumeSlider.interactable = interactable;
        bgmVolumeSlider.interactable = interactable;
        sfxVolumeSlider.interactable = interactable;
        masterMuteToggle.interactable = interactable;
        bgmMuteToggle.interactable = interactable;
        sfxMuteToggle.interactable = interactable;
    }

    private void SetMasterVolume(float volume)
    {
        soundManager?.SetMasterVolume(volume);
    }

    private void SetBGMVolume(float volume)
    {
        soundManager?.SetBGMVolume(volume);
    }

    private void SetSFXVolume(float volume)
    {
        soundManager?.SetSFXVolume(volume);
    }

    private void SetMasterMuted(bool muted)
    {
        soundManager?.SetMasterMuted(muted);
    }

    private void SetBGMMuted(bool muted)
    {
        soundManager?.SetBGMMuted(muted);
    }

    private void SetSFXMuted(bool muted)
    {
        soundManager?.SetSFXMuted(muted);
    }

    private bool HasRequiredReferences()
    {
        return masterVolumeSlider != null
            && bgmVolumeSlider != null
            && sfxVolumeSlider != null
            && masterMuteToggle != null
            && bgmMuteToggle != null
            && sfxMuteToggle != null;
    }
}
