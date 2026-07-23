using System;
using System.Collections.Generic;
using System.Linq;
using GameplayV3.Bases;
using GameplayV3.Session;
using GameplayV3.UI;
using Godot;
using WorldV2;

namespace GameplayV3.Bases.UI;

public partial class BaseManagementPanelV3 : Godot.Control
{
    internal const string EmptyStateSummaryText = "\uAE30\uC9C0 \uC5C6\uC74C";
    internal const string EmptyStateDetailText = "\uC544\uC9C1 \uC124\uB9BD\uB41C \uAE30\uC9C0\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.\n\uCE68\uB300, \uC800\uC7A5 \uAD6C\uC5ED, \uB18D\uC7A5 \uB4F1\uC758 \uC2DC\uC124\uC744 \uAC74\uC124\uD558\uBA74 \uCCAB \uAE30\uC9C0 \uC601\uC5ED\uC774 \uC790\uB3D9\uC73C\uB85C \uD615\uC131\uB429\uB2C8\uB2E4.";
    internal const string LoadingStateText = "\uAE30\uC9C0 \uC815\uBCF4\uB97C \uBD88\uB7EC\uC624\uB294 \uC911\uC785\uB2C8\uB2E4.";

    private BaseAreaSessionV3? _baseAreas;
    private BaseRoleSessionV3? _roles;
    private WorldManagerV2? _manager;
    private WorldV2CameraController? _camera;
    private WorldStreamManagerV2? _stream;
    private ItemList? _rows;
    private PanelContainer? _panel;
    private Button? _toggleButton;
    private Button? _headquartersButton;
    private Button? _baseButton;
    private Button? _outpostButton;
    private Button? _cameraButton;
    private Label? _summaryLabel;
    private Label? _statusLabel;
    private Label? _roleHintLabel;
    private Label? _detailLabel;
    private readonly List<BaseManagementDisplayRowV3> _displayRows = new();
    private string _companyId = string.Empty;
    private string _selectedBaseAreaId = string.Empty;
    private string _lastRefreshReason = "NotInitialized";
    private bool _initialized;
    private bool _refreshQueued;
    private bool _worldMapBlocked;
    private bool _sessionsSubscribed;
    private bool _exitingTree;
    private long _bindGeneration;
    private long _queuedRefreshGeneration;

    public bool IsPanelOpen => _panel?.Visible == true;
    public string SelectedManagementBaseId => _selectedBaseAreaId;
    public int BaseManagementRowCount => _rows?.ItemCount ?? 0;
    public int BaseManagementRefreshCount { get; private set; }
    public int BaseManagementEventCoalescedCount { get; private set; }
    public int BaseManagementPanelNodeCount => _initialized ? 1 : 0;
    public int PerBaseManagementNodeCount => 0;
    public int PerBaseManagementTimerCount => 0;
    public int BaseManagementPerFrameFullScanCount => 0;
    public int BaseManagementDuplicateSubscriptionCount { get; private set; }
    public int BaseManagementNullReferenceCount { get; private set; }
    public int BaseManagementStaleCallbackCount { get; private set; }
    public int BaseManagementStaleSessionQueryCount { get; private set; }
    public int BaseManagementSelectedMissingBaseCount { get; private set; }
    public event Action? PanelOpened;

    public void Initialize(WorldManagerV2 manager, WorldV2CameraController? camera, WorldStreamManagerV2? stream)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _manager = manager;
        _camera = camera;
        _stream = stream;
        MouseFilter = MouseFilterEnum.Ignore;
        Theme = GameplayUiThemeV3.Shared;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildInterface();
        GameplaySessionV3.SessionBegan += OnSessionBegan;
        _sessionsSubscribed = true;
        BindCurrentSessions();
        SetWorldMapBlocked(manager.WorldMapOverlayVisible);

#if DEBUG
        if (BaseManagementUiSelfCheckV3.TryValidate(out string reason))
        {
            GD.Print("[BaseManagementUiV3] Self-check PASS");
        }
        else
        {
            GD.PushError($"[BaseManagementUiV3] Self-check FAIL: {reason}");
        }
#endif
    }

    public override void _Process(double delta)
    {
        if (!_initialized)
        {
            return;
        }

        bool loading = _stream != null && !_stream.IsInitialLoadingComplete;
        if (_toggleButton != null)
        {
            _toggleButton.Disabled = _worldMapBlocked || loading;
            _toggleButton.TooltipText = loading ? "Loading" : "Base management";
        }

        string company = _manager?.LocalCompanyId ?? string.Empty;
        if (!string.IsNullOrEmpty(company) && company != _companyId)
        {
            BindCurrentSessions();
        }
    }

    public void TogglePanel()
    {
        if (_worldMapBlocked || IsLoading())
        {
            return;
        }

        SetPanelOpen(!IsPanelOpen, "Toggle");
        GetViewport().SetInputAsHandled();
    }

    public void ClosePanel(string reason)
    {
        SetPanelOpen(false, reason);
    }

    public bool HandleEscape()
    {
        if (!IsPanelOpen)
        {
            return false;
        }

        SetPanelOpen(false, "Escape");
        return true;
    }

    public void SetWorldMapBlocked(bool blocked)
    {
        _worldMapBlocked = blocked;
        if (blocked)
        {
            SetPanelOpen(false, "WorldMap");
        }

        if (_toggleButton != null)
        {
            _toggleButton.Visible = !blocked;
            _toggleButton.Disabled = blocked || IsLoading();
        }
    }

    public void PrintDebugDiagnostics()
    {
        GD.Print($"[BaseManagementUiV3] BaseManagementPanelOpen={IsPanelOpen} " +
            $"SelectedManagementBaseId={SelectedManagementBaseId} " +
            $"BaseManagementRowCount={BaseManagementRowCount} " +
            $"BaseManagementRefreshCount={BaseManagementRefreshCount} " +
            $"BaseManagementEventCoalescedCount={BaseManagementEventCoalescedCount} " +
            $"BaseManagementPanelNodeCount={BaseManagementPanelNodeCount} " +
            $"perBaseNodes={PerBaseManagementNodeCount} perBaseTimers={PerBaseManagementTimerCount} " +
            $"perFrameFullScan={BaseManagementPerFrameFullScanCount} duplicateSubscriptions={BaseManagementDuplicateSubscriptionCount} " +
            $"nullReferences={BaseManagementNullReferenceCount} staleCallbacks={BaseManagementStaleCallbackCount} " +
            $"staleSessionQueries={BaseManagementStaleSessionQueryCount} selectedMissing={BaseManagementSelectedMissingBaseCount}");
    }

    public override void _ExitTree()
    {
        _exitingTree = true;
        _bindGeneration++;
        _refreshQueued = false;
        UnbindSessions();
        if (_sessionsSubscribed)
        {
            GameplaySessionV3.SessionBegan -= OnSessionBegan;
            _sessionsSubscribed = false;
        }
    }

    private void BuildInterface()
    {
        _toggleButton = new Button
        {
            Name = "BaseManagementToggleButton",
            Text = "\uAE30\uC9C0",
            ToggleMode = true,
            FocusMode = FocusModeEnum.None,
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(76, 34)
        };
        _toggleButton.SetAnchorsPreset(LayoutPreset.BottomRight);
        _toggleButton.OffsetLeft = -88;
        _toggleButton.OffsetTop = -50;
        _toggleButton.OffsetRight = -12;
        _toggleButton.OffsetBottom = -12;
        _toggleButton.Pressed += TogglePanel;
        ApplyButtonStyle(_toggleButton, 76, 34);
        AddChild(_toggleButton);

        _panel = new PanelContainer
        {
            Name = "BaseManagementPanel",
            MouseFilter = MouseFilterEnum.Stop,
            Visible = false
        };
        _panel.SetAnchorsPreset(LayoutPreset.TopRight);
        _panel.OffsetLeft = -408;
        _panel.OffsetTop = 12;
        _panel.OffsetRight = -12;
        _panel.OffsetBottom = -70;
        _panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
        _panel.GuiInput += OnPanelGuiInput;
        AddChild(_panel);

        MarginContainer margin = new() { MouseFilter = MouseFilterEnum.Ignore };
        SetMargins(margin, 10, 8, 10, 8);
        _panel.AddChild(margin);
        VBoxContainer body = new() { MouseFilter = MouseFilterEnum.Ignore };
        body.AddThemeConstantOverride("separation", 5);
        margin.AddChild(body);

        Label title = MakeLabel("\uAE30\uC9C0 \uAD00\uB9AC", 18, new Color(.94f, .88f, .98f));
        body.AddChild(title);
        _summaryLabel = MakeLabel("\uAE30\uC9C0 \uC5C6\uC74C", 12, new Color(.76f, .78f, .84f));
        body.AddChild(_summaryLabel);

        _rows = new ItemList
        {
            Name = "BaseManagementRows",
            SelectMode = ItemList.SelectModeEnum.Single,
            AllowReselect = true,
            CustomMinimumSize = new Vector2(0, 190),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Stop
        };
        _rows.ItemSelected += OnRowSelected;
        body.AddChild(_rows);

        _detailLabel = MakeLabel("", 11, new Color(.84f, .85f, .9f));
        _detailLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _detailLabel.CustomMinimumSize = new Vector2(0, 132);
        body.AddChild(_detailLabel);

        _roleHintLabel = MakeLabel("", 11, new Color(.92f, .72f, .48f));
        _roleHintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(_roleHintLabel);

        HBoxContainer roleButtons = new() { MouseFilter = MouseFilterEnum.Ignore };
        roleButtons.AddThemeConstantOverride("separation", 4);
        _headquartersButton = MakeActionButton("\uBCF8\uBD80 \uC9C0\uC815", OnHeadquartersPressed);
        _baseButton = MakeActionButton("\uC77C\uBC18 \uAE30\uC9C0", OnBasePressed);
        _outpostButton = MakeActionButton("\uC804\uCD08\uAE30\uC9C0", OnOutpostPressed);
        roleButtons.AddChild(_headquartersButton);
        roleButtons.AddChild(_baseButton);
        roleButtons.AddChild(_outpostButton);
        body.AddChild(roleButtons);

        _cameraButton = MakeActionButton("\uC704\uCE58\uB85C \uC774\uB3D9", OnCameraPressed);
        body.AddChild(_cameraButton);

        _statusLabel = MakeLabel("", 11, new Color(.62f, .86f, .72f));
        _statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(_statusLabel);
    }

    private void BindCurrentSessions()
    {
        if (_exitingTree)
        {
            return;
        }

        _bindGeneration++;
        _refreshQueued = false;
        UnbindSessions();
        _companyId = _manager?.LocalCompanyId ?? string.Empty;
        GameplaySessionV3.TryGetBaseAreaSession(out _baseAreas);
        GameplaySessionV3.TryGetBaseRoleSession(out _roles);
        if (_baseAreas != null)
        {
            _baseAreas.Changed += OnBaseAreaChanged;
        }

        if (_roles != null)
        {
            _roles.BaseRoleChanged += OnBaseRoleChanged;
            _roles.HeadquartersChanged += OnHeadquartersChanged;
            _roles.BaseRoleRemoved += OnBaseRoleRemoved;
        }

        QueueRefresh("SessionBound");
    }

    private void UnbindSessions()
    {
        if (_baseAreas != null)
        {
            _baseAreas.Changed -= OnBaseAreaChanged;
        }

        if (_roles != null)
        {
            _roles.BaseRoleChanged -= OnBaseRoleChanged;
            _roles.HeadquartersChanged -= OnHeadquartersChanged;
            _roles.BaseRoleRemoved -= OnBaseRoleRemoved;
        }

        _baseAreas = null;
        _roles = null;
    }

    private void OnSessionBegan() => BindCurrentSessions();
    private void OnBaseAreaChanged(BaseAreaChangeEventV3 value) => QueueRefresh($"BaseArea:{value.Kind}");
    private void OnBaseRoleChanged(BaseRoleChangedV3 value) => QueueRefresh("RoleChanged");
    private void OnHeadquartersChanged(HeadquartersChangedV3 value) => QueueRefresh("HeadquartersChanged");
    private void OnBaseRoleRemoved(BaseRoleRemovedV3 value) => QueueRefresh("RoleRemoved");

    private void QueueRefresh(string reason)
    {
        if (_exitingTree || !IsInsideTree())
        {
            return;
        }

        _lastRefreshReason = reason;
        if (_refreshQueued)
        {
            BaseManagementEventCoalescedCount++;
            return;
        }

        _refreshQueued = true;
        _queuedRefreshGeneration = _bindGeneration;
        CallDeferred(MethodName.RefreshDeferred, _queuedRefreshGeneration);
    }

    private void RefreshDeferred(long scheduledGeneration)
    {
        if (!_refreshQueued)
        {
            return;
        }

        if (scheduledGeneration != _queuedRefreshGeneration)
        {
            BaseManagementStaleCallbackCount++;
            return;
        }

        _refreshQueued = false;
        if (!IsDeferredRefreshCurrent(_bindGeneration, _queuedRefreshGeneration, IsInsideTree(), _exitingTree))
        {
            BaseManagementStaleCallbackCount++;
            return;
        }

        if (!AreCurrentSessionsBound())
        {
            BaseManagementStaleSessionQueryCount++;
            SetLoadingState();
            return;
        }

        RefreshRows(_lastRefreshReason);
    }

    private void RefreshRows(string reason)
    {
        BaseManagementRefreshCount++;
        _displayRows.Clear();
        _rows?.Clear();

        if (!AreCurrentSessionsBound())
        {
            BaseManagementStaleSessionQueryCount++;
            SetLoadingState();
            return;
        }

        if (_baseAreas == null || _roles == null)
        {
            BaseManagementStaleSessionQueryCount++;
            SetLoadingState();
            return;
        }

        IReadOnlyList<BaseAreaV3> companyAreas = _baseAreas.Areas.GetForCompany(_companyId);
        if (companyAreas.Count == 0)
        {
            SetEmptyState();
            return;
        }

        string? headquartersId = null;
        if (_roles.TryGetHeadquarters(_companyId, out BaseRoleStateV3? headquarters) && headquarters != null)
        {
            headquartersId = headquarters.BaseAreaId;
        }

        if (!string.IsNullOrWhiteSpace(_companyId))
        {
            foreach (BaseAreaV3 area in companyAreas)
            {
                BaseRoleStateV3? role = null;
                _roles.TryGetRole(area.BaseAreaId, out role);
                _displayRows.Add(new(area, role, area.BaseAreaId == headquartersId));
            }
        }

        _displayRows.Sort(CompareRows);
        bool selectedStillExists = _displayRows.Any(row => row.Area.BaseAreaId == _selectedBaseAreaId);
        if (!selectedStillExists)
        {
            _selectedBaseAreaId = SelectFallback(
                _displayRows.Select(row => row.Area.BaseAreaId).ToList(), headquartersId, string.Empty);
        }

        string? defaultBaseId = null;
        if (_roles?.TryGetDefaultCompanyBase(_companyId, out BaseAreaV3? defaultArea) == true && defaultArea != null)
        {
            defaultBaseId = defaultArea.BaseAreaId;
        }

        for (int index = 0; index < _displayRows.Count; index++)
        {
            BaseManagementDisplayRowV3 row = _displayRows[index];
            string role = row.Role?.Role switch
            {
                BaseRoleV3.Headquarters => "HQ",
                BaseRoleV3.Base => "BASE",
                BaseRoleV3.Outpost => "OUTPOST",
                _ => "-"
            };
            string defaultMarker = row.Area.BaseAreaId == defaultBaseId ? " *" : string.Empty;
            string text = $"[{role}] {ShortId(row.Area.BaseAreaId)}  A{row.Area.AnchorCount} M{row.Area.MemberSourceCount}  " +
                $"{row.Area.Bounds.Size.X}x{row.Area.Bounds.Size.Y}{defaultMarker}";
            _rows?.AddItem(text);
            _rows?.SetItemTooltip(index, row.Area.BaseAreaId);
        }

        int selectedIndex = _displayRows.FindIndex(row => row.Area.BaseAreaId == _selectedBaseAreaId);
        if (selectedIndex >= 0)
        {
            _rows?.Select(selectedIndex);
        }

        _summaryLabel!.Text = _displayRows.Count == 0
            ? "\uAE30\uC9C0 \uC5C6\uC74C"
            : $"\uAE30\uC9C0 {_displayRows.Count}\uAC1C  /  HQ {_displayRows.Count(row => row.Role?.Role == BaseRoleV3.Headquarters)}  " +
              $"BASE {_displayRows.Count(row => row.Role?.Role == BaseRoleV3.Base)}  OUTPOST {_displayRows.Count(row => row.Role?.Role == BaseRoleV3.Outpost)}";
        UpdateDetails();
    }

    private bool AreCurrentSessionsBound()
    {
        return _baseAreas != null
            && _roles != null
            && GameplaySessionV3.IsCurrentBaseAreaSession(_baseAreas)
            && GameplaySessionV3.IsCurrentBaseRoleSession(_roles);
    }

    private void SetLoadingState()
    {
        _displayRows.Clear();
        _rows?.Clear();
        _selectedBaseAreaId = string.Empty;
        _summaryLabel!.Text = LoadingStateText;
        _detailLabel!.Text = LoadingStateText;
        _roleHintLabel!.Text = string.Empty;
        _statusLabel!.Text = string.Empty;
        SetRoleButtons(null);
        _cameraButton!.Disabled = true;
    }

    private void SetEmptyState()
    {
        _displayRows.Clear();
        _rows?.Clear();
        _selectedBaseAreaId = string.Empty;
        _summaryLabel!.Text = EmptyStateSummaryText;
        _detailLabel!.Text = EmptyStateDetailText;
        _roleHintLabel!.Text = string.Empty;
        _statusLabel!.Text = string.Empty;
        SetRoleButtons(null);
        _cameraButton!.Disabled = true;
    }

    private void UpdateDetails()
    {
        int selectedIndex = _displayRows.FindIndex(row => row.Area.BaseAreaId == _selectedBaseAreaId);
        if (selectedIndex < 0)
        {
            if (_displayRows.Count > 0 && !string.IsNullOrEmpty(_selectedBaseAreaId))
            {
                BaseManagementSelectedMissingBaseCount++;
            }

            _selectedBaseAreaId = string.Empty;
            _detailLabel!.Text = "\uC120\uD0DD\uB41C \uAE30\uC9C0\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
            _roleHintLabel!.Text = string.Empty;
            SetRoleButtons(null);
            _cameraButton!.Disabled = true;
            return;
        }

        BaseManagementDisplayRowV3 selected = _displayRows[selectedIndex];
        BaseAreaV3 area = selected.Area;
        BaseRoleStateV3? role = selected.Role;
        string roleText = role?.Role.ToString() ?? "Unknown";
        string sourceText = role?.AssignmentSource.ToString() ?? "Unknown";
        bool isDefault = _roles?.TryGetDefaultCompanyBase(_companyId, out BaseAreaV3? defaultArea) == true && defaultArea?.BaseAreaId == area.BaseAreaId;
        _detailLabel!.Text =
            $"ID: {area.BaseAreaId}\n" +
            $"Company: {area.CompanyId}\n" +
            $"Role: {roleText}  Source: {sourceText}\n" +
            $"Role Revision: {role?.Revision.ToString() ?? "-"}  CreationOrder: {area.CreationOrder}\n" +
            $"Anchors: {area.AnchorCount}  Members: {area.MemberSourceCount}\n" +
            $"Bounds: {area.Bounds.Position} / {area.Bounds.Size}\n" +
            $"Center: {area.CenterCell.Value}\n" +
            $"Default Company Base: {isDefault}";
        _roleHintLabel!.Text = role?.Role == BaseRoleV3.Headquarters
            ? ""
            : "\uB2E4\uB978 \uAE30\uC9C0\uB97C \uBCF8\uBD80\uB85C \uC9C0\uC815\uD558\uBA74 \uD604\uC7AC \uBCF8\uBD80\uAC00 \uC77C\uBC18 \uAE30\uC9C0\uB85C \uBCC0\uACBD\uB429\uB2C8\uB2E4.";
        SetRoleButtons(role?.Role);
        _cameraButton!.Disabled = false;
    }

    private void SetRoleButtons(BaseRoleV3? role)
    {
        BaseManagementRoleButtonStateV3 state = BaseManagementRoleButtonStateV3.For(role);
        _headquartersButton!.Disabled = !state.HeadquartersEnabled;
        _baseButton!.Disabled = !state.BaseEnabled;
        _outpostButton!.Disabled = !state.OutpostEnabled;
    }

    private void OnRowSelected(long index)
    {
        if (index < 0 || index >= _displayRows.Count)
        {
            return;
        }

        _selectedBaseAreaId = _displayRows[(int)index].Area.BaseAreaId;
        UpdateDetails();
        GetViewport().SetInputAsHandled();
    }

    private void OnHeadquartersPressed() => TryChangeRole(BaseRoleV3.Headquarters);
    private void OnBasePressed() => TryChangeRole(BaseRoleV3.Base);
    private void OnOutpostPressed() => TryChangeRole(BaseRoleV3.Outpost);

    private void TryChangeRole(BaseRoleV3 role)
    {
        if (_roles == null || string.IsNullOrWhiteSpace(_companyId) || string.IsNullOrWhiteSpace(_selectedBaseAreaId))
        {
            SetStatus("\uC120\uD0DD\uB41C \uAE30\uC9C0\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.", false);
            return;
        }

        if (_roles.TryGetRole(_selectedBaseAreaId, out BaseRoleStateV3? current) && current?.Role == BaseRoleV3.Headquarters)
        {
            SetStatus("\uD604\uC7AC \uBCF8\uBD80\uB294 \uC774 \uD328\uB110\uC5D0\uC11C \uAC15\uB4F1\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.", false);
            return;
        }

        bool success;
        string reason;
        if (role == BaseRoleV3.Headquarters)
        {
            success = _roles.TrySetHeadquarters(_companyId, _selectedBaseAreaId, out reason);
        }
        else
        {
            success = _roles.TrySetRole(_companyId, _selectedBaseAreaId, role, out reason);
        }

        if (!success)
        {
            SetStatus(string.IsNullOrWhiteSpace(reason) ? "\uC5ED\uD560 \uBCC0\uACBD\uC5D0 \uC2E4\uD328\uD588\uC2B5\uB2C8\uB2E4." : reason, false);
            return;
        }

        SetStatus("\uC5ED\uD560\uC774 \uBCC0\uACBD\uB418\uC5C8\uC2B5\uB2C8\uB2E4.", true);
        _refreshQueued = false;
        RefreshRows("RoleCommand");
        GetViewport().SetInputAsHandled();
    }

    private void OnCameraPressed()
    {
        BaseAreaV3? area = null;
        int selectedIndex = _displayRows.FindIndex(row => row.Area.BaseAreaId == _selectedBaseAreaId);
        if (selectedIndex >= 0) area = _displayRows[selectedIndex].Area;
        if (area == null || _camera == null)
        {
            SetStatus("\uC704\uCE58\uB85C \uC774\uB3D9\uD560 \uAE30\uC9C0\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.", false);
            return;
        }

        _camera.CenterOnGlobalCell(area.CenterCell.Value);
        SetStatus("\uAE30\uC9C0 \uC704\uCE58\uB85C \uC774\uB3D9\uD588\uC2B5\uB2C8\uB2E4.", true);
        GetViewport().SetInputAsHandled();
    }

    private void SetPanelOpen(bool open, string reason)
    {
        if (_panel == null)
        {
            return;
        }

        _panel.Visible = open && !_worldMapBlocked;
        _toggleButton!.ButtonPressed = _panel.Visible;
        if (_panel.Visible)
        {
            _refreshQueued = false;
            RefreshRows(reason);
            PanelOpened?.Invoke();
        }
    }

    private void OnPanelGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton or InputEventMouseMotion)
        {
            GetViewport().SetInputAsHandled();
        }
    }

    private bool IsLoading() => _stream != null && !_stream.IsInitialLoadingComplete;

    private void SetStatus(string message, bool success)
    {
        _statusLabel!.Text = message;
        _statusLabel.AddThemeColorOverride("font_color", success ? new Color(.62f, .86f, .72f) : new Color(.96f, .58f, .52f));
    }

    private static int CompareRows(BaseManagementDisplayRowV3 left, BaseManagementDisplayRowV3 right)
    {
        int role = RoleRank(left.Role?.Role).CompareTo(RoleRank(right.Role?.Role));
        if (role != 0) return role;
        int creation = left.Area.CreationOrder.CompareTo(right.Area.CreationOrder);
        return creation != 0 ? creation : string.CompareOrdinal(left.Area.BaseAreaId, right.Area.BaseAreaId);
    }

    internal static int RoleRank(BaseRoleV3? role) => role switch
    {
        BaseRoleV3.Headquarters => 0,
        BaseRoleV3.Base => 1,
        BaseRoleV3.Outpost => 2,
        _ => 3
    };

    internal static string SelectFallback(IReadOnlyList<string> ids, string? preferredId, string? currentId)
    {
        if (!string.IsNullOrEmpty(currentId) && ids.Contains(currentId, StringComparer.Ordinal)) return currentId;
        if (!string.IsNullOrEmpty(preferredId) && ids.Contains(preferredId, StringComparer.Ordinal)) return preferredId;
        return ids.Count == 0 ? string.Empty : ids[0];
    }

    internal static bool IsDeferredRefreshCurrent(long currentGeneration, long scheduledGeneration, bool insideTree, bool exitingTree) =>
        insideTree && !exitingTree && currentGeneration == scheduledGeneration;

    private static Label MakeLabel(string text, int fontSize, Color color)
    {
        Label label = new() { Text = text, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Button MakeActionButton(string text, Action action)
    {
        Button button = new()
        {
            Text = text,
            ToggleMode = false,
            FocusMode = FocusModeEnum.None,
            MouseFilter = MouseFilterEnum.Stop,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 28)
        };
        button.Pressed += action;
        ApplyButtonStyle(button, 0, 28);
        return button;
    }

    private static void SetMargins(MarginContainer margin, int left, int top, int right, int bottom)
    {
        margin.AddThemeConstantOverride("margin_left", left);
        margin.AddThemeConstantOverride("margin_top", top);
        margin.AddThemeConstantOverride("margin_right", right);
        margin.AddThemeConstantOverride("margin_bottom", bottom);
    }

    private static StyleBoxFlat CreatePanelStyle()
    {
        StyleBoxFlat style = new()
        {
            BgColor = new Color(.045f, .052f, .075f, .97f),
            BorderColor = new Color(.38f, .48f, .62f, .95f)
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(3);
        return style;
    }

    private static void ApplyButtonStyle(Button button, float width, float height)
    {
        if (width > 0) button.CustomMinimumSize = new Vector2(width, height);
        StyleBoxFlat normal = new() { BgColor = new Color(.10f, .12f, .17f, .98f), BorderColor = new Color(.30f, .38f, .5f, .95f) };
        StyleBoxFlat hover = new() { BgColor = new Color(.17f, .22f, .3f, .98f), BorderColor = new Color(.55f, .7f, .88f, .95f) };
        StyleBoxFlat pressed = new() { BgColor = new Color(.26f, .36f, .52f, .98f), BorderColor = new Color(.72f, .84f, .98f, 1f) };
        normal.SetBorderWidthAll(1); hover.SetBorderWidthAll(1); pressed.SetBorderWidthAll(1);
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressed);
        button.AddThemeFontSizeOverride("font_size", 12);
        button.AddThemeColorOverride("font_color", new Color(.9f, .92f, .97f));
        button.AddThemeColorOverride("font_hover_color", Colors.White);
    }

    private static string ShortId(string value) => value.Length <= 16 ? value : value[..16];

    private readonly record struct BaseManagementDisplayRowV3(BaseAreaV3 Area, BaseRoleStateV3? Role, bool IsHeadquarters);
}

internal readonly record struct BaseManagementRoleButtonStateV3(bool HeadquartersEnabled, bool BaseEnabled, bool OutpostEnabled)
{
    public static BaseManagementRoleButtonStateV3 For(BaseRoleV3? role) => role switch
    {
        BaseRoleV3.Headquarters => new(false, false, false),
        BaseRoleV3.Base => new(true, false, true),
        BaseRoleV3.Outpost => new(true, true, false),
        _ => new(false, false, false)
    };
}
