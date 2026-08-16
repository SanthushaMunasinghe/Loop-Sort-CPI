using System;
using System.Collections.Generic;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public sealed class AudioModule : ModuleBase, IInitializable
{
    [Inject] public Sounds Sounds { get; private set; }

    private GameObject _audioSourceContainer;
    private AudioSource _musicSource;
    private IDisposable _musicVolumeDisposable;
    private bool _audioMute;

    private readonly List<AudioSource> _audioSources = new();

    private const int AudioSourcePoolLimit = 30;
    private const float AudioSourceVolume = .25f;
    private const float MusicSourceVolume = .1f;

    public void Initialize()
    {
        // Deliberately not DontDestroyOnLoad — the sandbox keeps everything inside the scene.
        _audioSourceContainer = new GameObject("Audio Source Container");

        var musicSource = new GameObject("Music Source", typeof(AudioSource));
        _musicSource = musicSource.GetComponent<AudioSource>();
        _musicSource.loop = true;
        _musicSource.playOnAwake = false;
        _musicSource.volume = MusicSourceVolume;
    }

    public void EnableMusic()
    {
        if (_musicSource.isPlaying) return;
        _musicSource.Play();
    }

    public void DisableMusic() => _musicSource.Stop();

    public void SetMusicMute(bool mute) => _musicSource.mute = mute;

    public void PauseMusic() => _musicSource.Pause();

    public void UnPauseMusic() => _musicSource.UnPause();

    public void RestartMusic() => _musicSource.Play();

    public void SetAudioMute(bool mute)
    {
        _audioMute = mute;
        foreach (var audioSource in _audioSources)
            audioSource.mute = mute;
    }

    public void SetMusicVolume(float volume)
    {
        _musicVolumeDisposable?.Dispose();
        _musicVolumeDisposable = LMotion.Create(_musicSource.volume, volume, 1.25f)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToVolume(_musicSource)
            .ToDisposable();
    }

    public void ReturnOriginalMusicVolume()
    {
        SetMusicVolume(MusicSourceVolume);
    }

    public void ChangeMusic(AudioClip clip)
    {
        _musicSource.clip = clip;
    }

    public AudioPlayer GetPlayer()
    {
        var audioSource = GetAvailableSource();
        if (audioSource == null) return default;
        audioSource.mute = _audioMute;
        audioSource.volume = AudioSourceVolume;
        audioSource.pitch = 1f;
        return AudioPlayer.CreatePlayer(audioSource, _audioMute);
    }

    private AudioSource GetAvailableSource()
    {
        foreach (var audioSource in _audioSources)
        {
            if (audioSource.isPlaying) continue;
            return audioSource;
        }

        if (_audioSources.Count >= AudioSourcePoolLimit) return null;

        var source = _audioSourceContainer.AddComponent<AudioSource>();
        _audioSources.Add(source);
        return source;
    }
}

public struct AudioPlayer
{
    private AudioSource _audioSource;
    private bool _mute;
    private float _cooldownTime;
    private float _pitchIncreaseCooldownTime;
    private float _increasePitchAmount;
    private float _maxPitch;
    private int _skipCount;

    private static readonly Dictionary<int, float> LastPlayTimestampByClipInstanceID = new();
    private static readonly Dictionary<int, int> IncreasePitchCountByClipInstanceID = new();
    private static readonly Dictionary<int, int> SkipCountByClipInstanceID = new();

    public static AudioPlayer CreatePlayer(AudioSource audioSource, bool mute = false)
    {
        var audioPlayer = new AudioPlayer
        {
            _audioSource = audioSource,
            _mute = mute
        };
        return audioPlayer;
    }

    public void Play(AudioClip clip)
    {
        if (clip == null) return;
        if (_mute) return;
        if (_audioSource == null) return;

        var clipInstanceID = clip.GetInstanceID();
        if (LastPlayTimestampByClipInstanceID.TryGetValue(clipInstanceID, out var lastPlayTimestamp))
        {
            var timeInterval = Time.time - lastPlayTimestamp;
            if (_cooldownTime > timeInterval)
                return;

            IncreasePitchCountByClipInstanceID.TryGetValue(clipInstanceID, out var increaseCount);
            if (timeInterval > _pitchIncreaseCooldownTime) increaseCount = 0;
            _audioSource.pitch += increaseCount * _increasePitchAmount;
            IncreasePitchCountByClipInstanceID[clipInstanceID] = increaseCount + 1;
        }

        if (_maxPitch > 0f)
            _audioSource.pitch = Mathf.Min(_audioSource.pitch, _maxPitch);

        if (SkipCountByClipInstanceID.TryGetValue(clipInstanceID, out var skipCount) && skipCount > 0)
        {
            SkipCountByClipInstanceID[clipInstanceID] = skipCount - 1;
            return;
        }
        if (_skipCount > 0) SkipCountByClipInstanceID[clipInstanceID] = _skipCount;

        LastPlayTimestampByClipInstanceID[clipInstanceID] = Time.time;
        _audioSource.PlayOneShot(clip);
    }

    public AudioPlayer WithVolume(float volume)
    {
        if (_audioSource == null) return this;
        _audioSource.volume = volume;
        return this;
    }

    public AudioPlayer WithVolumeScale(float volumeScale)
    {
        if (_audioSource == null) return this;
        _audioSource.volume *= volumeScale;
        return this;
    }

    public AudioPlayer WithPitch(float pitch)
    {
        if (_audioSource == null) return this;
        _audioSource.pitch = pitch;
        return this;
    }

    public AudioPlayer WithPitchRange(float pitchRange)
    {
        if (_audioSource == null) return this;
        _audioSource.pitch = 1f + (Random.value * pitchRange - pitchRange / 2f);
        return this;
    }

    public AudioPlayer WithPlayProbability(float playProbability)
    {
        _mute = Random.value > playProbability;
        return this;
    }

    public AudioPlayer WithCooldown(float cooldownTime)
    {
        _cooldownTime = cooldownTime;
        return this;
    }

    public AudioPlayer WithPitchIncrease(float increaseAmount, float increaseCooldownTime)
    {
        _increasePitchAmount = increaseAmount;
        _pitchIncreaseCooldownTime = increaseCooldownTime;
        return this;
    }

    public AudioPlayer WithMaxPitch(float maxPitch)
    {
        _maxPitch = maxPitch;
        return this;
    }

    public AudioPlayer WithSkipCount(int skipCount)
    {
        _skipCount = skipCount;
        return this;
    }
}