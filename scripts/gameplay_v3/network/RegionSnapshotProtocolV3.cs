using System;
using System.Collections.Generic;
using System.Text.Json;
using GameplayV3.Construction;
using GameplayV3.Equipment;
using GameplayV3.Farming;
using GameplayV3.Mercenary;
using GameplayV3.Production;
using GameplayV3.Resources;
using GameplayV3.Session;

namespace GameplayV3.Network;

public sealed record SnapshotCellV3(int X,int Y);
public sealed record SnapshotMaterialV3(ResourceTypeV3 ResourceType,int Required,int Delivered);
public sealed record MercenarySnapshotDtoV3(
    string MercenaryId,string CompanyId,bool IsOwnedByRecipient,string DisplayName,SnapshotCellV3 Cell,bool AtRegion,
    MercenaryActivityStateV3 ActivityState,float Hunger,float Fatigue,int Strength,int Agility,int Endurance,int Intelligence,int Mental,
    int Hauling,int Construction,int Gathering,int Farming,int Production,int Medicine,int Guarding,
    string? MainHandEquipmentInstanceId,string? ArmorEquipmentInstanceId,string? ToolEquipmentInstanceId);
public sealed record ResourceNodeSnapshotDtoV3(string ResourceNodeId,string DefinitionId,ResourceNodeTypeV3 NodeType,ResourceTypeV3 ProducedResourceType,SnapshotCellV3 Cell,int RemainingAmount,int InitialAmount);
public sealed record GroundResourceStackSnapshotDtoV3(string ResourceStackId,ResourceTypeV3 ResourceType,SnapshotCellV3 Cell,int Quantity);
public sealed record StructureSnapshotDtoV3(string StructureId,string DefinitionId,string CompanyId,SnapshotCellV3 AnchorCell,StructureOrientationV3 Orientation,SnapshotCellV3[] Footprint,float CurrentHealth,float MaxHealth);
public sealed record BlueprintSnapshotDtoV3(string BlueprintId,string DefinitionId,string CompanyId,SnapshotCellV3 AnchorCell,StructureOrientationV3 Orientation,SnapshotCellV3[] Footprint,SnapshotMaterialV3[] Materials,ConstructionBlueprintStatusV3 Status,float ProgressSeconds,float RequiredWorkSeconds);
public sealed record StockpileSnapshotDtoV3(string StockpileId,string CompanyId,SnapshotCellV3[] Cells,bool IsEnabled,bool AllowsEquipment,ResourceTypeV3[] AllowedResources,string[] StoredResourceStackIds);
public sealed record ProductionOrderSnapshotDtoV3(string OrderId,string RecipeId,int RequestedBatches,int CompletedBatches,ProductionOrderStateV3 State,float ProgressSeconds);
public sealed record ProductionFacilitySnapshotDtoV3(string FacilityId,string DefinitionId,string CompanyId,int FacilityKind,SnapshotCellV3 AnchorCell,ProductionOrderSnapshotDtoV3[] Orders,string[] FacilityOutputEquipmentInstanceIds);
public sealed record FarmCropSnapshotDtoV3(SnapshotCellV3 Cell,CropStageV3 Stage,float GrowthNormalized);
public sealed record FarmPlotSnapshotDtoV3(string FarmPlotId,string CompanyId,string CropDefinitionId,SnapshotCellV3[] Cells,FarmCropSnapshotDtoV3[] Crops);
public sealed record EquipmentSnapshotDtoV3(
    string EquipmentInstanceId,string DefinitionId,EquipmentQualityV3 Quality,int QualityScore,
    string CrafterMercenaryId,int CrafterProductionSkillSnapshot,string OwnerCompanyId,
    EquipmentLocationKindV3 LocationKind,SnapshotCellV3? GroundCell,string? StorageId,SnapshotCellV3? StorageCell,
    string? FacilityId,string? EquippedMercenaryId,EquipmentSlotV3? EquippedSlot);

public sealed record InitialRegionSnapshotPayloadV3
{
    public string OwnerCompanyId{get;init;}=string.Empty;
    public int TerrainSeed{get;init;}
    public MercenarySnapshotDtoV3[] Mercenaries{get;init;}=Array.Empty<MercenarySnapshotDtoV3>();
    public ResourceNodeSnapshotDtoV3[] ResourceNodes{get;init;}=Array.Empty<ResourceNodeSnapshotDtoV3>();
    public GroundResourceStackSnapshotDtoV3[] GroundResourceStacks{get;init;}=Array.Empty<GroundResourceStackSnapshotDtoV3>();
    public StructureSnapshotDtoV3[] Structures{get;init;}=Array.Empty<StructureSnapshotDtoV3>();
    public BlueprintSnapshotDtoV3[] Blueprints{get;init;}=Array.Empty<BlueprintSnapshotDtoV3>();
    public StockpileSnapshotDtoV3[] Stockpiles{get;init;}=Array.Empty<StockpileSnapshotDtoV3>();
    public ProductionFacilitySnapshotDtoV3[] ProductionFacilities{get;init;}=Array.Empty<ProductionFacilitySnapshotDtoV3>();
    public FarmPlotSnapshotDtoV3[] FarmPlots{get;init;}=Array.Empty<FarmPlotSnapshotDtoV3>();
    public EquipmentSnapshotDtoV3[] Equipment{get;init;}=Array.Empty<EquipmentSnapshotDtoV3>();
}

public static class RegionSnapshotProtocolV3
{
    public const int MaximumSnapshotPayloadBytes=262144;
    internal static readonly JsonSerializerOptions JsonOptions=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
    public static byte[] Serialize(InitialRegionSnapshotPayloadV3 payload)=>JsonSerializer.SerializeToUtf8Bytes(payload,JsonOptions);
    public static bool TryDeserialize(string payload,out InitialRegionSnapshotPayloadV3? snapshot)
    {
        snapshot=null;
        if(string.IsNullOrEmpty(payload)||System.Text.Encoding.UTF8.GetByteCount(payload)>MaximumSnapshotPayloadBytes)return false;
        try{snapshot=JsonSerializer.Deserialize<InitialRegionSnapshotPayloadV3>(payload,JsonOptions);return snapshot!=null;}
        catch(JsonException){return false;}
    }
}
