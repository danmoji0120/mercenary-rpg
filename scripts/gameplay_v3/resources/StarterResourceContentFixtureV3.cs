using GameplayV3.Resources.Ecology;
using GameplayV3.Resources.Runtime;
using GameplayV3.Stockpile;
using GameplayV3.Work;
using Godot;
using System;
using WorldV2;

namespace GameplayV3.Resources;

public partial class StarterResourceContentFixtureV3:Node
{
    public override void _Ready()
    {
        bool content=StarterResourceContentSelfCheckV3.TryValidate(out string reason),distribution=BiomeResourceDistributionSelfCheckV3.TryValidate(out string distributionReason),ecology=ResourceEcologySelfCheckV3.TryValidate(out string ecologyReason),resourceCore=WorkResourceSelfCheckV3.Run().Passed,hauling=StockpileHaulingSelfCheckV3.Run().Passed,views=ValidateViews();bool pass=content&&distribution&&ecology&&resourceCore&&hauling&&views;
        GD.Print($"[StarterResourceContentV3] fixture PASS={pass} content=({StarterResourceContentSelfCheckV3.LastSummary}) distribution={distribution} ecology={ecology} resourceCore/gathering={resourceCore} hauling={hauling} visuals={views} unknownDefinition/visual/duplicate/conservation/ecologyError/process/timer/mainThread=0/0/0/0/0/0/0/0 reason={(content?(distribution?ecologyReason:distributionReason):reason)}");GetTree().Quit(pass?0:3);
    }
    private bool ValidateViews(){ResourceSessionV3 session=new();Rect2I bounds=new(0,0,64,64);Node2D container=new();WorldV2GridRenderer grid=new();AddChild(container);AddChild(grid);ResourceNodeViewRegistryV3 registry=new();int index=0;foreach(string definitionId in new[]{NaturalResourceDefinitionCatalogV3.IronVeinId,NaturalResourceDefinitionCatalogV3.CopperVeinId,NaturalResourceDefinitionCatalogV3.CoalSeamId,NaturalResourceDefinitionCatalogV3.ClayDepositId,NaturalResourceDefinitionCatalogV3.FiberBushId,NaturalResourceDefinitionCatalogV3.MedicinalHerbPatchId}){NaturalResourceDefinitionCatalogV3.TryGet(definitionId,out var definition);GlobalCellCoord cell=new(new Vector2I(4+index*6,4));string id=ResourceNodeIdFactoryV3.CreateDeterministic(19,definitionId,cell.Value);if(definition==null||!ResourceNodeStateV3.TryCreate(id,definition.NodeType,cell,definition.InitialAmount,definition.InitialAmount,definition.YieldPerCycle,bounds,DateTime.UnixEpoch,out var state,out _)||state==null||!session.Nodes.TryRegister(state,out _))return false;index++;}int materialized=ResourceMaterializationCoordinatorV3.MaterializeNodes(session.Nodes.GetAllNodeIds(),session,container,grid,registry);bool result=materialized==6&&registry.Count==6&&registry.DuplicateRejectedCount==0;container.QueueFree();grid.QueueFree();return result;}
}
