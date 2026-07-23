using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Godot;

namespace WorldV2;

public enum NaturalResourceCategoryV3 { Vegetation, Geological }
public enum NaturalResourceRenewalClassV3 { VegetativeSpread, GeologicalExposure, None }
public enum ResourceSpawnPatternV3 { Uniform, Clustered }
public enum NaturalResourceOriginKindV3 { InitialDistribution, StartingGuarantee, VegetativeSpread, SeedBankRecovery, GeologicalExposure }

public sealed record NaturalResourceDefinitionV3(
    string DefinitionId,
    GameplayV3.Resources.ResourceNodeTypeV3 NodeType,
    GameplayV3.Resources.ResourceTypeV3 OutputResourceType,
    string NodeDisplayName,
    string ResourceDisplayName,
    NaturalResourceCategoryV3 Category,
    NaturalResourceRenewalClassV3 RenewalClass,
    int InitialAmount,
    int YieldPerCycle);

public static class NaturalResourceDefinitionCatalogV3
{
    public const string TreeId = "natural.tree";
    public const string StoneId = "natural.stone";
    public const string IronVeinId = "iron_vein";
    public const string CopperVeinId = "copper_vein";
    public const string CoalSeamId = "coal_seam";
    public const string ClayDepositId = "clay_deposit";
    public const string FiberBushId = "fiber_bush";
    public const string MedicinalHerbPatchId = "medicinal_herb_patch";
    private static readonly IReadOnlyDictionary<string, NaturalResourceDefinitionV3> Definitions =
        new ReadOnlyDictionary<string, NaturalResourceDefinitionV3>(new Dictionary<string, NaturalResourceDefinitionV3>(StringComparer.Ordinal)
        {
            [TreeId] = new(TreeId, GameplayV3.Resources.ResourceNodeTypeV3.Tree, GameplayV3.Resources.ResourceTypeV3.Wood, "나무", "나무", NaturalResourceCategoryV3.Vegetation, NaturalResourceRenewalClassV3.VegetativeSpread, 15, 5),
            [StoneId] = new(StoneId, GameplayV3.Resources.ResourceNodeTypeV3.StoneOutcrop, GameplayV3.Resources.ResourceTypeV3.Stone, "돌 노두", "돌", NaturalResourceCategoryV3.Geological, NaturalResourceRenewalClassV3.GeologicalExposure, 20, 5),
            [IronVeinId] = new(IronVeinId, GameplayV3.Resources.ResourceNodeTypeV3.IronVein, GameplayV3.Resources.ResourceTypeV3.IronOre, "철광맥", "철광석", NaturalResourceCategoryV3.Geological, NaturalResourceRenewalClassV3.None, 12, 3),
            [CopperVeinId] = new(CopperVeinId, GameplayV3.Resources.ResourceNodeTypeV3.CopperVein, GameplayV3.Resources.ResourceTypeV3.CopperOre, "구리광맥", "구리광석", NaturalResourceCategoryV3.Geological, NaturalResourceRenewalClassV3.None, 10, 3),
            [CoalSeamId] = new(CoalSeamId, GameplayV3.Resources.ResourceNodeTypeV3.CoalSeam, GameplayV3.Resources.ResourceTypeV3.Coal, "석탄층", "석탄", NaturalResourceCategoryV3.Geological, NaturalResourceRenewalClassV3.None, 14, 4),
            [ClayDepositId] = new(ClayDepositId, GameplayV3.Resources.ResourceNodeTypeV3.ClayDeposit, GameplayV3.Resources.ResourceTypeV3.Clay, "점토 퇴적지", "점토", NaturalResourceCategoryV3.Geological, NaturalResourceRenewalClassV3.None, 16, 4),
            [FiberBushId] = new(FiberBushId, GameplayV3.Resources.ResourceNodeTypeV3.FiberBush, GameplayV3.Resources.ResourceTypeV3.Fiber, "섬유 덤불", "섬유", NaturalResourceCategoryV3.Vegetation, NaturalResourceRenewalClassV3.None, 10, 3),
            [MedicinalHerbPatchId] = new(MedicinalHerbPatchId, GameplayV3.Resources.ResourceNodeTypeV3.MedicinalHerbPatch, GameplayV3.Resources.ResourceTypeV3.MedicinalHerb, "약초 군락", "약초", NaturalResourceCategoryV3.Vegetation, NaturalResourceRenewalClassV3.None, 8, 2)
        });
    private static readonly IReadOnlyDictionary<GameplayV3.Resources.ResourceNodeTypeV3,NaturalResourceDefinitionV3> ByNodeType=BuildByNodeType();
    private static readonly IReadOnlyDictionary<GameplayV3.Resources.ResourceTypeV3,NaturalResourceDefinitionV3> ByOutputType=BuildByOutputType();

    public static bool TryGet(string definitionId, out NaturalResourceDefinitionV3? definition) => Definitions.TryGetValue(definitionId, out definition);
    public static bool TryGet(GameplayV3.Resources.ResourceNodeTypeV3 nodeType,out NaturalResourceDefinitionV3? definition)=>ByNodeType.TryGetValue(nodeType,out definition);
    public static bool TryGetResourceDefinition(GameplayV3.Resources.ResourceTypeV3 resourceType,out NaturalResourceDefinitionV3? definition)=>ByOutputType.TryGetValue(resourceType,out definition);
    public static string GetResourceDisplayName(GameplayV3.Resources.ResourceTypeV3 resourceType)=>ByOutputType.TryGetValue(resourceType,out var definition)?definition.ResourceDisplayName:resourceType.ToString();
    public static bool IsKnownResource(GameplayV3.Resources.ResourceTypeV3 resourceType)=>ByOutputType.ContainsKey(resourceType)||resourceType is GameplayV3.Resources.ResourceTypeV3.Ration or GameplayV3.Resources.ResourceTypeV3.Potato;
    public static bool IsRenewableResource(GameplayV3.Resources.ResourceTypeV3 resourceType)=>ByOutputType.TryGetValue(resourceType,out var definition)&&definition.RenewalClass!=NaturalResourceRenewalClassV3.None;
    public static IReadOnlyList<string> GetIds() { var ids=new List<string>(Definitions.Keys);ids.Sort(StringComparer.Ordinal);return ids.AsReadOnly(); }
    private static IReadOnlyDictionary<GameplayV3.Resources.ResourceNodeTypeV3,NaturalResourceDefinitionV3> BuildByNodeType(){var map=new Dictionary<GameplayV3.Resources.ResourceNodeTypeV3,NaturalResourceDefinitionV3>();foreach(var definition in Definitions.Values)map.Add(definition.NodeType,definition);return new ReadOnlyDictionary<GameplayV3.Resources.ResourceNodeTypeV3,NaturalResourceDefinitionV3>(map);}
    private static IReadOnlyDictionary<GameplayV3.Resources.ResourceTypeV3,NaturalResourceDefinitionV3> BuildByOutputType(){var map=new Dictionary<GameplayV3.Resources.ResourceTypeV3,NaturalResourceDefinitionV3>();foreach(var definition in Definitions.Values)map.Add(definition.OutputResourceType,definition);return new ReadOnlyDictionary<GameplayV3.Resources.ResourceTypeV3,NaturalResourceDefinitionV3>(map);}
}

public sealed record TerrainAffinityRuleV3(TileType Terrain, double SpawnWeightMultiplier, bool Allowed = true, bool Required = false, int Priority = 0);

public sealed class ResourceSpawnRuleV3
{
    public required string ResourceDefinitionId { get; init; }
    public required double TargetNodesPer1024EligibleCells { get; init; }
    public required ResourceSpawnPatternV3 Pattern { get; init; }
    public required int MinimumSpacingCells { get; init; }
    public required double ClusterScale { get; init; }
    public required double ClusterStrength { get; init; }
    public required double ClusterThreshold { get; init; }
    public required int MaxNodesPerChunk { get; init; }
    public required int RulePriority { get; init; }
    public required IReadOnlyList<TerrainAffinityRuleV3> TerrainAffinities { get; init; }
    public string StaticExclusionPolicyId { get; init; } = "natural.initial.static";
    public string ConflictGroupId { get; init; } = "natural.solid_node";
    public NaturalResourceRenewalClassV3 RenewalClass { get; init; }
    public bool Enabled { get; init; } = true;
}

public sealed class BiomeResourceDistributionProfileV3
{
    public required string ProfileId { get; init; }
    public required BiomeKindV3 Biome { get; init; }
    public required int ProfileVersion { get; init; }
    public required IReadOnlyList<ResourceSpawnRuleV3> Rules { get; init; }
    public string? FallbackProfileId { get; init; }
    public bool AllowStartingGuarantee { get; init; } = true;
    public int Priority { get; init; }
}

public static class BiomeResourceDistributionCatalogV3
{
    public const int ProfileVersion = 1;
    private static readonly IReadOnlyList<TerrainAffinityRuleV3> TreeAffinity = new List<TerrainAffinityRuleV3>
    {
        new(TileType.ForestGround,1.35), new(TileType.DenseForest,1.50), new(TileType.LightForest,1.25),
        new(TileType.Grass,1.00), new(TileType.Dirt,1.00), new(TileType.WetGrass,0.85),
        new(TileType.QuarryGround,0.10), new(TileType.StoneField,0.08), new(TileType.OreSpot,0.05)
    }.AsReadOnly();
    private static readonly IReadOnlyList<TerrainAffinityRuleV3> StoneAffinity = new List<TerrainAffinityRuleV3>
    {
        new(TileType.QuarryGround,1.65), new(TileType.StoneField,1.55), new(TileType.OreSpot,1.35),
        new(TileType.Hills,1.25), new(TileType.Mountain,1.45), new(TileType.Grass,0.65), new(TileType.Dirt,0.70),
        new(TileType.ForestGround,0.45), new(TileType.DenseForest,0.35), new(TileType.LightForest,0.50)
    }.AsReadOnly();
    private static readonly IReadOnlyList<TerrainAffinityRuleV3> ClayAffinity = new List<TerrainAffinityRuleV3>
    {new(TileType.Grass,1.15),new(TileType.Dirt,1.25),new(TileType.WetGrass,1.30),new(TileType.ForestGround,.45),new(TileType.QuarryGround,.35),new(TileType.StoneField,.25)}.AsReadOnly();
    private static readonly IReadOnlyList<TerrainAffinityRuleV3> FiberAffinity = new List<TerrainAffinityRuleV3>
    {new(TileType.ForestGround,1.45),new(TileType.DenseForest,1.50),new(TileType.LightForest,1.35),new(TileType.Grass,1.0),new(TileType.WetGrass,1.15),new(TileType.QuarryGround,.08),new(TileType.StoneField,.05)}.AsReadOnly();
    private static readonly IReadOnlyList<TerrainAffinityRuleV3> HerbAffinity = new List<TerrainAffinityRuleV3>
    {new(TileType.ForestGround,1.45),new(TileType.DenseForest,1.35),new(TileType.LightForest,1.50),new(TileType.Grass,1.0),new(TileType.WetGrass,1.20),new(TileType.QuarryGround,.12),new(TileType.StoneField,.08)}.AsReadOnly();

    private static ResourceSpawnRuleV3 Rule(string id,double density,ResourceSpawnPatternV3 pattern,int spacing,double strength,int priority,IReadOnlyList<TerrainAffinityRuleV3> affinity) => new()
    {
        ResourceDefinitionId=id,TargetNodesPer1024EligibleCells=density,Pattern=pattern,MinimumSpacingCells=spacing,
        ClusterScale=48.0,ClusterStrength=strength,ClusterThreshold=0.0,MaxNodesPerChunk=64,RulePriority=priority,
        TerrainAffinities=affinity,RenewalClass=NaturalResourceDefinitionCatalogV3.TryGet(id,out var definition)&&definition!=null?definition.RenewalClass:NaturalResourceRenewalClassV3.None
    };

    private static readonly BiomeResourceDistributionProfileV3 Low = new()
    {
        ProfileId="biome.resource.low",Biome=BiomeKindV3.Plains,ProfileVersion=ProfileVersion,AllowStartingGuarantee=true,Priority=-100,
        Rules=CreateRules(1.0,4,0,1.0,4,0,.05,.05,.05,.25,.25,.20)
    };
    private static readonly IReadOnlyDictionary<BiomeKindV3,BiomeResourceDistributionProfileV3> ByBiome = Build();
    private static readonly IReadOnlyDictionary<string,BiomeResourceDistributionProfileV3> ById = BuildById();
    public static int Count=>ById.Count;
    public static int RuleCount{get{int n=0;foreach(var profile in ById.Values)n+=profile.Rules.Count;return n;}}
    public static BiomeResourceDistributionProfileV3 Fallback=>Low;
    public static bool TryGet(BiomeKindV3 biome,out BiomeResourceDistributionProfileV3? profile)=>ByBiome.TryGetValue(biome,out profile);
    public static bool TryGet(string id,out BiomeResourceDistributionProfileV3? profile)=>ById.TryGetValue(id,out profile);
    public static BiomeResourceDistributionProfileV3 Resolve(BiomeKindV3 biome,out bool usedFallback){if(ByBiome.TryGetValue(biome,out var p)){usedFallback=false;return p;}usedFallback=true;return Low;}

    private static IReadOnlyDictionary<BiomeKindV3,BiomeResourceDistributionProfileV3> Build()
    {
        var map=new Dictionary<BiomeKindV3,BiomeResourceDistributionProfileV3>();
        Add(BiomeKindV3.Plains,"biome.resource.plains",5,3,0.15,3,3,0.25,.25,.15,.15,2,3,2);
        Add(BiomeKindV3.ForestLand,"biome.resource.forest",30,2,0.70,2,4,0.20,.10,.10,.20,.50,6,4);
        Add(BiomeKindV3.RockyHills,"biome.resource.rocky",0.5,6,0.0,20,2,0.65,3.5,2,3,.5,.1,.2);
        Add(BiomeKindV3.Dryland,"biome.resource.dryland",1.0,5,0.05,8,3,0.40,.4,.25,.5,1.5,.5,.25);
        Add(BiomeKindV3.Wasteland,"biome.resource.wasteland",0.5,6,0.0,5,4,0.25,1.5,.75,2,.3,.05,.05);
        return new ReadOnlyDictionary<BiomeKindV3,BiomeResourceDistributionProfileV3>(map);
        void Add(BiomeKindV3 biome,string id,double tree,int treeSpacing,double treeCluster,double stone,int stoneSpacing,double stoneCluster,double iron,double copper,double coal,double clay,double fiber,double herb)
        {map.Add(biome,new(){ProfileId=id,Biome=biome,ProfileVersion=ProfileVersion,Priority=10,Rules=CreateRules(tree,treeSpacing,treeCluster,stone,stoneSpacing,stoneCluster,iron,copper,coal,clay,fiber,herb)});}
    }
    private static IReadOnlyList<ResourceSpawnRuleV3> CreateRules(double tree,int treeSpacing,double treeCluster,double stone,int stoneSpacing,double stoneCluster,double iron,double copper,double coal,double clay,double fiber,double herb)=>new List<ResourceSpawnRuleV3>
    {
        Rule(NaturalResourceDefinitionCatalogV3.TreeId,tree,treeCluster>0?ResourceSpawnPatternV3.Clustered:ResourceSpawnPatternV3.Uniform,treeSpacing,treeCluster,100,TreeAffinity),
        Rule(NaturalResourceDefinitionCatalogV3.StoneId,stone,stoneCluster>0?ResourceSpawnPatternV3.Clustered:ResourceSpawnPatternV3.Uniform,stoneSpacing,stoneCluster,90,StoneAffinity),
        Rule(NaturalResourceDefinitionCatalogV3.IronVeinId,iron,ResourceSpawnPatternV3.Clustered,5,.55,40,StoneAffinity),
        Rule(NaturalResourceDefinitionCatalogV3.CopperVeinId,copper,ResourceSpawnPatternV3.Clustered,5,.50,35,StoneAffinity),
        Rule(NaturalResourceDefinitionCatalogV3.CoalSeamId,coal,ResourceSpawnPatternV3.Clustered,4,.60,30,StoneAffinity),
        Rule(NaturalResourceDefinitionCatalogV3.ClayDepositId,clay,ResourceSpawnPatternV3.Clustered,3,.35,25,ClayAffinity),
        Rule(NaturalResourceDefinitionCatalogV3.FiberBushId,fiber,ResourceSpawnPatternV3.Clustered,3,.45,20,FiberAffinity),
        Rule(NaturalResourceDefinitionCatalogV3.MedicinalHerbPatchId,herb,ResourceSpawnPatternV3.Clustered,4,.45,15,HerbAffinity)
    }.AsReadOnly();
    private static IReadOnlyDictionary<string,BiomeResourceDistributionProfileV3> BuildById(){var map=new Dictionary<string,BiomeResourceDistributionProfileV3>(StringComparer.Ordinal){{Low.ProfileId,Low}};foreach(var p in ByBiome.Values)map.Add(p.ProfileId,p);return new ReadOnlyDictionary<string,BiomeResourceDistributionProfileV3>(map);}

    public static bool TryValidate(out string reason)
    {
        var profileIds=new HashSet<string>(StringComparer.Ordinal);var biomes=new HashSet<BiomeKindV3>();
        foreach(var p in ById.Values){if(string.IsNullOrWhiteSpace(p.ProfileId)||!profileIds.Add(p.ProfileId)||p.ProfileVersion<=0){reason="Invalid or duplicate profile.";return false;}if(p!=Low&&!biomes.Add(p.Biome)){reason="Duplicate biome profile.";return false;}var rules=new HashSet<string>(StringComparer.Ordinal);foreach(var r in p.Rules){if(!rules.Add(r.ResourceDefinitionId)||!NaturalResourceDefinitionCatalogV3.TryGet(r.ResourceDefinitionId,out _)){reason="Invalid or duplicate resource rule.";return false;}if(!double.IsFinite(r.TargetNodesPer1024EligibleCells)||r.TargetNodesPer1024EligibleCells<0||r.MinimumSpacingCells<0||!double.IsFinite(r.ClusterScale)||r.ClusterScale<=0||!double.IsFinite(r.ClusterStrength)||r.ClusterStrength<0||r.ClusterStrength>1||r.MaxNodesPerChunk<0||string.IsNullOrWhiteSpace(r.ConflictGroupId)){reason="Invalid resource rule values.";return false;}}}
        reason=string.Empty;return true;
    }
}

public readonly record struct ResourceSpawnEnvironmentV3(int WorldSeed,string GeneratorVersion,Vector2I GlobalCell,Vector2I ChunkCoordinate,BiomeKindV3 Biome,TileType Terrain,bool IsWater,bool IsRoad,bool IsSettlementCore,bool IsLandmarkOccupied,bool IsStaticBlocked);
public readonly record struct NaturalResourceSpawnDescriptorV3(string DeterministicResourceId,string ResourceDefinitionId,Vector2I GlobalCell,string ProfileId,int ProfileVersion,int PlacementAlgorithmVersion,NaturalResourceOriginKindV3 OriginKind,BiomeKindV3 Biome,TileType Terrain,double SuitabilityScore,double CandidateScore,long SpawnSeed,int VisualVariant);
public readonly record struct ResourceEcologyCapacitySnapshotV3(Vector2I ChunkCoord,string ResourceDefinitionId,string ProfileId,BiomeKindV3 Biome,int EligibleCellCount,double InitialTargetNodeCount,int InitialSpawnCount);

public sealed class ResourcePlacementChunkDiagnosticsV3
{
    public Vector2I ChunkCoord{get;internal set;}public int CandidateCellsEvaluated{get;internal set;}public int EligibleCandidateCount{get;internal set;}public int TreeCount{get;internal set;}public int StoneCount{get;internal set;}public int StaticExclusionRejectedCount{get;internal set;}public int TerrainRejectedCount{get;internal set;}public int SpacingRejectedCount{get;internal set;}public int ConflictRejectedCount{get;internal set;}public int ChunkCapRejectedCount{get;internal set;}public int FallbackProfileUseCount{get;internal set;}public double ElapsedMilliseconds{get;internal set;}public bool WorkerThread{get;internal set;}public IReadOnlyList<ResourceEcologyCapacitySnapshotV3> EcologyCapacities{get;internal set;}=Array.Empty<ResourceEcologyCapacitySnapshotV3>();
}

public static class ResourcePlacementEvaluatorV3
{
    public const int AlgorithmVersion=1;
    private const ulong CandidateSalt=0x43414e4449444154UL;
    private const ulong DensitySalt=0x44454e5349545953UL;
    private const ulong ClusterSalt=0x434c555354455253UL;
    private const ulong IdSalt=0x5245534f55524345UL;

    public static IReadOnlyList<NaturalResourceSpawnDescriptorV3> GenerateChunk(int worldSeed,string generatorVersion,Vector2I chunkCoord,Rect2I worldBounds,Func<Vector2I,FlatlandCellSampleV2> sample,out ResourcePlacementChunkDiagnosticsV3 diagnostics)
    {
        string placementVersion=$"{generatorVersion}|resource-placement-{AlgorithmVersion}";var watch=Stopwatch.StartNew();diagnostics=new(){ChunkCoord=chunkCoord,WorkerThread=string.Equals(System.Threading.Thread.CurrentThread.Name,"WorldV2 Chunk Generation Worker",StringComparison.Ordinal)};var winners=new Dictionary<Vector2I,(NaturalResourceSpawnDescriptorV3 Descriptor,int Priority,string Conflict)>();var acceptedPerRule=new Dictionary<string,int>(StringComparer.Ordinal);var perRule=new Dictionary<string,int>(StringComparer.Ordinal);var eligible=new Dictionary<(string Resource,string Profile),(int Count,double Target,BiomeKindV3 Biome)>();Vector2I origin=WorldV2CoordinateUtility.GlobalChunkToOriginGlobalCell(chunkCoord);
        for(int y=0;y<ChunkDataV2.ChunkSize;y++)for(int x=0;x<ChunkDataV2.ChunkSize;x++)
        {
            Vector2I cell=origin+new Vector2I(x,y);if(!worldBounds.HasPoint(cell))continue;FlatlandCellSampleV2 s=sample(cell);BiomeResourceDistributionProfileV3 profile=BiomeResourceDistributionCatalogV3.Resolve(s.BiomeKind,out bool fallback);if(fallback)diagnostics.FallbackProfileUseCount++;
            foreach(ResourceSpawnRuleV3 rule in profile.Rules)
            {
                if(!rule.Enabled)continue;diagnostics.CandidateCellsEvaluated++;
                if(IsStaticExcluded(s)){diagnostics.StaticExclusionRejectedCount++;continue;}
                double affinity=GetTerrainAffinity(rule,s);if(affinity<=0){diagnostics.TerrainRejectedCount++;continue;}diagnostics.EligibleCandidateCount++;var capacityKey=(rule.ResourceDefinitionId,profile.ProfileId);var capacity=eligible.GetValueOrDefault(capacityKey);eligible[capacityKey]=(capacity.Count+1,capacity.Target+rule.TargetNodesPer1024EligibleCells/1024.0,profile.Biome);
                double rank=ToUnit(Hash(worldSeed,placementVersion,rule.ResourceDefinitionId,cell,CandidateSalt));if(!IsSpacingWinner(worldSeed,placementVersion,rule,cell,rank)){diagnostics.SpacingRejectedCount++;continue;}
                double cluster=rule.Pattern==ResourceSpawnPatternV3.Clustered?EvaluateCluster(worldSeed,rule,cell):1.0;double area=Math.Pow(rule.MinimumSpacingCells*2+1,2);double probability=Math.Clamp(rule.TargetNodesPer1024EligibleCells*area/1024.0*affinity*cluster,0,1);double densityRoll=ToUnit(Hash(worldSeed,placementVersion,rule.ResourceDefinitionId,cell,DensitySalt));if(densityRoll>=probability)continue;
                if(acceptedPerRule.GetValueOrDefault(rule.ResourceDefinitionId)>=rule.MaxNodesPerChunk){diagnostics.ChunkCapRejectedCount++;continue;}double suitability=affinity*cluster*(1.0-densityRoll);string id=CreateDeterministicId(worldSeed,rule.ResourceDefinitionId,cell);ulong seed=Hash(worldSeed,placementVersion,rule.ResourceDefinitionId,cell,IdSalt);var descriptor=new NaturalResourceSpawnDescriptorV3(id,rule.ResourceDefinitionId,cell,profile.ProfileId,profile.ProfileVersion,AlgorithmVersion,NaturalResourceOriginKindV3.InitialDistribution,s.BiomeKind,s.TileType,suitability,rank,unchecked((long)seed),(int)(seed%4));
                if(winners.TryGetValue(cell,out var existing)){bool existingLegacy=existing.Descriptor.ResourceDefinitionId is NaturalResourceDefinitionCatalogV3.TreeId or NaturalResourceDefinitionCatalogV3.StoneId;if(existingLegacy){diagnostics.ConflictRejectedCount++;continue;}int compare=suitability.CompareTo(existing.Descriptor.SuitabilityScore);if(compare<0||(compare==0&&(rule.RulePriority<existing.Priority||(rule.RulePriority==existing.Priority&&string.CompareOrdinal(rule.ResourceDefinitionId,existing.Descriptor.ResourceDefinitionId)>0)))){diagnostics.ConflictRejectedCount++;continue;}acceptedPerRule[existing.Descriptor.ResourceDefinitionId]=Math.Max(0,acceptedPerRule.GetValueOrDefault(existing.Descriptor.ResourceDefinitionId)-1);winners[cell]=(descriptor,rule.RulePriority,rule.ConflictGroupId);acceptedPerRule[rule.ResourceDefinitionId]=acceptedPerRule.GetValueOrDefault(rule.ResourceDefinitionId)+1;diagnostics.ConflictRejectedCount++;}
                else{winners.Add(cell,(descriptor,rule.RulePriority,rule.ConflictGroupId));acceptedPerRule[rule.ResourceDefinitionId]=acceptedPerRule.GetValueOrDefault(rule.ResourceDefinitionId)+1;}
            }
        }
        var result=new List<NaturalResourceSpawnDescriptorV3>(winners.Count);foreach(var pair in winners){result.Add(pair.Value.Descriptor);perRule[pair.Value.Descriptor.ResourceDefinitionId]=perRule.GetValueOrDefault(pair.Value.Descriptor.ResourceDefinitionId)+1;}result.Sort((a,b)=>{int c=a.GlobalCell.Y.CompareTo(b.GlobalCell.Y);if(c!=0)return c;c=a.GlobalCell.X.CompareTo(b.GlobalCell.X);return c!=0?c:string.CompareOrdinal(a.ResourceDefinitionId,b.ResourceDefinitionId);});var capacities=new List<ResourceEcologyCapacitySnapshotV3>();foreach(var pair in eligible.OrderBy(x=>x.Key.Resource,StringComparer.Ordinal).ThenBy(x=>x.Key.Profile,StringComparer.Ordinal)){int spawned=result.Count(x=>x.ResourceDefinitionId==pair.Key.Resource&&x.ProfileId==pair.Key.Profile);capacities.Add(new(chunkCoord,pair.Key.Resource,pair.Key.Profile,pair.Value.Biome,pair.Value.Count,pair.Value.Target,spawned));}diagnostics.EcologyCapacities=capacities.AsReadOnly();diagnostics.TreeCount=perRule.GetValueOrDefault(NaturalResourceDefinitionCatalogV3.TreeId);diagnostics.StoneCount=perRule.GetValueOrDefault(NaturalResourceDefinitionCatalogV3.StoneId);watch.Stop();diagnostics.ElapsedMilliseconds=watch.Elapsed.TotalMilliseconds;return result.AsReadOnly();
    }

    public static string CreateDeterministicId(int worldSeed,string definitionId,Vector2I cell){string version=$"resource-placement-{AlgorithmVersion}";ulong a=Hash(worldSeed,version,definitionId,cell,IdSalt),b=Hash(worldSeed,version,definitionId,cell,IdSalt^0x9e3779b97f4a7c15UL);return "rnode_"+a.ToString("x16",CultureInfo.InvariantCulture)+b.ToString("x16",CultureInfo.InvariantCulture);}
    public static bool TryEvaluateRenewalCell(string profileId,string definitionId,FlatlandCellSampleV2 sample,out ResourceSpawnRuleV3? rule,out double affinity){rule=null;affinity=0;if(IsStaticExcluded(sample)||!BiomeResourceDistributionCatalogV3.TryGet(profileId,out var profile)||profile==null)return false;foreach(var candidate in profile.Rules)if(candidate.Enabled&&candidate.ResourceDefinitionId==definitionId){rule=candidate;affinity=GetTerrainAffinity(candidate,sample);return affinity>0;}return false;}
    public static bool TryEvaluateRenewalCell(string profileId,string definitionId,CellData cell,out ResourceSpawnRuleV3? rule,out double affinity){rule=null;affinity=0;if(!cell.IsWalkable||cell.IsRiver||cell.IsRoad||cell.IsVillage||cell.IsStartingVillage||cell.IsLandmark||cell.IsDungeonEntrance||cell.IsBanditCamp||cell.IsFactionOutpost||cell.IsBuildRestricted||cell.TileType is TileType.Water or TileType.Road or TileType.TownPavement or TileType.Plaza or TileType.Village or TileType.Dungeon or TileType.BanditCamp or TileType.FactionOutpost or TileType.Ruin||!BiomeResourceDistributionCatalogV3.TryGet(profileId,out var profile)||profile==null)return false;foreach(var candidate in profile.Rules)if(candidate.Enabled&&candidate.ResourceDefinitionId==definitionId){rule=candidate;foreach(var a in candidate.TerrainAffinities)if(a.Terrain==cell.TileType){affinity=a.Allowed?a.SpawnWeightMultiplier:0;break;}if(affinity==0)affinity=definitionId==NaturalResourceDefinitionCatalogV3.TreeId?(cell.ForestStrength>=.42f?1.25:1):.65;return affinity>0;}return false;}
    private static bool IsStaticExcluded(FlatlandCellSampleV2 s)=>!s.IsWalkable||s.IsRiver||s.IsRoad||s.IsVillage||s.IsStartingVillage||s.IsLandmark||s.IsDungeonEntrance||s.IsBanditCamp||s.IsFactionOutpost||s.IsBuildRestricted||s.TileType is TileType.Water or TileType.Road or TileType.TownPavement or TileType.Plaza or TileType.Village or TileType.Dungeon or TileType.BanditCamp or TileType.FactionOutpost or TileType.Ruin;
    private static double GetTerrainAffinity(ResourceSpawnRuleV3 rule,FlatlandCellSampleV2 sample){double fallback=rule.ResourceDefinitionId==NaturalResourceDefinitionCatalogV3.TreeId?(sample.ForestStrength>=0.42f?1.25:1.0):0.65;foreach(var a in rule.TerrainAffinities)if(a.Terrain==sample.TileType)return a.Allowed?a.SpawnWeightMultiplier:0;return fallback;}
    private static bool IsSpacingWinner(int seed,string version,ResourceSpawnRuleV3 rule,Vector2I cell,double rank){int r=rule.MinimumSpacingCells;for(int y=-r;y<=r;y++)for(int x=-r;x<=r;x++){if(x==0&&y==0)continue;Vector2I other=cell+new Vector2I(x,y);double otherRank=ToUnit(Hash(seed,version,rule.ResourceDefinitionId,other,CandidateSalt));if(otherRank>rank||(otherRank==rank&&(other.Y<cell.Y||(other.Y==cell.Y&&other.X<cell.X))))return false;}return true;}
    private static double EvaluateCluster(int seed,ResourceSpawnRuleV3 rule,Vector2I cell){double scale=Math.Max(4,rule.ClusterScale);double x=cell.X/scale,y=cell.Y/scale;int x0=(int)Math.Floor(x),y0=(int)Math.Floor(y);double tx=Smooth(x-x0),ty=Smooth(y-y0);double a=Lerp(Grid(x0,y0),Grid(x0+1,y0),tx),b=Lerp(Grid(x0,y0+1),Grid(x0+1,y0+1),tx);double field=Lerp(a,b,ty);return Math.Max(0.05,(1-rule.ClusterStrength)+rule.ClusterStrength*(field*2.0));double Grid(int gx,int gy)=>ToUnit(Hash(seed,"cluster",rule.ResourceDefinitionId,new(gx,gy),ClusterSalt));}
    private static double Smooth(double t)=>t*t*(3-2*t);private static double Lerp(double a,double b,double t)=>a+(b-a)*t;
    private static double ToUnit(ulong value)=>(value>>11)*(1.0/(1UL<<53));
    private static ulong Hash(int seed,string version,string id,Vector2I cell,ulong salt){ulong h=1469598103934665603UL;Mix(unchecked((ulong)(uint)seed));foreach(char c in version)Mix(c);foreach(char c in id)Mix(c);Mix(unchecked((ulong)(uint)cell.X));Mix(unchecked((ulong)(uint)cell.Y));Mix(salt);h^=h>>33;h*=0xff51afd7ed558ccdUL;h^=h>>33;h*=0xc4ceb9fe1a85ec53UL;h^=h>>33;return h;void Mix(ulong v){h^=v;h*=1099511628211UL;}}
}

public static class BiomeResourceDistributionSelfCheckV3
{
    public static string LastDistributionSummary{get;private set;}=string.Empty;
    public static bool TryValidate(out string reason)
    {
        if(!BiomeResourceDistributionCatalogV3.TryValidate(out reason))return false;
        var plainsProfile=BiomeResourceDistributionCatalogV3.Resolve(BiomeKindV3.Plains,out _);var forestProfile=BiomeResourceDistributionCatalogV3.Resolve(BiomeKindV3.ForestLand,out _);var rockyProfile=BiomeResourceDistributionCatalogV3.Resolve(BiomeKindV3.RockyHills,out _);
        double P(BiomeResourceDistributionProfileV3 p,string id){foreach(var r in p.Rules)if(r.ResourceDefinitionId==id)return r.TargetNodesPer1024EligibleCells;return -1;}
        if(P(forestProfile,NaturalResourceDefinitionCatalogV3.TreeId)<P(plainsProfile,NaturalResourceDefinitionCatalogV3.TreeId)*4||P(rockyProfile,NaturalResourceDefinitionCatalogV3.StoneId)<P(plainsProfile,NaturalResourceDefinitionCatalogV3.StoneId)*5||P(rockyProfile,NaturalResourceDefinitionCatalogV3.TreeId)>P(plainsProfile,NaturalResourceDefinitionCatalogV3.TreeId)*.1||P(forestProfile,NaturalResourceDefinitionCatalogV3.StoneId)>P(plainsProfile,NaturalResourceDefinitionCatalogV3.StoneId)){reason="Relative biome resource densities are invalid.";return false;}
        string a=ResourcePlacementEvaluatorV3.CreateDeterministicId(42,NaturalResourceDefinitionCatalogV3.TreeId,new(100,200));string b=ResourcePlacementEvaluatorV3.CreateDeterministicId(42,NaturalResourceDefinitionCatalogV3.TreeId,new(100,200));if(a!=b||!GameplayV3.Resources.ResourceNodeIdFactoryV3.IsValid(a)){reason="Deterministic resource ID is invalid.";return false;}
        var bounds=new Rect2I(Vector2I.Zero,new Vector2I(4096,4096));
        (int Trees,int Stones) plains=Count(BiomeKindV3.Plains,TileType.Grass,10);
        (int Trees,int Stones) forest=Count(BiomeKindV3.ForestLand,TileType.ForestGround,10);
        (int Trees,int Stones) rocky=Count(BiomeKindV3.RockyHills,TileType.QuarryGround,10);
        if(forest.Trees<=plains.Trees*2||rocky.Stones<=plains.Stones*3||rocky.Trees>plains.Trees){reason=$"Observed relative distribution failed P={plains} F={forest} R={rocky}.";return false;}
        var first=ResourcePlacementEvaluatorV3.GenerateChunk(73,"V3",new(20,20),bounds,c=>Sample(BiomeKindV3.ForestLand,TileType.ForestGround,c),out _);var second=ResourcePlacementEvaluatorV3.GenerateChunk(73,"V3",new(20,20),bounds,c=>Sample(BiomeKindV3.ForestLand,TileType.ForestGround,c),out _);if(first.Count!=second.Count){reason="Repeated chunk result count differs.";return false;}for(int i=0;i<first.Count;i++)if(first[i]!=second[i]){reason="Repeated chunk descriptors differ.";return false;}
        var adjacent=ResourcePlacementEvaluatorV3.GenerateChunk(73,"V3",new(21,20),bounds,c=>Sample(BiomeKindV3.ForestLand,TileType.ForestGround,c),out _);var seam=new List<NaturalResourceSpawnDescriptorV3>(first);seam.AddRange(adjacent);for(int i=0;i<seam.Count;i++)for(int j=i+1;j<seam.Count;j++)if(seam[i].ResourceDefinitionId==seam[j].ResourceDefinitionId){int spacing=0;foreach(var rule in forestProfile.Rules)if(rule.ResourceDefinitionId==seam[i].ResourceDefinitionId){spacing=rule.MinimumSpacingCells;break;}int distance=Math.Max(Math.Abs(seam[i].GlobalCell.X-seam[j].GlobalCell.X),Math.Abs(seam[i].GlobalCell.Y-seam[j].GlobalCell.Y));if(distance<=spacing){reason="Chunk seam minimum spacing failed.";return false;}}
        var excluded=ResourcePlacementEvaluatorV3.GenerateChunk(73,"V3",new(21,20),bounds,c=>new FlatlandCellSampleV2{GlobalCellCoord=c,BiomeKind=BiomeKindV3.ForestLand,TileType=TileType.Road,IsRoad=true,IsWalkable=true},out _);if(excluded.Count!=0){reason="Static road exclusion failed.";return false;}
        LastDistributionSummary=$"10 chunks P(T/S)={plains.Trees}/{plains.Stones} F={forest.Trees}/{forest.Stones} R={rocky.Trees}/{rocky.Stones}";
        reason=string.Empty;return true;
        (int Trees,int Stones) Count(BiomeKindV3 biome,TileType terrain,int chunks){int trees=0,stones=0;for(int i=0;i<chunks;i++){Vector2I cc=new(10+i,12);var descriptors=ResourcePlacementEvaluatorV3.GenerateChunk(73,"V3",cc,bounds,c=>Sample(biome,terrain,c),out _);foreach(var d in descriptors)if(d.ResourceDefinitionId==NaturalResourceDefinitionCatalogV3.TreeId)trees++;else if(d.ResourceDefinitionId==NaturalResourceDefinitionCatalogV3.StoneId)stones++;}return(trees,stones);}
        static FlatlandCellSampleV2 Sample(BiomeKindV3 biome,TileType terrain,Vector2I cell)=>new(){GlobalCellCoord=cell,BiomeKind=biome,TileType=terrain,ForestStrength=biome==BiomeKindV3.ForestLand?.75f:0,IsWalkable=true};
    }

    public static string RunPerformanceFixture(int chunkCount)
    {
        var watch=Stopwatch.StartNew();int trees=0,stones=0,descriptors=0;var bounds=new Rect2I(Vector2I.Zero,new Vector2I(16384,16384));
        for(int i=0;i<chunkCount;i++)
        {
            BiomeKindV3 biome=(BiomeKindV3)(i%5);TileType terrain=biome switch{BiomeKindV3.ForestLand=>TileType.ForestGround,BiomeKindV3.RockyHills=>TileType.QuarryGround,_=>TileType.Grass};Vector2I chunk=new(100+i%100,100+i/100);
            var result=ResourcePlacementEvaluatorV3.GenerateChunk(991,"V3",chunk,bounds,c=>new FlatlandCellSampleV2{GlobalCellCoord=c,BiomeKind=biome,TileType=terrain,ForestStrength=biome==BiomeKindV3.ForestLand?.75f:0,IsWalkable=true},out _);descriptors+=result.Count;foreach(var d in result)if(d.ResourceDefinitionId==NaturalResourceDefinitionCatalogV3.TreeId)trees++;else if(d.ResourceDefinitionId==NaturalResourceDefinitionCatalogV3.StoneId)stones++;
        }
        watch.Stop();return $"chunks={chunkCount} descriptors={descriptors} trees={trees} stones={stones} elapsedMs={watch.Elapsed.TotalMilliseconds:0.00} avgMs={watch.Elapsed.TotalMilliseconds/Math.Max(1,chunkCount):0.000}";
    }

    public static string RunPresetFixture()
    {
        var summaries=new List<string>();foreach(WorldMapSizePresetV2 preset in Enum.GetValues<WorldMapSizePresetV2>())
        {
            var watch=Stopwatch.StartNew();var request=new WorldGenerationRequestV2(preset,81273,WorldPlanVersionV2.V3);var generator=new ProceduralWorldGeneratorV2();generator.SetGenerationSettings(WorldGenerationSettingsV2.Default);generator.SetGenerationRequest(request);Vector2I chunk=WorldV2CoordinateUtility.GlobalCellToGlobalChunkCoord(request.MapSize.CenterCell);ChunkDataV2 first=generator.GenerateChunkDataOnly(request.WorldId,request.Seed,chunk,request);ChunkDataV2 second=generator.GenerateChunkDataOnly(request.WorldId,request.Seed,chunk,request);bool same=first.NaturalResourceSpawns.Count==second.NaturalResourceSpawns.Count;for(int i=0;same&&i<first.NaturalResourceSpawns.Count;i++)same=first.NaturalResourceSpawns[i]==second.NaturalResourceSpawns[i];watch.Stop();summaries.Add($"{preset}:resources={first.NaturalResourceSpawns.Count},deterministic={same},ms={watch.Elapsed.TotalMilliseconds:0.0}");
        }
        return string.Join("; ",summaries);
    }
}
