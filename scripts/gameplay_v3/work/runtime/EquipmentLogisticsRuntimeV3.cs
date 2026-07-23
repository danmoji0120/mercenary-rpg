using GameplayV3.Equipment;
using GameplayV3.Mercenary;
using GameplayV3.Movement;
using GameplayV3.Session;
using GameplayV3.Stockpile;
using WorldV2;

namespace GameplayV3.Work.Runtime;

public partial class MercenaryWorkRuntimeV3
{
    private void TickEquipmentLogistics()
    {
        foreach(EquipmentLogisticsExecutionV3 execution in _work!.GetActiveEquipmentExecutions())
        {
            if(execution.Phase!=EquipmentLogisticsPhaseV3.WaitingForSourcePath)continue;
            if(!_mercenaries!.Registry.TryGetState(execution.MercenaryId,out MercenaryStateV3? state)||state==null){_work.Fail(execution.WorkRequestId,"WorkerRemoved");continue;}
            if(!GameplaySessionV3.TryGetEquipmentRuntime(out EquipmentRuntimeV3? equipment)||equipment==null||!equipment.IsReservedBy(execution.EquipmentInstanceId,execution.WorkRequestId)){_work.Fail(execution.WorkRequestId,"EquipmentReservationLost");continue;}
            if(state.CurrentCell.Value==execution.SourceCell.Value){CompleteEquipmentSourceAction(execution,state);continue;}
            if(!_control!.ExternalMovements.TryRequest(execution.MercenaryId,state.CurrentCell,execution.SourceCell,MovementRequestSourceTypeV3.Work,execution.WorkRequestId,_work.SessionRevision,execution.Revision,_control.Movements,out _,out string reason)){_work.Fail(execution.WorkRequestId,reason);continue;}
            execution.Phase=EquipmentLogisticsPhaseV3.MovingToSource;
            if(_work.TryGetRequest(execution.WorkRequestId,out WorkRequestV3? request)&&request!=null)request.Status=WorkRequestStatusV3.MovingToTarget;
        }
    }

    private bool TryHandleEquipmentMovementResult(MercenaryMovementResultV3 result)
    {
        if(!_work!.TryGetEquipmentExecution(result.Request.SourceId,out EquipmentLogisticsExecutionV3? execution)||execution==null||execution.Revision!=result.Request.SourceRevision)return false;
        if(!_mercenaries!.Registry.TryGetState(execution.MercenaryId,out MercenaryStateV3? state)||state==null){_work.Fail(execution.WorkRequestId,"WorkerRemoved");return true;}
        if(!result.Succeeded){_work.Fail(execution.WorkRequestId,execution.Phase==EquipmentLogisticsPhaseV3.MovingToSource?"NoPath":"NoStoragePath");return true;}
        if(execution.Phase==EquipmentLogisticsPhaseV3.MovingToSource)
        {
            if(state.CurrentCell.Value!=execution.SourceCell.Value){_work.Fail(execution.WorkRequestId,"InvalidSourceArrival");return true;}
            CompleteEquipmentSourceAction(execution,state);return true;
        }
        if(execution.Phase==EquipmentLogisticsPhaseV3.MovingToDestination)
        {
            if(execution.DestinationCell is not { } destination||state.CurrentCell.Value!=destination.Value||execution.StorageId is not { } storageId||!_work.Stockpiles.Zones.TryGetZoneAtCell(destination,out StockpileZoneStateV3? zone)||zone==null||zone.StockpileZoneId!=storageId||!zone.AllowsEquipment||!GameplaySessionV3.TryGetEquipmentRuntime(out EquipmentRuntimeV3? destinationEquipment)||destinationEquipment==null||!destinationEquipment.TryGetInstance(execution.EquipmentInstanceId,out EquipmentInstanceV3? destinationInstance)||destinationInstance==null||zone.CompanyId!=destinationInstance.OwnerCompanyId){_work.Fail(execution.WorkRequestId,"InvalidStorageArrival");return true;}
            string reason="StorageCommitFailed";
            if(!GameplaySessionV3.TryGetEquipmentRuntime(out EquipmentRuntimeV3? equipment)||equipment==null||!equipment.TryMoveGroundToStorage(execution.EquipmentInstanceId,storageId,destination.Value,zone.CompanyId,execution.WorkRequestId,out reason)){_work.Fail(execution.WorkRequestId,reason);return true;}
            _work.Stockpiles.CellReservations.TryRelease(destination,execution.WorkRequestId);_work.Complete(execution.WorkRequestId);_manager?.UpdateDebugHud("\uc7a5\ube44 \uc6b4\ubc18 \uc644\ub8cc.");return true;
        }
        return true;
    }

    private void RequestEquipmentDestination(EquipmentLogisticsExecutionV3 execution,MercenaryStateV3 state)
    {
        if(execution.DestinationCell is not { } destination){_work!.Fail(execution.WorkRequestId,"NoAllowedStorage");return;}
        if(state.CurrentCell.Value==destination.Value)
        {
            string reason="StorageCommitFailed";
            if(!GameplaySessionV3.TryGetEquipmentRuntime(out EquipmentRuntimeV3? equipment)||equipment==null||execution.StorageId is not { } storageId||!_work!.Stockpiles.Zones.TryGetZoneAtCell(destination,out StockpileZoneStateV3? zone)||zone==null||!equipment.TryMoveGroundToStorage(execution.EquipmentInstanceId,storageId,destination.Value,zone.CompanyId,execution.WorkRequestId,out reason)){_work!.Fail(execution.WorkRequestId,reason);return;}
            _work.Stockpiles.CellReservations.TryRelease(destination,execution.WorkRequestId);_work.Complete(execution.WorkRequestId);return;
        }
        if(!_control!.ExternalMovements.TryRequest(execution.MercenaryId,state.CurrentCell,destination,MovementRequestSourceTypeV3.Work,execution.WorkRequestId,_work!.SessionRevision,execution.Revision,_control.Movements,out _,out string failure)){_work.Fail(execution.WorkRequestId,failure);return;}
        execution.Phase=EquipmentLogisticsPhaseV3.MovingToDestination;
    }

    private void CompleteEquipmentSourceAction(EquipmentLogisticsExecutionV3 execution,MercenaryStateV3 state)
    {
        if(execution.Kind==EquipmentLogisticsWorkKindV3.Hauling){RequestEquipmentDestination(execution,state);return;}
        EquipmentCommandFailureV3 failure=EquipmentCommandFailureV3.InvalidSession;
        if(!GameplaySessionV3.TryGetEquipmentLoadouts(out EquipmentLoadoutRuntimeV3? loadouts)||loadouts==null||!loadouts.TryEquipReservedAt(execution.MercenaryId,execution.EquipmentInstanceId,execution.WorkRequestId,state.CurrentCell,_work!.Stockpiles,_work.SessionRevision,out failure)){_work!.Fail(execution.WorkRequestId,failure.ToString());return;}
        _work!.Complete(execution.WorkRequestId);_manager?.UpdateDebugHud("\uc7a5\ube44 \uc7a5\ucc29 \uc644\ub8cc.");
    }
}
