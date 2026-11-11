using Microsoft.Xna.Framework.Graphics;

namespace Game.Desktop.Screens;

/// <summary>One state of the application (menu, battle, debriefing). The host shows one at a time.</summary>
public abstract class Screen : IDisposable
{
    protected Screen(GameHost host) => Host = host;

    protected GameHost Host { get; }
    protected GameSession Session => Host.Session;

    public abstract void Update(float dt);
    public abstract void Draw(SpriteBatch batch);

    public virtual void Dispose()
    {
    }
}
