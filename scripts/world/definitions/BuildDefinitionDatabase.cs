using System;
using System.Collections.Generic;
using Godot;

public static class BuildDefinitionDatabase
{
    private static readonly BuildMaterialType[] MaterialOrder =
    {
        BuildMaterialType.Wood,
        BuildMaterialType.Stone,
        BuildMaterialType.Metal
    };

    private static readonly BuildCost EmptyCost = Cost();

    private static readonly Dictionary<TileBuildType, BuildDefinitionEntry> Entries = new()
    {
        { TileBuildType.Floor, Entry(TileBuildType.Floor, "\uBC14\uB2E5", Cost((BaseResourceType.Wood, 1)), Vector2I.One, true) },
        { TileBuildType.Wall, Entry(TileBuildType.Wall, "\uBCBD", Cost((BaseResourceType.Stone, 2)), Vector2I.One, true) },
        { TileBuildType.Door, Entry(TileBuildType.Door, "\uBB38", Cost((BaseResourceType.Wood, 2), (BaseResourceType.IronOre, 1)), Vector2I.One, true) },
        { TileBuildType.Bed, Entry(TileBuildType.Bed, "\uAE30\uBCF8 \uCE68\uB300", Cost((BaseResourceType.Wood, 5)), new Vector2I(1, 2), true) },
        { TileBuildType.Storage, Entry(TileBuildType.Storage, "\uAE30\uBCF8 \uCC3D\uACE0", Cost((BaseResourceType.Wood, 4)), new Vector2I(2, 2), true) },
        { TileBuildType.GuardPost, Entry(TileBuildType.GuardPost, "\uACBD\uBE44\uCD08\uC18C", Cost((BaseResourceType.Wood, 3), (BaseResourceType.Stone, 2)), Vector2I.One, false) },
        { TileBuildType.ImprovisedBed, Entry(TileBuildType.ImprovisedBed, "\uAE09\uC870 \uCE68\uB300", Cost((BaseResourceType.Wood, 2)), Vector2I.One, false) },
        { TileBuildType.LuxuryBed, Entry(TileBuildType.LuxuryBed, "\uACE0\uAE09 \uCE68\uB300", Cost((BaseResourceType.Wood, 10), (BaseResourceType.IronIngot, 2)), new Vector2I(2, 2), false) },
        { TileBuildType.SmallCabinet, Entry(TileBuildType.SmallCabinet, nameof(TileBuildType.SmallCabinet), Cost((BaseResourceType.Wood, 3)), Vector2I.One, false) },
        { TileBuildType.SmallDesk, Entry(TileBuildType.SmallDesk, nameof(TileBuildType.SmallDesk), Cost((BaseResourceType.Wood, 4)), new Vector2I(2, 1), false) },
        { TileBuildType.Lamp, Entry(TileBuildType.Lamp, nameof(TileBuildType.Lamp), Cost((BaseResourceType.IronOre, 1)), Vector2I.One, false) },
        { TileBuildType.Chair, Entry(TileBuildType.Chair, nameof(TileBuildType.Chair), Cost((BaseResourceType.Wood, 1)), Vector2I.One, false) },
        { TileBuildType.SmallDiningTable, Entry(TileBuildType.SmallDiningTable, nameof(TileBuildType.SmallDiningTable), Cost((BaseResourceType.Wood, 4)), new Vector2I(2, 1), false) },
        { TileBuildType.LongDiningTable, Entry(TileBuildType.LongDiningTable, nameof(TileBuildType.LongDiningTable), Cost((BaseResourceType.Wood, 8)), new Vector2I(3, 1), false) },
        { TileBuildType.ServingCounter, Entry(TileBuildType.ServingCounter, nameof(TileBuildType.ServingCounter), Cost((BaseResourceType.Wood, 4), (BaseResourceType.IronOre, 1)), new Vector2I(2, 1), false) },
        { TileBuildType.KitchenCounter, Entry(TileBuildType.KitchenCounter, nameof(TileBuildType.KitchenCounter), Cost((BaseResourceType.Wood, 4), (BaseResourceType.Stone, 2)), new Vector2I(2, 1), false) },
        { TileBuildType.Hearth, Entry(TileBuildType.Hearth, nameof(TileBuildType.Hearth), Cost((BaseResourceType.Stone, 8), (BaseResourceType.IronOre, 2)), new Vector2I(2, 2), false) },
        { TileBuildType.IngredientCrate, Entry(TileBuildType.IngredientCrate, "\uC2DD\uC7AC\uB8CC \uC0C1\uC790", Cost((BaseResourceType.Wood, 3)), Vector2I.One, false) },
        { TileBuildType.SmallChest, Entry(TileBuildType.SmallChest, "\uC791\uC740 \uC0C1\uC790", Cost((BaseResourceType.Wood, 3)), Vector2I.One, true) },
        { TileBuildType.LargeStorage, Entry(TileBuildType.LargeStorage, "\uB300\uD615 \uCC3D\uACE0", Cost((BaseResourceType.Wood, 16), (BaseResourceType.IronIngot, 4)), new Vector2I(3, 2), false) },
        { TileBuildType.MaterialShelf, Entry(TileBuildType.MaterialShelf, "\uC790\uC7AC \uC120\uBC18", Cost((BaseResourceType.Wood, 5)), new Vector2I(2, 1), false) },
        { TileBuildType.WeaponRack, Entry(TileBuildType.WeaponRack, nameof(TileBuildType.WeaponRack), Cost((BaseResourceType.Wood, 4), (BaseResourceType.IronIngot, 2)), new Vector2I(2, 1), false) },
        { TileBuildType.MedicineShelf, Entry(TileBuildType.MedicineShelf, "\uC57D\uD488 \uC120\uBC18", Cost((BaseResourceType.Wood, 3)), Vector2I.One, false) },
        { TileBuildType.Workbench, Entry(TileBuildType.Workbench, "\uC791\uC5C5\uB300", Cost((BaseResourceType.Wood, 6), (BaseResourceType.IronOre, 1)), new Vector2I(2, 1), true) },
        { TileBuildType.LargeWorkbench, Entry(TileBuildType.LargeWorkbench, nameof(TileBuildType.LargeWorkbench), Cost((BaseResourceType.Wood, 12), (BaseResourceType.IronIngot, 4)), new Vector2I(3, 2), false) },
        { TileBuildType.RepairBench, Entry(TileBuildType.RepairBench, nameof(TileBuildType.RepairBench), Cost((BaseResourceType.Wood, 5), (BaseResourceType.IronIngot, 3)), new Vector2I(2, 1), false) },
        { TileBuildType.Forge, Entry(TileBuildType.Forge, nameof(TileBuildType.Forge), Cost((BaseResourceType.Stone, 10), (BaseResourceType.IronOre, 2), (BaseResourceType.Coal, 1)), new Vector2I(2, 2), false) },
        { TileBuildType.AlchemyBench, Entry(TileBuildType.AlchemyBench, nameof(TileBuildType.AlchemyBench), Cost((BaseResourceType.Wood, 4), (BaseResourceType.Stone, 4), (BaseResourceType.IronIngot, 2)), new Vector2I(2, 1), false) },
        { TileBuildType.TrainingDummy, Entry(TileBuildType.TrainingDummy, nameof(TileBuildType.TrainingDummy), Cost((BaseResourceType.Wood, 4)), Vector2I.One, false) },
        { TileBuildType.Sandbag, Entry(TileBuildType.Sandbag, nameof(TileBuildType.Sandbag), Cost((BaseResourceType.Stone, 2)), Vector2I.One, false) },
        { TileBuildType.TrainingMat, Entry(TileBuildType.TrainingMat, nameof(TileBuildType.TrainingMat), Cost((BaseResourceType.Wood, 4)), new Vector2I(2, 2), false) },
        { TileBuildType.WeaponTrainingRack, Entry(TileBuildType.WeaponTrainingRack, nameof(TileBuildType.WeaponTrainingRack), Cost((BaseResourceType.Wood, 5), (BaseResourceType.IronIngot, 2)), new Vector2I(2, 1), false) },
        { TileBuildType.TrainingRing, Entry(TileBuildType.TrainingRing, nameof(TileBuildType.TrainingRing), Cost((BaseResourceType.Wood, 12), (BaseResourceType.Stone, 6)), new Vector2I(4, 2), false) },
        { TileBuildType.ImprovisedMedicalBed, Entry(TileBuildType.ImprovisedMedicalBed, nameof(TileBuildType.ImprovisedMedicalBed), Cost((BaseResourceType.Wood, 3)), Vector2I.One, false) },
        { TileBuildType.MedicalBed, Entry(TileBuildType.MedicalBed, nameof(TileBuildType.MedicalBed), Cost((BaseResourceType.Wood, 6), (BaseResourceType.IronOre, 1)), new Vector2I(1, 2), false) },
        { TileBuildType.LuxuryMedicalBed, Entry(TileBuildType.LuxuryMedicalBed, nameof(TileBuildType.LuxuryMedicalBed), Cost((BaseResourceType.Wood, 10), (BaseResourceType.IronIngot, 4)), new Vector2I(2, 2), false) },
        { TileBuildType.MedicalTable, Entry(TileBuildType.MedicalTable, nameof(TileBuildType.MedicalTable), Cost((BaseResourceType.Wood, 5), (BaseResourceType.IronIngot, 2)), new Vector2I(2, 1), false) },
        { TileBuildType.MedicineCabinet, Entry(TileBuildType.MedicineCabinet, nameof(TileBuildType.MedicineCabinet), Cost((BaseResourceType.Wood, 4), (BaseResourceType.IronOre, 1)), Vector2I.One, false) },
        { TileBuildType.PlantPot, Entry(TileBuildType.PlantPot, nameof(TileBuildType.PlantPot), Cost((BaseResourceType.Wood, 1), (BaseResourceType.Stone, 1)), Vector2I.One, false) },
        { TileBuildType.SmallRug, Entry(TileBuildType.SmallRug, nameof(TileBuildType.SmallRug), Cost((BaseResourceType.Wood, 1)), Vector2I.One, false) },
        { TileBuildType.LargeRug, Entry(TileBuildType.LargeRug, nameof(TileBuildType.LargeRug), Cost((BaseResourceType.Wood, 3)), new Vector2I(2, 2), false) },
        { TileBuildType.WallBanner, Entry(TileBuildType.WallBanner, "\uBCBD\uAC78\uC774 \uAE43\uBC1C", Cost((BaseResourceType.Wood, 2)), Vector2I.One, false) },
        { TileBuildType.TrophyDisplay, Entry(TileBuildType.TrophyDisplay, nameof(TileBuildType.TrophyDisplay), Cost((BaseResourceType.Wood, 5), (BaseResourceType.IronIngot, 2)), new Vector2I(2, 1), false) },
        { TileBuildType.Erase, Entry(TileBuildType.Erase, "\uCCA0\uAC70", EmptyCost, Vector2I.One, false) }
    };

    public static bool TryGet(TileBuildType buildType, out BuildDefinitionEntry entry)
    {
        return Entries.TryGetValue(buildType, out entry);
    }

    public static BuildStructureDefinition Get(TileBuildType buildType)
    {
        return Get(buildType, BuildMaterialType.Basic);
    }

    public static BuildStructureDefinition Get(TileBuildType buildType, BuildMaterialType materialType)
    {
        if (IsMaterialSensitiveBuildType(buildType))
        {
            return GetMaterialDefinition(buildType, materialType);
        }

        BuildDefinitionEntry entry = GetEntryOrDefault(buildType);
        return new BuildStructureDefinition(
            buildType,
            BuildMaterialType.Basic,
            entry.DisplayName,
            entry.Cost.Resources,
            0,
            1.0f,
            0.0f,
            0.0f,
            1.0f,
            "");
    }

    public static BuildCost GetCost(TileBuildType buildType)
    {
        return GetEntryOrDefault(buildType).Cost;
    }

    public static BuildCost GetCost(TileBuildType buildType, BuildMaterialType materialType)
    {
        return IsMaterialSensitiveBuildType(buildType)
            ? new BuildCost(Get(buildType, materialType).Cost)
            : GetCost(buildType);
    }

    public static string GetDisplayName(TileBuildType buildType)
    {
        return GetEntryOrDefault(buildType).DisplayName;
    }

    public static Vector2I GetSize(TileBuildType buildType)
    {
        return GetEntryOrDefault(buildType).Size;
    }

    public static bool UsesDirectConstruction(TileBuildType buildType)
    {
        return TryGet(buildType, out BuildDefinitionEntry entry) && entry.UsesDirectConstruction;
    }

    public static IReadOnlyList<BuildMaterialType> GetMaterialOptions(TileBuildType buildType)
    {
        return IsMaterialSensitiveBuildType(buildType) ? MaterialOrder : Array.Empty<BuildMaterialType>();
    }

    public static bool IsMaterialSensitiveBuildType(TileBuildType buildType)
    {
        return buildType == TileBuildType.Floor
            || buildType == TileBuildType.Wall
            || buildType == TileBuildType.Door;
    }

    public static bool CanUseMaterial(TileBuildType buildType, BuildMaterialType materialType)
    {
        if (!IsMaterialSensitiveBuildType(buildType))
        {
            return false;
        }

        if (buildType == TileBuildType.Door && materialType == BuildMaterialType.Stone)
        {
            return false;
        }

        return materialType == BuildMaterialType.Wood
            || materialType == BuildMaterialType.Stone
            || materialType == BuildMaterialType.Metal;
    }

    public static BuildMaterialType NormalizeMaterial(TileBuildType buildType, BuildMaterialType materialType)
    {
        return CanUseMaterial(buildType, materialType) ? materialType : BuildMaterialType.Wood;
    }

    public static string GetMaterialDisplayName(BuildMaterialType materialType)
    {
        return materialType switch
        {
            BuildMaterialType.Wood => "\uB098\uBB34",
            BuildMaterialType.Stone => "\uB3CC",
            BuildMaterialType.Metal => "\uAE08\uC18D",
            _ => "\uAE30\uBCF8"
        };
    }

    private static BuildStructureDefinition GetMaterialDefinition(TileBuildType buildType, BuildMaterialType materialType)
    {
        BuildMaterialType normalizedMaterial = NormalizeMaterial(buildType, materialType);

        return buildType switch
        {
            TileBuildType.Floor => GetFloorDefinition(normalizedMaterial),
            TileBuildType.Wall => GetWallDefinition(normalizedMaterial),
            TileBuildType.Door => GetDoorDefinition(normalizedMaterial),
            _ => Get(buildType)
        };
    }

    private static BuildStructureDefinition GetFloorDefinition(BuildMaterialType materialType)
    {
        return materialType switch
        {
            BuildMaterialType.Stone => new BuildStructureDefinition(
                TileBuildType.Floor,
                BuildMaterialType.Stone,
                "\uB3CC \uBC14\uB2E5",
                Cost((BaseResourceType.Stone, 1)).Resources,
                80,
                1.2f,
                0.3f,
                0.2f,
                0.95f,
                "\uB2E8\uB2E8\uD558\uACE0 \uC704\uC0DD\uC801\uC778 \uB3CC \uBC14\uB2E5\uC785\uB2C8\uB2E4."),
            BuildMaterialType.Metal => new BuildStructureDefinition(
                TileBuildType.Floor,
                BuildMaterialType.Metal,
                "\uCCA0\uD310 \uBC14\uB2E5",
                Cost((BaseResourceType.IronIngot, 1)).Resources,
                120,
                1.5f,
                0.1f,
                0.0f,
                0.9f,
                "\uB0B4\uAD6C\uB3C4\uAC00 \uB192\uC9C0\uB9CC \uCC28\uAC11\uACE0 \uBB34\uAC70\uC6B4 \uCCA0\uD310 \uBC14\uB2E5\uC785\uB2C8\uB2E4."),
            _ => new BuildStructureDefinition(
                TileBuildType.Floor,
                BuildMaterialType.Wood,
                "\uB098\uBB34 \uBC14\uB2E5",
                Cost((BaseResourceType.Wood, 1)).Resources,
                40,
                1.0f,
                0.2f,
                0.0f,
                1.0f,
                "\uB530\uB73B\uD558\uACE0 \uBB34\uB09C\uD55C \uB098\uBB34 \uBC14\uB2E5\uC785\uB2C8\uB2E4.")
        };
    }

    private static BuildStructureDefinition GetWallDefinition(BuildMaterialType materialType)
    {
        return materialType switch
        {
            BuildMaterialType.Stone => new BuildStructureDefinition(
                TileBuildType.Wall,
                BuildMaterialType.Stone,
                "\uB3CC \uBCBD",
                Cost((BaseResourceType.Stone, 2)).Resources,
                140,
                1.4f,
                0.3f,
                0.0f,
                1.0f,
                "\uB0B4\uAD6C\uB3C4\uAC00 \uB192\uC740 \uB3CC \uBCBD\uC785\uB2C8\uB2E4."),
            BuildMaterialType.Metal => new BuildStructureDefinition(
                TileBuildType.Wall,
                BuildMaterialType.Metal,
                "\uCCA0\uBCBD",
                Cost((BaseResourceType.IronIngot, 2)).Resources,
                220,
                1.8f,
                0.2f,
                0.0f,
                1.0f,
                "\uAC00\uC7A5 \uB2E8\uB2E8\uD55C \uAE08\uC18D \uBCBD\uC785\uB2C8\uB2E4."),
            _ => new BuildStructureDefinition(
                TileBuildType.Wall,
                BuildMaterialType.Wood,
                "\uB098\uBB34 \uBCBD",
                Cost((BaseResourceType.Wood, 2)).Resources,
                60,
                1.0f,
                0.1f,
                0.0f,
                1.0f,
                "\uBE60\uB974\uAC8C \uC138\uC6B8 \uC218 \uC788\uB294 \uB098\uBB34 \uBCBD\uC785\uB2C8\uB2E4.")
        };
    }

    private static BuildStructureDefinition GetDoorDefinition(BuildMaterialType materialType)
    {
        return materialType == BuildMaterialType.Metal
            ? new BuildStructureDefinition(
                TileBuildType.Door,
                BuildMaterialType.Metal,
                "\uBCF4\uAC15 \uBB38",
                Cost((BaseResourceType.Wood, 2), (BaseResourceType.IronIngot, 1)).Resources,
                130,
                1.4f,
                0.0f,
                0.0f,
                1.0f,
                "\uAE08\uC18D\uC73C\uB85C \uBCF4\uAC15\uD55C \uD2BC\uD2BC\uD55C \uBB38\uC785\uB2C8\uB2E4.")
            : new BuildStructureDefinition(
                TileBuildType.Door,
                BuildMaterialType.Wood,
                "\uB098\uBB34 \uBB38",
                Cost((BaseResourceType.Wood, 3)).Resources,
                50,
                1.0f,
                0.0f,
                0.0f,
                1.0f,
                "\uAE30\uBCF8\uC801\uC778 \uB098\uBB34 \uBB38\uC785\uB2C8\uB2E4.");
    }

    private static BuildDefinitionEntry GetEntryOrDefault(TileBuildType buildType)
    {
        return Entries.TryGetValue(buildType, out BuildDefinitionEntry entry)
            ? entry
            : Entry(buildType, buildType.ToString(), EmptyCost, Vector2I.One, false);
    }

    private static BuildDefinitionEntry Entry(
        TileBuildType buildType,
        string displayName,
        BuildCost cost,
        Vector2I size,
        bool usesDirectConstruction)
    {
        return new BuildDefinitionEntry(buildType, displayName, cost, size, usesDirectConstruction);
    }

    private static BuildCost Cost(params (BaseResourceType ResourceType, int Amount)[] resources)
    {
        Dictionary<BaseResourceType, int> cost = new();

        foreach ((BaseResourceType resourceType, int amount) in resources)
        {
            if (amount > 0)
            {
                cost[resourceType] = amount;
            }
        }

        return new BuildCost(cost);
    }
}
