using System;
using System.Collections.Generic;
using GameplayV3.Jobs;
using GameplayV3.Resources.Runtime;
using GameplayV3.Stockpile;
using GameplayV3.Work;
using Godot;
using WorldV2;

namespace GameplayV3.Resources;

public static class ResourceRuntimeScaleGuardSelfCheckV3
{
    public static string LastSummary{get;private set;}=string.Empty;
    public static bool TryValidate(out string reason)
    {
        try
        {
            ResourceSessionV3 resources=new();Rect2I bounds=new(0,0,256,128);const int count=20000;
            for(int i=0;i<count;i++)
            {
                Vector2I cell=new(i%200,i/200);ResourceNodeTypeV3 type=i%2==0?ResourceNodeTypeV3.Tree:ResourceNodeTypeV3.StoneOutcrop;string definition=type==ResourceNodeTypeV3.Tree?NaturalResourceDefinitionCatalogV3.TreeId:NaturalResourceDefinitionCatalogV3.StoneId;string id=ResourceNodeIdFactoryV3.CreateDeterministic(811,definition,cell);
                Require(ResourceNodeStateV3.TryCreate(id,type,new(cell),20,20,5,bounds,DateTime.UnixEpoch,out ResourceNodeStateV3? node,out reason)&&node!=null&&resources.Nodes.TryRegister(node,out reason),reason);
            }
            JobManagerV3 jobs=new(811);List<Vector2I> mercenaryCells=new(){new(208,112),new(209,112),new(210,112)};Vector2I activeChunk=new(4,1);
            GatheringJobMaterializerV3 materializer=new(jobs,resources.Nodes,"company",()=>mercenaryCells,new[]{activeChunk});
            for(int i=0;i<8&&materializer.MaterializedCount<materializer.Budget;i++)materializer.Synchronize();
            Require(resources.Nodes.Count==count,"Resource registry fixture count changed.");Require(materializer.Budget==128,"Three-mercenary budget was not 128.");Require(materializer.MaterializedCount<=128&&materializer.MaterializedCount>0,"Gathering materialization was not bounded.");Require(jobs.Count<=128,"Jobs still scale 1:1 with resources.");Require(jobs.Diagnostics.GatheringFullRegistryScanCount==0,"Full resource registry scan was used.");
            int before=materializer.CountMaterializedInChunk(activeChunk);Require(before>0,"Detach fixture did not materialize its active chunk.");mercenaryCells.Clear();mercenaryCells.Add(Vector2I.Zero);materializer.OnChunkDetached(activeChunk);for(int i=0;i<8&&materializer.CountMaterializedInChunk(activeChunk)>0;i++)materializer.Synchronize();Require(materializer.CountMaterializedInChunk(activeChunk)==0,"Detached queued jobs were not retired.");
            WorldChunkCacheV2 cache=new();cache.MarkGenerating(new(9,9));Require(cache.CancelGenerating(new(9,9)),"Generating cancellation failed.");cache.MarkGenerating(new(10,10));cache.StoreChunkData(new(10,10),new(new(10,10),Vector2I.Zero,Vector2I.Zero));WorldChunkCacheSummaryV2 summary=cache.GetSummary();Require(summary.GeneratingCount==0&&cache.GenerationStartedTotal==2&&cache.GenerationCompletedTotal==1&&cache.GenerationCancelledTotal==1,"Cache lifecycle counters are inconsistent.");
            Require(WorkResourceSelfCheckV3.Run().Passed,"Resource/Gathering regression failed.");Require(StockpileHaulingSelfCheckV3.Run().Passed,"Hauling/Stockpile regression failed.");
            LastSummary=$"registry={count} autoJobs={materializer.MaterializedCount}/{materializer.Budget} peakJobs={jobs.Count} retired={jobs.Diagnostics.GatheringJobRetiredTotal} fullScan={jobs.Diagnostics.GatheringFullRegistryScanCount} cache current/start/complete/cancel=0/{cache.GenerationStartedTotal}/{cache.GenerationCompletedTotal}/{cache.GenerationCancelledTotal}";reason=string.Empty;return true;
        }
        catch(Exception exception){reason=exception.Message;LastSummary="FAIL: "+reason;return false;}
    }
    private static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
}
