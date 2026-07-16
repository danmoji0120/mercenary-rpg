using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WorldV2;

namespace GameplayV3.Resources.Runtime;

public partial class ResourceNodeEntityV3 : Node2D
{
    private Label? _label;private ResourceNodeTypeV3 _type;private int _remaining;private int _max;private bool _reserved;
    public string ResourceNodeId{get;private set;}=string.Empty;public bool IsInitialized{get;private set;}
    public bool TryInitialize(string id,ResourceNodeRegistryV3 registry,WorldV2GridRenderer grid,out string reason){if(!registry.TryGet(id,out ResourceNodeStateV3? state)||state==null){reason="Resource node state is missing.";return false;}ResourceNodeId=id;_type=state.NodeType;Position=grid.CellToWorldCenter(state.Cell.Value);EnsureLabel();Refresh(state);IsInitialized=true;QueueRedraw();reason=string.Empty;return true;}
    public void Refresh(ResourceNodeStateV3 state){_remaining=state.RemainingAmount;_max=state.MaxAmount;if(_label!=null)_label.Text=$"{_remaining}";QueueRedraw();}
    public void SetReserved(bool reserved){if(_reserved==reserved)return;_reserved=reserved;QueueRedraw();}
    public override void _Draw(){if(!IsInitialized)return;Color outline=new(0.04f,0.07f,0.06f,0.95f);if(_type==ResourceNodeTypeV3.Tree){DrawRect(new Rect2(-3,2,6,9),new Color(0.35f,0.20f,0.10f));DrawCircle(new Vector2(0,-3),8,new Color(0.16f,0.58f,0.25f));DrawCircle(new Vector2(-5,-1),5,new Color(0.20f,0.68f,0.30f));}else{Vector2[] points=new[]{new Vector2(-9,6),new Vector2(-5,-6),new Vector2(3,-9),new Vector2(9,3),new Vector2(5,8)};DrawColoredPolygon(points,new Color(0.55f,0.58f,0.62f));DrawPolyline(new[]{new Vector2(-9,6),new Vector2(-5,-6),new Vector2(3,-9),new Vector2(9,3),new Vector2(5,8),new Vector2(-9,6)},outline,1.5f);}float ratio=_max<=0?0:(float)_remaining/_max;DrawRect(new Rect2(-9,10,18,2),new Color(0.08f,0.08f,0.08f));DrawRect(new Rect2(-9,10,18*ratio,2),_type==ResourceNodeTypeV3.Tree?new Color(0.3f,0.9f,0.35f):new Color(0.75f,0.8f,0.9f));if(_reserved)DrawArc(Vector2.Zero,13,0,Mathf.Tau,28,new Color(1f,0.78f,0.2f),1.5f);}
    private void EnsureLabel(){_label??=GetNodeOrNull<Label>("Amount");if(_label==null){_label=new Label{Name="Amount",Position=new Vector2(-12,12),Size=new Vector2(24,14),HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=Godot.Control.MouseFilterEnum.Ignore};_label.AddThemeFontSizeOverride("font_size",9);AddChild(_label);}}
}

public partial class GroundResourceStackEntityV3 : Node2D
{
    private Label? _label;private ResourceTypeV3 _type;private bool _reserved;private bool _stored;
    public string ResourceStackId{get;private set;}=string.Empty;public bool IsInitialized{get;private set;}
    public bool TryInitialize(string id,GroundResourceStackRegistryV3 registry,WorldV2GridRenderer grid,out string reason){if(!registry.TryGet(id,out GroundResourceStackV3? stack)||stack==null){reason="Ground stack state is missing.";return false;}ResourceStackId=id;_type=stack.ResourceType;Position=grid.CellToWorldCenter(stack.Cell.Value)+new Vector2(7,7);EnsureLabel();Refresh(stack);IsInitialized=true;QueueRedraw();reason=string.Empty;return true;}
    public void Refresh(GroundResourceStackV3 stack){if(_label!=null)_label.Text=stack.Amount.ToString();QueueRedraw();}
    public void SetReserved(bool reserved){if(_reserved==reserved)return;_reserved=reserved;QueueRedraw();}public void SetStored(bool stored){if(_stored==stored)return;_stored=stored;QueueRedraw();}
    public override void _Draw(){if(!IsInitialized)return;Color color=_type switch{ResourceTypeV3.Wood=>new Color(0.62f,0.34f,0.14f),ResourceTypeV3.Stone=>new Color(0.65f,0.68f,0.72f),ResourceTypeV3.Potato=>new Color(0.68f,0.48f,0.24f),_=>new Color(0.82f,0.66f,0.30f)};if(_type==ResourceTypeV3.Ration){DrawRect(new Rect2(-5,-4,10,8),color,true);DrawRect(new Rect2(-5,-4,10,8),new Color(.24f,.16f,.08f,.95f),false,1.2f);DrawLine(new(-3,-1),new(3,-1),color.Lightened(.24f),1.2f);}else if(_type==ResourceTypeV3.Potato){DrawEllipse(new(-2,-1),new(4.5f,3.2f),color);DrawEllipse(new(3,2),new(3.6f,2.6f),color.Darkened(.08f));DrawEllipse(new(-4,3),new(3.2f,2.4f),color.Lightened(.08f));}else{DrawCircle(Vector2.Zero,5,color);if(_type==ResourceTypeV3.Wood)DrawLine(new(-4,-3),new(4,3),color.Lightened(0.25f),2);else DrawRect(new Rect2(-3,-3,6,6),color.Lightened(0.15f));}if(_stored)DrawArc(Vector2.Zero,7,0,Mathf.Tau,20,new Color(0.25f,0.92f,0.95f,0.8f),1);if(_reserved)DrawArc(Vector2.Zero,9,0,Mathf.Tau,24,new Color(1f,0.78f,0.2f),1.3f);}
    private void DrawEllipse(Vector2 center,Vector2 radius,Color color){Vector2[] points=new Vector2[16];for(int i=0;i<points.Length;i++){float a=Mathf.Tau*i/points.Length;points[i]=center+new Vector2(Mathf.Cos(a)*radius.X,Mathf.Sin(a)*radius.Y);}DrawColoredPolygon(points,color);DrawPolyline(points.Concat(new[]{points[0]}).ToArray(),new Color(.22f,.13f,.06f,.9f),.8f);}
    private void EnsureLabel(){_label??=GetNodeOrNull<Label>("Amount");if(_label==null){_label=new Label{Name="Amount",Position=new Vector2(-8,4),Size=new Vector2(16,12),HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=Godot.Control.MouseFilterEnum.Ignore};_label.AddThemeFontSizeOverride("font_size",8);AddChild(_label);}}
}

public sealed class ResourceNodeViewRegistryV3
{
    private readonly Dictionary<string,ResourceNodeEntityV3> _views=new(StringComparer.Ordinal);public int Count=>_views.Count;public int DuplicateRejectedCount{get;private set;}
    public bool TryRegister(string id,ResourceNodeEntityV3 view,out string reason){if(!GodotObject.IsInstanceValid(view)||!view.IsInitialized||view.ResourceNodeId!=id){reason="Invalid resource node view.";return false;}if(_views.TryGetValue(id,out ResourceNodeEntityV3? existing)){if(ReferenceEquals(existing,view)){reason=string.Empty;return true;}DuplicateRejectedCount++;reason="Duplicate resource node view.";return false;}_views.Add(id,view);reason=string.Empty;return true;}
    public bool TryGet(string id,out ResourceNodeEntityV3? view){if(!_views.TryGetValue(id,out view))return false;if(GodotObject.IsInstanceValid(view))return true;_views.Remove(id);view=null;return false;}
    public bool TryRemove(string id,bool queueFree=true){if(!_views.Remove(id,out ResourceNodeEntityV3? view))return false;if(queueFree&&GodotObject.IsInstanceValid(view))view.QueueFree();return true;}
    public IReadOnlyList<string> GetIds(){ClearInvalid();List<string> ids=new(_views.Keys);ids.Sort(StringComparer.Ordinal);return ids.AsReadOnly();}
    public void ClearInvalid(){List<string> bad=new();foreach(var pair in _views)if(!GodotObject.IsInstanceValid(pair.Value))bad.Add(pair.Key);foreach(string id in bad)_views.Remove(id);}
    public void Clear(){_views.Clear();}
}

public sealed class GroundResourceStackViewRegistryV3
{
    private readonly Dictionary<string,GroundResourceStackEntityV3> _views=new(StringComparer.Ordinal);public int Count=>_views.Count;public int DuplicateRejectedCount{get;private set;}
    public bool TryRegister(string id,GroundResourceStackEntityV3 view,out string reason){if(!GodotObject.IsInstanceValid(view)||!view.IsInitialized||view.ResourceStackId!=id){reason="Invalid ground stack view.";return false;}if(_views.ContainsKey(id)){DuplicateRejectedCount++;reason="Duplicate ground stack view.";return false;}_views.Add(id,view);reason=string.Empty;return true;}
    public bool TryGet(string id,out GroundResourceStackEntityV3? view){if(!_views.TryGetValue(id,out view))return false;if(GodotObject.IsInstanceValid(view))return true;_views.Remove(id);view=null;return false;}
    public bool TryRemove(string id,bool queueFree=true){if(!_views.Remove(id,out GroundResourceStackEntityV3? view))return false;if(queueFree&&GodotObject.IsInstanceValid(view))view.QueueFree();return true;}
    public IReadOnlyList<string> GetIds(){List<string> ids=new(_views.Keys);ids.Sort(StringComparer.Ordinal);return ids.AsReadOnly();}
    public void Clear(){_views.Clear();}
}

public static class ResourceMaterializationCoordinatorV3
{
    public static int MaterializeNodes(ResourceSessionV3 session,Node2D container,WorldV2GridRenderer grid,ResourceNodeViewRegistryV3 views)
    {int created=0;foreach(string id in session.Nodes.GetAllNodeIds()){if(!session.Nodes.TryGet(id,out ResourceNodeStateV3? state)||state==null||state.IsDepleted||views.TryGet(id,out _))continue;ResourceNodeEntityV3 view=new(){Name=$"Resource_{id}"};container.AddChild(view);if(view.TryInitialize(id,session.Nodes,grid,out _)&&views.TryRegister(id,view,out _))created++;else view.QueueFree();}return created;}
    public static void RefreshNode(string id,ResourceSessionV3 session,ResourceNodeViewRegistryV3 views){if(session.Nodes.TryGet(id,out ResourceNodeStateV3? state)&&state!=null&&views.TryGet(id,out ResourceNodeEntityV3? view)&&view!=null)view.Refresh(state);}
    public static GroundResourceStackEntityV3? MaterializeOrRefreshStack(GroundResourceStackV3 stack,ResourceSessionV3 session,Node2D container,WorldV2GridRenderer grid,GroundResourceStackViewRegistryV3 views)
    {if(views.TryGet(stack.ResourceStackId,out GroundResourceStackEntityV3? existing)&&existing!=null){existing.Refresh(stack);return existing;}GroundResourceStackEntityV3 view=new(){Name=$"Stack_{stack.ResourceStackId}"};container.AddChild(view);if(view.TryInitialize(stack.ResourceStackId,session.GroundStacks,grid,out _)&&views.TryRegister(stack.ResourceStackId,view,out _))return view;view.QueueFree();return null;}
}
