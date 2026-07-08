using Godot;

namespace WorldV2;

public readonly struct ChunkCoord
{
    public const int SizeCells = 32;

    public ChunkCoord(int x, int y)
    {
        Value = new Vector2I(x, y);
    }

    public ChunkCoord(Vector2I value)
    {
        Value = value;
    }

    public Vector2I Value { get; }
    public int X => Value.X;
    public int Y => Value.Y;

    public static ChunkCoord FromLocalCell(Vector2I localCellCoord)
    {
        return new ChunkCoord(
            Mathf.FloorToInt(localCellCoord.X / (float)SizeCells),
            Mathf.FloorToInt(localCellCoord.Y / (float)SizeCells));
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    public static implicit operator Vector2I(ChunkCoord coord)
    {
        return coord.Value;
    }

    public static implicit operator ChunkCoord(Vector2I value)
    {
        return new ChunkCoord(value);
    }
}
