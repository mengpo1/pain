// Placeholder art & sound generator.
//   dotnet run --project tools/PlaceholderArt -- <repo root>
// Writes Content/Base/assets and Mods/NoctInspired/assets. Output is deterministic: re-running
// overwrites every file with identical bytes.
using PlaceholderArt;

string root = Path.GetFullPath(args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());

var basePack = new Pack(Path.Combine(root, "Content", "Base", "assets"));
var noctPack = new Pack(Path.Combine(root, "Mods", "NoctInspired", "assets"));

BaseArt.Generate(basePack);
BaseSounds.Generate(basePack);
basePack.Text("LICENSE.md", License.Text("Base"));

NoctArt.Generate(noctPack);
NoctSounds.Generate(noctPack);
noctPack.Text("LICENSE.md", License.Text("NoctInspired mod"));

foreach (var pack in new[] { basePack, noctPack })
{
    Console.WriteLine($"{pack.Root}  ({pack.Written.Count} files)");
    foreach (var line in pack.Written) Console.WriteLine($"  {line}");
}
