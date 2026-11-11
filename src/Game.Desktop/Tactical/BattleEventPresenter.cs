using Game.Core.Battle;
using Game.Core.Common;
using Game.Core.Content;
using Game.Core.Definitions;
using Game.Desktop.Graphics;
using Microsoft.Xna.Framework;

namespace Game.Desktop.Tactical;

/// <summary>
/// Turns simulation events into what the player sees and hears: animations, sounds, messages,
/// camera moves. It reads the battle but never changes it.
/// </summary>
public sealed class BattleEventPresenter(
    GameSession session, UserSettings settings, Battle battle, BattleView view, BattlePlayback playback, BattleHud hud, Action selectNext)
{
    private readonly GameSession _session = session;
    private readonly UserSettings _settings = settings;
    private readonly Battle _battle = battle;
    private readonly BattleView _view = view;
    private readonly BattlePlayback _playback = playback;
    private readonly BattleHud _hud = hud;
    private readonly Action _selectNext = selectNext;

    private Localizer Texts => _session.Texts;

    public void Play(BattleEvent battleEvent)
    {
        GameSounds sounds = _session.Content.Game.Sounds;
        switch (battleEvent)
        {
            case UnitMoved moved when _battle.GetUnit(moved.UnitId) is { } unit && BattleRenderer.IsShown(_battle, _view, unit):
                _playback.PlayWalk(new WalkAnimation(unit.Id, BattlePlayback.FeetWorld(_battle, moved.From), BattlePlayback.FeetWorld(_battle, moved.To)), _settings.AnimationStep);
                _session.Audio.Play(sounds.Footstep, Pan(moved.To), 0.6f);
                if (unit == _view.Selected || unit.Faction != Faction.Player) Follow(moved.To);
                if (moved.To.Z > _view.Level && unit.Faction == Faction.Player) _view.Level = moved.To.Z;
                break;
            case ShotFired shot when IsVisible(shot.Origin.Tile) || IsVisible(shot.Impact.Tile):
            {
                BattleItem? weapon = _battle.GetItem(shot.WeaponId);
                Vector2 from = Iso.VoxelToWorld(shot.Origin), to = Iso.VoxelToWorld(shot.Impact);
                float seconds = Math.Clamp(Vector2.Distance(from, to) / 1400f, 0.06f, 0.45f);
                if (shot.Mode == AttackMode.Melee) _playback.Wait(0.2f);
                else _playback.PlayProjectile(new ProjectileAnimation(weapon?.Definition.ProjectileSprite, from, to), seconds);
                _session.Audio.Play(weapon?.Definition.FireSound, Pan(shot.Origin.Tile));
                break;
            }
            case ProjectileHit hit when IsVisible(hit.Impact.Tile):
                _playback.PlayFlash(Iso.VoxelToWorld(hit.Impact));
                _session.Audio.Play(_battle.GetItem(hit.WeaponId)?.Definition.HitSound, Pan(hit.Impact.Tile));
                break;
            case UnitDied died when _battle.GetUnit(died.UnitId) is { } dead:
                _session.Audio.Play(dead.Definition.DeathSound ?? sounds.Death, Pan(dead.Position));
                if (dead == _view.Selected) _selectNext();
                _playback.Wait(0.35f);
                break;
            case UnitKnockedOut knocked when _battle.GetUnit(knocked.UnitId) is { } down:
                if (down == _view.Selected) _selectNext();
                _playback.Wait(0.25f);
                break;
            case ItemThrown thrown when IsVisible(thrown.Origin.Tile) || IsVisible(thrown.Landing.Tile):
            {
                Vector2 from = Iso.VoxelToWorld(thrown.Origin), to = Iso.VoxelToWorld(thrown.Landing);
                float seconds = Math.Clamp(Vector2.Distance(from, to) / 500f, 0.25f, 0.9f);
                _playback.PlayProjectile(new ProjectileAnimation(_battle.GetItem(thrown.ItemId)?.Definition.ProjectileSprite, from, to, Vector2.Distance(from, to) * 0.35f), seconds);
                break;
            }
            case ExplosionStarted explosion:
                if (IsVisible(explosion.Center.Tile)) _playback.PlayExplosion(Iso.VoxelToWorld(explosion.Center), explosion.Radius);
                _session.Audio.Play(sounds.Explosion, Pan(explosion.Center.Tile));
                if (IsVisible(explosion.Center.Tile)) Follow(explosion.Center.Tile);
                break;
            case UnitPanicked panicked when _battle.GetUnit(panicked.UnitId) is { } shaken && BattleRenderer.IsShown(_battle, _view, shaken):
                string panicKey = panicked.State == MoraleState.Berserk ? "info.berserk" : "info.panicked";
                _hud.Show(Texts.Format(panicKey, ("name", UnitNames.Of(shaken, Texts))), 2.5f);
                Follow(shaken.Position);
                _playback.Wait(0.6f);
                break;
            case GrenadePrimed:
                _hud.Show(Texts.Get("info.grenade_primed"), 2f);
                break;
            case DoorOpened opened when IsVisible(opened.Tile):
                _session.Audio.Play(sounds.Door, Pan(opened.Tile));
                break;
            case EnemySpotted { Faction: Faction.Player }:
                _hud.Show(Texts.Get("info.enemy_spotted"), 2f);
                _playback.Wait(0.2f);
                break;
            case ActionInterrupted interrupted when _battle.GetUnit(interrupted.UnitId)?.Faction == Faction.Player && interrupted.ReasonKey != "info.enemy_spotted":
                _hud.Show(Texts.Format(interrupted.ReasonKey, ("value", 0)));
                break;
            case TurnStarted started:
                OnTurnStarted(started);
                break;
            case BattleEnded ended:
                string title = Texts.Get(ended.Outcome.Victory ? "debrief.title_victory" : "debrief.title_defeat");
                _playback.Banner = (title, ended.Outcome.Victory ? _session.Ui.Colors.Good : _session.Ui.Colors.Bad, 2.5f);
                _session.Audio.Play(ended.Outcome.Victory ? sounds.Victory : sounds.Defeat);
                break;
        }
    }

    private void OnTurnStarted(TurnStarted started)
    {
        MissionDefinition mission = _battle.Mission;
        string side = Texts.Get(started.Faction switch
        {
            Faction.Player => mission.PlayerNameKey,
            Faction.Hostile => mission.HostileNameKey,
            _ => mission.NeutralNameKey,
        });
        if (started.Faction == Faction.Player)
        {
            _playback.Banner = ($"{Texts.Format("ui.turn", ("turn", started.Turn))} — {Texts.Format("ui.your_turn", ("side", side))}", _session.Ui.Colors.Friendly, 1.6f);
            if (_view.Selected is not { IsActive: true }) _selectNext();
            _session.Audio.Play(_session.Content.Game.Sounds.TurnStart);
        }
        else if (_battle.ActiveUnitsOf(started.Faction).Any())
        {
            _playback.Banner = (Texts.Format("ui.enemy_turn", ("side", side)), _session.Ui.Colors.Hostile, 1.2f);
        }

        _view.ActionMenu = null;
        _view.Targeting = null;
    }

    private bool IsVisible(Position p) => _view.RevealAll || _battle.IsTileVisible(Faction.Player, p);

    private float Pan(Position p)
    {
        Vector2 screen = _view.Camera.WorldToScreen(Iso.TileCenter(p));
        Rectangle viewport = _view.Camera.Viewport;
        return viewport.Width <= 0 ? 0f : Math.Clamp((screen.X - viewport.Center.X) / (viewport.Width * 0.6f), -1f, 1f);
    }

    /// <summary>Moves the camera when a point leaves the central part of the screen.</summary>
    private void Follow(Position p)
    {
        Vector2 screen = _view.Camera.WorldToScreen(Iso.TileCenter(p));
        Rectangle viewport = _view.Camera.Viewport;
        Rectangle comfort = viewport;
        comfort.Inflate(-viewport.Width / 5, -viewport.Height / 5);
        if (!comfort.Contains(screen)) _view.Camera.Center = Iso.TileCenter(p);
    }
}
