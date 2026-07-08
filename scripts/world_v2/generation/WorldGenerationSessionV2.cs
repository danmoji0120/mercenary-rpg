namespace WorldV2;

public static class WorldGenerationSessionV2
{
    private static WorldGenerationRequestV2? _pendingRequest;
    private static WorldGenerationRequestV2? _activeRequest;

    public static WorldGenerationRequestV2? PendingRequest => _pendingRequest;
    public static WorldGenerationRequestV2? ActiveRequest => _activeRequest;

    public static void SetPendingRequest(WorldGenerationRequestV2 request)
    {
        _pendingRequest = request;
    }

    public static WorldGenerationRequestV2 ConsumePendingOrCreateDefault(int fallbackSeed)
    {
        WorldGenerationRequestV2 request = _pendingRequest ?? WorldGenerationRequestV2.CreateDevDefault(fallbackSeed);
        _pendingRequest = null;
        _activeRequest = request;
        return request;
    }
}
