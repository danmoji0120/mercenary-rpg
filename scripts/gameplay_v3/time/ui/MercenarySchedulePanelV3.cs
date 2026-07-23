using System;
using System.Collections.Generic;
using GameplayV3.Control;
using GameplayV3.Mercenary;
using GameplayV3.Session;
using GameplayV3.UI;
using Godot;
using WorldV2;

namespace GameplayV3.Time.UI;

public partial class MercenarySchedulePanelV3 : Godot.Control
{
    public const int SlotCount = 24;
    public const int GridColumns = 12;
    public const int PresetButtonCount = 5;
    public const int StateToolButtonCount = 4;

    private static readonly MercenaryScheduleStateV3[] StateTools =
    {
        MercenaryScheduleStateV3.Work,
        MercenaryScheduleStateV3.Anything,
        MercenaryScheduleStateV3.Recreation,
        MercenaryScheduleStateV3.Sleep
    };

    private static readonly MercenarySchedulePresetV3[] Presets =
    {
        MercenarySchedulePresetV3.Standard,
        MercenarySchedulePresetV3.DayShift,
        MercenarySchedulePresetV3.NightShift,
        MercenarySchedulePresetV3.Free,
        MercenarySchedulePresetV3.Custom
    };

    private MercenaryControlSessionV3? _control;
    private MercenarySessionV3? _mercenaries;
    private MercenaryScheduleSessionV3? _schedules;
    private SimulationClockSessionV3? _clock;
    private WorldManagerV2? _manager;
    private MarginContainer? _layoutRoot;
    private PanelContainer? _panel;
    private Label? _targetLabel;
    private Label? _summaryLabel;
    private Label? _statusLabel;
    private string _targetMercenaryId = string.Empty;
    private bool _worldMapBlocked;
    private bool _initialized;
    private bool _sessionSubscribed;
    private bool _refreshQueued;
    private bool _pendingFullRefresh;
    private readonly HashSet<int> _pendingPartialHours = new();
    private MercenaryScheduleStateV3 _selectedTool = MercenaryScheduleStateV3.Work;
    private readonly Button[] _slotButtons = new Button[SlotCount];
    private readonly Button[] _presetButtons = new Button[PresetButtonCount];
    private readonly Button[] _stateToolButtons = new Button[StateToolButtonCount];
    private readonly Dictionary<MercenaryScheduleStateV3, StyleBoxFlat> _slotStyles = new();
    private readonly Dictionary<MercenaryScheduleStateV3, StyleBoxFlat> _currentSlotStyles = new();

    public bool IsPanelOpen => _panel?.Visible == true;
    public string SchedulePanelTargetMercenaryId => _targetMercenaryId;
    public int SchedulePanelRootCount => _initialized ? 1 : 0;
    public int ScheduleSlotButtonCount => CountCreated(_slotButtons);
    public int SchedulePresetButtonCount => CountCreated(_presetButtons);
    public int ScheduleStateToolButtonCount => CountCreated(_stateToolButtons);
    public int ScheduleFullRefreshCount { get; private set; }
    public int SchedulePartialRefreshCount { get; private set; }
    public int ScheduleEventCoalescedCount { get; private set; }
    public int SchedulePanelDuplicateSubscriptionCount { get; private set; }
    public int ScheduleUiWorldInputLeakCount { get; private set; }

    public void Initialize(WorldManagerV2 manager)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _manager = manager;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = GameplayUiThemeV3.Shared;
        BuildInterface();
        GameplaySessionV3.SessionBegan += OnSessionBegan;
        _sessionSubscribed = true;
        BindCurrentSessions();
        SetWorldMapBlocked(manager.WorldMapOverlayVisible);

#if DEBUG
        if (MercenaryScheduleUiSelfCheckV3.TryValidate(out string reason))
        {
            GD.Print("[MercenaryScheduleUiV3] Self-check PASS");
        }
        else
        {
            GD.PushError($"[MercenaryScheduleUiV3] Self-check FAIL: {reason}");
        }
#endif
    }

    public override void _ExitTree()
    {
        UnbindCurrentSessions();
        if (_sessionSubscribed)
        {
            GameplaySessionV3.SessionBegan -= OnSessionBegan;
            _sessionSubscribed = false;
        }
    }

    public void ToggleForSelectedMercenary(string mercenaryId)
    {
        if (_worldMapBlocked || !IsLocalSelectedMercenary(mercenaryId))
        {
            ClosePanel("InvalidTarget");
            return;
        }

        if (IsPanelOpen && _targetMercenaryId == mercenaryId)
        {
            ClosePanel("Toggle");
            GetViewport().SetInputAsHandled();
            return;
        }

        _targetMercenaryId = mercenaryId;
        _selectedTool = GetCurrentTargetState();
        _panel!.Visible = true;
        RefreshAll("Open");
        GetViewport().SetInputAsHandled();
    }

    public bool HandleEscape()
    {
        if (!IsPanelOpen)
        {
            return false;
        }

        ClosePanel("Escape");
        return true;
    }

    public void SetWorldMapBlocked(bool blocked)
    {
        _worldMapBlocked = blocked;
        if (blocked)
        {
            ClosePanel("WorldMap");
        }
    }

    public void BindCurrentSession()
    {
        if (!_initialized)
        {
            return;
        }

        UnbindCurrentSessions();
        BindCurrentSessions();
    }

    public void PrintDebugDiagnostics()
    {
        GD.Print($"[MercenaryScheduleUiV3] SchedulePanelOpen={IsPanelOpen} " +
            $"SchedulePanelTargetMercenaryId={SchedulePanelTargetMercenaryId} " +
            $"SchedulePanelRootCount={SchedulePanelRootCount} " +
            $"ScheduleSlotButtonCount={ScheduleSlotButtonCount} " +
            $"SchedulePresetButtonCount={SchedulePresetButtonCount} " +
            $"ScheduleStateToolButtonCount={ScheduleStateToolButtonCount} " +
            $"ScheduleFullRefreshCount={ScheduleFullRefreshCount} " +
            $"SchedulePartialRefreshCount={SchedulePartialRefreshCount} " +
            $"ScheduleEventCoalescedCount={ScheduleEventCoalescedCount} " +
            $"SchedulePanelDuplicateSubscriptionCount={SchedulePanelDuplicateSubscriptionCount} " +
            $"ScheduleUiWorldInputLeakCount={ScheduleUiWorldInputLeakCount}");
    }

    private void BuildInterface()
    {
        _layoutRoot = new MarginContainer
        {
            Name = "MercenaryScheduleLayout",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _layoutRoot.AnchorLeft = 0.55f;
        _layoutRoot.AnchorRight = 1f;
        _layoutRoot.AnchorTop = 0f;
        _layoutRoot.AnchorBottom = 1f;
        _layoutRoot.OffsetLeft = 0;
        _layoutRoot.OffsetRight = -12;
        _layoutRoot.OffsetTop = 92;
        _layoutRoot.OffsetBottom = -74;
        AddChild(_layoutRoot);

        _panel = new PanelContainer
        {
            Name = "MercenarySchedulePanel",
            MouseFilter = MouseFilterEnum.Stop
        };
        _panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
        _panel.GuiInput += OnPanelGuiInput;
        _layoutRoot.AddChild(_panel);

        MarginContainer panelMargin = new();
        panelMargin.AddThemeConstantOverride("margin_left", 10);
        panelMargin.AddThemeConstantOverride("margin_right", 10);
        panelMargin.AddThemeConstantOverride("margin_top", 8);
        panelMargin.AddThemeConstantOverride("margin_bottom", 8);
        _panel.AddChild(panelMargin);

        ScrollContainer scroll = new()
        {
            Name = "ScheduleScrollContainer",
            MouseFilter = MouseFilterEnum.Stop,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        scroll.GuiInput += OnPanelGuiInput;
        panelMargin.AddChild(scroll);

        VBoxContainer content = new()
        {
            Name = "ScheduleContent",
            CustomMinimumSize = new Vector2(500, 360),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 5);
        scroll.AddChild(content);

        BuildHeader(content);
        BuildSummary(content);
        BuildPresetRow(content);
        BuildStateToolRow(content);
        BuildSlotGrid(content);
        _statusLabel = MakeLabel(string.Empty, 11, new Color(0.72f, 0.84f, 0.9f));
        _statusLabel.Name = "ScheduleStatus";
        content.AddChild(_statusLabel);
        Label help = MakeLabel("\uC2AC\uB86F\uC744 \uD074\uB9AD\uD574 \uD55C \uC2DC\uAC04\uC758 \uC0C1\uD0DC\uB97C \uBCC0\uACBD\uD569\uB2C8\uB2E4.", 10, new Color(0.58f, 0.66f, 0.7f));
        help.Name = "ScheduleHelp";
        content.AddChild(help);

        _panel.Visible = false;
    }

    private void BuildHeader(VBoxContainer content)
    {
        HBoxContainer header = new() { Name = "ScheduleHeader" };
        Label title = MakeLabel("\uC6A9\uBCD1 \uC2DC\uAC04\uD45C", 17);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _targetLabel = MakeLabel(string.Empty, 11, new Color(0.68f, 0.78f, 0.82f));
        _targetLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _targetLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Button close = new()
        {
            Name = "CloseScheduleButton",
            Text = "\uB2EB\uAE30",
            CustomMinimumSize = new Vector2(52, 26),
            MouseFilter = MouseFilterEnum.Stop
        };
        close.Pressed += () =>
        {
            ClosePanel("CloseButton");
            GetViewport().SetInputAsHandled();
        };
        header.AddChild(title);
        header.AddChild(_targetLabel);
        header.AddChild(close);
        content.AddChild(header);
    }

    private void BuildSummary(VBoxContainer content)
    {
        _summaryLabel = MakeLabel(string.Empty, 11, new Color(0.82f, 0.86f, 0.88f));
        _summaryLabel.Name = "ScheduleSummary";
        _summaryLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        content.AddChild(_summaryLabel);
        content.AddChild(MakeSeparator());
    }

    private void BuildPresetRow(VBoxContainer content)
    {
        HBoxContainer row = new() { Name = "PresetRow" };
        row.AddThemeConstantOverride("separation", 4);
        for (int index = 0; index < PresetButtonCount; index++)
        {
            int captured = index;
            Button button = new()
            {
                Name = $"PresetButton{index}",
                Text = index == PresetButtonCount - 1 ? "\uAE30\uBCF8\uAC12 \uBCF5\uC6D0" : PresetLabel(Presets[index]),
                ToggleMode = true,
                CustomMinimumSize = new Vector2(0, 28),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Stop
            };
            button.Pressed += () => OnPresetPressed(captured);
            _presetButtons[index] = button;
            row.AddChild(button);
        }

        content.AddChild(row);
    }

    private void BuildStateToolRow(VBoxContainer content)
    {
        HBoxContainer row = new() { Name = "StateToolRow" };
        row.AddThemeConstantOverride("separation", 4);
        for (int index = 0; index < StateToolButtonCount; index++)
        {
            int captured = index;
            MercenaryScheduleStateV3 state = StateTools[index];
            Button button = new()
            {
                Name = $"StateToolButton{state}",
                Text = $"{StateLetter(state)}  {StateLabel(state)}",
                ToggleMode = true,
                CustomMinimumSize = new Vector2(0, 28),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Stop
            };
            button.Pressed += () => OnStateToolPressed(captured);
            _stateToolButtons[index] = button;
            row.AddChild(button);
        }

        content.AddChild(row);
    }

    private void BuildSlotGrid(VBoxContainer content)
    {
        GridContainer grid = new()
        {
            Name = "ScheduleHourGrid",
            Columns = GridColumns,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 4);
        BuildSlotStyles();
        for (int hour = 0; hour < SlotCount; hour++)
        {
            int captured = hour;
            Button button = new()
            {
                Name = $"HourSlot{hour:00}",
                CustomMinimumSize = new Vector2(36, 48),
                MouseFilter = MouseFilterEnum.Stop,
                Alignment = HorizontalAlignment.Center
            };
            button.Pressed += () => OnSlotPressed(captured);
            _slotButtons[hour] = button;
            grid.AddChild(button);
        }

        content.AddChild(grid);
    }

    private void BuildSlotStyles()
    {
        _slotStyles[MercenaryScheduleStateV3.Work] = CreateSlotStyle(new Color(0.18f, 0.32f, 0.48f, 0.96f), false);
        _slotStyles[MercenaryScheduleStateV3.Anything] = CreateSlotStyle(new Color(0.19f, 0.4f, 0.28f, 0.96f), false);
        _slotStyles[MercenaryScheduleStateV3.Recreation] = CreateSlotStyle(new Color(0.38f, 0.25f, 0.43f, 0.96f), false);
        _slotStyles[MercenaryScheduleStateV3.Sleep] = CreateSlotStyle(new Color(0.14f, 0.22f, 0.36f, 0.96f), false);
        _currentSlotStyles[MercenaryScheduleStateV3.Work] = CreateSlotStyle(new Color(0.18f, 0.32f, 0.48f, 0.96f), true);
        _currentSlotStyles[MercenaryScheduleStateV3.Anything] = CreateSlotStyle(new Color(0.19f, 0.4f, 0.28f, 0.96f), true);
        _currentSlotStyles[MercenaryScheduleStateV3.Recreation] = CreateSlotStyle(new Color(0.38f, 0.25f, 0.43f, 0.96f), true);
        _currentSlotStyles[MercenaryScheduleStateV3.Sleep] = CreateSlotStyle(new Color(0.14f, 0.22f, 0.36f, 0.96f), true);
    }

    private void BindCurrentSessions()
    {
        if (!GameplaySessionV3.TryGetControlSession(out MercenaryControlSessionV3? control) || control == null
            || !GameplaySessionV3.TryGetMercenarySession(out MercenarySessionV3? mercenaries) || mercenaries == null
            || !GameplaySessionV3.TryGetMercenarySchedule(out MercenaryScheduleSessionV3? schedules) || schedules == null
            || !GameplaySessionV3.TryGetSimulationClock(out SimulationClockSessionV3? clock) || clock == null)
        {
            return;
        }

        _control = control;
        _mercenaries = mercenaries;
        _schedules = schedules;
        _clock = clock;
        _control.Selection.SelectionChanged += OnSelectionChanged;
        _schedules.ScheduleChanged += OnScheduleChanged;
        _schedules.CurrentScheduleStateChanged += OnCurrentScheduleStateChanged;
        _schedules.SchedulePresetApplied += OnSchedulePresetApplied;
        _schedules.ScheduleRemoved += OnScheduleRemoved;
        _clock.HourChanged += OnClockHourChanged;
        _mercenaries.Registry.MercenaryRemoved += OnMercenaryRemoved;
    }

    private void UnbindCurrentSessions()
    {
        if (_control != null) _control.Selection.SelectionChanged -= OnSelectionChanged;
        if (_schedules != null)
        {
            _schedules.ScheduleChanged -= OnScheduleChanged;
            _schedules.CurrentScheduleStateChanged -= OnCurrentScheduleStateChanged;
            _schedules.SchedulePresetApplied -= OnSchedulePresetApplied;
            _schedules.ScheduleRemoved -= OnScheduleRemoved;
        }

        if (_clock != null) _clock.HourChanged -= OnClockHourChanged;
        if (_mercenaries != null) _mercenaries.Registry.MercenaryRemoved -= OnMercenaryRemoved;
        _control = null;
        _mercenaries = null;
        _schedules = null;
        _clock = null;
    }

    private void OnSessionBegan()
    {
        UnbindCurrentSessions();
        CancelQueuedRefresh();
        _targetMercenaryId = string.Empty;
        if (_panel != null) _panel.Visible = false;
        _statusLabel!.Text = string.Empty;
        BindCurrentSessions();
    }

    private void OnSelectionChanged()
    {
        if (_control == null)
        {
            return;
        }

        IReadOnlyList<string> selected = _control.Selection.GetSelectedIds();
        if (selected.Count != 1 || !IsLocalSelectedMercenary(selected[0]))
        {
            if (IsPanelOpen) ClosePanel("SelectionChanged");
            return;
        }

        if (IsPanelOpen && _targetMercenaryId != selected[0])
        {
            _targetMercenaryId = selected[0];
            _selectedTool = GetCurrentTargetState();
            QueueRefresh(true, null, "SelectionChanged");
        }
    }

    private void OnMercenaryRemoved(string mercenaryId, string companyId)
    {
        if (mercenaryId == _targetMercenaryId)
        {
            ClosePanel("MercenaryRemoved");
        }
    }

    private void OnScheduleChanged(MercenaryScheduleEventV3 value)
    {
        if (value.MercenaryId != _targetMercenaryId) return;
        QueueRefresh(value.ChangedHour == null, value.ChangedHour, "ScheduleChanged");
    }

    private void OnCurrentScheduleStateChanged(MercenaryScheduleEventV3 value)
    {
        if (value.MercenaryId != _targetMercenaryId) return;
        QueueRefresh(false, value.ChangedHour, "CurrentStateChanged");
    }

    private void OnSchedulePresetApplied(MercenaryScheduleEventV3 value)
    {
        if (value.MercenaryId == _targetMercenaryId) QueueRefresh(true, null, "PresetApplied");
    }

    private void OnScheduleRemoved(MercenaryScheduleEventV3 value)
    {
        if (value.MercenaryId == _targetMercenaryId) ClosePanel("ScheduleRemoved");
    }

    private void OnClockHourChanged(SimulationClockEventV3 value)
    {
        if (!IsPanelOpen || string.IsNullOrEmpty(_targetMercenaryId)) return;
        QueueRefresh(false, value.CurrentHour, "HourChanged");
        QueueRefresh(false, value.PreviousHour, "HourChanged");
    }

    private void QueueRefresh(bool full, int? hour, string reason)
    {
        if (!IsPanelOpen) return;
        if (full) _pendingFullRefresh = true;
        if (hour.HasValue && hour.Value is >= 0 and < SlotCount) _pendingPartialHours.Add(hour.Value);
        if (_refreshQueued)
        {
            ScheduleEventCoalescedCount++;
            return;
        }

        _refreshQueued = true;
        CallDeferred(MethodName.RefreshDeferred);
    }

    private void RefreshDeferred()
    {
        _refreshQueued = false;
        if (!IsPanelOpen) return;
        bool full = _pendingFullRefresh;
        HashSet<int> hours = new(_pendingPartialHours);
        _pendingFullRefresh = false;
        _pendingPartialHours.Clear();
        if (full) RefreshAll("Deferred");
        else if (hours.Count > 0) RefreshPartial(hours, "Deferred");
    }

    private void OnStateToolPressed(int index)
    {
        if (index < 0 || index >= StateToolButtonCount) return;
        _selectedTool = StateTools[index];
        for (int i = 0; i < _stateToolButtons.Length; i++)
            if (_stateToolButtons[i] != null) _stateToolButtons[i].ButtonPressed = i == index;
        SetStatus($"{StateLabel(_selectedTool)} \uC0C1\uD0DC\uB97C \uC120\uD0DD\uD588\uC2B5\uB2C8\uB2E4.", true);
        GetViewport().SetInputAsHandled();
    }

    private void OnSlotPressed(int hour)
    {
        if (!TryGetTarget(out _) || _schedules == null) return;
        bool noOp = _schedules.TryGetSlot(_targetMercenaryId, hour, out MercenaryScheduleStateV3 previous)
            && previous == _selectedTool;
        if (!_schedules.TrySetHourSlot(_targetMercenaryId, hour, _selectedTool, out string reason))
        {
            SetStatus(ReasonText(reason), false);
            return;
        }

        CancelQueuedRefresh();
        SetStatus($"{hour:00}\uC2DC\uB97C {StateLabel(_selectedTool)}\uC73C\uB85C \uBCC0\uACBD\uD588\uC2B5\uB2C8\uB2E4.", true);
        if (!noOp) RefreshPartial(new HashSet<int> { hour }, "SlotEdited");
        GetViewport().SetInputAsHandled();
    }

    private void OnPresetPressed(int index)
    {
        if (!TryGetTarget(out MercenaryScheduleSnapshotV3? snapshot) || _schedules == null) return;
        bool noOp = index < PresetButtonCount - 1 && snapshot!.Preset == Presets[index];
        bool ok = index == PresetButtonCount - 1
            ? _schedules.TryResetDefault(_targetMercenaryId, out string reason)
            : _schedules.TryApplyPreset(_targetMercenaryId, Presets[index], out reason);
        if (!ok)
        {
            SetStatus(ReasonText(reason), false);
            return;
        }

        CancelQueuedRefresh();
        string label = index == PresetButtonCount - 1 ? "\uAE30\uBCF8\uAC12" : PresetLabel(Presets[index]);
        SetStatus($"{label}\uD504\uB9AC\uC14B\uC744 \uC801\uC6A9\uD588\uC2B5\uB2C8\uB2E4.", true);
        if (!noOp) RefreshAll("PresetEdited");
        GetViewport().SetInputAsHandled();
    }

    private void RefreshAll(string reason)
    {
        if (!TryGetTarget(out MercenaryScheduleSnapshotV3? snapshot) || snapshot == null || _schedules == null)
        {
            ClosePanel("TargetUnavailable");
            return;
        }

        ScheduleFullRefreshCount++;
        UpdateTargetAndSummary(snapshot);
        for (int hour = 0; hour < SlotCount; hour++) RefreshSlot(snapshot, hour);
        UpdateToolButtons();
        UpdatePresetButtons(snapshot.Preset);
    }

    private void RefreshPartial(HashSet<int> hours, string reason)
    {
        if (!TryGetTarget(out MercenaryScheduleSnapshotV3? snapshot) || snapshot == null) return;
        SchedulePartialRefreshCount++;
        foreach (int hour in hours) RefreshSlot(snapshot, hour);
        UpdateTargetAndSummary(snapshot);
        UpdateToolButtons();
        UpdatePresetButtons(snapshot.Preset);
    }

    private void UpdateTargetAndSummary(MercenaryScheduleSnapshotV3 snapshot)
    {
        string name = _targetMercenaryId;
        if (_mercenaries?.Registry.TryGetProfile(_targetMercenaryId, out MercenaryProfileV3? profile) == true && profile != null)
            name = profile.DisplayName;
        _targetLabel!.Text = $"{name}  ·  {ShortId(_targetMercenaryId)}";
        SimulationClockSnapshotV3 clock = _clock?.GetSnapshot() ?? default;
        MercenarySchedulePolicyV3 policy = _schedules!.GetCurrentPolicy(_targetMercenaryId);
        string next = snapshot.NextTransitionHour < 0 ? "\uC5C6\uC74C" : $"{snapshot.NextTransitionHour:00}:00";
        _summaryLabel!.Text =
            $"\uD604\uC7AC {clock.Hour:00}:{clock.Minute:00} · {StateLabel(snapshot.CurrentState)}  |  \uB2E4\uC74C \uC804\uD658 {next}\n" +
            $"\uD504\uB9AC\uC14B: {PresetLabel(snapshot.Preset)}  |  \uC790\uB3D9 \uC791\uC5C5: {OnOff(policy.AutomaticJobEligible)}  |  \uC608\uC57D \uC218\uBA74: {OnOff(policy.WantsScheduledRest)}  |  \uC624\uB77D \uC758\uB3C4: {OnOff(policy.RecreationIntent)}";
    }

    private void RefreshSlot(MercenaryScheduleSnapshotV3 snapshot, int hour)
    {
        if (_schedules == null || hour is < 0 or >= SlotCount || _slotButtons[hour] == null) return;
        if (!_schedules.TryGetSlot(_targetMercenaryId, hour, out MercenaryScheduleStateV3 state)) return;
        int next = (hour + 1) % SlotCount;
        Button button = _slotButtons[hour];
        button.Text = $"{hour:00}\n{StateLetter(state)}";
        button.TooltipText = $"{hour:00}:00~{next:00}:00 · {StateLabel(state)}";
        bool current = hour == snapshot.CurrentHour;
        StyleBoxFlat style = current ? _currentSlotStyles[state] : _slotStyles[state];
        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", style);
        button.AddThemeStyleboxOverride("pressed", style);
        button.AddThemeStyleboxOverride("disabled", style);
    }

    private void UpdateToolButtons()
    {
        for (int index = 0; index < StateTools.Length; index++)
            _stateToolButtons[index].ButtonPressed = StateTools[index] == _selectedTool;
    }

    private void UpdatePresetButtons(MercenarySchedulePresetV3 preset)
    {
        for (int index = 0; index < PresetButtonCount; index++)
        {
            bool active = index < PresetButtonCount - 1 && Presets[index] == preset;
            _presetButtons[index].ButtonPressed = active;
            _presetButtons[index].Disabled = active;
        }

        _presetButtons[PresetButtonCount - 1].Disabled = false;
        _presetButtons[PresetButtonCount - 1].ButtonPressed = false;
    }

    private bool TryGetTarget(out MercenaryScheduleSnapshotV3? snapshot)
    {
        snapshot = null;
        return _schedules != null && !string.IsNullOrEmpty(_targetMercenaryId)
            && IsLocalSelectedMercenary(_targetMercenaryId)
            && _schedules.TryGetSchedule(_targetMercenaryId, out snapshot)
            && snapshot != null;
    }

    private bool IsLocalSelectedMercenary(string mercenaryId)
    {
        if (_control == null || _mercenaries == null || _schedules == null || _manager == null
            || _control.Selection.Count != 1 || !_control.Selection.Contains(mercenaryId)
            || !_mercenaries.Registry.TryGetState(mercenaryId, out MercenaryStateV3? state) || state == null
            || state.CompanyId != _manager.LocalCompanyId)
        {
            return false;
        }

        return _schedules.TryGetSchedule(mercenaryId, out _);
    }

    private MercenaryScheduleStateV3 GetCurrentTargetState()
    {
        return _schedules != null && _schedules.TryGetCurrentState(_targetMercenaryId, out MercenaryScheduleStateV3 state)
            ? state
            : MercenaryScheduleStateV3.Work;
    }

    public void ClosePanel(string reason)
    {
        CancelQueuedRefresh();
        _panel!.Visible = false;
        _targetMercenaryId = string.Empty;
        _statusLabel!.Text = string.Empty;
    }

    private void CancelQueuedRefresh()
    {
        _refreshQueued = false;
        _pendingFullRefresh = false;
        _pendingPartialHours.Clear();
    }

    private void OnPanelGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton or InputEventMouseMotion)
        {
            GetViewport().SetInputAsHandled();
        }
    }

    private void SetStatus(string message, bool success)
    {
        _statusLabel!.Text = message;
        _statusLabel.AddThemeColorOverride("font_color", success ? new Color(0.62f, 0.86f, 0.72f) : new Color(0.96f, 0.58f, 0.52f));
    }

    private static StyleBoxFlat CreatePanelStyle() => new()
    {
        BgColor = new Color(0.035f, 0.043f, 0.05f, 0.98f),
        BorderColor = new Color(0.28f, 0.34f, 0.38f, 0.98f),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 4,
        CornerRadiusTopRight = 4,
        CornerRadiusBottomLeft = 4,
        CornerRadiusBottomRight = 4
    };

    private static StyleBoxFlat CreateSlotStyle(Color color, bool current) => new()
    {
        BgColor = color,
        BorderColor = current ? new Color(0.98f, 0.86f, 0.36f, 1f) : new Color(0.26f, 0.31f, 0.36f, 0.9f),
        BorderWidthLeft = current ? 2 : 1,
        BorderWidthTop = current ? 2 : 1,
        BorderWidthRight = current ? 2 : 1,
        BorderWidthBottom = current ? 2 : 1,
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3,
        CornerRadiusBottomLeft = 3,
        CornerRadiusBottomRight = 3,
        ContentMarginLeft = 2,
        ContentMarginRight = 2,
        ContentMarginTop = 2,
        ContentMarginBottom = 2
    };

    private static Label MakeLabel(string text, int fontSize, Color? color = null)
    {
        Label label = new() { Text = text, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        if (color.HasValue) label.AddThemeColorOverride("font_color", color.Value);
        return label;
    }

    private static HSeparator MakeSeparator() => new() { MouseFilter = MouseFilterEnum.Ignore };

    public static string StateLetter(MercenaryScheduleStateV3 state) => state switch
    {
        MercenaryScheduleStateV3.Work => "W",
        MercenaryScheduleStateV3.Anything => "A",
        MercenaryScheduleStateV3.Recreation => "R",
        MercenaryScheduleStateV3.Sleep => "S",
        _ => "?"
    };

    public static string StateLabel(MercenaryScheduleStateV3 state) => state switch
    {
        MercenaryScheduleStateV3.Work => "\uC791\uC5C5",
        MercenaryScheduleStateV3.Anything => "\uC790\uC720",
        MercenaryScheduleStateV3.Recreation => "\uC624\uB77D",
        MercenaryScheduleStateV3.Sleep => "\uC218\uBA74",
        _ => "?"
    };

    public static string PresetLabel(MercenarySchedulePresetV3 preset) => preset switch
    {
        MercenarySchedulePresetV3.Standard => "\uD45C\uC900",
        MercenarySchedulePresetV3.DayShift => "\uC8FC\uAC04",
        MercenarySchedulePresetV3.NightShift => "\uC57C\uAC04",
        MercenarySchedulePresetV3.Free => "\uC790\uC720",
        MercenarySchedulePresetV3.Custom => "\uC0AC\uC6A9\uC790 \uC9C0\uC815",
        _ => "?"
    };

    private static string ReasonText(string reason) => reason switch
    {
        "MercenaryNotRegistered" => "\uC120\uD0DD\uD55C \uC6A9\uBCD1\uC744 \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.",
        "InvalidHour" => "\uC798\uBABB\uB41C \uC2DC\uAC04\uC785\uB2C8\uB2E4.",
        "InvalidScheduleState" => "\uC9C0\uC6D0\uD558\uC9C0 \uC54A\uB294 \uC0C1\uD0DC\uC785\uB2C8\uB2E4.",
        "InvalidPreset" => "\uC9C0\uC6D0\uD558\uC9C0 \uC54A\uB294 \uD504\uB9AC\uC14B\uC785\uB2C8\uB2E4.",
        _ => string.IsNullOrEmpty(reason) ? "\uC2DC\uAC04\uD45C\uB97C \uBCC0\uACBD\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4." : reason
    };
    private static string OnOff(bool value) => value ? "\uD65C\uC131" : "\uBE44\uD65C\uC131";
    private static string ShortId(string id) => string.IsNullOrEmpty(id) ? "-" : id.Length <= 12 ? id : id[..12];
    private static int CountCreated(Button[] buttons)
    {
        int count = 0;
        foreach (Button button in buttons) if (button != null) count++;
        return count;
    }
}
