using Godot;
using GameplayV3.Production.UI;
using GameplayV3.Work;
using GameplayV3.Jobs;

namespace GameplayV3.Production;

public partial class StarterProductionFixtureV3:Node
{
    public override void _Ready()
    {
        bool pass=StarterProductionSelfCheckV3.TryValidate(out string reason)&&ProductionWorkPrioritySelfCheckV3.TryValidate(out reason)&&DirectProductionOrderSelfCheckV3.TryValidate(out reason)&&StarterProductionUiSelfCheckV3.TryValidate(out reason)&&StarterFoodToolContentSelfCheckV3.TryValidate(out reason);
        if(pass)GD.Print($"[ProductionV3] Content fixture {StarterProductionSelfCheckV3.LastSummary}; {StarterFoodToolContentSelfCheckV3.LastSummary}");
        else GD.PushError($"[ProductionV3] Phase A fixture FAIL: {reason}");
        GetTree().Quit(pass?0:3);
    }
}
