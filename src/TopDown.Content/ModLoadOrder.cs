namespace TopDown.Content;

/// <summary>
/// Decides which packs are loaded and in which order: base first, then enabled mods in the
/// requested order, each mod after its dependencies. Broken mods are skipped with a diagnostic.
/// </summary>
internal static class ModLoadOrder
{
    public static List<ContentPack> Resolve(ContentPack basePack, IReadOnlyList<ContentPack> installed, IReadOnlyList<string> enabled, DiagnosticList diagnostics)
    {
        var candidates = new Dictionary<string, ContentPack>(StringComparer.Ordinal);
        foreach (string id in enabled.Distinct(StringComparer.Ordinal))
        {
            ContentPack? mod = installed.FirstOrDefault(m => m.Manifest.Id == id);
            if (mod is null)
            {
                diagnostics.Error($"mod:{id}", "This mod is enabled but not installed.");
                continue;
            }

            if (mod.Manifest.FormatVersion != PackManifest.SupportedFormatVersion)
            {
                diagnostics.Error($"mod:{id}",
                    $"Content format {mod.Manifest.FormatVersion} is not supported (expected {PackManifest.SupportedFormatVersion}); the mod is not loaded.");
                continue;
            }

            candidates[id] = mod;
        }

        // Drop mods whose dependencies are unavailable (repeat: removing one may break another).
        bool removed;
        do
        {
            removed = false;
            foreach (ContentPack mod in candidates.Values.ToList())
            {
                string? missing = mod.Manifest.Dependencies.FirstOrDefault(dep => dep != basePack.Manifest.Id && !candidates.ContainsKey(dep));
                if (missing is null) continue;
                diagnostics.Error($"mod:{mod.Manifest.Id}", $"Requires '{missing}', which is not installed or not enabled; the mod is not loaded.");
                candidates.Remove(mod.Manifest.Id);
                removed = true;
            }
        }
        while (removed);

        var ordered = new List<ContentPack> { basePack };
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var done = new HashSet<string>(StringComparer.Ordinal) { basePack.Manifest.Id };
        var failed = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in enabled)
        {
            if (candidates.ContainsKey(id)) Visit(id, candidates, visiting, done, failed, ordered, diagnostics);
        }

        return ordered;
    }

    /// <summary>Adds a mod after its dependencies. Returns false if it cannot be loaded (dependency cycle).</summary>
    private static bool Visit(string id, Dictionary<string, ContentPack> candidates, HashSet<string> visiting, HashSet<string> done,
        HashSet<string> failed, List<ContentPack> ordered, DiagnosticList diagnostics)
    {
        if (done.Contains(id)) return true;
        if (failed.Contains(id)) return false;
        if (!visiting.Add(id))
        {
            diagnostics.Error($"mod:{id}", "Circular dependency between mods; the mods involved are not loaded.");
            failed.Add(id);
            return false;
        }

        bool ok = true;
        foreach (string dependency in candidates[id].Manifest.Dependencies)
        {
            if (candidates.ContainsKey(dependency) && !Visit(dependency, candidates, visiting, done, failed, ordered, diagnostics)) ok = false;
        }

        visiting.Remove(id);
        if (!ok || failed.Contains(id))
        {
            failed.Add(id);
            return false;
        }

        if (done.Add(id)) ordered.Add(candidates[id]);
        return true;
    }

    /// <summary>True if <paramref name="pack"/> depends (directly or not) on <paramref name="other"/>.</summary>
    public static bool DependsOn(ContentPack pack, ContentPack other, IReadOnlyList<ContentPack> packs)
    {
        if (other.IsBase) return true;
        var stack = new Stack<string>(pack.Manifest.Dependencies);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (stack.TryPop(out string? id))
        {
            if (id == other.Manifest.Id) return true;
            if (!seen.Add(id)) continue;
            ContentPack? dependency = packs.FirstOrDefault(p => p.Manifest.Id == id);
            if (dependency is null) continue;
            foreach (string next in dependency.Manifest.Dependencies) stack.Push(next);
        }

        return false;
    }
}
