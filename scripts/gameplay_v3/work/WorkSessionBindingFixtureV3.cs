using GameplayV3.Control;
using GameplayV3.Jobs;
using GameplayV3.Session;
using Godot;

namespace GameplayV3.Work;

public partial class WorkSessionBindingFixtureV3:Node
{
    public override void _Ready()
    {
        GameplaySessionV3.BeginNewSession();GameplaySessionV3.TryGetWorkSession(out var firstWork);GameplaySessionV3.TryGetControlSession(out var firstControl);GameplaySessionV3.TryGetJobManager(out var firstJobs);bool firstReady=GameplaySessionV3.EnsureCompanyCoreInitialized(out _,out _,out _)&&WorkSessionBindingSelfCheckV3.TryValidateCurrent(out string reason)&&firstWork!=null&&firstControl!=null&&firstJobs!=null;long firstRevision=GameplaySessionV3.SessionRevision;
        bool f11f12=GameplaySessionV3.TryGetWorkSession(out var sameWork)&&ReferenceEquals(firstWork,sameWork)&&GameplaySessionV3.TryGetControlSession(out var sameControl)&&ReferenceEquals(firstControl,sameControl)&&GameplaySessionV3.TryGetJobManager(out var sameJobs)&&ReferenceEquals(firstJobs,sameJobs);
        GameplaySessionV3.BeginNewSession();bool secondReady=GameplaySessionV3.EnsureCompanyCoreInitialized(out _,out _,out _)&&WorkSessionBindingSelfCheckV3.TryValidateCurrent(out reason)&&GameplaySessionV3.TryGetWorkSession(out var secondWork)&&secondWork!=null&&!ReferenceEquals(firstWork,secondWork)&&GameplaySessionV3.TryGetControlSession(out MercenaryControlSessionV3? secondControl)&&secondControl!=null&&!ReferenceEquals(firstControl,secondControl)&&GameplaySessionV3.TryGetJobManager(out JobManagerV3? secondJobs)&&secondJobs!=null&&!ReferenceEquals(firstJobs,secondJobs);bool staleRejected=firstWork!=null&&!firstWork.TryIssueGathering("none","none",System.Array.Empty<string>(),"none",null!,GameplaySessionV3.SessionRevision,out _,out string staleReason)&&staleReason=="InvalidSession";bool pass=firstReady&&f11f12&&secondReady&&staleRejected&&GameplaySessionV3.SessionRevision==firstRevision+1;GD.Print($"[WorkBindingV3] fixture PASS={pass} first/second={firstRevision}/{GameplaySessionV3.SessionRevision} f11f12={f11f12} staleRejected={staleRejected} summary={WorkSessionBindingSelfCheckV3.LastSummary}");GetTree().Quit(pass?0:3);
    }
}
