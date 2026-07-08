using Godot;

namespace WorldV2;

public partial class WorldV2CameraController : Camera2D
{
    [Export]
    public float Acceleration { get; set; } = 3600.0f;

    [Export]
    public float Deceleration { get; set; } = 4600.0f;

    [Export]
    public float MaxSpeed { get; set; } = 980.0f;

    [Export]
    public float SprintSpeedMultiplier { get; set; } = 2.25f;

    [Export]
    public float ZoomStep { get; set; } = 0.10f;

    [Export]
    public float MinZoom { get; set; } = 0.10f;

    [Export]
    public float MaxZoom { get; set; } = 4.0f;

    [Export]
    public int TileSize { get; set; } = 24;

    public Vector2 Velocity { get; private set; } = Vector2.Zero;
    public float CurrentSpeed => Velocity.Length();

    public override void _Ready()
    {
        MakeCurrent();
        ClampZoom();
    }

    public override void _Process(double delta)
    {
        float deltaSeconds = (float)delta;
        Vector2 inputDirection = GetInputDirection();
        float speedLimit = Input.IsKeyPressed(Key.Shift) ? MaxSpeed * SprintSpeedMultiplier : MaxSpeed;

        if (inputDirection != Vector2.Zero)
        {
            Velocity += inputDirection * Acceleration * deltaSeconds;
            Velocity = ClampVectorLength(Velocity, speedLimit);
        }
        else
        {
            Velocity = MoveTowardZero(Velocity, Deceleration * deltaSeconds);
        }

        if (Velocity.LengthSquared() < 0.01f)
        {
            Velocity = Vector2.Zero;
        }

        Position += Velocity * deltaSeconds;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseButton || !mouseButton.Pressed)
        {
            return;
        }

        if (mouseButton.ButtonIndex == MouseButton.WheelUp)
        {
            SetZoomValue(Zoom.X + ZoomStep);
        }
        else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
        {
            SetZoomValue(Zoom.X - ZoomStep);
        }
    }

    public void CenterOnGlobalCell(Vector2I globalCellCoord)
    {
        Position = GlobalCellToWorldCenter(globalCellCoord);
        Velocity = Vector2.Zero;
    }

    public void ClampToWorldBounds(Rect2I cellBounds)
    {
        if (cellBounds.Size.X <= 0 || cellBounds.Size.Y <= 0)
        {
            return;
        }

        Vector2I currentCell = GetCameraGlobalCell();
        Vector2I maxCell = cellBounds.End - Vector2I.One;
        Vector2I clampedCell = new(
            Mathf.Clamp(currentCell.X, cellBounds.Position.X, maxCell.X),
            Mathf.Clamp(currentCell.Y, cellBounds.Position.Y, maxCell.Y));

        if (clampedCell == currentCell)
        {
            return;
        }

        CenterOnGlobalCell(clampedCell);
    }

    public void PanByCells(Vector2I cellDelta)
    {
        Position += new Vector2(cellDelta.X * TileSize, cellDelta.Y * TileSize);
        Velocity = Vector2.Zero;
    }

    public Vector2I GetCameraGlobalCell()
    {
        return new Vector2I(
            Mathf.FloorToInt(GlobalPosition.X / TileSize),
            Mathf.FloorToInt(GlobalPosition.Y / TileSize));
    }

    public Vector2 GlobalCellToWorldCenter(Vector2I globalCellCoord)
    {
        return new Vector2(
            globalCellCoord.X * TileSize + TileSize * 0.5f,
            globalCellCoord.Y * TileSize + TileSize * 0.5f);
    }

    private void SetZoomValue(float zoomValue)
    {
        float clamped = Mathf.Clamp(zoomValue, MinZoom, MaxZoom);
        Zoom = new Vector2(clamped, clamped);
    }

    private void ClampZoom()
    {
        SetZoomValue(Zoom.X);
    }

    private static Vector2 GetInputDirection()
    {
        Vector2 direction = Vector2.Zero;

        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
        {
            direction.X -= 1.0f;
        }

        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
        {
            direction.X += 1.0f;
        }

        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
        {
            direction.Y -= 1.0f;
        }

        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
        {
            direction.Y += 1.0f;
        }

        return direction == Vector2.Zero ? Vector2.Zero : direction.Normalized();
    }

    private static Vector2 ClampVectorLength(Vector2 value, float maxLength)
    {
        float length = value.Length();
        return length > maxLength && length > 0.0f ? value / length * maxLength : value;
    }

    private static Vector2 MoveTowardZero(Vector2 value, float amount)
    {
        float length = value.Length();

        if (length <= amount || length <= 0.0f)
        {
            return Vector2.Zero;
        }

        return value / length * (length - amount);
    }
}
