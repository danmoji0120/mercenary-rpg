using System;
using GameplayV3.Construction;
using GameplayV3.Resources;
using GameplayV3.UI;
using GameplayV3.Work;
using GameplayV3.Equipment;
using Godot;
using WorldV2;

namespace GameplayV3.Selection;

public partial class SelectionInspectPanelV3:Godot.Control
{
    private readonly Label[] _rows=new Label[12];
    private PanelContainer? _panel;private Label? _title;private Label? _kind;private Label? _status;private Button? _manageButton;private Button? _directProductionButton;private Button? _equipButton;private Button? _haulEquipmentButton;private SelectionInspectSnapshotBuilderV3? _builder;private Action<string>? _openProduction;private Action<string>? _directProduction;private Action<string>? _equipEquipment;private Action<string>? _haulEquipment;private Func<bool>? _hasSelectedMercenary;private SelectionTargetRefV3? _target;private double _refreshCredit;private long _lastRevision=-1;private bool _constructionUiBlocked;
    public SelectionDiagnosticsV3 Diagnostics{get;}=new();public int RootCount=>_panel==null?0:1;public int DynamicRowCreatedTotal=>_rows.Length;public int DynamicRowActiveCount{get;private set;}

    public void Initialize(ResourceSessionV3 resources,ConstructionSessionV3 construction,MercenaryWorkSessionV3 work,Action<string>? openProduction=null,GameplayV3.Production.ProductionSessionV3? production=null,Action<string>? directProduction=null,Func<bool>? hasSelectedMercenary=null,Func<string,string>? productionBlockReason=null,EquipmentRuntimeV3? equipment=null,Action<string>? equipEquipment=null,Action<string>? haulEquipment=null){if(_builder!=null)return;_builder=new(resources,construction,work,production,productionBlockReason,equipment);_openProduction=openProduction;_directProduction=directProduction;_hasSelectedMercenary=hasSelectedMercenary;_equipEquipment=equipEquipment;_haulEquipment=haulEquipment;MouseFilter=MouseFilterEnum.Ignore;SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);BuildUi();SetProcess(true);}
    public bool TryHandleWorldInput(InputEvent e,WorldV2GridRenderer grid,long sessionRevision){if(_constructionUiBlocked||e is not InputEventMouseButton mb||mb.Pressed||mb.ButtonIndex!=MouseButton.Left||_builder==null)return false;GlobalCellCoord cell=new(grid.WorldToCell(GetViewport().GetCanvasTransform().AffineInverse()*mb.Position));if(!_builder.TryHit(cell,sessionRevision,out var target)){ClearSelection();return false;}SetTarget(target,sessionRevision);return true;}
    public void SetConstructionUiBlocked(bool blocked){_constructionUiBlocked=blocked;if(blocked)ClearSelection();}
    public void ClearSelection(){if(_target==null)return;_target=null;Diagnostics.SelectionTargetChangeCount++;if(_panel!=null)_panel.Visible=false;}
    public bool HandleEscape(){if(_target==null)return false;ClearSelection();return true;}
    public override void _Process(double delta){if(_target==null||_panel?.Visible!=true)return;_refreshCredit+=delta;if(_refreshCredit<.25)return;_refreshCredit=0;Refresh(GameplayV3.Session.GameplaySessionV3.SessionRevision);}
    private void SetTarget(SelectionTargetRefV3 target,long revision){if(_target==target)return;_target=target;Diagnostics.SelectionTargetChangeCount++;_lastRevision=-1;Refresh(revision);}
    private void Refresh(long revision)
    {
        if(_target==null||_builder==null)return;
        if(!_builder.TryBuild(_target.Value,revision,out var snapshot)||snapshot==null){Diagnostics.SelectionStaleTargetDropCount++;ClearSelection();return;}
        if(snapshot.SourceRevision==_lastRevision)return;_lastRevision=snapshot.SourceRevision;Diagnostics.SelectionSnapshotBuildCount++;
        SetText(_title,snapshot.DisplayName);SetText(_kind,snapshot.KindLabel);SetText(_status,snapshot.Status);DynamicRowActiveCount=Math.Min(_rows.Length,snapshot.Rows.Count);
        for(int i=0;i<_rows.Length;i++){bool show=i<DynamicRowActiveCount;_rows[i].Visible=show;if(show)SetText(_rows[i],$"{snapshot.Rows[i].Label}    {snapshot.Rows[i].Value}");}
        if(_manageButton!=null)_manageButton.Visible=snapshot.Target.TargetKind==SelectionTargetKindV3.CompletedStructure&&snapshot.KindLabel=="\uC0DD\uC0B0 \uC2DC\uC124";
        if(_directProductionButton!=null)_directProductionButton.Visible=_manageButton?.Visible==true&&(_hasSelectedMercenary?.Invoke()??false);
        if(_equipButton!=null)_equipButton.Visible=snapshot.Target.TargetKind==SelectionTargetKindV3.Equipment&&(_hasSelectedMercenary?.Invoke()??false);
        if(_haulEquipmentButton!=null)_haulEquipmentButton.Visible=snapshot.Target.TargetKind==SelectionTargetKindV3.Equipment&&(_hasSelectedMercenary?.Invoke()??false);
        _panel!.Visible=true;
    }
    private void BuildUi()
    {
        _panel=new PanelContainer{Name="SelectionInspectPanel",Visible=false};GameplayUiShellV3.ConfigureSelection(_panel);AddChild(_panel);
        ScrollContainer scroll=new(){Name="DetailsScroll",HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,VerticalScrollMode=ScrollContainer.ScrollMode.Auto,MouseFilter=MouseFilterEnum.Stop};_panel.AddChild(scroll);
        VBoxContainer body=new(){Name="Body",MouseFilter=MouseFilterEnum.Ignore};scroll.AddChild(body);
        _title=new Label{Name="Title"};_title.AddThemeFontSizeOverride("font_size",18);body.AddChild(_title);
        _kind=new Label{Name="Kind"};_kind.AddThemeColorOverride("font_color",GameplayUiThemeV3.TextSecondary);body.AddChild(_kind);
        _status=new Label{Name="Status"};body.AddChild(_status);body.AddChild(new HSeparator());
        for(int i=0;i<_rows.Length;i++){_rows[i]=new Label{Name=$"DetailRow{i}",Visible=false,AutowrapMode=TextServer.AutowrapMode.WordSmart};body.AddChild(_rows[i]);}
        _manageButton=new Button{Name="ManageButton",Text="\uC81C\uC791 \uBA85\uB839 \uAD00\uB9AC",Visible=false,MouseFilter=MouseFilterEnum.Stop,CustomMinimumSize=new Vector2(0,GameplayUiThemeV3.CommandButtonHeight)};_manageButton.Pressed+=()=>{if(_target!=null)_openProduction?.Invoke(_target.Value.StableTargetId);};body.AddChild(_manageButton);
        _directProductionButton=new Button{Name="DirectProductionButton",Text="\uC120\uD0DD \uC6A9\uBCD1\uC5D0\uAC8C \uC81C\uC791 \uBA85\uB839",Visible=false,MouseFilter=MouseFilterEnum.Stop,CustomMinimumSize=new Vector2(0,GameplayUiThemeV3.CommandButtonHeight)};_directProductionButton.Pressed+=()=>{if(_target!=null)_directProduction?.Invoke(_target.Value.StableTargetId);};body.AddChild(_directProductionButton);
        _equipButton=new Button{Name="EquipEquipmentButton",Text="\uC120\uD0DD \uC6A9\uBCD1\uC5D0\uAC8C \uC7A5\uCC29",Visible=false,MouseFilter=MouseFilterEnum.Stop,CustomMinimumSize=new Vector2(0,GameplayUiThemeV3.CommandButtonHeight)};_equipButton.Pressed+=()=>{if(_target!=null)_equipEquipment?.Invoke(_target.Value.StableTargetId);};body.AddChild(_equipButton);
        _haulEquipmentButton=new Button{Name="HaulEquipmentButton",Text="\uC774 \uC7A5\uBE44 \uC6B4\uBC18 \uC6B0\uC120 \uC218\uD589",Visible=false,MouseFilter=MouseFilterEnum.Stop,CustomMinimumSize=new Vector2(0,GameplayUiThemeV3.CommandButtonHeight)};_haulEquipmentButton.Pressed+=()=>{if(_target!=null)_haulEquipment?.Invoke(_target.Value.StableTargetId);};body.AddChild(_haulEquipmentButton);
    }
    private static void SetText(Label? label,string value){if(label==null||label.Text==value)return;label.Text=value;}
}

public partial class WorldHoverTooltipV3:Godot.Control
{
    private PanelContainer? _panel;private Label? _label;private SelectionInspectSnapshotBuilderV3? _builder;private SelectionTargetRefV3? _target;private long _lastRevision=-1;public SelectionDiagnosticsV3 Diagnostics{get;}=new();public int RootCount=>_panel==null?0:1;
    public void Initialize(ResourceSessionV3 resources,ConstructionSessionV3 construction,MercenaryWorkSessionV3 work){if(_builder!=null)return;_builder=new(resources,construction,work);MouseFilter=MouseFilterEnum.Ignore;SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);_panel=new PanelContainer{Name="WorldHoverTooltip",Visible=false,MouseFilter=MouseFilterEnum.Ignore,Theme=GameplayUiThemeV3.Shared};_label=new Label{Name="Text",MouseFilter=MouseFilterEnum.Ignore};_panel.AddChild(_label);AddChild(_panel);}
    public void UpdateHover(Vector2 screen,WorldV2GridRenderer grid,long revision,bool suppressed){if(_builder==null||_panel==null||_label==null)return;if(suppressed){Clear();return;}GlobalCellCoord cell=new(grid.WorldToCell(GetViewport().GetCanvasTransform().AffineInverse()*screen));if(!_builder.TryHit(cell,revision,out var target)){Clear();return;}if(_target!=target){_target=target;_lastRevision=-1;Diagnostics.HoverTargetChangeCount++;}if(!_builder.TryBuild(target,revision,out var snapshot)||snapshot==null){Clear();return;}if(snapshot.SourceRevision!=_lastRevision){string core=snapshot.Rows.Count>0?$" \u00B7 {snapshot.Rows[0].Label} {snapshot.Rows[0].Value}":string.Empty;string text=snapshot.DisplayName+core;if(_label.Text!=text){_label.Text=text;Diagnostics.HoverTextAssignmentCount++;}Diagnostics.HoverSnapshotBuildCount++;_lastRevision=snapshot.SourceRevision;}_panel.Position=Clamp(screen+new Vector2(14,16),_panel.Size,GetViewportRect().Size);_panel.Visible=true;}
    public void Clear(){_target=null;_lastRevision=-1;if(_panel!=null)_panel.Visible=false;}
    private static Vector2 Clamp(Vector2 position,Vector2 size,Vector2 viewport)=>new(Mathf.Clamp(position.X,4,Mathf.Max(4,viewport.X-size.X-4)),Mathf.Clamp(position.Y,4,Mathf.Max(4,viewport.Y-size.Y-4)));
}
