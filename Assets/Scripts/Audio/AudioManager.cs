using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Background music and SFX manager.
/// Dynamic music system - plays different tracks based on game state.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public enum MusicTrack
    {
        MainMenu,
        CastlePeaceful,
        CastleTension,
        Battle,
        Victory,
        Defeat,
        WorldMap
    }

    public enum SFXType
    {
        ButtonClick,
        ResourceGain,
        ResourceLose,
        BuildingComplete,
        NPCGreeting,
        NPCAngry,
        OrcRoar,
        SwordClash,
        FireCrackle,
        CoinClink,
        NotificationPop,
        DayAdvance,
        VictoryFanfare
    }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource _musicSource;
    [SerializeField] private AudioSource _sfxSource;
    [SerializeField] private AudioSource _ambienceSource;

    [Header("Music Clips")]
    [SerializeField] private AudioClip _mainMenuMusic;
    [SerializeField] private AudioClip _castlePeacefulMusic;
    [SerializeField] private AudioClip _castleTensionMusic;
    [SerializeField] private AudioClip _battleMusic;
    [SerializeField] private AudioClip _victoryMusic;
    [SerializeField] private AudioClip _defeatMusic;
    [SerializeField] private AudioClip _worldMapMusic;

    [Header("SFX Clips")]
    [SerializeField] private AudioClip _buttonClickSFX;
    [SerializeField] private AudioClip _resourceGainSFX;
    [SerializeField] private AudioClip _resourceLoseSFX;
    [SerializeField] private AudioClip _buildingCompleteSFX;
    [SerializeField] private AudioClip _coinClinkSFX;
    [SerializeField] private AudioClip _notificationSFX;
    [SerializeField] private AudioClip _swordClashSFX;

    [Header("Settings")]
    [SerializeField, Range(0f, 1f)] private float _musicVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float _sfxVolume = 1.0f;

    private MusicTrack _currentTrack = MusicTrack.MainMenu;
    private Coroutine _fadeCoroutine;
    private Dictionary<SFXType, AudioClip> _sfxMap;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SetupAudioSources();
        BuildSFXMap();
        LoadVolumeSettings();
    }

    private void SetupAudioSources()
    {
        if (_musicSource == null)
        {
            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.loop = true;
            _musicSource.volume = _musicVolume;
            _musicSource.spatialBlend = 0f;
        }
        if (_sfxSource == null)
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.loop = false;
            _sfxSource.volume = _sfxVolume;
            _sfxSource.spatialBlend = 0f;
        }
    }

    private void BuildSFXMap()
    {
        _sfxMap = new Dictionary<SFXType, AudioClip>
        {
            { SFXType.ButtonClick, _buttonClickSFX },
            { SFXType.ResourceGain, _resourceGainSFX },
            { SFXType.ResourceLose, _resourceLoseSFX },
            { SFXType.BuildingComplete, _buildingCompleteSFX },
            { SFXType.CoinClink, _coinClinkSFX },
            { SFXType.NotificationPop, _notificationSFX },
            { SFXType.SwordClash, _swordClashSFX }
        };
    }

    private void LoadVolumeSettings()
    {
        _musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
        _sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1.0f);
        if (_musicSource != null) _musicSource.volume = _musicVolume;
        if (_sfxSource != null) _sfxSource.volume = _sfxVolume;
    }

    private void Start()
    {
        // Subscribe to game state changes
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStateChanged += OnGameStateChanged;
    }

    private void OnGameStateChanged(GameManager.GameState oldState, GameManager.GameState newState)
    {
        var targetTrack = newState switch
        {
            GameManager.GameState.MainMenu => MusicTrack.MainMenu,
            GameManager.GameState.Castle => MusicTrack.CastlePeaceful,
            GameManager.GameState.WorldMap => MusicTrack.WorldMap,
            GameManager.GameState.Battle => MusicTrack.Battle,
            GameManager.GameState.Event => MusicTrack.CastleTension,
            GameManager.GameState.Victory => MusicTrack.Victory,
            GameManager.GameState.GameOver => MusicTrack.Defeat,
            _ => _currentTrack
        };

        if (targetTrack != _currentTrack)
            PlayMusic(targetTrack);
    }

    public void PlayMusic(MusicTrack track, float fadeTime = 1f)
    {
        if (track == _currentTrack && _musicSource.isPlaying) return;
        _currentTrack = track;

        AudioClip clip = GetMusicClip(track);
        if (clip == null)
        {
            Debug.LogWarning($"[Audio] No clip assigned for track: {track}");
            return;
        }

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(CrossfadeMusic(clip, fadeTime));
    }

    private IEnumerator CrossfadeMusic(AudioClip newClip, float duration)
    {
        float startVolume = _musicSource.volume;

        // Fade out
        float elapsed = 0f;
        while (elapsed < duration / 2f)
        {
            elapsed += Time.deltaTime;
            _musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / (duration / 2f));
            yield return null;
        }

        _musicSource.clip = newClip;
        _musicSource.Play();

        // Fade in
        elapsed = 0f;
        while (elapsed < duration / 2f)
        {
            elapsed += Time.deltaTime;
            _musicSource.volume = Mathf.Lerp(0f, _musicVolume, elapsed / (duration / 2f));
            yield return null;
        }

        _musicSource.volume = _musicVolume;
    }

    public void PlaySFX(SFXType type)
    {
        if (_sfxMap.TryGetValue(type, out var clip) && clip != null)
            _sfxSource?.PlayOneShot(clip, _sfxVolume);
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip != null) _sfxSource?.PlayOneShot(clip, _sfxVolume);
    }

    private AudioClip GetMusicClip(MusicTrack track) => track switch
    {
        MusicTrack.MainMenu => _mainMenuMusic,
        MusicTrack.CastlePeaceful => _castlePeacefulMusic,
        MusicTrack.CastleTension => _castleTensionMusic,
        MusicTrack.Battle => _battleMusic,
        MusicTrack.Victory => _victoryMusic,
        MusicTrack.Defeat => _defeatMusic,
        MusicTrack.WorldMap => _worldMapMusic,
        _ => null
    };

    public void SetMusicVolume(float volume)
    {
        _musicVolume = Mathf.Clamp01(volume);
        if (_musicSource != null) _musicSource.volume = _musicVolume;
        PlayerPrefs.SetFloat("MusicVolume", _musicVolume);
    }

    public void SetSFXVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        if (_sfxSource != null) _sfxSource.volume = _sfxVolume;
        PlayerPrefs.SetFloat("SFXVolume", _sfxVolume);
    }

    public void StopMusic() => _musicSource?.Stop();
    public void PauseMusic() => _musicSource?.Pause();
    public void ResumeMusic() => _musicSource?.UnPause();
}
