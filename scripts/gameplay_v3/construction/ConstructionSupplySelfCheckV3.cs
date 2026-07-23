using GameplayV3.Construction.Runtime;
using GameplayV3.Resources;

namespace GameplayV3.Construction;

public static class ConstructionSupplySelfCheckV3
{
    public static bool TryValidate(out string reason)
    {
        ConstructionMaterialBufferV3 selectable=new(new[]{new StructureMaterialRequirementV3(ResourceTypeV3.WoodPlank,5)});
        if(!ConstructionWorkCoordinatorV3.TryGetNextMissingMaterial(selectable,out ResourceTypeV3 selected,out int selectedAmount)||selected!=ResourceTypeV3.WoodPlank||selectedAmount!=5){reason="Selectable material was not resolved by resource id.";return false;}
        ConstructionMaterialBufferV3 fixedRecipe=new(new[]{new StructureMaterialRequirementV3(ResourceTypeV3.Stone,30),new StructureMaterialRequirementV3(ResourceTypeV3.Wood,10)});
        if(!fixedRecipe.TryDeliver(ResourceTypeV3.Stone,30,out _)||!ConstructionWorkCoordinatorV3.TryGetNextMissingMaterial(fixedRecipe,out ResourceTypeV3 remaining,out int remainingAmount)||remaining!=ResourceTypeV3.Wood||remainingAmount!=10){reason="Fixed recipe did not advance to its remaining resource.";return false;}
        if(!fixedRecipe.TryDeliver(ResourceTypeV3.Wood,10,out _)||ConstructionWorkCoordinatorV3.TryGetNextMissingMaterial(fixedRecipe,out _,out _)){reason="Complete material buffer still requested supply.";return false;}
        reason="";return true;
    }
}
