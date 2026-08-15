using MessagePipe;
using VContainer;
using VContainer.Unity;

public sealed class SettingsModule : ModuleBase, IInitializable
{
    [Inject] private AudioModule _audioModule;
    [Inject] private HapticModule _hapticModule;
    [Inject] private RemoteConfigModule _remoteConfigModule;

    [Inject] private IPublisher<ColorBlindUpdateMessage> _colorBlindUpdatePub;

    private ColorBlindConfig _colorBlindConfig;

    private PlayerPrefsBool _sound = Prefs.Sound;
    private PlayerPrefsBool _music = Prefs.Music;
    private PlayerPrefsBool _colorBlind = Prefs.ColorBlind;

    public void Initialize()
    {
        SetSound(_sound);
        SetMusic(_music);
        // SetNotification(_notification); - NotificationModule.Wait

        _colorBlindConfig = _remoteConfigModule.GetDataClassNew<ColorBlindConfig>();
    }

    public bool IsSoundActive() => _sound.Value;
    public bool IsMusicActive() => _music.Value;
    public bool IsColorBlindActive() => _colorBlind.HasValue() ? _colorBlind : _colorBlindConfig.Active;

    public void SetSound(bool isActive)
    {
        _sound.Set(isActive);
        _audioModule.SetAudioMute(!isActive);
    }

    public void SetMusic(bool isActive)
    {
        _music.Set(isActive);
        _audioModule.SetMusicMute(!isActive);
    }

    public void SetHaptic(bool isActive)
    {
        _hapticModule.SetMute(!isActive);
    }

    public void SetColorBlind(bool isActive)
    {
        _colorBlind.Set(isActive);
        _colorBlindUpdatePub.Publish(new ColorBlindUpdateMessage { Active = isActive });
    }
}

public struct ColorBlindUpdateMessage
{
    public bool Active;
}