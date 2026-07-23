using System;
using System.Collections.Generic;
using GameplayV3.Resources.Ecology;
using GameplayV3.Stockpile;
using GameplayV3.Work;
using Godot;
using WorldV2;

namespace GameplayV3.Resources;

public static class StarterResourceContentSelfCheckV3
{
    private static readonly string[] DefinitionIds={NaturalResourceDefinitionCatalogV3.IronVeinId,NaturalResourceDefinitionCatalogV3.CopperVeinId,NaturalResourceDefinitionCatalogV3.CoalSeamId,NaturalResourceDefinitionCatalogV3.ClayDepositId,NaturalResourceDefinitionCatalogV3.FiberBushId,NaturalResourceDefinitionCatalogV3.MedicinalHerbPatchId};
    public static string LastSummary{get;private set;}=string.Empty;

    public static bool TryValidate(out string reason)
    {
        List<string> failures=new();HashSet<ResourceNodeTypeV3> nodeTypes=new();HashSet<ResourceTypeV3> outputs=new();
        int[] expectedAmounts={12,10,14,16,10,8},expectedYields={3,3,4,4,3,2};
        for(int i=0;i<DefinitionIds.Length;i++)
        {
            Require(NaturalResourceDefinitionCatalogV3.TryGet(DefinitionIds[i],out var definition)&&definition!=null,$"missing definition {DefinitionIds[i]}",failures);if(definition==null)continue;
            Require(nodeTypes.Add(definition.NodeType)&&outputs.Add(definition.OutputResourceType),$"duplicate type {DefinitionIds[i]}",failures);Require(!string.IsNullOrWhiteSpace(definition.NodeDisplayName)&&!string.IsNullOrWhiteSpace(definition.ResourceDisplayName),$"missing display name {DefinitionIds[i]}",failures);Require(definition.InitialAmount==expectedAmounts[i]&&definition.YieldPerCycle==expectedYields[i],$"amount/yield {DefinitionIds[i]}",failures);Require(definition.RenewalClass==NaturalResourceRenewalClassV3.None,$"renewal {DefinitionIds[i]}",failures);Require(!ResourceRenewalCatalogV3.TryGet(BiomeKindV3.Plains,DefinitionIds[i],out _),$"ecology rule {DefinitionIds[i]}",failures);
        }
        Require(NaturalResourceDefinitionCatalogV3.TryGet(NaturalResourceDefinitionCatalogV3.TreeId,out var tree)&&tree?.InitialAmount==15&&tree.YieldPerCycle==5&&tree.OutputResourceType==ResourceTypeV3.Wood,"legacy tree definition",failures);
        Require(NaturalResourceDefinitionCatalogV3.TryGet(NaturalResourceDefinitionCatalogV3.StoneId,out var stone)&&stone?.InitialAmount==20&&stone.YieldPerCycle==5&&stone.OutputResourceType==ResourceTypeV3.Stone,"legacy stone definition",failures);
        ValidateGatheringAndStorage(failures);ValidateEcologyBoundary(failures);ValidateDistribution(failures,out string distribution);
        LastSummary=$"definitions=6 gathering=6 stockpile=6 distribution=({distribution}) ecologyNonRenewable=6";reason=failures.Count==0?string.Empty:string.Join(" | ",failures);return failures.Count==0;
    }

    private static void ValidateGatheringAndStorage(List<string> failures)
    {
        Rect2I bounds=new(0,0,128,128);ResourceSessionV3 resources=new();ResourceReservationRegistryV3 reservations=new();StockpileSessionV3 stockpiles=new();stockpiles.Zones.TryCreateZone("company",new[]{new GlobalCellCoord(new Vector2I(90,90)),new GlobalCellCoord(new Vector2I(91,90)),new GlobalCellCoord(new Vector2I(92,90)),new GlobalCellCoord(new Vector2I(93,90))},bounds,out var zone,out _);
        for(int i=0;i<DefinitionIds.Length;i++)
        {
            NaturalResourceDefinitionCatalogV3.TryGet(DefinitionIds[i],out var definition);if(definition==null)continue;GlobalCellCoord cell=new(new Vector2I(8+i*4,8));string id=ResourceNodeIdFactoryV3.CreateDeterministic(411,definition.DefinitionId,cell.Value);Require(ResourceNodeStateV3.TryCreate(id,definition.NodeType,cell,definition.InitialAmount,definition.InitialAmount,definition.YieldPerCycle,bounds,DateTime.UnixEpoch,out var node,out _)&&node!=null&&resources.Nodes.TryRegister(node,out _),$"register {definition.DefinitionId}",failures);if(node==null)continue;
            string work=$"work_{i}";ResourceReservationV3 reservation=new(id,work,"merc","company",1,DateTime.UnixEpoch);Require(reservations.TryReserve(reservation,out _),$"reserve {definition.DefinitionId}",failures);int gathered=0,cycles=0;while(!node.IsDepleted&&cycles++<16){Require(node.TryHarvest(out int amount,out _),$"harvest {definition.DefinitionId}",failures);if(cycles==1)Require(amount==definition.YieldPerCycle,$"yield {definition.DefinitionId}",failures);gathered+=amount;resources.GroundStacks.TryAddOrMerge(definition.OutputResourceType,amount,cell,out _,out _,out _);resources.GenerationLedger.TryRecord(definition.OutputResourceType,amount,"Gathering","merc",work+"_"+cycles,id,cell);resources.Nodes.NotifyChanged(id);}Require(gathered==definition.InitialAmount&&node.IsDepleted,$"depletion {definition.DefinitionId}",failures);Require(reservations.TryRelease(id,work)&&!reservations.IsReserved(id),$"release {definition.DefinitionId}",failures);Require(resources.GroundStacks.GetTotalAmount(definition.OutputResourceType)==definition.InitialAmount,$"stack conservation {definition.DefinitionId}",failures);Require(resources.GenerationLedger.GetGeneratedAmount(definition.OutputResourceType)==definition.InitialAmount,$"ledger {definition.DefinitionId}",failures);Require(zone?.Allows(definition.OutputResourceType)==true,$"stockpile allows {definition.DefinitionId}",failures);
        }
        GlobalCellCoord mergeCell=new(new Vector2I(70,70));resources.GroundStacks.TryAddStack(ResourceTypeV3.IronOre,3,mergeCell,out var iron,out _,out _);resources.GroundStacks.TryAddStack(ResourceTypeV3.IronOre,2,mergeCell,out var mergedIron,out bool merged,out _);resources.GroundStacks.TryAddStack(ResourceTypeV3.CopperOre,2,mergeCell,out var copper,out bool copperMerged,out _);Require(merged&&mergedIron?.ResourceStackId==iron?.ResourceStackId&&mergedIron?.Amount==5&&!copperMerged&&copper?.ResourceStackId!=iron?.ResourceStackId,"stack merge isolation",failures);
        MercenaryCarryRegistryV3 carries=new();Require(resources.GroundStacks.TryTakeAmount(iron!.ResourceStackId,2,out int taken,out _,out _,out _)&&taken==2,"partial take",failures);Require(carries.TryBeginCarry("merc","haul",ResourceTypeV3.IronOre,taken,iron.ResourceStackId,1,2,out _,out _),"partial carry",failures);Require(carries.TryDropCarryAtCell("merc","haul",new(new Vector2I(90,90)),resources.GroundStacks,out var stored,out _)&&stored?.ResourceType==ResourceTypeV3.IronOre&&stored.Amount==2,"partial haul/drop",failures);
    }

    private static void ValidateDistribution(List<string> failures,out string summary)
    {
        Dictionary<string,int> plains=Count(BiomeKindV3.Plains,TileType.Grass,20),forest=Count(BiomeKindV3.ForestLand,TileType.ForestGround,20),rocky=Count(BiomeKindV3.RockyHills,TileType.QuarryGround,20);
        Require(rocky.GetValueOrDefault(NaturalResourceDefinitionCatalogV3.IronVeinId)>plains.GetValueOrDefault(NaturalResourceDefinitionCatalogV3.IronVeinId),"rocky iron direction",failures);Require(rocky.GetValueOrDefault(NaturalResourceDefinitionCatalogV3.CopperVeinId)>plains.GetValueOrDefault(NaturalResourceDefinitionCatalogV3.CopperVeinId),"rocky copper direction",failures);Require(rocky.GetValueOrDefault(NaturalResourceDefinitionCatalogV3.CoalSeamId)>plains.GetValueOrDefault(NaturalResourceDefinitionCatalogV3.CoalSeamId),"rocky coal direction",failures);Require(forest.GetValueOrDefault(NaturalResourceDefinitionCatalogV3.FiberBushId)>=plains.GetValueOrDefault(NaturalResourceDefinitionCatalogV3.FiberBushId),"forest fiber direction",failures);Require(forest.GetValueOrDefault(NaturalResourceDefinitionCatalogV3.MedicinalHerbPatchId)>=plains.GetValueOrDefault(NaturalResourceDefinitionCatalogV3.MedicinalHerbPatchId),"forest herb direction",failures);Require(plains.GetValueOrDefault(NaturalResourceDefinitionCatalogV3.ClayDepositId)>=forest.GetValueOrDefault(NaturalResourceDefinitionCatalogV3.ClayDepositId)&&plains.GetValueOrDefault(NaturalResourceDefinitionCatalogV3.ClayDepositId)>=rocky.GetValueOrDefault(NaturalResourceDefinitionCatalogV3.ClayDepositId),"plains clay direction",failures);foreach(string id in DefinitionIds)Require(plains.GetValueOrDefault(id)+forest.GetValueOrDefault(id)+rocky.GetValueOrDefault(id)>0,$"sample missing {id}",failures);
        string first=Signature(217),second=Signature(217),other=Signature(218);Require(first==second&&first!=other,"distribution determinism/seed variance",failures);summary=$"P={Format(plains)} F={Format(forest)} R={Format(rocky)}";
        static Dictionary<string,int> Count(BiomeKindV3 biome,TileType terrain,int chunks){Dictionary<string,int> counts=new(StringComparer.Ordinal);Rect2I bounds=new(0,0,8192,8192);for(int i=0;i<chunks;i++){Vector2I chunk=new(20+i,20+(int)biome*2);var values=ResourcePlacementEvaluatorV3.GenerateChunk(217,"V3",chunk,bounds,c=>new(){GlobalCellCoord=c,BiomeKind=biome,TileType=terrain,ForestStrength=biome==BiomeKindV3.ForestLand?.75f:0,IsWalkable=true},out _);foreach(var value in values)counts[value.ResourceDefinitionId]=counts.GetValueOrDefault(value.ResourceDefinitionId)+1;}return counts;}
        static string Signature(int seed){Rect2I bounds=new(0,0,8192,8192);var values=ResourcePlacementEvaluatorV3.GenerateChunk(seed,"V3",new(31,31),bounds,c=>new(){GlobalCellCoord=c,BiomeKind=BiomeKindV3.RockyHills,TileType=TileType.QuarryGround,IsWalkable=true},out _);return string.Join(';',values);}
        static string Format(Dictionary<string,int> values)=>$"I{values.GetValueOrDefault(NaturalResourceDefinitionCatalogV3.IronVeinId)}/C{values.GetValueOrDefault(NaturalResourceDefinitionCatalogV3.CopperVeinId)}/K{values.GetValueOrDefault(NaturalResourceDefinitionCatalogV3.CoalSeamId)}/L{values.GetValueOrDefault(NaturalResourceDefinitionCatalogV3.ClayDepositId)}/F{values.GetValueOrDefault(NaturalResourceDefinitionCatalogV3.FiberBushId)}/H{values.GetValueOrDefault(NaturalResourceDefinitionCatalogV3.MedicinalHerbPatchId)}";
    }

    private static void ValidateEcologyBoundary(List<string> failures)
    {
        Rect2I bounds=new(0,0,64,64);ResourceNodeRegistryV3 nodes=new();ResourceEcologySessionV3 ecology=new(nodes,71,bounds);ecology.RegisterCapacity(new(Vector2I.Zero,NaturalResourceDefinitionCatalogV3.StoneId,"biome.resource.plains",BiomeKindV3.Plains,1024,3,0));ecology.RegisterCapacity(new(Vector2I.Zero,NaturalResourceDefinitionCatalogV3.IronVeinId,"biome.resource.plains",BiomeKindV3.Plains,1024,3,0));ecology.SynchronizeChunk(Vector2I.Zero);ecology.TryGetState(new(Vector2I.Zero,NaturalResourceDefinitionCatalogV3.StoneId),out var stoneState);NaturalResourceDefinitionCatalogV3.TryGet(NaturalResourceDefinitionCatalogV3.IronVeinId,out var iron);string id=ResourceNodeIdFactoryV3.CreateDeterministic(71,NaturalResourceDefinitionCatalogV3.IronVeinId,new(3,3));ResourceNodeStateV3.TryCreate(id,iron!.NodeType,new(new Vector2I(3,3)),iron.InitialAmount,iron.InitialAmount,iron.YieldPerCycle,bounds,DateTime.UnixEpoch,out var node,out _);nodes.TryRegister(node,out _);while(node!=null&&!node.IsDepleted)node.TryHarvest(out _,out _);nodes.NotifyChanged(id);Require(ecology.StateCount==1&&stoneState?.CurrentNodeCount==0&&ecology.Diagnostics.UnsupportedResourceIgnoredCount==1,"nonrenewable ecology isolation",failures);ecology.Dispose();
    }

    private static void Require(bool condition,string failure,List<string> failures){if(!condition)failures.Add(failure);}
}
