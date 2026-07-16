using System;
using System.Collections.Generic;
using GameplayV3.Navigation;
using GameplayV3.Resources;
using GameplayV3.Farming;
using GameplayV3.Session;
using Godot;
using WorldV2;

namespace GameplayV3.Stockpile.Runtime;

public partial class StockpileOverlayV3 : Node2D
{
    public static readonly Color ZoneFillColor=new(0.714f,0.541f,0.847f,0.22f);
    public static readonly Color ZoneBorderColor=new(0.776f,0.627f,0.894f,0.78f);
    public static readonly Color AddPreviewColor=new(0.824f,0.694f,0.922f,0.32f);
    public static readonly Color RemovePreviewColor=new(0.780f,0.478f,0.616f,0.30f);
    private StockpileSessionV3? _session;private WorldV2GridRenderer? _grid;private string _localCompany=string.Empty;private IReadOnlyList<GlobalCellCoord> _preview=Array.Empty<GlobalCellCoord>();private StockpileDesignationModeV3 _previewMode;private long _lastRequestedRegistryRevision=long.MinValue;private int _previewRevision;private int _lastRequestedPreviewRevision=-1;
    public StockpileSessionV3? BoundSession=>_session;
    public long BoundRegistryRevision=>_session?.Zones.Revision??-1;
    public long LastObservedRegistryRevision{get;private set;}=-1;
    public int RedrawRequestCount{get;private set;}
    public int LastDrawnZoneCount{get;private set;}
    public int LastDrawnCellCount{get;private set;}
    public int LastDrawnFillRectCount{get;private set;}
    public int LastDrawnBorderEdgeCount{get;private set;}
    public Vector2I LastDrawnFirstCell{get;private set;}
    public Rect2 LastDrawnFirstRect{get;private set;}
    public void Initialize(StockpileSessionV3 session,WorldV2GridRenderer grid,string localCompany)=>BindSession(session,grid,localCompany);
    public void BindSession(StockpileSessionV3 session,WorldV2GridRenderer grid,string localCompany){_session=session;_grid=grid;_localCompany=localCompany;Visible=true;ZIndex=1;_preview=Array.Empty<GlobalCellCoord>();_previewMode=StockpileDesignationModeV3.None;_previewRevision++;RequestRedraw(true);}
    public void SetPreview(IReadOnlyList<GlobalCellCoord> cells,StockpileDesignationModeV3 mode){if(mode==_previewMode&&SameCells(_preview,cells))return;_preview=cells;_previewMode=mode;_previewRevision++;RequestRedraw();}
    public void ClearPreview(){if(_preview.Count==0)return;_preview=Array.Empty<GlobalCellCoord>();_previewRevision++;RequestRedraw();}
    public void Refresh()=>RequestRedraw();
    private void RequestRedraw(bool force=false){long registryRevision=BoundRegistryRevision;if(force||registryRevision!=_lastRequestedRegistryRevision||_previewRevision!=_lastRequestedPreviewRevision){_lastRequestedRegistryRevision=registryRevision;_lastRequestedPreviewRevision=_previewRevision;if(RedrawRequestCount<1000000)RedrawRequestCount++;QueueRedraw();}}
    public override void _Draw(){LastObservedRegistryRevision=BoundRegistryRevision;LastDrawnZoneCount=0;LastDrawnCellCount=0;LastDrawnFillRectCount=0;LastDrawnBorderEdgeCount=0;LastDrawnFirstCell=default;LastDrawnFirstRect=default;if(_session==null||_grid==null)return;foreach(string id in _session.Zones.GetAllZoneIds())if(_session.Zones.TryGetZone(id,out StockpileZoneStateV3? zone)&&zone!=null)DrawZone(zone);Color preview=_previewMode==StockpileDesignationModeV3.Remove?RemovePreviewColor:AddPreviewColor;DrawPreview(preview);}
    private void DrawZone(StockpileZoneStateV3 zone){LastDrawnZoneCount++;IReadOnlyCollection<GlobalCellCoord> zoneCells=zone.Cells;HashSet<Vector2I> cells=new();foreach(GlobalCellCoord cell in zoneCells)cells.Add(cell.Value);if(cells.Count>0&&LastDrawnCellCount==0){foreach(GlobalCellCoord first in zoneCells){LastDrawnFirstCell=first.Value;LastDrawnFirstRect=CellRect(first.Value);break;}}Color fill=ZoneFillColor;Color border=ZoneBorderColor;foreach(Vector2I cell in cells){Rect2 rect=CellRect(cell);DrawRect(rect,fill);LastDrawnFillRectCount++;LastDrawnCellCount++;LastDrawnBorderEdgeCount+=DrawBoundary(rect,cell,cells,border,true);}}
    private void DrawPreview(Color color){HashSet<Vector2I> cells=new();foreach(GlobalCellCoord cell in _preview)cells.Add(cell.Value);foreach(Vector2I cell in cells){Rect2 rect=CellRect(cell);DrawRect(rect,color);DrawBoundary(rect,cell,cells,color.Lightened(0.12f),false);}}
    private Rect2 CellRect(Vector2I cell)=>new(cell.X*_grid!.TileSize,cell.Y*_grid.TileSize,_grid.TileSize,_grid.TileSize);
    private int DrawBoundary(Rect2 rect,Vector2I cell,HashSet<Vector2I> cells,Color color,bool record){float width=1f;int edges=0;if(!cells.Contains(cell+Vector2I.Up)){DrawLine(rect.Position,new(rect.End.X,rect.Position.Y),color,width);edges++;}if(!cells.Contains(cell+Vector2I.Right)){DrawLine(new(rect.End.X,rect.Position.Y),rect.End,color,width);edges++;}if(!cells.Contains(cell+Vector2I.Down)){DrawLine(new(rect.Position.X,rect.End.Y),rect.End,color,width);edges++;}if(!cells.Contains(cell+Vector2I.Left)){DrawLine(rect.Position,new(rect.Position.X,rect.End.Y),color,width);edges++;}return record?edges:0;}
    private static bool SameCells(IReadOnlyList<GlobalCellCoord> a,IReadOnlyList<GlobalCellCoord> b){if(a.Count!=b.Count)return false;for(int i=0;i<a.Count;i++)if(a[i].Value!=b[i].Value)return false;return true;}
}

public partial class StockpileDesignationControllerV3 : Node
{
    public const int MaxCellsPerDrag=256;private StockpileSessionV3? _session;private ResourceSessionV3? _resources;private IMercenaryNavigationWorldQueryV3? _query;private WorldV2GridRenderer? _grid;private WorldManagerV2? _manager;private StockpileOverlayV3? _overlay;private Label? _modeLabel;private bool _dragging;private Vector2 _startScreen;
    public StockpileDesignationModeV3 Mode=>_session?.Diagnostics.DesignationMode??StockpileDesignationModeV3.None;
    public event Action<StockpileDesignationModeV3>? ModeChanged;
    public void Initialize(StockpileSessionV3 session,ResourceSessionV3 resources,IMercenaryNavigationWorldQueryV3 query,WorldV2GridRenderer grid,WorldManagerV2 manager,StockpileOverlayV3 overlay,CanvasLayer canvas){_session=session;_resources=resources;_query=query;_grid=grid;_manager=manager;_overlay=overlay;_modeLabel=new Label{Name="StockpileModeLabel",Position=new Vector2(12,12),MouseFilter=Godot.Control.MouseFilterEnum.Ignore,Visible=false};_modeLabel.AddThemeFontSizeOverride("font_size",13);_modeLabel.AddThemeColorOverride("font_color",new Color(0.55f,1f,0.92f));canvas.AddChild(_modeLabel);}
    public bool TryHandleInput(InputEvent e)
    {
        if(_session==null)return false;if(e is InputEventKey key&&key.Pressed&&!key.Echo&&key.Keycode==Key.P){SetMode(key.ShiftPressed?StockpileDesignationModeV3.Remove:Mode==StockpileDesignationModeV3.Add?StockpileDesignationModeV3.None:StockpileDesignationModeV3.Add);return true;}
        if(Mode==StockpileDesignationModeV3.None)return false;if(e is InputEventKey escape&&escape.Pressed&&!escape.Echo&&escape.Keycode==Key.Escape){SetMode(StockpileDesignationModeV3.None);return true;}if(e is InputEventMouseButton button){if(button.ButtonIndex==MouseButton.Right&&button.Pressed){SetMode(StockpileDesignationModeV3.None);return true;}if(button.ButtonIndex==MouseButton.Left){return HandlePointer(button.Position,button.Pressed);}}if(e is InputEventMouseMotion motion&&_dragging){UpdatePreview(motion.Position);return true;}return false;
    }
    public void SetMode(StockpileDesignationModeV3 mode){if(_session==null)return;_session.Diagnostics.DesignationMode=mode;_dragging=false;_overlay?.ClearPreview();if(_modeLabel!=null){_modeLabel.Visible=mode!=StockpileDesignationModeV3.None;_modeLabel.Text=mode==StockpileDesignationModeV3.Add?"Stockpile: Add":mode==StockpileDesignationModeV3.Remove?"Stockpile: Remove":string.Empty;}_manager?.UpdateDebugHud(mode==StockpileDesignationModeV3.None?"Stockpile designation ended.":$"Stockpile designation: {mode}");ModeChanged?.Invoke(mode);}
    public bool HandlePointer(Vector2 screen,bool pressed){if(Mode==StockpileDesignationModeV3.None)return false;if(pressed){_dragging=true;_startScreen=screen;UpdatePreview(screen);return true;}if(!_dragging)return false;IReadOnlyList<GlobalCellCoord> cells=BuildCells(_startScreen,screen);_dragging=false;_overlay?.ClearPreview();Apply(cells);return true;}
    private void UpdatePreview(Vector2 screen)=>_overlay?.SetPreview(BuildCells(_startScreen,screen),Mode);
    private IReadOnlyList<GlobalCellCoord> BuildCells(Vector2 a,Vector2 b){List<GlobalCellCoord> result=new();Vector2 wa=GetViewport().GetCanvasTransform().AffineInverse()*a,wb=GetViewport().GetCanvasTransform().AffineInverse()*b;Vector2I ca=_grid!.WorldToCell(wa),cb=_grid.WorldToCell(wb);int minX=Math.Min(ca.X,cb.X),maxX=Math.Max(ca.X,cb.X),minY=Math.Min(ca.Y,cb.Y),maxY=Math.Max(ca.Y,cb.Y);GameplaySessionV3.TryGetFarmSession(out FarmSessionV3? farm);for(int y=minY;y<=maxY&&result.Count<MaxCellsPerDrag;y++)for(int x=minX;x<=maxX&&result.Count<MaxCellsPerDrag;x++){Vector2I cell=new(x,y);GlobalCellCoord global=new(cell);if(!_query!.IsInsideWorld(cell)||!_query.IsWalkable(cell)||_resources!.Nodes.ContainsCell(cell))continue;if(Mode==StockpileDesignationModeV3.Add&&(_session!.Zones.IsStockpileCell(global)||farm?.Plots.ContainsCell(cell)==true))continue;result.Add(global);}return result.AsReadOnly();}
    private void Apply(IReadOnlyList<GlobalCellCoord> cells){if(_session==null||_manager==null)return;if(cells.Count==0){_session.Diagnostics.LastFailureReason="No valid stockpile cells.";_manager.UpdateDebugHud(_session.Diagnostics.LastFailureReason);return;}if(Mode==StockpileDesignationModeV3.Add){if(_session.Zones.TryCreateZone(_manager.LocalCompanyId,cells,_manager.WorldBounds,out StockpileZoneStateV3? zone,out string reason)){_session.Diagnostics.CreatedZoneCount++;_session.Diagnostics.LastFailureReason=string.Empty;_overlay?.Refresh();GD.Print($"[StockpileV3] zone created id={zone!.StockpileZoneId} cells={zone.CellCount} coords={FormatCells(zone)} registryRevision={_session.Zones.Revision}");_manager.UpdateDebugHud($"Stockpile created ({zone.CellCount} cells).");}else{_session.Diagnostics.LastFailureReason=reason;_manager.UpdateDebugHud(reason);}return;}
        Dictionary<string,List<GlobalCellCoord>> byZone=new(StringComparer.Ordinal);foreach(GlobalCellCoord cell in cells)if(_session.Zones.TryGetZoneAtCell(cell,out StockpileZoneStateV3? zone)&&zone?.CompanyId==_manager.LocalCompanyId){if(!byZone.TryGetValue(zone.StockpileZoneId,out List<GlobalCellCoord>? list)){list=new();byZone.Add(zone.StockpileZoneId,list);}list.Add(cell);}int removed=0;foreach(var pair in byZone){int before=_session.Zones.TryGetZone(pair.Key,out StockpileZoneStateV3? zone)&&zone!=null?zone.CellCount:0;if(_session.Zones.TryRemoveCells(pair.Key,_manager.LocalCompanyId,pair.Value,out _,out string reason))removed+=Math.Min(before,pair.Value.Count);else _session.Diagnostics.LastFailureReason=reason;}_session.Diagnostics.RemovedCellCount+=removed;_overlay?.Refresh();_manager.UpdateDebugHud($"Removed {removed} stockpile cells.");}
    private static string FormatCells(StockpileZoneStateV3 zone){List<string> cells=new();foreach(GlobalCellCoord cell in zone.Cells){if(cells.Count>=4)break;cells.Add(cell.ToString());}return string.Join(",",cells);}
}
