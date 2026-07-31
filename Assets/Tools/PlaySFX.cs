using UnityEngine;
using System;

// Plug and play script to play a sound effect when a button is clicked or an event occurs. Attach this script to a GameObject and set the desired SFX in the inspector.
public class PlaySFX : MonoBehaviour
{
    [SerializeField] private ESfx sfxToPlay;

    public void Play()
    {
        if (sfxToPlay == ESfx.NONE)
        {
            Debug.LogWarning("PlaySFX: No SFX specified to play.", this);
            return;
        }

        SoundManager.Instance?.PlaySFX(sfxToPlay);
    }
}