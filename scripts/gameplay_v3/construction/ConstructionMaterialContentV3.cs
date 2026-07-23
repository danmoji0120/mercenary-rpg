using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GameplayV3.Production;
using GameplayV3.Resources;
using Godot;

namespace GameplayV3.Construction;

[Flags]
public enum ConstructionMaterialTagV3 { None=0, Structural=1, Flooring=2, Furniture=4, Door=8 }
public enum ConstructionCostModeV3 { FixedRecipe, SelectableMaterial }
public enum ConstructionDisplayCategoryV3 { Structure, FurnitureAndLiving, Production, Zone }

public sealed record ConstructionMaterialDefinitionV3(ResourceTypeV3 ResourceType,string DisplayName,ConstructionMaterialTagV3 Tags,float HealthMultiplier,float WorkMultiplier,string VisualStyleKey,bool IsFlammable,string ShortDescription,int DisplayOrder);

public static class ConstructionMaterialContentV3
{
    private static readonly IReadOnlyDictionary<ResourceTypeV3,ConstructionMaterialDefinitionV3> Definitions=Build();
    public static int Count=>Definitions.Count;
    public static bool TryGet(ResourceTypeV3 type,out ConstructionMaterialDefinitionV3? definition)=>Definitions.TryGetValue(type,out definition);
    public static IReadOnlyList<ConstructionMaterialDefinitionV3> GetAllowed(ConstructionMaterialTagV3 tags)=>Definitions.Values.Where(x=>(x.Tags&tags)!=0).OrderBy(x=>x.DisplayOrder).ToList().AsReadOnly();
    public static string GetResourceDisplayName(ResourceTypeV3 type)=>StarterProcessingContentV3.GetResourceDisplayName(type);
    public static string GetStructureDisplayName(StructureDefinitionV3 definition,ResourceTypeV3? material)
    {if(material==null||!TryGet(material.Value,out var value)||value==null)return definition.DisplayName;string noun=definition.DefinitionId==StructureDefinitionCatalogV3.WoodenDoorId?"문":definition.DefinitionId==StructureDefinitionCatalogV3.WoodenWallId?"벽":definition.DisplayName;string prefix=material.Value switch{ResourceTypeV3.WoodPlank=>"목재",ResourceTypeV3.StoneBlock=>"석재 블록",ResourceTypeV3.Brick=>"벽돌",ResourceTypeV3.IronIngot=>"철제",_=>value.DisplayName};return $"{prefix} {noun}";}
    public static float ResolveMaxHealth(StructureDefinitionV3 definition,ResourceTypeV3? material)=>definition.BaseMaxHealth*(material!=null&&TryGet(material.Value,out var d)&&d!=null?d.HealthMultiplier:1f);
    public static float ResolveWorkSeconds(StructureDefinitionV3 definition,ResourceTypeV3? material)=>definition.BaseConstructionDurationSeconds*(material!=null&&TryGet(material.Value,out var d)&&d!=null?d.WorkMultiplier:1f);
    public static Color ResolveTint(ResourceTypeV3? material)=>material switch{ResourceTypeV3.WoodPlank=>new Color("a5744f"),ResourceTypeV3.StoneBlock=>new Color("a7adb5"),ResourceTypeV3.Brick=>new Color("a85542"),ResourceTypeV3.IronIngot=>new Color("58636f"),_=>new Color("a5744f")};
    private static IReadOnlyDictionary<ResourceTypeV3,ConstructionMaterialDefinitionV3> Build()=>new ReadOnlyDictionary<ResourceTypeV3,ConstructionMaterialDefinitionV3>(new Dictionary<ResourceTypeV3,ConstructionMaterialDefinitionV3>
    {
        [ResourceTypeV3.WoodPlank]=new(ResourceTypeV3.WoodPlank,"목재 판자",ConstructionMaterialTagV3.Structural|ConstructionMaterialTagV3.Flooring|ConstructionMaterialTagV3.Furniture|ConstructionMaterialTagV3.Door,.75f,.75f,"wood",true,"빠르게 건설할 수 있지만 내구도가 낮고 불에 취약합니다.",10),
        [ResourceTypeV3.StoneBlock]=new(ResourceTypeV3.StoneBlock,"석재 블록",ConstructionMaterialTagV3.Structural|ConstructionMaterialTagV3.Flooring|ConstructionMaterialTagV3.Door,1.4f,1.25f,"stone",false,"건설은 느리지만 내구도가 높습니다.",20),
        [ResourceTypeV3.Brick]=new(ResourceTypeV3.Brick,"벽돌",ConstructionMaterialTagV3.Structural|ConstructionMaterialTagV3.Flooring|ConstructionMaterialTagV3.Door,1.15f,1f,"brick",false,"건설 속도와 내구도가 균형 잡힌 재질입니다.",30),
        [ResourceTypeV3.IronIngot]=new(ResourceTypeV3.IronIngot,"철 주괴",ConstructionMaterialTagV3.Structural|ConstructionMaterialTagV3.Door,1.8f,1.4f,"iron",false,"비싸고 건설이 느리지만 매우 튼튼합니다.",40)
    });
}

public static class ConstructionMaterialSelfCheckV3
{
    public static bool TryValidate(out string reason){if(ConstructionMaterialContentV3.Count!=4){reason="Material definition count mismatch.";return false;}foreach(ResourceTypeV3 type in new[]{ResourceTypeV3.WoodPlank,ResourceTypeV3.StoneBlock,ResourceTypeV3.Brick,ResourceTypeV3.IronIngot})if(!ConstructionMaterialContentV3.TryGet(type,out var d)||d==null||string.IsNullOrWhiteSpace(d.DisplayName)||d.HealthMultiplier<=0||d.WorkMultiplier<=0){reason=$"Invalid material {type}.";return false;}var catalog=new StructureDefinitionCatalogV3();if(!catalog.TryGetDefinition(StructureDefinitionCatalogV3.WoodenWallId,out var wall)||wall?.CostMode!=ConstructionCostModeV3.SelectableMaterial||wall.ResolveRequirements(ResourceTypeV3.StoneBlock)[0].ResourceType!=ResourceTypeV3.StoneBlock){reason="Wall selectable material policy failed.";return false;}if(!catalog.TryGetDefinition(StructureDefinitionCatalogV3.ProcessingWorkbenchId,out var fixedDefinition)||fixedDefinition?.CostMode!=ConstructionCostModeV3.FixedRecipe){reason="Fixed recipe policy changed.";return false;}reason=string.Empty;return true;}
}
