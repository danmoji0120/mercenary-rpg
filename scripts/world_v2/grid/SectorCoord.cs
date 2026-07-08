using Godot;

namespace WorldV2;

public readonly struct SectorCoord
{
    public SectorCoord(int x, int y)
    {
        Value = new Vector2I(x, y);
    }

    public SectorCoord(Vector2I value)
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

    public static implicit operator Vector2I(SectorCoord coord)
    {
        return coord.Value;
    }

    public static implicit operator SectorCoord(Vector2I value)
    {
        return new SectorCoord(value);
    }
}
