using System;
using Godot;
using GameplayV3.Jobs.Runtime;

namespace GameplayV3.Jobs;

public static class ProductionWorkPrioritySelfCheckV3
{
    public static bool TryValidate(out string reason)
    {
        if(!Enum.IsDefined(JobTypeV3.Production)){reason="Production job category is missing.";return false;}
        if(!MercenaryWorkPriorityPanelV3.IncludesProduction||MercenaryWorkPriorityPanelV3.CategoryCount!=Enum.GetValues<JobTypeV3>().Length){reason="Production priority UI category is missing.";return false;}
        MercenaryWorkPriorityProfileV3 profile=new();
        if(profile.GetPriority(JobTypeV3.Production)!=3||!profile.IsEnabled(JobTypeV3.Production)){reason="Production default priority is not enabled at normal priority.";return false;}
        int hauling=profile.GetPriority(JobTypeV3.Hauling),changes=0;
        JobManagerV3 jobs=new(17);
        jobs.PriorityChanged+=(_,type,_)=>{if(type==JobTypeV3.Production)changes++;};
        if(!jobs.TrySetPriority("worker",JobTypeV3.Production,0,out reason)||jobs.GetOrCreatePriorityProfile("worker").IsEnabled(JobTypeV3.Production)){reason="Production priority could not be disabled.";return false;}
        if(!jobs.TrySetPriority("worker",JobTypeV3.Production,0,out reason)||changes!=1){reason="No-op production priority emitted a duplicate change.";return false;}
        if(jobs.GetOrCreatePriorityProfile("worker").GetPriority(JobTypeV3.Hauling)!=hauling){reason="Production priority changed another category.";return false;}
        JobSourceKeyV3 key=new("company",JobTypeV3.Production,JobSourceKindV3.ProductionFacility,"facility");
        if(!jobs.TryUpsertSource(key,Vector2I.Zero,1,out JobRecordV3? job,out bool created,out reason)||!created||job?.JobType!=JobTypeV3.Production){reason="Production source was not mapped to the production category.";return false;}
        JobManagerV3 automatic=new(21);automatic.TryUpsertSource(key,new Vector2I(4,4),1,out _,out _,out _);automatic.QueueIdleMercenary("producer");JobTypeV3? dispatched=null;
        automatic.TickAssignments(0,_=>Vector2I.Zero,_=>"company",_=>true,(_,_)=>true,(record,_)=>record.JobType==JobTypeV3.Production?11:1,(record,_)=>{dispatched=record.JobType;return JobDispatchResultV3.Success("production-work");});
        if(dispatched!=JobTypeV3.Production||automatic.ActiveAssignmentCount!=1){reason="Ready production job was not automatically assigned.";return false;}
        JobManagerV3 disabled=new(22);disabled.TryUpsertSource(key,new Vector2I(4,4),1,out _,out _,out _);disabled.TrySetPriority("disabled",JobTypeV3.Production,0,out _);disabled.QueueIdleMercenary("disabled");
        disabled.TickAssignments(0,_=>Vector2I.Zero,_=>"company",_=>true,(_,_)=>true,(_,_)=>11,(_,_)=>JobDispatchResultV3.Success("unexpected"));if(disabled.ActiveAssignmentCount!=0){reason="Disabled production priority received an automatic assignment.";return false;}
        reason=string.Empty;return true;
    }
}
