using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

namespace GameplayV3.Equipment;

public sealed class RegionEquipmentLocationStoreV3
{
    internal RegionEquipmentLocationStoreV3(string regionId)
    {
        if(string.IsNullOrWhiteSpace(regionId))throw new ArgumentException("RegionId is required.",nameof(regionId));
        RegionId=regionId;
    }

    public string RegionId{get;}
    internal Dictionary<string,List<string>> OutputIdsByFacility{get;}=new(StringComparer.Ordinal);
    internal Dictionary<Vector2I,SortedSet<string>> GroundIdsByCell{get;}=new();
    internal Dictionary<string,SortedSet<string>> GroundIdsByCompany{get;}=new(StringComparer.Ordinal);
    internal Dictionary<string,SortedSet<string>> StorageIdsByZone{get;}=new(StringComparer.Ordinal);
    internal Dictionary<Vector2I,SortedSet<string>> StorageIdsByCell{get;}=new();
    internal Dictionary<string,SortedSet<string>> StoredIdsByCompany{get;}=new(StringComparer.Ordinal);

    public IReadOnlyList<string> GetGroundEquipmentAtCell(Vector2I cell)=>Copy(GroundIdsByCell,cell);
    public IReadOnlyList<string> GetStoredEquipmentAtCell(Vector2I cell)=>Copy(StorageIdsByCell,cell);
    public IReadOnlyList<string> GetFacilityOutput(string facilityId)=>OutputIdsByFacility.TryGetValue(facilityId,out List<string>? ids)?new ReadOnlyCollection<string>(new List<string>(ids)):Array.Empty<string>();
    public IReadOnlyList<string> GetAllLocatedInstanceIds()
    {
        SortedSet<string> ids=new(StringComparer.Ordinal);
        foreach(SortedSet<string> values in GroundIdsByCompany.Values)ids.UnionWith(values);
        foreach(SortedSet<string> values in StoredIdsByCompany.Values)ids.UnionWith(values);
        foreach(List<string> values in OutputIdsByFacility.Values)ids.UnionWith(values);
        return new ReadOnlyCollection<string>(new List<string>(ids));
    }
    public int GroundCount=>CountUnique(GroundIdsByCompany);
    public int StorageCount=>CountUnique(StoredIdsByCompany);
    public int FacilityOutputCount{get{int count=0;foreach(List<string> ids in OutputIdsByFacility.Values)count+=ids.Count;return count;}}

    private static IReadOnlyList<string> Copy<TKey>(Dictionary<TKey,SortedSet<string>> index,TKey key) where TKey:notnull=>
        index.TryGetValue(key,out SortedSet<string>? ids)?new ReadOnlyCollection<string>(new List<string>(ids)):Array.Empty<string>();
    private static int CountUnique(Dictionary<string,SortedSet<string>> index){int count=0;foreach(SortedSet<string> ids in index.Values)count+=ids.Count;return count;}
}
