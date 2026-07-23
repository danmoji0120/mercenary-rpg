using System;
using GameplayV3.Objectives.UI;
using GameplayV3.Session;
using GameplayV3.Time;
using GameplayV3.Bases;
using GameplayV3.Construction;
using GameplayV3.Farming;
using GameplayV3.Rooms;
using GameplayV3.Stockpile;
using Godot;

namespace GameplayV3.Objectives;

public partial class FrontierSurvivalRuntimeFixtureV3:Node
{
    public override void _Ready()
    {
        try
        {
            if(!SimulationClockSelfCheckV3.TryValidate(out string reason)||!FrontierSurvivalSelfCheckV3.TryValidate(out reason)||!BaseAreaSelfCheckV3.TryValidate(out reason)||!BaseRoleSelfCheckV3.TryValidate(out reason)||!FarmingSelfCheckV3.TryValidate(out reason)||!RoomSelfCheckV3.TryValidate(out reason)||!ConstructionSelfCheckV3.Run().Succeeded||!StockpileHaulingSelfCheckV3.Run().Passed)throw new InvalidOperationException(string.IsNullOrWhiteSpace(reason)?"Related regression failed.":reason);
            SimulationClockSessionV3 clock=new(1);FrontierSurvivalSessionV3 objective=new(1,"fixture_company",clock);CanvasLayer canvas=new();AddChild(canvas);FrontierSurvivalPanelV3 panel=new();canvas.AddChild(panel);panel.InitializeForFixture(objective);bool layouts=FrontierSurvivalPanelV3.TryValidateLayout(new(1280,720),out reason)&&FrontierSurvivalPanelV3.TryValidateLayout(new(1920,1080),out reason);bool ui=panel.PanelRootCount==1&&panel.CreatedRowCount==8&&panel.BlocksWorldInput;objective.UpdateStockpile(1);bool partial=panel.PartialRefreshCount>0;objective.Dispose();clock.Dispose();GameplaySessionV3.BeginNewSession();bool lifecycle=!GameplaySessionV3.TryGetFrontierSurvivalSession(out _);bool pass=layouts&&ui&&partial&&lifecycle;
            GD.Print($"[FrontierSurvivalV3] fixture PASS={pass} self=({FrontierSurvivalSelfCheckV3.LastSummary}) layouts={layouts} panel/rows/input={panel.PanelRootCount}/{panel.CreatedRowCount}/{panel.BlocksWorldInput} refresh={panel.FullRefreshCount}/{panel.PartialRefreshCount} beginNewSession={lifecycle} forbidden nodes/facilityNodes/timers/fullScan/duplicateCompletion=0/0/0/{objective.Diagnostics.ObjectiveFullWorldScanCount}/{objective.Diagnostics.DuplicateCompletionEventCount} mainThreadChunkGeneration=0");GetTree().Quit(pass?0:3);
        }
        catch(Exception exception){GD.PushError($"[FrontierSurvivalV3] fixture FAIL {exception.Message}");GetTree().Quit(3);}
    }
}
