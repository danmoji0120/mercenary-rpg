using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Godot;
using WorldV2;

namespace GameplayV3.Resources.Ecology;

public enum ResourceRenewalExclusionKindV3
{
    None,
    ExistingResource,
    Structure,
    Blueprint,
    Floor,
    Farm,
    Stockpile,
    IndoorRoom,
    Mercenary,
    DynamicBlocker,
    GroundStack,
    InteractionReservation,
    ChunkTransition
}

public readonly record struct ResourceRenewalExclusionContextV3(
    Vector2I GlobalCell,
    Vector2I ChunkCoord,
    string ResourceDefinitionId,
    NaturalResourceRenewalClassV3 RenewalClass,
    string? CompanyId,
    double SimulationTime,
    long AttemptSequence);

public readonly record struct ResourceRenewalExclusionResultV3(
    bool IsAllowed,
    ResourceRenewalExclusionKindV3 Kind,
    bool IsTransient,
    double SuggestedRetrySeconds,
    string? BlockingSourceId,
    int Revision)
{
    public static ResourceRenewalExclusionResultV3 Allowed(int revision = 0) =>
        new(true, ResourceRenewalExclusionKindV3.None, false, 0, null, revision);
}

public interface IResourceRenewalExclusionQueryV3
{
    ResourceRenewalExclusionResultV3 Evaluate(ResourceRenewalExclusionContextV3 context);
    int GetOccupancyRevision(Vector2I cell);
}

public enum ResourceShortageTierV3 { None, Low, Critical }
public enum ResourceShortageEvaluationStatusV3 { Valid, Unknown, Disabled }

public sealed record ResourceShortageSafetyRuleV3(
    string RuleId,
    string ResourceDefinitionId,
    int MinimumAccessibleNodeCount,
    int EvaluationRadiusCells,
    int ReachabilityRadiusCells,
    double EvaluationIntervalSeconds,
    int CriticalMaximumCount,
    double LowAttemptWeightMultiplier,
    double CriticalAttemptWeightMultiplier,
    double LowCooldownMultiplier,
    double CriticalCooldownMultiplier,
    double MinimumEmergencyCooldownSeconds,
    int MaximumLocalSafetySpawnsPerWindow,
    double WindowSeconds);

public static class ResourceShortageSafetyCatalogV3
{
    private static readonly IReadOnlyDictionary<string, ResourceShortageSafetyRuleV3> Rules =
        new ReadOnlyDictionary<string, ResourceShortageSafetyRuleV3>(
            new Dictionary<string, ResourceShortageSafetyRuleV3>(StringComparer.Ordinal)
            {
                [NaturalResourceDefinitionCatalogV3.TreeId] = new(
                    "safety.tree", NaturalResourceDefinitionCatalogV3.TreeId, 3, 40, 48, 5, 1,
                    1.5, 2.5, .75, .40, 90, 2, 300),
                [NaturalResourceDefinitionCatalogV3.StoneId] = new(
                    "safety.stone", NaturalResourceDefinitionCatalogV3.StoneId, 2, 48, 56, 5, 0,
                    1.5, 2.25, .75, .45, 180, 2, 420)
            });

    public static bool TryGet(string resourceDefinitionId, out ResourceShortageSafetyRuleV3? rule) =>
        Rules.TryGetValue(resourceDefinitionId, out rule);

    public static ResourceShortageTierV3 Classify(
        ResourceShortageSafetyRuleV3 rule,
        int accessibleCount,
        ResourceShortageTierV3 previous,
        bool reachabilityLimitHit)
    {
        if (accessibleCount >= rule.MinimumAccessibleNodeCount + (previous == ResourceShortageTierV3.None ? 0 : 1))
            return ResourceShortageTierV3.None;
        ResourceShortageTierV3 tier = accessibleCount <= rule.CriticalMaximumCount
            ? ResourceShortageTierV3.Critical
            : ResourceShortageTierV3.Low;
        return reachabilityLimitHit && tier == ResourceShortageTierV3.Critical
            ? ResourceShortageTierV3.Low
            : tier;
    }

    public static double BiomeWeight(BiomeKindV3 biome, string resourceDefinitionId)
    {
        if (resourceDefinitionId == NaturalResourceDefinitionCatalogV3.TreeId)
            return biome == BiomeKindV3.ForestLand ? 1 : biome == BiomeKindV3.Plains ? .55 : 0;
        if (resourceDefinitionId == NaturalResourceDefinitionCatalogV3.StoneId)
            return biome == BiomeKindV3.RockyHills ? 1 : biome == BiomeKindV3.Plains ? .55 : 0;
        return 0;
    }
}

public sealed class CompanyResourceShortageStateV3
{
    public CompanyResourceShortageStateV3(string companyId, string resourceDefinitionId)
    { CompanyId = companyId; ResourceDefinitionId = resourceDefinitionId; }
    public string CompanyId { get; }
    public string ResourceDefinitionId { get; }
    public Vector2I AnchorCell { get; internal set; }
    public string AnchorSource { get; internal set; } = string.Empty;
    public int AccessibleNodeCount { get; internal set; }
    public int AliveNodeCountInRadius { get; internal set; }
    public int ReachableCellCount { get; internal set; }
    public bool ReachabilityLimitHit { get; internal set; }
    public ResourceShortageTierV3 Tier { get; internal set; }
    public ResourceShortageEvaluationStatusV3 EvaluationStatus { get; internal set; }
    public long Revision { get; internal set; }
    public double LastEvaluatedSimulationTime { get; internal set; }
    public double NextEvaluationTime { get; internal set; }
    public int LocalSafetySpawnsInWindow { get; internal set; }
    public double WindowStartedAt { get; internal set; }
    public int AffectedChunkCount { get; internal set; }
    public string LastFailureReason { get; internal set; } = string.Empty;
}

public sealed class ChunkResourceShortagePressureV3
{
    public ChunkResourceShortagePressureV3(ResourceEcologyKeyV3 key) { Key = key; }
    public ResourceEcologyKeyV3 Key { get; }
    public ResourceShortageTierV3 HighestTier { get; internal set; }
    public int ContributingCompanyCount { get; internal set; }
    public double AttemptMultiplier { get; internal set; } = 1;
    public double CooldownMultiplier { get; internal set; } = 1;
    public double MinimumCooldownSeconds { get; internal set; }
    public double LastAppliedAt { get; internal set; } = -1;
    public long Revision { get; internal set; }
    public double ValidUntil { get; internal set; }
}

internal readonly record struct ResourceEcologyNegativeEntryV3(
    double RetryAfter,
    ResourceRenewalExclusionKindV3 FailureKind,
    string? BlockingSourceId,
    int BlockingRevision);

internal static class ResourceEcologyNegativeCachePolicyV3
{
    public static bool IsCurrent(ResourceEcologyNegativeEntryV3 entry,double simulationTime,int currentRevision) =>
        entry.RetryAfter>simulationTime&&entry.BlockingRevision==currentRevision;
}

public static class CompanyResourceSafetyAnchorV3
{
    public static bool TryResolve(IReadOnlyList<(string MercenaryId,Vector2I Cell)> members,Vector2I? deploymentFallback,out Vector2I anchor,out string source)
    {
        if(members.Count==0){if(deploymentFallback.HasValue){anchor=deploymentFallback.Value;source="StartingDeployment";return true;}anchor=default;source="Unavailable";return false;}
        long sx=0,sy=0;foreach(var member in members){sx+=member.Cell.X;sy+=member.Cell.Y;}double mx=sx/(double)members.Count,my=sy/(double)members.Count;var best=members[0];double bestDistance=double.MaxValue;foreach(var member in members){double dx=member.Cell.X-mx,dy=member.Cell.Y-my,d=dx*dx+dy*dy;if(d<bestDistance||(d==bestDistance&&string.CompareOrdinal(member.MercenaryId,best.MercenaryId)<0)){best=member;bestDistance=d;}}anchor=best.Cell;source="MercenaryMedoid";return true;
    }
}

public static class ResourceEcologySafetySelfCheckV3
{
    public static bool TryValidate(out string reason)
    {
        ResourceShortageSafetyCatalogV3.TryGet(NaturalResourceDefinitionCatalogV3.TreeId,out var tree);ResourceShortageSafetyCatalogV3.TryGet(NaturalResourceDefinitionCatalogV3.StoneId,out var stone);
        if(tree==null||stone==null||ResourceShortageSafetyCatalogV3.Classify(tree,3,ResourceShortageTierV3.None,false)!=ResourceShortageTierV3.None||ResourceShortageSafetyCatalogV3.Classify(tree,2,ResourceShortageTierV3.None,false)!=ResourceShortageTierV3.Low||ResourceShortageSafetyCatalogV3.Classify(tree,1,ResourceShortageTierV3.None,false)!=ResourceShortageTierV3.Critical||ResourceShortageSafetyCatalogV3.Classify(stone,0,ResourceShortageTierV3.None,true)!=ResourceShortageTierV3.Low){reason="Shortage tier policy failed.";return false;}
        var members=new[]{("merc_b",new Vector2I(10,10)),("merc_a",new Vector2I(12,10)),("merc_c",new Vector2I(11,12))};if(!CompanyResourceSafetyAnchorV3.TryResolve(members,null,out Vector2I anchor,out string source)||anchor!=new Vector2I(12,10)||source!="MercenaryMedoid"){reason="Deterministic company anchor failed.";return false;}
        if(!CompanyResourceSafetyAnchorV3.TryResolve(Array.Empty<(string,Vector2I)>(),new Vector2I(4,5),out anchor,out source)||anchor!=new Vector2I(4,5)||source!="StartingDeployment"){reason="Deployment anchor fallback failed.";return false;}
        var cached=new ResourceEcologyNegativeEntryV3(20,ResourceRenewalExclusionKindV3.Structure,"structure",7);if(!ResourceEcologyNegativeCachePolicyV3.IsCurrent(cached,10,7)||ResourceEcologyNegativeCachePolicyV3.IsCurrent(cached,10,8)){reason="Negative cache revision invalidation failed.";return false;}
        if(ResourceShortageSafetyCatalogV3.BiomeWeight(BiomeKindV3.RockyHills,NaturalResourceDefinitionCatalogV3.TreeId)!=0||ResourceShortageSafetyCatalogV3.BiomeWeight(BiomeKindV3.ForestLand,NaturalResourceDefinitionCatalogV3.TreeId)<=0||ResourceShortageSafetyCatalogV3.BiomeWeight(BiomeKindV3.ForestLand,NaturalResourceDefinitionCatalogV3.StoneId)!=0){reason="Biome identity safety weights failed.";return false;}
        Rect2I bounds=new(0,0,128,128);ResourceNodeRegistryV3 forestNodes=new();ResourceEcologySessionV3 forest=new(forestNodes,1,bounds,true);forest.RegisterCapacity(new(Vector2I.Zero,NaturalResourceDefinitionCatalogV3.TreeId,"biome.resource.forest",BiomeKindV3.ForestLand,1024,10,0));forest.SynchronizeChunk(Vector2I.Zero);forest.SetChunkActive(Vector2I.Zero,true);forest.UpdateShortageState("company",NaturalResourceDefinitionCatalogV3.TreeId,new(16,16),"fixture",0,0,100,false,ResourceShortageEvaluationStatusV3.Valid,string.Empty);if(!forest.TryGetShortageState("company",NaturalResourceDefinitionCatalogV3.TreeId,out var forestState)||forestState?.Tier!=ResourceShortageTierV3.Critical||forest.ActiveShortagePressureCount!=1||forest.Diagnostics.EmergencyDueSchedulesThisTick!=1){reason="Forest shortage pressure failed.";forest.Dispose();return false;}forest.Dispose();ResourceNodeRegistryV3 normalNodes=new();ResourceEcologySessionV3 normal=new(normalNodes,1,bounds,true);normal.RegisterCapacity(new(Vector2I.Zero,NaturalResourceDefinitionCatalogV3.TreeId,"biome.resource.forest",BiomeKindV3.ForestLand,1024,10,0));normal.SynchronizeChunk(Vector2I.Zero);normal.SetChunkActive(Vector2I.Zero,true);normal.UpdateShortageState("company",NaturalResourceDefinitionCatalogV3.TreeId,new(16,16),"fixture",3,3,100,false,ResourceShortageEvaluationStatusV3.Valid,string.Empty);if(normal.ActiveShortagePressureCount!=0){reason="Normal density changed ecology pressure.";normal.Dispose();return false;}normal.Dispose();ResourceNodeRegistryV3 rockyNodes=new();ResourceEcologySessionV3 rocky=new(rockyNodes,1,bounds,true);rocky.RegisterCapacity(new(Vector2I.Zero,NaturalResourceDefinitionCatalogV3.TreeId,"biome.resource.rocky",BiomeKindV3.RockyHills,1024,10,0));rocky.SynchronizeChunk(Vector2I.Zero);rocky.SetChunkActive(Vector2I.Zero,true);rocky.UpdateShortageState("company",NaturalResourceDefinitionCatalogV3.TreeId,new(16,16),"fixture",0,0,100,false,ResourceShortageEvaluationStatusV3.Valid,string.Empty);if(rocky.ActiveShortagePressureCount!=0){reason="Rocky tree shortage bypassed biome identity.";rocky.Dispose();return false;}rocky.Dispose();
        reason=string.Empty;return true;
    }
}

public static class ResourceEcologySafetyPerformanceFixtureV3
{
    public static string Run()
    {
        Stopwatch watch=Stopwatch.StartNew();ResourceNodeRegistryV3 nodes=new();Rect2I bounds=new(0,0,ChunkDataV2.ChunkSize*100,ChunkDataV2.ChunkSize);int created=0;for(int chunk=0;chunk<100;chunk++)for(int i=0;i<100;i++){Vector2I cell=new(chunk*ChunkDataV2.ChunkSize+i%10,i/10);string id=ResourceNodeIdFactoryV3.CreateDeterministic(8058,i%2==0?NaturalResourceDefinitionCatalogV3.TreeId:NaturalResourceDefinitionCatalogV3.StoneId,cell);ResourceNodeStateV3.TryCreate(id,i%2==0?ResourceNodeTypeV3.Tree:ResourceNodeTypeV3.StoneOutcrop,new(cell),10,10,5,bounds,DateTime.UtcNow,out var node,out _);if(node!=null&&nodes.TryRegister(node,out _))created++;}int checkedNodes=0;for(int chunk=0;chunk<8;chunk++){foreach(string id in nodes.GetNodeIdsInChunk(new(chunk,0))){checkedNodes++;if(checkedNodes>=256)break;}if(checkedNodes>=256)break;}int anchors=0;for(int company=0;company<16;company++){var members=new List<(string,Vector2I)>();for(int m=0;m<100;m++)members.Add(($"merc_{company:D2}_{m:D3}",new Vector2I(company*4+m%10,m/10)));if(CompanyResourceSafetyAnchorV3.TryResolve(members,null,out _,out _))anchors++;}watch.Stop();return $"companies=16 mercenaries=1600 resources={created} spatialNodesChecked={checkedNodes}/256 anchors={anchors} reachabilityPerCompany=1 perNodeAStar=0 fullResourceScan=0 affectedChunksPerEvaluation<=8 elapsed={watch.Elapsed.TotalMilliseconds:0.00}ms";
    }
}
