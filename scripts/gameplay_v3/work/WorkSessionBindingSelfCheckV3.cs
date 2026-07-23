using System;
using GameplayV3.Company;
using GameplayV3.Control;
using GameplayV3.Mercenary;
using GameplayV3.Resources;
using GameplayV3.Session;
using GameplayV3.Stockpile;

namespace GameplayV3.Work;

public static class WorkSessionBindingSelfCheckV3
{
    public static string LastSummary{get;private set;}=string.Empty;
    public static bool TryValidateCurrent(out string reason)
    {
        long revision=GameplaySessionV3.SessionRevision;
        if(!GameplaySessionV3.TryGetControlSession(out var control)||control==null||!GameplaySessionV3.TryGetWorkSession(out var work)||work==null||!GameplaySessionV3.TryGetResourceSession(out var resources)||resources==null||!GameplaySessionV3.TryGetMercenarySession(out var mercenaries)||mercenaries==null||!GameplaySessionV3.TryGetJobManager(out var jobs)||jobs==null){reason="Current gameplay work bundle is incomplete.";return false;}
        if(control.SessionRevision!=revision||work.SessionRevision!=revision||jobs.SessionRevision!=revision||!GameplaySessionV3.IsCurrentControlSession(control)||!GameplaySessionV3.IsCurrentWorkSession(work)||!work.IsBoundTo(control,resources,mercenaries)){reason=$"Session binding mismatch current={revision} control={control.SessionRevision} work={work.SessionRevision} jobs={jobs.SessionRevision}.";return false;}
        CompanySessionV3 staleCompanies=new();MercenarySessionV3 staleMercenaries=new(staleCompanies.CompanyRegistry);MercenaryControlSessionV3 staleControl=new(Math.Max(0,revision-1),staleCompanies,staleMercenaries);ResourceSessionV3 staleResources=new();StockpileSessionV3 staleStockpiles=new();MercenaryWorkSessionV3 staleWork=new(Math.Max(0,revision-1),staleCompanies,staleMercenaries,staleResources,staleStockpiles,staleControl);bool accepted=staleWork.TryIssueGathering("none","none",Array.Empty<string>(),"none",null!,revision,out _,out string staleReason);if(accepted||staleReason!="InvalidSession"){reason="Stale work session guard failed.";return false;}
        LastSummary=$"PASS current/control/work/jobs={revision}/{control.SessionRevision}/{work.SessionRevision}/{jobs.SessionRevision} staleRejected=True fullScans=0";reason=string.Empty;return true;
    }
}
