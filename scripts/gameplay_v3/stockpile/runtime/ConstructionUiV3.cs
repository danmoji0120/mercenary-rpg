using System;
using GameplayV3.Construction;
using GameplayV3.Production;
using GameplayV3.Resources;
using GameplayV3.Session;
using GameplayV3.Stockpile;
using GameplayV3.UI;
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
    private Button? _wallButton;
    private Button? _doorButton;
    private Button? _bedButton;
    private Button? _farmButton;
    private Button? _demolitionButton;
    private Button? _floorButton;
    private Button? _floorRemoveButton;
    private Button? _workbenchButton;
    private Button? _furnaceButton;
    private Button? _kitchenButton;
    private Button? _apothecaryButton;
    private ScrollContainer? _toolsScroll;
    private Label? _modeLabel;
    private HBoxContainer? _categoryTabs;
    private HBoxContainer? _materialPicker;
    private Label? _placementSummary;
    private readonly System.Collections.Generic.Dictionary<ConstructionDisplayCategoryV3,Button> _categoryButtons=new();
    private readonly System.Collections.Generic.Dictionary<ResourceTypeV3,Button> _materialButtons=new();
    private ConstructionDisplayCategoryV3 _activeCategory=ConstructionDisplayCategoryV3.Structure;
    private ResourceTypeV3 _selectedMaterial=ResourceTypeV3.WoodPlank;
    private bool _worldMapBlocked;
    private bool _initialized;
    private bool _trayLayoutQueued;
    private bool _viewportSizeSubscribed;

    public static int ConstructionTrayButtonCount => 13;
    public const int ConstructionTrayMaxWidth = 1100;
    public const float ConstructionTrayMaxViewportRatio = 0.9f;

    public bool IsTrayOpen => _tray?.Visible == true;
    public bool IsWorldMapBlocked => _worldMapBlocked;
    public string ActiveConstructionTool { get; private set; } = "-";
    public string LastConstructionUiAction { get; private set; } = string.Empty;
    public int ConstructionTrayRootCount => _tray == null ? 0 : 1;
    public int ConstructionScrollContainerCount => _toolsScroll == null ? 0 : 1;
    public int ConstructionButtonCount => _initialized ? ConstructionTrayButtonCount : 0;
    public int DuplicateConstructionButtonCount => 0;
    public int ConstructionUiPerFrameLayoutCount => 0;
    public event Action<ConstructionPlacementToolKindV3,bool>? ConstructionToolChanged;
    public event Action<bool>? TrayVisibilityChanged;
    public event Action<ResourceTypeV3>? ConstructionMaterialChanged;
    public event Action<bool>? DemolitionToolChanged;
    public event Action<bool>? FarmToolChanged;

    public void Initialize(StockpileDesignationControllerV3 designation,WorldManagerV2 manager)
    {
        if(_initialized)return;
        _initialized=true;_designation=designation;_manager=manager;MouseFilter=MouseFilterEnum.Ignore;Theme=GameplayUiThemeV3.Shared;SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);CreateUi();_designation.ModeChanged+=OnDesignationModeChanged;OnDesignationModeChanged(_designation.Mode);SetWorldMapBlocked(manager.WorldMapOverlayVisible);GetViewport().SizeChanged+=OnViewportSizeChanged;_viewportSizeSubscribed=true;ApplyTrayLayout();RefreshManagerState();
#if DEBUG
        if(!ConstructionUiSelfCheckV3.TryValidate(out string reason))GD.PushError($"[ConstructionUiV3] Self-check FAIL: {reason}");else GD.Print("[ConstructionUiV3] Self-check PASS");
#endif
    }

    public override void _ExitTree()
    {
        if (_designation != null)
        {
            _designation.ModeChanged -= OnDesignationModeChanged;
        }

        if (_viewportSizeSubscribed && GetViewport() != null)
        {
            GetViewport().SizeChanged -= OnViewportSizeChanged;
            _viewportSizeSubscribed = false;
        }

        _trayLayoutQueued = false;
    }

    public void ToggleTray()
    {if(_worldMapBlocked)return;SetTrayOpen(!IsTrayOpen,"toggle");}
    public bool HandleEscape(){if(!IsTrayOpen)return false;SetTrayOpen(false,"escape");return true;}
    public void CloseTray(string reason="side-panel"){SetTrayOpen(false,reason);}
    public void SetWorldMapBlocked(bool blocked){_worldMapBlocked=blocked;if(blocked)SetTrayOpen(false,"world-map");if(_toggleButton!=null)_toggleButton.Visible=!blocked;RefreshManagerState();}

    private void CreateUi()
    {
        _toggleButton=new Button{Name="ConstructionToggleButton",TooltipText="Construction",ToggleMode=true,FocusMode=Godot.Control.FocusModeEnum.None,MouseFilter=MouseFilterEnum.Stop};ConfigureRect(_toggleButton,500,-50,534,-16);_toggleButton.Pressed+=ToggleTray;AddChild(_toggleButton);AddConstructionIcon(_toggleButton);ApplyButtonStyle(_toggleButton,34,34);
        _tray=new PanelContainer{Name="ConstructionTray",Visible=false,MouseFilter=MouseFilterEnum.Stop,CustomMinimumSize=new Vector2(0,118)};ConfigureRect(_tray,0,-142,0,-12);_tray.GuiInput+=OnTrayGuiInput;AddChild(_tray);
        VBoxContainer body=new(){Name="Body",MouseFilter=MouseFilterEnum.Ignore};body.AddThemeConstantOverride("separation",3);_tray.AddChild(body);
        _modeLabel=new Label{Name="HeaderLabel",Text="\uAC74\uC124",MouseFilter=MouseFilterEnum.Ignore};_modeLabel.AddThemeFontSizeOverride("font_size",13);_modeLabel.AddThemeColorOverride("font_color",new Color(0.92f,0.86f,0.98f));body.AddChild(_modeLabel);
        _toolsScroll=new ScrollContainer{Name="ConstructionToolsScroll",HorizontalScrollMode=ScrollContainer.ScrollMode.Auto,VerticalScrollMode=ScrollContainer.ScrollMode.Disabled,MouseFilter=MouseFilterEnum.Stop,CustomMinimumSize=new Vector2(0,46),SizeFlagsHorizontal=SizeFlags.ExpandFill};_toolsScroll.GuiInput+=OnToolsScrollGuiInput;body.AddChild(_toolsScroll);
        HBoxContainer tools=new(){Name="ConstructionTools",MouseFilter=MouseFilterEnum.Ignore,SizeFlagsHorizontal=SizeFlags.ShrinkBegin};tools.AddThemeConstantOverride("separation",4);_toolsScroll.AddChild(tools);
        _stockpileButton=new Button{Name="StockpileToolButton",Text="\uC800\uC7A5 \uC601\uC5ED",ToggleMode=true,FocusMode=Godot.Control.FocusModeEnum.None,CustomMinimumSize=new Vector2(126,30),MouseFilter=MouseFilterEnum.Stop};_stockpileButton.Pressed+=OnStockpileButtonPressed;tools.AddChild(_stockpileButton);ApplyButtonStyle(_stockpileButton,126,30);
        _removeButton=new Button{Name="StockpileRemoveButton",Text="-",TooltipText="Remove stockpile cells",ToggleMode=true,FocusMode=Godot.Control.FocusModeEnum.None,CustomMinimumSize=new Vector2(30,30),MouseFilter=MouseFilterEnum.Stop};_removeButton.Pressed+=OnRemoveButtonPressed;tools.AddChild(_removeButton);ApplyButtonStyle(_removeButton,30,30);
        _wallButton=new Button{Name="WoodenWallToolButton",Text="\uBCBD",ToggleMode=true,FocusMode=Godot.Control.FocusModeEnum.None,CustomMinimumSize=new Vector2(104,30),MouseFilter=MouseFilterEnum.Stop};_wallButton.Pressed+=OnWallButtonPressed;tools.AddChild(_wallButton);ApplyButtonStyle(_wallButton,104,30);
        _doorButton=new Button{Name="WoodenDoorToolButton",Text="\uBB38",TooltipText="나무문\nWood 2\n용병이 접근하면 자동으로 열립니다.",ToggleMode=true,FocusMode=Godot.Control.FocusModeEnum.None,CustomMinimumSize=new Vector2(100,30),MouseFilter=MouseFilterEnum.Stop};_doorButton.Pressed+=OnDoorButtonPressed;tools.AddChild(_doorButton);ApplyButtonStyle(_doorButton,100,30);
        _bedButton=new Button{Name="BasicBedToolButton",Text="\uCE68\uB300",ToggleMode=true,FocusMode=Godot.Control.FocusModeEnum.None,CustomMinimumSize=new Vector2(104,30),MouseFilter=MouseFilterEnum.Stop};_bedButton.Pressed+=OnBedButtonPressed;tools.AddChild(_bedButton);ApplyButtonStyle(_bedButton,104,30);
        _farmButton=new Button{Name="PotatoFarmToolButton",Text="\uAC10\uC790\uBC2D",ToggleMode=true,FocusMode=Godot.Control.FocusModeEnum.None,CustomMinimumSize=new Vector2(88,30),MouseFilter=MouseFilterEnum.Stop};_farmButton.Pressed+=OnFarmButtonPressed;tools.AddChild(_farmButton);ApplyButtonStyle(_farmButton,88,30);
        _demolitionButton=new Button{Name="DemolitionToolButton",Text="\uCCA0\uAC70",ToggleMode=true,FocusMode=Godot.Control.FocusModeEnum.None,CustomMinimumSize=new Vector2(76,30),MouseFilter=MouseFilterEnum.Stop};_demolitionButton.Pressed+=OnDemolitionButtonPressed;tools.AddChild(_demolitionButton);ApplyButtonStyle(_demolitionButton,76,30);
        _floorButton=new Button{Name="WoodenFloorToolButton",Text="\uB098\uBB34 \uBC14\uB2E5",TooltipText="\uB098\uBB34 \uBC14\uB2E5\nWood 1 / Cell\n\uC774\uB3D9 \uC18D\uB3C4 +10%",ToggleMode=true,FocusMode=Godot.Control.FocusModeEnum.None,CustomMinimumSize=new Vector2(96,30),MouseFilter=MouseFilterEnum.Stop};_floorButton.Pressed+=OnFloorButtonPressed;tools.AddChild(_floorButton);ApplyButtonStyle(_floorButton,96,30);
        _floorRemoveButton=new Button{Name="FloorRemovalToolButton",Text="\uBC14\uB2E5 \uC81C\uAC70",TooltipText="\uC644\uC131\uB41C \uBC14\uB2E5\uC744 \uC81C\uAC70\uD558\uACE0 Wood 1\uC744 \uD68C\uC218\uD569\uB2C8\uB2E4.",ToggleMode=true,FocusMode=Godot.Control.FocusModeEnum.None,CustomMinimumSize=new Vector2(96,30),MouseFilter=MouseFilterEnum.Stop};_floorRemoveButton.Pressed+=OnFloorRemoveButtonPressed;tools.AddChild(_floorRemoveButton);ApplyButtonStyle(_floorRemoveButton,96,30);
        _workbenchButton=new Button{Name="ProcessingWorkbenchToolButton",Text="\uAC00\uACF5 \uC791\uC5C5\uB300",TooltipText="목재 20 + 돌 5",ToggleMode=true,FocusMode=Godot.Control.FocusModeEnum.None,MouseFilter=MouseFilterEnum.Stop};_workbenchButton.Pressed+=OnWorkbenchButtonPressed;tools.AddChild(_workbenchButton);ApplyButtonStyle(_workbenchButton,104,30);
        _furnaceButton=new Button{Name="BasicFurnaceToolButton",Text="\uC6A9\uAD11\uB85C",TooltipText="돌 30 + 목재 10",ToggleMode=true,FocusMode=Godot.Control.FocusModeEnum.None,MouseFilter=MouseFilterEnum.Stop};_furnaceButton.Pressed+=OnFurnaceButtonPressed;tools.AddChild(_furnaceButton);ApplyButtonStyle(_furnaceButton,84,30);
        _kitchenButton=new Button{Name="FieldKitchenToolButton",Text="\uC57C\uC804 \uCDE8\uC0AC\uB300",TooltipText="\uBAA9\uC7AC \uD310\uC790 8 + \uC11D\uC7AC \uBE14\uB85D 4",ToggleMode=true,FocusMode=Godot.Control.FocusModeEnum.None,MouseFilter=MouseFilterEnum.Stop};_kitchenButton.Pressed+=OnKitchenButtonPressed;tools.AddChild(_kitchenButton);ApplyButtonStyle(_kitchenButton,98,30);
        _apothecaryButton=new Button{Name="ApothecaryTableToolButton",Text="\uC57D\uC81C \uC791\uC5C5\uB300",TooltipText="\uBAA9\uC7AC \uD310\uC790 10 + \uC11D\uC7AC \uBE14\uB85D 4 + \uCC9C 2",ToggleMode=true,FocusMode=Godot.Control.FocusModeEnum.None,MouseFilter=MouseFilterEnum.Stop};_apothecaryButton.Pressed+=OnApothecaryButtonPressed;tools.AddChild(_apothecaryButton);ApplyButtonStyle(_apothecaryButton,98,30);
        CreateCategoryTabs(body);
        CreateMaterialPicker(body);
        ApplyCategory(ConstructionDisplayCategoryV3.Structure);
    }

    private void CreateCategoryTabs(VBoxContainer body)
    {
        _categoryTabs=new HBoxContainer{Name="ConstructionCategoryTabs",MouseFilter=MouseFilterEnum.Ignore};body.AddChild(_categoryTabs);body.MoveChild(_categoryTabs,1);
        foreach(var entry in new[]{(ConstructionDisplayCategoryV3.Structure,"\uAD6C\uC870"),(ConstructionDisplayCategoryV3.FurnitureAndLiving,"\uAC00\uAD6C\u00B7\uC0DD\uD65C"),(ConstructionDisplayCategoryV3.Production,"\uC0DD\uC0B0"),(ConstructionDisplayCategoryV3.Zone,"\uAD6C\uC5ED")}){Button button=new(){Text=entry.Item2,ToggleMode=true,MouseFilter=MouseFilterEnum.Stop,FocusMode=Godot.Control.FocusModeEnum.None};ConstructionDisplayCategoryV3 category=entry.Item1;button.Pressed+=()=>ApplyCategory(category);_categoryTabs.AddChild(button);_categoryButtons.Add(category,button);ApplyButtonStyle(button,92,26);}
    }
    private void CreateMaterialPicker(VBoxContainer body)
    {
        _materialPicker=new HBoxContainer{Name="MaterialPicker",Visible=false,MouseFilter=MouseFilterEnum.Stop};body.AddChild(_materialPicker);foreach(var definition in ConstructionMaterialContentV3.GetAllowed(ConstructionMaterialTagV3.Structural|ConstructionMaterialTagV3.Door)){Button button=new(){Text=definition.DisplayName,ToggleMode=true,MouseFilter=MouseFilterEnum.Stop,FocusMode=Godot.Control.FocusModeEnum.None,TooltipText=definition.ShortDescription};ResourceTypeV3 material=definition.ResourceType;button.Pressed+=()=>SelectMaterial(material);_materialPicker.AddChild(button);_materialButtons.Add(material,button);ApplyButtonStyle(button,112,28);}_placementSummary=new Label{Name="PlacementSummary",Visible=false,MouseFilter=MouseFilterEnum.Ignore};body.AddChild(_placementSummary);
    }
    private void ApplyCategory(ConstructionDisplayCategoryV3 category){if(category!=ConstructionDisplayCategoryV3.Structure){if(_wallButton?.ButtonPressed==true)SetWallTool(false);if(_doorButton?.ButtonPressed==true)SetDoorTool(false);SetMaterialPickerVisible(false);}_activeCategory=category;foreach(var pair in _categoryButtons)pair.Value.ButtonPressed=pair.Key==category;bool structure=category==ConstructionDisplayCategoryV3.Structure,living=category==ConstructionDisplayCategoryV3.FurnitureAndLiving,production=category==ConstructionDisplayCategoryV3.Production,zone=category==ConstructionDisplayCategoryV3.Zone;if(_wallButton!=null)_wallButton.Visible=structure;if(_doorButton!=null)_doorButton.Visible=structure;if(_floorButton!=null)_floorButton.Visible=structure;if(_floorRemoveButton!=null)_floorRemoveButton.Visible=structure;if(_demolitionButton!=null)_demolitionButton.Visible=structure;if(_bedButton!=null)_bedButton.Visible=living;if(_workbenchButton!=null)_workbenchButton.Visible=production;if(_furnaceButton!=null)_furnaceButton.Visible=production;if(_kitchenButton!=null)_kitchenButton.Visible=production;if(_apothecaryButton!=null)_apothecaryButton.Visible=production;if(_stockpileButton!=null)_stockpileButton.Visible=zone;if(_removeButton!=null)_removeButton.Visible=zone;if(_farmButton!=null)_farmButton.Visible=zone;}
    private void SelectMaterial(ResourceTypeV3 material){_selectedMaterial=material;foreach(var pair in _materialButtons)pair.Value.ButtonPressed=pair.Key==material;ConstructionMaterialChanged?.Invoke(material);RefreshMaterialSummary(1,true);GetViewport().SetInputAsHandled();}
    public void RefreshMaterialSummary(int validCellCount,bool valid){if(_materialPicker?.Visible!=true||_placementSummary==null)return;if(!ConstructionMaterialContentV3.TryGet(_selectedMaterial,out var material)||material==null)return;int perCell=_doorButton?.ButtonPressed==true?6:5,total=Math.Max(0,validCellCount)*perCell,available=0;if(GameplaySessionV3.TryGetProductionSession(out ProductionSessionV3? production)&&production!=null&&_manager!=null)available=production.GetResourceAvailability(_manager.LocalCompanyId,_selectedMaterial);int missing=Math.Max(0,total-available);string text=$"{material.DisplayName} \u00B7 \uCD1D \uD544\uC694 {total} \u00B7 \uBCF4\uC720 {available}"+(missing>0?$" \u00B7 \uBD80\uC871 {missing}":"")+(valid?"":" \u00B7 \uBC30\uCE58 \uBD88\uAC00");if(_placementSummary.Text!=text)_placementSummary.Text=text;_placementSummary.Visible=true;}
    private void SetMaterialPickerVisible(bool visible){if(_materialPicker!=null)_materialPicker.Visible=visible;if(_placementSummary!=null)_placementSummary.Visible=visible;ApplyTrayHeight(visible?180:142);if(visible)SelectMaterial(_selectedMaterial);}
    private void ApplyTrayHeight(float height){if(_tray==null)return;_tray.OffsetTop=-height;_tray.OffsetBottom=-12;}
    private static void ConfigureRect(Godot.Control control,float left,float top,float right,float bottom){control.AnchorLeft=0;control.AnchorTop=1;control.AnchorRight=0;control.AnchorBottom=1;control.OffsetLeft=left;control.OffsetTop=top;control.OffsetRight=right;control.OffsetBottom=bottom;}

    private void OnViewportSizeChanged()
    {
        if (_trayLayoutQueued || !IsInsideTree()) return;
        _trayLayoutQueued=true;
        CallDeferred(MethodName.RefreshTrayLayoutDeferred);
    }

    private void RefreshTrayLayoutDeferred()
    {
        _trayLayoutQueued=false;
        if (IsInsideTree()) ApplyTrayLayout();
    }

    private void ApplyTrayLayout()
    {
        if (_tray==null) return;
        float viewportWidth=Mathf.Max(0,GetViewportRect().Size.X);
        float trayWidth=ConstructionUiSelfCheckV3.CalculateTrayWidth(viewportWidth);
        float left=ConstructionUiSelfCheckV3.CalculateTrayLeft(viewportWidth);
        _tray.OffsetLeft=left;
        _tray.OffsetRight=left+trayWidth;
    }

    private void OnTrayGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton or InputEventMouseMotion) GetViewport().SetInputAsHandled();
    }

    private void OnToolsScrollGuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouse || !mouse.Pressed || (mouse.ButtonIndex!=MouseButton.WheelUp && mouse.ButtonIndex!=MouseButton.WheelDown)) return;
        if (_toolsScroll!=null)
        {
            int direction=mouse.ButtonIndex==MouseButton.WheelDown?1:-1;
            _toolsScroll.ScrollHorizontal=Math.Max(0,_toolsScroll.ScrollHorizontal+direction*112);
        }
        GetViewport().SetInputAsHandled();
    }

    private void EnsureToolVisible(Godot.Control? control)
    {
        if (_toolsScroll==null || control==null || !IsInsideTree()) return;
        _toolsScroll.EnsureControlVisible(control);
    }

    private void AddConstructionIcon(Button button){Texture2D? texture=GD.Load<Texture2D>("res://assets/ui/icons/construction.svg");if(texture==null){GD.PushWarning("[ConstructionUiV3] construction.svg could not be loaded.");return;}TextureRect icon=new(){Name="ConstructionIcon",Texture=texture,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=MouseFilterEnum.Ignore,CustomMinimumSize=new Vector2(22,22),Size=new Vector2(22,22)};icon.SetAnchorsAndOffsetsPreset(LayoutPreset.Center,LayoutPresetMode.KeepSize,12);button.AddChild(icon);}
    private static StyleBoxFlat CreatePanelStyle(){StyleBoxFlat style=new(){BgColor=new Color(0.055f,0.045f,0.07f,0.94f),BorderColor=new Color(0.58f,0.46f,0.68f,0.85f)};style.SetBorderWidthAll(1);style.SetCornerRadiusAll(2);style.ContentMarginLeft=8;style.ContentMarginRight=8;style.ContentMarginTop=5;style.ContentMarginBottom=5;return style;}
    private static void ApplyButtonStyle(Button button,float width,float height){button.CustomMinimumSize=new Vector2(width,height);StyleBoxFlat normal=new(){BgColor=new Color(0.12f,0.10f,0.15f,0.96f),BorderColor=new Color(0.40f,0.34f,0.48f,0.9f)};normal.SetBorderWidthAll(1);StyleBoxFlat hover=new(){BgColor=new Color(0.19f,0.15f,0.23f,0.98f),BorderColor=new Color(0.68f,0.55f,0.76f,0.95f)};hover.SetBorderWidthAll(1);StyleBoxFlat pressed=new(){BgColor=new Color(0.40f,0.27f,0.48f,0.98f),BorderColor=new Color(0.78f,0.64f,0.86f,1)};pressed.SetBorderWidthAll(1);button.AddThemeStyleboxOverride("normal",normal);button.AddThemeStyleboxOverride("hover",hover);button.AddThemeStyleboxOverride("pressed",pressed);button.AddThemeFontSizeOverride("font_size",12);button.AddThemeColorOverride("font_color",new Color(0.92f,0.90f,0.96f));button.AddThemeColorOverride("font_hover_color",Colors.White);}
    private void OnStockpileButtonPressed(){if(_designation==null)return;SetFloorTool(false);SetFloorRemovalTool(false);SetWallTool(false);SetDoorTool(false);SetBedTool(false);StockpileDesignationModeV3 next=_designation.Mode==StockpileDesignationModeV3.Add?StockpileDesignationModeV3.None:StockpileDesignationModeV3.Add;_designation.SetMode(next);LastConstructionUiAction=next==StockpileDesignationModeV3.Add?"Stockpile Add":"Stockpile Add ended";RefreshManagerState();GetViewport().SetInputAsHandled();}
    private void OnRemoveButtonPressed(){if(_designation==null)return;SetFloorTool(false);SetFloorRemovalTool(false);SetWallTool(false);SetDoorTool(false);SetBedTool(false);StockpileDesignationModeV3 next=_designation.Mode==StockpileDesignationModeV3.Remove?StockpileDesignationModeV3.None:StockpileDesignationModeV3.Remove;_designation.SetMode(next);LastConstructionUiAction=next==StockpileDesignationModeV3.Remove?"Stockpile Remove":"Stockpile Remove ended";RefreshManagerState();GetViewport().SetInputAsHandled();}
    private void OnWallButtonPressed(){bool active=_wallButton?.ButtonPressed==true;if(active){_designation?.SetMode(StockpileDesignationModeV3.None);SetFloorTool(false);SetFloorRemovalTool(false);}SetWallTool(active);GetViewport().SetInputAsHandled();}
    private void OnDoorButtonPressed(){bool active=_doorButton?.ButtonPressed==true;if(active){_designation?.SetMode(StockpileDesignationModeV3.None);SetFloorTool(false);SetFloorRemovalTool(false);}SetDoorTool(active);GetViewport().SetInputAsHandled();}
    private void OnBedButtonPressed(){bool active=_bedButton?.ButtonPressed==true;if(active){_designation?.SetMode(StockpileDesignationModeV3.None);SetFloorTool(false);SetFloorRemovalTool(false);}SetBedTool(active);GetViewport().SetInputAsHandled();}
    private void OnFarmButtonPressed(){bool active=_farmButton?.ButtonPressed==true;if(active){_designation?.SetMode(StockpileDesignationModeV3.None);SetFloorTool(false);SetFloorRemovalTool(false);}SetFarmTool(active);GetViewport().SetInputAsHandled();}
    private void OnDemolitionButtonPressed(){bool active=_demolitionButton?.ButtonPressed==true;if(active){_designation?.SetMode(StockpileDesignationModeV3.None);SetFloorTool(false);SetFloorRemovalTool(false);}SetDemolitionTool(active);GetViewport().SetInputAsHandled();}
    private void OnFloorButtonPressed(){bool active=_floorButton?.ButtonPressed==true;if(active)_designation?.SetMode(StockpileDesignationModeV3.None);SetFloorTool(active);GetViewport().SetInputAsHandled();}
    private void OnFloorRemoveButtonPressed(){bool active=_floorRemoveButton?.ButtonPressed==true;if(active)_designation?.SetMode(StockpileDesignationModeV3.None);SetFloorRemovalTool(active);GetViewport().SetInputAsHandled();}
    private void OnKitchenButtonPressed(){bool active=_kitchenButton?.ButtonPressed==true;if(active){_designation?.SetMode(StockpileDesignationModeV3.None);ClearNonFloorButtons();_kitchenButton!.ButtonPressed=true;EnsureToolVisible(_kitchenButton);}ConstructionToolChanged?.Invoke(ConstructionPlacementToolKindV3.FieldKitchen,active);RefreshManagerState();GetViewport().SetInputAsHandled();}
    private void OnApothecaryButtonPressed(){bool active=_apothecaryButton?.ButtonPressed==true;if(active){_designation?.SetMode(StockpileDesignationModeV3.None);ClearNonFloorButtons();_apothecaryButton!.ButtonPressed=true;EnsureToolVisible(_apothecaryButton);}ConstructionToolChanged?.Invoke(ConstructionPlacementToolKindV3.ApothecaryTable,active);RefreshManagerState();GetViewport().SetInputAsHandled();}
    private void OnWorkbenchButtonPressed(){SetProductionTool(ConstructionPlacementToolKindV3.ProcessingWorkbench,_workbenchButton?.ButtonPressed==true);GetViewport().SetInputAsHandled();}
    private void OnFurnaceButtonPressed(){SetProductionTool(ConstructionPlacementToolKindV3.BasicFurnace,_furnaceButton?.ButtonPressed==true);GetViewport().SetInputAsHandled();}
    private void SetProductionTool(ConstructionPlacementToolKindV3 kind,bool active){if(active){_designation?.SetMode(StockpileDesignationModeV3.None);ClearNonFloorButtons();if(_floorButton!=null)_floorButton.ButtonPressed=false;if(_floorRemoveButton!=null)_floorRemoveButton.ButtonPressed=false;}SetMaterialPickerVisible(false);if(_workbenchButton!=null)_workbenchButton.ButtonPressed=active&&kind==ConstructionPlacementToolKindV3.ProcessingWorkbench;if(_furnaceButton!=null)_furnaceButton.ButtonPressed=active&&kind==ConstructionPlacementToolKindV3.BasicFurnace;if(active)EnsureToolVisible(kind==ConstructionPlacementToolKindV3.ProcessingWorkbench?_workbenchButton:_furnaceButton);RefreshActiveConstructionTool();ConstructionToolChanged?.Invoke(kind,active);RefreshManagerState();}
    public void SetWallTool(bool active){if(active){if(_demolitionButton?.ButtonPressed==true)SetDemolitionTool(false);if(_doorButton!=null)_doorButton.ButtonPressed=false;if(_bedButton!=null)_bedButton.ButtonPressed=false;if(_farmButton?.ButtonPressed==true)SetFarmTool(false);}if(_wallButton!=null)_wallButton.ButtonPressed=active;if(active)EnsureToolVisible(_wallButton);RefreshActiveConstructionTool();LastConstructionUiAction=active?"Wall selected":"Wall cancelled";ConstructionToolChanged?.Invoke(ConstructionPlacementToolKindV3.WoodenWall,active);SetMaterialPickerVisible(active);RefreshManagerState();}
    public void SetDoorTool(bool active){if(active){if(_demolitionButton?.ButtonPressed==true)SetDemolitionTool(false);if(_wallButton!=null)_wallButton.ButtonPressed=false;if(_bedButton!=null)_bedButton.ButtonPressed=false;if(_farmButton?.ButtonPressed==true)SetFarmTool(false);}if(_doorButton!=null)_doorButton.ButtonPressed=active;if(active)EnsureToolVisible(_doorButton);RefreshActiveConstructionTool();LastConstructionUiAction=active?"Door selected":"Door cancelled";ConstructionToolChanged?.Invoke(ConstructionPlacementToolKindV3.WoodenDoor,active);SetMaterialPickerVisible(active);RefreshManagerState();}
    public void SetBedTool(bool active){if(active){if(_demolitionButton?.ButtonPressed==true)SetDemolitionTool(false);if(_wallButton!=null)_wallButton.ButtonPressed=false;if(_doorButton!=null)_doorButton.ButtonPressed=false;if(_farmButton?.ButtonPressed==true)SetFarmTool(false);SetMaterialPickerVisible(false);}if(_bedButton!=null)_bedButton.ButtonPressed=active;if(active)EnsureToolVisible(_bedButton);RefreshActiveConstructionTool();LastConstructionUiAction=active?"Basic Bed selected":"Basic Bed cancelled";ConstructionToolChanged?.Invoke(ConstructionPlacementToolKindV3.BasicBed,active);RefreshManagerState();}
    public void SetFarmTool(bool active){if(active){if(_demolitionButton?.ButtonPressed==true)SetDemolitionTool(false);if(_wallButton!=null)_wallButton.ButtonPressed=false;if(_doorButton!=null)_doorButton.ButtonPressed=false;if(_bedButton!=null)_bedButton.ButtonPressed=false;}if(_farmButton!=null)_farmButton.ButtonPressed=active;if(active)EnsureToolVisible(_farmButton);RefreshActiveConstructionTool();LastConstructionUiAction=active?"Potato Farm selected":"Potato Farm cancelled";FarmToolChanged?.Invoke(active);RefreshManagerState();}
    public void SetDemolitionTool(bool active){if(active){if(_wallButton!=null)_wallButton.ButtonPressed=false;if(_doorButton!=null)_doorButton.ButtonPressed=false;if(_bedButton!=null)_bedButton.ButtonPressed=false;if(_farmButton?.ButtonPressed==true)SetFarmTool(false);}if(_demolitionButton!=null)_demolitionButton.ButtonPressed=active;if(active)EnsureToolVisible(_demolitionButton);RefreshActiveConstructionTool();LastConstructionUiAction=active?"Demolition selected":"Demolition cancelled";DemolitionToolChanged?.Invoke(active);RefreshManagerState();}
    public void SetFloorTool(bool active){if(active)ClearNonFloorButtons();if(_floorButton!=null)_floorButton.ButtonPressed=active;if(active&&_floorRemoveButton!=null)_floorRemoveButton.ButtonPressed=false;if(active)EnsureToolVisible(_floorButton);RefreshActiveConstructionTool();LastConstructionUiAction=active?"Wooden Floor selected":"Wooden Floor cancelled";ConstructionToolChanged?.Invoke(ConstructionPlacementToolKindV3.WoodenFloor,active);RefreshManagerState();}
    public void SetFloorRemovalTool(bool active){if(active)ClearNonFloorButtons();if(_floorRemoveButton!=null)_floorRemoveButton.ButtonPressed=active;if(active&&_floorButton!=null)_floorButton.ButtonPressed=false;if(active)EnsureToolVisible(_floorRemoveButton);RefreshActiveConstructionTool();LastConstructionUiAction=active?"Floor Removal selected":"Floor Removal cancelled";ConstructionToolChanged?.Invoke(ConstructionPlacementToolKindV3.FloorRemoval,active);RefreshManagerState();}
    private void ClearNonFloorButtons(){SetMaterialPickerVisible(false);if(_wallButton!=null)_wallButton.ButtonPressed=false;if(_doorButton!=null)_doorButton.ButtonPressed=false;if(_bedButton!=null)_bedButton.ButtonPressed=false;if(_workbenchButton!=null)_workbenchButton.ButtonPressed=false;if(_furnaceButton!=null)_furnaceButton.ButtonPressed=false;if(_kitchenButton!=null)_kitchenButton.ButtonPressed=false;if(_apothecaryButton!=null)_apothecaryButton.ButtonPressed=false;if(_farmButton?.ButtonPressed==true)SetFarmTool(false);if(_demolitionButton?.ButtonPressed==true)SetDemolitionTool(false);}
    private void OnDesignationModeChanged(StockpileDesignationModeV3 mode){if(mode!=StockpileDesignationModeV3.None){SetWallTool(false);SetDoorTool(false);SetBedTool(false);SetFarmTool(false);SetDemolitionTool(false);}if(_stockpileButton!=null)_stockpileButton.ButtonPressed=mode==StockpileDesignationModeV3.Add;if(_removeButton!=null)_removeButton.ButtonPressed=mode==StockpileDesignationModeV3.Remove;RefreshActiveConstructionTool();LastConstructionUiAction=mode switch{StockpileDesignationModeV3.Add=>"Stockpile Add entered",StockpileDesignationModeV3.Remove=>"Stockpile Remove entered",_=>"Designation cancelled"};RefreshManagerState();}
    private void RefreshActiveConstructionTool(){ActiveConstructionTool=_designation?.Mode switch{StockpileDesignationModeV3.Add=>"StockpileAdd",StockpileDesignationModeV3.Remove=>"StockpileRemove",_=>_floorRemoveButton?.ButtonPressed==true?"FloorRemoval":_floorButton?.ButtonPressed==true?"WoodenFloor":_workbenchButton?.ButtonPressed==true?"ProcessingWorkbench":_furnaceButton?.ButtonPressed==true?"BasicFurnace":_demolitionButton?.ButtonPressed==true?"Demolition":_farmButton?.ButtonPressed==true?"PotatoFarm":_bedButton?.ButtonPressed==true?"BasicBed":_doorButton?.ButtonPressed==true?"WoodenDoor":_wallButton?.ButtonPressed==true?"WoodenWall":"-"};}
    private void SetTrayOpen(bool open,string action){if(_tray==null||_tray.Visible==open)return;_tray.Visible=open;if(!open){SetWallTool(false);SetDoorTool(false);SetBedTool(false);SetFarmTool(false);SetDemolitionTool(false);SetFloorTool(false);SetFloorRemovalTool(false);}LastConstructionUiAction=open?"Construction tray opened":action=="escape"?"Construction tray closed":"Construction tray closed";if(_toggleButton!=null)_toggleButton.ButtonPressed=open;TrayVisibilityChanged?.Invoke(open);RefreshManagerState();if(open)GD.Print("[ConstructionUiV3] Construction tray opened");else GD.Print("[ConstructionUiV3] Construction tray closed");}
    private void RefreshManagerState(){_manager?.SetConstructionUiState(IsTrayOpen,ActiveConstructionTool,_worldMapBlocked,LastConstructionUiAction);if(_tray!=null)_manager?.SetConstructionUiGlobalRect(_tray.GetGlobalRect());}
}

public static class ConstructionUiSelfCheckV3
{
    public static bool TryValidate(out string reason)
    {
        if(!IsColor(StockpileOverlayV3.ZoneFillColor,0.714f,0.541f,0.847f,0.22f)){reason="Stockpile fill color drifted.";return false;}
        if(!IsColor(StockpileOverlayV3.ZoneBorderColor,0.776f,0.627f,0.894f,0.78f)){reason="Stockpile border color drifted.";return false;}
        if(!IsColor(StockpileOverlayV3.AddPreviewColor,0.824f,0.694f,0.922f,0.32f)){reason="Stockpile add preview color drifted.";return false;}
        if(!IsColor(StockpileOverlayV3.RemovePreviewColor,0.780f,0.478f,0.616f,0.30f)){reason="Stockpile remove preview color drifted.";return false;}
        if(!ValidateTrayLayouts(out reason))return false;
        reason=string.Empty;return true;
    }
    internal static float CalculateTrayWidth(float viewportWidth)=>Mathf.Min(ConstructionUiV3.ConstructionTrayMaxWidth,Mathf.Max(0,viewportWidth)*ConstructionUiV3.ConstructionTrayMaxViewportRatio);
    internal static float CalculateTrayLeft(float viewportWidth)=>(Mathf.Max(0,viewportWidth)-CalculateTrayWidth(viewportWidth))*0.5f;
    internal static bool NeedsHorizontalScroll(int buttonCount,float viewportWidth){float contentWidth=Mathf.Max(0,buttonCount)*104f+Mathf.Max(0,buttonCount-1)*4f;return contentWidth>CalculateTrayWidth(viewportWidth);}
    private static bool ValidateTrayLayouts(out string reason)
    {
        foreach(float viewportWidth in new[]{1024f,1280f,1920f})
        {
            float width=CalculateTrayWidth(viewportWidth),left=CalculateTrayLeft(viewportWidth);
            if(left<0||left+width>viewportWidth+0.01f||width>viewportWidth*ConstructionUiV3.ConstructionTrayMaxViewportRatio+0.01f||width>ConstructionUiV3.ConstructionTrayMaxWidth){reason=$"Tray bounds invalid at {viewportWidth}.";return false;}
            if(NeedsHorizontalScroll(4,viewportWidth)||!NeedsHorizontalScroll(12,viewportWidth)||!NeedsHorizontalScroll(24,viewportWidth)){reason=$"Tray scroll policy invalid at {viewportWidth}.";return false;}
        }
        if(ConstructionUiV3.ConstructionTrayButtonCount<1){reason="Construction button count is invalid.";return false;}
        reason=string.Empty;return true;
    }
    private static bool IsColor(Color actual,float r,float g,float b,float a)=>Mathf.IsEqualApprox(actual.R,r)&&Mathf.IsEqualApprox(actual.G,g)&&Mathf.IsEqualApprox(actual.B,b)&&Mathf.IsEqualApprox(actual.A,a);
}
