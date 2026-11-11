using Game.Core.Battle;
using Game.Core.Common;
using Game.Core.Definitions;
using Game.Desktop.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Desktop.Tactical;

/// <summary>
/// Draws the battle map in isometric view from the real state of the simulation and what the
/// player's side is allowed to see: never-seen tiles stay black, seen-but-not-in-view tiles are
/// dimmed, hostile units appear only while spotted. Painter's order: level by level, then
/// back-to-front diagonals; on each tile floor, walls, object, items, unit.
/// </summary>
public sealed class BattleRenderer
{
    private const int ArrowSize = 16;
    private readonly GameSession _session;
    private readonly Vector2 _mapAnchor;

    public BattleRenderer(GameSession session)
    {
        _session = session;
        int[] anchor = session.Content.Game.Ui.MapSpriteAnchor;
        _mapAnchor = anchor.Length == 2 ? new Vector2(anchor[0], anchor[1]) : new Vector2(32, 48);
    }

    public void Draw(SpriteBatch batch, Battle battle, BattleView view, BattlePlayback playback, float time)
    {
        BattleMap map = battle.Map;
        batch.Begin(transformMatrix: view.Camera.Transform, samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);
        for (int z = 0; z <= Math.Min(view.Level, map.Height - 1); z++)
        {
            for (int diagonal = 0; diagonal <= map.Width + map.Length - 2; diagonal++)
            {
                for (int x = Math.Max(0, diagonal - map.Length + 1); x <= Math.Min(diagonal, map.Width - 1); x++)
                {
                    int y = diagonal - x;
                    DrawTile(batch, battle, view, playback, map[x, y, z]!);
                }
            }
        }

        DrawOverlays(batch, battle, view, playback, time);
        batch.End();
    }

    private void DrawTile(SpriteBatch batch, Battle battle, BattleView view, BattlePlayback playback, Tile tile)
    {
        Position p = tile.Position;
        bool discovered = view.RevealAll || tile.IsDiscovered(Faction.Player);
        bool westSeen = discovered || IsDiscovered(battle, view, p.Offset(-1, 0));
        bool northSeen = discovered || IsDiscovered(battle, view, p.Offset(0, -1));
        Color shade = Shade(battle, view, p);
        Vector2 top = Iso.TileTop(p);

        if (discovered && tile.Floor is { } floor) Sprite(batch, floor.Sprite, top, shade);
        if (westSeen && tile.WestWall is { } west) Sprite(batch, west.Sprite, top, westSeen && !discovered ? Shade(battle, view, p.Offset(-1, 0)) : shade);
        if (northSeen && tile.NorthWall is { } north) Sprite(batch, north.Sprite, top, northSeen && !discovered ? Shade(battle, view, p.Offset(0, -1)) : shade);
        if (!discovered) return;
        if (tile.Object is { } obj) Sprite(batch, obj.Sprite, top, shade);

        if (tile.Zone is { } zone && zone == battle.Mission.Objective.Zone) Sprite(batch, "ui.zone", top, Color.White);
        int shown = 0;
        foreach (BattleItem item in tile.Items)
        {
            if (shown++ >= 3) break;
            Sprite(batch, item.Definition.FloorSprite, top + new Vector2(0, -2 * (shown - 1)), shade);
        }

        // A walking unit is drawn on top by the overlay pass, between its two tiles.
        if (tile.Unit is { IsActive: true } unit && playback.Walk?.UnitId != unit.Id && IsShown(battle, view, unit))
            DrawUnit(batch, battle, unit, BattlePlayback.FeetWorld(battle, p), shade);
    }

    private static bool IsDiscovered(Battle battle, BattleView view, Position p) =>
        view.RevealAll || (battle.Map[p]?.IsDiscovered(Faction.Player) ?? false);

    public static bool IsShown(Battle battle, BattleView view, BattleUnit unit) =>
        view.RevealAll || unit.Faction == Faction.Player || battle.IsSpottedBy(Faction.Player, unit);

    /// <summary>Lit tiles in view are bright; remembered tiles are dimmed.</summary>
    private static Color Shade(Battle battle, BattleView view, Position p)
    {
        float light = 0.42f + 0.58f * battle.LightAt(p) / 15f;
        if (!view.RevealAll && !battle.IsTileVisible(Faction.Player, p)) light *= 0.55f;
        return new Color(light, light, light * 1.04f);
    }

    public void DrawUnit(SpriteBatch batch, Battle battle, BattleUnit unit, Vector2 feet, Color shade)
    {
        int frame = unit.Direction + (unit.Kneeling ? 8 : 0);
        SpriteFrame sprite = _session.Assets.Sprite(unit.Armor.Sprite, frame);
        // Unit sheets are drawn with their feet (tile centre) on the anchor's tile centre.
        Vector2 top = feet - new Vector2(0, Iso.TileHeight / 2f);
        Vector2 anchor = sprite.HasAnchor ? sprite.Anchor : _mapAnchor;
        batch.Draw(sprite.Texture, top, sprite.Source, shade, 0f, anchor, 1f, SpriteEffects.None, 0f);
    }

    private void Sprite(SpriteBatch batch, string id, Vector2 tileTop, Color color)
    {
        if (string.IsNullOrEmpty(id)) return;
        SpriteFrame sprite = _session.Assets.Sprite(id);
        Vector2 anchor = sprite.HasAnchor ? sprite.Anchor : _mapAnchor;
        batch.Draw(sprite.Texture, tileTop, sprite.Source, color, 0f, anchor, 1f, SpriteEffects.None, 0f);
    }

    private void DrawOverlays(SpriteBatch batch, Battle battle, BattleView view, BattlePlayback playback, float time)
    {
        // Path preview: green while affordable, orange beyond the unit's TU.
        if (view.Path is { Found: true } path && view.Selected is { } walker && view.Targeting is null)
        {
            for (int i = 0; i < path.Steps.Count; i++)
            {
                Position step = path.Steps[i].To;
                if (step.Z > view.Level) continue;
                string marker = path.CostAfter(i + 1) <= walker.Tu ? "ui.path" : "ui.path_short";
                Sprite(batch, marker, Iso.TileTop(step) - new Vector2(0, battle.Map.StandElevation(step) * 2f), Color.White);
            }
        }

        if (view.Hovered is { } hovered)
        {
            string cursor = view.Targeting is not null ? "ui.cursor_target" : "ui.cursor";
            Sprite(batch, cursor, Iso.TileTop(hovered) - new Vector2(0, battle.Map.StandElevation(hovered) * 2f), Color.White);
        }

        if (playback.Walk is { } walk && battle.GetUnit(walk.UnitId) is { } moving && IsShown(battle, view, moving))
            DrawUnit(batch, battle, moving, playback.WalkPosition(walk.UnitId)!.Value, Color.White);

        if (view.Selected is { IsActive: true } selected && selected.Position.Z <= view.Level)
        {
            Vector2 feet = playback.WalkPosition(selected.Id) ?? BattlePlayback.FeetWorld(battle, selected.Position);
            float bob = MathF.Sin(time * 5f) * 2f;
            SpriteFrame arrow = _session.Assets.Sprite("ui.arrow");
            batch.Draw(arrow.Texture, feet - new Vector2(0, selected.Height * 2f + 18f + bob), arrow.Source, Color.White, 0f,
                new Vector2(arrow.Source.Width / 2f, arrow.Source.Height), ArrowSize / (float)Math.Max(1, arrow.Source.Width), SpriteEffects.None, 0f);
        }

        if (playback.Projectile is { Sprite: { } projectileSprite } projectile && playback.ProjectilePosition is { } position)
        {
            SpriteFrame sprite = _session.Assets.Sprite(projectileSprite);
            Vector2 delta = projectile.To - projectile.From;
            float angle = MathF.Atan2(delta.Y, delta.X);
            batch.Draw(sprite.Texture, position, sprite.Source, Color.White, angle, new Vector2(sprite.Source.Width / 2f, sprite.Source.Height / 2f), 1f, SpriteEffects.None, 0f);
        }

        if (playback.Flash is { } flash)
        {
            int frames = _session.Assets.FrameCount("fx.explosion");
            int frame = Math.Clamp((int)((1f - flash.Time / 0.15f) * frames), 0, frames - 1);
            SpriteFrame boom = _session.Assets.Sprite("fx.explosion", frame);
            batch.Draw(boom.Texture, flash.Position, boom.Source, Color.White * 0.9f, 0f, boom.Anchor, 0.35f, SpriteEffects.None, 0f);
        }
    }
}
