using Godot;

namespace WorldV2;

public readonly struct GlobalChunkCoord
{
    public GlobalChunkCoord(int x, int y)
    {
        Value = new Vector2I(x, y);
    }

    public GlobalChunkCoord(Vector2I value)
    {
        Value = value;
    }

    public Vector2I Value { get; }
    public int X => Value.X;
    public int Y => Value.Y;

    public override string ToString()
    {
        return Value.ToString();
    }

    public static implicit operator Vector2I(GlobalChunkCoord coord)
    {
        return coord.Value;
    }

    public static implicit operator GlobalChunkCoord(Vector2I value)
    {
        return new GlobalChunkCoord(value);
    }
}
