using Microsoft.Xna.Framework.Graphics;

namespace TopDown.Desktop.Screens;

/// <summary>One state of the application (menu, gameplay…). The host shows one at a time.</summary>
public abstract class Screen : IDisposable
{
    protected Screen(GameHost host) => Host = host;

    protected GameHost Host { get; }
    protected GameSession Session => Host.Session;

    /// <summary>Shows the operating system cursor (menus) or hides it (gameplay crosshair).</summary>
    public virtual bool ShowSystemCursor => true;

    public abstract void Update(float dt);
    public abstract void Draw(SpriteBatch batch, float dt);

    public virtual void Dispose()
    {
    }
}
