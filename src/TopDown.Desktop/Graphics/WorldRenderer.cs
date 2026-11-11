using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TopDown.Content;
using TopDown.Core.Definitions;
using TopDown.Core.Geometry;
using TopDown.Core.World;
using NVector2 = System.Numerics.Vector2;

namespace TopDown.Desktop.Graphics;

/// <summary>
/// Draws a <see cref="GameWorld"/> from top to bottom layers. It only reads the simulation:
/// positions are interpolated between the last two steps for smooth motion at any frame rate.
/// </summary>
public sealed class WorldRenderer
{
    /// <summary>World units walked per second of leg animation time.</summary>
    private const float LegDistancePerSecond = 48f;
    private readonly AssetCache _assets;
    private readonly ThemeColors _colors;
    private readonly ShadowStyle _shadow;
    private readonly Color _shadowColor;
    private readonly float _fallDuration;

    public WorldRenderer(AssetCache assets, ThemeDefinition theme, FeelSettings feel)
    {
        _assets = assets;
        _fallDuration = feel.FallDuration;
        _colors = new ThemeColors(theme);
        _shadow = theme.Shadow;
        _shadowColor = ColorParser.Parse(theme.Shadow.Color, new Color(0, 0, 0, 100));
    }

    public ThemeColors Colors => _colors;

    public void Draw(SpriteBatch batch, GameWorld world, Camera2D camera, EffectsLayer effects, GoreLayer gore, float alpha, float time,
        Func<Actor, bool> isVisible, Func<Actor, float> flash)
    {
        Rectangle view = camera.VisibleWorld;
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, RasterizerState.CullNone, null, camera.Transform);

        DrawTiles(batch, world, view, TileLayer.Floor, Vector2.Zero, null);
        DrawDecor(batch, world, DecorLayer.Floor);
        effects.DrawFloor(batch, _assets);
        DrawObjectiveZone(batch, world, time);
        DrawItems(batch, world, time);

        if (_shadow.Enabled)
        {
            Vector2 offset = ToXna(_shadow.Offset);
            DrawTiles(batch, world, view, TileLayer.Wall, offset, _shadowColor);
            foreach (Actor actor in world.Actors)
            {
                if (actor.IsAlive && !actor.IsDown && isVisible(actor)) DrawActorBody(batch, world, actor, alpha, time, offset, _shadowColor);
            }
        }

        DrawTiles(batch, world, view, TileLayer.Wall, Vector2.Zero, null);
        DrawProps(batch, world);
        DrawDoors(batch, world, alpha);

        foreach (Actor actor in world.Actors)
        {
            if ((!actor.IsAlive || actor.IsDown) && isVisible(actor)) DrawFallen(batch, actor, alpha, gore);
        }

        foreach (WorldWeapon weapon in world.Weapons)
        {
            if (!weapon.IsFlying) DrawWorldWeapon(batch, weapon, alpha);
        }

        foreach (Actor actor in world.Actors)
        {
            if (actor.IsAlive && !actor.IsDown && isVisible(actor)) DrawStanding(batch, world, actor, alpha, time);
        }

        foreach (WorldWeapon weapon in world.Weapons)
        {
            if (weapon.IsFlying) DrawWorldWeapon(batch, weapon, alpha);
        }

        DrawExecutionMarker(batch, world, time);
        DrawProjectiles(batch, world, alpha);
        effects.DrawTop(batch, _assets);
        DrawDecor(batch, world, DecorLayer.Top);
        batch.End();

        batch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, null, RasterizerState.CullNone, null, camera.Transform);
        effects.DrawAdditive(batch, _assets);
        DrawHitFlashes(batch, world, alpha, isVisible, flash);
        batch.End();
    }

    /// <summary>Characters just hit flash white for a few frames (additive pass).</summary>
    private void DrawHitFlashes(SpriteBatch batch, GameWorld world, float alpha, Func<Actor, bool> isVisible, Func<Actor, float> flash)
    {
        foreach (Actor actor in world.Actors)
        {
            float amount = flash(actor);
            if (amount <= 0f || !isVisible(actor)) continue;
            Vector2 position = ToXna(NVector2.Lerp(actor.PreviousPosition, actor.Position, alpha));
            bool fallen = !actor.IsAlive || actor.IsDown;
            SpriteFrame frame = _assets.Sprite(fallen ? FallenSprite(actor) : actor.Definition.Sprites.Body);
            batch.Draw(frame.Texture, position, frame.Source, Color.White * amount, actor.Facing, frame.Origin, fallen ? FallScale(actor) : 1f, SpriteEffects.None, 0f);
        }
    }

    /// <summary>Discreet marker on the target the player can finish off right now.</summary>
    private void DrawExecutionMarker(SpriteBatch batch, GameWorld world, float time)
    {
        Actor player = world.Player;
        if (!player.CanAct || !world.Rules.Execution.Enabled || world.Combat.FindExecutionTarget(player) is not { } target) return;
        Vector2 center = ToXna(target.Position);
        float size = target.Radius + 7f + MathF.Sin(time * 10f) * 1.5f;
        Color color = _colors.Accent * 0.9f;
        for (int i = 0; i < 4; i++)
        {
            float angle = time * 1.5f + i * MathF.PI / 2f;
            Vector2 corner = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * size;
            Vector2 along = new Vector2(MathF.Cos(angle + 2.35f), MathF.Sin(angle + 2.35f)) * 5f;
            Vector2 across = new Vector2(MathF.Cos(angle - 2.35f), MathF.Sin(angle - 2.35f)) * 5f;
            Primitives.Line(batch, _assets.Pixel, corner, corner + along, color, 2f);
            Primitives.Line(batch, _assets.Pixel, corner, corner + across, color, 2f);
        }
    }

    private void DrawTiles(SpriteBatch batch, GameWorld world, Rectangle view, TileLayer layer, Vector2 offset, Color? overrideColor)
    {
        TileMap map = world.Map;
        float size = map.TileSize;
        int minX = Math.Max(0, (int)(view.Left / size));
        int minY = Math.Max(0, (int)(view.Top / size));
        int maxX = Math.Min(map.Width - 1, (int)(view.Right / size));
        int maxY = Math.Min(map.Height - 1, (int)(view.Bottom / size));
        Color tint = overrideColor ?? (layer == TileLayer.Wall ? _colors.Wall : _colors.Floor);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                TileDefinition tile = map.Get(x, y);
                if (tile.Layer != layer || string.IsNullOrEmpty(tile.SpriteId)) continue;
                SpriteFrame frame = _assets.Sprite(tile.SpriteId);
                var destination = new Rectangle((int)(x * size + offset.X), (int)(y * size + offset.Y), (int)size, (int)size);
                batch.Draw(frame.Texture, destination, frame.Source, tint);
            }
        }
    }

    private void DrawDecor(SpriteBatch batch, GameWorld world, DecorLayer layer)
    {
        foreach (LevelDecor decor in world.Level.Decor)
        {
            if (decor.Layer != layer) continue;
            SpriteFrame frame = _assets.Sprite(decor.Sprite);
            Vector2 position = ToXna(world.Level.ToWorld(decor.Position));
            batch.Draw(frame.Texture, position, frame.Source, _colors.Decor, Geo.DegToRad(decor.RotationDegrees), frame.Origin, decor.Scale, SpriteEffects.None, 0f);
        }
    }

    private void DrawProps(SpriteBatch batch, GameWorld world)
    {
        foreach (Prop prop in world.Props)
        {
            SpriteFrame frame = _assets.Sprite(prop.Definition.Sprite);
            Vector2 center = ToXna(prop.Bounds.Center);
            if (_shadow.Enabled)
                batch.Draw(frame.Texture, center + ToXna(_shadow.Offset), frame.Source, _shadowColor, Geo.DegToRad(prop.Definition.RotationDegrees), frame.Origin, 1f, SpriteEffects.None, 0f);

            batch.Draw(frame.Texture, center, frame.Source, _colors.Decor, Geo.DegToRad(prop.Definition.RotationDegrees), frame.Origin, 1f, SpriteEffects.None, 0f);
        }
    }

    private void DrawItems(SpriteBatch batch, GameWorld world, float time)
    {
        foreach (ItemPickup item in world.Items)
        {
            if (item.Collected) continue;
            SpriteFrame frame = _assets.Sprite(item.Definition.Sprite);
            float bob = 1f + MathF.Sin(time * 4f) * 0.08f;
            batch.Draw(frame.Texture, ToXna(item.Position), frame.Source, _colors.Weapon, 0f, frame.Origin, bob, SpriteEffects.None, 0f);
        }

        foreach (Interactable interactable in world.Interactables)
        {
            SpriteFrame frame = _assets.Sprite(interactable.Definition.Sprite);
            Color color = interactable.Available ? _colors.Decor : _colors.Decor * 0.5f;
            batch.Draw(frame.Texture, ToXna(interactable.Position), frame.Source, color, 0f, frame.Origin, 1f, SpriteEffects.None, 0f);
        }
    }

    /// <summary>Pulsing outline and arrow on the zone the player must reach.</summary>
    private void DrawObjectiveZone(SpriteBatch batch, GameWorld world, float time)
    {
        if (world.Objectives.Current is not { Type: ObjectiveType.ReachZone, Zone: { } zoneName }) return;
        if (!world.Zones.TryGetValue(zoneName, out RectF zone)) return;
        float pulse = 0.5f + 0.5f * MathF.Sin(time * 5f);
        Color color = _colors.Accent * (0.35f + 0.45f * pulse);
        Primitives.Rect(batch, _assets.Pixel, new Vector2(zone.X + 2f, zone.Y + 2f), new Vector2(zone.Width - 4f, zone.Height - 4f), color, 2f);

        SpriteFrame arrow = _assets.Sprite(EffectSprites.Arrow);
        Vector2 center = ToXna(zone.Center);
        batch.Draw(arrow.Texture, center + new Vector2(0f, -6f - pulse * 6f), arrow.Source, color, MathF.PI / 2f, arrow.Origin, 1.5f, SpriteEffects.None, 0f);
    }

    private void DrawDoors(SpriteBatch batch, GameWorld world, float alpha)
    {
        foreach (Door door in world.Doors)
        {
            SpriteFrame frame = _assets.Sprite(door.Definition.SpriteId);
            float angle = door.ClosedAngle + Lerp(door.PreviousAngle, door.Angle, alpha);
            var scale = new Vector2(door.Length / frame.Source.Width, door.Definition.Thickness / frame.Source.Height);
            var origin = new Vector2(0f, frame.Source.Height * 0.5f);
            Color tint = door.Locked ? _colors.Wall : _colors.Decor;
            if (_shadow.Enabled)
                batch.Draw(frame.Texture, ToXna(door.Hinge) + ToXna(_shadow.Offset), frame.Source, _shadowColor, angle, origin, scale, SpriteEffects.None, 0f);
            batch.Draw(frame.Texture, ToXna(door.Hinge), frame.Source, tint, angle, origin, scale, SpriteEffects.None, 0f);
        }
    }

    private void DrawWorldWeapon(SpriteBatch batch, WorldWeapon weapon, float alpha)
    {
        SpriteFrame frame = _assets.Sprite(weapon.Weapon.Definition.SpriteId);
        Vector2 position = ToXna(NVector2.Lerp(weapon.PreviousPosition, weapon.Position, alpha));
        Color tint = weapon.Weapon.IsEmpty ? _colors.Weapon * 0.55f : _colors.Weapon;
        batch.Draw(frame.Texture, position, frame.Source, tint, weapon.Rotation, frame.Origin, 1f, SpriteEffects.None, 0f);
    }

    private void DrawFallen(SpriteBatch batch, Actor actor, float alpha, GoreLayer gore)
    {
        Vector2 position = ToXna(NVector2.Lerp(actor.PreviousPosition, actor.Position, alpha));
        float wobble = actor.State switch
        {
            ActorState.GettingUp => MathF.Sin(actor.StateTimer * 30f) * 0.1f,
            ActorState.BeingExecuted => MathF.Sin(actor.StateElapsed * 45f) * 0.04f,
            _ => 0f,
        };

        // Under an execution: the body jolts with each blow, the part being pulled stretches out.
        int pulled = -1;
        float tug = 0f;
        if (actor.State == ActorState.BeingExecuted && actor.Executioner is { } executioner)
        {
            float jolt = Fade(SinceLastBlow(executioner), JoltTime);
            position += SafeNormalize(ToXna(ExecutionPose.HeadOf(actor) - executioner.Position)) * (JoltDistance * jolt);
            pulled = gore.NextPart(actor, executioner.ExecutionAnimation);
            tug = 1.2f + 2.8f * Fade(SinceLastBlow(executioner), 0.18f) + MathF.Sin(actor.StateElapsed * 40f) * 0.4f;
        }

        float rotation = actor.Facing + wobble;
        float scale = FallScale(actor);
        ActorSprites sprites = actor.Definition.Sprites;
        BodyDamage? damage = gore.Damage(actor);
        bool inPieces = sprites.Torso is not null && sprites.Parts.Count > 0 && (damage?.Torn.Count > 0 || pulled >= 0);
        if (!inPieces)
        {
            SpriteFrame frame = _assets.Sprite(FallenSprite(actor));
            batch.Draw(frame.Texture, position, frame.Source, _colors.Actor, rotation, frame.Origin, scale, SpriteEffects.None, 0f);
        }
        else
        {
            DrawParts(batch, actor, damage, position, rotation, scale, over: false, pulled, tug);
            SpriteFrame torso = _assets.Sprite(sprites.Torso);
            batch.Draw(torso.Texture, position, torso.Source, _colors.Actor, rotation, torso.Origin, scale, SpriteEffects.None, 0f);
            DrawParts(batch, actor, damage, position, rotation, scale, over: true, pulled, tug);
            if (damage is not null) DrawStumps(batch, actor, damage, position, rotation, scale);
        }

        // A head beaten in: more and more gore, burst on the last blow.
        if (damage is { HeadWounds: > 0f })
        {
            SpriteFrame frame = _assets.Sprite(EffectSprites.Gore);
            Vector2 head = position + Rotate(new Vector2(actor.Radius * ExecutionPose.HeadDistance * scale, 0f), rotation);
            float size = (0.45f + 0.55f * damage.HeadWounds) * scale;
            batch.Draw(frame.Texture, head, frame.Source, Blood(actor), rotation, frame.Origin, size, SpriteEffects.None, 0f);
        }
    }

    /// <summary>Body parts still attached, under or over the torso. The pulled one stretches out.</summary>
    private void DrawParts(SpriteBatch batch, Actor actor, BodyDamage? damage, Vector2 position, float rotation, float scale, bool over, int pulled, float tug)
    {
        ActorSprites sprites = actor.Definition.Sprites;
        for (int i = 0; i < sprites.Parts.Count; i++)
        {
            BodyPart part = sprites.Parts[i];
            if (part.Over != over || damage?.Torn.Contains(i) == true) continue;
            Vector2 offset = Vector2.Zero;
            if (i == pulled)
            {
                PartShape shape = _assets.PartShape(part.Sprite, sprites.Torso!);
                offset = SafeNormalize(shape.Center - shape.Joint) * tug;
            }

            SpriteFrame frame = _assets.Sprite(part.Sprite);
            batch.Draw(frame.Texture, position + Rotate(offset * scale, rotation), frame.Source, _colors.Actor, rotation, frame.Origin, scale, SpriteEffects.None, 0f);
        }
    }

    /// <summary>Bloody stumps where parts were torn off.</summary>
    private void DrawStumps(SpriteBatch batch, Actor actor, BodyDamage damage, Vector2 position, float rotation, float scale)
    {
        ActorSprites sprites = actor.Definition.Sprites;
        SpriteFrame drop = _assets.Sprite(EffectSprites.BloodDrop);
        SpriteFrame gib = _assets.Sprite(EffectSprites.Gib);
        foreach (int i in damage.Torn)
        {
            PartShape shape = _assets.PartShape(sprites.Parts[i].Sprite, sprites.Torso!);
            Vector2 joint = position + Rotate(shape.Joint * scale, rotation);
            batch.Draw(drop.Texture, joint, drop.Source, Blood(actor), rotation, drop.Origin, 2.4f * scale, SpriteEffects.None, 0f);
            batch.Draw(gib.Texture, joint, gib.Source, Blood(actor), rotation + i, gib.Origin, 0.8f * scale, SpriteEffects.None, 0f);
        }
    }

    private Color Blood(Actor actor) => ColorParser.Parse(actor.Definition.Sprites.BloodTint, Color.White).MultiplyBy(_colors.Blood);

    private static string FallenSprite(Actor actor) =>
        (actor.IsAlive ? actor.Definition.Sprites.Down : actor.Definition.Sprites.Dead)
        ?? actor.Definition.Sprites.Down ?? actor.Definition.Sprites.Body;

    /// <summary>A body slams to the floor: slightly bigger for a few frames, then settles.</summary>
    private float FallScale(Actor actor)
    {
        bool justFell = actor.State is ActorState.KnockedDown or ActorState.Dead;
        if (!justFell || _fallDuration <= 0f || actor.StateElapsed >= _fallDuration) return 1f;
        float t = actor.StateElapsed / _fallDuration;
        return 1f + 0.18f * (1f - t);
    }

    private void DrawStanding(SpriteBatch batch, GameWorld world, Actor actor, float alpha, float time)
    {
        Vector2 position = ToXna(NVector2.Lerp(actor.PreviousPosition, actor.Position, alpha));
        if (actor.Definition.Sprites.Legs is { } legs && actor.Velocity.LengthSquared() > 25f)
        {
            SpriteFrame legFrame = _assets.Sprite(legs, LegAnimationTime(actor));
            float legAngle = MathF.Atan2(actor.Velocity.Y, actor.Velocity.X);
            batch.Draw(legFrame.Texture, position, legFrame.Source, _colors.Actor, legAngle, legFrame.Origin, 1f, SpriteEffects.None, 0f);
        }

        DrawActorBody(batch, world, actor, alpha, time, Vector2.Zero, null);
    }

    /// <summary>Held weapon then body. Also used for the drop shadow (with a flat colour).</summary>
    private void DrawActorBody(SpriteBatch batch, GameWorld world, Actor actor, float alpha, float time, Vector2 offset, Color? flatColor)
    {
        Vector2 position = ToXna(NVector2.Lerp(actor.PreviousPosition, actor.Position, alpha)) + offset;
        float facing = actor.PreviousFacing + Geo.AngleDifference(actor.PreviousFacing, actor.Facing) * alpha;
        Vector2 forward = new(MathF.Cos(facing), MathF.Sin(facing));
        Vector2 right = new(-forward.Y, forward.X);
        if (actor.State == ActorState.Executing)
        {
            DrawExecutioner(batch, world, actor, time, position, facing, flatColor);
            return;
        }

        float swing = 0f;
        float lunge = 0f;
        if (actor.CurrentAttack is { } attack && attack.AttackDuration > 0f)
        {
            // Fast swing ending at the hit, then a short follow-through. Alternates sides (left/right punches).
            float side = actor.AttackCount % 2 == 0 ? 1f : -1f;
            float t = Math.Clamp(actor.AttackElapsed / MathF.Max(attack.HitDelay, 0.01f), 0f, 1f);
            float after = Math.Clamp((actor.AttackElapsed - attack.HitDelay) / MathF.Max(attack.AttackDuration - attack.HitDelay, 0.01f), 0f, 1f);
            swing = side * ((t * 2f - 1f) + after * 0.25f) * Geo.DegToRad(attack.ArcDegrees) * 0.5f;
            lunge = (t < 1f ? t : 1f - after) * 5f;
        }

        // Actor timings use the simulation clock (pauses and hitstops included).
        float sinceAttack = world.Time - actor.LastAttackTime;
        float recoil = actor.Weapon is { Definition.IsFirearm: true } && sinceAttack < 0.08f ? -2.5f : 0f;
        Vector2 bodyPosition = position + forward * (lunge + recoil);

        WeaponDefinition? held = actor.Weapon?.Definition;
        if (held is not null)
        {
            SpriteFrame weaponFrame = _assets.Sprite(held.SpriteId);
            float weaponAngle = facing + swing;
            Vector2 hold = forward * held.HoldOffset.X + right * held.HoldOffset.Y;
            if (swing != 0f) hold = Rotate(hold, swing);
            batch.Draw(weaponFrame.Texture, bodyPosition + hold, weaponFrame.Source, flatColor ?? _colors.Weapon, weaponAngle, weaponFrame.Origin, 1f, SpriteEffects.None, 0f);
        }

        SpriteFrame body = _assets.Sprite(actor.Definition.Sprites.Body, time);
        float stunWobble = actor.State == ActorState.Stunned ? MathF.Sin(time * 40f) * 0.15f : 0f;
        batch.Draw(body.Texture, bodyPosition, body.Source, flatColor ?? _colors.Actor, facing + stunWobble, body.Origin, 1f, SpriteEffects.None, 0f);
    }

    /// <summary>
    /// An executioner at work. Astride: leans back to lift, slams forward on each blow (or heaves
    /// backwards to tear). Standing: kicks, weapon raised then brought down, point-blank shot, bite.
    /// </summary>
    private void DrawExecutioner(SpriteBatch batch, GameWorld world, Actor actor, float time, Vector2 position, float facing, Color? flatColor)
    {
        Vector2 forward = new(MathF.Cos(facing), MathF.Sin(facing));
        Vector2 right = new(-forward.Y, forward.X);
        ExecutionLook look = LookOf(actor, time);
        float sinceAttack = world.Time - actor.LastAttackTime;
        float recoil = actor.Weapon is { Definition.IsFirearm: true } && sinceAttack < 0.08f ? -2.5f : 0f;
        Vector2 bodyPosition = position + forward * (look.Lean + recoil);

        if (look.Legs is { } legs && flatColor is null)
        {
            SpriteFrame legFrame = _assets.Sprite(legs);
            batch.Draw(legFrame.Texture, position, legFrame.Source, _colors.Actor, facing, legFrame.Origin, 1f, SpriteEffects.None, 0f);
        }

        if (actor.Weapon?.Definition is { } held)
        {
            SpriteFrame weaponFrame = _assets.Sprite(held.SpriteId);
            Vector2 hold = look.Astride
                ? forward * (AstrideHandReach + look.Reach) + right * 2f
                : forward * (held.HoldOffset.X + look.Reach) + right * held.HoldOffset.Y;
            hold = Rotate(hold, look.Swing);
            batch.Draw(weaponFrame.Texture, bodyPosition + hold, weaponFrame.Source, flatColor ?? _colors.Weapon, facing + look.Swing,
                weaponFrame.Origin, 1f, SpriteEffects.None, 0f);
        }

        SpriteFrame body = _assets.Sprite(look.Sprite, time);
        batch.Draw(body.Texture, bodyPosition, body.Source, flatColor ?? _colors.Actor, facing, body.Origin, look.Scale, SpriteEffects.None, 0f);
    }

    /// <summary>Sprite and offsets of an executioner's body, from the timing of its blows.</summary>
    private static ExecutionLook LookOf(Actor actor, float time)
    {
        ActorSprites sprites = actor.Definition.Sprites;
        string mount = sprites.Mount ?? sprites.Body;
        float since = SinceLastBlow(actor);
        // 1 = ready for the next blow (arm raised, head lifted), 0 = the blow just landed.
        float raise = actor.ExecutionStrikesDone == 0
            ? Smooth(actor.NextStrikeAt > 0f ? actor.ExecutionElapsed / actor.NextStrikeAt : 1f)
            : since < ImpactHold ? 0f : Smooth((since - ImpactHold) / RaiseTime);
        float jerk = Fade(since, 0.18f);

        switch (actor.ExecutionAnimation)
        {
            case ExecutionAnimation.HeadBash:
                return new ExecutionLook(mount, 2.5f - 4f * raise, 1f + 0.07f * raise, 0f, 0f, null, true);
            case ExecutionAnimation.Stab:
                return new ExecutionLook(mount, 2f - 3f * raise, 1f + 0.05f * raise, 0f, 4f - 6f * raise, null, true);
            case ExecutionAnimation.TearHead:
            case ExecutionAnimation.TearLegs:
                float strain = MathF.Sin(time * 38f) * 0.5f * (1f - jerk);
                return new ExecutionLook(mount, 0.8f - 4.3f * jerk + strain, 1f + 0.05f * jerk, 0f, 0f, null, true);
            case ExecutionAnimation.Stomp:
                float kick = Fade(since, 0.14f);
                return new ExecutionLook(sprites.Body, -2.5f * kick + 1f * (1f - raise), 1f, 0f, 0f, kick > 0.2f ? sprites.Kick ?? sprites.Legs : null, false);
            case ExecutionAnimation.Bite:
                return new ExecutionLook(sprites.Body, 6f * (1f - raise), 1f, 0f, 0f, null, false);
            case ExecutionAnimation.Shoot:
                return new ExecutionLook(sprites.Body, 0f, 1f, 0f, 0f, null, false);
            default:
                // Weapon raised behind the shoulder, brought down on the head.
                return new ExecutionLook(sprites.Body, 3f * (1f - raise), 1f, -1.9f * raise, 0f, null, false);
        }
    }

    /// <summary>Time since the executioner's last blow (very large before the first one).</summary>
    private static float SinceLastBlow(Actor executioner) =>
        executioner.ExecutionStrikesDone > 0 ? executioner.ExecutionElapsed - executioner.LastStrikeAt : float.MaxValue;

    /// <summary>1 right after an event, 0 after <paramref name="duration"/>.</summary>
    private static float Fade(float since, float duration) => since < duration ? 1f - since / duration : 0f;

    private static float Smooth(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static Vector2 SafeNormalize(Vector2 v) => v.LengthSquared() > 0.0001f ? Vector2.Normalize(v) : Vector2.Zero;

    /// <summary>How an executioner looks right now.</summary>
    /// <param name="Lean">Offset of the body along the facing (forward on a blow).</param>
    /// <param name="Swing">Weapon rotation from the facing.</param>
    /// <param name="Reach">Extra forward offset of the weapon.</param>
    /// <param name="Legs">Legs drawn under the body (kick), if any.</param>
    private readonly record struct ExecutionLook(string Sprite, float Lean, float Scale, float Swing, float Reach, string? Legs, bool Astride);

    private const float ImpactHold = 0.05f;
    private const float RaiseTime = 0.16f;
    private const float JoltTime = 0.12f;
    private const float JoltDistance = 1.8f;
    private const float AstrideHandReach = 9f;

    private void DrawProjectiles(SpriteBatch batch, GameWorld world, float alpha)
    {
        foreach (Projectile projectile in world.Projectiles)
        {
            string sprite = string.IsNullOrEmpty(projectile.Weapon.ProjectileSpriteId) ? EffectSprites.Bullet : projectile.Weapon.ProjectileSpriteId;
            SpriteFrame frame = _assets.Sprite(sprite);
            Vector2 position = ToXna(NVector2.Lerp(projectile.PreviousPosition, projectile.Position, alpha));
            float angle = MathF.Atan2(projectile.Velocity.Y, projectile.Velocity.X);
            batch.Draw(frame.Texture, position, frame.Source, _colors.Fx, angle, frame.Origin, 1f, SpriteEffects.None, 0f);
        }
    }

    /// <summary>Legs animate with the distance walked, not with time, so feet never slide.</summary>
    public static float LegAnimationTime(Actor actor) => actor.DistanceWalked / LegDistancePerSecond;

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static Vector2 Rotate(Vector2 v, float angle)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    public static Vector2 ToXna(NVector2 v) => new(v.X, v.Y);
}

/// <summary>Theme colours parsed once.</summary>
public sealed class ThemeColors
{
    public ThemeColors(ThemeDefinition theme)
    {
        Color Tint(string role) => ColorParser.Parse(theme.Tints.GetValueOrDefault(role), Color.White);
        Background = ColorParser.Parse(theme.Background, Color.Black);
        Floor = Tint("floor");
        Wall = Tint("wall");
        Actor = Tint("actor");
        Weapon = Tint("weapon");
        Decor = Tint("decor");
        Fx = Tint("fx");
        Blood = Tint("blood");
        Text = ColorParser.Parse(theme.Ui.Text, Color.White);
        Accent = ColorParser.Parse(theme.Ui.Accent, Color.HotPink);
        Secondary = ColorParser.Parse(theme.Ui.Secondary, Color.Cyan);
        Danger = ColorParser.Parse(theme.Ui.Danger, Color.Red);
        Panel = ColorParser.Parse(theme.Ui.Panel, Color.Black * 0.6f);
        Shadow = ColorParser.Parse(theme.Ui.Shadow, Color.Black);
    }

    public Color Background { get; }
    public Color Floor { get; }
    public Color Wall { get; }
    public Color Actor { get; }
    public Color Weapon { get; }
    public Color Decor { get; }
    public Color Fx { get; }
    public Color Blood { get; }
    public Color Text { get; }
    public Color Accent { get; }
    public Color Secondary { get; }
    public Color Danger { get; }
    public Color Panel { get; }
    public Color Shadow { get; }
}
