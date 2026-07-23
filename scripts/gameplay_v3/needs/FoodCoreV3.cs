using System;
using System.Collections.Generic;
using GameplayV3.Resources;

namespace GameplayV3.Needs;

public sealed record FoodSpecV3(string DisplayName,int CaloriesPerUnit,float EatDurationPerUnitSeconds,bool CanEatRaw,int EatingPriority=0,string ShortDescription="")
{public bool IsValid=>!string.IsNullOrWhiteSpace(DisplayName)&&CaloriesPerUnit>0&&float.IsFinite(EatDurationPerUnitSeconds)&&EatDurationPerUnitSeconds>0;}

public sealed class FoodCatalogV3
{
    private static readonly IReadOnlyList<ResourceTypeV3> EdibleTypes=Array.AsReadOnly(new[]{ResourceTypeV3.Ration,ResourceTypeV3.Potato,ResourceTypeV3.RoastedPotato,ResourceTypeV3.PotatoStew,ResourceTypeV3.DriedPotato});
    private readonly Dictionary<ResourceTypeV3,FoodSpecV3> _specs=new()
    {
        [ResourceTypeV3.Ration]=new("\uBE44\uC0C1 \uC2DD\uB7C9",450,3f,true,20),
        [ResourceTypeV3.Potato]=new("\uAC10\uC790",150,1f,true,10),
        [ResourceTypeV3.RoastedPotato]=new("\uAD6C\uC6B4 \uAC10\uC790",220,2f,true,30,"\uBD88\uC5D0 \uC775\uD600 \uBA39\uAE30 \uC26C\uC6B4 \uAC10\uC790\uC785\uB2C8\uB2E4."),
        [ResourceTypeV3.PotatoStew]=new("\uAC10\uC790 \uC218\uD504",450,3f,true,50,"\uD3EC\uB9CC\uB3C4\uAC00 \uB192\uC740 \uB530\uB73B\uD55C \uC218\uD504\uC785\uB2C8\uB2E4."),
        [ResourceTypeV3.DriedPotato]=new("\uB9D0\uB9B0 \uAC10\uC790",300,2.5f,true,40,"\uBCF4\uAD00\uACFC \uD734\uB300\uC5D0 \uC801\uD569\uD55C \uB9D0\uB9B0 \uC2DD\uB7C9\uC785\uB2C8\uB2E4.")
    };
    public int Count=>_specs.Count;
    public bool TryGet(ResourceTypeV3 type,out FoodSpecV3? spec)=>_specs.TryGetValue(type,out spec);
    public IReadOnlyList<ResourceTypeV3> GetEdibleTypes()=>EdibleTypes;
}

public readonly record struct FoodMealPlanV3(int RequiredCalories,int RequiredUnits,int PlannedUnits,int PlannedCalories,float ExpectedHungerReduction,float ExpectedFinalHunger,float EatingDurationSeconds,bool IsCompleteMeal,int CalorieOvershoot)
{public bool IsValid=>RequiredCalories>0&&RequiredUnits>0&&PlannedUnits>0&&PlannedCalories>0&&float.IsFinite(ExpectedHungerReduction)&&float.IsFinite(ExpectedFinalHunger)&&float.IsFinite(EatingDurationSeconds)&&EatingDurationSeconds>0;}

public static class FoodMealCalculatorV3
{
    private const double HungerEpsilon=1e-7,CalorieEpsilon=1e-3;
    public static bool TryPlan(float currentHunger,float targetHunger,int calorieCapacity,FoodSpecV3? spec,int availableUnits,out FoodMealPlanV3 plan,out string reason)
    {plan=default;if(!float.IsFinite(currentHunger)||!float.IsFinite(targetHunger)||currentHunger<0||currentHunger>1||targetHunger<0||targetHunger>1||calorieCapacity<1||spec==null||!spec.IsValid||availableUnits<0){reason="MealPlanInvalid";return false;}double recover=Math.Max(0,(double)currentHunger-targetHunger);if(recover<=HungerEpsilon){reason="AlreadyFull";return false;}int requiredCalories=(int)Math.Ceiling(recover*calorieCapacity-CalorieEpsilon);if(requiredCalories<1){reason="AlreadyFull";return false;}int requiredUnits=(requiredCalories+spec.CaloriesPerUnit-1)/spec.CaloriesPerUnit;int planned=Math.Min(requiredUnits,availableUnits);if(planned<1){reason="FoodAmountInsufficient";return false;}int calories=checked(planned*spec.CaloriesPerUnit);float reduction=(float)((double)calories/calorieCapacity);float final=Math.Clamp(currentHunger-reduction,0,1);float duration=planned*spec.EatDurationPerUnitSeconds;plan=new(requiredCalories,requiredUnits,planned,calories,reduction,final,duration,calories>=requiredCalories,Math.Max(0,calories-requiredCalories));reason=string.Empty;return true;}
}

public static class FoodCoreSelfCheckV3
{
    public static bool TryValidate(out string reason)
    {FoodCatalogV3 catalog=new();if(catalog.Count!=5||!catalog.TryGet(ResourceTypeV3.Ration,out var ration)||ration?.CaloriesPerUnit!=450||!catalog.TryGet(ResourceTypeV3.Potato,out var potato)||potato?.CaloriesPerUnit!=150||!catalog.TryGet(ResourceTypeV3.RoastedPotato,out var roasted)||roasted?.CaloriesPerUnit!=220||!catalog.TryGet(ResourceTypeV3.PotatoStew,out var stew)||stew?.CaloriesPerUnit!=450||!catalog.TryGet(ResourceTypeV3.DriedPotato,out var dried)||dried?.CaloriesPerUnit!=300){reason="Food catalog mismatch.";return false;}if(!Check(.65f,ration!,18,450,1,1,3,.20f,true,out reason)||!Check(.65f,potato!,18,450,3,3,3,.20f,true,out reason)||!Check(.80f,ration!,18,600,2,2,6,0,true,out reason)||!Check(.80f,potato!,18,600,4,4,4,.20f,true,out reason)||!Check(.95f,potato!,18,750,5,5,5,.20f,true,out reason)||!Check(.65f,potato!,2,450,3,2,2,.35f,false,out reason))return false;if(FoodMealCalculatorV3.TryPlan(.20f,.20f,1000,ration,18,out _,out _)||FoodMealCalculatorV3.TryPlan(float.NaN,.20f,1000,ration,18,out _,out _)||FoodMealCalculatorV3.TryPlan(.65f,.20f,1000,new("bad",0,1,true),18,out _,out _)){reason="Invalid meal plan accepted.";return false;}reason=string.Empty;return true;}
    private static bool Check(float hunger,FoodSpecV3 spec,int available,int calories,int required,int planned,float duration,float final,bool complete,out string reason){if(!FoodMealCalculatorV3.TryPlan(hunger,.20f,1000,spec,available,out var p,out reason)||p.RequiredCalories!=calories||p.RequiredUnits!=required||p.PlannedUnits!=planned||Math.Abs(p.EatingDurationSeconds-duration)>.001f||Math.Abs(p.ExpectedFinalHunger-final)>.001f||p.IsCompleteMeal!=complete){reason=$"Meal plan mismatch for {spec.DisplayName}/{hunger}: {p}";return false;}reason=string.Empty;return true;}
}
