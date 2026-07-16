using System;
using System.Collections.Generic;
using GameplayV3.Control;
using Godot;

namespace GameplayV3.Jobs.Runtime;

public partial class MercenaryWorkPriorityPanelV3:Godot.Control
{
    private static readonly (JobTypeV3 Type,string Label)[] Rows=
    {
        (JobTypeV3.Hauling,"운반"),(JobTypeV3.Construction,"건설"),(JobTypeV3.Demolition,"철거"),
        (JobTypeV3.Gathering,"채집"),(JobTypeV3.Sowing,"파종"),(JobTypeV3.Harvesting,"수확")
    };
    private JobManagerV3? _jobs;private MercenaryControlSessionV3? _control;private PanelContainer? _panel;private Label? _title;
    private readonly Dictionary<JobTypeV3,Button> _buttons=new();private string _mercenaryId=string.Empty;private bool _worldMapBlocked;
    public string LastAction{get;private set;}="Closed";public bool IsOpen=>_panel?.Visible==true;

    public void Initialize(JobManagerV3 jobs,MercenaryControlSessionV3 control)
    { _jobs=jobs;_control=control;Build();control.Selection.SelectionChanged+=OnSelectionChanged;Visible=true; }
    public override void _ExitTree(){if(_control!=null)_control.Selection.SelectionChanged-=OnSelectionChanged;}
    public void Toggle(string mercenaryId)
    {
        if(_worldMapBlocked||_panel==null||_control?.Selection.Count!=1||string.IsNullOrWhiteSpace(mercenaryId)){Close("InvalidSelection");return;}
        if(_panel.Visible&&_mercenaryId==mercenaryId){Close("ToggleClosed");return;}_mercenaryId=mercenaryId;_panel.Visible=true;LastAction="Opened";Refresh();
    }
    public void Close(string action="Closed"){if(_panel!=null)_panel.Visible=false;_mercenaryId=string.Empty;LastAction=action;}
    public void SetWorldMapBlocked(bool blocked){_worldMapBlocked=blocked;if(blocked)Close("WorldMapBlocked");}
    private void OnSelectionChanged(){if(_control?.Selection.Count!=1||(!_control.Selection.Contains(_mercenaryId)&&_mercenaryId.Length>0))Close("SelectionChanged");}
    private void Build()
    {
        MouseFilter=MouseFilterEnum.Ignore;SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _panel=new PanelContainer{Name="WorkPriorityPanel",MouseFilter=MouseFilterEnum.Stop,Visible=false};_panel.SetAnchorsPreset(LayoutPreset.BottomLeft);
        _panel.OffsetLeft=612;_panel.OffsetRight=862;_panel.OffsetTop=-304;_panel.OffsetBottom=-88;
        StyleBoxFlat style=new(){BgColor=new Color(.035f,.043f,.05f,.97f),BorderColor=new Color(.28f,.31f,.33f,.95f),BorderWidthLeft=1,BorderWidthTop=1,BorderWidthRight=1,BorderWidthBottom=1,ContentMarginLeft=8,ContentMarginRight=8,ContentMarginTop=7,ContentMarginBottom=7};_panel.AddThemeStyleboxOverride("panel",style);AddChild(_panel);
        VBoxContainer root=new();root.AddThemeConstantOverride("separation",3);_panel.AddChild(root);
        HBoxContainer header=new();_title=new Label{Text="작업 우선순위",SizeFlagsHorizontal=SizeFlags.ExpandFill};_title.AddThemeFontSizeOverride("font_size",13);header.AddChild(_title);
        Button reset=new(){Text="기본값",CustomMinimumSize=new Vector2(54,24),MouseFilter=MouseFilterEnum.Stop};reset.Pressed+=()=>{if(_jobs!=null&&_mercenaryId.Length>0){_jobs.ResetPriorities(_mercenaryId);LastAction="ResetDefaults";Refresh();}};header.AddChild(reset);
        Button close=new(){Text="×",CustomMinimumSize=new Vector2(26,24),MouseFilter=MouseFilterEnum.Stop};close.Pressed+=()=>Close("CloseButton");header.AddChild(close);root.AddChild(header);
        foreach(var row in Rows){HBoxContainer line=new();Label label=new(){Text=row.Label,SizeFlagsHorizontal=SizeFlags.ExpandFill,VerticalAlignment=VerticalAlignment.Center};label.AddThemeFontSizeOverride("font_size",12);line.AddChild(label);Button button=new(){CustomMinimumSize=new Vector2(76,24),MouseFilter=MouseFilterEnum.Stop,ToggleMode=true};JobTypeV3 captured=row.Type;button.Pressed+=()=>Cycle(captured);_buttons.Add(row.Type,button);line.AddChild(button);root.AddChild(line);}
    }
    private void Cycle(JobTypeV3 type)
    {
        if(_jobs==null||_mercenaryId.Length==0)return;int current=_jobs.GetOrCreatePriorityProfile(_mercenaryId).GetPriority(type);int next=current==4?0:current+1;
        if(_jobs.TrySetPriority(_mercenaryId,type,next,out string reason)){LastAction=$"{type}={next}";Refresh();}else LastAction=reason;
        GetViewport().SetInputAsHandled();
    }
    private void Refresh()
    {
        if(_jobs==null||_mercenaryId.Length==0)return;MercenaryWorkPriorityProfileV3 profile=_jobs.GetOrCreatePriorityProfile(_mercenaryId);
        if(_title!=null)_title.Text=$"작업 우선순위 · {ShortId(_mercenaryId)}";
        foreach(var row in Rows){int value=profile.GetPriority(row.Type);_buttons[row.Type].Text=value==0?"끔":$"우선 {value}";_buttons[row.Type].ButtonPressed=value>0;}
    }
    private static string ShortId(string id)=>id.Length<=12?id:id[..12];
}
