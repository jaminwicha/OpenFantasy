using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>Earns native input saves through production gameplay; exports do not claim native initiation of those actions.</summary>
public sealed class RestorativeNativeFixtureExportTests
{
    [Theory]
    [InlineData("raw-self")]
    [InlineData("crafted-self")]
    [InlineData("helper")]
    public void Actual_source_stat_injury_and_finite_stock_produce_native_ready_restorative_inputs(string name)
    {
        var fixture = new OrdinaryCareWorldFixture();
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var session = new CareWorldSession(authority);
        var injured = fixture.ReachFullDoseInjuredExploration(session);
        Assert.InRange(injured.Progression!.GetActor(fixture.Player).CurrentHp, 1, 20);
        Assert.Empty(injured.Care!.Ordinary!.Uses);
        Assert.Empty(new LocalRestorativeController().SelectActions(injured, authority.IsIdentityOccupiedForHost,
            new HashSet<EntityId>()));
        WorldSnapshot before = name switch
        {
            "raw-self" => fixture.Supply(session, self: true),
            "crafted-self" => CraftPlayerDose(fixture, session, authority),
            "helper" => fixture.Supply(session, self: false),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
        Assert.Equal((ushort)26, before.ProfileVersion);
        Assert.Equal(injured.Progression.ContentHash, before.Progression!.ContentHash);
        Assert.Equivalent(fixture.Genesis.Progression!.GetActor(fixture.Player).Stats,
            before.Progression.GetActor(fixture.Player).Stats, strict: true);
        Assert.True(WorldActorAvailability.CanAct(before, fixture.Player));
        Assert.True(WorldActorAvailability.CanAct(before, fixture.Helper));
        Assert.Null(before.Care!.Pending);
        Assert.False(before.HasReadingHold);
        Assert.Null(before.PendingExcavation);
        bool helper = name == "helper";
        WorldActionCommand selected;
        if (helper)
        {
            selected = Assert.Single(new LocalRestorativeController().SelectActions(before,
                authority.IsIdentityOccupiedForHost, new HashSet<EntityId>()));
            Assert.Equal(fixture.Helper, selected.ActorId);
            Assert.True(before.Care.Ordinary!.Permissions.Single().IsActive);
        }
        else
        {
            var definition = before.Care.Ordinary!.Definitions.Single(value => value.DeliveryMode == RestorativeDeliveryMode.SelfUse);
            var prepared = Assert.IsType<UseRestorativeItemIntent>(WorldRestorativeActions.Prepare(before,
                fixture.Player, fixture.Player, definition.DefinitionId));
            selected = fixture.Journey.Command(before, fixture.Player, prepared);
            Assert.Empty(new LocalRestorativeController().SelectActions(before, authority.IsIdentityOccupiedForHost,
                new HashSet<EntityId>()));
        }
        var intent = Assert.IsType<UseRestorativeItemIntent>(selected.Intent);
        Assert.Equal(name == "raw-self" ? IngredientLotKind.Source : IngredientLotKind.Transformed, intent.Ingredient.Kind);
        Assert.Equal(1UL, intent.Ingredient.Quantity);
        Assert.Equal(fixture.Player, intent.RecipientActorId);
        Assert.Equal(WorldActionRejection.None, WorldRestorativeActions.Evaluate(before, selected.ActorId, intent));
        AssertDoseOwnership(before, selected.ActorId, intent);
        var preview = authority.Preview(fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep, [selected]));
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(preview.Receipts).Status);
        Assert.Equal(before.Progression.GetActor(fixture.Player).CurrentHp + 40,
            preview.Candidate.Progression!.GetActor(fixture.Player).CurrentHp);
        Assert.Equal(before.Production!.ConsumedItems.Count + 1, preview.Candidate.Production!.ConsumedItems.Count);
        Assert.Equal(before.Care.Ordinary!.Uses.Count + 1, preview.Candidate.Care!.Ordinary!.Uses.Count);
        Assert.Equal(before.ContentHash, authority.ReadStateForHost().ContentHash);
        Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(before))));
        Export(name, authority, fixture, before, selected, preview.Candidate);

        if (name == "raw-self")
        {
            // The native player must be able to navigate the revoke UI without racing an
            // already eligible automatic helper. Same-cutoff helper priority is a session test.
            var permission = before.Care.Ordinary.Permissions.Single();
            var revoke = fixture.Journey.Command(before, fixture.Player,
                new RevokeCarePermissionIntent(permission.PermissionId, permission.Revision, permission.GrantVersion));
            var revocation = authority.Preview(fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep, [revoke]));
            Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(revocation.Receipts).Status);
            Assert.False(revocation.Candidate.Care!.Ordinary!.Permissions.Single().IsActive);
            Assert.Equal(before.Production.ContentHash, revocation.Candidate.Production!.ContentHash);
            Assert.Equal(before.Progression.ContentHash, revocation.Candidate.Progression!.ContentHash);
            Export("revoke", authority, fixture, before, revoke, revocation.Candidate);
        }
        Assert.Equal(before.ContentHash, authority.ReadStateForHost().ContentHash);
    }

    private static WorldSnapshot CraftPlayerDose(OrdinaryCareWorldFixture fixture, CareWorldSession session,
        SqliteWorldAuthority authority)
    {
        var state = session.ReadStateForHost();
        var home = state.Space.GetActor(fixture.Player).Feet;
        var permission = state.Care!.Ordinary!.Permissions.Single();
        state = CareRewardJourney.Commit(session, fixture.Journey.Batch(state, WorldBatchKind.AdvancingStep,
            [fixture.Journey.Command(state, fixture.Player,
                new RevokeCarePermissionIntent(permission.PermissionId, permission.Revision, permission.GrantVersion))])).Candidate;
        EntityId storage = state.GetResident(fixture.Player).InventoryCustodianId;
        var raw = state.Inventory.Custody.Lots.Single(value => value.CustodianId == storage
            && value.Definition == state.Care!.Remedies.Single().Item);
        Assert.Equal(1UL, raw.Quantity);
        var give = fixture.Journey.Command(state, fixture.Player, new GiveItemIntent(fixture.Helper,
            state.Space.GetActor(fixture.Helper).Revision, raw.LotId, raw.Revision, storage,
            state.Inventory.Custody.GetStorage(storage).Revision, 1));
        state = CareRewardJourney.Commit(session, fixture.Journey.Batch(state, WorldBatchKind.AdvancingStep, [give])).Candidate;
        Assert.DoesNotContain(state.Inventory.Custody.Lots, value => value.CustodianId == storage
            && value.Definition == state.Care!.Remedies.Single().Item);

        var recipe = state.Production!.Recipes.Single(value => value.PhysicalRecipe.Key == CareJourneyAdmission.RecipeKey);
        var term = Assert.Single(recipe.Inputs);
        Assert.Equal(3UL, term.Count);
        var berries = recipe.ItemFor(term);
        var node = state.Gathering!.Nodes.Single(value => value.Definition.YieldDefinition == berries);
        ulong initialIssued = node.NextUnissuedUnit;
        state = WalkPlayer(fixture, session, node.Feet);
        Assert.NotEqual(state.AdvancingSteps, state.ProcessIntervals);
        Assert.Equal(state.ProcessIntervals, state.Gathering!.AdvancingStep);
        var tool = state.Gathering!.Tools.Single(value => value.Kind == node.Definition.RequiredToolKind);
        Assert.Equal(1U, tool.Power);
        for (int harvest = 0; harvest < 3; harvest++)
        {
            for (int rest = 0; state.Gathering!.GetActorWork(fixture.Player).Effort < GatherRulesV1.EffortPerSwing; rest++)
            {
                Assert.True(rest < 40, "One actual source gathering swing must use the existing finite work-effort recovery.");
                state = CareRewardJourney.Commit(session, fixture.Journey.Batch(state, WorldBatchKind.AdvancingStep)).Candidate;
            }
            node = state.Gathering!.Nodes.Single(value => value.Definition.YieldDefinition == berries);
            var toolLot = state.Inventory.Custody.Lots.Single(value => value.CustodianId == storage && value.Definition == tool.Definition);
            var gather = fixture.Journey.Command(state, fixture.Player,
                new GatherIntent(node.NodeId, node.Revision, toolLot.LotId, toolLot.Revision));
            var batch = fixture.Journey.Batch(state, WorldBatchKind.AdvancingStep, [gather]);
            var prepared = new WorldStepProcessor().Decide(state, batch, authority.IsIdentityOccupiedForHost);
            Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(prepared.Receipts).Status);
            Assert.Equal(state.ContentHash, authority.ReadStateForHost().ContentHash);
            state = CareRewardJourney.Commit(session, batch).Candidate;
            Assert.Equal(prepared.Candidate.ContentHash, state.ContentHash);
        }
        Assert.Equal(initialIssued + 3, state.Gathering!.Nodes.Single(value => value.NodeId == node.NodeId).NextUnissuedUnit);
        var inputs = state.Inventory.Custody.Lots.Where(value => value.CustodianId == storage && value.Definition == berries)
            .OrderBy(value => value.LotId).ToArray();
        Assert.Equal(3UL, inputs.Aggregate(0UL, (sum, value) => sum + value.Quantity));
        var craft = fixture.Journey.Command(state, fixture.Player, new CraftIntent(new(recipe.DefinitionId, recipe.Version,
            state.Production!.Revision, storage, state.Inventory.Custody.GetStorage(storage).Revision,
            inputs.Select(value => new IngredientRequest(IngredientLotKind.Source, value.LotId, value.Revision, value.Quantity)))));
        state = CareRewardJourney.Commit(session, fixture.Journey.Batch(state, WorldBatchKind.AdvancingStep, [craft])).Candidate;
        var output = state.Production!.Lots.Single(value => value.CustodianId == storage
            && value.Definition == state.Care!.Remedies.Single().Item);
        var production = state.Production.Batches.Single(value => value.BatchId == output.Output.BatchId);
        Assert.Equal(fixture.Player, production.ActorId);
        Assert.Equal(craft.CommandId, production.CommandId);
        Assert.Equal(recipe.DefinitionId, production.RecipeId);
        Assert.Equal(3UL, production.Ingredients.Aggregate(0UL, (sum, value) => sum + value.Quantity));
        Assert.Equal(1UL, output.Quantity);
        state = WalkPlayer(fixture, session, home);
        Assert.False(state.Care!.Ordinary!.Permissions.Single().IsActive);
        return state;
    }

    private static WorldSnapshot WalkPlayer(OrdinaryCareWorldFixture fixture, CareWorldSession session, SubunitPosition goal)
    {
        var state = session.ReadStateForHost();
        var origin = state.Space.GetActor(fixture.Player).Feet;
        // Follow the farm's south then east connector, and reverse it on return. Local queries
        // verify every actual edge without asking the bounded route search for a distant path.
        var corner = goal.Y >= origin.Y
            ? new SubunitPosition(origin.X, goal.Y, goal.Z)
            : new SubunitPosition(goal.X, origin.Y, goal.Z);
        int step = 0;
        foreach (var waypoint in new[] { corner, goal })
        {
            while (state.Space.GetActor(fixture.Player).Feet != waypoint)
            {
                Assert.True(step++ < 256, "The actual local berry route must remain within the existing finite movement budget.");
                var feet = state.Space.GetActor(fixture.Player).Feet;
                long dx = Math.Clamp(waypoint.X - feet.X, -32, 32);
                long dy = dx == 0 ? Math.Clamp(waypoint.Y - feet.Y, -32, 32) : 0;
                var destination = feet.Offset(dx, dy, 0);
                var route = state.Space.QueryGroundRoute(fixture.Player, destination);
                Assert.Equal(GroundRouteStatus.Reachable, route.Status);
                Assert.Equal(destination, Assert.IsType<SubunitPosition>(route.NextWaypoint));
                var control = state.Inventory.Controls.Single(value => value.ActorId == fixture.Player);
                var basis = fixture.Journey.Batch(state, WorldBatchKind.AdvancingStep);
                state = CareRewardJourney.Commit(session, new(basis.CauseId, basis.WorldId, basis.BranchId,
                    basis.ExpectedRevision, basis.ExpectedAdvancingSteps, basis.Kind,
                    [new(new(control.PrincipalId, control.SessionId), fixture.Player, dx, dy)], [], basis.ProfileVersion)).Candidate;
                Assert.Equal(destination, state.Space.GetActor(fixture.Player).Feet);
            }
        }
        return state;
    }

    private static void AssertDoseOwnership(WorldSnapshot world, EntityId actor, UseRestorativeItemIntent intent)
    {
        EntityId storage = world.GetResident(actor).InventoryCustodianId;
        if (intent.Ingredient.Kind == IngredientLotKind.Source)
        {
            var lot = world.Inventory.Custody.Lots.Single(value => value.LotId == intent.Ingredient.LotId);
            Assert.Equal(storage, lot.CustodianId);
            Assert.Equal(intent.Ingredient.ExpectedRevision, lot.Revision);
            Assert.Equal(1UL, lot.Quantity);
        }
        else
        {
            var lot = world.Production!.Lots.Single(value => value.LotId == intent.Ingredient.LotId);
            Assert.Equal(storage, lot.CustodianId);
            Assert.Equal(intent.Ingredient.ExpectedRevision, lot.Revision);
            Assert.Equal(1UL, lot.Quantity);
            Assert.Equal(actor, world.Production.Batches.Single(value => value.BatchId == lot.Output.BatchId).ActorId);
        }
    }

    private static void Export(string name, SqliteWorldAuthority authority, OrdinaryCareWorldFixture fixture,
        WorldSnapshot before, WorldActionCommand selected, WorldSnapshot expected)
    {
        if (Environment.GetEnvironmentVariable("OPENFANTASY_RESTORATIVE_NATIVE_EVIDENCE") is not { Length: > 0 } root) return;
        string directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        authority.BackupForHost(Path.Combine(directory, "before.sqlite3"));
        File.WriteAllBytes(Path.Combine(directory, "before.ofc"), WorldSnapshotCodec.Encode(before));
        var player = before.Progression!.GetActor(fixture.Player);
        var permission = before.Care!.Ordinary!.Permissions.Single();
        var use = selected.Intent as UseRestorativeItemIntent;
        var transformed = use?.Ingredient.Kind == IngredientLotKind.Transformed
            ? before.Production!.Lots.Single(value => value.LotId == use.Ingredient.LotId) : null;
        var raw = use?.Ingredient.Kind == IngredientLotKind.Source
            ? before.Inventory.Custody.Lots.Single(value => value.LotId == use.Ingredient.LotId) : null;
        var crafted = transformed is null ? null : before.Production!.Batches.Single(value => value.BatchId == transformed.Output.BatchId);
        File.WriteAllText(Path.Combine(directory, "metadata.json"), JsonSerializer.Serialize(new
        {
            fixture = name, sourceStat = true,
            setupContract = "Managed actual source-stat defeat, acknowledged finite care, victory and readiness; native continuation begins at this earned exploration boundary.",
            fixtureBoundary = name == "revoke"
                ? "Helper's raw aftermath dose is spent and no replacement was crafted; native revocation tests patient UI and persistence without a timing race."
                : "The named restorative selection is eligible at this retained exploration boundary.",
            seed = EncounterJourneyWorldFixture.SeedText, playerName = EncounterJourneyWorldFixture.PlayerName,
            worldId = before.WorldId.ToString(), branchId = before.BranchId.ToString(),
            admissionHash = before.AdmissionVersion.Value, profileVersion = before.ProfileVersion,
            playerActorId = fixture.Player.ToString(), helperActorId = fixture.Helper.ToString(),
            beforeRevision = before.Revision, beforeHash = before.ContentHash.Value,
            beforeCalendar = new { days = before.Calendar.Days, millisecondsWithinDay = before.Calendar.MillisecondsWithinDay },
            beforeAdvancingSteps = before.AdvancingSteps,
            beforeHp = player.CurrentHp, beforeMp = player.CurrentMp, maximumHp = player.Stats.MaxHp,
            beforeProgressionRevision = player.Revision,
            beforeAcknowledgments = before.Care.Acknowledgments.Count, beforeTreatments = before.Care.Treatments.Count,
            beforeOrdinaryUses = before.Care.Ordinary.Uses.Count, beforeConsumedDoses = before.Production!.ConsumedItems.Count,
            priorPermissionId = permission.PermissionId.ToString(), priorPermissionActive = permission.IsActive,
            priorPermissionRevision = permission.Revision, priorPermissionGrantHash = permission.GrantVersion.Value,
            expectedAction = use is null ? "RevokeCarePermission" : name == "helper" ? "HostHelperUseRestorativeItem" : "PlayerSelfUseRestorativeItem",
            expectedActorId = selected.ActorId.ToString(), expectedRecipientId = fixture.Player.ToString(),
            expectedHelperCommandId = name == "helper" ? selected.CommandId.ToString() : null,
            expectedHp = expected.Progression!.GetActor(fixture.Player).CurrentHp,
            expectedCappedHp = expected.Progression.GetActor(fixture.Player).CurrentHp,
            expectedMp = expected.Progression.GetActor(fixture.Player).CurrentMp,
            expectedCalendar = new { days = expected.Calendar.Days, millisecondsWithinDay = expected.Calendar.MillisecondsWithinDay },
            expectedAdvancingSteps = expected.AdvancingSteps,
            expectedAcknowledgments = expected.Care!.Acknowledgments.Count, expectedTreatments = expected.Care.Treatments.Count,
            expectedOrdinaryUses = expected.Care.Ordinary!.Uses.Count, expectedConsumedDoses = expected.Production!.ConsumedItems.Count,
            expectedPermissionActive = expected.Care.Ordinary.Permissions.Single().IsActive,
            expectedPermissionRevision = expected.Care.Ordinary.Permissions.Single().Revision,
            expectedIngredientKind = use?.Ingredient.Kind.ToString(), expectedIngredientLotId = use?.Ingredient.LotId.ToString(),
            expectedIngredientRevision = use?.Ingredient.ExpectedRevision, expectedIngredientQuantity = use?.Ingredient.Quantity,
            expectedSourceId = raw?.Source.SourceId.ToString(), expectedSourceStart = raw?.Source.Start,
            expectedSourceEnd = raw?.Source.End, expectedProductionBatchId = transformed?.Output.BatchId.ToString(),
            expectedProductionOutputIndex = transformed?.Output.OutputIndex, expectedProductionActorId = crafted?.ActorId.ToString(),
            expectedProductionCommandId = crafted?.CommandId.ToString(), expectedProductionRecipeId = crafted?.RecipeId.ToString(),
            expectedUseDefinitionId = use?.UseDefinitionId.ToString(), expectedUseDefinitionVersion = use?.UseDefinitionVersion.Value
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
