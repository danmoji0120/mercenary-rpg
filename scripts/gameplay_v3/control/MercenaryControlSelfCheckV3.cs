using System;
using System.Collections.Generic;
using GameplayV3.Company;
using GameplayV3.Mercenary;
using GameplayV3.Movement;
using GameplayV3.Navigation;
using Godot;
using WorldV2;

namespace GameplayV3.Control;

public readonly struct MercenaryControlSelfCheckResultV3
{
    public MercenaryControlSelfCheckResultV3(bool passed, string summary) { Passed = passed; Summary = summary; }
    public bool Passed { get; }
    public string Summary { get; }
}

public static class MercenaryControlSelfCheckV3
{
    public static MercenaryControlSelfCheckResultV3 Run()
    {
        List<string> failures = new();
        CheckCommandIds(failures);
        CheckSelectionAndCommands(failures);
        CheckNavigation(failures);
        return new(failures.Count == 0, failures.Count == 0 ? "PASS" : $"FAIL: {string.Join(" | ", failures)}");
    }

    private static void CheckCommandIds(List<string> failures)
    {
        string id = CommandIdFactoryV3.CreateCommandId();
        Require(CommandIdFactoryV3.IsValidCommandId(id), "canonical command id rejected", failures);
        Require(!CommandIdFactoryV3.IsValidCommandId("bad_" + id[4..]), "invalid command prefix accepted", failures);
        Require(!CommandIdFactoryV3.IsValidCommandId("cmd_ABCDEF0123456789ABCDEF0123456789"), "uppercase command id accepted", failures);
    }

    private static void CheckSelectionAndCommands(List<string> failures)
    {
        CompanySessionV3 companies = new();
        Require(companies.TryInitializeLocalSinglePlayer(out _, out string companyReason), companyReason, failures);
        if (!companies.LocalContext.TryGetLocalCompanyId(out string companyId, out _)) return;
        MercenarySessionV3 mercenaries = new(companies.CompanyRegistry);
        List<string> ids = new();
        for (int index = 0; index < 12; index++)
        {
            if (!TryAddMercenary(mercenaries, companyId, index, out string id, out string reason))
            {
                failures.Add(reason);
                return;
            }
            ids.Add(id);
        }

        MercenaryControlSessionV3 control = new(7, companies, mercenaries);
        Require(control.Selection.TryReplaceSelection(ids, out string selectReason), selectReason, failures);
        Require(control.Selection.Count == 12, "selection did not retain 10+ mercenaries", failures);
        Require(!control.Selection.TryReplaceSelection(new[] { ids[0], ids[0] }, out _), "duplicate selection replace accepted", failures);
        Require(control.Selection.Count == 12, "failed replace mutated selection", failures);
        long revision = control.Selection.Revision;
        Require(control.Selection.TryAdd(ids[0], out _), "idempotent add failed", failures);
        Require(control.Selection.Revision == revision, "idempotent add changed revision", failures);

        TestNavigationQuery query = new(new Rect2I(0, 0, 40, 40));
        Require(control.TryIssueDirectMove(
            companies.LocalPlayer!.PlayerId,
            companyId,
            ids,
            new GlobalCellCoord(new Vector2I(20, 20)),
            query,
            7,
            out DirectMoveCommandV3? command,
            out string commandReason), commandReason, failures);
        Require(command?.ResolvedDestinationCells.Count == ids.Count, "command destinations are not unique and complete", failures);
        Require(!control.TryIssueDirectMove(companies.LocalPlayer.PlayerId, companyId, new[] { ids[0], ids[0] }, new(new Vector2I(2, 2)), query, 7, out _, out _), "duplicate command target accepted", failures);
        Require(!control.TryIssueDirectMove(companies.LocalPlayer.PlayerId, companyId, ids, new(new Vector2I(-1, 2)), query, 7, out _, out _), "out-of-bounds command accepted", failures);

        Require(control.TryIssueDirectMove(companies.LocalPlayer.PlayerId, companyId, new[] { ids[0] }, new(new Vector2I(4, 1)), query, 7, out _, out string moveReason), moveReason, failures);
        if (control.Commands.TryGetActiveOrder(ids[0], out MercenaryMoveOrderV3? order) && order != null)
        {
            MercenaryPathResultV3 path = Solve(query, order.StartCell.Value, order.DestinationCell.Value, new MercenaryNavigationSettingsV3());
            MercenaryMovementCoordinatorV3 movement = new(new MercenaryMovementSettingsV3());
            Require(path.Success, "movement path failed", failures);
            bool started = movement.TryStart(order, path, mercenaries, query, control.Movements, out string startReason);
            Require(started, startReason, failures);
            if (!started) return;
            mercenaries.Registry.TryGetState(ids[0], out MercenaryStateV3? movingState);
            GlobalCellCoord before = movingState!.CurrentCell;
            Require(control.Movements.TryGet(ids[0], out MercenaryMovementStateV3? active) && active != null, "movement state missing", failures);
            movement.Tick(active!.SegmentDuration * 0.5f, control, query);
            Require(movingState.CurrentCell.Value == before.Value, "CurrentCell changed before segment boundary", failures);
            movement.Tick(100f, control, query);
            Require(movingState.CurrentCell.Value == order.DestinationCell.Value, "large delta did not reach destination", failures);
            Require(movingState.ActivityState == MercenaryActivityStateV3.Idle, "movement completion did not restore Idle", failures);
        }
        else
        {
            failures.Add("movement order missing");
        }
    }

    private static void CheckNavigation(List<string> failures)
    {
        TestNavigationQuery open = new(new Rect2I(0, 0, 16, 16));
        MercenaryPathResultV3 diagonalA = Solve(open, new Vector2I(1, 1), new Vector2I(5, 5), new MercenaryNavigationSettingsV3());
        MercenaryPathResultV3 diagonalB = Solve(open, new Vector2I(1, 1), new Vector2I(5, 5), new MercenaryNavigationSettingsV3());
        Require(diagonalA.Success && diagonalA.Path.Count == 4, "diagonal path failed", failures);
        Require(SamePath(diagonalA.Path, diagonalB.Path), "A* result is not deterministic", failures);
        Require(diagonalA.Path.Count == 0 || diagonalA.Path[0].Value != new Vector2I(1, 1), "path includes start", failures);

        TestNavigationQuery corner = new(new Rect2I(0, 0, 2, 2));
        corner.Blocked.Add(new Vector2I(1, 0));
        corner.Blocked.Add(new Vector2I(0, 1));
        MercenaryPathResultV3 cornerResult = Solve(corner, Vector2I.Zero, Vector2I.One, new MercenaryNavigationSettingsV3());
        Require(!cornerResult.Success && cornerResult.Failure == MercenaryPathFailureV3.NoPath, "diagonal corner cutting allowed", failures);

        MercenaryPathResultV3 limited = Solve(open, Vector2I.Zero, new Vector2I(15, 15), new MercenaryNavigationSettingsV3 { MaxExpandedNodes = 1 });
        Require(!limited.Success && limited.Failure == MercenaryPathFailureV3.SearchLimitExceeded, "search node limit not enforced", failures);
        Require(Mathf.IsEqualApprox(MercenaryMovementCostPolicyV3.DirectionDistance(Vector2I.Zero, Vector2I.One), MercenaryMovementCostPolicyV3.DiagonalDistance), "diagonal distance changed", failures);
    }

    private static MercenaryPathResultV3 Solve(TestNavigationQuery query, Vector2I start, Vector2I destination, MercenaryNavigationSettingsV3 settings)
    {
        MercenaryPathRequestV3 request = new("test:path", "merc_test", new(start), new(destination), 1, 1);
        MercenaryPathfindingSchedulerV3 scheduler = new(settings);
        scheduler.Enqueue(request);
        for (int tick = 0; tick < 4096; tick++)
        {
            IReadOnlyList<MercenaryPathResultV3> results = scheduler.Tick(query, _ => true, out _);
            if (results.Count > 0) return results[0];
        }
        return new(request, false, Array.Empty<GlobalCellCoord>(), 0, 0, 0, MercenaryPathFailureV3.SearchLimitExceeded);
    }

    private static bool TryAddMercenary(MercenarySessionV3 session, string companyId, int index, out string id, out string reason)
    {
        id = MercenaryIdFactoryV3.CreateMercenaryId();
        DateTime created = DateTime.UtcNow;
        MercenaryAttributeSetV3.TryCreate(10, 10, 10, 10, 10, out MercenaryAttributeSetV3? attributes, out _);
        MercenaryWorkSkillSetV3.TryCreate(10, 10, 10, 10, 10, 10, 10, out MercenaryWorkSkillSetV3? skills, out _);
        if (!MercenaryProfileV3.TryCreate(id, $"Test {index}", "placeholder", attributes, skills, created, out MercenaryProfileV3? profile, out reason)
            || !MercenaryStateV3.TryCreate(id, companyId, new(new Vector2I(index + 1, 1)), MercenaryActivityStateV3.Idle, created, out MercenaryStateV3? state, out reason))
        {
            return false;
        }
        return session.Registry.TryRegisterMercenary(profile, state, out reason);
    }

    private static bool SamePath(IReadOnlyList<GlobalCellCoord> left, IReadOnlyList<GlobalCellCoord> right)
    {
        if (left.Count != right.Count) return false;
        for (int index = 0; index < left.Count; index++) if (left[index].Value != right[index].Value) return false;
        return true;
    }

    private static void Require(bool condition, string reason, List<string> failures)
    {
        if (!condition) failures.Add(string.IsNullOrWhiteSpace(reason) ? "unspecified failure" : reason);
    }

    private sealed class TestNavigationQuery : IMercenaryNavigationWorldQueryV3
    {
        private readonly Rect2I _bounds;
        public TestNavigationQuery(Rect2I bounds) { _bounds = bounds; }
        public HashSet<Vector2I> Blocked { get; } = new();
        public bool IsInsideWorld(Vector2I cell) => _bounds.HasPoint(cell);
        public MercenaryNavigationCellInfoV3 GetCellInfo(Vector2I cell)
        {
            bool inside = IsInsideWorld(cell);
            return new(inside, inside && !Blocked.Contains(cell), 1.0f, TileType.Grass, BiomeKindV3.Plains);
        }
        public bool IsWalkable(Vector2I cell) => GetCellInfo(cell).IsWalkable;
        public float GetTraversalMultiplier(Vector2I cell) => GetCellInfo(cell).TraversalMultiplier;
    }
}
