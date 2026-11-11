// Placeholder art & sound generator.
//   dotnet run --project tools/PlaceholderArt -- <repo root>
// Writes Content/horizon/assets and Content/mistvale/assets. Output is deterministic: re-running
// overwrites every file with identical bytes.
using PlaceholderArt;

string root = Path.GetFullPath(args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());

var horizon = new Pack(Path.Combine(root, "Content", "horizon", "assets"));
var mistvale = new Pack(Path.Combine(root, "Content", "mistvale", "assets"));

HorizonArt.Generate(horizon);
horizon.Text("LICENSE.md", License.Text("Horizon"));
MistvaleArt.Generate(mistvale);
mistvale.Text("LICENSE.md", License.Text("Mistvale"));

foreach (var pack in new[] { horizon, mistvale })
{
    Console.WriteLine($"{pack.Root}  ({pack.Written.Count} files)");
    foreach (var line in pack.Written) Console.WriteLine($"  {line}");
}
