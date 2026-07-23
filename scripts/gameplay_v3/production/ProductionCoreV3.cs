using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GameplayV3.Construction;
using GameplayV3.Equipment;
using GameplayV3.Resources;
using GameplayV3.Stockpile;
using WorldV2;

namespace GameplayV3.Production;

public enum ProductionOrderStateV3 { Queued, WaitingMaterials, Ready, Producing, OutputBlocked, Completed, Cancelled, Failed }
public enum ProductionChangeReasonV3 { FacilityRegistered, FacilityRemoved, OrderAdded, OrderChanged, OrderCancelled, MaterialDelivered, WorkStarted, BatchCompleted, OutputBlocked, OutputReleased, SessionReset }
public readonly record struct ProductionCompletionWorkerV3(string MercenaryId,string CompanyId,int ProductionSkill,long SessionRevision,string WorkRequestId);
public sealed record ProductionOrderSnapshotV3(string OrderId,string FacilityId,string RecipeId,int RequestedBatches,int CompletedBatches,int RemainingBatches,ProductionOrderStateV3 State,float WorkProgressSeconds,string? AssignedMercenaryId,int? AssignedProductionSkill,long Revision,string LastFailureReason);
public sealed record ProductionFacilitySnapshotV3(string FacilityId,string CompanyId,string StructureId,ProductionFacilityKindV3 FacilityKind,GlobalCellCoord AnchorCell,IReadOnlyList<ProductionOrderSnapshotV3> Queue,IReadOnlyList<StructureMaterialRequirementV3> MaterialBuffer,IReadOnlyDictionary<ResourceTypeV3,int> OutputBuffer,IReadOnlyList<string> EquipmentOutputInstanceIds,int EquipmentOutputCapacity,long Revision,string LastChangedReason);
public sealed record ProductionEventV3(string FacilityId,string? OrderId,ProductionChangeReasonV3 Reason,long Revision);
public sealed record ProductionCheckpointFacilityV3(
    string FacilityId,long EquipmentCompletionSequence,string LastCommittedBatchToken,long Revision,string LastChangedReason,
    IReadOnlyList<ProductionOrderSnapshotV3> Queue,IReadOnlyList<StructureMaterialRequirementV3> DeliveredMaterials,
    IReadOnlyDictionary<ResourceTypeV3,int> OutputBuffer);
public sealed class ProductionDiagnosticsV3
{public int FullFacilityScanCount{get;internal set;}public int FullResourceScanCount{get;internal set;}public int FullMercenaryScanCount{get;internal set;}public int ReservationLeakCount{get;internal set;}public int DirectProductionReservationLeakCount{get;internal set;}public int ConservationMismatchCount{get;internal set;}public int DuplicateJobCount{get;internal set;}public int DuplicateDirectOrderCount{get;internal set;}public int ProductionPriorityMissingCount{get;internal set;}public int StaleSessionProductionWorkCount{get;internal set;}public int DirtyFacilityCount{get;internal set;}public int FacilityCount{get;internal set;}public int QueuedOrderCount{get;internal set;}public int ActiveOrderCount{get;internal set;}public int WaitingMaterialCount{get;internal set;}public int OutputBlockedCount{get;internal set;}}

internal sealed class ProductionOrderStateDataV3
{
    public ProductionOrderStateDataV3(string id,string facility,string recipe,int batches,long order){OrderId=id;FacilityId=facility;RecipeId=recipe;RequestedBatches=batches;CreatedOrder=order;}
    public string OrderId{get;}public string FacilityId{get;}public string RecipeId{get;}public int RequestedBatches{get;set;}public int CompletedBatches{get;set;}public int RemainingBatches=>Math.Max(0,RequestedBatches-CompletedBatches);public ProductionOrderStateV3 State{get;set;}=ProductionOrderStateV3.Queued;public float WorkProgressSeconds{get;set;}public string? AssignedMercenaryId{get;set;}public int? AssignedProductionSkill{get;set;}public long Revision{get;set;}public long CreatedOrder{get;}public string LastFailureReason{get;set;}=string.Empty;
    public ProductionOrderSnapshotV3 Snapshot()=>new(OrderId,FacilityId,RecipeId,RequestedBatches,CompletedBatches,RemainingBatches,State,WorkProgressSeconds,AssignedMercenaryId,AssignedProductionSkill,Revision,LastFailureReason);
}
internal sealed class ProductionFacilityStateDataV3
{
    public ProductionFacilityStateDataV3(StructureStateV3 structure,ProductionFacilityKindV3 kind){FacilityId=structure.StructureId;StructureId=structure.StructureId;CompanyId=structure.CompanyId;FacilityKind=kind;AnchorCell=structure.AnchorCell;}
    public string FacilityId{get;}public string StructureId{get;}public string CompanyId{get;}public ProductionFacilityKindV3 FacilityKind{get;}public GlobalCellCoord AnchorCell{get;}public List<ProductionOrderStateDataV3> Queue{get;}=new();public ConstructionMaterialBufferV3? MaterialBuffer{get;set;}public Dictionary<ResourceTypeV3,int> OutputBuffer{get;}=new();public long EquipmentCompletionSequence{get;set;}public string LastCommittedBatchToken{get;set;}=string.Empty;public long Revision{get;set;}public string LastChangedReason{get;set;}=string.Empty;
}

public sealed class ProductionSessionV3:IDisposable
{
    public const int MaxQueueRows=20,MaxBatchesPerOrder=20,MaxRemainingBatchesPerFacility=100;
    private readonly ConstructionSessionV3 _construction;private readonly ResourceSessionV3 _resources;private readonly StockpileSessionV3 _stockpiles;private readonly Dictionary<string,ProductionFacilityStateDataV3> _facilities=new(StringComparer.Ordinal);private readonly Dictionary<string,ProductionOrderStateDataV3> _orders=new(StringComparer.Ordinal);private readonly Queue<ProductionEventV3> _recent=new();private EquipmentRuntimeV3? _equipment;private string? _equipmentRegionId;private int _qualitySeed;private long _idSequence,_revision;private bool _disposed;
    public ProductionSessionV3(long sessionRevision,ConstructionSessionV3 construction,ResourceSessionV3 resources,StockpileSessionV3 stockpiles){SessionRevision=sessionRevision;_construction=construction;_resources=resources;_stockpiles=stockpiles;construction.Structures.StructureRegistered+=OnStructureRegistered;construction.Structures.StructureRemoved+=OnStructureRemoved;foreach(string id in construction.Structures.GetAllStructureIds())if(construction.Structures.TryGet(id,out var structure)&&structure!=null)OnStructureRegistered(structure);}
    public long SessionRevision{get;private set;}public bool IsDisposed=>_disposed;public long Revision=>_revision;public ProductionDiagnosticsV3 Diagnostics{get;}=new();public event Action<ProductionEventV3>? Changed;public event Action<ProductionEventV3>? FacilityRemoved;
    public void RebindSessionRevision(long sessionRevision){if(_disposed)throw new ObjectDisposedException(nameof(ProductionSessionV3));if(sessionRevision<1)throw new ArgumentOutOfRangeException(nameof(sessionRevision));SessionRevision=sessionRevision;}
    public void PrepareForDeactivate()
    {
        if(_disposed)return;
        foreach(ProductionFacilityStateDataV3 facility in _facilities.Values)
        {
            if(facility.Queue.Count==0)continue;
            ProductionOrderStateDataV3 order=facility.Queue[0];
            if(order.State!=ProductionOrderStateV3.Producing)continue;
            order.AssignedMercenaryId=null;
            order.AssignedProductionSkill=null;
            order.State=facility.MaterialBuffer?.IsComplete==true?ProductionOrderStateV3.Ready:ProductionOrderStateV3.WaitingMaterials;
            order.LastFailureReason=string.Empty;
            order.Revision++;
            Touch(facility,order,ProductionChangeReasonV3.SessionReset);
        }
    }
    public bool AttachEquipmentRuntime(EquipmentRuntimeV3 equipment,int deterministicSeed,out string reason){if(_disposed||equipment.IsDisposed||equipment.SessionRevision!=SessionRevision){reason="InvalidEquipmentSession";return false;}if(_equipment!=null&&!ReferenceEquals(_equipment,equipment)){reason="EquipmentRuntimeAlreadyAttached";return false;}_equipment=equipment;_qualitySeed=deterministicSeed;reason=string.Empty;return true;}
    public void BindEquipmentRegion(string regionId){if(string.IsNullOrWhiteSpace(regionId))throw new ArgumentException("RegionId is required.",nameof(regionId));_equipmentRegionId=regionId;}
    public IReadOnlyList<ProductionRecipeDefinitionV3> GetAvailableRecipes(string facilityId)=>_facilities.TryGetValue(facilityId,out var f)?StarterProcessingContentV3.GetFor(f.FacilityKind):Array.Empty<ProductionRecipeDefinitionV3>();
    public bool TryGetRecipe(string id,out ProductionRecipeDefinitionV3? recipe)=>StarterProcessingContentV3.TryGet(id,out recipe);
    public IReadOnlyList<ProductionFacilitySnapshotV3> GetFacilities(string company)=>_facilities.Values.Where(x=>x.CompanyId==company).OrderBy(x=>x.FacilityId,StringComparer.Ordinal).Select(Snapshot).ToList().AsReadOnly();
    public bool TryGetFacility(string id,out ProductionFacilitySnapshotV3? snapshot){snapshot=null;if(!_facilities.TryGetValue(id,out var f))return false;snapshot=Snapshot(f);return true;}
    public bool TryGetOrder(string id,out ProductionOrderSnapshotV3? snapshot){snapshot=null;if(!_orders.TryGetValue(id,out var o))return false;snapshot=o.Snapshot();return true;}
    public IReadOnlyList<ProductionOrderSnapshotV3> GetQueue(string facilityId)=>_facilities.TryGetValue(facilityId,out var f)?f.Queue.Select(x=>x.Snapshot()).ToList().AsReadOnly():Array.Empty<ProductionOrderSnapshotV3>();
    public long NextOrderSequence=>_idSequence+1;
    public IReadOnlyList<ProductionCheckpointFacilityV3> GetCheckpointFacilities()
    {
        return _facilities.Values.OrderBy(x=>x.FacilityId,StringComparer.Ordinal).Select(f=>new ProductionCheckpointFacilityV3(
            f.FacilityId,f.EquipmentCompletionSequence,f.LastCommittedBatchToken,f.Revision,f.LastChangedReason,
            f.Queue.Select(x=>x.Snapshot()).ToList().AsReadOnly(),
            f.MaterialBuffer?.GetDeliveredMaterialsSnapshot()??Array.Empty<StructureMaterialRequirementV3>(),
            new ReadOnlyDictionary<ResourceTypeV3,int>(new Dictionary<ResourceTypeV3,int>(f.OutputBuffer)))).ToList().AsReadOnly();
    }
    internal bool TryRestoreCheckpointFacility(ProductionCheckpointFacilityV3 checkpoint,out string reason)
    {
        if(!_facilities.TryGetValue(checkpoint.FacilityId,out ProductionFacilityStateDataV3? facility)){reason="CheckpointFacilityMissing";return false;}
        if(facility.Queue.Count!=0||checkpoint.Queue.Select(x=>x.OrderId).Distinct(StringComparer.Ordinal).Count()!=checkpoint.Queue.Count){reason="DuplicateCheckpointOrder";return false;}
        foreach(ProductionOrderSnapshotV3 snapshot in checkpoint.Queue)
        {
            if(snapshot.FacilityId!=facility.FacilityId||_orders.ContainsKey(snapshot.OrderId)||!StarterProcessingContentV3.TryGet(snapshot.RecipeId,out ProductionRecipeDefinitionV3? recipe)||recipe==null||
               snapshot.RequestedBatches<1||snapshot.CompletedBatches<0||snapshot.CompletedBatches>snapshot.RequestedBatches){reason="InvalidCheckpointOrder";return false;}
            ProductionOrderStateDataV3 order=new(snapshot.OrderId,snapshot.FacilityId,snapshot.RecipeId,snapshot.RequestedBatches,++_idSequence)
            {CompletedBatches=snapshot.CompletedBatches,WorkProgressSeconds=snapshot.WorkProgressSeconds,Revision=snapshot.Revision,LastFailureReason=snapshot.LastFailureReason};
            order.State=snapshot.State==ProductionOrderStateV3.Producing?ProductionOrderStateV3.WaitingMaterials:snapshot.State;
            order.AssignedMercenaryId=null;order.AssignedProductionSkill=null;facility.Queue.Add(order);_orders.Add(order.OrderId,order);
        }
        if(facility.Queue.Count>0)
        {
            ProductionRecipeDefinitionV3 recipe=StarterProcessingContentV3.TryGet(facility.Queue[0].RecipeId,out ProductionRecipeDefinitionV3? found)&&found!=null?found:throw new InvalidOperationException();
            facility.MaterialBuffer=new ConstructionMaterialBufferV3(recipe.Inputs);
            foreach(StructureMaterialRequirementV3 item in checkpoint.DeliveredMaterials)if(!facility.MaterialBuffer.TryDeliver(item.ResourceType,item.RequiredAmount,out reason))return false;
            ProductionOrderStateDataV3 head=facility.Queue[0];head.State=facility.MaterialBuffer.IsComplete?ProductionOrderStateV3.Ready:ProductionOrderStateV3.WaitingMaterials;
        }
        facility.OutputBuffer.Clear();foreach(var pair in checkpoint.OutputBuffer){if(pair.Value<1){reason="InvalidCheckpointOutput";return false;}facility.OutputBuffer.Add(pair.Key,pair.Value);}
        facility.EquipmentCompletionSequence=checkpoint.EquipmentCompletionSequence;facility.LastCommittedBatchToken=checkpoint.LastCommittedBatchToken;
        facility.Revision=checkpoint.Revision;facility.LastChangedReason=checkpoint.LastChangedReason;reason=string.Empty;return true;
    }
    internal void RestoreNextOrderSequence(long nextSequence){if(nextSequence<1)throw new ArgumentOutOfRangeException(nameof(nextSequence));_idSequence=Math.Max(_idSequence,nextSequence-1);}
    public int GetEquipmentOutputCount(string facilityId)=>_equipment==null?0:_equipmentRegionId==null?_equipment.GetEquipmentOutputCount(facilityId):_equipment.GetEquipmentOutputCount(_equipmentRegionId,facilityId);
    public int GetEquipmentOutputCapacity(string facilityId)=>_equipment==null?0:_equipmentRegionId==null?_equipment.GetEquipmentOutputCapacity(facilityId):_equipment.GetEquipmentOutputCapacity(_equipmentRegionId,facilityId);
    public bool HasEquipmentOutputCapacity(string facilityId)=>GetEquipmentOutputCount(facilityId)<GetEquipmentOutputCapacity(facilityId);
    public IReadOnlyList<string> GetEquipmentOutputInstanceIds(string facilityId)=>_equipment==null?Array.Empty<string>():_equipmentRegionId==null?_equipment.GetEquipmentOutputInstanceIds(facilityId):_equipment.GetEquipmentOutputInstanceIds(_equipmentRegionId,facilityId);
    public bool TryRemoveEquipmentOutput(string facilityId,string instanceId,out EquipmentInstanceV3? instance,out string reason){instance=null;if(_equipment==null||!_equipment.TryRemoveEquipmentOutput(facilityId,instanceId,out instance)){reason="InvalidEquipmentOutput";return false;}if(_facilities.TryGetValue(facilityId,out var f)&&f.Queue.Count>0&&f.Queue[0].State==ProductionOrderStateV3.OutputBlocked&&f.Queue[0].LastFailureReason=="EquipmentOutputBufferFull"){var o=f.Queue[0];o.State=ProductionOrderStateV3.Ready;o.LastFailureReason=string.Empty;o.Revision++;Touch(f,o,ProductionChangeReasonV3.OutputReleased);}reason=string.Empty;return true;}
    public bool TryEjectNextEquipmentOutput(string facilityId,Func<GlobalCellCoord?> findOutputCell,out string? instanceId)
    {
        instanceId=null;if(_equipment==null||!_facilities.TryGetValue(facilityId,out var facility))return false;IReadOnlyList<string> ids=_equipment.GetEquipmentOutputInstanceIds(facilityId);if(ids.Count==0)return false;GlobalCellCoord? cell=findOutputCell();if(cell==null)return false;string id=ids[0];if(!_equipment.TryMoveFacilityOutputToGround(facilityId,id,cell.Value.Value,out _))return false;instanceId=id;Touch(facility,null,ProductionChangeReasonV3.OutputReleased);if(facility.Queue.Count>0&&facility.Queue[0].State==ProductionOrderStateV3.OutputBlocked&&facility.Queue[0].LastFailureReason=="EquipmentOutputBufferFull"){var order=facility.Queue[0];order.State=facility.MaterialBuffer?.IsComplete==true?ProductionOrderStateV3.Ready:ProductionOrderStateV3.WaitingMaterials;order.LastFailureReason=string.Empty;order.Revision++;Touch(facility,order,ProductionChangeReasonV3.OutputReleased);}return true;
    }
    public int GetResourceAvailability(string company,ResourceTypeV3 type){int total=0;foreach(string id in _resources.GroundStacks.GetStackIdsByType(type))if(_resources.GroundStacks.TryGet(id,out var stack)&&stack!=null&&_stockpiles.Zones.IsOwnedStockpileCell(company,stack.Cell))total+=_resources.AmountReservations.GetAvailableAmount(id);return total;}
    public bool TryAddOrder(string company,string facilityId,string recipeId,int batches,out string result)
    {result=string.Empty;if(!ValidateOrder(company,facilityId,recipeId,batches,out var facility,out var recipe,out result))return false;if(facility!.Queue.Sum(x=>x.RemainingBatches)+batches>MaxRemainingBatchesPerFacility){result="OrderBatchLimitExceeded";return false;}ProductionOrderStateDataV3? last=facility.Queue.LastOrDefault();if(last!=null&&last.RecipeId==recipeId&&last.State is ProductionOrderStateV3.Queued or ProductionOrderStateV3.WaitingMaterials&&last.RequestedBatches+batches<=MaxBatchesPerOrder){last.RequestedBatches+=batches;last.Revision++;Touch(facility,last,ProductionChangeReasonV3.OrderChanged);result=last.OrderId;return true;}if(facility.Queue.Count>=MaxQueueRows){result="OrderQueueLimitExceeded";return false;}string id=$"prod_{SessionRevision:x}_{++_idSequence:x}";ProductionOrderStateDataV3 order=new(id,facilityId,recipeId,batches,_idSequence);facility.Queue.Add(order);_orders.Add(id,order);ActivateHead(facility);Touch(facility,order,ProductionChangeReasonV3.OrderAdded);result=id;return true;}
    public bool TryCancelOrder(string company,string facilityId,string orderId,out string result)
    {result=string.Empty;if(!_facilities.TryGetValue(facilityId,out var f)||f.CompanyId!=company||!_orders.TryGetValue(orderId,out var o)||o.FacilityId!=facilityId){result="InvalidOrder";return false;}if(o.State==ProductionOrderStateV3.Producing){result="CurrentBatchProducing";return false;}bool wasHead=ReferenceEquals(o,f.Queue.FirstOrDefault());o.State=ProductionOrderStateV3.Cancelled;o.Revision++;f.Queue.Remove(o);_orders.Remove(orderId);if(wasHead)RefundBuffer(f);ActivateHead(f);Touch(f,o,ProductionChangeReasonV3.OrderCancelled);result="Cancelled";return true;}
    public bool TryIncreaseOrder(string company,string orderId,int batches,out string result){result=string.Empty;if(!_orders.TryGetValue(orderId,out var o)||!_facilities.TryGetValue(o.FacilityId,out var f)||f.CompanyId!=company||batches<1||o.RequestedBatches+batches>MaxBatchesPerOrder||f.Queue.Sum(x=>x.RemainingBatches)+batches>MaxRemainingBatchesPerFacility){result="InvalidOrderChange";return false;}o.RequestedBatches+=batches;o.Revision++;Touch(f,o,ProductionChangeReasonV3.OrderChanged);result="Changed";return true;}
    public bool TryDecreaseOrder(string company,string orderId,int batches,out string result){result=string.Empty;if(!_orders.TryGetValue(orderId,out var o)||!_facilities.TryGetValue(o.FacilityId,out var f)||f.CompanyId!=company||batches<1||o.RequestedBatches-batches<Math.Max(1,o.CompletedBatches+1)){result="InvalidOrderChange";return false;}o.RequestedBatches-=batches;o.Revision++;Touch(f,o,ProductionChangeReasonV3.OrderChanged);result="Changed";return true;}
    public bool TryDeliverMaterial(string facilityId,ResourceTypeV3 type,int amount,out string reason){reason="InvalidFacility";if(!_facilities.TryGetValue(facilityId,out var f)||f.MaterialBuffer==null)return false;if(!f.MaterialBuffer.TryDeliver(type,amount,out reason))return false;ActivateHead(f);Touch(f,f.Queue.First(),ProductionChangeReasonV3.MaterialDelivered);return true;}
    public bool CanDispatch(string facilityId)=>_facilities.TryGetValue(facilityId,out var f)&&f.Queue.Count>0&&f.Queue[0].State is ProductionOrderStateV3.WaitingMaterials or ProductionOrderStateV3.Ready;
    public bool TryBeginWork(string facilityId,string mercenaryId,out ProductionOrderSnapshotV3? snapshot,out string reason)=>TryBeginWork(facilityId,mercenaryId,null,out snapshot,out reason);
    public bool TryBeginWork(string facilityId,string mercenaryId,int? productionSkill,out ProductionOrderSnapshotV3? snapshot,out string reason){snapshot=null;reason="InvalidFacility";if(!_facilities.TryGetValue(facilityId,out var f)||f.Queue.Count==0||f.MaterialBuffer?.IsComplete!=true)return false;var o=f.Queue[0];if(o.State is not (ProductionOrderStateV3.Ready or ProductionOrderStateV3.Producing)){reason="OrderNotReady";return false;}if(StarterProcessingContentV3.TryGet(o.RecipeId,out var recipe)&&recipe?.OutputKind==ProductionOutputKindV3.Equipment&&!HasEquipmentOutputCapacity(facilityId)){o.State=ProductionOrderStateV3.OutputBlocked;o.LastFailureReason="EquipmentOutputBufferFull";Touch(f,o,ProductionChangeReasonV3.OutputBlocked);reason="EquipmentOutputBufferFull";return false;}o.State=ProductionOrderStateV3.Producing;o.AssignedMercenaryId=mercenaryId;o.AssignedProductionSkill=productionSkill;o.LastFailureReason=string.Empty;o.Revision++;Touch(f,o,ProductionChangeReasonV3.WorkStarted);snapshot=o.Snapshot();reason=string.Empty;return true;}
    public bool TryAdvanceWork(string facilityId,float seconds,Func<GlobalCellCoord?> outputCell,out bool completed,out string reason)=>TryAdvanceWorkCore(facilityId,seconds,outputCell,null,out completed,out reason);
    public bool TryAdvanceWork(string facilityId,float seconds,Func<GlobalCellCoord?> outputCell,ProductionCompletionWorkerV3 completionWorker,out bool completed,out string reason)=>TryAdvanceWorkCore(facilityId,seconds,outputCell,completionWorker,out completed,out reason);
    private bool TryAdvanceWorkCore(string facilityId,float seconds,Func<GlobalCellCoord?> outputCell,ProductionCompletionWorkerV3? completionWorker,out bool completed,out string reason)
    {
        completed=false;reason="InvalidFacility";
        if(!_facilities.TryGetValue(facilityId,out var f)||f.Queue.Count==0)return false;
        var o=f.Queue[0];
        if(o.State!=ProductionOrderStateV3.Producing||!StarterProcessingContentV3.TryGet(o.RecipeId,out var recipe)||recipe==null){reason="OrderNotProducing";return false;}
        if(seconds<=0){reason=string.Empty;return true;}
        o.WorkProgressSeconds+=seconds;o.Revision++;
        if(o.WorkProgressSeconds+0.0001f<recipe.BaseWorkSeconds){f.Revision++;reason=string.Empty;return true;}
        if(recipe.OutputKind==ProductionOutputKindV3.Equipment)return TryCommitEquipmentBatch(f,o,recipe,completionWorker,out completed,out reason);
        if(recipe.OutputResource==null){reason="InvalidResourceOutput";return false;}
        f.MaterialBuffer!.TryWithdrawAll(out var consumed);
        int consumedTotal=consumed.Sum(x=>x.RequiredAmount);
        if(consumedTotal!=recipe.Inputs.Sum(x=>x.RequiredAmount))Diagnostics.ConservationMismatchCount++;
        ResourceTypeV3 output=recipe.OutputResource.Value;GlobalCellCoord? cell=outputCell();
        if(cell!=null)_resources.GroundStacks.TryAddOrMerge(output,recipe.OutputAmount,cell.Value,out _,out _,out _);
        else{f.OutputBuffer[output]=f.OutputBuffer.GetValueOrDefault(output)+recipe.OutputAmount;o.State=ProductionOrderStateV3.OutputBlocked;o.LastFailureReason="ResourceOutputBlocked";Diagnostics.OutputBlockedCount++;Touch(f,o,ProductionChangeReasonV3.OutputBlocked);reason="OutputBlocked";return true;}
        FinishBatch(f,o);completed=true;reason=string.Empty;return true;
    }
    private bool TryCommitEquipmentBatch(ProductionFacilityStateDataV3 f,ProductionOrderStateDataV3 o,ProductionRecipeDefinitionV3 recipe,ProductionCompletionWorkerV3? completionWorker,out bool completed,out string reason)
    {
        completed=false;
        if(_equipment==null||_equipment.IsDisposed||_equipment.SessionRevision!=SessionRevision){reason="InvalidEquipmentSession";return false;}
        if(recipe.OutputEquipmentDefinitionId is not { Length:>0 } definitionId||!_equipment.Definitions.Contains(definitionId)){reason="UnknownEquipmentDefinition";return false;}
        if(completionWorker is not { } worker||worker.SessionRevision!=SessionRevision||worker.CompanyId!=f.CompanyId||worker.MercenaryId!=o.AssignedMercenaryId||string.IsNullOrWhiteSpace(worker.WorkRequestId)){reason="InvalidCompletionWorker";return false;}
        string batchToken=f.FacilityId+"|"+o.OrderId+"|"+o.CompletedBatches+"|"+worker.WorkRequestId;
        if(f.LastCommittedBatchToken==batchToken){reason="DuplicateBatchCompletion";return false;}
        if(f.MaterialBuffer?.IsComplete!=true){reason="WaitingMaterials";return false;}
        if(!_equipment.HasEquipmentOutputCapacity(f.FacilityId)){o.State=ProductionOrderStateV3.OutputBlocked;o.LastFailureReason="EquipmentOutputBufferFull";Touch(f,o,ProductionChangeReasonV3.OutputBlocked);reason="EquipmentOutputBufferFull";return true;}
        long nextSequence=f.EquipmentCompletionSequence+1;
        double variation=(HashToUniform(f.FacilityId,recipe.RecipeId,nextSequence,0x51ed2705)+HashToUniform(f.FacilityId,recipe.RecipeId,nextSequence,0x9e3779b9))/2.0;
        EquipmentQualityResolutionV3 quality=EquipmentQualityResolverV3.Resolve(worker.ProductionSkill,variation);
        if(!_equipment.TryCreateInstanceInFacilityOutput(definitionId,quality.Quality,quality.QualityScore,worker.MercenaryId,quality.ProductionSkill,f.CompanyId,f.FacilityId,SessionRevision,out EquipmentInstanceV3? instance,out reason)||instance==null)return false;
        if(!f.MaterialBuffer.TryWithdrawAll(out var consumed)||!MaterialsMatch(recipe.Inputs,consumed))
        {
            foreach(var item in consumed)f.MaterialBuffer.TryDeliver(item.ResourceType,item.RequiredAmount,out _);
            _equipment.RemoveInstance(instance.EquipmentInstanceId);
            Diagnostics.ConservationMismatchCount++;
            reason="MaterialCommitFailed";
            return false;
        }
        f.EquipmentCompletionSequence=nextSequence;
        f.LastCommittedBatchToken=batchToken;
        FinishBatch(f,o);
        completed=true;reason=string.Empty;return true;
    }
    private static bool MaterialsMatch(IReadOnlyList<StructureMaterialRequirementV3> required,IReadOnlyList<StructureMaterialRequirementV3> consumed)
    {
        if(required.Count!=consumed.Count)return false;
        foreach(var item in required)if(consumed.FirstOrDefault(x=>x.ResourceType==item.ResourceType).RequiredAmount!=item.RequiredAmount)return false;
        return true;
    }
    private double HashToUniform(string facilityId,string recipeId,long sequence,uint salt)
    {
        ulong hash=14695981039346656037UL;
        Mix(_qualitySeed.ToString(System.Globalization.CultureInfo.InvariantCulture));Mix("|");Mix(SessionRevision.ToString(System.Globalization.CultureInfo.InvariantCulture));Mix("|");Mix(facilityId);Mix("|");Mix(recipeId);Mix("|");Mix(sequence.ToString(System.Globalization.CultureInfo.InvariantCulture));Mix("|");Mix(salt.ToString(System.Globalization.CultureInfo.InvariantCulture));
        double unit=(hash>>11)*(1.0/9007199254740992.0);
        return EquipmentQualityResolverV3.MinimumVariationRoll+unit*(EquipmentQualityResolverV3.MaximumVariationRoll-EquipmentQualityResolverV3.MinimumVariationRoll);
        void Mix(string value){foreach(char character in value){hash^=character;hash*=1099511628211UL;}}
    }
    private void FinishBatch(ProductionFacilityStateDataV3 f,ProductionOrderStateDataV3 o)
    {
        o.CompletedBatches++;o.WorkProgressSeconds=0;o.AssignedMercenaryId=null;o.AssignedProductionSkill=null;o.LastFailureReason=string.Empty;
        if(o.RemainingBatches==0){o.State=ProductionOrderStateV3.Completed;f.Queue.RemoveAt(0);_orders.Remove(o.OrderId);}
        ActivateHead(f);Touch(f,o,ProductionChangeReasonV3.BatchCompleted);
    }
    public bool TryFlushOutput(string facilityId,Func<GlobalCellCoord?> outputCell){if(!_facilities.TryGetValue(facilityId,out var f)||f.OutputBuffer.Count==0)return false;GlobalCellCoord? cell=outputCell();if(cell==null)return false;foreach(var pair in f.OutputBuffer.ToList())_resources.GroundStacks.TryAddOrMerge(pair.Key,pair.Value,cell.Value,out _,out _,out _);f.OutputBuffer.Clear();if(f.Queue.Count>0&&f.Queue[0].State==ProductionOrderStateV3.OutputBlocked){var o=f.Queue[0];o.CompletedBatches++;o.WorkProgressSeconds=0;o.AssignedMercenaryId=null;if(o.RemainingBatches==0){o.State=ProductionOrderStateV3.Completed;f.Queue.RemoveAt(0);_orders.Remove(o.OrderId);}ActivateHead(f);Touch(f,o,ProductionChangeReasonV3.OutputReleased);}return true;}
    public IReadOnlyList<ProductionEventV3> GetRecentEvents()=>_recent.ToList().AsReadOnly();
    private bool ValidateOrder(string company,string facilityId,string recipeId,int batches,out ProductionFacilityStateDataV3? facility,out ProductionRecipeDefinitionV3? recipe,out string reason){facility=null;recipe=null;if(batches<1||batches>MaxBatchesPerOrder){reason="InvalidBatchCount";return false;}if(!_facilities.TryGetValue(facilityId,out facility)||facility.CompanyId!=company){reason="InvalidFacility";return false;}if(!StarterProcessingContentV3.TryGet(recipeId,out recipe)||recipe==null||recipe.FacilityKind!=facility.FacilityKind){reason="UnsupportedRecipe";return false;}if(recipe.OutputKind==ProductionOutputKindV3.Resource&&(recipe.OutputResource==null||recipe.Output.EquipmentDefinitionId!=null||recipe.Output.ResourceQuantity<1)){reason="InvalidResourceOutput";return false;}if(recipe.OutputKind==ProductionOutputKindV3.Equipment&&(_equipment==null||recipe.OutputEquipmentDefinitionId is not { Length:>0 } equipmentId||recipe.Output.EquipmentQuantity!=1||!_equipment.Definitions.Contains(equipmentId))){reason="InvalidEquipmentOutput";return false;}reason=string.Empty;return true;}
    private void ActivateHead(ProductionFacilityStateDataV3 f){if(f.Queue.Count==0){f.MaterialBuffer=null;return;}var o=f.Queue[0];if(o.State==ProductionOrderStateV3.OutputBlocked)return;if(!StarterProcessingContentV3.TryGet(o.RecipeId,out var recipe)||recipe==null){o.State=ProductionOrderStateV3.Failed;return;}f.MaterialBuffer??=new ConstructionMaterialBufferV3(recipe.Inputs);if(recipe.OutputKind==ProductionOutputKindV3.Equipment&&!HasEquipmentOutputCapacity(f.FacilityId)){o.State=ProductionOrderStateV3.OutputBlocked;o.LastFailureReason="EquipmentOutputBufferFull";}else{o.State=f.MaterialBuffer.IsComplete?ProductionOrderStateV3.Ready:ProductionOrderStateV3.WaitingMaterials;o.LastFailureReason=string.Empty;}o.Revision++;}
    private void OnStructureRegistered(StructureStateV3 structure){if(_disposed||!StarterProcessingContentV3.TryGetFacilityKind(structure.DefinitionId,out var kind)||_facilities.ContainsKey(structure.StructureId))return;var f=new ProductionFacilityStateDataV3(structure,kind);_facilities.Add(f.FacilityId,f);Diagnostics.FacilityCount=_facilities.Count;Touch(f,null,ProductionChangeReasonV3.FacilityRegistered);}
    private void OnStructureRemoved(StructureStateV3 structure)
    {
        if(!_facilities.Remove(structure.StructureId,out var f))return;
        RefundBuffer(f);
        foreach(var pair in f.OutputBuffer)_resources.GroundStacks.TryAddOrMerge(pair.Key,pair.Value,f.AnchorCell,out _,out _,out _);
        if(_equipment!=null)
        {
            foreach(string instanceId in _equipment.GetEquipmentOutputInstanceIds(f.FacilityId).ToList())
                if(!_equipment.TryMoveFacilityOutputToGround(f.FacilityId,instanceId,f.AnchorCell.Value,out _))
                    _equipment.TryMoveFacilityOutputToCompanyHolding(f.FacilityId,instanceId,out _);
        }
        foreach(var o in f.Queue)_orders.Remove(o.OrderId);
        Diagnostics.FacilityCount=_facilities.Count;
        var e=new ProductionEventV3(f.FacilityId,null,ProductionChangeReasonV3.FacilityRemoved,++_revision);
        Record(e);
        FacilityRemoved?.Invoke(e);
    }
    private void RefundBuffer(ProductionFacilityStateDataV3 f){if(f.MaterialBuffer==null)return;f.MaterialBuffer.TryWithdrawAll(out var items);foreach(var item in items)_resources.GroundStacks.TryAddOrMerge(item.ResourceType,item.RequiredAmount,f.AnchorCell,out _,out _,out _);f.MaterialBuffer=null;}
    private ProductionFacilitySnapshotV3 Snapshot(ProductionFacilityStateDataV3 f)=>new(f.FacilityId,f.CompanyId,f.StructureId,f.FacilityKind,f.AnchorCell,f.Queue.Select(x=>x.Snapshot()).ToList().AsReadOnly(),f.MaterialBuffer?.GetDeliveredMaterialsSnapshot()??Array.Empty<StructureMaterialRequirementV3>(),new ReadOnlyDictionary<ResourceTypeV3,int>(new Dictionary<ResourceTypeV3,int>(f.OutputBuffer)),GetEquipmentOutputInstanceIds(f.FacilityId),GetEquipmentOutputCapacity(f.FacilityId),f.Revision,f.LastChangedReason);
    private void Touch(ProductionFacilityStateDataV3 f,ProductionOrderStateDataV3? o,ProductionChangeReasonV3 reason){f.Revision++;f.LastChangedReason=reason.ToString();var e=new ProductionEventV3(f.FacilityId,o?.OrderId,reason,++_revision);Record(e);Changed?.Invoke(e);RefreshDiagnostics();}
    private void Record(ProductionEventV3 e){_recent.Enqueue(e);while(_recent.Count>16)_recent.Dequeue();}
    private void RefreshDiagnostics(){Diagnostics.FacilityCount=_facilities.Count;Diagnostics.QueuedOrderCount=_orders.Count;Diagnostics.ActiveOrderCount=_orders.Values.Count(x=>x.State==ProductionOrderStateV3.Producing);Diagnostics.WaitingMaterialCount=_orders.Values.Count(x=>x.State==ProductionOrderStateV3.WaitingMaterials);Diagnostics.OutputBlockedCount=_orders.Values.Count(x=>x.State==ProductionOrderStateV3.OutputBlocked);}
    public void Dispose(){if(_disposed)return;_disposed=true;_construction.Structures.StructureRegistered-=OnStructureRegistered;_construction.Structures.StructureRemoved-=OnStructureRemoved;_facilities.Clear();_orders.Clear();_recent.Clear();}
}
