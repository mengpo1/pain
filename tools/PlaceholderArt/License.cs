namespace PlaceholderArt;

static class License
{
    public static string Text(string packName) => $"""
        # {packName} placeholder assets

        Every image and sound in this folder is an original placeholder asset generated procedurally by
        `tools/PlaceholderArt` (run `dotnet run --project tools/PlaceholderArt -- <repo root>` to rebuild them).
        They are not derived from, traced from or sampled from any existing game, artwork or recording.

        To the extent possible under law, the authors have dedicated all copyright and related and neighbouring
        rights to these files to the public domain worldwide under the
        **CC0 1.0 Universal Public Domain Dedication**:
        https://creativecommons.org/publicdomain/zero/1.0/

        You can copy, modify and distribute them, even for commercial purposes, without asking permission.

        """;
}
