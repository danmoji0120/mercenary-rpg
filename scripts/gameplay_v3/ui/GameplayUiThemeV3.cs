using Godot;

namespace GameplayV3.UI;

public static class GameplayUiThemeV3
{
    public static readonly Color PanelBackground=new("20242c");
    public static readonly Color PanelBackgroundRaised=new("292f39");
    public static readonly Color PanelBorder=new("5a6472");
    public static readonly Color SelectionAccent=new("62a8d8");
    public static readonly Color TextPrimary=new("edf1f5");
    public static readonly Color TextSecondary=new("aeb8c5");
    public static readonly Color StateWarning=new("e4b45d");
    public static readonly Color StateBlocked=new("e07171");
    public static readonly Color StatePositive=new("75c78e");
    public const int PanelPadding=12,RowSpacing=7,ButtonHeight=32,CommandButtonHeight=36,ProgressBarHeight=12,CornerRadius=5,BorderWidth=1;
    public static Theme Shared{get;}=CreateTheme();
    public static int ThemeInstanceCount=>1;
    public static int SharedStyleBoxCount=>5;

    private static Theme CreateTheme()
    {
        Theme theme=new();
        theme.SetColor("font_color","Label",TextPrimary);theme.SetColor("font_color","Button",TextPrimary);theme.SetColor("font_disabled_color","Button",TextSecondary.Darkened(.25f));
        theme.SetFontSize("font_size","Label",14);theme.SetFontSize("font_size","Button",14);theme.SetConstant("separation","VBoxContainer",RowSpacing);theme.SetConstant("separation","HBoxContainer",RowSpacing);
        theme.SetStylebox("panel","PanelContainer",Box(PanelBackground,PanelBorder));theme.SetStylebox("normal","Button",Box(PanelBackgroundRaised,PanelBorder));theme.SetStylebox("hover","Button",Box(PanelBackgroundRaised.Lightened(.08f),SelectionAccent));theme.SetStylebox("pressed","Button",Box(PanelBackgroundRaised.Darkened(.08f),SelectionAccent));theme.SetStylebox("disabled","Button",Box(PanelBackground.Darkened(.12f),PanelBorder.Darkened(.25f)));return theme;
    }
    private static StyleBoxFlat Box(Color background,Color border){StyleBoxFlat box=new(){BgColor=background,BorderColor=border,ContentMarginLeft=PanelPadding,ContentMarginRight=PanelPadding,ContentMarginTop=PanelPadding,ContentMarginBottom=PanelPadding};box.SetBorderWidthAll(BorderWidth);box.SetCornerRadiusAll(CornerRadius);return box;}
}

public static class GameplayUiShellV3
{
    public static void ConfigureSelection(PanelContainer panel){panel.Theme=GameplayUiThemeV3.Shared;panel.MouseFilter=Godot.Control.MouseFilterEnum.Stop;panel.SetAnchorsPreset(Godot.Control.LayoutPreset.BottomLeft);panel.OffsetLeft=14;panel.OffsetBottom=-96;panel.OffsetRight=414;panel.OffsetTop=-376;panel.CustomMinimumSize=new Vector2(360,180);}
    public static void ConfigureSide(PanelContainer panel){panel.Theme=GameplayUiThemeV3.Shared;panel.MouseFilter=Godot.Control.MouseFilterEnum.Stop;panel.SetAnchorsPreset(Godot.Control.LayoutPreset.CenterRight);panel.OffsetLeft=-510;panel.OffsetRight=-14;panel.OffsetTop=-300;panel.OffsetBottom=300;panel.CustomMinimumSize=new Vector2(420,420);}
}
