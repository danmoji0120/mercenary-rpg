using System;
using System.Collections.Generic;
using System.Diagnostics;
using GameplayV3.Control;
using Godot;
using WorldV2;

namespace GameplayV3.Navigation;

public sealed class MercenaryNavigationCellInfoV3
{
    public MercenaryNavigationCellInfoV3(bool inside, bool walkable, float traversalMultiplier, TileType tileType, BiomeKindV3 biomeKind)
    { IsInsideWorld=inside; IsWalkable=walkable; TraversalMultiplier=traversalMultiplier; TileType=tileType; BiomeKind=biomeKind; }
    public bool IsInsideWorld { get; }
    public bool IsWalkable { get; }
    public float TraversalMultiplier { get; }
    public TileType TileType { get; }
    public BiomeKindV3 BiomeKind { get; }
}

public interface IMercenaryNavigationWorldQueryV3
{
    bool IsInsideWorld(Vector2I cell);
    MercenaryNavigationCellInfoV3 GetCellInfo(Vector2I cell);
    bool IsWalkable(Vector2I cell);
    float GetTraversalMultiplier(Vector2I cell);
}
public interface INavigationOccupancyRevisionV3{long OccupancyRevision{get;}}
public static class MercenaryNavigationRevisionPolicyV3{public static long GetRevision(IMercenaryNavigationWorldQueryV3 query)=>query is INavigationOccupancyRevisionV3 dynamicQuery?dynamicQuery.OccupancyRevision:0;public static bool IsCurrent(MercenaryPathRequestV3 request,IMercenaryNavigationWorldQueryV3 query)=>request.NavigationOccupancyRevision==GetRevision(query);}

public sealed class MercenaryNavigationWorldQueryV3 : IMercenaryNavigationWorldQueryV3
{
    private readonly Rect2I _bounds; private readonly Func<Vector2I,FlatlandCellSampleV2> _sample;
    public MercenaryNavigationWorldQueryV3(Rect2I bounds, Func<Vector2I,FlatlandCellSampleV2> sample) { _bounds=bounds; _sample=sample; }
    public bool IsInsideWorld(Vector2I cell)=>_bounds.HasPoint(cell);
    public MercenaryNavigationCellInfoV3 GetCellInfo(Vector2I cell)
    {
        if(!IsInsideWorld(cell))return new(false,false,1f,TileType.Wasteland,BiomeKindV3.Wasteland);
        FlatlandCellSampleV2 s=_sample(cell); bool walk=s.IsWalkable&&(!s.IsRiver||s.IsBridgeCandidate);
        return new(true,walk,MercenaryMovementCostPolicyV3.GetTraversalMultiplier(s),s.TileType,s.BiomeKind);
    }
    public bool IsWalkable(Vector2I cell)=>GetCellInfo(cell).IsWalkable;
    public float GetTraversalMultiplier(Vector2I cell)=>GetCellInfo(cell).TraversalMultiplier;
}

public static class MercenaryMovementCostPolicyV3
{
    public const float StraightDistance=1f;
    public const float DiagonalDistance=1.41421356237f;
    public const float MinimumTraversalMultiplier=0.75f;
    public static float GetTraversalMultiplier(FlatlandCellSampleV2 s)
    {
        if(s.IsVillage||s.IsStartingVillage)return 0.90f;
        if(s.IsRoad)return 0.75f;
        if(s.ForestStrength>=0.62f)return 1.50f;
        if(s.ForestStrength>=0.42f||s.IsForest)return 1.35f;
        return s.BiomeKind switch { BiomeKindV3.RockyHills=>1.20f, BiomeKindV3.Wasteland=>1.15f, BiomeKindV3.Dryland=>1.10f, BiomeKindV3.ForestLand=>1.05f, _=>1f };
    }
    public static float Octile(Vector2I a,Vector2I b)
    { int dx=Math.Abs(a.X-b.X),dy=Math.Abs(a.Y-b.Y); return Math.Max(dx,dy)+(DiagonalDistance-1f)*Math.Min(dx,dy); }
    public static float DirectionDistance(Vector2I a,Vector2I b)=>a.X!=b.X&&a.Y!=b.Y?DiagonalDistance:StraightDistance;
}

public sealed class MercenaryNavigationSettingsV3
{
    public int MaxExpandedNodes { get; init; }=32768;
    public int MaxExpansionsPerTick { get; init; }=256;
    public int SearchQuantum { get; init; }=32;
    public int MaxActiveSearches { get; init; }=3;
    public double MaxMillisecondsPerTick { get; init; }=2.0;
}

public sealed class MercenaryPathRequestV3
{
    public MercenaryPathRequestV3(string requestId,string mercenaryId,GlobalCellCoord start,GlobalCellCoord destination,long sessionRevision,long orderRevision)
    { PathRequestId=requestId;MercenaryId=mercenaryId;StartCell=start;DestinationCell=destination;SessionRevision=sessionRevision;OrderRevision=orderRevision; }
    public string PathRequestId{get;} public string MercenaryId{get;} public GlobalCellCoord StartCell{get;} public GlobalCellCoord DestinationCell{get;} public long SessionRevision{get;} public long OrderRevision{get;} public long NavigationOccupancyRevision{get;internal set;}
}

public enum MercenaryPathFailureV3 { None, InvalidStart, InvalidDestination, StartBlocked, DestinationBlocked, NoPath, SearchLimitExceeded, Superseded }
public sealed class MercenaryPathResultV3
{
    public MercenaryPathResultV3(MercenaryPathRequestV3 request,bool success,IReadOnlyList<GlobalCellCoord> path,float totalCost,int expanded,double ms,MercenaryPathFailureV3 failure)
    { Request=request;Success=success;Path=new List<GlobalCellCoord>(path).AsReadOnly();TotalCost=totalCost;ExpandedNodeCount=expanded;SearchDurationMs=ms;Failure=failure; }
    public MercenaryPathRequestV3 Request{get;} public bool Success{get;} public IReadOnlyList<GlobalCellCoord> Path{get;} public float TotalCost{get;} public int ExpandedNodeCount{get;} public double SearchDurationMs{get;} public MercenaryPathFailureV3 Failure{get;}
}

internal readonly struct OpenPriorityV3:IComparable<OpenPriorityV3>
{
    public OpenPriorityV3(float f,float h,Vector2I c,long seq){F=f;H=h;Y=c.Y;X=c.X;Sequence=seq;}
    private float F{get;} private float H{get;} private int Y{get;} private int X{get;} private long Sequence{get;}
    public int CompareTo(OpenPriorityV3 o){int c=F.CompareTo(o.F);if(c!=0)return c;c=H.CompareTo(o.H);if(c!=0)return c;c=Y.CompareTo(o.Y);if(c!=0)return c;c=X.CompareTo(o.X);return c!=0?c:Sequence.CompareTo(o.Sequence);}
}

internal sealed class MercenaryAStarSearchV3
{
    private static readonly Vector2I[] Neighbors={new(0,-1),new(1,0),new(0,1),new(-1,0),new(1,-1),new(1,1),new(-1,1),new(-1,-1)};
    private readonly MercenaryPathRequestV3 _request; private readonly IMercenaryNavigationWorldQueryV3 _query; private readonly MercenaryNavigationSettingsV3 _settings;
    private readonly PriorityQueue<Vector2I,OpenPriorityV3> _open=new(); private readonly Dictionary<Vector2I,float> _g=new(); private readonly Dictionary<Vector2I,Vector2I> _came=new(); private readonly HashSet<Vector2I> _closed=new(); private readonly Dictionary<Vector2I,MercenaryNavigationCellInfoV3> _cells=new();
    private long _sequence; private double _cpuMs;
    public MercenaryAStarSearchV3(MercenaryPathRequestV3 request,IMercenaryNavigationWorldQueryV3 query,MercenaryNavigationSettingsV3 settings)
    { _request=request;_query=query;_settings=settings; Vector2I start=request.StartCell.Value; float h=Heuristic(start);_g[start]=0;_open.Enqueue(start,new(h,h,start,_sequence++)); }
    public MercenaryPathRequestV3 Request=>_request; public bool IsComplete{get;private set;} public MercenaryPathResultV3? Result{get;private set;} public int Expanded=>_closed.Count; public int Discovered=>_g.Count;
    public void Supersede(){if(!IsComplete)Complete(false,Array.Empty<GlobalCellCoord>(),0,MercenaryPathFailureV3.Superseded);}
    public int Step(int budget)
    {
        if(IsComplete)return 0; Stopwatch sw=Stopwatch.StartNew(); Vector2I start=_request.StartCell.Value,dest=_request.DestinationCell.Value;
        bool timingCaptured=false;
        void Finish(bool success,IReadOnlyList<GlobalCellCoord> path,float cost,MercenaryPathFailureV3 failure)
        {
            sw.Stop();_cpuMs+=sw.Elapsed.TotalMilliseconds;timingCaptured=true;Complete(success,path,cost,failure);
        }
        if(_closed.Count==0)
        { if(!_query.IsInsideWorld(start)){Finish(false,Array.Empty<GlobalCellCoord>(),0,MercenaryPathFailureV3.InvalidStart);return 0;} if(!_query.IsInsideWorld(dest)){Finish(false,Array.Empty<GlobalCellCoord>(),0,MercenaryPathFailureV3.InvalidDestination);return 0;} if(!Cell(start).IsWalkable){Finish(false,Array.Empty<GlobalCellCoord>(),0,MercenaryPathFailureV3.StartBlocked);return 0;} if(!Cell(dest).IsWalkable){Finish(false,Array.Empty<GlobalCellCoord>(),0,MercenaryPathFailureV3.DestinationBlocked);return 0;} if(start==dest){Finish(true,Array.Empty<GlobalCellCoord>(),0,MercenaryPathFailureV3.None);return 0;} }
        int used=0;
        while(used<budget&&_open.Count>0)
        {
            Vector2I current=_open.Dequeue(); if(!_closed.Add(current))continue; used++;
            if(current==dest){List<GlobalCellCoord> path=BuildPath(current,start);Finish(true,path,_g[current],MercenaryPathFailureV3.None);break;}
            if(_closed.Count>=_settings.MaxExpandedNodes){Finish(false,Array.Empty<GlobalCellCoord>(),0,MercenaryPathFailureV3.SearchLimitExceeded);break;}
            foreach(Vector2I offset in Neighbors)
            {
                Vector2I next=current+offset;if(_closed.Contains(next)||!Cell(next).IsWalkable)continue;
                bool diagonal=offset.X!=0&&offset.Y!=0;if(diagonal&&(!Cell(current+new Vector2I(offset.X,0)).IsWalkable||!Cell(current+new Vector2I(0,offset.Y)).IsWalkable))continue;
                float tentative=_g[current]+(diagonal?MercenaryMovementCostPolicyV3.DiagonalDistance:1f)*Cell(next).TraversalMultiplier;
                if(_g.TryGetValue(next,out float old)&&tentative>=old-0.00001f)continue;
                _g[next]=tentative;_came[next]=current;float h=Heuristic(next);_open.Enqueue(next,new(tentative+h,h,next,_sequence++));
            }
        }
        if(!IsComplete&&_open.Count==0)Finish(false,Array.Empty<GlobalCellCoord>(),0,MercenaryPathFailureV3.NoPath);
        if(!timingCaptured){sw.Stop();_cpuMs+=sw.Elapsed.TotalMilliseconds;}return used;
    }
    private MercenaryNavigationCellInfoV3 Cell(Vector2I cell){if(!_cells.TryGetValue(cell,out MercenaryNavigationCellInfoV3? info)){info=_query.GetCellInfo(cell);_cells[cell]=info;}return info;}
    private float Heuristic(Vector2I cell)=>MercenaryMovementCostPolicyV3.Octile(cell,_request.DestinationCell.Value)*MercenaryMovementCostPolicyV3.MinimumTraversalMultiplier;
    private List<GlobalCellCoord> BuildPath(Vector2I current,Vector2I start){List<GlobalCellCoord> path=new();while(current!=start){path.Add(new(current));current=_came[current];}path.Reverse();return path;}
    private void Complete(bool success,IReadOnlyList<GlobalCellCoord> path,float cost,MercenaryPathFailureV3 failure){IsComplete=true;Result=new(_request,success,path,cost,_closed.Count,_cpuMs,failure);}
}

public sealed class MercenaryPathfindingSchedulerV3
{
    private readonly MercenaryNavigationSettingsV3 _settings; private readonly Dictionary<string,MercenaryAStarSearchV3> _active=new(StringComparer.Ordinal); private readonly Queue<MercenaryPathRequestV3> _queued=new(); private readonly HashSet<string> _known=new(StringComparer.Ordinal);
    private int _roundRobinCursor;
    public MercenaryPathfindingSchedulerV3(MercenaryNavigationSettingsV3 settings){_settings=settings;}
    public int PendingCount=>_queued.Count; public int ActiveCount=>_active.Count;
    public bool IsKnown(string pathRequestId)=>_known.Contains(pathRequestId);
    public void Enqueue(MercenaryPathRequestV3 request){if(_known.Add(request.PathRequestId))_queued.Enqueue(request);}
    public IReadOnlyList<MercenaryPathResultV3> Tick(IMercenaryNavigationWorldQueryV3 query,Func<MercenaryPathRequestV3,bool> isCurrent,out int peakDiscovered)
    {
        while(_active.Count<_settings.MaxActiveSearches&&_queued.Count>0){MercenaryPathRequestV3 r=_queued.Dequeue();if(!isCurrent(r)){_known.Remove(r.PathRequestId);continue;}_active[r.PathRequestId]=new(r,query,_settings);}
        List<MercenaryPathResultV3> results=new();List<string> keys=new(_active.Keys);keys.Sort(StringComparer.Ordinal);Stopwatch frame=Stopwatch.StartNew();int remaining=_settings.MaxExpansionsPerTick;peakDiscovered=0;
        int processed=0;for(int index=0;index<keys.Count;index++){if(remaining<=0||frame.Elapsed.TotalMilliseconds>=_settings.MaxMillisecondsPerTick)break;string key=keys[(_roundRobinCursor+index)%keys.Count];MercenaryAStarSearchV3 s=_active[key];if(!isCurrent(s.Request))s.Supersede();else remaining-=s.Step(Math.Min(_settings.SearchQuantum,remaining));processed++;peakDiscovered=Math.Max(peakDiscovered,s.Discovered);if(s.IsComplete&&s.Result!=null){results.Add(s.Result);_active.Remove(key);_known.Remove(key);}}
        if(keys.Count>0)_roundRobinCursor=(_roundRobinCursor+Math.Max(1,processed))%keys.Count;
        return results;
    }
}
