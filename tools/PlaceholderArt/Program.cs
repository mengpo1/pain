// Placeholder art & sound generator.
//   dotnet run --project tools/PlaceholderArt -- <repo root>
// Writes Content/Base/assets, Mods/NoctInspired/assets and Mods/NoctPrototype/assets. Output is deterministic: re-running
// overwrites every file with identical bytes.
using PlaceholderArt;

string root = Path.GetFullPath(args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());

var basePack = new Pack(Path.Combine(root, "Content", "Base", "assets"));
var noctPack = new Pack(Path.Combine(root, "Mods", "NoctInspired", "assets"));
var prototypePack = new Pack(Path.Combine(root, "Mods", "NoctPrototype", "assets"));

BaseArt.Generate(basePack);
BaseSounds.Generate(basePack);
basePack.Text("LICENSE.md", License.Text("Base"));

NoctArt.Generate(noctPack);
NoctSounds.Generate(noctPack);
noctPack.Text("LICENSE.md", License.Text("NoctInspired mod"));

// The prototype is self-contained: the shared thermal set, then its own creatures and objects.
NoctArt.Generate(prototypePack);
NoctSounds.Generate(prototypePack);
NoctPrototypeArt.Generate(prototypePack);
NoctPrototypeSounds.Generate(prototypePack);
prototypePack.Text("LICENSE.md", License.Text("NoctPrototype mod"));

foreach (var pack in new[] { basePack, noctPack, prototypePack })
{
    Console.WriteLine($"{pack.Root}  ({pack.Written.Count} files)");
    foreach (var line in pack.Written) Console.WriteLine($"  {line}");
}
