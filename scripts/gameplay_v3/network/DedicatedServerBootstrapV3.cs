using System;
using GameplayV3.Checkpoint;
using Godot;

namespace GameplayV3.Network;

public enum GameplayExecutionRoleV3
{
    LocalStandalone=1,
    DedicatedServer=2,
    NetworkClient=3
}

public partial class DedicatedServerBootstrapV3:Node
{
    private DedicatedServerHostV3? _host;
    public GameplayExecutionRoleV3 Role{get;private set;}=GameplayExecutionRoleV3.LocalStandalone;

    public override void _Ready()
    {
        string[] engineArgs=OS.GetCmdlineArgs();
        string[] userArgs=OS.GetCmdlineUserArgs();
        string[] args=new string[engineArgs.Length+userArgs.Length];
        Array.Copy(engineArgs,args,engineArgs.Length);
        Array.Copy(userArgs,0,args,engineArgs.Length,userArgs.Length);
        Role=ResolveRole(args);
        if(Role!=GameplayExecutionRoleV3.DedicatedServer)return;
        int port=ParsePort(args,27020);
        bool loadCheckpoint=Array.Exists(args,x=>x=="--load-checkpoint"),newWorld=Array.Exists(args,x=>x=="--new-world");
        string? checkpointPath=ParseValue(args,"--checkpoint-path=");
        if(loadCheckpoint&&newWorld||loadCheckpoint&&string.IsNullOrWhiteSpace(checkpointPath))
        {
            GD.PushError("[DedicatedServerV3] startup failed: InvalidCheckpointMode");
            GetTree().Quit(3);return;
        }
        _host=new DedicatedServerHostV3();
        bool started=loadCheckpoint
            ?_host.TryStartFromCheckpoint(port,checkpointPath!,new ServerCheckpointStoreV3(),out string reason)
            :_host.TryStart(port,System.Environment.TickCount,out reason);
        if(!started)
        {
            GD.PushError($"[DedicatedServerV3] startup failed: {reason}");
            GetTree().Quit(3);
            return;
        }
        GD.Print($"[DedicatedServerV3] listening port={port} protocol={NetworkProtocolV3.ProtocolVersion} world={_host.World!.WorldId}");
    }

    public override void _Process(double delta)=>_host?.Poll();
    public override void _ExitTree(){_host?.Dispose();_host=null;}

    public static GameplayExecutionRoleV3 ResolveRole(string[] args)
    {
        foreach(string arg in args)
        {
            if(arg=="--dedicated-server"||arg=="--server")return GameplayExecutionRoleV3.DedicatedServer;
            if(arg=="--network-client")return GameplayExecutionRoleV3.NetworkClient;
        }
        return GameplayExecutionRoleV3.LocalStandalone;
    }

    private static int ParsePort(string[] args,int fallback)
    {
        foreach(string arg in args)
            if(arg.StartsWith("--port=",StringComparison.Ordinal)&&int.TryParse(arg.AsSpan(7),out int port)&&port is >0 and <=65535)return port;
        return fallback;
    }
    private static string? ParseValue(string[] args,string prefix)
    {
        foreach(string arg in args)if(arg.StartsWith(prefix,StringComparison.Ordinal)&&arg.Length>prefix.Length)return arg[prefix.Length..];
        return null;
    }
}
