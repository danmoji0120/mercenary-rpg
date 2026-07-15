using System;
using GameplayV3.Stockpile;
using Godot;
using WorldV2;

namespace GameplayV3.Stockpile.Runtime;

public partial class ConstructionUiV3 : Godot.Control
{
    private StockpileDesignationControllerV3? _designation;
    private WorldManagerV2? _manager;
    private Button? _toggleButton;
    private PanelContainer? _tray;
    private Button? _stockpileButton;
    private Button? _removeButton;
    private Label? _modeLabel;
    private bool _worldMapBlocked;
    private bool _initialized;

    public bool IsTrayOpen => _tray?.Visible == true;
    public bool IsWorldMapBlocked => _worldMapBlocked;
    public string ActiveConstructionTool { get; private set; } = "-";
    public string LastConstructionUiAction { get; private set; } = string.Empty;

    public void Initialize(StockpileDesignationControllerV3 designation,WorldManagerV2 manager)
    {
        if(_initialized)return;
        _initialized=true;_designation=designation;_manager=manager;MouseFilter=MouseFilterEnum.Ignore;SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);CreateUi();_designation.ModeChanged+=OnDesignationModeChanged;OnDesignationModeChanged(_designation.Mode);SetWorldMapBlocked(manager.WorldMapOverlayVisible);RefreshManagerState();
#if DEBUG
        if(!ConstructionUiSelfCheckV3.TryValidate(out string reason))GD.PushError($"[ConstructionUiV3] Self-check FAIL: {reason}");else GD.Print("[ConstructionUiV3] Self-check PASS");
#endif
    }

    public void ToggleTray()
    {if(_worldMapBlocked)return;SetTrayOpen(!IsTrayOpen,"toggle");}
    public bool HandleEscape(){if(!IsTrayOpen)return false;SetTrayOpen(false,"escape");return true;}
    public void SetWorldMapBlocked(bool blocked){_worldMapBlocked=blocked;if(blocked)SetTrayOpen(false,"world-map");if(_toggleButton!=null)_toggleButton.Visible=!blocked;RefreshManagerState();}

    private void CreateUi()
    {
        _toggleButton=new Button{Name="ConstructionToggleButton",TooltipText="Construction",ToggleMode=true,FocusMode=Godot.Control.FocusModeEnum.None,MouseFilter=MouseFilterEnum.Stop};ConfigureRect(_toggleButton,12,-50,46,-16);_toggleButton.Pressed+=ToggleTray;AddChild(_toggleButton);AddConstructionIcon(_toggleButton);ApplyButtonStyle(_toggleButton,34,34);
        _tray=new PanelContainer{Name="ConstructionTray",Visible=false,MouseFilter=MouseFilterEnum.Stop,CustomMinimumSize=new Vector2(220,58)};ConfigureRect(_tray,54,-76,274,-12);_tray.AddThemeStyleboxOverride("panel",CreatePanelStyle());AddChild(_tray);
        VBoxContainer body=new(){Name="Body",MouseFilter=MouseFilterEnum.Ignore};body.AddThemeConstantOverride("separation",3);_tray.AddChild(body);
        _modeLabel=new Label{Name="HeaderLabel",Text="\uAC74\uC124",MouseFilter=MouseFilterEnum.Ignore};_modeLabel.AddThemeFontSizeOverride("font_size",13);_modeLabel.AddThemeColorOverride("font_color",new Color(0.92f,0.86f,0.98f));body.AddChild(_modeLabel);
        HBoxContainer tools=new(){Name="ConstructionTools",MouseFilter=MouseFilterEnum.Ignore};tools.AddThemeConstantOverride("separation",4);body.AddChild(tools);
        _stockpileButton=new Button{Name="StockpileToolButton",Text="\uC800\uC7A5 \uC601\uC5ED",ToggleMode=true,FocusMode=Godot.Control.FocusModeEnum.None,CustomMinimumSize=new Vector2(126,30),MouseFilter=MouseFilterEnum.Stop};_stockpileButton.Pressed+=OnStockpileButtonPressed;tools.AddChild(_stockpileButton);ApplyButtonStyle(_stockpileButton,126,30);
        _removeButton=new Button{Name="StockpileRemoveButton",Text="-",TooltipText="Remove stockpile cells",ToggleMode=true,FocusMode=Godot.Control.FocusModeEnum.None,CustomMinimumSize=new Vector2(30,30),MouseFilter=MouseFilterEnum.Stop};_removeButton.Pressed+=OnRemoveButtonPressed;tools.AddChild(_removeButton);ApplyButtonStyle(_removeButton,30,30);
    }
    private static void ConfigureRect(Godot.Control control,float left,float top,float right,float bottom){control.AnchorLeft=0;control.AnchorTop=1;control.AnchorRight=0;control.AnchorBottom=1;control.OffsetLeft=left;control.OffsetTop=top;control.OffsetRight=right;control.OffsetBottom=bottom;}
    private void AddConstructionIcon(Button button){Texture2D? texture=GD.Load<Texture2D>("res://assets/ui/icons/construction.svg");if(texture==null){GD.PushWarning("[ConstructionUiV3] construction.svg could not be loaded.");return;}TextureRect icon=new(){Name="ConstructionIcon",Texture=texture,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=MouseFilterEnum.Ignore,CustomMinimumSize=new Vector2(22,22),Size=new Vector2(22,22)};icon.SetAnchorsAndOffsetsPreset(LayoutPreset.Center,LayoutPresetMode.KeepSize,12);button.AddChild(icon);}
    private static StyleBoxFlat CreatePanelStyle(){StyleBoxFlat style=new(){BgColor=new Color(0.055f,0.045f,0.07f,0.94f),BorderColor=new Color(0.58f,0.46f,0.68f,0.85f)};style.SetBorderWidthAll(1);style.SetCornerRadiusAll(2);style.ContentMarginLeft=8;style.ContentMarginRight=8;style.ContentMarginTop=5;style.ContentMarginBottom=5;return style;}
    private static void ApplyButtonStyle(Button button,float width,float height){button.CustomMinimumSize=new Vector2(width,height);StyleBoxFlat normal=new(){BgColor=new Color(0.12f,0.10f,0.15f,0.96f),BorderColor=new Color(0.40f,0.34f,0.48f,0.9f)};normal.SetBorderWidthAll(1);StyleBoxFlat hover=new(){BgColor=new Color(0.19f,0.15f,0.23f,0.98f),BorderColor=new Color(0.68f,0.55f,0.76f,0.95f)};hover.SetBorderWidthAll(1);StyleBoxFlat pressed=new(){BgColor=new Color(0.40f,0.27f,0.48f,0.98f),BorderColor=new Color(0.78f,0.64f,0.86f,1)};pressed.SetBorderWidthAll(1);button.AddThemeStyleboxOverride("normal",normal);button.AddThemeStyleboxOverride("hover",hover);button.AddThemeStyleboxOverride("pressed",pressed);button.AddThemeFontSizeOverride("font_size",12);button.AddThemeColorOverride("font_color",new Color(0.92f,0.90f,0.96f));button.AddThemeColorOverride("font_hover_color",Colors.White);}
    private void OnStockpileButtonPressed(){if(_designation==null)return;StockpileDesignationModeV3 next=_designation.Mode==StockpileDesignationModeV3.Add?StockpileDesignationModeV3.None:StockpileDesignationModeV3.Add;_designation.SetMode(next);LastConstructionUiAction=next==StockpileDesignationModeV3.Add?"Stockpile Add":"Stockpile Add ended";RefreshManagerState();GetViewport().SetInputAsHandled();}
    private void OnRemoveButtonPressed(){if(_designation==null)return;StockpileDesignationModeV3 next=_designation.Mode==StockpileDesignationModeV3.Remove?StockpileDesignationModeV3.None:StockpileDesignationModeV3.Remove;_designation.SetMode(next);LastConstructionUiAction=next==StockpileDesignationModeV3.Remove?"Stockpile Remove":"Stockpile Remove ended";RefreshManagerState();GetViewport().SetInputAsHandled();}
    private void OnDesignationModeChanged(StockpileDesignationModeV3 mode){if(_stockpileButton!=null)_stockpileButton.ButtonPressed=mode==StockpileDesignationModeV3.Add;if(_removeButton!=null)_removeButton.ButtonPressed=mode==StockpileDesignationModeV3.Remove;ActiveConstructionTool=mode switch{StockpileDesignationModeV3.Add=>"StockpileAdd",StockpileDesignationModeV3.Remove=>"StockpileRemove",_=>"-"};LastConstructionUiAction=mode switch{StockpileDesignationModeV3.Add=>"Stockpile Add entered",StockpileDesignationModeV3.Remove=>"Stockpile Remove entered",_=>"Designation cancelled"};RefreshManagerState();}
    private void SetTrayOpen(bool open,string action){if(_tray==null)return;_tray.Visible=open;LastConstructionUiAction=open?"Construction tray opened":action=="escape"?"Construction tray closed":"Construction tray closed";if(_toggleButton!=null)_toggleButton.ButtonPressed=open;RefreshManagerState();if(open)GD.Print("[ConstructionUiV3] Construction tray opened");else GD.Print("[ConstructionUiV3] Construction tray closed");}
    private void RefreshManagerState(){_manager?.SetConstructionUiState(IsTrayOpen,ActiveConstructionTool,_worldMapBlocked,LastConstructionUiAction);}
}

public static class ConstructionUiSelfCheckV3
{
    public static bool TryValidate(out string reason)
    {
        if(!IsColor(StockpileOverlayV3.ZoneFillColor,0.714f,0.541f,0.847f,0.22f)){reason="Stockpile fill color drifted.";return false;}
        if(!IsColor(StockpileOverlayV3.ZoneBorderColor,0.776f,0.627f,0.894f,0.78f)){reason="Stockpile border color drifted.";return false;}
        if(!IsColor(StockpileOverlayV3.AddPreviewColor,0.824f,0.694f,0.922f,0.32f)){reason="Stockpile add preview color drifted.";return false;}
        if(!IsColor(StockpileOverlayV3.RemovePreviewColor,0.780f,0.478f,0.616f,0.30f)){reason="Stockpile remove preview color drifted.";return false;}
        reason=string.Empty;return true;
    }
    private static bool IsColor(Color actual,float r,float g,float b,float a)=>Mathf.IsEqualApprox(actual.R,r)&&Mathf.IsEqualApprox(actual.G,g)&&Mathf.IsEqualApprox(actual.B,b)&&Mathf.IsEqualApprox(actual.A,a);
}
