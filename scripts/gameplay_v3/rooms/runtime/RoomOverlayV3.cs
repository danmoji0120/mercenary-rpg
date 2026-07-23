using System;
using System.Collections.Generic;
using GameplayV3.Construction;
using Godot;
using WorldV2;

namespace GameplayV3.Rooms.Runtime;

public partial class RoomOverlayV3:Node2D
{
    public const int MaxRoomVisualCellsPerFrame=4096;private RoomSessionV3? _session;private WorldV2GridRenderer? _grid;private Vector2I _lastChunk=new(int.MinValue,int.MinValue);public bool OverlayEnabled{get;private set;}
    public void Initialize(RoomSessionV3 session,WorldV2GridRenderer grid){_session=session;_grid=grid;ZIndex=0;Visible=false;session.Registry.Remapped+=OnRemapped;}
    public override void _ExitTree(){if(_session!=null)_session.Registry.Remapped-=OnRemapped;}
    public void Toggle(){OverlayEnabled=!OverlayEnabled;Visible=OverlayEnabled;if(Visible)QueueRedraw();}
    public void HideOverlay(){OverlayEnabled=false;Visible=false;}
    public void Refresh(){if(Visible)QueueRedraw();}
    private void OnRemapped(RoomTopologyRemapV3 remap)=>Refresh();
    public override void _Process(double delta){if(!Visible||_grid==null)return;Vector2 center=GetViewport().GetCanvasTransform().AffineInverse()*(GetViewportRect().Size*.5f);Vector2I chunk=FloorRegistryV3.ChunkOf(_grid.WorldToCell(center));if(chunk!=_lastChunk){_lastChunk=chunk;QueueRedraw();}}
    public override void _Draw(){if(!Visible||_session==null||_grid==null)return;Transform2D inv=GetViewport().GetCanvasTransform().AffineInverse();Vector2 a=inv*Vector2.Zero,b=inv*GetViewportRect().Size;Vector2I minCell=_grid.WorldToCell(new(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y))),maxCell=_grid.WorldToCell(new(Math.Max(a.X,b.X),Math.Max(a.Y,b.Y)));Vector2I minChunk=FloorRegistryV3.ChunkOf(minCell),maxChunk=FloorRegistryV3.ChunkOf(maxCell);HashSet<string> rooms=new(StringComparer.Ordinal);for(int y=minChunk.Y-1;y<=maxChunk.Y+1;y++)for(int x=minChunk.X-1;x<=maxChunk.X+1;x++)foreach(var room in _session.Registry.EnumerateRoomsInChunk(new(x,y)))rooms.Add(room.RoomId);int drawn=0;float tile=_grid.TileSize;foreach(string id in rooms){if(!_session.Registry.TryGetRoomById(id,out var room)||room==null)continue;_session.Registry.TryGetMetadata(id,out var metadata);Color color=metadata?.Role switch{RoomRoleV3.Bedroom=>new(.35f,.62f,1f,.14f),RoomRoleV3.Storage=>new(.74f,.48f,.92f,.14f),RoomRoleV3.Mixed=>new(.94f,.62f,.28f,.15f),_=>new(.35f,.9f,.72f,.12f)};foreach(var cell in room.Cells){if(drawn++>=MaxRoomVisualCellsPerFrame)return;if(cell.Value.X<minCell.X-1||cell.Value.X>maxCell.X+1||cell.Value.Y<minCell.Y-1||cell.Value.Y>maxCell.Y+1)continue;Rect2 rect=new(new Vector2(cell.Value.X*tile,cell.Value.Y*tile),new(tile,tile));DrawRect(rect,color,true);}Rect2 bounds=new(new Vector2(room.Bounds.Position.X*tile,room.Bounds.Position.Y*tile),new Vector2(room.Bounds.Size.X*tile,room.Bounds.Size.Y*tile));DrawRect(bounds,new Color(color.R,color.G,color.B,.55f),false,1.5f);string label=metadata==null?"Room":metadata.Role.ToString();DrawString(ThemeDB.FallbackFont,new Vector2(bounds.Position.X+3,bounds.Position.Y+11),$"{label} {room.CellCount}",HorizontalAlignment.Left,-1,10,new Color(.92f,.98f,1f,.9f));}}
}
