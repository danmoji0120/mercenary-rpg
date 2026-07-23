using System;
using System.Collections.Generic;
using GameplayV3.Resources;
using GameplayV3.Stockpile;

namespace GameplayV3.Work;

public sealed record WorkToolDefinitionV3(ResourceTypeV3 ResourceType,string DisplayName,float SpeedMultiplier,string EffectText);
public sealed record WorkToolReservationSnapshotV3(string WorkRequestId,string MercenaryId,string StackId,ResourceTypeV3 ResourceType,string DisplayName,float SpeedMultiplier,string EffectText);

public sealed class WorkToolDiagnosticsV3
{
    public long ReservationGrantedTotal{get;internal set;}
    public long ReservationMissTotal{get;internal set;}
    public long ReservationReleasedTotal{get;internal set;}
    public long FullStackScanCount{get;internal set;}
    public long DuplicateReservationCount{get;internal set;}
    public long ReservationLeakCount{get;internal set;}
}

public sealed class WorkToolReservationSessionV3:IDisposable
{
    private static readonly WorkToolDefinitionV3 Axe=new(ResourceTypeV3.IronAxe,"\uCCA0 \uB3C4\uB07C",1.20f,"\uCC44\uC9D1 \uC18D\uB3C4 +20%");
    private static readonly WorkToolDefinitionV3 Pickaxe=new(ResourceTypeV3.IronPickaxe,"\uCCA0 \uACE1\uAD2D\uC774",1.20f,"\uAD11\uBB3C \uCC44\uC9D1 \uC18D\uB3C4 +20%");
    private static readonly WorkToolDefinitionV3 Hammer=new(ResourceTypeV3.IronHammer,"\uCCA0 \uB9DD\uCE58",1.15f,"\uAC74\uC124 \uC18D\uB3C4 +15%");
    private readonly ResourceSessionV3 _resources;private readonly StockpileSessionV3 _stockpiles;private readonly Dictionary<string,WorkToolReservationSnapshotV3> _byWork=new(StringComparer.Ordinal);private readonly Dictionary<string,string> _workByMercenary=new(StringComparer.Ordinal);private bool _disposed;
    public WorkToolReservationSessionV3(long sessionRevision,ResourceSessionV3 resources,StockpileSessionV3 stockpiles){SessionRevision=sessionRevision;_resources=resources;_stockpiles=stockpiles;}
    public long SessionRevision{get;}public int ActiveReservationCount=>_byWork.Count;public bool IsDisposed=>_disposed;public WorkToolDiagnosticsV3 Diagnostics{get;}=new();public event Action<string>? Changed;
    public static int DefinitionCount=>3;
    public bool TryReserveForGathering(string companyId,string mercenaryId,string workRequestId,ResourceNodeTypeV3 nodeType,out float multiplier)=>TryReserve(companyId,mercenaryId,workRequestId,GetGatheringTool(nodeType),out multiplier);
    public bool TryReserveForConstruction(string companyId,string mercenaryId,string workRequestId,out float multiplier)=>TryReserve(companyId,mercenaryId,workRequestId,Hammer,out multiplier);
    public bool TryGetForWork(string workRequestId,out WorkToolReservationSnapshotV3? snapshot)=>_byWork.TryGetValue(workRequestId,out snapshot);
    public bool TryGetForMercenary(string mercenaryId,out WorkToolReservationSnapshotV3? snapshot){snapshot=null;return _workByMercenary.TryGetValue(mercenaryId,out string? work)&&_byWork.TryGetValue(work,out snapshot);}
    public bool Release(string workRequestId){if(!_byWork.Remove(workRequestId,out var held))return false;_resources.AmountReservations.ReleaseByWorkRequest(workRequestId);_workByMercenary.Remove(held.MercenaryId);Diagnostics.ReservationReleasedTotal++;Changed?.Invoke(held.MercenaryId);return true;}
    public int ReleaseByMercenary(string mercenaryId){if(!_workByMercenary.TryGetValue(mercenaryId,out string? work))return 0;return Release(work)?1:0;}
    private bool TryReserve(string companyId,string mercenaryId,string workRequestId,WorkToolDefinitionV3? tool,out float multiplier)
    {
        multiplier=1f;if(_disposed||tool==null)return false;if(_byWork.TryGetValue(workRequestId,out var existing)){Diagnostics.DuplicateReservationCount++;multiplier=existing.SpeedMultiplier;return true;}
        foreach(string stackId in _resources.GroundStacks.GetStackIdsByType(tool.ResourceType))
        {
            if(!_resources.GroundStacks.TryGet(stackId,out var stack)||stack==null||!_stockpiles.Zones.IsOwnedStockpileCell(companyId,stack.Cell)||_resources.AmountReservations.GetAvailableAmount(stackId)<1)continue;
            if(!_resources.AmountReservations.TryReserve(stackId,mercenaryId,workRequestId,tool.ResourceType,1,ResourceAmountReservationPurposeV3.WorkTool,out _,out _))continue;
            var snapshot=new WorkToolReservationSnapshotV3(workRequestId,mercenaryId,stackId,tool.ResourceType,tool.DisplayName,tool.SpeedMultiplier,tool.EffectText);_byWork.Add(workRequestId,snapshot);_workByMercenary[mercenaryId]=workRequestId;Diagnostics.ReservationGrantedTotal++;multiplier=tool.SpeedMultiplier;Changed?.Invoke(mercenaryId);return true;
        }
        Diagnostics.ReservationMissTotal++;return false;
    }
    private static WorkToolDefinitionV3? GetGatheringTool(ResourceNodeTypeV3 type)=>type switch{ResourceNodeTypeV3.Tree=>Axe,ResourceNodeTypeV3.StoneOutcrop or ResourceNodeTypeV3.IronVein or ResourceNodeTypeV3.CopperVein or ResourceNodeTypeV3.CoalSeam or ResourceNodeTypeV3.ClayDeposit=>Pickaxe,_=>null};
    public void Dispose(){if(_disposed)return;foreach(string work in new List<string>(_byWork.Keys))Release(work);_disposed=true;}
}
