using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GameplayV3.Construction;
using GameplayV3.Equipment;
using GameplayV3.Resources;

namespace GameplayV3.Production;

public enum ProductionFacilityKindV3 { ProcessingWorkbench, BasicFurnace, FieldKitchen, ApothecaryTable }
public enum ProductionOutputKindV3 { Resource = 0, Equipment = 1 }

public sealed record ProductionOutputDefinitionV3(
    ProductionOutputKindV3 Kind,
    ResourceTypeV3? ResourceType,
    int ResourceQuantity,
    string? EquipmentDefinitionId,
    int EquipmentQuantity)
{
    public static ProductionOutputDefinitionV3 Resource(ResourceTypeV3 type,int quantity)=>new(ProductionOutputKindV3.Resource,type,quantity,null,0);
    public static ProductionOutputDefinitionV3 Equipment(string definitionId)=>new(ProductionOutputKindV3.Equipment,null,0,definitionId,1);
}

public sealed record ProductionRecipeDefinitionV3(
    string RecipeId,string DisplayName,ProductionFacilityKindV3 FacilityKind,
    IReadOnlyList<StructureMaterialRequirementV3> Inputs,ProductionOutputDefinitionV3 Output,
    float BaseWorkSeconds,int DisplayOrder,string ShortDescription)
{
    public ProductionOutputKindV3 OutputKind=>Output.Kind;
    public ResourceTypeV3? OutputResource=>Output.ResourceType;
    public int OutputAmount=>Output.Kind==ProductionOutputKindV3.Resource?Output.ResourceQuantity:Output.EquipmentQuantity;
    public string? OutputEquipmentDefinitionId=>Output.EquipmentDefinitionId;
}

public static class StarterProcessingContentV3
{
    private static readonly IReadOnlyDictionary<string,ProductionRecipeDefinitionV3> Recipes=Build();
    public static IReadOnlyList<ProductionRecipeDefinitionV3> GetAll()=>Recipes.Values.OrderBy(x=>x.DisplayOrder).ToList().AsReadOnly();
    public static IReadOnlyList<ProductionRecipeDefinitionV3> GetFor(ProductionFacilityKindV3 kind)=>Recipes.Values.Where(x=>x.FacilityKind==kind).OrderBy(x=>x.DisplayOrder).ToList().AsReadOnly();
    public static bool TryGet(string id,out ProductionRecipeDefinitionV3? definition)=>Recipes.TryGetValue(id,out definition);
    public static bool TryGetFacilityKind(string definitionId,out ProductionFacilityKindV3 kind)
    {if(definitionId==StructureDefinitionCatalogV3.ProcessingWorkbenchId){kind=ProductionFacilityKindV3.ProcessingWorkbench;return true;}if(definitionId==StructureDefinitionCatalogV3.BasicFurnaceId){kind=ProductionFacilityKindV3.BasicFurnace;return true;}if(definitionId==StructureDefinitionCatalogV3.FieldKitchenId){kind=ProductionFacilityKindV3.FieldKitchen;return true;}if(definitionId==StructureDefinitionCatalogV3.ApothecaryTableId){kind=ProductionFacilityKindV3.ApothecaryTable;return true;}kind=default;return false;}
    public static string GetResourceDisplayName(ResourceTypeV3 type)=>type switch
    {ResourceTypeV3.Wood=>"\uBAA9\uC7AC",ResourceTypeV3.Stone=>"\uB3CC",ResourceTypeV3.Ration=>"\uBE44\uC0C1\uC2DD\uB7C9",ResourceTypeV3.Potato=>"\uAC10\uC790",ResourceTypeV3.IronOre=>"\uCCA0\uAD11\uC11D",ResourceTypeV3.CopperOre=>"\uAD6C\uB9AC\uAD11\uC11D",ResourceTypeV3.Coal=>"\uC11D\uD0C4",ResourceTypeV3.Clay=>"\uC810\uD1A0",ResourceTypeV3.Fiber=>"\uC12C\uC720",ResourceTypeV3.MedicinalHerb=>"\uC57D\uCD08",ResourceTypeV3.WoodPlank=>"\uBAA9\uC7AC \uD310\uC790",ResourceTypeV3.StoneBlock=>"\uC11D\uC7AC \uBE14\uB85D",ResourceTypeV3.IronIngot=>"\uCCA0 \uC8FC\uAD34",ResourceTypeV3.CopperIngot=>"\uAD6C\uB9AC \uC8FC\uAD34",ResourceTypeV3.Brick=>"\uBCBD\uB3CC",ResourceTypeV3.Cloth=>"\uCC9C",ResourceTypeV3.HerbPowder=>"\uC57D\uCD08 \uAC00\uB8E8",ResourceTypeV3.RoastedPotato=>"\uAD6C\uC6B4 \uAC10\uC790",ResourceTypeV3.PotatoStew=>"\uAC10\uC790 \uC218\uD504",ResourceTypeV3.DriedPotato=>"\uB9D0\uB9B0 \uAC10\uC790",ResourceTypeV3.Bandage=>"\uBD95\uB300",ResourceTypeV3.SimpleMedicine=>"\uAC04\uB2E8\uD55C \uCE58\uB8CC\uC57D",ResourceTypeV3.IronAxe=>"\uCCA0 \uB3C4\uB07C",ResourceTypeV3.IronPickaxe=>"\uCCA0 \uACE1\uAD2D\uC774",ResourceTypeV3.IronHammer=>"\uCCA0 \uB9DD\uCE58",_=>type.ToString()};
    public static string GetUsageText(ResourceTypeV3 type)=>type switch
    {ResourceTypeV3.RoastedPotato=>"\uC6A9\uBCD1\n\uC6A9\uBCD1\uC774 \uBC30\uACE0\uD50C \uB54C \uC790\uB3D9\uC73C\uB85C \uC12D\uCDE8\uD569\uB2C8\uB2E4.\n\n\uD6A8\uACFC\n\uD3EC\uB9CC\uB3C4 +22%",ResourceTypeV3.PotatoStew=>"\uC6A9\uBCD1\n\uC6A9\uBCD1\uC774 \uBC30\uACE0\uD50C \uB54C \uC790\uB3D9\uC73C\uB85C \uC12D\uCDE8\uD569\uB2C8\uB2E4.\n\n\uD6A8\uACFC\n\uD3EC\uB9CC\uB3C4 +45%",ResourceTypeV3.DriedPotato=>"\uC6A9\uBCD1\n\uBCF4\uAD00\uACFC \uD734\uB300\uC5D0 \uC801\uD569\uD55C \uC2DD\uB7C9\uC785\uB2C8\uB2E4.\n\n\uD6A8\uACFC\n\uD3EC\uB9CC\uB3C4 +30%",ResourceTypeV3.Bandage=>"\uC6A9\uBCD1\n\uC751\uAE09\uCC98\uCE58\uC5D0 \uC0AC\uC6A9\uD558\uB294 \uAE30\uBCF8 \uC758\uB8CC \uBB3C\uC790\uC785\uB2C8\uB2E4.",ResourceTypeV3.SimpleMedicine=>"\uC6A9\uBCD1\n\uBD80\uC0C1\uACFC \uC9C8\uBCD1 \uCE58\uB8CC\uC5D0 \uC0AC\uC6A9\uD560 \uAE30\uCD08 \uC758\uC57D\uD488\uC785\uB2C8\uB2E4.",ResourceTypeV3.IronAxe=>"\uC6A9\uBCD1\n\uB098\uBB34 \uCC44\uC9D1 \uC791\uC5C5\uC790\uAC00 \uC790\uB3D9\uC73C\uB85C \uC0AC\uC6A9\uD569\uB2C8\uB2E4.\n\n\uD6A8\uACFC\n\uC791\uC5C5 \uC18D\uB3C4 +20%\n\uC18C\uBAA8\uB418\uC9C0 \uC54A\uC74C",ResourceTypeV3.IronPickaxe=>"\uC6A9\uBCD1\n\uAD11\uBB3C \uCC44\uC9D1 \uC791\uC5C5\uC790\uAC00 \uC790\uB3D9\uC73C\uB85C \uC0AC\uC6A9\uD569\uB2C8\uB2E4.\n\n\uD6A8\uACFC\n\uC791\uC5C5 \uC18D\uB3C4 +20%\n\uC18C\uBAA8\uB418\uC9C0 \uC54A\uC74C",ResourceTypeV3.IronHammer=>"\uC6A9\uBCD1\n\uAC74\uC124 \uC791\uC5C5\uC790\uAC00 \uC790\uB3D9\uC73C\uB85C \uC0AC\uC6A9\uD569\uB2C8\uB2E4.\n\n\uD6A8\uACFC\n\uC791\uC5C5 \uC18D\uB3C4 +15%\n\uC18C\uBAA8\uB418\uC9C0 \uC54A\uC74C",_=>string.Empty};
    private static IReadOnlyDictionary<string,ProductionRecipeDefinitionV3> Build()
    {
        Dictionary<string,ProductionRecipeDefinitionV3> d=new(StringComparer.Ordinal);
        Add("process_wood_plank","\uBAA9\uC7AC \uD310\uC790",ProductionFacilityKindV3.ProcessingWorkbench,ResourceTypeV3.WoodPlank,3,5,10,"\uBAA9\uC7AC\uB97C \uB2E4\uB4EC\uC5B4 \uD310\uC790\uB85C \uB9CC\uB4ED\uB2C8\uB2E4.",(ResourceTypeV3.Wood,5));
        Add("process_stone_block","\uC11D\uC7AC \uBE14\uB85D",ProductionFacilityKindV3.ProcessingWorkbench,ResourceTypeV3.StoneBlock,3,6,20,"\uB3CC\uC744 \uADDC\uACA9\uC5D0 \uB9DE\uAC8C \uB2E4\uB4EC\uC2B5\uB2C8\uB2E4.",(ResourceTypeV3.Stone,5));
        Add("smelt_iron_ingot","\uCCA0 \uC8FC\uAD34",ProductionFacilityKindV3.BasicFurnace,ResourceTypeV3.IronIngot,2,8,30,"\uCCA0\uAD11\uC11D\uC744 \uC11D\uD0C4\uACFC \uD568\uAED8 \uC81C\uB828\uD569\uB2C8\uB2E4.",(ResourceTypeV3.IronOre,3),(ResourceTypeV3.Coal,1));
        Add("smelt_copper_ingot","\uAD6C\uB9AC \uC8FC\uAD34",ProductionFacilityKindV3.BasicFurnace,ResourceTypeV3.CopperIngot,2,8,40,"\uAD6C\uB9AC\uAD11\uC11D\uC744 \uC11D\uD0C4\uACFC \uD568\uAED8 \uC81C\uB828\uD569\uB2C8\uB2E4.",(ResourceTypeV3.CopperOre,3),(ResourceTypeV3.Coal,1));
        Add("fire_brick","\uBCBD\uB3CC",ProductionFacilityKindV3.BasicFurnace,ResourceTypeV3.Brick,4,7,50,"\uC810\uD1A0\uB97C \uAD6C\uC6CC \uBCBD\uB3CC\uB85C \uB9CC\uB4ED\uB2C8\uB2E4.",(ResourceTypeV3.Clay,4),(ResourceTypeV3.Coal,1));
        Add("weave_cloth","\uCC9C",ProductionFacilityKindV3.ProcessingWorkbench,ResourceTypeV3.Cloth,2,6,60,"\uC12C\uC720\uB97C \uC5EE\uC5B4 \uCC9C\uC73C\uB85C \uB9CC\uB4ED\uB2C8\uB2E4.",(ResourceTypeV3.Fiber,4));
        Add("grind_herb_powder","\uC57D\uCD08 \uAC00\uB8E8",ProductionFacilityKindV3.ProcessingWorkbench,ResourceTypeV3.HerbPowder,2,5,70,"\uC57D\uCD08\uB97C \uACE0\uAC8C \uAC08\uC544 \uAC00\uB8E8\uB85C \uB9CC\uB4ED\uB2C8\uB2E4.",(ResourceTypeV3.MedicinalHerb,3));
        Add("cook_roasted_potato","\uAD6C\uC6B4 \uAC10\uC790",ProductionFacilityKindV3.FieldKitchen,ResourceTypeV3.RoastedPotato,3,5,110,"\uBD88\uC5D0 \uC775\uD600 \uBA39\uAE30 \uC26C\uC6B4 \uAC10\uC790\uC785\uB2C8\uB2E4.",(ResourceTypeV3.Potato,3),(ResourceTypeV3.Wood,1));
        Add("cook_potato_stew","\uAC10\uC790 \uC218\uD504",ProductionFacilityKindV3.FieldKitchen,ResourceTypeV3.PotatoStew,2,8,120,"\uD3EC\uB9CC\uB3C4\uAC00 \uB192\uC740 \uB530\uB73B\uD55C \uC218\uD504\uC785\uB2C8\uB2E4.",(ResourceTypeV3.Potato,4),(ResourceTypeV3.MedicinalHerb,1),(ResourceTypeV3.Wood,1));
        Add("cook_dried_potato","\uB9D0\uB9B0 \uAC10\uC790",ProductionFacilityKindV3.FieldKitchen,ResourceTypeV3.DriedPotato,4,7,130,"\uBCF4\uAD00\uACFC \uD734\uB300\uC5D0 \uC801\uD569\uD55C \uB9D0\uB9B0 \uC2DD\uB7C9\uC785\uB2C8\uB2E4.",(ResourceTypeV3.Potato,5),(ResourceTypeV3.Wood,1));
        Add("craft_bandage","\uBD95\uB300",ProductionFacilityKindV3.ApothecaryTable,ResourceTypeV3.Bandage,2,5,140,"\uC751\uAE09\uCC98\uCE58\uC5D0 \uC0AC\uC6A9\uD558\uB294 \uAE30\uBCF8 \uC758\uB8CC \uBB3C\uC790\uC785\uB2C8\uB2E4.",(ResourceTypeV3.Cloth,1),(ResourceTypeV3.HerbPowder,1));
        Add("craft_simple_medicine","\uAC04\uB2E8\uD55C \uCE58\uB8CC\uC57D",ProductionFacilityKindV3.ApothecaryTable,ResourceTypeV3.SimpleMedicine,1,8,150,"\uBD80\uC0C1\uACFC \uC9C8\uBCD1 \uCE58\uB8CC\uC5D0 \uC0AC\uC6A9\uD560 \uAE30\uCD08 \uC758\uC57D\uD488\uC785\uB2C8\uB2E4.",(ResourceTypeV3.HerbPowder,2),(ResourceTypeV3.Cloth,1));
        Add("craft_iron_axe","\uCCA0 \uB3C4\uB07C",ProductionFacilityKindV3.ProcessingWorkbench,ResourceTypeV3.IronAxe,1,8,160,"\uB098\uBB34 \uCC44\uC9D1 \uC18D\uB3C4\uB97C \uB192\uC774\uB294 \uACF5\uC720 \uB3C4\uAD6C\uC785\uB2C8\uB2E4.",(ResourceTypeV3.WoodPlank,2),(ResourceTypeV3.IronIngot,1));
        Add("craft_iron_hammer","\uCCA0 \uB9DD\uCE58",ProductionFacilityKindV3.ProcessingWorkbench,ResourceTypeV3.IronHammer,1,7,180,"\uAC74\uC124 \uC18D\uB3C4\uB97C \uB192\uC774\uB294 \uACF5\uC720 \uB3C4\uAD6C\uC785\uB2C8\uB2E4.",(ResourceTypeV3.WoodPlank,1),(ResourceTypeV3.IronIngot,1));
        AddEquipment("craft_iron_pickaxe","\uCCA0 \uACE1\uAD2D\uC774 \uC81C\uC791",StarterEquipmentContentV3.IronPickaxeDefinitionId,15,190,"\uCCA0 \uC8FC\uAD34\uC640 \uBAA9\uC7AC \uD310\uC790\uB85C \uCC44\uC9D1\uC6A9 \uACE1\uAD2D\uC774\uB97C \uC81C\uC791\uD569\uB2C8\uB2E4.",(ResourceTypeV3.IronIngot,1),(ResourceTypeV3.WoodPlank,1));
        AddEquipment("craft_padded_armor","\uB204\uBE44\uAC11\uC637 \uC81C\uC791",StarterEquipmentContentV3.PaddedArmorDefinitionId,16,200,"\uC5EC\uB7EC \uACB9\uC758 \uCC9C\uC744 \uB367\uB300\uC5B4 \uAE30\uBCF8 \uBC29\uC5B4\uAD6C\uB97C \uC81C\uC791\uD569\uB2C8\uB2E4.",(ResourceTypeV3.Cloth,3));
        AddEquipment("craft_iron_sword","\uCCA0\uAC80 \uC81C\uC791",StarterEquipmentContentV3.IronSwordDefinitionId,18,210,"\uCCA0 \uC8FC\uAD34\uC640 \uBAA9\uC7AC \uD310\uC790\uB85C \uAE30\uBCF8\uC801\uC778 \uCCA0\uAC80\uC744 \uC81C\uC791\uD569\uB2C8\uB2E4.",(ResourceTypeV3.IronIngot,2),(ResourceTypeV3.WoodPlank,1));
        return new ReadOnlyDictionary<string,ProductionRecipeDefinitionV3>(d);
        void Add(string id,string name,ProductionFacilityKindV3 kind,ResourceTypeV3 output,int amount,float seconds,int order,string description,params (ResourceTypeV3 Type,int Amount)[] inputs)=>AddCore(id,name,kind,ProductionOutputDefinitionV3.Resource(output,amount),seconds,order,description,inputs);
        void AddEquipment(string id,string name,string equipmentDefinitionId,float seconds,int order,string description,params (ResourceTypeV3 Type,int Amount)[] inputs)=>AddCore(id,name,ProductionFacilityKindV3.ProcessingWorkbench,ProductionOutputDefinitionV3.Equipment(equipmentDefinitionId),seconds,order,description,inputs);
        void AddCore(string id,string name,ProductionFacilityKindV3 kind,ProductionOutputDefinitionV3 output,float seconds,int order,string description,params (ResourceTypeV3 Type,int Amount)[] inputs){d.Add(id,new(id,name,kind,inputs.Select(x=>new StructureMaterialRequirementV3(x.Type,x.Amount)).ToList().AsReadOnly(),output,seconds,order,description));}
    }
}
