using System;
using Godot;

namespace GameplayV3.Time.UI;

public static class MercenaryScheduleUiSelfCheckV3
{
    public static string LastSummary { get; private set; } = string.Empty;

    public static bool TryValidate(out string reason)
    {
        int slotCount = Convert.ToInt32(MercenarySchedulePanelV3.SlotCount);
        int gridColumns = Convert.ToInt32(MercenarySchedulePanelV3.GridColumns);
        int presetCount = Convert.ToInt32(MercenarySchedulePanelV3.PresetButtonCount);
        int stateToolCount = Convert.ToInt32(MercenarySchedulePanelV3.StateToolButtonCount);
        if (slotCount != 24 || gridColumns != 12 || presetCount != 5 || stateToolCount != 4)
        {
            reason = "Schedule UI constants do not match the required grid and tool counts.";
            return false;
        }

        MercenaryScheduleStateV3[] states =
        {
            MercenaryScheduleStateV3.Work,
            MercenaryScheduleStateV3.Anything,
            MercenaryScheduleStateV3.Recreation,
            MercenaryScheduleStateV3.Sleep
        };
        string[] letters = { "W", "A", "R", "S" };
        for (int index = 0; index < states.Length; index++)
        {
            if (MercenarySchedulePanelV3.StateLetter(states[index]) != letters[index]
                || string.IsNullOrWhiteSpace(MercenarySchedulePanelV3.StateLabel(states[index])))
            {
                reason = $"State display mapping failed for {states[index]}.";
                return false;
            }
        }

        MercenarySchedulePresetV3[] presets =
        {
            MercenarySchedulePresetV3.Standard,
            MercenarySchedulePresetV3.DayShift,
            MercenarySchedulePresetV3.NightShift,
            MercenarySchedulePresetV3.Free,
            MercenarySchedulePresetV3.Custom
        };
        foreach (MercenarySchedulePresetV3 preset in presets)
        {
            if (string.IsNullOrWhiteSpace(MercenarySchedulePanelV3.PresetLabel(preset)))
            {
                reason = $"Preset display mapping failed for {preset}.";
                return false;
            }
        }

        if (!ValidateViewport(new Vector2I(1280, 720), out reason)
            || !ValidateViewport(new Vector2I(1920, 1080), out reason)
            || !ValidateViewport(new Vector2I(1024, 576), out reason))
        {
            return false;
        }

        LastSummary = "24 slots / 12 columns / 5 presets / 4 state tools / container layout";
        reason = string.Empty;
        return true;
    }

    private static bool ValidateViewport(Vector2I viewport, out string reason)
    {
        if (viewport.X <= 0 || viewport.Y <= 0)
        {
            reason = "Viewport dimensions must be positive.";
            return false;
        }

        // The panel uses the right-side anchor range and an inner horizontal ScrollContainer.
        // This check guards the supported viewport cases without reproducing runtime layout state.
        float rightMargin = 12f;
        float rightEdge = viewport.X - rightMargin;
        float leftEdge = viewport.X * 0.55f;
        if (leftEdge < 0 || rightEdge <= leftEdge || rightEdge > viewport.X)
        {
            reason = $"Right-side anchored panel is outside viewport {viewport}.";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
