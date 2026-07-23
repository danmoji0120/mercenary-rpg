using System;
using GameplayV3.Resources;
using WorldV2;

namespace GameplayV3.Work;

public static class EmptyGroundStackHaulingSelfCheckV3
{
    public static bool TryValidate(out string reason)
    {
        try
        {
            ResourceSessionV3 resources=new();
            GlobalCellCoord cell=new(new(3,3));
            if(!resources.GroundStacks.TryAddStack(ResourceTypeV3.Wood,1,cell,out GroundResourceStackV3? stack,out bool merged,out reason)||stack==null||merged)
                return false;

            GroundStackReservationRegistryV3 reservations=new(resources.AmountReservations,resources.GroundStacks);
            GroundStackReservationV3 source=new(stack.ResourceStackId,"empty_stack_self_check_work","mercenary","company",1,1,DateTime.UtcNow);
            if(!reservations.TryReserve(source,out reason))
                return false;
            if(!resources.GroundStacks.TryTakeAmount(stack.ResourceStackId,1,out int taken,out _,out bool removed,out reason)||taken!=1||!removed)
                return false;
            if(resources.GroundStacks.Contains(stack.ResourceStackId)||resources.GroundStacks.GetStacksAtCell(cell).Count!=0)
            {
                reason="Empty stack remained in the registry or cell index.";
                return false;
            }
            if(reservations.ReleaseByGroundStack(stack.ResourceStackId)!=1||resources.AmountReservations.Count!=0)
            {
                reason="Empty stack reservation remained after cleanup.";
                return false;
            }
            reason=string.Empty;
            return true;
        }
        catch(Exception exception)
        {
            reason=exception.Message;
            return false;
        }
    }
}
