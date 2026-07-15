using System;
using GameplayV3.Mercenary;
using GameplayV3.Resources;
using Godot;
using WorldV2;

namespace GameplayV3.Mercenary.Runtime;

public partial class MercenaryEntityV3 : Node2D
{
    private Color _bodyColor = new(0.25f, 0.90f, 0.82f, 1.0f);
    private Label? _nameLabel;
    private Label? _initialLabel;
    private Sprite2D? _portrait;
    private Sprite2D? _workGroupBadge;
    private bool _selected;
    private bool _working;
    private float _workProgress;
    private bool _carrying;
    private ResourceTypeV3 _carryType;
    private int _carryAmount;

    public string MercenaryId { get; private set; } = string.Empty;
    public bool IsInitialized { get; private set; }

    public bool TryInitialize(
        string mercenaryId,
        MercenaryRegistryV3 registry,
        WorldV2GridRenderer gridRenderer,
        out string reason)
    {
        if (!registry.TryGetMercenary(mercenaryId, out MercenaryProfileV3? profile, out MercenaryStateV3? state)
            || profile == null
            || state == null)
        {
            reason = "Mercenary Profile/State pair is missing.";
            return false;
        }

        MercenaryId = mercenaryId;
        Position = gridRenderer.CellToWorldCenter(state.CurrentCell.Value);
        _bodyColor = profile.InitialSquadSlotIndex switch
        {
            1 => new Color(0.36f, 0.78f, 1.0f, 1.0f),
            2 => new Color(0.44f, 0.94f, 0.58f, 1.0f),
            _ => new Color(0.25f, 0.90f, 0.82f, 1.0f)
        };
        EnsureRuntimeChildren(profile.DisplayName);
        RefreshTokenVisual(profile.AppearanceKey, profile.DisplayName);
        IsInitialized = true;
        QueueRedraw();
        reason = string.Empty;
        return true;
    }

    public override void _Draw()
    {
        if (!IsInitialized)
        {
            return;
        }

        Color outline = new(0.04f, 0.09f, 0.09f, 0.95f);
        if (_selected) { DrawCircle(Vector2.Zero, 12.0f, new Color(0.30f,1.0f,0.88f,0.24f)); DrawArc(Vector2.Zero,12.0f,0,Mathf.Tau,32,new Color(0.45f,1.0f,0.92f),1.5f); }
        DrawCircle(Vector2.Zero, 9.5f, outline);
        DrawCircle(Vector2.Zero, 8.0f, _bodyColor);
        DrawArc(Vector2.Zero,8.0f,0,Mathf.Tau,24,_bodyColor.Lightened(0.30f),1.0f);
        if (_working)
        {
            DrawRect(new Rect2(-8.0f, 9.5f, 16.0f, 2.0f), new Color(0.05f, 0.07f, 0.07f, 0.9f));
            DrawRect(new Rect2(-8.0f, 9.5f, 16.0f * Mathf.Clamp(_workProgress, 0.0f, 1.0f), 2.0f), new Color(1.0f, 0.78f, 0.22f));
        }
        if(_carrying)
        {
            Color carryColor=_carryType==ResourceTypeV3.Wood?new Color(0.72f,0.40f,0.16f):new Color(0.72f,0.76f,0.82f);
            DrawCircle(new Vector2(9.0f,7.0f),5.5f,new Color(0.04f,0.06f,0.07f,0.95f));
            DrawString(ThemeDB.FallbackFont,new Vector2(5.5f,10.0f),$"{(_carryType==ResourceTypeV3.Wood?'W':'S')}{_carryAmount}",HorizontalAlignment.Left,-1,7,carryColor);
        }
    }

    public void SetSelected(bool selected){if(_selected==selected)return;_selected=selected;QueueRedraw();}
    public void SetWorkFeedback(bool working,float progress){_working=working;_workProgress=Mathf.Clamp(progress,0,1);QueueRedraw();}
    public void SetCarryFeedback(ResourceTypeV3 type,int amount){_carrying=amount>0;_carryType=type;_carryAmount=Math.Max(0,amount);QueueRedraw();}
    public void ClearCarryFeedback(){if(!_carrying)return;_carrying=false;_carryAmount=0;QueueRedraw();}
    public void SetPortraitTexture(Texture2D? texture){EnsureVisualSprites();_portrait!.Texture=texture;_portrait.Visible=texture!=null;if(_initialLabel!=null)_initialLabel.Visible=texture==null;}
    public void SetWorkGroupBadgeTexture(Texture2D? texture){EnsureVisualSprites();_workGroupBadge!.Texture=texture;_workGroupBadge.Visible=texture!=null;}
    public void RefreshTokenVisual(string appearanceKey,string displayName)
    { EnsureVisualSprites();SetPortraitTexture(null);if(_initialLabel!=null){_initialLabel.Text=string.IsNullOrWhiteSpace(displayName)?"?":displayName.Trim()[0].ToString().ToUpperInvariant();}Rotation=0;QueueRedraw(); }

    private void EnsureRuntimeChildren(string displayName)
    {
        Area2D? area = GetNodeOrNull<Area2D>("Area2D");
        if (area == null)
        {
            area = new Area2D
            {
                Name = "Area2D",
                InputPickable = false,
                Monitoring = false,
                Monitorable = false
            };
            CollisionShape2D collision = new()
            {
                Name = "CollisionShape2D",
                Disabled = true,
                Shape = new CircleShape2D { Radius = 8.0f }
            };
            area.AddChild(collision);
            AddChild(area);
        }

        _nameLabel ??= GetNodeOrNull<Label>("NameLabel");
        if (_nameLabel == null)
        {
            _nameLabel = new Label
            {
                Name = "NameLabel",
                Position = new Vector2(-34.0f, 11.0f),
                Size = new Vector2(68.0f, 18.0f),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore
            };
            _nameLabel.AddThemeFontSizeOverride("font_size", 11);
            _nameLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.98f, 0.94f));
            _nameLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.9f));
            _nameLabel.AddThemeConstantOverride("shadow_offset_x", 1);
            _nameLabel.AddThemeConstantOverride("shadow_offset_y", 1);
            AddChild(_nameLabel);
        }

        _nameLabel.Text = displayName;
        EnsureVisualSprites();
    }

    private void EnsureVisualSprites()
    {
        _portrait ??= GetNodeOrNull<Sprite2D>("Portrait");
        if(_portrait==null){_portrait=new Sprite2D{Name="Portrait",Visible=false};AddChild(_portrait);}
        _workGroupBadge ??= GetNodeOrNull<Sprite2D>("WorkGroupBadge");
        if(_workGroupBadge==null){_workGroupBadge=new Sprite2D{Name="WorkGroupBadge",Visible=false,Position=new Vector2(7,7)};AddChild(_workGroupBadge);}
        _initialLabel ??= GetNodeOrNull<Label>("InitialLabel");
        if(_initialLabel==null){_initialLabel=new Label{Name="InitialLabel",Position=new Vector2(-8,-9),Size=new Vector2(16,18),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,MouseFilter=Godot.Control.MouseFilterEnum.Ignore};_initialLabel.AddThemeFontSizeOverride("font_size",10);AddChild(_initialLabel);}
    }
}
