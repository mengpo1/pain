using System.Globalization;
using System.Numerics;
using System.Text;

namespace PlaceholderArt.Rig;

/// <summary>
/// Collects the texture and animation entries of generated sprite sheets and writes them as an asset
/// manifest (<c>assets.rig.json</c>), loaded by the game after the pack's hand-written <c>assets.json</c>.
/// </summary>
public sealed class RigManifest
{
    readonly SortedDictionary<string, string> _textures = new(StringComparer.Ordinal);
    readonly SortedDictionary<string, string> _animations = new(StringComparer.Ordinal);

    public void Texture(string id, string file) => _textures[id] = file;

    public void Animation(string id, string json) => _animations[id] = json;

    public string ToJson(string comment)
    {
        var sb = new StringBuilder();
        sb.Append("{\n  // ").Append(comment).Append('\n');
        sb.Append("  \"textures\": {\n");
        sb.Append(string.Join(",\n", _textures.Select(kv => $"    \"{kv.Key}\": \"{kv.Value}\"")));
        sb.Append("\n  },\n  \"animations\": {\n");
        sb.Append(string.Join(",\n", _animations.Select(kv => $"    \"{kv.Key}\": {kv.Value}")));
        sb.Append("\n  }\n}\n");
        return sb.ToString();
    }
}

/// <summary>Renders rig clips into sprite sheets (one row of frames each) with their timing, events and weapon points.</summary>
public static class RigSheets
{
    static string F(float v) => MathF.Round(v, 3).ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>A finished strip: frames, per-frame weapon points and the animation entry describing it.</summary>
    public static void Write(Pack pack, RigManifest manifest, string animationId, string file, IReadOnlyList<Canvas> frames, IReadOnlyList<float> durations,
        bool loop, IReadOnlyDictionary<int, string> events, IReadOnlyList<float[]>? weapon)
    {
        int w = frames[0].Width, h = frames[0].Height;
        var sheet = new Canvas(w * frames.Count, h);
        for (int i = 0; i < frames.Count; i++) sheet.Draw(frames[i], i * w, 0);
        pack.Png(file, sheet);
        string texture = animationId + ".sheet";
        manifest.Texture(texture, "assets/" + file);

        var json = new StringBuilder();
        json.Append($"{{ \"texture\": \"{texture}\", \"frameWidth\": {w}, \"frameHeight\": {h}, \"loop\": {(loop ? "true" : "false")}, ");
        json.Append("\"durations\": [").Append(string.Join(", ", durations.Select(F))).Append(']');
        if (events.Count > 0)
            json.Append(", \"events\": { ").Append(string.Join(", ", events.OrderBy(e => e.Key).Select(e => $"\"{e.Key}\": \"{e.Value}\""))).Append(" }");
        if (weapon is not null)
            json.Append(", \"attach\": { \"weapon\": [").Append(string.Join(", ", weapon.Select(p => "[" + string.Join(", ", p.Select(F)) + "]"))).Append("] }");
        json.Append(" }");
        manifest.Animation(animationId, json.ToString());
    }

    /// <summary>A second id for an existing strip (generic fallbacks such as "idle" for "idle.unarmed").</summary>
    public static void Alias(RigManifest manifest, string animationId, string json) => manifest.Animation(animationId, json);
}

/// <summary>Everything a human character needs, drawn from the rig: animation strips and the still sprites the game also uses.</summary>
public static class RigHumans
{
    /// <summary>Pixel-art blood pool: hard edge, three tones, no blending.</summary>
    public static Canvas Pool(int size, float cx, float cy, float radius, BloodColors colors, int seed)
    {
        var soft = new Canvas(size, size);
        if (radius > 0.5f) Effects.BloodPool(soft, cx, cy, radius, colors, seed);
        var hard = new Canvas(size, size);
        Rgba[] tones = [colors.Dark, colors.Mid, colors.Light];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Rgba p = soft[x, y];
                if (p.A < 0.5f) continue;
                hard[x, y] = tones.MinBy(t => (t.R - p.R) * (t.R - p.R) + (t.G - p.G) * (t.G - p.G) + (t.B - p.B) * (t.B - p.B)) with { A = 1f };
            }
        }
        return hard;
    }

    public static void Generate(Pack pack, RigManifest manifest, string name, HumanBuild build, BloodColors blood, int seed,
        bool heavy = false, RenderStyle? style = null, int upperSize = 44, int fullSize = 48)
    {
        style ??= new RenderStyle();
        string folder = $"sprites/actors/{name}";
        var standing = new HashSet<int>(HumanPart.Upper);
        var legs = new HashSet<int>(HumanPart.Legs);

        Canvas Draw(HumanPose pose, int size, ClipKind kind, float pool = 0f, Func<Prim, bool>? only = null)
        {
            List<Prim> prims = HumanModel.Build(pose, build);
            Func<Prim, bool> include = only ?? (kind switch
            {
                ClipKind.Upper => p => standing.Contains(p.Part),
                ClipKind.Legs => p => legs.Contains(p.Part),
                _ => _ => true,
            });
            var origin = new Vector2(size / 2f, size / 2f);
            Canvas body = TopDownRenderer.Render(prims, build.Palette, size, size, origin, style, include);
            if (pool <= 0f) return body;
            float k = build.Scale;
            Canvas under = Pool(size, size / 2f + 3f * k, size / 2f + 1.5f * k, 15.5f * k * pool, blood, seed);
            under.Draw(body);
            return under;
        }

        foreach (RigClip clip in HumanClips.All(heavy))
        {
            int size = clip.Kind == ClipKind.Full ? fullSize : upperSize;
            var frames = new List<Canvas>();
            var points = new List<float[]>();
            var events = new Dictionary<int, string>();
            for (int i = 0; i < clip.Frames.Count; i++)
            {
                RigFrame frame = clip.Frames[i];
                frames.Add(Draw(frame.Pose, size, clip.Kind, frame.Pool));
                HumanSkeleton k = HumanModel.Pose(frame.Pose, build);
                Vector3 fore = k.WristRight - k.ElbowRight;
                float angle = frame.WeaponAngle ?? MathF.Atan2(fore.Y, fore.X) * 180f / MathF.PI;
                points.Add([k.HandRight.X, k.HandRight.Y, angle, frame.WeaponOver ? 1f : 0f]);
                if (frame.Event is { } e) events[i] = e;
            }
            string id = $"actor.{name}.{clip.Name}";
            bool armed = clip.Kind != ClipKind.Legs;
            RigSheets.Write(pack, manifest, id, $"{folder}/{clip.Name.Replace('.', '_')}.png", frames, clip.Frames.Select(f => f.Duration).ToList(),
                clip.Loop, events, armed ? points : null);
        }

        // Still sprites used outside the animation set (menus, decals, the dismemberment pieces).
        pack.Png($"sprites/actors/{name}.png", Draw(HumanClips.Unarmed, upperSize, ClipKind.Upper));
        pack.Png($"sprites/actors/{name}_down.png", Draw(HumanClips.Lying, fullSize, ClipKind.Full));
        pack.Png($"sprites/actors/{name}_dead.png", Draw(HumanClips.Lying, fullSize, ClipKind.Full, 1f));
        var mount = HumanClips.All(heavy).First(c => c.Name == "execute.headbash").Frames[^1].Pose;
        pack.Png($"sprites/actors/{name}_mount.png", Draw(mount, fullSize, ClipKind.Full));
        (string Part, int Id)[] pieces =
        [
            ("head", HumanPart.Head), ("torso", HumanPart.Torso), ("arm1", HumanPart.ArmRight), ("arm2", HumanPart.ArmLeft),
            ("leg1", HumanPart.LegRight), ("leg2", HumanPart.LegLeft),
        ];
        foreach (var (part, id) in pieces)
            pack.Png($"sprites/actors/{name}_{part}.png", Draw(HumanClips.Lying, fullSize, ClipKind.Full, only: p => p.Part == id));
    }
}
