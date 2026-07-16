using System;
using System.Diagnostics;
using Godot;

namespace GameplayV3.Jobs;

public static class JobManagerSelfCheckV3
{
    public static long Last100WorkerCandidateEvaluations{get;private set;}public static long Last300WorkerCandidateEvaluations{get;private set;}public static double Last100WorkerMilliseconds{get;private set;}public static double Last300WorkerMilliseconds{get;private set;}
    public static bool TryValidate(out string reason)
    {
        string id=JobIdFactoryV3.Create();if(!JobIdFactoryV3.IsValid(id)||JobIdFactoryV3.IsValid("job_bad")||JobIdFactoryV3.IsValid("cmd_"+Guid.NewGuid().ToString("N"))){reason="JobId validation failed.";return false;}
        JobManagerV3 manager=new(1);string company="cmp_test",worker="merc_test";
        MercenaryWorkPriorityProfileV3 defaults=manager.GetOrCreatePriorityProfile(worker);if(defaults.GetPriority(JobTypeV3.Hauling)!=3||defaults.GetPriority(JobTypeV3.Construction)!=3||defaults.GetPriority(JobTypeV3.Demolition)!=4||defaults.GetPriority(JobTypeV3.Gathering)!=3||defaults.GetPriority(JobTypeV3.Sowing)!=3||defaults.GetPriority(JobTypeV3.Harvesting)!=2){reason="Default work priorities failed.";return false;}
        var gathering=new JobSourceKeyV3(company,JobTypeV3.Gathering,JobSourceKindV3.ResourceNode,"node_far");
        var hauling=new JobSourceKeyV3(company,JobTypeV3.Hauling,JobSourceKindV3.GroundResourceStack,"stack_near");
        if(!manager.TryUpsertSource(gathering,new(20,0),1,out var gatherJob,out bool created,out reason)||!created||gatherJob==null)return false;
        if(!manager.TryUpsertSource(gathering,new(20,0),1,out var same,out created,out reason)||created||same?.JobId!=gatherJob.JobId){reason="SourceKey was not idempotent.";return false;}
        if(manager.TryUpsertSource(gathering,new(20,0),0,out _,out _,out _)){reason="Stale source revision was accepted.";return false;}
        manager.TryUpsertSource(hauling,new(1,0),1,out _,out _,out _);
        manager.TrySetPriority(worker,JobTypeV3.Gathering,1,out _);manager.TrySetPriority(worker,JobTypeV3.Hauling,4,out _);manager.QueueIdleMercenary(worker);
        string assigned=string.Empty;manager.TickAssignments(0,_=>Vector2I.Zero,_=>company,_=>true,(_,_)=>true,(_,_)=>1,(job,_)=>{assigned=job.JobId;return JobDispatchResultV3.Success("work_test");});
        if(assigned!=gatherJob.JobId){reason="Strict priority did not beat distance.";return false;}
        manager.MarkAssignmentTerminal(worker,false,1,string.Empty);manager.TrySetPriority(worker,JobTypeV3.Gathering,0,out _);manager.TrySetPriority(worker,JobTypeV3.Hauling,0,out _);manager.QueueIdleMercenary(worker);assigned=string.Empty;
        manager.TickAssignments(2,_=>Vector2I.Zero,_=>company,_=>true,(_,_)=>true,(_,_)=>1,(job,_)=>{assigned=job.JobId;return JobDispatchResultV3.Success("work_off");});
        if(assigned.Length!=0){reason="Off priority dispatched work.";return false;}
        JobManagerV3 needBlocked=new(3);needBlocked.TryUpsertSource(gathering,new(2,2),1,out _,out _,out _);needBlocked.QueueIdleMercenary(worker);int dispatchCount=0;needBlocked.TickAssignments(0,_=>Vector2I.Zero,_=>company,_=>false,(_,_)=>true,(_,_)=>1,(job,_)=>{dispatchCount++;return JobDispatchResultV3.Success("unexpected");});if(dispatchCount!=0||needBlocked.ActiveAssignmentCount!=0){reason="Need-priority gate had assignment side effects.";return false;}
        if(!Stress(100,1000,out long eval100,out double ms100,out reason)||!Stress(300,5000,out long eval300,out double ms300,out reason))return false;Last100WorkerCandidateEvaluations=eval100;Last300WorkerCandidateEvaluations=eval300;Last100WorkerMilliseconds=ms100;Last300WorkerMilliseconds=ms300;
        if(eval100>100L*Enum.GetValues<JobTypeV3>().Length*12||eval300>300L*Enum.GetValues<JobTypeV3>().Length*12){reason="Bounded candidate budget was exceeded.";return false;}
        JobManagerV3 replacement=new(2);if(replacement.Count!=0||replacement.ActiveAssignmentCount!=0||replacement.SessionRevision!=2){reason="New session reset failed.";return false;}
        reason=string.Empty;return true;
    }

    private static bool Stress(int workerCount,int jobCount,out long evaluations,out double milliseconds,out string reason)
    {
        Stopwatch watch=Stopwatch.StartNew();
        JobManagerV3 manager=new(7);string company="cmp_stress";
        for(int i=0;i<jobCount;i++)
        {
            JobTypeV3 type=(JobTypeV3)(i%Enum.GetValues<JobTypeV3>().Length);JobSourceKindV3 kind=type switch{JobTypeV3.Hauling=>JobSourceKindV3.GroundResourceStack,JobTypeV3.Construction=>JobSourceKindV3.Blueprint,JobTypeV3.Demolition=>JobSourceKindV3.Structure,JobTypeV3.Gathering=>JobSourceKindV3.ResourceNode,_=>JobSourceKindV3.FarmCell};
            manager.TryUpsertSource(new(company,type,kind,$"source_{i}"),new Vector2I(i%256,(i/256)%256),1,out _,out _,out _);
        }
        for(int i=0;i<workerCount;i++)manager.QueueIdleMercenary($"merc_{i}");
        int ticks=(workerCount+manager.Settings.MaxAssignmentsPerTick-1)/manager.Settings.MaxAssignmentsPerTick;
        for(int tick=0;tick<ticks;tick++)manager.TickAssignments(tick,_=>new Vector2I(tick%32,tick%16),_=>company,_=>true,(_,_)=>true,(_,_)=>1,(job,worker)=>JobDispatchResultV3.Success($"work_{worker}"));
        watch.Stop();evaluations=manager.Diagnostics.CandidateEvaluations;milliseconds=watch.Elapsed.TotalMilliseconds;
        if(manager.Diagnostics.AssignmentsSucceeded!=workerCount){reason=$"Stress assignment mismatch {workerCount}/{manager.Diagnostics.AssignmentsSucceeded}.";return false;}
        if(manager.Diagnostics.CartesianScanCount!=0){reason="Cartesian scan counter changed.";return false;}
        reason=string.Empty;return true;
    }
}
