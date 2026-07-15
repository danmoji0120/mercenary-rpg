using System;
using GameplayV3.Company;
using GameplayV3.Control;
using GameplayV3.Mercenary;
using GameplayV3.Movement;
using GameplayV3.Navigation;
using GameplayV3.Resources;
using GameplayV3.Stockpile;
using GameplayV3.Work;
using Godot;
using WorldV2;

namespace GameplayV3.Needs.Runtime;

public static class EatingArrivalTransitionSelfCheckV3
{
    public static bool TryValidate(out string reason)
    {
        Rect2I bounds = new(0, 0, 20, 20);
        CompanySessionV3 companies = new();
        if (!companies.TryInitializeLocalSinglePlayer(out _, out reason)) return false;
        string playerId = companies.LocalPlayer!.PlayerId;
        string companyId = companies.LocalContext.LocalCompanyId!;
        MercenarySessionV3 mercenaries = new(companies.CompanyRegistry);
        MercenaryAttributeSetV3.TryCreate(10, 10, 10, 10, 10, out var attributes, out _);
        MercenaryWorkSkillSetV3.TryCreate(8, 8, 8, 8, 8, 8, 8, out var skills, out _);
        string mercenaryId = MercenaryIdFactoryV3.CreateMercenaryId();
        DateTime createdUtc = DateTime.UtcNow;
        MercenaryProfileV3.TryCreate(mercenaryId, "Arrival Eater", "placeholder", attributes, skills, createdUtc, out var profile, out _);
        MercenaryStateV3.TryCreate(mercenaryId, companyId, new(new Vector2I(4, 4)), MercenaryActivityStateV3.Idle, createdUtc, out var worker, out _);
        mercenaries.Registry.TryRegisterMercenary(profile, worker, out _);
        MercenaryControlSessionV3 control = new(1, companies, mercenaries);
        ResourceSessionV3 resources = new();
        StockpileSessionV3 stockpiles = new();
        MercenaryWorkSessionV3 work = new(1, companies, mercenaries, resources, stockpiles, control);
        control.AttachWorkSession(work);
        MercenaryNeedsSessionV3 needs = new(1);
        needs.EnsureMercenaries(mercenaries.Registry);
        needs.Hunger.EnsureForMercenary(mercenaryId).TrySetHunger(.70f, out _);
        GlobalCellCoord foodCell = new(new Vector2I(7, 4));
        resources.GroundStacks.TryAddStack(ResourceTypeV3.Ration, 18, foodCell, out var food, out _, out _);
        EatingWorkCoordinatorV3 coordinator = new(needs, resources, mercenaries, control, work, new TestQuery(bounds), playerId, companyId);

        if (!coordinator.TryIssueManual(mercenaryId, out reason) || !coordinator.TryGet(mercenaryId, out var eating) || eating == null || eating.Phase != EatingWorkPhaseV3.MovingToFood || eating.MovementRequestId.Length == 0 || eating.InteractionCell.Value != foodCell.Value || eating.MovementDestinationCell?.Value != foodCell.Value)
        { reason = "Moving eating request was not correlated with the food interaction cell."; return false; }
        if (!control.ExternalMovements.TryGetActive(mercenaryId, out var movement) || movement == null || movement.MovementRequestId != eating.MovementRequestId)
        { reason = "Eating movement request was not active."; return false; }
        worker!.TrySetCurrentCell(foodCell, out _);
        control.ExternalMovements.Complete(movement, true, string.Empty);
        if (!control.ExternalMovements.TryDequeueResult(out var completion) || completion == null || !coordinator.TryHandleMovementResult(completion) || eating.Phase != EatingWorkPhaseV3.Eating || eating.EatingProgressSeconds != 0 || worker.ActivityState != MercenaryActivityStateV3.Working || resources.AmountReservations.Count != 1)
        { reason = "MovingToFood did not transition to Eating after authoritative completion."; return false; }
        if (!coordinator.Diagnostics.LastArrivalMatched || coordinator.Diagnostics.LastEatingTransition != "MovingToFood->Eating")
        { reason = "Eating arrival diagnostics did not record a matched transition."; return false; }
        coordinator.Tick(2.99f);
        if (resources.GroundStacks.GetTotalAmount(ResourceTypeV3.Ration) != 18 || Math.Abs(needs.Hunger.GetHunger(mercenaryId) - .70f) > .001f)
        { reason = "Eating consumed before three seconds."; return false; }
        coordinator.Tick(.01f);
        if (resources.GroundStacks.GetTotalAmount(ResourceTypeV3.Ration) != 17 || Math.Abs(needs.Hunger.GetHunger(mercenaryId) - .25f) > .001f || resources.AmountReservations.Count != 0 || resources.ConsumptionLedger.GetConsumedAmount(ResourceTypeV3.Ration) != 1 || worker.ActivityState != MercenaryActivityStateV3.Idle)
        { reason = "Three-second eating completion invariants failed."; return false; }

        worker.TrySetCurrentCell(new(new Vector2I(4, 4)), out _);
        needs.Hunger.EnsureForMercenary(mercenaryId).TrySetHunger(.70f, out _);
        if (!coordinator.TryIssueAuto(mercenaryId, out reason) || !coordinator.TryGet(mercenaryId, out eating) || eating == null || !control.ExternalMovements.TryGetActive(mercenaryId, out movement) || movement == null)
        { reason = "Auto Eat did not create a moving eating request."; return false; }
        worker.TrySetCurrentCell(foodCell, out _);
        control.ExternalMovements.Complete(movement, true, string.Empty);
        control.ExternalMovements.TryDequeueResult(out completion);
        if (completion == null || !coordinator.TryHandleMovementResult(completion) || eating.Phase != EatingWorkPhaseV3.Eating)
        { reason = "Auto Eat did not share the arrival transition."; return false; }
        coordinator.Cancel(mercenaryId, "CancelledByDirectMove");
        if (resources.AmountReservations.Count != 0 || Math.Abs(needs.Hunger.GetHunger(mercenaryId) - .70f) > .001f)
        { reason = "Direct Move cancellation leaked or consumed the food reservation."; return false; }

        worker.TrySetCurrentCell(new(new Vector2I(4, 4)), out _);
        if (!coordinator.TryIssueManual(mercenaryId, out reason) || !coordinator.TryGet(mercenaryId, out eating) || eating == null || !control.ExternalMovements.TryGetActive(mercenaryId, out movement) || movement == null)
        { reason = "Cell mismatch fixture could not start."; return false; }
        control.ExternalMovements.Complete(movement, true, string.Empty);
        control.ExternalMovements.TryDequeueResult(out completion);
        if (completion == null || !coordinator.TryHandleMovementResult(completion) || coordinator.ActiveCount != 0 || resources.AmountReservations.Count != 0 || coordinator.Diagnostics.LastEatingTransitionFailure != "EatingArrivalCellMismatch")
        { reason = "Completed movement with a mismatched CurrentCell did not fail terminally."; return false; }

        if (!coordinator.TryIssueManual(mercenaryId, out reason) || !coordinator.TryGet(mercenaryId, out eating) || eating == null || !control.ExternalMovements.TryGetActive(mercenaryId, out movement) || movement == null)
        { reason = "Reservation-loss fixture could not start."; return false; }
        resources.AmountReservations.Release(eating.ReservationId);
        worker.TrySetCurrentCell(foodCell, out _);
        control.ExternalMovements.Complete(movement, true, string.Empty);
        control.ExternalMovements.TryDequeueResult(out completion);
        if (completion == null || !coordinator.TryHandleMovementResult(completion) || coordinator.ActiveCount != 0 || resources.AmountReservations.Count != 0 || coordinator.Diagnostics.LastEatingTransitionFailure != "FoodReservationLost")
        { reason = "Lost food reservation did not fail terminally."; return false; }

        worker.TrySetCurrentCell(new(new Vector2I(4, 4)), out _);
        if (!coordinator.TryIssueManual(mercenaryId, out reason) || !coordinator.TryGet(mercenaryId, out eating) || eating == null || !control.ExternalMovements.TryGetActive(mercenaryId, out movement) || movement == null)
        { reason = "Removed-stack fixture could not start."; return false; }
        resources.GroundStacks.TryRemove(eating.GroundStackId, out _);
        worker.TrySetCurrentCell(foodCell, out _);
        control.ExternalMovements.Complete(movement, true, string.Empty);
        control.ExternalMovements.TryDequeueResult(out completion);
        if (completion == null || !coordinator.TryHandleMovementResult(completion) || coordinator.ActiveCount != 0 || resources.AmountReservations.Count != 0 || coordinator.Diagnostics.LastEatingTransitionFailure != "FoodStackInvalidAtArrival" || Math.Abs(needs.Hunger.GetHunger(mercenaryId) - .70f) > .001f || resources.ConsumptionLedger.GetConsumedAmount(ResourceTypeV3.Ration) != 1)
        { reason = "Removed food stack did not fail terminally without Eating side effects."; return false; }

        reason = string.Empty;
        return true;
    }

    private sealed class TestQuery : IMercenaryNavigationWorldQueryV3
    {
        private readonly Rect2I _bounds;
        public TestQuery(Rect2I bounds) => _bounds = bounds;
        public bool IsInsideWorld(Vector2I cell) => _bounds.HasPoint(cell);
        public MercenaryNavigationCellInfoV3 GetCellInfo(Vector2I cell) => new(IsInsideWorld(cell), IsWalkable(cell), 1, TileType.Grass, BiomeKindV3.Plains);
        public bool IsWalkable(Vector2I cell) => IsInsideWorld(cell);
        public float GetTraversalMultiplier(Vector2I cell) => 1;
    }
}
