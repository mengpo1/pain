using Game.Core.Common;
using Microsoft.Xna.Framework;

namespace Game.Desktop.Graphics;

/// <summary>
/// Isometric projection of the battle map. A tile is a 64 × 32 pixel diamond and a level is 48
/// pixels high, i.e. one voxel is 2 pixels wide horizontally and 2 pixels high vertically (twice
/// OpenXcom's scale). "World pixels" are screen pixels at zoom 1 before the camera offset.
/// </summary>
public static class Iso
{
    public const int TileWidth = 64;
    public const int TileHeight = 32;
    public const int LevelHeight = 48;

    /// <summary>World pixel of the north corner (top vertex) of a tile's floor diamond.</summary>
    public static Vector2 TileTop(int x, int y, int z) =>
        new((x - y) * (TileWidth / 2), (x + y) * (TileHeight / 2) - z * LevelHeight);

    public static Vector2 TileTop(Position p) => TileTop(p.X, p.Y, p.Z);

    /// <summary>World pixel of the centre of a tile's floor, raised by <paramref name="elevation"/> voxels.</summary>
    public static Vector2 TileCenter(Position p, float elevation = 0f) =>
        TileTop(p) + new Vector2(0f, TileHeight / 2f - elevation * 2f);

    /// <summary>World pixel of a voxel.</summary>
    public static Vector2 VoxelToWorld(float vx, float vy, float vz) =>
        new((vx - vy) * 2f, (vx + vy) - vz * 2f);

    public static Vector2 VoxelToWorld(Voxel v) => VoxelToWorld(v.X, v.Y, v.Z);

    /// <summary>Tile whose floor diamond at level <paramref name="z"/> contains a world pixel.</summary>
    public static (int X, int Y) WorldToTile(Vector2 world, int z)
    {
        float sy = world.Y + z * LevelHeight;
        float a = world.X / (TileWidth / 2f);   // x - y
        float b = sy / (TileHeight / 2f);       // x + y
        return ((int)MathF.Floor((a + b) / 2f), (int)MathF.Floor((b - a) / 2f));
    }
}

/// <summary>Pan and zoom over the isometric world.</summary>
public sealed class IsoCamera
{
    public const float MinZoom = 0.5f;
    public const float MaxZoom = 2.5f;

    /// <summary>World pixel shown at the centre of the viewport.</summary>
    public Vector2 Center { get; set; }

    public float Zoom { get; set; } = 1f;

    public Rectangle Viewport { get; set; }

    public Matrix Transform =>
        Matrix.CreateTranslation(-MathF.Round(Center.X), -MathF.Round(Center.Y), 0f)
        * Matrix.CreateScale(Zoom, Zoom, 1f)
        * Matrix.CreateTranslation(Viewport.X + Viewport.Width / 2f, Viewport.Y + Viewport.Height / 2f, 0f);

    public Vector2 ScreenToWorld(Point screen) =>
        new Vector2(screen.X - Viewport.X - Viewport.Width / 2f, screen.Y - Viewport.Y - Viewport.Height / 2f) / Zoom + new Vector2(MathF.Round(Center.X), MathF.Round(Center.Y));

    public Vector2 WorldToScreen(Vector2 world) =>
        (world - new Vector2(MathF.Round(Center.X), MathF.Round(Center.Y))) * Zoom + new Vector2(Viewport.X + Viewport.Width / 2f, Viewport.Y + Viewport.Height / 2f);

    public void ZoomBy(float factor) => Zoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
}
