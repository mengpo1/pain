using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TopDown.Content;

namespace TopDown.Desktop.Graphics;

/// <summary>A light placed in the world (level light, player light, flash).</summary>
public readonly record struct WorldLight(Vector2 Position, float Radius, Color Color, float Intensity);

/// <summary>
/// Screen effects configured by the theme: darkness with lights, colour grading, vignette,
/// film grain, scanlines, flicker and video glitches. Built from blend states and generated
/// textures only, so it works on every MonoGame desktop platform without compiled shaders.
/// </summary>
public sealed class PostProcessor : IDisposable
{
    private static readonly BlendState Multiply = new()
    {
        ColorBlendFunction = BlendFunction.Add,
        ColorSourceBlend = Blend.DestinationColor,
        ColorDestinationBlend = Blend.Zero,
        AlphaBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One,
    };

    private readonly GraphicsDevice _device;
    private readonly AssetCache _assets;
    private readonly PostEffects _settings;
    private readonly Color _darkness;
    private readonly Color _colorMultiply;
    private readonly Random _random = new(13);
    private RenderTarget2D? _scene;
    private RenderTarget2D? _lights;
    private float _time;
    private float _glitchTimer;
    private float _flicker;

    public PostProcessor(GraphicsDevice device, AssetCache assets, PostEffects settings)
    {
        _device = device;
        _assets = assets;
        _settings = settings;
        _darkness = ColorParser.Parse(settings.DarknessColor, Color.Black);
        _colorMultiply = ColorParser.Parse(settings.ColorMultiply, Color.White);
    }

    public PostEffects Settings => _settings;

    /// <summary>Starts drawing the world into an off-screen target.</summary>
    public void BeginScene(Color clear)
    {
        int width = Math.Max(1, _device.PresentationParameters.BackBufferWidth);
        int height = Math.Max(1, _device.PresentationParameters.BackBufferHeight);
        Ensure(ref _scene, width, height);
        Ensure(ref _lights, width, height);
        _device.SetRenderTarget(_scene);
        _device.Clear(clear);
    }

    /// <summary>Composites the scene with lights and screen effects onto the back buffer.</summary>
    public void EndScene(SpriteBatch batch, Camera2D camera, IEnumerable<WorldLight> lights, float dt)
    {
        _time += dt;
        bool lit = _settings.Darkness > 0f;
        if (lit) RenderLightMap(batch, camera, lights);

        _device.SetRenderTarget(null);
        Rectangle screen = _device.Viewport.Bounds;

        batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp);
        batch.Draw(_scene!, screen, Color.White);
        batch.End();

        batch.Begin(SpriteSortMode.Deferred, Multiply, SamplerState.LinearClamp);
        if (lit) batch.Draw(_lights!, screen, Color.White);
        if (_colorMultiply != Color.White) batch.Draw(_assets.Pixel, screen, _colorMultiply);
        batch.End();

        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap);
        DrawGlitches(batch, screen, dt);
        if (_settings.Scanlines > 0f)
            batch.Draw(_assets.Scanlines, screen, new Rectangle(0, 0, 1, screen.Height), Color.White * _settings.Scanlines);

        if (_settings.Grain > 0f)
        {
            var source = new Rectangle(_random.Next(256), _random.Next(256), screen.Width / 2, screen.Height / 2);
            batch.Draw(_assets.Noise, screen, source, Color.White * _settings.Grain);
        }

        if (_settings.Flicker > 0f)
        {
            float target = _random.NextDouble() < 0.08 ? (float)_random.NextDouble() : 0f;
            _flicker += (target - _flicker) * MathF.Min(1f, dt * 20f);
            batch.Draw(_assets.Pixel, screen, Color.Black * (_flicker * _settings.Flicker * 0.35f));
        }

        if (_settings.Vignette > 0f) batch.Draw(_assets.Vignette, screen, Color.Black * _settings.Vignette);
        batch.End();
    }

    private void RenderLightMap(SpriteBatch batch, Camera2D camera, IEnumerable<WorldLight> lights)
    {
        _device.SetRenderTarget(_lights);
        _device.Clear(Color.Lerp(Color.White, _darkness, _settings.Darkness));
        batch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, null, null, null, camera.Transform);
        Texture2D circle = _assets.SoftCircle;
        var origin = new Vector2(circle.Width, circle.Height) * 0.5f;
        foreach (WorldLight light in lights)
        {
            float scale = light.Radius * 2f / circle.Width;
            batch.Draw(circle, light.Position, null, light.Color * light.Intensity, 0f, origin, scale, SpriteEffects.None, 0f);
        }

        batch.End();
    }

    /// <summary>Horizontal strips of the image shifted sideways, like a damaged video signal.</summary>
    private void DrawGlitches(SpriteBatch batch, Rectangle screen, float dt)
    {
        if (_settings.VideoNoise <= 0f || _scene is null) return;
        _glitchTimer -= dt;
        if (_glitchTimer > 0f && _glitchTimer < 0.12f)
        {
            for (int i = 0; i < 3; i++)
            {
                int height = _random.Next(2, 14);
                int y = _random.Next(screen.Height - height);
                int shift = _random.Next(-24, 25);
                batch.Draw(_scene, new Rectangle(shift, y, screen.Width, height), new Rectangle(0, y, screen.Width, height), Color.White);
                batch.Draw(_assets.Noise, new Rectangle(0, y, screen.Width, height), new Rectangle(_random.Next(200), _random.Next(200), screen.Width / 3, height), Color.White * 0.15f);
            }
        }
        else if (_glitchTimer <= 0f)
        {
            // Average delay between glitches shrinks as VideoNoise grows.
            _glitchTimer = 0.12f + (float)_random.NextDouble() * 6f / MathF.Max(0.05f, _settings.VideoNoise);
        }
    }

    private void Ensure(ref RenderTarget2D? target, int width, int height)
    {
        if (target is not null && target.Width == width && target.Height == height) return;
        target?.Dispose();
        target = new RenderTarget2D(_device, width, height, false, SurfaceFormat.Color, DepthFormat.None);
    }

    public void Dispose()
    {
        _scene?.Dispose();
        _lights?.Dispose();
    }
}
