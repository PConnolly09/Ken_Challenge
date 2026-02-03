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
    [Range(0f, 1f)] public float sfxVolume = 0.5f;

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

    void Start()
    {
        // Initialize volumes immediately
        UpdateSourceVolumes();
        if (musicTracks.Count > 0) PlayNextTrack();
    }

    void Update()
    {
        if (!musicSource.isPlaying && musicTracks.Count > 0) PlayNextTrack();

        // Force update music volume in real-time for slider feedback
        if (musicSource.volume != musicVolume) musicSource.volume = musicVolume;
    }

    private void InitializeSources()
    {
        if (!musicSource) musicSource = gameObject.AddComponent<AudioSource>();
        if (!sfxSource) sfxSource = gameObject.AddComponent<AudioSource>();
        if (!uiSource) uiSource = gameObject.AddComponent<AudioSource>();
        if (!footstepSource) footstepSource = gameObject.AddComponent<AudioSource>();

        UpdateSourceVolumes();
    }

    private void UpdateSourceVolumes()
    {
        if (musicSource) musicSource.volume = musicVolume;
        if (sfxSource) sfxSource.volume = sfxVolume;
        if (uiSource) uiSource.volume = sfxVolume;
        if (footstepSource) footstepSource.volume = sfxVolume;
    }

    public void PlayNextTrack()
    {
        if (musicTracks.Count == 0) return;
        currentTrackIndex = (currentTrackIndex + 1) % musicTracks.Count;
        musicSource.clip = musicTracks[currentTrackIndex];
        musicSource.Play();
    }

    // --- GENERIC HELPERS ---

    public void PlayOneShot(AudioClip clip, float volScale = 1f)
    {
        // Note: PlayOneShot scales volume by the Source volume automatically.
        // We pass the relative scale (e.g. 0.5 for a quiet sound)
        if (clip) sfxSource.PlayOneShot(clip, volScale);
    }

    public void PlayUI(AudioClip clip, float volScale = 1f)
    {
        if (clip) uiSource.PlayOneShot(clip, volScale);
    }

    // --- FOOTSTEPS ---

    public void PlayRandomFootstep(bool onContainer = false)
    {
        AudioClip[] clips = onContainer ? containerStepClips : footstepClips;

        if (clips != null && clips.Length > 0)
        {
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            // Randomize pitch slightly for variety
            footstepSource.pitch = Random.Range(0.9f, 1.1f);
            footstepSource.PlayOneShot(clip, 0.6f);
        }
    }

    // --- UI EVENT TRIGGERS ---

    public void PlayClick() => PlayUI(menuClickClip);
    public void PlayBack() => PlayUI(menuBackClip);
    public void PlayVictory() => PlayUI(victoryClip);
    public void PlayHighScoreEntry() => PlayUI(highScoreEntryClip);

    // --- SETTINGS (Called by UI Sliders) ---

    public void SetMusicVolume(float value)
    {
        // IMPORTANT: Ensure your UI Slider is set to Min: 0, Max: 1
        musicVolume = Mathf.Clamp01(value);
        if (musicSource) musicSource.volume = musicVolume;
    }

    public void SetSFXVolume(float value)
    {
        // IMPORTANT: Ensure your UI Slider is set to Min: 0, Max: 1
        sfxVolume = Mathf.Clamp01(value);
        if (sfxSource) sfxSource.volume = sfxVolume;
        if (uiSource) uiSource.volume = sfxVolume;
        if (footstepSource) footstepSource.volume = sfxVolume;
    }
}