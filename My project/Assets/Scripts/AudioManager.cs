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

    // IMPACT LOGIC
    private float nextImpactTime = 0f;

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

    // --- IMPACT SFX LOGIC ---
    public void PlayImpact(AudioClip clip)
    {
        if (clip == null) return;

        // Debounce: Only play if the previous impact has finished
        if (Time.time < nextImpactTime) return;

        // Play quietly (0.4 scale)
        PlayOneShot(clip, 0.4f);

        // Prevent another impact until this one finishes
        nextImpactTime = Time.time + clip.length;
    }

    // --- FOOTSTEPS ---

    public void PlayRandomFootstep(bool onContainer = false)
    {
        AudioClip[] clips = onContainer ? containerStepClips : footstepClips;

        if (clips != null && clips.Length > 0)
        {
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            footstepSource.pitch = Random.Range(0.9f, 1.1f);
            footstepSource.PlayOneShot(clip, 0.6f);
        }
    }

    // --- UI EVENT TRIGGERS ---

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
        if (sfxSource) sfxSource.volume = value;
        if (uiSource) uiSource.volume = value;
        if (footstepSource) footstepSource.volume = value;
    }
}