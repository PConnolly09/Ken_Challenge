using UnityEngine;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;      // General SFX
    public AudioSource uiSource;       // UI (Unpitchable)
    public AudioSource footstepSource; // Dedicated for pitch shifting steps

    [Header("Music")]
    public List<AudioClip> musicTracks;
    [Range(0f, 1f)] public float musicVolume = 0.5f;

    [Header("Player Movement")]
    public AudioClip[] footstepClips;
    public AudioClip[] containerStepClips;
    public AudioClip jumpClip;
    public AudioClip landHeavyClip;
    public AudioClip landWaterClip;

    [Header("Player Abilities")]
    public AudioClip jukeClip;
    public AudioClip spinClip;
    public AudioClip stiffArmClip;
    public AudioClip impactClip;
    public AudioClip fumbleClip;

    [Header("Object SFX")]
    public AudioClip packagePickupClip;
    public AudioClip containerImpactClip;
    public AudioClip containerSquishClip;
    public AudioClip craneMoveClip;
    public AudioClip grabberMoveClip;

    [Header("UI SFX")]
    public AudioClip menuClickClip;
    public AudioClip menuBackClip;
    public AudioClip victoryClip;
    public AudioClip highScoreEntryClip;
    public AudioClip leaderboardClip;

    private int currentTrackIndex = 0;

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); InitializeSources(); }
        else Destroy(gameObject);
    }

    void Start() { if (musicTracks.Count > 0) PlayNextTrack(); }

    void Update()
    {
        if (!musicSource.isPlaying && musicTracks.Count > 0) PlayNextTrack();
        musicSource.volume = musicVolume;
    }

    private void InitializeSources()
    {
        if (!musicSource) musicSource = gameObject.AddComponent<AudioSource>();
        if (!sfxSource) sfxSource = gameObject.AddComponent<AudioSource>();
        if (!uiSource) uiSource = gameObject.AddComponent<AudioSource>();
        if (!footstepSource) footstepSource = gameObject.AddComponent<AudioSource>();
    }

    public void PlayNextTrack()
    {
        currentTrackIndex = (currentTrackIndex + 1) % musicTracks.Count;
        musicSource.clip = musicTracks[currentTrackIndex];
        musicSource.Play();
    }

    // --- GENERIC HELPERS ---

    public void PlayOneShot(AudioClip clip, float vol = 1f)
    {
        if (clip) sfxSource.PlayOneShot(clip, vol);
    }

    public void PlayUI(AudioClip clip, float vol = 1f)
    {
        if (clip) uiSource.PlayOneShot(clip, vol);
    }

    // --- FOOTSTEPS (Fixed Pitch Issue) ---

    public void PlayRandomFootstep(bool onContainer = false)
    {
        AudioClip[] clips = onContainer ? containerStepClips : footstepClips;

        if (clips != null && clips.Length > 0)
        {
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            // Use dedicated source so pitch changes don't affect other SFX
            footstepSource.pitch = Random.Range(0.9f, 1.1f);
            footstepSource.PlayOneShot(clip, 0.6f);
        }
    }

    // --- UI EVENT TRIGGERS (Hook these to Buttons in Inspector) ---

    public void PlayClick() => PlayUI(menuClickClip);
    public void PlayBack() => PlayUI(menuBackClip);
    public void PlayVictory() => PlayUI(victoryClip);
    public void PlayHighScoreEntry() => PlayUI(highScoreEntryClip);

    // --- SETTINGS ---
    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        if (musicSource) musicSource.volume = musicVolume;
    }

    public void SetSFXVolume(float value)
    {
        // For simple setup, we just affect the volume property of sources directly or use AudioListener for global
        // Here we just set the volume for the next plays if we stored it, but since PlayOneShot takes volume scale,
        // we might want a global multiplier. For now, this stub exists for the UI Slider hookup.
        if (sfxSource) sfxSource.volume = value;
        if (uiSource) uiSource.volume = value;
        if (footstepSource) footstepSource.volume = value;
    }
}