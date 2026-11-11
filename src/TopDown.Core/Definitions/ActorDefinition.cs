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
}
