namespace TopDown.Core.Definitions;

/// <summary>Idle behaviour of an AI character. Combat reactions are shared by all behaviours.</summary>
public enum AiBehavior
{
    /// <summary>Stands at its post and looks around.</summary>
    Guard,
    /// <summary>Walks along a patrol route.</summary>
    Patrol,
    /// <summary>Roams randomly around its starting point.</summary>
    Wander,
    /// <summary>Does nothing until a level event activates it.</summary>
    Dormant,
    /// <summary>Lies in wait without moving; wakes up when its target comes within <see cref="AiDefinition.WakeRadius"/>.</summary>
    Ambush,
}

/// <summary>Data describing a character (the player or any enemy).</summary>
public sealed class ActorDefinition
{
    public string Id { get; set; } = "";
    public string NameKey { get; set; } = "";

    /// <summary>Characters of different factions are hostile to each other.</summary>
    public string Faction { get; set; } = "enemy";

    public float Health { get; set; } = 100f;
    public float Radius { get; set; } = 11f;

    /// <summary>Body type ("human", "quadruped"…): weapons can execute each one differently.</summary>
    public string Anatomy { get; set; } = "human";

    /// <summary>How this character crawls when wounded (see <see cref="WeaponDefinition.WoundChance"/>). Null: never wounded.</summary>
    public WoundDefinition? Wound { get; set; }

    /// <summary>The player can run (at <see cref="RunSpeed"/>) while it has stamina. Null: no running.</summary>
    public StaminaDefinition? Stamina { get; set; }

    /// <summary>Walking speed (units/s).</summary>
    public float Speed { get; set; } = 120f;

    /// <summary>Speed when chasing or fleeing (units/s). The player always uses <see cref="Speed"/>.</summary>
    public float RunSpeed { get; set; } = 200f;

    public float Acceleration { get; set; } = 3000f;

    /// <summary>Deceleration when not moving (also slows corpses down).</summary>
    public float Friction { get; set; } = 2200f;

    /// <summary>Weapon carried at the start, if any.</summary>
    public string? WeaponId { get; set; }

    /// <summary>Weapon used when empty-handed (fists, claws, bite). Defaults to the rules' unarmed weapon.</summary>
    public string? NaturalWeaponId { get; set; }

    public bool KnockdownImmune { get; set; }

    /// <summary>A knockdown-immune character hit by a knockdown attack staggers this long instead.</summary>
    public float KnockdownResistStun { get; set; } = 0.3f;
    public float HitStunDuration { get; set; } = 0.15f;
    public float GetUpDuration { get; set; } = 0.4f;

    /// <summary>Damage multiplier per damage type (missing types use 1).</summary>
    public Dictionary<string, float> DamageMultipliers { get; set; } = new();

    /// <summary>Counted by "eliminate all" objectives.</summary>
    public bool CountsForObjectives { get; set; } = true;

    public ActorSprites Sprites { get; set; } = new();
    public ActorSounds Sounds { get; set; } = new();
    public AiDefinition Ai { get; set; } = new();
}

/// <summary>A wounded character crawls away on the floor until it is finished off (or bleeds out).</summary>
public sealed class WoundDefinition
{
    /// <summary>Crawling speed (units/s).</summary>
    public float CrawlSpeed { get; set; } = 24f;

    /// <summary>Time before dying of its wounds; 0 = never (it must be finished off).</summary>
    public float BleedOutTime { get; set; }
}

/// <summary>Stamina spent by running, recovered when not running.</summary>
public sealed class StaminaDefinition
{
    public float Max { get; set; } = 100f;
    /// <summary>Spent per second of running.</summary>
    public float RunDrain { get; set; } = 28f;
    /// <summary>Recovered per second when not running.</summary>
    public float Regen { get; set; } = 22f;
    /// <summary>Pause after running before recovery starts.</summary>
    public float RegenDelay { get; set; } = 0.6f;
    /// <summary>Once exhausted, running is possible again from this amount.</summary>
    public float RecoverThreshold { get; set; } = 25f;
    /// <summary>Running footsteps can be heard this far (0 = silent).</summary>
    public float RunNoise { get; set; }
}

/// <summary>
/// Presentation of a character on a thermal camera: how bright it is at rest and when active, and
/// a slow pulse that gives away a creature lying in wait.
/// </summary>
public sealed class HeatDefinition
{
    public float Rest { get; set; } = 1f;
    public float Active { get; set; } = 1f;
    /// <summary>Amplitude of the slow breathing pulse at rest.</summary>
    public float Pulse { get; set; }
}

public sealed class ActorSprites
{
    public string Body { get; set; } = "";
    public string? Legs { get; set; }
    public string? Down { get; set; }
    public string? Dead { get; set; }

    /// <summary>Kneeling astride a body (executions). Falls back to <see cref="Body"/>.</summary>
    public string? Mount { get; set; }

    /// <summary>Legs while kicking (executions). Falls back to <see cref="Legs"/>.</summary>
    public string? Kick { get; set; }

    /// <summary>Wounded, face down, crawling (animated with the distance crawled). Falls back to <see cref="Down"/>.</summary>
    public string? Crawl { get; set; }

    /// <summary>
    /// Body on the floor without its <see cref="Parts"/>, for executions that tear them off. Each part
    /// is an image of the same size as the torso, drawn at the same place, so they fit together.
    /// </summary>
    public string? Torso { get; set; }
    public List<BodyPart> Parts { get; set; } = new();

    /// <summary>Colour multiplied with blood effects ("#rrggbb").</summary>
    public string BloodTint { get; set; } = "#ffffff";

    /// <summary>Thermal signature (brightness at rest and when active). Null: always fully visible.</summary>
    public HeatDefinition? Heat { get; set; }

    /// <summary>
    /// Animation set: pose key → animation (or sprite) id, for instance "idle.club", "attack.unarmed",
    /// "fire.long", "hurt", "knockdown", "getup", "death.shot", "walk", "leap.crouch", "awaken".
    /// Missing keys fall back to simpler ones, then to <see cref="Body"/> (see ActorPose).
    /// </summary>
    public Dictionary<string, string> Animations { get; set; } = new();

    /// <summary>
    /// Prefix of a whole animation set: key "attack.club" also looks for the animation
    /// "{AnimationSet}.attack.club". Entries of <see cref="Animations"/> take precedence.
    /// </summary>
    public string? AnimationSet { get; set; }
}

/// <summary>A piece of a body that can be torn off ("head", "leg").</summary>
public sealed class BodyPart
{
    public string Id { get; set; } = "";
    public string Sprite { get; set; } = "";

    /// <summary>Drawn over the torso (a head) rather than under it (legs).</summary>
    public bool Over { get; set; }
}

public sealed class ActorSounds
{
    public string? Death { get; set; }
    public string? Hurt { get; set; }
    public string? Alert { get; set; }
    /// <summary>Played now and then while it lies in wait (a hint for an attentive player).</summary>
    public string? Idle { get; set; }
    /// <summary>A leap is being prepared.</summary>
    public string? Leap { get; set; }
}

/// <summary>Perception and reaction parameters, reused by every AI behaviour.</summary>
public sealed class AiDefinition
{
    public AiBehavior Behavior { get; set; } = AiBehavior.Guard;
    public float VisionRange { get; set; } = 420f;
    public float VisionAngleDegrees { get; set; } = 120f;

    /// <summary>Within this distance the target is noticed whatever the facing direction.</summary>
    public float CloseDetectionRadius { get; set; } = 36f;

    /// <summary>Multiplies the radius of every heard noise.</summary>
    public float HearingMultiplier { get; set; } = 1f;

    /// <summary>Delay between spotting the target and the first attack.</summary>
    public float ReactionTime { get; set; } = 0.35f;

    public float TurnSpeedDegrees { get; set; } = 400f;
    public float AimErrorDegrees { get; set; } = 3f;

    /// <summary>Firearm users try to keep this fraction of their weapon range.</summary>
    public float PreferredRangeFactor { get; set; } = 0.45f;

    /// <summary>Time without seeing the target before searching for it.</summary>
    public float LoseTargetTime { get; set; } = 2.5f;

    public float SearchDuration { get; set; } = 4f;
    public float SuspicionDelay { get; set; } = 0.4f;
    public float PatrolPause { get; set; } = 1.2f;

    /// <summary>Radius (in tiles) of the wander behaviour.</summary>
    public float WanderRadiusTiles { get; set; } = 4f;

    /// <summary>When alerted, allies within this radius are alerted too.</summary>
    public float AlertAlliesRadius { get; set; }

    /// <summary>Behaviour "ambush": wakes up when the target comes this close in clear view (units).</summary>
    public float WakeRadius { get; set; } = 110f;

    /// <summary>Behaviour "ambush": time spent rising after waking up, before the hunt (seconds).</summary>
    public float WakeTime { get; set; }

    /// <summary>Leap attack (see <see cref="LeapDefinition"/>). Null: no leaping.</summary>
    public LeapDefinition? Leap { get; set; }
}

/// <summary>
/// A sudden leap at the target: a visible crouch, a fast straight jump stopped by walls, one hit on
/// contact (with the character's natural weapon), then a short recovery.
/// </summary>
public sealed class LeapDefinition
{
    /// <summary>Leaps when the target is between these distances (units, centre to centre).</summary>
    public float MinRange { get; set; } = 40f;
    public float Range { get; set; } = 170f;
    /// <summary>Crouch before the leap: the warning the player can react to.</summary>
    public float WindUp { get; set; } = 0.45f;
    public float Speed { get; set; } = 560f;
    /// <summary>Length of the leap (units).</summary>
    public float Distance { get; set; } = 210f;
    /// <summary>Vulnerable pause after landing.</summary>
    public float Recovery { get; set; } = 0.7f;
    /// <summary>Time before the next leap.</summary>
    public float Cooldown { get; set; } = 1.4f;
}
