using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameplayV3.Session;
using Godot;

namespace GameplayV3.Network;

public partial class DedicatedServerEnetHandshakeFixtureV3:Node
{
    private DedicatedServerHostV3? _host;
    private readonly List<NetworkTestClientV3> _clients=new();

    public override async void _Ready()
    {
        bool pass;
        string summary;
        try{summary=await RunFixture();pass=true;}
        catch(Exception exception){pass=false;summary=exception.ToString();}
        finally
        {
            foreach(NetworkTestClientV3 client in _clients)client.Dispose();
            _clients.Clear();
            _host?.Dispose();
        }
        GD.Print($"[DedicatedServerEnetV3] PASS={pass} {summary}");
        GetTree().Quit(pass?0:3);
    }

    private async Task<string> RunFixture()
    {
        Check(DedicatedServerBootstrapV3.ResolveRole(Array.Empty<string>())==GameplayExecutionRoleV3.LocalStandalone,"LocalStandalone role changed.");
        _host=new DedicatedServerHostV3();
        int port=await StartOnBoundedPort(_host);
        DedicatedServerSnapshotV3 started=_host.GetSnapshot();
        Check(started.ServerRunning&&started.PersistentWorldCount==1&&started.PlayerCompanyCount==1&&started.RegionCount==1&&started.ActiveRegionSessionCount==1,"Dedicated server authority did not start.");
        PersistentWorldStateV3 world=_host.World!;
        string developmentAccount="dev_fixture_account";

        NetworkTestClientV3 primary=await ConnectClient(port);
        Send(primary,new NetworkMessageV3{MessageType=NetworkMessageTypeV3.ClientHello,RequestId=1,DevelopmentPlayerAccountId=developmentAccount,ClientBuild="fixture"});
        NetworkMessageV3 hello=await WaitFor(primary,NetworkMessageTypeV3.ServerHelloAccepted);
        Check(hello.ProtocolVersion==NetworkProtocolV3.ProtocolVersion&&hello.WorldId==world.WorldId&&hello.CompanyId.Length>0&&hello.PlayerAccountId.Length>0&&hello.RegionId==PersistentWorldStateV3.InitialEstateRegionId&&hello.ActiveSessionRevision==GameplaySessionV3.SessionRevision,"ServerHello metadata mismatch.");
        string assignedCompanyId=hello.CompanyId;
        string assignedPlayerId=hello.PlayerAccountId;

        Send(primary,new NetworkMessageV3{MessageType=NetworkMessageTypeV3.JoinRegionRequest,RequestId=2,RegionId=PersistentWorldStateV3.InitialEstateRegionId});
        NetworkMessageV3 joined=await WaitFor(primary,NetworkMessageTypeV3.JoinRegionAccepted);
        RegionPersistentStateV3 estate=world.Regions[PersistentWorldStateV3.InitialEstateRegionId];
        Check(joined.WorldId==world.WorldId&&joined.CompanyId==assignedCompanyId&&joined.RegionId==estate.RegionId&&joined.RegionRevision==estate.RegionRevision&&joined.ActiveSessionRevision==GameplaySessionV3.SessionRevision,"JoinRegion metadata mismatch.");

        NetworkTestClientV3 duplicate=await ConnectClient(port);
        Send(duplicate,new NetworkMessageV3{MessageType=NetworkMessageTypeV3.ClientHello,RequestId=1,DevelopmentPlayerAccountId=developmentAccount});
        Check((await WaitFor(duplicate,NetworkMessageTypeV3.ServerHelloRejected)).RejectReason==NetworkRejectReasonV3.DuplicateAccountConnection,"Live duplicate account was not rejected.");
        await DisconnectAndRemove(duplicate);

        NetworkTestClientV3 protocolMismatch=await ConnectClient(port);
        Send(protocolMismatch,new NetworkMessageV3{ProtocolVersion=NetworkProtocolV3.ProtocolVersion+1,MessageType=NetworkMessageTypeV3.ClientHello,RequestId=1,DevelopmentPlayerAccountId="bad_protocol"});
        Check((await WaitFor(protocolMismatch,NetworkMessageTypeV3.ServerHelloRejected)).RejectReason==NetworkRejectReasonV3.ProtocolMismatch,"Protocol mismatch was not rejected.");
        await DisconnectAndRemove(protocolMismatch);

        NetworkTestClientV3 emptyAccount=await ConnectClient(port);
        Send(emptyAccount,new NetworkMessageV3{MessageType=NetworkMessageTypeV3.ClientHello,RequestId=1,DevelopmentPlayerAccountId=""});
        Check((await WaitFor(emptyAccount,NetworkMessageTypeV3.ServerHelloRejected)).RejectReason==NetworkRejectReasonV3.EmptyDevelopmentPlayerAccountId,"Empty development account was not rejected.");
        await DisconnectAndRemove(emptyAccount);

        NetworkTestClientV3 preHandshake=await ConnectClient(port);
        Send(preHandshake,new NetworkMessageV3{MessageType=NetworkMessageTypeV3.JoinRegionRequest,RequestId=1,RegionId=estate.RegionId});
        Check((await WaitFor(preHandshake,NetworkMessageTypeV3.JoinRegionRejected)).RejectReason==NetworkRejectReasonV3.HandshakeRequired,"Pre-handshake join was not rejected.");
        await DisconnectAndRemove(preHandshake);

        NetworkTestClientV3 unknownMessage=await ConnectClient(port);
        Send(unknownMessage,new NetworkMessageV3{MessageType=(NetworkMessageTypeV3)999,RequestId=1});
        Check((await WaitFor(unknownMessage,NetworkMessageTypeV3.ProtocolRejected)).RejectReason==NetworkRejectReasonV3.UnknownMessageType,"Unknown message type was not rejected.");
        await DisconnectAndRemove(unknownMessage);

        Send(primary,new NetworkMessageV3{MessageType=NetworkMessageTypeV3.JoinRegionRequest,RequestId=3,RegionId="region_missing_fixture"});
        Check((await WaitFor(primary,NetworkMessageTypeV3.JoinRegionRejected)).RejectReason==NetworkRejectReasonV3.UnknownRegion,"Unknown region was not rejected.");
        await DisconnectAndRemove(primary,graceful:false);
        Check(_host.Connections.Count==0,"Disconnect did not clear connection registry.");
        Check(world.PlayerCompanies.Count==1&&world.Regions.Count==1,"Disconnect removed persistent authority.");

        NetworkTestClientV3 reconnect=await ConnectClient(port);
        Send(reconnect,new NetworkMessageV3{MessageType=NetworkMessageTypeV3.ClientHello,RequestId=1,DevelopmentPlayerAccountId=developmentAccount});
        NetworkMessageV3 reconnectHello=await WaitFor(reconnect,NetworkMessageTypeV3.ServerHelloAccepted);
        Check(reconnectHello.CompanyId==assignedCompanyId&&reconnectHello.PlayerAccountId==assignedPlayerId,"Reconnect did not reuse server-assigned company.");
        await DisconnectAndRemove(reconnect);

        DedicatedServerSnapshotV3 snapshot=_host.GetSnapshot();
        Check(snapshot.ConnectedPeerCount==0&&snapshot.HandshakenConnectionCount==0&&snapshot.JoinedRegionConnectionCount==0,"Connection diagnostics were not cleaned.");
        Check(snapshot.Diagnostics.AcceptedHandshakeCount==2&&snapshot.Diagnostics.RejectedHandshakeCount==3&&snapshot.Diagnostics.RejectedProtocolMismatchCount==1&&snapshot.Diagnostics.RejectedPreHandshakeRequestCount==1&&snapshot.Diagnostics.RejectedDuplicateAccountCount==1,"Handshake rejection diagnostics mismatch.");
        Check(snapshot.Diagnostics.StalePeerRequestAcceptedCount==0&&snapshot.Diagnostics.DisconnectCleanupFailureCount==0,"Stale peer or disconnect cleanup violation.");
        _host.Stop();
        Check(!_host.IsRunning&&_host.Connections.Count==0&&_host.RegionSessions==null&&GameplaySessionV3.ActiveRegion==null,"Server shutdown leaked peer or active region runtime.");
        return $"port={port} protocol={snapshot.ProtocolVersion} accepted/rejected={snapshot.Diagnostics.AcceptedHandshakeCount}/{snapshot.Diagnostics.RejectedHandshakeCount} preHandshake/protocol/duplicate={snapshot.Diagnostics.RejectedPreHandshakeRequestCount}/{snapshot.Diagnostics.RejectedProtocolMismatchCount}/{snapshot.Diagnostics.RejectedDuplicateAccountCount} world/company/region={snapshot.PersistentWorldCount}/{snapshot.PlayerCompanyCount}/{snapshot.RegionCount} stale/cleanup={snapshot.Diagnostics.StalePeerRequestAcceptedCount}/{snapshot.Diagnostics.DisconnectCleanupFailureCount}";
    }

    private async Task<int> StartOnBoundedPort(DedicatedServerHostV3 host)
    {
        int first=31000+(int)(OS.GetProcessId()%1000);
        for(int offset=0;offset<24;offset++)
        {
            int port=first+offset;
            if(host.TryStart(port,51303,out _))return port;
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        }
        throw new InvalidOperationException("No bounded localhost ENet test port was available.");
    }

    private async Task<NetworkTestClientV3> ConnectClient(int port)
    {
        NetworkTestClientV3 client=new(port);
        _clients.Add(client);
        await PumpUntil(()=>client.IsConnected,client);
        return client;
    }

    private async Task<NetworkMessageV3> WaitFor(NetworkTestClientV3 client,NetworkMessageTypeV3 type)
    {
        NetworkMessageV3? found=null;
        await PumpUntil(()=>client.TryTake(type,out found),client);
        return found!;
    }

    private async Task DisconnectAndRemove(NetworkTestClientV3 client,bool graceful=true)
    {
        int expected=Math.Max(0,_host!.Connections.Count-1);
        if(graceful)Send(client,new NetworkMessageV3{MessageType=NetworkMessageTypeV3.ClientDisconnect,RequestId=long.MaxValue});
        else client.Close();
        await PumpUntil(()=>_host.Connections.Count<=expected,client,allowDisconnected:!graceful);
        if(graceful)client.Close();
        _clients.Remove(client);
        client.Dispose();
        await PumpFrames(4);
    }

    private async Task PumpUntil(Func<bool> condition,NetworkTestClientV3 client,bool allowDisconnected=false)
    {
        for(int index=0;index<600;index++)
        {
            _host!.Poll();
            client.Poll();
            if(condition())return;
            if(!allowDisconnected&&client.IsFailed)throw new InvalidOperationException("ENet client connection failed.");
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        }
        throw new TimeoutException("ENet fixture timed out.");
    }

    private async Task PumpFrames(int count)
    {
        for(int index=0;index<count;index++){_host!.Poll();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    }

    private static void Send(NetworkTestClientV3 client,NetworkMessageV3 message)
    {
        if(!client.Send(message))throw new InvalidOperationException("Client packet send failed.");
    }

    private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}

    private sealed class NetworkTestClientV3:IDisposable
    {
        private readonly ENetMultiplayerPeer _peer=new();
        private readonly Queue<NetworkMessageV3> _inbox=new();

        public NetworkTestClientV3(int port)
        {
            Error error=_peer.CreateClient("127.0.0.1",port);
            if(error!=Error.Ok)throw new InvalidOperationException("ENet CreateClient failed: "+error);
        }

        public bool IsConnected=>_peer.GetConnectionStatus()==MultiplayerPeer.ConnectionStatus.Connected;
        public bool IsFailed=>_peer.GetConnectionStatus()==MultiplayerPeer.ConnectionStatus.Disconnected;

        public void Poll()
        {
            if(IsFailed)return;
            _peer.Poll();
            while(_peer.GetAvailablePacketCount()>0)
                if(NetworkProtocolV3.TryDeserialize(_peer.GetPacket(),out NetworkMessageV3? message)&&message!=null)_inbox.Enqueue(message);
        }

        public bool Send(NetworkMessageV3 message)
        {
            if(!IsConnected)return false;
            _peer.SetTargetPeer(1);
            return _peer.PutPacket(NetworkProtocolV3.Serialize(message))==Error.Ok;
        }

        public bool TryTake(NetworkMessageTypeV3 type,out NetworkMessageV3? message)
        {
            message=null;
            int count=_inbox.Count;
            for(int index=0;index<count;index++)
            {
                NetworkMessageV3 item=_inbox.Dequeue();
                if(message==null&&item.MessageType==type)message=item;
                else _inbox.Enqueue(item);
            }
            return message!=null;
        }

        public void Close(){_peer.Close();}
        public void Dispose(){_peer.Dispose();}
    }
}
