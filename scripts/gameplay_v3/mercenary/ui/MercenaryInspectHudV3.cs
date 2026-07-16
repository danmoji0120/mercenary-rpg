using System;
using System.Collections.Generic;
using GameplayV3.Construction.Runtime;
using GameplayV3.Control;
using GameplayV3.Work;
using GameplayV3.Needs;
using GameplayV3.Needs.Runtime;
using GameplayV3.Farming.Runtime;
using Godot;
using WorldV2;

namespace GameplayV3.Mercenary.UI;

public partial class MercenaryInspectHudV3 : Godot.Control
{
    public const float RefreshIntervalSeconds = 0.1f;
    public const int MaximumVisibleSelectionRows = 8;
    private const float PanelWidth = 600f;
    private const float SinglePanelHeight = 300f;
    private const float MultiplePanelHeight = 222f;
    private const float BottomOffset = 88f;

    private MercenaryControlSessionV3? _control;
    private MercenaryInspectHudSnapshotBuilderV3? _snapshotBuilder;
    private WorldManagerV2? _manager;
    private PanelContainer? _panel;
    private VBoxContainer? _singleView;
    private VBoxContainer? _multipleView;
    private Label? _nameLabel;
    private Label? _statusLabel;
    private Label? _companyLabel;
    private Label? _workLabel;
    private Label? _targetLabel;
    private ProgressBar? _workProgress;
    private Label? _workProgressText;
    private readonly ProgressBar[] _conditionBars = new ProgressBar[4];
    private readonly Label[] _conditionValues = new Label[4];
    private Label? _conditionDetail;
    private Label? _attributeLabel;
    private Label? _skillLabel;
    private Label? _derivedLabel;
    private Label? _multipleTitle;
    private Label? _multipleSummary;
    private Label? _multipleHighestSkills;
    private VBoxContainer? _multipleRows;
    private readonly Button[] _selectionButtons = new Button[MaximumVisibleSelectionRows];
    private readonly string[] _selectionButtonIds = new string[MaximumVisibleSelectionRows];
    private Button? _cancelButton;
    private Label? _bedLabel;private Button? _assignBedButton;private Button? _unassignBedButton;private Button? _restButton;private Label? _foodLabel;private Button? _eatButton;private Button? _workPriorityButton;private MercenaryNeedsSessionV3? _needs;private RestWorkCoordinatorV3? _restWork;private EatingWorkCoordinatorV3? _eatingWork;
    private double _refreshAccumulator;
    private bool _worldMapBlocked;

    public MercenaryInspectHudModeV3 Mode { get; private set; }
    public int RefreshCount { get; private set; }
    public string LastRefreshReason { get; private set; } = "NotInitialized";
    public string LastAction { get; private set; } = string.Empty;
    public string DisplayedMercenaryId { get; private set; } = string.Empty;
    public string DisplayedWorkType { get; private set; } = string.Empty;
    public string DisplayedWorkPhase { get; private set; } = string.Empty;
    public string DisplayedCarry { get; private set; } = string.Empty;
    public float DisplayedProgress { get; private set; }
    public bool WorldMapBlocked => _worldMapBlocked;
    public event Action<string>? BedAssignmentRequested;
    public event Action<string>? WorkPriorityRequested;

    public void Initialize(
        MercenaryControlSessionV3 control,
        MercenarySessionV3 mercenaries,
        MercenaryWorkSessionV3 work,
        WorldManagerV2 manager,
        ConstructionWorkCoordinatorV3? construction,
        DemolitionWorkCoordinatorV3? demolition,
        MercenaryNeedsSessionV3? needs=null,RestWorkCoordinatorV3? restWork=null,EatingWorkCoordinatorV3? eatingWork=null,FarmingWorkCoordinatorV3? farmingWork=null)
    {
        _control = control;
        _manager = manager;
        _needs=needs;_restWork=restWork;_eatingWork=eatingWork;
        _snapshotBuilder = new MercenaryInspectHudSnapshotBuilderV3(
            mercenaries,
            control,
            work,
            manager.LocalCompanyName,
            needs==null?new PlaceholderMercenaryConditionSnapshotProviderV3():new MixedMercenaryConditionSnapshotProviderV3(needs),
            construction,
            demolition,
            eatingWork,
            farmingWork);
        BuildInterface();
        _control.Selection.SelectionChanged += OnSelectionChanged;
        if (MercenaryConditionSelfCheckV3.TryValidate(out string reason))
        {
            GD.Print("[MercenaryInspectHudV3] Condition self-check PASS");
        }
        else
        {
            GD.PushError($"[MercenaryInspectHudV3] Condition self-check FAIL: {reason}");
        }

        Refresh("Initialize");
    }

    public override void _ExitTree()
    {
        if (_control != null)
        {
            _control.Selection.SelectionChanged -= OnSelectionChanged;
        }
    }

    public override void _Process(double delta)
    {
        if (_worldMapBlocked || _control == null || _control.Selection.Count == 0)
        {
            return;
        }

        _refreshAccumulator += delta;
        if (_refreshAccumulator < RefreshIntervalSeconds)
        {
            return;
        }

        _refreshAccumulator %= RefreshIntervalSeconds;
        Refresh("10HzSelectedRefresh");
    }

    public void SetWorldMapBlocked(bool blocked)
    {
        _worldMapBlocked = blocked;
        if (blocked)
        {
            Visible = false;
            LastAction = "HiddenByWorldMap";
            PushDiagnostics(null, "WorldMapBlocked");
            return;
        }

        Refresh("WorldMapClosed");
    }

    private void BuildInterface()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _panel = new PanelContainer
        {
            Name = "MercenaryInspectPanel",
            MouseFilter = MouseFilterEnum.Stop
        };
        _panel.SetAnchorsPreset(LayoutPreset.BottomLeft);
        SetPanelBounds(SinglePanelHeight);
        StyleBoxFlat panelStyle = new()
        {
            BgColor = new Color(0.035f, 0.043f, 0.05f, 0.97f),
            BorderColor = new Color(0.28f, 0.31f, 0.33f, 0.95f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 9,
            ContentMarginRight = 9,
            ContentMarginTop = 7,
            ContentMarginBottom = 7
        };
        _panel.AddThemeStyleboxOverride("panel", panelStyle);
        _panel.GuiInput += OnPanelGuiInput;
        AddChild(_panel);

        MarginContainer margin = new();
        _panel.AddChild(margin);
        _singleView = BuildSingleView();
        _multipleView = BuildMultipleView();
        margin.AddChild(_singleView);
        margin.AddChild(_multipleView);
        _panel.Visible = false;
    }

    private VBoxContainer BuildSingleView()
    {
        VBoxContainer root = new() { Name = "SingleMercenaryView" };
        root.AddThemeConstantOverride("separation", 3);

        HBoxContainer header = new();
        _nameLabel = MakeLabel(string.Empty, 17);
        _nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _statusLabel = MakeLabel(string.Empty, 12, new Color(0.78f, 0.84f, 0.88f));
        _cancelButton = new Button { Text = "×", CustomMinimumSize = new Vector2(28, 24), TooltipText = "현재 명령 또는 작업 취소", MouseFilter = MouseFilterEnum.Stop };
        _cancelButton.AddThemeFontSizeOverride("font_size", 15);
        _cancelButton.Pressed += OnCancelPressed;
        header.AddChild(_nameLabel);
        header.AddChild(_statusLabel);
        header.AddChild(_cancelButton);
        root.AddChild(header);

        _companyLabel = MakeLabel(string.Empty, 11, new Color(0.58f, 0.64f, 0.68f));
        root.AddChild(_companyLabel);
        root.AddChild(MakeSeparator());

        _workLabel = MakeLabel(string.Empty, 12);
        _targetLabel = MakeLabel(string.Empty, 11, new Color(0.67f, 0.72f, 0.75f));
        root.AddChild(_workLabel);
        root.AddChild(_targetLabel);
        HBoxContainer workProgressRow = new();
        _workProgress = MakeBar(new Color(0.31f, 0.65f, 0.91f), 8);
        _workProgress.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _workProgressText = MakeLabel(string.Empty, 10, new Color(0.72f, 0.76f, 0.79f));
        _workProgressText.CustomMinimumSize = new Vector2(72, 0);
        workProgressRow.AddChild(_workProgress);
        workProgressRow.AddChild(_workProgressText);
        root.AddChild(workProgressRow);
        root.AddChild(MakeSeparator());

        HBoxContainer columns = new();
        columns.AddThemeConstantOverride("separation", 12);
        VBoxContainer conditions = new() { CustomMinimumSize = new Vector2(258, 0) };
        conditions.AddThemeConstantOverride("separation", 2);
        _conditionDetail = MakeLabel(string.Empty, 11);
        conditions.AddChild(_conditionDetail);
        AddConditionRow(conditions, 0, "건강", new Color(0.35f, 0.82f, 0.38f));
        AddConditionRow(conditions, 1, "포만", new Color(0.66f, 0.72f, 0.22f));
        AddConditionRow(conditions, 2, "휴식", new Color(0.76f, 0.55f, 0.16f));
        AddConditionRow(conditions, 3, "기분", new Color(0.50f, 0.65f, 0.20f));
        columns.AddChild(conditions);

        VBoxContainer stats = new();
        stats.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        HBoxContainer statColumns = new();
        _attributeLabel = MakeLabel(string.Empty, 11);
        _attributeLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _skillLabel = MakeLabel(string.Empty, 11);
        _skillLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        statColumns.AddChild(_attributeLabel);
        statColumns.AddChild(_skillLabel);
        stats.AddChild(statColumns);
        _derivedLabel = MakeLabel(string.Empty, 10, new Color(0.64f, 0.72f, 0.78f));
        stats.AddChild(_derivedLabel);
        columns.AddChild(stats);
        root.AddChild(columns);
        HBoxContainer restFooter=new(){Name="RestFooter"};restFooter.AddThemeConstantOverride("separation",4);_bedLabel=MakeLabel("침대: 미배정",11,new Color(.72f,.76f,.8f));_bedLabel.SizeFlagsHorizontal=SizeFlags.ExpandFill;restFooter.AddChild(_bedLabel);_assignBedButton=new Button{Text="배정/재배정",CustomMinimumSize=new Vector2(86,24),MouseFilter=MouseFilterEnum.Stop};_assignBedButton.Pressed+=()=>{LastAction="BedAssignmentRequested";BedAssignmentRequested?.Invoke(DisplayedMercenaryId);GetViewport().SetInputAsHandled();};restFooter.AddChild(_assignBedButton);_unassignBedButton=new Button{Text="해제",CustomMinimumSize=new Vector2(42,24),MouseFilter=MouseFilterEnum.Stop};_unassignBedButton.Pressed+=OnUnassignBed;restFooter.AddChild(_unassignBedButton);_restButton=new Button{Text="휴식",CustomMinimumSize=new Vector2(48,24),MouseFilter=MouseFilterEnum.Stop};_restButton.Pressed+=OnRestPressed;restFooter.AddChild(_restButton);root.AddChild(restFooter);HBoxContainer foodFooter=new(){Name="FoodFooter"};foodFooter.AddThemeConstantOverride("separation",4);_foodLabel=MakeLabel("식량: 비상식량 없음",11,new Color(.82f,.72f,.48f));_foodLabel.SizeFlagsHorizontal=SizeFlags.ExpandFill;foodFooter.AddChild(_foodLabel);_eatButton=new Button{Text="식사",CustomMinimumSize=new Vector2(48,24),MouseFilter=MouseFilterEnum.Stop};_eatButton.Pressed+=OnEatPressed;foodFooter.AddChild(_eatButton);_workPriorityButton=new Button{Text="작업 우선순위",CustomMinimumSize=new Vector2(92,24),MouseFilter=MouseFilterEnum.Stop};_workPriorityButton.Pressed+=()=>{LastAction="WorkPriorityRequested";WorkPriorityRequested?.Invoke(DisplayedMercenaryId);GetViewport().SetInputAsHandled();};foodFooter.AddChild(_workPriorityButton);root.AddChild(foodFooter);
        return root;
    }

    private VBoxContainer BuildMultipleView()
    {
        VBoxContainer root = new() { Name = "MultipleMercenaryView", Visible = false };
        root.AddThemeConstantOverride("separation", 3);
        _multipleTitle = MakeLabel("선택된 용병", 16);
        _multipleSummary = MakeLabel(string.Empty, 11, new Color(0.68f, 0.74f, 0.78f));
        root.AddChild(_multipleTitle);
        root.AddChild(_multipleSummary);
        root.AddChild(MakeSeparator());
        _multipleRows = new VBoxContainer();
        _multipleRows.AddThemeConstantOverride("separation", 1);
        for (int index = 0; index < MaximumVisibleSelectionRows; index++)
        {
            int captured = index;
            Button button = new()
            {
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(0, 20),
                MouseFilter = MouseFilterEnum.Stop,
                Visible = false
            };
            button.AddThemeFontSizeOverride("font_size", 11);
            button.Pressed += () => OnSelectionRowPressed(captured);
            _selectionButtons[index] = button;
            _multipleRows.AddChild(button);
        }

        root.AddChild(_multipleRows);
        _multipleHighestSkills = MakeLabel(string.Empty, 11, new Color(0.82f, 0.72f, 0.42f));
        root.AddChild(_multipleHighestSkills);
        return root;
    }

    private void AddConditionRow(VBoxContainer parent, int index, string title, Color fill)
    {
        HBoxContainer row = new();
        Label name = MakeLabel(title, 10);
        name.CustomMinimumSize = new Vector2(34, 0);
        _conditionBars[index] = MakeBar(fill, 8);
        _conditionBars[index].SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _conditionValues[index] = MakeLabel(string.Empty, 10);
        _conditionValues[index].CustomMinimumSize = new Vector2(34, 0);
        row.AddChild(name);
        row.AddChild(_conditionBars[index]);
        row.AddChild(_conditionValues[index]);
        parent.AddChild(row);
    }

    private static Label MakeLabel(string text, int fontSize, Color? color = null)
    {
        Label label = new() { Text = text, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        if (color.HasValue)
        {
            label.AddThemeColorOverride("font_color", color.Value);
        }

        return label;
    }

    private static HSeparator MakeSeparator()
    {
        HSeparator separator = new();
        separator.Modulate = new Color(0.55f, 0.58f, 0.60f, 0.38f);
        return separator;
    }

    private static ProgressBar MakeBar(Color fill, float height)
    {
        ProgressBar bar = new() { MinValue = 0, MaxValue = 100, ShowPercentage = false, CustomMinimumSize = new Vector2(0, height), MouseFilter = MouseFilterEnum.Ignore };
        bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.12f, 0.14f, 0.15f, 1f) });
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = fill });
        return bar;
    }

    private void OnSelectionChanged()
    {
        Refresh("SelectionChanged");
    }

    private void Refresh(string reason)
    {
        if (_control == null || _snapshotBuilder == null || _panel == null || _singleView == null || _multipleView == null)
        {
            return;
        }

        IReadOnlyList<string> selected = _control.Selection.GetSelectedIds();
        RefreshCount++;
        LastRefreshReason = reason;
        if (_worldMapBlocked || selected.Count == 0)
        {
            Mode = MercenaryInspectHudModeV3.None;
            Visible = !_worldMapBlocked;
            _panel.Visible = false;
            DisplayedMercenaryId = string.Empty;
            PushDiagnostics(null, reason);
            return;
        }

        Visible = true;
        _panel.Visible = true;
        if (selected.Count == 1 && _snapshotBuilder.TryBuild(selected[0], out MercenaryInspectHudSnapshotV3? snapshot) && snapshot != null)
        {
            Mode = MercenaryInspectHudModeV3.Single;
            _singleView.Visible = true;
            _multipleView.Visible = false;
            SetPanelBounds(SinglePanelHeight);
            RenderSingle(snapshot);
            PushDiagnostics(snapshot, reason);
            return;
        }

        Mode = MercenaryInspectHudModeV3.Multiple;
        _singleView.Visible = false;
        _multipleView.Visible = true;
        SetPanelBounds(MultiplePanelHeight);
        RenderMultiple(selected);
        PushDiagnostics(null, reason);
    }

    private void RenderSingle(MercenaryInspectHudSnapshotV3 value)
    {
        DisplayedMercenaryId = value.MercenaryId;
        DisplayedWorkType = value.WorkType;
        DisplayedWorkPhase = value.Phase;
        DisplayedCarry = value.Carry;
        DisplayedProgress = value.ProgressNormalized;
        _nameLabel!.Text = value.DisplayName;
        _statusLabel!.Text = value.Status;
        _companyLabel!.Text = $"{value.CompanyName}  ·  {ShortId(value.MercenaryId)}  ·  Cell {value.State.CurrentCell}";
        _workLabel!.Text = $"{value.WorkType} / {value.Phase}  ·  {value.CommandSource}";
        _targetLabel!.Text = $"대상 {value.Target}  ·  목적지 {(value.Destination?.ToString() ?? "-")}  ·  운반 {value.Carry}";
        _workProgress!.Visible = value.HasProgress;
        _workProgressText!.Visible = value.HasProgress;
        _workProgress.Value = value.ProgressNormalized * 100f;
        _workProgressText.Text = value.HasProgress ? $"{value.Progress:0.0}/{value.Required:0.0}" : string.Empty;
        _cancelButton!.Disabled = string.IsNullOrEmpty(value.WorkRequestId);
        RestAssignmentV3? bed=null;bool assigned=_needs!=null&&_needs.Assignments.TryGetByMercenary(value.MercenaryId,out bed)&&bed!=null;_bedLabel!.Text=assigned?$"침대: {ShortId(bed!.StructureId)} / 슬롯 {bed.SlotIndex}":"침대: 미배정";_unassignBedButton!.Disabled=!assigned;_restButton!.Disabled=!assigned||value.Condition.FatigueNormalized<=(_needs?.Settings.ManualRestMinimum??.2f);int ration=_eatingWork?.GetAvailableFoodAmount(GameplayV3.Resources.ResourceTypeV3.Ration)??0;int potato=_eatingWork?.GetAvailableFoodAmount(GameplayV3.Resources.ResourceTypeV3.Potato)??0;float hunger=_needs?.Hunger.GetHunger(value.MercenaryId)??value.Condition.HungerNormalized;string meal="";if(_eatingWork?.TryPreviewMeal(value.MercenaryId,out _,out var preview,out _)==true)meal=$" · 예정 {preview.PlannedUnits}개/{preview.PlannedCalories}kcal";_foodLabel!.Text=$"식량: 비상식량 {ration} / 감자 {potato} · {HungerPolicyV3.Stage(hunger)}{meal}";_eatButton!.Disabled=_eatingWork==null||ration+potato<1||hunger<(_needs?.HungerConfig.ManualEatMinimumHunger??.2f);

        MercenaryConditionSnapshotV3 condition = value.Condition;
        _conditionDetail!.Text = $"{condition.HealthSummary} · {condition.InjurySummary}\n{condition.TreatmentSummary}";
        SetCondition(0, condition.HealthNormalized);
        SetCondition(1, condition.FullnessNormalized);
        SetCondition(2, condition.RestNormalized);
        SetCondition(3, condition.MoraleNormalized);

        MercenaryAttributeSetV3 attributes = value.Profile.Attributes;
        _attributeLabel!.Text = "능력치\n" +
            $"근력 {attributes.Strength,2}\n기민 {attributes.Agility,2}\n체력 {attributes.Endurance,2}\n지능 {attributes.Intelligence,2}\n정신 {attributes.Mental,2}";
        IReadOnlyList<MercenaryWorkSkillValueV3> top = value.Profile.WorkSkills.GetTopSkills(2);
        HashSet<MercenaryWorkSkillTypeV3> topTypes = new();
        foreach (MercenaryWorkSkillValueV3 item in top) topTypes.Add(item.Type);
        MercenaryWorkSkillSetV3 skills = value.Profile.WorkSkills;
        _skillLabel!.Text = "숙련도\n" +
            SkillLine(MercenaryWorkSkillTypeV3.Hauling, skills.Hauling, topTypes) + "\n" +
            SkillLine(MercenaryWorkSkillTypeV3.Construction, skills.Construction, topTypes) + "\n" +
            SkillLine(MercenaryWorkSkillTypeV3.Gathering, skills.Gathering, topTypes) + "\n" +
            SkillLine(MercenaryWorkSkillTypeV3.Farming, skills.Farming, topTypes) + "\n" +
            SkillLine(MercenaryWorkSkillTypeV3.Production, skills.Production, topTypes) + "\n" +
            SkillLine(MercenaryWorkSkillTypeV3.Medicine, skills.Medicine, topTypes) + "\n" +
            SkillLine(MercenaryWorkSkillTypeV3.Guarding, skills.Guarding, topTypes);
        _derivedLabel!.Text = $"이동 ×{value.Derived.MoveSpeedMultiplier:0.00}  작업 ×{value.Derived.WorkSpeedMultiplier:0.00}  운반 {value.Derived.CarryCapacity:0.0}\n" +
            "상태 수치는 결정적 표시용 placeholder이며 gameplay에 영향 없음";
    }

    private void RenderMultiple(IReadOnlyList<string> selected)
    {
        DisplayedMercenaryId = string.Empty;
        DisplayedWorkType = "Multiple";
        DisplayedWorkPhase = string.Empty;
        DisplayedCarry = string.Empty;
        DisplayedProgress = 0f;
        _multipleTitle!.Text = $"선택된 용병 {selected.Count}명";
        int idle = 0, moving = 0, working = 0, gathering = 0, hauling = 0, construction = 0, demolition = 0;
        Dictionary<MercenaryWorkSkillTypeV3, (int Value, string Name)> highest = new();
        for (int index = 0; index < _selectionButtons.Length; index++)
        {
            _selectionButtons[index].Visible = false;
            _selectionButtonIds[index] = string.Empty;
        }

        for (int index = 0; index < selected.Count; index++)
        {
            if (!_snapshotBuilder!.TryBuild(selected[index], out MercenaryInspectHudSnapshotV3? snapshot) || snapshot == null) continue;
            if (index < MaximumVisibleSelectionRows)
            {
                Button button = _selectionButtons[index];
                _selectionButtonIds[index] = snapshot.MercenaryId;
                button.Text = $"{index + 1}. {snapshot.DisplayName,-14}  {snapshot.Status,-9}  {snapshot.WorkType,-8}  Cell {snapshot.State.CurrentCell}";
                button.Visible = true;
            }
            switch (snapshot.State.ActivityState)
            {
                case MercenaryActivityStateV3.Idle: idle++; break;
                case MercenaryActivityStateV3.Moving: moving++; break;
                case MercenaryActivityStateV3.Working: working++; break;
            }

            switch (snapshot.WorkType)
            {
                case "채집": gathering++; break;
                case "운반": hauling++; break;
                case "건설": case "건설 자재 운반": construction++; break;
                case "철거": demolition++; break;
            }

            UpdateHighest(highest, MercenaryWorkSkillTypeV3.Gathering, snapshot.Profile.WorkSkills.Gathering, snapshot.DisplayName);
            UpdateHighest(highest, MercenaryWorkSkillTypeV3.Hauling, snapshot.Profile.WorkSkills.Hauling, snapshot.DisplayName);
            UpdateHighest(highest, MercenaryWorkSkillTypeV3.Construction, snapshot.Profile.WorkSkills.Construction, snapshot.DisplayName);
            UpdateHighest(highest, MercenaryWorkSkillTypeV3.Guarding, snapshot.Profile.WorkSkills.Guarding, snapshot.DisplayName);
        }

        int hidden = Math.Max(0, selected.Count - MaximumVisibleSelectionRows);
        _multipleSummary!.Text = $"대기 {idle} · 이동 {moving} · 작업 {working}  |  채집 {gathering} 운반 {hauling} 건설 {construction} 철거 {demolition}" + (hidden > 0 ? $" · 외 {hidden}명" : string.Empty);
        _multipleHighestSkills!.Text = "최고 숙련  " + HighestText(highest, MercenaryWorkSkillTypeV3.Gathering) + "  ·  " + HighestText(highest, MercenaryWorkSkillTypeV3.Hauling) + "  ·  " + HighestText(highest, MercenaryWorkSkillTypeV3.Construction) + "  ·  " + HighestText(highest, MercenaryWorkSkillTypeV3.Guarding);
    }

    private void SetCondition(int index, float value)
    {
        value = Mathf.Clamp(value, 0f, 1f);
        _conditionBars[index].Value = value * 100f;
        _conditionValues[index].Text = $"{value * 100f:0}%";
    }

    private void OnCancelPressed()
    {
        if (_control == null || string.IsNullOrEmpty(DisplayedMercenaryId)) return;
        bool changed = _control.CancelCurrentActivity(DisplayedMercenaryId);
        LastAction = changed ? "CurrentActivityCancelled" : "NothingToCancel";
        GetViewport().SetInputAsHandled();
        Refresh("CancelPressed");
    }

    private void OnUnassignBed(){if(_needs!=null&&!string.IsNullOrEmpty(DisplayedMercenaryId))_needs.Assignments.TryUnassign(DisplayedMercenaryId);LastAction="BedUnassigned";GetViewport().SetInputAsHandled();Refresh("BedUnassigned");}
    private void OnRestPressed(){if(_restWork!=null&&!string.IsNullOrEmpty(DisplayedMercenaryId)){bool ok=_restWork.TryIssue(DisplayedMercenaryId,false,out string reason);LastAction=ok?"ManualRestIssued":reason;}GetViewport().SetInputAsHandled();Refresh("RestPressed");}
    private void OnEatPressed(){if(_eatingWork!=null&&!string.IsNullOrEmpty(DisplayedMercenaryId)){bool ok=_eatingWork.TryIssueManual(DisplayedMercenaryId,out string reason);LastAction=ok?"ManualEatIssued":reason;}GetViewport().SetInputAsHandled();Refresh("EatPressed");}

    private void OnSelectionRowPressed(int index)
    {
        if (_control == null || index < 0 || index >= _selectionButtonIds.Length || string.IsNullOrEmpty(_selectionButtonIds[index])) return;
        _control.Selection.TrySelectSingle(_selectionButtonIds[index], out _);
        LastAction = "MultiRowSelected";
        GetViewport().SetInputAsHandled();
    }

    private void OnPanelGuiInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton or InputEventMouseMotion)
        {
            GetViewport().SetInputAsHandled();
        }
    }

    private void SetPanelBounds(float height)
    {
        if (_panel == null) return;
        _panel.OffsetLeft = 12f;
        _panel.OffsetRight = 12f + PanelWidth;
        _panel.OffsetBottom = -BottomOffset;
        _panel.OffsetTop = -BottomOffset - height;
    }

    private void PushDiagnostics(MercenaryInspectHudSnapshotV3? snapshot, string reason)
    {
        _manager?.SetMercenaryInspectHudState(
            _panel?.Visible == true,
            Mode.ToString(),
            _control?.Selection.Count ?? 0,
            DisplayedMercenaryId,
            DisplayedWorkType,
            DisplayedWorkPhase,
            DisplayedCarry,
            DisplayedProgress,
            RefreshCount,
            reason,
            _worldMapBlocked,
            LastAction,
            _panel?.GetGlobalRect() ?? default,
            snapshot?.Condition);
    }

    private static string SkillLine(MercenaryWorkSkillTypeV3 type, int value, HashSet<MercenaryWorkSkillTypeV3> top)
        => $"{(top.Contains(type) ? "★" : " ")} {MercenaryHudTextFormatterV3.Skill(type)} {value,2}";

    private static void UpdateHighest(Dictionary<MercenaryWorkSkillTypeV3, (int Value, string Name)> values, MercenaryWorkSkillTypeV3 type, int value, string name)
    {
        if (!values.TryGetValue(type, out var current) || value > current.Value)
        {
            values[type] = (value, name);
        }
    }

    private static string HighestText(Dictionary<MercenaryWorkSkillTypeV3, (int Value, string Name)> values, MercenaryWorkSkillTypeV3 type)
        => values.TryGetValue(type, out var value) ? $"{MercenaryHudTextFormatterV3.Skill(type)} {value.Value} {value.Name}" : $"{MercenaryHudTextFormatterV3.Skill(type)} -";

    private static string ShortId(string value) => value.Length <= 13 ? value : value[..13];
}
