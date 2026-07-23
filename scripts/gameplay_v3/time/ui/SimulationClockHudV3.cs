using System;
using Godot;

namespace GameplayV3.Time.UI;

public partial class SimulationClockHudV3 : CenterContainer
{
    private SimulationClockSessionV3? _clock;
    private Label? _timeLabel;
    private Label? _phaseLabel;
    private PanelContainer? _clockPanel;
    private Button? _pauseButton;
    private readonly Button?[] _scaleButtons=new Button?[4];
    private double _displayAccumulator;

    public const float LayoutTopOffset=8f;
    public const float LayoutHeight=40f;
    public const float PanelWidth=376f;
    public const float PanelHeight=34f;

    public int RootNodeCount=>IsInsideTree()?1:0;
    public int DuplicateSubscriptionCount { get; private set; }
    public int RefreshCount { get; private set; }
    public long BoundSessionRevision=>_clock?.SessionRevision??-1;
    public bool BlocksWorldInput=>MouseFilter==MouseFilterEnum.Ignore&&_clockPanel?.MouseFilter==MouseFilterEnum.Stop&&_pauseButton?.MouseFilter==MouseFilterEnum.Stop&&_scaleButtons[1]?.MouseFilter==MouseFilterEnum.Stop&&_scaleButtons[2]?.MouseFilter==MouseFilterEnum.Stop&&_scaleButtons[3]?.MouseFilter==MouseFilterEnum.Stop;

    public static bool TryValidateLayout(Vector2 viewportSize,out string reason)
    {
        if(viewportSize.X<PanelWidth||viewportSize.Y<LayoutTopOffset+LayoutHeight){reason="Viewport is smaller than the clock HUD layout.";return false;}
        float left=(viewportSize.X-PanelWidth)/2f;
        if(left<0||left+PanelWidth>viewportSize.X){reason="Centered clock panel would be clipped.";return false;}
        reason=string.Empty;return true;
    }

    public override void _Ready()
    {
        Name="SimulationClockHudV3";
        AddToGroup("simulation_clock_hud_v3");
        MouseFilter=MouseFilterEnum.Ignore;
        AnchorLeft=0f;AnchorRight=1f;AnchorTop=0f;AnchorBottom=0f;
        OffsetLeft=0f;OffsetRight=0f;OffsetTop=LayoutTopOffset;OffsetBottom=LayoutTopOffset+LayoutHeight;
        CustomMinimumSize=new Vector2(0,LayoutHeight);
        BuildUi();
    }

    public override void _ExitTree()=>Unbind();

    public void Bind(SimulationClockSessionV3 clock)
    {
        if(ReferenceEquals(_clock,clock)){Refresh();return;}
        Unbind();_clock=clock;
        _clock.HourChanged+=OnClockChanged;_clock.DayPhaseChanged+=OnClockChanged;_clock.DayStarted+=OnClockChanged;
        _clock.TimeScaleChanged+=OnClockChanged;_clock.PauseChanged+=OnClockChanged;
        Refresh();
    }

    public void SetGameplayReady(bool ready){Visible=ready;MouseFilter=MouseFilterEnum.Ignore;}

    public void AdvanceDisplay(double realDeltaSeconds)
    {
        if(!Visible||_clock==null||realDeltaSeconds<=0)return;
        _displayAccumulator+=realDeltaSeconds;
        if(_displayAccumulator<.25)return;
        _displayAccumulator%=.25;Refresh();
    }

    public void Refresh()
    {
        if(_clock==null||_timeLabel==null||_phaseLabel==null||_pauseButton==null)return;
        SimulationClockSnapshotV3 value=_clock.GetSnapshot();
        _timeLabel.Text=$"{value.DayIndex}일차 {SimulationClockSessionV3.FormatClockTime(value.Hour,value.Minute)}";
        _phaseLabel.Text=value.DayPhase switch { DayPhaseV3.Dawn=>"새벽",DayPhaseV3.Day=>"낮",DayPhaseV3.Evening=>"저녁",_=>"밤" };
        _pauseButton.ButtonPressed=value.IsPaused;
        _pauseButton.Text=value.IsPaused?"Ⅱ 정지":"Ⅱ";
        for(int scale=1;scale<=3;scale++)if(_scaleButtons[scale]!=null)_scaleButtons[scale]!.Disabled=!value.IsPaused&&value.TimeScale==scale;
        RefreshCount++;
    }

    private void BuildUi()
    {
        _clockPanel=new PanelContainer{Name="ClockPanel",MouseFilter=MouseFilterEnum.Stop,CustomMinimumSize=new Vector2(PanelWidth,PanelHeight)};
        AddChild(_clockPanel);
        HBoxContainer row=new(){Name="ClockRow",MouseFilter=MouseFilterEnum.Stop};row.AddThemeConstantOverride("separation",6);_clockPanel.AddChild(row);
        _timeLabel=MakeLabel("1일차 08:00",14);_timeLabel.CustomMinimumSize=new Vector2(104,28);row.AddChild(_timeLabel);
        _phaseLabel=MakeLabel("낮",12);_phaseLabel.CustomMinimumSize=new Vector2(30,28);row.AddChild(_phaseLabel);
        _pauseButton=MakeButton("Ⅱ",48);_pauseButton.ToggleMode=true;_pauseButton.Pressed+=()=>{_clock?.TogglePaused();GetViewport().SetInputAsHandled();};row.AddChild(_pauseButton);
        for(int scale=1;scale<=3;scale++){int captured=scale;Button button=MakeButton($"{scale}x",42);button.Pressed+=()=>{_clock?.SetPaused(false);_clock?.TrySetTimeScale(captured,out _);GetViewport().SetInputAsHandled();};_scaleButtons[scale]=button;row.AddChild(button);}
    }

    private static Label MakeLabel(string text,int fontSize){Label label=new(){Text=text,VerticalAlignment=VerticalAlignment.Center,MouseFilter=MouseFilterEnum.Ignore};label.AddThemeFontSizeOverride("font_size",fontSize);return label;}
    private static Button MakeButton(string text,float width){Button button=new(){Text=text,CustomMinimumSize=new Vector2(width,28),MouseFilter=MouseFilterEnum.Stop,FocusMode=FocusModeEnum.None};button.AddThemeFontSizeOverride("font_size",12);return button;}
    private void OnClockChanged(SimulationClockEventV3 _)=>Refresh();
    private void Unbind(){if(_clock==null)return;_clock.HourChanged-=OnClockChanged;_clock.DayPhaseChanged-=OnClockChanged;_clock.DayStarted-=OnClockChanged;_clock.TimeScaleChanged-=OnClockChanged;_clock.PauseChanged-=OnClockChanged;_clock=null;}
}
