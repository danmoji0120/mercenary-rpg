using System;
using System.Collections.Generic;
using System.Linq;
using GameplayV3.Construction;
using GameplayV3.Mercenary;
using Godot;
using WorldV2;

namespace GameplayV3.Needs.Runtime;

public partial class RestAssignmentOverlayV3:Node2D
{
    private readonly List<Entry> _entries=new();private ConstructionSessionV3? _construction;private MercenaryNeedsSessionV3? _needs;private MercenarySessionV3? _mercenaries;private WorldV2GridRenderer? _grid;private string _companyId=string.Empty;private string _targetMercenaryId=string.Empty;
    private sealed record Entry(IReadOnlyList<GlobalCellCoord> Cells,Color Fill,Color Border,string Label);
    public bool IsAssignmentModeActive{get;private set;}public int CachedFacilityCount=>_entries.Count;public int RedrawRequestCount{get;private set;}
    public void Initialize(ConstructionSessionV3 construction,MercenaryNeedsSessionV3 needs,MercenarySessionV3 mercenaries,WorldV2GridRenderer grid,string companyId){_construction=construction;_needs=needs;_mercenaries=mercenaries;_grid=grid;_companyId=companyId;needs.Assignments.Changed+=Refresh;needs.Reservations.Changed+=Refresh;ZIndex=1;Visible=false;}
    public void SetAssignmentMode(bool active,string targetMercenaryId=""){IsAssignmentModeActive=active;_targetMercenaryId=active?targetMercenaryId:string.Empty;Visible=active;if(active)Refresh();else{_entries.Clear();RequestRedraw();}}
    public void Refresh(){if(!IsAssignmentModeActive||_construction==null||_needs==null)return;_entries.Clear();foreach(StructureStateV3 structure in _construction.Structures.GetStructuresByCompany(_companyId).OrderBy(x=>x.StructureId,StringComparer.Ordinal)){if(!_construction.Definitions.TryGetDefinition(structure.DefinitionId,out var definition)||definition?.RestFacility==null)continue;IReadOnlyList<RestFacilitySlotV3> slots=RestFacilitySlotResolverV3.Resolve(structure,definition);int assigned=0,reserved=0;bool target=false;string ownerName=string.Empty;foreach(var slot in slots){if(_needs.Assignments.TryGetMercenaryBySlot(slot.RestSlotId,out string owner)){assigned++;if(owner==_targetMercenaryId)target=true;if(ownerName.Length==0&&_mercenaries!.Registry.TryGetProfile(owner,out var profile)&&profile!=null)ownerName=profile.DisplayName;}if(_needs.Reservations.IsReserved(slot.RestSlotId))reserved++;}bool demolition=_needs.IsStructureUnderDemolition(structure.StructureId);Color fill,border;string label;if(demolition){fill=new(.8f,.12f,.12f,.20f);border=new(1f,.28f,.28f,.92f);label="철거 중";}else if(target){fill=new(.2f,.75f,.35f,.18f);border=new(.42f,1f,.58f,.9f);label=ownerName.Length>0?ownerName:"배정됨";}else if(assigned<slots.Count){fill=new(.16f,.75f,.72f,.16f);border=new(.35f,1f,.94f,.88f);label="미할당";}else{fill=new(.45f,.25f,.28f,.16f);border=new(.72f,.45f,.48f,.88f);label=reserved>0?"사용 중":ownerName.Length>0?ownerName:"배정됨";}_entries.Add(new(structure.OccupiedCells,fill,border,label));}RequestRedraw();}
    private void RequestRedraw(){RedrawRequestCount++;QueueRedraw();}
    public override void _Draw(){if(!IsAssignmentModeActive||_grid==null)return;float tile=_grid.TileSize;foreach(var entry in _entries){HashSet<Vector2I> cells=new(entry.Cells.Select(x=>x.Value));foreach(var cell in entry.Cells){Rect2 rect=new(new Vector2(cell.Value.X*tile,cell.Value.Y*tile),new Vector2(tile,tile));DrawRect(rect,entry.Fill,true);DrawEdges(rect,cell.Value,cells,entry.Border,tile);}GlobalCellCoord first=entry.Cells[0];DrawString(ThemeDB.FallbackFont,_grid.CellToWorldCenter(first.Value)+new Vector2(-12,-10),entry.Label,HorizontalAlignment.Left,64,10,entry.Border);}}
    private void DrawEdges(Rect2 r,Vector2I cell,HashSet<Vector2I> cells,Color color,float t){if(!cells.Contains(cell+Vector2I.Up))DrawLine(r.Position,r.Position+new Vector2(t,0),color,2);if(!cells.Contains(cell+Vector2I.Right))DrawLine(r.Position+new Vector2(t,0),r.End,color,2);if(!cells.Contains(cell+Vector2I.Down))DrawLine(r.End,r.Position+new Vector2(0,t),color,2);if(!cells.Contains(cell+Vector2I.Left))DrawLine(r.Position+new Vector2(0,t),r.Position,color,2);}
}
