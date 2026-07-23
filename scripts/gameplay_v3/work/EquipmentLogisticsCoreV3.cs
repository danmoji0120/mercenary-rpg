using System;
using System.Collections.Generic;
using GameplayV3.Equipment;
using GameplayV3.Mercenary;
using GameplayV3.Navigation;
using GameplayV3.Stockpile;
using Godot;
using WorldV2;

namespace GameplayV3.Work;

public enum EquipmentLogisticsWorkKindV3 { Hauling = 1, Equip = 2 }
public enum EquipmentLogisticsPhaseV3 { WaitingForSourcePath, MovingToSource, WaitingForDestinationPath, MovingToDestination, Completed, Failed, Cancelled }

public sealed class EquipmentLogisticsExecutionV3
{
    internal EquipmentLogisticsExecutionV3(WorkRequestV3 request,string equipmentInstanceId,EquipmentLogisticsWorkKindV3 kind,GlobalCellCoord source,string? storageId,GlobalCellCoord? destination)
    {WorkRequestId=request.WorkRequestId;MercenaryId=request.AssignedMercenaryId;EquipmentInstanceId=equipmentInstanceId;Kind=kind;SourceCell=source;StorageId=storageId;DestinationCell=destination;Revision=request.Revision;}
    public string WorkRequestId{get;}public string MercenaryId{get;}public string EquipmentInstanceId{get;}public EquipmentLogisticsWorkKindV3 Kind{get;}public GlobalCellCoord SourceCell{get;}public string? StorageId{get;}public GlobalCellCoord? DestinationCell{get;}public EquipmentLogisticsPhaseV3 Phase{get;internal set;}=EquipmentLogisticsPhaseV3.WaitingForSourcePath;public long Revision{get;}
}

public sealed partial class MercenaryWorkSessionV3
{
    private EquipmentRuntimeV3? _equipment;
    private readonly Dictionary<string,EquipmentLogisticsExecutionV3> _equipmentExecutions=new(StringComparer.Ordinal);
    public void AttachEquipmentRuntime(EquipmentRuntimeV3 equipment){if(equipment.SessionRevision==SessionRevision&&!equipment.IsDisposed)_equipment=equipment;}
    public IReadOnlyList<EquipmentLogisticsExecutionV3> GetActiveEquipmentExecutions(){List<EquipmentLogisticsExecutionV3> values=new(_equipmentExecutions.Values);values.Sort((a,b)=>string.CompareOrdinal(a.WorkRequestId,b.WorkRequestId));return values.AsReadOnly();}
    public bool TryGetEquipmentExecution(string requestId,out EquipmentLogisticsExecutionV3? execution)=>_equipmentExecutions.TryGetValue(requestId,out execution);

    public bool TryIssueEquipmentHauling(string issuer,string companyId,IReadOnlyList<string> selected,string instanceId,IMercenaryNavigationWorldQueryV3 query,long currentSession,out WorkRequestV3? request,out string reason)
    {
        request=null;
        if(currentSession!=SessionRevision||_equipment==null||_equipment.SessionRevision!=SessionRevision){reason="InvalidSession";return false;}
        if(_companies.LocalPlayer?.PlayerId!=issuer||!_companies.CanPlayerControlCompany(issuer,companyId)){reason="OwnershipDenied";return false;}
        if(!_equipment.TryGetInstance(instanceId,out var instance)||instance==null){reason="EquipmentNotFound";return false;}
        if(instance.CreatedSessionRevision!=SessionRevision){reason="InvalidSession";return false;}
        if(instance.OwnerCompanyId!=companyId){reason="WrongOwner";return false;}
        if(instance.LocationKind!=EquipmentLocationKindV3.Ground||instance.GroundCell is not { } sourceCell||!_equipment.IsGroundEquipment(instanceId,sourceCell)){reason="InvalidLocation";return false;}
        GlobalCellCoord source=new(sourceCell);
        if(!HaulingWorkerSelectionServiceV3.TrySelect(selected,source,_mercenaries,issuer,Carries,out string worker,out reason))return false;
        if(!CanStartFor(worker,WorkTypeV3.Hauling,out reason))return false;
        if(!_mercenaries.Registry.TryGetState(worker,out MercenaryStateV3? workerState)||workerState==null||workerState.CompanyId!=companyId){reason="InvalidMercenary";return false;}
        (StockpileZoneStateV3 Zone,GlobalCellCoord Cell)? destination=FindEquipmentDestination(companyId,source,query);
        if(destination==null){reason="NoAllowedStorage";return false;}
        string? replaceable=_assignmentsByMercenary.TryGetValue(worker,out var oldAssignment)?oldAssignment.WorkRequestId:null;
        if(_equipment.TryGetReservation(instanceId,out var held)&&held?.WorkRequestId!=replaceable){reason="Reserved";return false;}
        NotifyExternalWorkSupersede(worker);if(oldAssignment!=null)Terminal(oldAssignment.WorkRequestId,WorkRequestStatusV3.Superseded,"SupersededByNewWork");
        long revision=++_revision;DateTime now=DateTime.UtcNow;
        request=new(WorkRequestIdFactoryV3.Create(),WorkTypeV3.Hauling,WorkTargetKindV3.GroundEquipment,companyId,instanceId,source,worker,revision,now);
        if(!_equipment.TryReserve(instanceId,request.WorkRequestId,worker,EquipmentReservationPurposeV3.Hauling,SessionRevision,out reason)){request=null;return false;}
        StockpileCellReservationV3 cellReservation=new(destination.Value.Zone.StockpileZoneId,destination.Value.Cell,request.WorkRequestId,worker,instanceId,revision,now);
        if(!Stockpiles.CellReservations.TryReserve(cellReservation,out reason)){_equipment.ReleaseReservation(instanceId,request.WorkRequestId);request=null;return false;}
        _control.SupersedeDirectMovementForWork(worker);MercenaryWorkAssignmentV3 assignment=new(WorkAssignmentIdFactoryV3.Create(),request,now);EquipmentLogisticsExecutionV3 execution=new(request,instanceId,EquipmentLogisticsWorkKindV3.Hauling,source,destination.Value.Zone.StockpileZoneId,destination.Value.Cell);
        request.Status=WorkRequestStatusV3.Assigned;_requests.Add(request.WorkRequestId,request);_assignmentsByMercenary.Add(worker,assignment);_equipmentExecutions.Add(request.WorkRequestId,execution);Diagnostics.LastWorkRequestId=request.WorkRequestId;reason=string.Empty;return true;
    }

    public bool TryIssueDirectEquipmentEquip(string issuer,string companyId,string mercenaryId,string instanceId,IMercenaryNavigationWorldQueryV3 query,long currentSession,out WorkRequestV3? request,out string reason)
    {
        request=null;
        if(currentSession!=SessionRevision||_equipment==null||_equipment.SessionRevision!=SessionRevision||_equipmentLoadouts==null){reason="InvalidSession";return false;}
        if(_companies.LocalPlayer?.PlayerId!=issuer||!_companies.CanPlayerControlCompany(issuer,companyId)||!_mercenaries.CanPlayerControlMercenary(issuer,mercenaryId)){reason="OwnershipDenied";return false;}
        if(!_mercenaries.Registry.TryGetState(mercenaryId,out MercenaryStateV3? worker)||worker==null||worker.CompanyId!=companyId){reason="InvalidMercenary";return false;}
        if(!_equipment.TryGetInstance(instanceId,out var instance)||instance==null){reason="EquipmentNotFound";return false;}
        if(instance.CreatedSessionRevision!=SessionRevision){reason="InvalidSession";return false;}
        if(instance.OwnerCompanyId!=companyId){reason="WrongOwner";return false;}
        Vector2I? sourceCell=instance.LocationKind switch{EquipmentLocationKindV3.Ground=>instance.GroundCell,EquipmentLocationKindV3.Storage=>instance.StorageCell,_=>null};
        if(sourceCell==null){reason="InvalidLocation";return false;}
        if(_equipment.TryGetReservation(instanceId,out var held)){reason="Reserved";return false;}
        if(!query.IsInsideWorld(sourceCell.Value)){reason="NoPath";return false;}
        if(_assignmentsByMercenary.TryGetValue(mercenaryId,out var old))Terminal(old.WorkRequestId,WorkRequestStatusV3.Superseded,"SupersededByDirectEquip");
        NotifyExternalWorkSupersede(mercenaryId);long revision=++_revision;DateTime now=DateTime.UtcNow;GlobalCellCoord source=new(sourceCell.Value);
        request=new(WorkRequestIdFactoryV3.Create(),WorkTypeV3.Hauling,instance.LocationKind==EquipmentLocationKindV3.Ground?WorkTargetKindV3.GroundEquipment:WorkTargetKindV3.StoredEquipment,companyId,instanceId,source,mercenaryId,revision,now);
        if(!_equipment.TryReserve(instanceId,request.WorkRequestId,mercenaryId,EquipmentReservationPurposeV3.Equip,SessionRevision,out reason)){request=null;return false;}
        _control.SupersedeDirectMovementForWork(mercenaryId);MercenaryWorkAssignmentV3 assignment=new(WorkAssignmentIdFactoryV3.Create(),request,now);EquipmentLogisticsExecutionV3 execution=new(request,instanceId,EquipmentLogisticsWorkKindV3.Equip,source,null,null);
        request.Status=WorkRequestStatusV3.Assigned;_requests.Add(request.WorkRequestId,request);_assignmentsByMercenary.Add(mercenaryId,assignment);_equipmentExecutions.Add(request.WorkRequestId,execution);Diagnostics.LastWorkRequestId=request.WorkRequestId;reason=string.Empty;return true;
    }

    private (StockpileZoneStateV3 Zone,GlobalCellCoord Cell)? FindEquipmentDestination(string companyId,GlobalCellCoord source,IMercenaryNavigationWorldQueryV3 query)
    {
        (StockpileZoneStateV3 Zone,GlobalCellCoord Cell,float Distance)? best=null;int checkedCells=0;
        foreach(StockpileZoneStateV3 zone in Stockpiles.Zones.GetZonesByCompany(companyId))
        {
            if(!zone.IsEnabled||!zone.AllowsEquipment)continue;
            foreach(GlobalCellCoord cell in zone.Cells)
            {
                if(++checkedCells>256)break;
                if(!query.IsInsideWorld(cell.Value)||!query.IsWalkable(cell.Value)||Stockpiles.CellReservations.IsReserved(cell))continue;
                float distance=MercenaryMovementCostPolicyV3.Octile(source.Value,cell.Value);
                if(best==null||distance<best.Value.Distance||(Math.Abs(distance-best.Value.Distance)<.001f&&(cell.Value.Y<best.Value.Cell.Value.Y||(cell.Value.Y==best.Value.Cell.Value.Y&&cell.Value.X<best.Value.Cell.Value.X))))best=(zone,cell,distance);
            }
            if(checkedCells>256)break;
        }
        return best is { } value?(value.Zone,value.Cell):null;
    }

    private void CleanupEquipmentTerminal(WorkRequestV3 request)
    {
        if(!_equipmentExecutions.Remove(request.WorkRequestId,out var execution))return;
        _equipment?.ReleaseReservation(execution.EquipmentInstanceId,request.WorkRequestId);
        Stockpiles.CellReservations.ReleaseByWorkRequest(request.WorkRequestId);
    }
}
