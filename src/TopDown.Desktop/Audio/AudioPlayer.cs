using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using TopDown.Desktop.Graphics;

namespace TopDown.Desktop.Audio;

public enum AudioChannel
{
    Music,
    Ambience,
    Effects,
    Interface,
}

/// <summary>
/// Plays sounds by logical id on four volume channels, with simple 2D spatialisation
/// (stereo pan and distance attenuation relative to the listener).
/// </summary>
public sealed class AudioPlayer : IDisposable
{
    /// <summary>The same sound is not restarted more often than this (shotgun pellets, bullet sprays…).</summary>
    private const float MinRepeatInterval = 0.03f;

    private readonly AssetCache _assets;
    private readonly UserSettings _settings;
    private readonly Dictionary<string, float> _lastPlayed = new(StringComparer.Ordinal);
    private readonly Random _random = new(11);
    private readonly Loop _music;
    private readonly Loop _ambience;
    private float _time;

    public AudioPlayer(AssetCache assets, UserSettings settings, bool enabled)
    {
        _assets = assets;
        _settings = settings;
        Enabled = enabled;
        _music = new Loop(this, AudioChannel.Music);
        _ambience = new Loop(this, AudioChannel.Ambience);
    }

    public bool Enabled { get; }
    public Vector2 ListenerPosition { get; set; }

    /// <summary>Distance at which positional sounds become inaudible (world units).</summary>
    public float HearingDistance { get; set; } = 900f;

    public float Volume(AudioChannel channel) => _settings.MasterVolume * channel switch
    {
        AudioChannel.Music => _settings.MusicVolume,
        AudioChannel.Ambience => _settings.AmbienceVolume,
        AudioChannel.Interface => _settings.UiVolume,
        _ => _settings.EffectsVolume,
    };

    public void Play(string? soundId, Vector2? worldPosition = null, AudioChannel channel = AudioChannel.Effects, float volume = 1f, float pitchVariation = 0.06f)
    {
        if (!Enabled || soundId is null) return;
        SoundEffect? sound = _assets.Sound(soundId);
        if (sound is null) return;
        if (_lastPlayed.TryGetValue(soundId, out float last) && _time - last < MinRepeatInterval) return;
        _lastPlayed[soundId] = _time;

        float pan = 0f;
        float gain = volume * Volume(channel);
        if (worldPosition is { } position)
        {
            Vector2 offset = position - ListenerPosition;
            float distance = offset.Length();
            gain *= MathF.Pow(Math.Clamp(1f - distance / HearingDistance, 0f, 1f), 1.5f);
            pan = Math.Clamp(offset.X / (HearingDistance * 0.5f), -0.85f, 0.85f);
        }

        if (gain <= 0.005f) return;
        float pitch = ((float)_random.NextDouble() * 2f - 1f) * pitchVariation;
        try
        {
            sound.Play(Math.Clamp(gain, 0f, 1f), pitch, pan);
        }
        catch (InstancePlayLimitException)
        {
            // Too many sounds at once: dropping one is better than stalling the game.
        }
    }

    public void PlayMusic(string? soundId) => _music.Set(soundId);

    public void PlayAmbience(string? soundId) => _ambience.Set(soundId);

    public void Update(float dt)
    {
        _time += dt;
        _music.Update(dt);
        _ambience.Update(dt);
    }

    public void Dispose()
    {
        _music.Dispose();
        _ambience.Dispose();
    }

    /// <summary>A looping track with short fades when it changes.</summary>
    private sealed class Loop(AudioPlayer owner, AudioChannel channel) : IDisposable
    {
        private const float FadeSpeed = 1.5f;
        private SoundEffectInstance? _current;
        private SoundEffectInstance? _fadingOut;
        private string? _currentId;
        private float _fade;

        public void Set(string? soundId)
        {
            if (!owner.Enabled || soundId == _currentId) return;
            _fadingOut?.Dispose();
            _fadingOut = _current;
            _current = null;
            _currentId = soundId;
            _fade = 0f;
            if (owner._assets.Sound(soundId) is not { } sound) return;
            _current = sound.CreateInstance();
            _current.IsLooped = true;
            _current.Volume = 0f;
            _current.Play();
        }

        public void Update(float dt)
        {
            _fade = MathF.Min(1f, _fade + dt * FadeSpeed);
            float volume = owner.Volume(channel);
            if (_current is not null) _current.Volume = Math.Clamp(volume * _fade, 0f, 1f);
            if (_fadingOut is null) return;
            float outVolume = volume * (1f - _fade);
            if (outVolume <= 0.01f)
            {
                _fadingOut.Dispose();
                _fadingOut = null;
            }
            else
            {
                _fadingOut.Volume = Math.Clamp(outVolume, 0f, 1f);
            }
        }

        public void Dispose()
        {
            _current?.Dispose();
            _fadingOut?.Dispose();
        }
    }
}
