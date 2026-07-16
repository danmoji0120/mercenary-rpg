using System;
using System.Collections.Generic;
using GameplayV3.Company;
using GameplayV3.Construction;
using GameplayV3.Control;
using GameplayV3.Farming.Runtime;
using GameplayV3.Mercenary;
using GameplayV3.Navigation;
using GameplayV3.Needs;
using GameplayV3.Needs.Runtime;
using GameplayV3.Resources;
using GameplayV3.Stockpile;
using GameplayV3.Work;
using Godot;
using WorldV2;

namespace GameplayV3.Farming;

public static class FarmingSelfCheckV3
{
    public static bool TryValidate(out string reason)
    {
        if(!FoodCoreSelfCheckV3.TryValidate(out reason))return false;
        CompanySessionV3 companies=new();
        if(!companies.TryInitializeLocalSinglePlayer(out _,out reason))return false;
        string player=companies.LocalPlayer!.PlayerId,company=companies.LocalContext.LocalCompanyId!;
        MercenarySessionV3 mercenaries=new(companies.CompanyRegistry);
        if(!CreateMercenary(mercenaries,company,new(5,5),out string mercenaryId,out reason))return false;
        MercenaryControlSessionV3 control=new(1,companies,mercenaries);
        ResourceSessionV3 resources=new();StockpileSessionV3 stockpiles=new();
        MercenaryWorkSessionV3 work=new(1,companies,mercenaries,resources,stockpiles,control);control.AttachWorkSession(work);
        FarmSessionV3 farm=new(1);GlobalCellCoord cell=new(new Vector2I(5,5));
        if(!farm.Plots.TryCreate(company,CropCatalogV3.PotatoCropId,new[]{cell},FarmSessionV3.MaxFarmCellsPerCompany,out _,out reason))return false;
        if(!farm.Plots.TryGetCrop(cell,out var crop)||crop==null||crop.Stage!=CropStageV3.Empty){reason="Initial crop state mismatch.";return false;}
        FarmingWorkCoordinatorV3 coordinator=new(farm,resources,work,control,mercenaries,new TestQuery(),player,company);
        work.AttachExternalWorkSupersede(id=>coordinator.Cancel(id,"SupersededByNewWork"));
        if(!coordinator.TryIssue(cell,new[]{mercenaryId},out reason)||!coordinator.TryGet(mercenaryId,out var sowing)||sowing==null||sowing.Action!=FarmingActionV3.Sowing){reason=$"Sowing issue failed: {reason}";return false;}
        coordinator.Tick(sowing.RequiredDurationSeconds);
        if(crop.Stage!=CropStageV3.Growing||farm.Reservations.Count!=0||coordinator.ActiveCount!=0){reason="Sowing completion invariants failed.";return false;}
        for(int second=0;second<119;second++)crop.AdvanceGrowth(1f,120f);
        if(crop.Stage!=CropStageV3.Growing){reason="Crop matured before 120 seconds.";return false;}
        crop.AdvanceGrowth(1f,120f);
        if(crop.Stage!=CropStageV3.Mature||Math.Abs(crop.GrowthNormalized-1f)>.0001f){reason="Crop did not mature at 120 seconds.";return false;}
        if(!coordinator.TryIssue(cell,new[]{mercenaryId},out reason)||!coordinator.TryGet(mercenaryId,out var harvesting)||harvesting==null||harvesting.Action!=FarmingActionV3.Harvesting){reason=$"Harvest issue failed: {reason}";return false;}
        coordinator.Tick(harvesting.RequiredDurationSeconds);
        if(crop.Stage!=CropStageV3.Empty||resources.GroundStacks.GetTotalAmount(ResourceTypeV3.Potato)!=3||resources.GenerationLedger.GetGeneratedAmount(ResourceTypeV3.Potato)!=3||farm.Reservations.Count!=0){reason="Harvest output or cleanup failed.";return false;}
        MercenaryNeedsSessionV3 needs=new(1);needs.EnsureMercenaries(mercenaries.Registry);needs.Hunger.EnsureForMercenary(mercenaryId).TrySetHunger(.65f,out _);
        EatingWorkCoordinatorV3 eating=new(needs,resources,mercenaries,control,work,new TestQuery(),player,company);
        if(!eating.TryIssueManual(mercenaryId,out reason)||!eating.TryGet(mercenaryId,out var meal)||meal==null||meal.ResourceType!=ResourceTypeV3.Potato||meal.PlannedUnits!=3||Math.Abs(meal.EatingDurationSeconds-3f)>.001f){reason=$"Potato meal planning failed: {reason}";return false;}
        eating.Tick(2.99f);
        if(resources.GroundStacks.GetTotalAmount(ResourceTypeV3.Potato)!=3){reason="Potato consumed before meal duration.";return false;}
        eating.Tick(.01f);
        if(resources.GroundStacks.GetTotalAmount(ResourceTypeV3.Potato)!=0||resources.ConsumptionLedger.GetConsumedAmount(ResourceTypeV3.Potato)!=3||Math.Abs(needs.Hunger.GetHunger(mercenaryId)-.20f)>.001f){reason="Potato eating completion failed.";return false;}int potatoMismatch=resources.GenerationLedger.GetGeneratedAmount(ResourceTypeV3.Potato)-resources.GroundStacks.GetTotalAmount(ResourceTypeV3.Potato)-work.Carries.GetTotalAmount(ResourceTypeV3.Potato)-resources.ConsumptionLedger.GetConsumedAmount(ResourceTypeV3.Potato);if(potatoMismatch!=0){reason="Potato conservation mismatch.";return false;}
        ResourceSessionV3 partialResources=new();partialResources.GroundStacks.TryAddStack(ResourceTypeV3.Potato,2,cell,out _,out _,out _);needs.Hunger.EnsureForMercenary(mercenaryId).TrySetHunger(.65f,out _);
        EatingWorkCoordinatorV3 partialEating=new(needs,partialResources,mercenaries,control,work,new TestQuery(),player,company);
        if(!partialEating.TryIssueManual(mercenaryId,out reason)||!partialEating.TryGet(mercenaryId,out var partial)||partial==null||partial.IsCompleteMeal||partial.PlannedUnits!=2){reason="Partial meal was not planned.";return false;}
        partialEating.Tick(2f);
        if(Math.Abs(needs.Hunger.GetHunger(mercenaryId)-.35f)>.001f||partialResources.ConsumptionLedger.GetConsumedAmount(ResourceTypeV3.Potato)!=2){reason="Partial meal result mismatch.";return false;}
        if(farm.Plots.TryRemoveEmpty(cell,farm.Reservations,out reason)==false||farm.Plots.CellCount!=0){reason="Empty farm cell removal failed.";return false;}
        foreach(WorldMapSizePresetV2 preset in Enum.GetValues<WorldMapSizePresetV2>())
        {
            Rect2I bounds=WorldMapSizeDefinitionV2.FromPreset(preset).CellBounds;Vector2I center=bounds.GetCenter();FarmSessionV3 presetFarm=new(1);
            GlobalCellCoord[] cells={new(center),new(center+Vector2I.Right),new(center+Vector2I.Down),new(center+Vector2I.One)};
            if(!presetFarm.Plots.TryCreate(company,CropCatalogV3.PotatoCropId,cells,FarmSessionV3.MaxFarmCellsPerCompany,out var plot,out reason)||plot?.CellCount!=4){reason=$"{preset} farm plot failed: {reason}";return false;}
            foreach(GlobalCellCoord presetCell in cells)if(!bounds.HasPoint(presetCell.Value)){reason=$"{preset} farm cell escaped bounds.";return false;}
        }
        reason=string.Empty;return true;
    }

    private static bool CreateMercenary(MercenarySessionV3 session,string company,Vector2I cell,out string id,out string reason)
    {
        id=MercenaryIdFactoryV3.CreateMercenaryId();DateTime created=DateTime.UtcNow;
        MercenaryAttributeSetV3.TryCreate(10,10,10,10,10,out var attributes,out _);
        MercenaryWorkSkillSetV3.TryCreate(8,8,8,12,8,8,8,out var skills,out _);
        if(!MercenaryProfileV3.TryCreate(id,"Farmer","placeholder",attributes,skills,created,out var profile,out reason))return false;
        if(!MercenaryStateV3.TryCreate(id,company,new(cell),MercenaryActivityStateV3.Idle,created,out var state,out reason))return false;
        return session.Registry.TryRegisterMercenary(profile,state,out reason);
    }

    private sealed class TestQuery:IMercenaryNavigationWorldQueryV3
    {
        public bool IsInsideWorld(Vector2I cell)=>cell.X>=0&&cell.Y>=0&&cell.X<20&&cell.Y<20;
        public MercenaryNavigationCellInfoV3 GetCellInfo(Vector2I cell)=>new(IsInsideWorld(cell),IsInsideWorld(cell),1f,TileType.Grass,BiomeKindV3.Plains);
        public bool IsWalkable(Vector2I cell)=>IsInsideWorld(cell);
        public float GetTraversalMultiplier(Vector2I cell)=>1f;
    }
}
