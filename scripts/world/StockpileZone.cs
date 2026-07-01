using System.Collections.Generic;
using Godot;

public sealed class StockpileZone
{
    public int ZoneId { get; set; }
    public string DisplayName { get; set; } = "";
    public HashSet<Vector2I> Cells { get; } = new();
    public Rect2I Bounds { get; set; }
    public StoragePolicy Policy { get; set; } = new(StoragePolicyPreset.All, StoragePriority.Low);
    public int CapacityPerCell { get; set; } = 30;
    public Dictionary<BaseResourceType, int> StoredResources { get; } = new();

    public int WeightCapacity => Mathf.Max(0, Cells.Count * CapacityPerCell);
}
