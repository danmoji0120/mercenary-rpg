using System.Collections.Generic;
using Godot;

public partial class FacilityDebugOverlay : Node2D
{
    private BaseBuildManager? _buildManager;
    private bool _enabled;
    private readonly List<FacilityInfo> _facilities = new();

    public override void _Process(double delta)
    {
        if (!_enabled || _buildManager == null)
        {
            return;
        }

        _facilities.Clear();
        _facilities.AddRange(_buildManager.GetFacilities());
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_enabled)
        {
            return;
        }

        foreach (FacilityInfo facility in _facilities)
        {
            DrawFacilityMarker(facility);
        }
    }

    public void SetBuildManager(BaseBuildManager buildManager)
    {
        _buildManager = buildManager;
    }

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;

        if (!_enabled)
        {
            Clear();
            return;
        }

        if (_buildManager != null)
        {
            _facilities.Clear();
            _facilities.AddRange(_buildManager.GetFacilities());
        }

        QueueRedraw();
    }

    public void Clear()
    {
        _facilities.Clear();
        QueueRedraw();
    }

    private void DrawFacilityMarker(FacilityInfo facility)
    {
        Vector2 center = facility.WorldPosition;
        Color color = GetFacilityColor(facility.FacilityType);
        string label = GetFacilityLabel(facility.FacilityType);

        DrawCircle(center, 10.0f, new Color(color.R, color.G, color.B, 0.24f));
        DrawArc(center, 12.0f, 0.0f, Mathf.Tau, 32, color, 2.0f);

        if (facility.IsOccupied)
        {
            DrawArc(center, 17.0f, 0.0f, Mathf.Tau, 32, new Color(0.3f, 1.0f, 0.54f, 0.95f), 2.5f);
            DrawString(ThemeDB.FallbackFont, center + new Vector2(9.0f, -12.0f), "O", HorizontalAlignment.Left, -1.0f, 12, new Color(0.3f, 1.0f, 0.54f, 0.95f));
        }
        else if (facility.IsReserved)
        {
            DrawArc(center, 16.0f, 0.0f, Mathf.Tau, 32, new Color(1.0f, 1.0f, 0.28f, 0.95f), 2.0f);
            DrawString(ThemeDB.FallbackFont, center + new Vector2(9.0f, -12.0f), "R", HorizontalAlignment.Left, -1.0f, 12, new Color(1.0f, 1.0f, 0.28f, 0.95f));
        }

        if (facility.FacilityType == FacilityType.Bed)
        {
            DrawRect(new Rect2(center + new Vector2(-9.0f, -5.0f), new Vector2(18.0f, 10.0f)), color, false, 2.0f);
            DrawLine(center + new Vector2(-9.0f, -1.0f), center + new Vector2(9.0f, -1.0f), color, 2.0f);
        }
        else if (facility.FacilityType == FacilityType.Storage)
        {
            DrawRect(new Rect2(center + new Vector2(-8.0f, -8.0f), new Vector2(16.0f, 16.0f)), color, false, 2.0f);
            DrawLine(center + new Vector2(-8.0f, -8.0f), center + new Vector2(8.0f, 8.0f), color, 1.5f);
            DrawLine(center + new Vector2(8.0f, -8.0f), center + new Vector2(-8.0f, 8.0f), color, 1.5f);
        }
        else if (facility.FacilityType == FacilityType.GuardPost)
        {
            DrawLine(center + new Vector2(-4.0f, 8.0f), center + new Vector2(-4.0f, -10.0f), color, 2.0f);
            DrawColoredPolygon(new[]
            {
                center + new Vector2(-4.0f, -10.0f),
                center + new Vector2(9.0f, -6.0f),
                center + new Vector2(-4.0f, -2.0f)
            }, color);
        }

        DrawString(ThemeDB.FallbackFont, center + new Vector2(-5.0f, 27.0f), label, HorizontalAlignment.Left, -1.0f, 14, color);
    }

    private static Color GetFacilityColor(FacilityType facilityType)
    {
        return facilityType switch
        {
            FacilityType.Bed => new Color(0.42f, 0.72f, 1.0f, 0.95f),
            FacilityType.Storage => new Color(1.0f, 0.68f, 0.28f, 0.95f),
            FacilityType.GuardPost => new Color(1.0f, 0.32f, 0.28f, 0.95f),
            _ => new Color(0.8f, 0.8f, 0.8f, 0.95f)
        };
    }

    private static string GetFacilityLabel(FacilityType facilityType)
    {
        return facilityType switch
        {
            FacilityType.Bed => "B",
            FacilityType.Storage => "S",
            FacilityType.GuardPost => "G",
            _ => "?"
        };
    }
}
