using Game.Desktop.Graphics;
using Microsoft.Xna.Framework.Audio;

namespace Game.Desktop.Audio;

/// <summary>Plays sounds by logical id, and one looping music track.</summary>
public sealed class AudioPlayer : IDisposable
{
    /// <summary>The same sound is not restarted more often than this (auto fire, several deaths at once).</summary>
    private const float MinRepeatInterval = 0.05f;

    private readonly AssetCache _assets;
    private readonly UserSettings _settings;
    private readonly Dictionary<string, float> _lastPlayed = new(StringComparer.Ordinal);
    private readonly Random _pitch = new(11);
    private SoundEffectInstance? _music;
    private string? _musicId;
    private float _time;

    public AudioPlayer(AssetCache assets, UserSettings settings, bool enabled)
    {
        _assets = assets;
        _settings = settings;
        Enabled = enabled;
    }

    public bool Enabled { get; }

    /// <summary>Plays a sound; <paramref name="pan"/> from -1 (left) to 1 (right).</summary>
    public void Play(string? soundId, float pan = 0f, float volume = 1f)
    {
        if (!Enabled || soundId is null || _assets.Sound(soundId) is not { } sound) return;
        if (_lastPlayed.TryGetValue(soundId, out float last) && _time - last < MinRepeatInterval) return;
        _lastPlayed[soundId] = _time;
        float gain = Math.Clamp(volume * _settings.MasterVolume * _settings.EffectsVolume, 0f, 1f);
        if (gain <= 0.005f) return;
        float pitch = ((float)_pitch.NextDouble() * 2f - 1f) * 0.05f;
        try
        {
            sound.Play(gain, pitch, Math.Clamp(pan, -0.8f, 0.8f));
        }
        catch (InstancePlayLimitException)
        {
            // Too many sounds at once: dropping one is better than stalling the game.
        }
    }

    public void PlayMusic(string? soundId)
    {
        if (!Enabled || soundId == _musicId) return;
        _music?.Dispose();
        _music = null;
        _musicId = soundId;
        if (_assets.Sound(soundId) is not { } sound) return;
        _music = sound.CreateInstance();
        _music.IsLooped = true;
        _music.Volume = Math.Clamp(_settings.MasterVolume * _settings.MusicVolume, 0f, 1f);
        _music.Play();
    }

    public void Update(float dt)
    {
        _time += dt;
        if (_music is not null) _music.Volume = Math.Clamp(_settings.MasterVolume * _settings.MusicVolume, 0f, 1f);
    }

    public void Dispose() => _music?.Dispose();
}
