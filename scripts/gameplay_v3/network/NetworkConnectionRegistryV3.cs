using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GameplayV3.Network;

public enum NetworkConnectionStateV3
{
    Connected=1,
    Handshaken=2,
    JoinedRegion=3,
    Traveling=4
}
public enum ConnectionWorldPresenceV3{None=0,AtRegion=1,Traveling=2}

public sealed class NetworkConnectionStateDataV3
{
    private const int MaximumCachedCommandResults=32;
    private readonly Dictionary<long,(long RequestId,NetworkMessageV3 Response)> _commandResults=new();
    private readonly Queue<long> _commandResultOrder=new();
    internal NetworkConnectionStateDataV3(int peerId)
    {
        PeerId=peerId;
        ConnectedUtc=DateTime.UtcNow;
    }

    public int PeerId{get;}
    public DateTime ConnectedUtc{get;}
    public NetworkConnectionStateV3 State{get;internal set;}=NetworkConnectionStateV3.Connected;
    public string DevelopmentAccountId{get;internal set;}=string.Empty;
    public string PlayerAccountId{get;internal set;}=string.Empty;
    public string CompanyId{get;internal set;}=string.Empty;
    public string CurrentRegionId{get;internal set;}=string.Empty;
    public string CurrentTravelingGroupId{get;internal set;}=string.Empty;
    public ConnectionWorldPresenceV3 WorldPresence{get;internal set;}
    public long LastApprovedRequestId{get;internal set;}
    public bool InitialSnapshotApproved{get;internal set;}
    public long LastClientCommandSequence{get;internal set;}
    public long LastDeltaSequence{get;internal set;}
    public int CachedCommandResultCount=>_commandResults.Count;
    public bool TryGetCommandResult(long sequence,long requestId,out NetworkMessageV3? response)
    {
        response=null;
        if(!_commandResults.TryGetValue(sequence,out var cached)||cached.RequestId!=requestId)return false;
        response=cached.Response;return true;
    }
    public bool HasCommandSequence(long sequence)=>_commandResults.ContainsKey(sequence);
    public void CacheCommandResult(long sequence,long requestId,NetworkMessageV3 response)
    {
        if(_commandResults.ContainsKey(sequence))return;
        _commandResults.Add(sequence,(requestId,response));_commandResultOrder.Enqueue(sequence);
        while(_commandResultOrder.Count>MaximumCachedCommandResults)_commandResults.Remove(_commandResultOrder.Dequeue());
        LastClientCommandSequence=Math.Max(LastClientCommandSequence,sequence);
    }
    internal void ResetRegionCommandLifecycle()
    {
        _commandResults.Clear();_commandResultOrder.Clear();LastClientCommandSequence=0;LastDeltaSequence=0;
    }
}

public sealed class NetworkConnectionRegistryV3
{
    private readonly Dictionary<int,NetworkConnectionStateDataV3> _byPeer=new();
    private readonly Dictionary<string,int> _peerByDevelopmentAccount=new(StringComparer.Ordinal);
    private readonly Dictionary<string,HashSet<int>> _peersByRegion=new(StringComparer.Ordinal);

    public int Count=>_byPeer.Count;
    public int HandshakenCount{get{int count=0;foreach(NetworkConnectionStateDataV3 item in _byPeer.Values)if(item.State>=NetworkConnectionStateV3.Handshaken)count++;return count;}}
    public int JoinedRegionCount{get{int count=0;foreach(NetworkConnectionStateDataV3 item in _byPeer.Values)if(item.State==NetworkConnectionStateV3.JoinedRegion)count++;return count;}}
    public IReadOnlyCollection<NetworkConnectionStateDataV3> Connections=>new ReadOnlyCollection<NetworkConnectionStateDataV3>(new List<NetworkConnectionStateDataV3>(_byPeer.Values));
    public int RegionMembershipCount{get{int count=0;foreach(HashSet<int> peers in _peersByRegion.Values)count+=peers.Count;return count;}}

    public NetworkConnectionStateDataV3 GetOrAddConnected(int peerId)
    {
        if(peerId<2)throw new ArgumentOutOfRangeException(nameof(peerId));
        if(!_byPeer.TryGetValue(peerId,out NetworkConnectionStateDataV3? value)){value=new(peerId);_byPeer.Add(peerId,value);}
        return value;
    }

    public bool TryGet(int peerId,out NetworkConnectionStateDataV3? connection)=>_byPeer.TryGetValue(peerId,out connection);
    public bool IsDevelopmentAccountConnected(string accountId,int excludingPeerId=0)=>_peerByDevelopmentAccount.TryGetValue(accountId,out int peerId)&&peerId!=excludingPeerId;

    public void MarkHandshaken(NetworkConnectionStateDataV3 connection,string developmentAccountId,string playerAccountId,string companyId,long requestId)
    {
        connection.DevelopmentAccountId=developmentAccountId;
        connection.PlayerAccountId=playerAccountId;
        connection.CompanyId=companyId;
        connection.LastApprovedRequestId=requestId;
        connection.State=NetworkConnectionStateV3.Handshaken;
        _peerByDevelopmentAccount[developmentAccountId]=connection.PeerId;
    }

    public void MarkJoined(NetworkConnectionStateDataV3 connection,string regionId,long requestId)
    {
        RemoveRegionMembership(connection);
        connection.CurrentRegionId=regionId;
        connection.CurrentTravelingGroupId=string.Empty;
        connection.WorldPresence=ConnectionWorldPresenceV3.AtRegion;
        connection.LastApprovedRequestId=requestId;
        connection.State=NetworkConnectionStateV3.JoinedRegion;
        connection.InitialSnapshotApproved=false;
        connection.ResetRegionCommandLifecycle();
        if(!_peersByRegion.TryGetValue(regionId,out HashSet<int>? peers)){peers=new();_peersByRegion.Add(regionId,peers);}
        peers.Add(connection.PeerId);
    }

    public void MarkTraveling(NetworkConnectionStateDataV3 connection,string travelingGroupId,long requestId)
    {
        RemoveRegionMembership(connection);
        connection.CurrentRegionId=string.Empty;
        connection.CurrentTravelingGroupId=travelingGroupId;
        connection.WorldPresence=ConnectionWorldPresenceV3.Traveling;
        connection.LastApprovedRequestId=requestId;
        connection.State=NetworkConnectionStateV3.Traveling;
        connection.InitialSnapshotApproved=false;
        connection.ResetRegionCommandLifecycle();
    }

    public IReadOnlyList<NetworkConnectionStateDataV3> GetJoinedConnections(string regionId)
    {
        List<NetworkConnectionStateDataV3> result=new();
        if(_peersByRegion.TryGetValue(regionId,out HashSet<int>? peers))
            foreach(int peerId in peers)if(_byPeer.TryGetValue(peerId,out NetworkConnectionStateDataV3? connection)&&connection.State==NetworkConnectionStateV3.JoinedRegion)result.Add(connection);
        result.Sort((a,b)=>a.PeerId.CompareTo(b.PeerId));
        return new ReadOnlyCollection<NetworkConnectionStateDataV3>(result);
    }

    public int GetRegionConnectionCount(string regionId)=>_peersByRegion.TryGetValue(regionId,out HashSet<int>? peers)?peers.Count:0;

    public void MarkRequestApproved(NetworkConnectionStateDataV3 connection,long requestId)
    {
        if(requestId<=connection.LastApprovedRequestId)throw new ArgumentOutOfRangeException(nameof(requestId));
        connection.LastApprovedRequestId=requestId;
    }

    public void MarkInitialSnapshotApproved(NetworkConnectionStateDataV3 connection,long requestId)
    {
        MarkRequestApproved(connection,requestId);
        connection.InitialSnapshotApproved=true;
        connection.LastDeltaSequence=0;
    }

    public bool Remove(int peerId)
    {
        if(!_byPeer.Remove(peerId,out NetworkConnectionStateDataV3? value))return false;
        RemoveRegionMembership(value);
        if(value.DevelopmentAccountId.Length>0&&_peerByDevelopmentAccount.TryGetValue(value.DevelopmentAccountId,out int registered)&&registered==peerId)
            _peerByDevelopmentAccount.Remove(value.DevelopmentAccountId);
        return true;
    }

    public void Clear(){_byPeer.Clear();_peerByDevelopmentAccount.Clear();_peersByRegion.Clear();}

    private void RemoveRegionMembership(NetworkConnectionStateDataV3 connection)
    {
        if(connection.CurrentRegionId.Length==0||!_peersByRegion.TryGetValue(connection.CurrentRegionId,out HashSet<int>? peers))return;
        peers.Remove(connection.PeerId);
        if(peers.Count==0)_peersByRegion.Remove(connection.CurrentRegionId);
    }
}
