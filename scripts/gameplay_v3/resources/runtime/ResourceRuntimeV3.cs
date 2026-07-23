using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WorldV2;

namespace GameplayV3.Resources.Runtime;

public partial class ResourceNodeEntityV3 : Node2D
{
    private Label? _label;private ResourceNodeTypeV3 _type;private int _remaining;private int _max;private bool _reserved;
    public string ResourceNodeId{get;private set;}=string.Empty;public ResourceNodeTypeV3 NodeType=>_type;public bool IsInitialized{get;private set;}
    public bool TryInitialize(string id,ResourceNodeRegistryV3 registry,WorldV2GridRenderer grid,out string reason){if(!registry.TryGet(id,out ResourceNodeStateV3? state)||state==null){reason="Resource node state is missing.";return false;}ResourceNodeId=id;_type=state.NodeType;Position=grid.CellToWorldCenter(state.Cell.Value);EnsureLabel();Refresh(state);IsInitialized=true;QueueRedraw();reason=string.Empty;return true;}
    public void Refresh(ResourceNodeStateV3 state){_remaining=state.RemainingAmount;_max=state.MaxAmount;if(_label!=null)_label.Text=$"{_remaining}";QueueRedraw();}
    public void SetReserved(bool reserved){if(_reserved==reserved)return;_reserved=reserved;QueueRedraw();}
    public override void _Draw(){if(!IsInitialized)return;switch(_type){case ResourceNodeTypeV3.Tree:DrawTree();break;case ResourceNodeTypeV3.StoneOutcrop:DrawRock(new(.55f,.58f,.62f),new(.75f,.80f,.90f));break;case ResourceNodeTypeV3.IronVein:DrawRock(new(.34f,.36f,.38f),new(.60f,.27f,.16f));break;case ResourceNodeTypeV3.CopperVein:DrawRock(new(.42f,.43f,.42f),new(.16f,.66f,.58f));break;case ResourceNodeTypeV3.CoalSeam:DrawRock(new(.11f,.12f,.14f),new(.28f,.30f,.33f));break;case ResourceNodeTypeV3.ClayDeposit:DrawDeposit(new(.61f,.29f,.18f));break;case ResourceNodeTypeV3.FiberBush:DrawBush(new(.63f,.78f,.20f),new(.83f,.86f,.42f));break;case ResourceNodeTypeV3.MedicinalHerbPatch:DrawBush(new(.12f,.48f,.22f),new(.62f,.31f,.72f));break;}float ratio=_max<=0?0:(float)_remaining/_max;DrawRect(new Rect2(-9,10,18,2),new Color(0.08f,0.08f,0.08f));DrawRect(new Rect2(-9,10,18*ratio,2),_type is ResourceNodeTypeV3.Tree or ResourceNodeTypeV3.FiberBush or ResourceNodeTypeV3.MedicinalHerbPatch?new Color(0.3f,0.9f,0.35f):new Color(0.75f,0.8f,0.9f));if(_reserved)DrawArc(Vector2.Zero,13,0,Mathf.Tau,28,new Color(1f,0.78f,0.2f),1.5f);}
    private void DrawTree(){DrawRect(new Rect2(-3,2,6,9),new Color(0.35f,0.20f,0.10f));DrawCircle(new Vector2(0,-3),8,new Color(0.16f,0.58f,0.25f));DrawCircle(new Vector2(-5,-1),5,new Color(0.20f,0.68f,0.30f));}
    private void DrawRock(Color body,Color accent){Vector2[] points={new(-9,6),new(-5,-6),new(3,-9),new(9,3),new(5,8)};DrawColoredPolygon(points,body);DrawPolyline(new[]{points[0],points[1],points[2],points[3],points[4],points[0]},new Color(.04f,.07f,.06f,.95f),1.5f);DrawLine(new(-4,2),new(5,-3),accent,2);DrawCircle(new(1,3),1.5f,accent);}
    private void DrawDeposit(Color color){Vector2[] points={new(-10,6),new(-7,0),new(-2,-4),new(6,-3),new(10,5),new(5,8),new(-6,8)};DrawColoredPolygon(points,color);DrawPolyline(new[]{points[0],points[1],points[2],points[3],points[4],points[5],points[6],points[0]},color.Darkened(.45f),1.4f);DrawCircle(new(-3,2),2,color.Lightened(.18f));}
    private void DrawBush(Color leaf,Color accent){DrawLine(new(0,7),new(0,-4),leaf.Darkened(.35f),2);DrawCircle(new(-5,1),4.5f,leaf);DrawCircle(new(4,0),5,leaf.Darkened(.08f));DrawCircle(new(0,-5),4,leaf.Lightened(.08f));DrawCircle(new(3,-4),1.5f,accent);DrawCircle(new(-4,-1),1.3f,accent);}
    private void EnsureLabel(){_label??=GetNodeOrNull<Label>("Amount");if(_label==null){_label=new Label{Name="Amount",Position=new Vector2(-12,12),Size=new Vector2(24,14),HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=Godot.Control.MouseFilterEnum.Ignore};_label.AddThemeFontSizeOverride("font_size",9);AddChild(_label);}}
}

public partial class GroundResourceStackEntityV3 : Node2D
{
    private Label? _label;private ResourceTypeV3 _type;private bool _reserved;private bool _stored;
    public string ResourceStackId{get;private set;}=string.Empty;public bool IsInitialized{get;private set;}
    public bool TryInitialize(string id,GroundResourceStackRegistryV3 registry,WorldV2GridRenderer grid,out string reason){if(!registry.TryGet(id,out GroundResourceStackV3? stack)||stack==null){reason="Ground stack state is missing.";return false;}ResourceStackId=id;_type=stack.ResourceType;Position=grid.CellToWorldCenter(stack.Cell.Value)+new Vector2(7,7);EnsureLabel();Refresh(stack);IsInitialized=true;QueueRedraw();reason=string.Empty;return true;}
    public void Refresh(GroundResourceStackV3 stack){if(_label!=null)_label.Text=stack.Amount.ToString();QueueRedraw();}
    public void SetReserved(bool reserved){if(_reserved==reserved)return;_reserved=reserved;QueueRedraw();}public void SetStored(bool stored){if(_stored==stored)return;_stored=stored;QueueRedraw();}
    public override void _Draw(){if(!IsInitialized)return;Color color=_type switch{ResourceTypeV3.Wood=>new Color(.62f,.34f,.14f),ResourceTypeV3.Stone=>new Color(.65f,.68f,.72f),ResourceTypeV3.Potato=>new Color(.68f,.48f,.24f),ResourceTypeV3.IronOre=>new Color(.48f,.31f,.26f),ResourceTypeV3.CopperOre=>new Color(.19f,.62f,.55f),ResourceTypeV3.Coal=>new Color(.13f,.14f,.16f),ResourceTypeV3.Clay=>new Color(.64f,.31f,.19f),ResourceTypeV3.Fiber=>new Color(.72f,.80f,.31f),ResourceTypeV3.MedicinalHerb=>new Color(.27f,.63f,.31f),_=>new Color(.82f,.66f,.30f)};if(_type==ResourceTypeV3.Ration){DrawRect(new Rect2(-5,-4,10,8),color,true);DrawRect(new Rect2(-5,-4,10,8),new Color(.24f,.16f,.08f,.95f),false,1.2f);DrawLine(new(-3,-1),new(3,-1),color.Lightened(.24f),1.2f);}else if(_type==ResourceTypeV3.Potato){DrawEllipse(new(-2,-1),new(4.5f,3.2f),color);DrawEllipse(new(3,2),new(3.6f,2.6f),color.Darkened(.08f));DrawEllipse(new(-4,3),new(3.2f,2.4f),color.Lightened(.08f));}else if(_type is ResourceTypeV3.Fiber or ResourceTypeV3.MedicinalHerb){DrawLine(new(0,4),new(0,-4),color.Darkened(.3f),1.5f);DrawCircle(new(-3,-1),3,color);DrawCircle(new(3,-2),3,color.Lightened(.1f));}else{DrawCircle(Vector2.Zero,5,color);if(_type==ResourceTypeV3.Wood)DrawLine(new(-4,-3),new(4,3),color.Lightened(.25f),2);else DrawRect(new Rect2(-3,-3,6,6),color.Lightened(.15f));}if(_stored)DrawArc(Vector2.Zero,7,0,Mathf.Tau,20,new Color(.25f,.92f,.95f,.8f),1);if(_reserved)DrawArc(Vector2.Zero,9,0,Mathf.Tau,24,new Color(1f,.78f,.2f),1.3f);}
    private void DrawEllipse(Vector2 center,Vector2 radius,Color color){Vector2[] points=new Vector2[16];for(int i=0;i<points.Length;i++){float a=Mathf.Tau*i/points.Length;points[i]=center+new Vector2(Mathf.Cos(a)*radius.X,Mathf.Sin(a)*radius.Y);}DrawColoredPolygon(points,color);DrawPolyline(points.Concat(new[]{points[0]}).ToArray(),new Color(.22f,.13f,.06f,.9f),.8f);}
    private void EnsureLabel(){_label??=GetNodeOrNull<Label>("Amount");if(_label==null){_label=new Label{Name="Amount",Position=new Vector2(-8,4),Size=new Vector2(16,12),HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=Godot.Control.MouseFilterEnum.Ignore};_label.AddThemeFontSizeOverride("font_size",8);AddChild(_label);}}
}

public sealed class ResourceNodeViewRegistryV3
{
    private readonly Dictionary<string,ResourceNodeEntityV3> _views=new(StringComparer.Ordinal);public int Count=>_views.Count;public int DuplicateRejectedCount{get;private set;}public long CreatedTotal{get;private set;}
    public bool TryRegister(string id,ResourceNodeEntityV3 view,out string reason){if(!GodotObject.IsInstanceValid(view)||!view.IsInitialized||view.ResourceNodeId!=id){reason="Invalid resource node view.";return false;}if(_views.TryGetValue(id,out ResourceNodeEntityV3? existing)){if(ReferenceEquals(existing,view)){reason=string.Empty;return true;}DuplicateRejectedCount++;reason="Duplicate resource node view.";return false;}_views.Add(id,view);CreatedTotal++;reason=string.Empty;return true;}
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
    public static int MaterializeNodes(IReadOnlyList<string> ids,ResourceSessionV3 session,Node2D container,WorldV2GridRenderer grid,ResourceNodeViewRegistryV3 views)
    {int created=0;foreach(string id in ids){if(!session.Nodes.TryGet(id,out ResourceNodeStateV3? state)||state==null||state.IsDepleted||views.TryGet(id,out _))continue;ResourceNodeEntityV3 view=new(){Name=$"Resource_{id}"};container.AddChild(view);if(view.TryInitialize(id,session.Nodes,grid,out _)&&views.TryRegister(id,view,out _))created++;else view.QueueFree();}return created;}
    public static void RefreshNode(string id,ResourceSessionV3 session,ResourceNodeViewRegistryV3 views){if(session.Nodes.TryGet(id,out ResourceNodeStateV3? state)&&state!=null&&views.TryGet(id,out ResourceNodeEntityV3? view)&&view!=null)view.Refresh(state);}
    public static GroundResourceStackEntityV3? MaterializeOrRefreshStack(GroundResourceStackV3 stack,ResourceSessionV3 session,Node2D container,WorldV2GridRenderer grid,GroundResourceStackViewRegistryV3 views)
    {if(views.TryGet(stack.ResourceStackId,out GroundResourceStackEntityV3? existing)&&existing!=null){existing.Refresh(stack);return existing;}GroundResourceStackEntityV3 view=new(){Name=$"Stack_{stack.ResourceStackId}"};container.AddChild(view);if(view.TryInitialize(stack.ResourceStackId,session.GroundStacks,grid,out _)&&views.TryRegister(stack.ResourceStackId,view,out _))return view;view.QueueFree();return null;}
}
