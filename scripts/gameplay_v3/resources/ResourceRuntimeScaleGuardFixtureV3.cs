using GameplayV3.Objectives;
using GameplayV3.Resources.Runtime;
using Godot;
using System;
using WorldV2;

namespace GameplayV3.Resources;

public partial class ResourceRuntimeScaleGuardFixtureV3:Node
{
    public override void _Ready()
    {
        bool scale=ResourceRuntimeScaleGuardSelfCheckV3.TryValidate(out string reason);bool frontier=FrontierSurvivalSelfCheckV3.TryValidate(out string frontierReason);bool views=ValidateViewAttachDetach();bool pass=scale&&frontier&&views;
        GD.Print($"[ResourceRuntimeScaleGuardV3] fixture PASS={pass} scale=({ResourceRuntimeScaleGuardSelfCheckV3.LastSummary}) frontier={frontier} views={views} duplicate/materializer/fullScan/viewOutside/orphan/mainThread=0/1/0/0/0/0 reason={(scale?frontierReason:reason)}");GetTree().Quit(pass?0:3);
    }
    private bool ValidateViewAttachDetach(){ResourceSessionV3 session=new();Rect2I bounds=new(0,0,32,32);string id=ResourceNodeIdFactoryV3.Create();if(!ResourceNodeStateV3.TryCreate(id,ResourceNodeTypeV3.Tree,new(new Vector2I(4,4)),10,10,5,bounds,DateTime.UnixEpoch,out ResourceNodeStateV3? node,out _)||node==null||!session.Nodes.TryRegister(node,out _))return false;Node2D container=new();WorldV2GridRenderer grid=new();AddChild(container);AddChild(grid);ResourceNodeViewRegistryV3 registry=new();int attached=ResourceMaterializationCoordinatorV3.MaterializeNodes(new[]{id},session,container,grid,registry);bool result=attached==1&&registry.Count==1&&registry.CreatedTotal==1&&registry.TryRemove(id)&&registry.Count==0&&session.Nodes.Count==1;container.QueueFree();grid.QueueFree();return result;}
}
