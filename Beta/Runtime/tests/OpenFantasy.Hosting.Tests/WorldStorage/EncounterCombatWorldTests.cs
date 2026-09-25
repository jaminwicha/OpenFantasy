using System.Numerics;
using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class EncounterCombatWorldTests
{
    [Fact]
    public void Actual_duel_recovers_queued_and_lethal_boundaries_with_one_receipt_draw_and_cost_per_action()
    {
        using var files = new StorageFiles();
        var fixture = new EncounterCombatWorldFixture();
        var genesis = fixture.Genesis;
        var evidence = new EncounterCombatEvidence(genesis);
        var history = new SortedDictionary<ulong, WorldSnapshot> { [0] = genesis };
        var queued = new List<(WorldActionCommand Command, WorldActionReceipt Receipt)>();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        WorldSnapshot final;
        int resolutions = 0;
        ulong ticks = 0;
        try
        {
            for (int boundary = 0; boundary < 1_000 && !WorldEncounterActions.IsOutcome(authority.ReadStateForHost()); boundary++)
            {
                var before = authority.ReadStateForHost();
                var timing = before.Pacing!.Encounter!;
                var due = WorldEncounterActions.DueActors(before);
                Assert.DoesNotContain(due, actor => actor.ActorId == fixture.Wren);
                var choices = due.Where(actor => !timing.Decisions.Any(choice => choice.ActorId == actor.ActorId))
                    .Select(actor => fixture.Attack(before, actor.ActorId, actor.ActorId == fixture.Player ? fixture.Wolf : fixture.Player))
                    .ToArray();
                var kind = choices.Length != 0 ? WorldBatchKind.EncounterDecision
                    : due.Count != 0 ? WorldBatchKind.EncounterResolution : WorldBatchKind.TacticalAdvance;
                var batch = fixture.Batch(before, kind, choices);
                WorldSnapshot? preview = null;
                bool first = false, lethal = false;
                if (kind == WorldBatchKind.EncounterResolution)
                {
                    preview = authority.Preview(batch).Candidate;
                    first = resolutions == 0;
                    lethal = WorldEncounterActions.IsOutcome(preview);
                    if (first || lethal)
                    {
                        // Both copies are closed actual authority saves with their original queued commands.
                        authority.Dispose();
                        evidence.BeforeResolution(files.Path, before, batch, first, lethal);
                        byte[] closed = File.ReadAllBytes(files.Path);
                        authority = SqliteWorldAuthority.OpenExisting(files.Path);
                        Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
                        authority.Dispose();
                        Assert.Equal(closed, File.ReadAllBytes(files.Path));
                        authority = SqliteWorldAuthority.OpenExisting(files.Path);
                        history[before.Revision] = before;
                        foreach (var command in before.EncounterActions!.Pending)
                        {
                            var receipt = authority.FindReceipt(command, fixture.DeliveryFor(command.ActorId));
                            Assert.NotNull(receipt);
                            Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
                            Assert.True(receipt.CommitSequence <= before.Revision);
                        }
                    }
                }
                WorldStepDecision decision = authority.Commit(batch);
                var after = decision.Candidate;
                evidence.Record(after, batch, decision.Receipts);
                Assert.Equal(before.Revision + 1, after.Revision);
                AssertRetainedOwners(genesis, after, fixture.Wren);
                if (kind == WorldBatchKind.EncounterDecision)
                {
                    Assert.Equal(choices.Length, decision.Receipts.Count);
                    Assert.Equal(before.Progression!.ContentHash, after.Progression!.ContentHash);
                    Assert.Equal(before.EncounterActions!.Random, after.EncounterActions!.Random);
                    foreach (var command in choices)
                    {
                        var receipt = Assert.Single(decision.Receipts, value => value.CommandId == command.CommandId);
                        Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
                        Assert.Equal(after.Revision, receipt.CommitSequence);
                        Assert.Equal(command.ContentHash, receipt.CommandHash);
                        Assert.Contains(after.Pacing!.Encounter!.Decisions,
                            value => value.DecisionId == command.CommandId && value.ActionPin == command.ContentHash);
                        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(
                            authority.FindReceipt(command, fixture.DeliveryFor(command.ActorId))!));
                        queued.Add((command, receipt));
                    }
                }
                else
                {
                    Assert.Empty(decision.Receipts); // Resolution never creates a second queue-command receipt.
                }
                if (kind == WorldBatchKind.EncounterResolution)
                {
                    Assert.Equal(WorldSnapshotCodec.Encode(preview!), WorldSnapshotCodec.Encode(after));
                    AssertPhysicalResolution(before, after);
                    resolutions++;
                    evidence.AfterResolution(after, first, lethal);
                    if (first || lethal) history[after.Revision] = after;
                }
                if (kind == WorldBatchKind.TacticalAdvance)
                {
                    ticks++;
                    Assert.Equal(before.Progression!.ContentHash, after.Progression!.ContentHash);
                    Assert.Equal(before.EncounterActions!.ContentHash, after.EncounterActions!.ContentHash);
                }
                Assert.Equal(genesis.Calendar.AddMilliseconds(checked((long)ticks * 50)), after.Calendar);
                Assert.Equal(ticks, after.AdvancingSteps);
                Assert.Equal(ticks * 5, after.Pacing!.Encounter!.ElapsedTacticalUnits);
                Assert.Equal(ticks / 36, after.ProcessIntervals);
                Assert.All(after.Residents, resident => Assert.Equal(after.Pacing.Progress.Elapsed, resident.ExperiencedTime));
                if (after.ProcessIntervals == before.ProcessIntervals)
                {
                    Assert.Equal(before.Gathering!.ContentHash, after.Gathering!.ContentHash);
                    Assert.Equal(before.LiquidCells!.ContentHash, after.LiquidCells!.ContentHash);
                }
                else
                {
                    Assert.Equal(before.Gathering!.AdvanceStep(after.ProcessIntervals).ContentHash, after.Gathering!.ContentHash);
                    var liquid = new LiquidCellPlanner().PlanAdvance(before.LiquidCells!, after.Pacing.ProcessBoundary).ApplyTo(before.LiquidCells!);
                    Assert.Equal(liquid.ContentHash, after.LiquidCells!.ContentHash);
                }
            }
            final = authority.ReadStateForHost();
            Assert.True(WorldEncounterActions.IsOutcome(final), "The actual source-admitted duel exceeded 1,000 accepted boundaries.");
            Assert.True(resolutions > 1);
            var incapacitated = Assert.Single(final.Progression!.Actors, actor => actor.CurrentHp == 0);
            Assert.False(WorldEncounterActions.CanParticipate(final, incapacitated.ActorId));
            Assert.DoesNotContain(WorldEncounterActions.DueActors(final), actor => actor.ActorId == incapacitated.ActorId);
            Assert.False(WorldEncounterActions.HasRequiredDecisions(final));
            Assert.Empty(final.EncounterActions!.Pending);
            Assert.Empty(final.Pacing!.Encounter!.Decisions);
            Assert.Equal(genesis.Space.Actors.Count, final.Space.Actors.Count); // HP0 does not claim death/removal.
            Assert.Equal((ulong)queued.Count, authority.InspectForHost().Journal.CommandOutcomeCount);
            foreach (var (command, receipt) in queued)
            {
                Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(
                    authority.FindReceipt(command, fixture.DeliveryFor(command.ActorId))!));
            }
            var original = queued[0].Command;
            Assert.Throws<AuthorityDispatchException>(() => authority.FindReceipt(original, fixture.DeliveryFor(
                original.ActorId == fixture.Player ? fixture.Wolf : fixture.Player)));
            var conflict = new WorldActionCommand(original.CommandId, original.PrincipalId, original.OriginSessionId,
                original.ActorId, original.WorldId, original.BranchId, original.ExpectedActorRevision,
                new QueueEncounterWaitIntent(final.Pacing.Encounter.EncounterId, 0));
            Assert.Throws<AuthorityDispatchException>(() => authority.FindReceipt(conflict, fixture.DeliveryFor(original.ActorId)));
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(fixture.Batch(final, WorldBatchKind.TacticalAdvance)));
            Assert.Equal(final.ContentHash, authority.ReadStateForHost().ContentHash);
        }
        finally { authority.Dispose(); }
        evidence.Complete(files.Path, fixture, final);
        byte[] saved = File.ReadAllBytes(files.Path);
        using (var restored = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(restored.ReadStateForHost()));
            foreach (var state in history.Values.Reverse())
            {
                var recorded = restored.ReadRecordedStateForHost(state.Revision);
                Assert.Equal(WorldSnapshotCodec.Encode(state), WorldSnapshotCodec.Encode(recorded.State));
                Assert.Equal(final.Revision, recorded.SourceHeadRevision);
                Assert.Equal(final.ContentHash, recorded.SourceHeadHash);
            }
        }
        Assert.Equal(saved, File.ReadAllBytes(files.Path));
        using (var database = WorldDatabase.Open(files.Path, create: false))
        {
            var identities = database.ReadIdentities().ToArray();
            Assert.Equal(final.Revision, (ulong)identities.Count(identity => identity.Kind == WorldIdentityKind.CommitCause));
            Assert.Equal(queued.Count, identities.Count(identity => identity.Kind == WorldIdentityKind.Command));
            Assert.Equal(queued.Count, identities.Count(identity => identity.Kind == WorldIdentityKind.Result));
            Assert.DoesNotContain(identities, identity => identity.Kind is WorldIdentityKind.SpawnedEntity or WorldIdentityKind.AdmittedDefinition);
            Assert.Equal(identities.Length, identities.Select(identity => identity.Identity).Distinct().Count());
        }
        Assert.Equal(saved, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public void Refused_choices_and_unauthorized_delivery_cannot_spend_resources_draw_or_bypass_required_decisions()
    {
        using var files = new StorageFiles();
        var fixture = new EncounterCombatWorldFixture();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var original = fixture.Genesis;
        Assert.Throws<AuthorityDispatchException>(() => authority.Commit(fixture.Batch(original, WorldBatchKind.EncounterResolution)));
        Assert.Throws<AuthorityDispatchException>(() => authority.Commit(fixture.Batch(original, WorldBatchKind.TacticalAdvance)));
        var refusalCases = new (Func<WorldSnapshot, WorldActionCommand> Request, WorldActionRejection Rejection)[]
        {
            (state => fixture.Attack(state, fixture.Player, fixture.Wolf, timingRevision: 99), WorldActionRejection.RevisionConflict),
            (state => fixture.Attack(state, fixture.Player, fixture.Wren), WorldActionRejection.Unavailable),
            (state => fixture.Attack(state, fixture.Player, fixture.Player), WorldActionRejection.Unavailable),
            (state => fixture.Attack(state, fixture.Player, fixture.Wolf, ability: fixture.Bite), WorldActionRejection.Unavailable),
            (state => StaleActor(fixture.Attack(state, fixture.Player, fixture.Wolf)), WorldActionRejection.RevisionConflict),
            (state => fixture.Wait(state, fixture.Wren), WorldActionRejection.Unavailable)
        };
        foreach (var (request, expected) in refusalCases)
        {
            var before = authority.ReadStateForHost();
            var command = request(before);
            var batch = fixture.Batch(before, WorldBatchKind.EncounterDecision, [command]);
            var result = authority.Commit(batch);
            var receipt = Assert.Single(result.Receipts);
            Assert.Equal(CommandReceiptStatus.Rejected, receipt.Status);
            Assert.Equal(expected, receipt.Rejection);
            Assert.Equal(original.Calendar, result.Candidate.Calendar);
            Assert.Equal(original.Progression!.ContentHash, result.Candidate.Progression!.ContentHash);
            Assert.Equal(original.Pacing!.ContentHash, result.Candidate.Pacing!.ContentHash);
            Assert.Equal(original.EncounterActions!.ContentHash, result.Candidate.EncounterActions!.ContentHash);
            AssertRetainedOwners(original, result.Candidate, fixture.Wren);
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(
                authority.FindReceipt(command, fixture.DeliveryFor(command.ActorId))!));
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(batch));
            Assert.True(WorldEncounterActions.HasRequiredDecisions(result.Candidate));
        }
        var current = authority.ReadStateForHost();
        var authorized = fixture.Attack(current, fixture.Player, fixture.Wolf);
        var wolfControl = fixture.DeliveryFor(fixture.Wolf);
        var forged = new WorldActionCommand(authorized.CommandId, wolfControl.PrincipalId, wolfControl.SessionId,
            authorized.ActorId, authorized.WorldId, authorized.BranchId, authorized.ExpectedActorRevision, authorized.Intent);
        var usage = authority.InspectForHost().Journal;
        Assert.Throws<AuthorityDispatchException>(() => authority.Commit(fixture.Batch(current, WorldBatchKind.EncounterDecision, [forged])));
        Assert.Throws<AuthorityDispatchException>(() => authority.FindReceipt(authorized, wolfControl));
        Assert.Equal(current.ContentHash, authority.ReadStateForHost().ContentHash);
        Assert.Equal(usage, authority.InspectForHost().Journal);
        Assert.Null(authority.FindReceipt(authorized, fixture.Delivery));
    }

    private static WorldActionCommand StaleActor(WorldActionCommand command) => new(command.CommandId,
        command.PrincipalId, command.OriginSessionId, command.ActorId, command.WorldId, command.BranchId,
        checked(command.ExpectedActorRevision + 1), command.Intent);

    [Fact]
    public void Alternate_authored_extreme_attack_commits_target_HP_zero_and_replays_without_intermediate_overflow()
    {
        using var files = new StorageFiles();
        var fixture = new EncounterCombatWorldFixture();
        var original = fixture.Genesis;
        var player = original.Progression!.GetActor(fixture.Player);
        var stats = player.Stats;
        var profile = new ActorStatProfileDefinition(fixture.NextId(), "extreme_attack_control", "Authored arithmetic control",
            ActorStatProfileKind.Unassigned, new(stats.MaxHp, stats.MaxMp, int.MaxValue, stats.Magic, stats.Defense, stats.Speed),
            ActorStatModifiers.Zero, ActorStatModifiers.Zero);
        var changedPlayer = new ActorProgressionState(player.ActorId, 0, profile, player.Curve, player.Level,
            player.Experience, player.CurrentHp, player.CurrentMp);
        var progression = new ProgressionSnapshot(original.WorldId, original.BranchId, 0,
            original.Progression.Profiles.Append(profile), original.Progression.Curves,
            original.Progression.Actors.Select(actor => actor.ActorId == player.ActorId ? changedPlayer : actor),
            original.Progression.Currency);
        var oldAbility = original.EncounterActions!.GetAbility(fixture.Strike);
        var ability = new EncounterAbilityDefinition(oldAbility.DefinitionId, oldAbility.Key, "Authored maximum-power control",
            ControlPin(original), 1_000, oldAbility.RangeSubunits, oldAbility.ReadinessCostNumerator,
            oldAbility.ReadinessCostDenominator, oldAbility.ManaCost, oldAbility.MinimumLevel, oldAbility.RequiredFunction);
        var actions = EncounterActionSnapshot.Begin(original.EncounterActions.Scope, original.EncounterActions.Seed,
            original.EncounterActions.Abilities.Select(value => value.DefinitionId == ability.DefinitionId ? ability : value),
            original.EncounterActions.Combatants);
        var authored = AuthoredControl(original, progression: progression, actions: actions);
        WorldSnapshot final;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, authored))
        {
            var choices = new[] { fixture.Attack(authored, fixture.Player, fixture.Wolf), fixture.Wait(authored, fixture.Wolf) };
            var queued = authority.Commit(fixture.Batch(authored, WorldBatchKind.EncounterDecision, choices));
            Assert.All(queued.Receipts, receipt => Assert.Equal(CommandReceiptStatus.Applied, receipt.Status));
            for (int boundary = 0; boundary < 2 && !WorldEncounterActions.IsOutcome(authority.ReadStateForHost()); boundary++)
            {
                var state = authority.ReadStateForHost();
                authority.Commit(fixture.Batch(state, WorldBatchKind.EncounterResolution));
            }
            final = authority.ReadStateForHost();
            var resolution = final.EncounterActions!.LastResolution!;
            Assert.Equal(EncounterActionResolutionStatus.Damaged, resolution.Status);
            Assert.Equal(fixture.Player, resolution.ActorId);
            Assert.Equal((uint)original.Progression.GetActor(fixture.Wolf).CurrentHp, resolution.Damage);
            Assert.Equal(0, final.Progression!.GetActor(fixture.Wolf).CurrentHp);
            Assert.Equal(0UL, final.AdvancingSteps);
            Assert.True(WorldEncounterActions.IsOutcome(final));
            Assert.Equal(2UL, authority.InspectForHost().Journal.CommandOutcomeCount);
            Assert.Equal(original.Inventory.ContentHash, final.Inventory.ContentHash);
            Assert.Equal(original.Production!.ContentHash, final.Production!.ContentHash);
        }
        byte[] closed = File.ReadAllBytes(files.Path);
        using (var restored = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(restored.ReadStateForHost()));
            Assert.Equal(WorldSnapshotCodec.Encode(authored), WorldSnapshotCodec.Encode(restored.ReadRecordedStateForHost(0).State));
        }
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
        Assert.NotEqual(profile.Version, player.Profile.Version); // This is a separate authored control, not the source Squire.
        Assert.Equal(stats.Attack, original.Progression.GetActor(fixture.Player).Stats.Attack);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Alternate_authored_loss_of_actual_hands_or_jaw_refuses_only_the_corresponding_physical_ability(bool wolf)
    {
        using var files = new StorageFiles();
        var fixture = new EncounterCombatWorldFixture();
        var original = fixture.Genesis;
        var actorId = wolf ? fixture.Wolf : fixture.Player;
        var targetId = wolf ? fixture.Player : fixture.Wolf;
        var function = wolf ? BodyFunction.NaturalWeapon : BodyFunction.Manipulation;
        var actor = original.Space.GetActor(actorId);
        var slots = actor.Condition.FunctionalSlots(function);
        Assert.NotEmpty(slots);
        var injuries = slots.Select(slot => new BodyInjury(fixture.NextId(), fixture.NextId(), slot,
            BodyInjuryKind.MissingPart)).ToArray();
        var condition = new BodyConditionSnapshot(actor.Condition.FormInstanceId, actor.Condition.Definition,
            actor.Condition.Injuries.Concat(injuries));
        Assert.False(condition.SupportsFunction(function));
        var altered = new ActorPresence(actor.Binding, condition, actor.Scope, actor.Feet, actor.Revision, actor.HomePlaceId);
        var space = original.Space.WithActors(original.Space.Actors.Select(value => value.ActorId == actorId ? altered : value));
        var authored = AuthoredControl(original, space: space);
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, authored);
        var command = fixture.Attack(authored, actorId, targetId);
        var refused = authority.Commit(fixture.Batch(authored, WorldBatchKind.EncounterDecision, [command]));
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(refused.Receipts).Rejection);
        Assert.Equal(authored.Progression!.ContentHash, refused.Candidate.Progression!.ContentHash);
        Assert.Equal(authored.EncounterActions!.ContentHash, refused.Candidate.EncounterActions!.ContentHash);
        Assert.Equal(authored.Calendar, refused.Candidate.Calendar);
        var other = fixture.Attack(refused.Candidate, targetId, actorId);
        Assert.Equal(WorldActionRejection.None,
            WorldEncounterActions.EvaluateAbility(refused.Candidate, targetId, (QueueEncounterAbilityIntent)other.Intent));
        Assert.True(original.Space.GetActor(actorId).Condition.SupportsFunction(function));
        Assert.Equal(condition.ComputeContentHash(), refused.Candidate.Space.GetActor(actorId).Condition.ComputeContentHash());
    }

    private static ContentHash ControlPin(WorldSnapshot basis) => ContentHash.Compute(
        Encoding.UTF8.GetBytes("encounter.actions.authored-test-control.v1").Concat(basis.ContentHash.ToByteArray()).ToArray());

    private static WorldSnapshot AuthoredControl(WorldSnapshot basis, LocalSpatialSnapshot? space = null,
        ProgressionSnapshot? progression = null, EncounterActionSnapshot? actions = null) => new(basis.Inventory,
        space ?? basis.Space, basis.Residents, ControlPin(basis), basis.Calendar, 0, 0, basis.Dialogues,
        basis.Gathering, basis.Production, progression ?? basis.Progression, basis.Quests, basis.Construction,
        reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
        excavation: basis.Excavation, pacing: basis.Pacing, encounterActions: actions ?? basis.EncounterActions);

    private static void AssertPhysicalResolution(WorldSnapshot before, WorldSnapshot after)
    {
        var result = after.EncounterActions!.LastResolution!;
        Assert.Equal(EncounterActionResolutionStatus.Damaged, result.Status);
        Assert.Equal(after.Revision, result.WorldSequence);
        Assert.Equal(after.Pacing!.Encounter!.Revision, result.TimingRevision);
        var source = before.Progression!.GetActor(result.ActorId);
        var target = before.Progression.GetActor(result.TargetActorId!.Value);
        var ability = before.EncounterActions!.GetAbility(result.AbilityDefinitionId!.Value);
        BigInteger baseDamage = BigInteger.Max(1, (BigInteger)source.Stats.Attack * ability.PowerTenths / 10 - target.Stats.Defense / 2);
        uint rawDamage = checked((uint)BigInteger.Max(1, baseDamage * result.VariationPermille / 1000));
        uint damage = Math.Min((uint)target.CurrentHp, rawDamage);
        Assert.Equal(damage, result.Damage);
        Assert.Equal((uint)target.CurrentHp, result.TargetHpBefore);
        Assert.Equal((uint)target.CurrentHp - damage, result.TargetHpAfter);
        Assert.Equal((int)result.TargetHpAfter, after.Progression!.GetActor(target.ActorId).CurrentHp);
        Assert.Equal(source.CurrentMp - (int)ability.ManaCost, after.Progression.GetActor(source.ActorId).CurrentMp);
        Assert.Equal(ability.ManaCost, result.ManaSpent);
        BigInteger numerator = (BigInteger)10_000 * ability.ReadinessCostNumerator;
        BigInteger denominator = (BigInteger)ability.ReadinessCostDenominator * before.Pacing!.Encounter!.GetActor(source.ActorId).NormalizedSpeed;
        Assert.Equal((ulong)((numerator + denominator - 1) / denominator), result.Delay);
        Assert.Equal(result.Delay, after.Pacing.Encounter.GetActor(source.ActorId).RemainingDelay);
        var random = before.EncounterActions.CreateRandom();
        Assert.Equal(900U + (uint)random.NextUInt64(201), result.VariationPermille);
        Assert.Equal(random.CaptureState(), after.EncounterActions.Random);
        Assert.Equal(before.Calendar, after.Calendar);
        Assert.DoesNotContain(after.EncounterActions.Pending, command => command.CommandId == result.DecisionCommandId);
        foreach (var actor in before.Progression.Actors.Where(actor => actor.ActorId != target.ActorId && actor.ActorId != source.ActorId))
        {
            var retained = after.Progression.GetActor(actor.ActorId);
            Assert.Equal((actor.Revision, actor.CurrentHp, actor.CurrentMp),
                (retained.Revision, retained.CurrentHp, retained.CurrentMp));
        }
    }

    private static void AssertRetainedOwners(WorldSnapshot original, WorldSnapshot current, EntityId bystander)
    {
        Assert.Equal(23, current.ProfileVersion);
        Assert.Equal(original.Space.Regions.Select(region => region.ComputeContentHash()), current.Space.Regions.Select(region => region.ComputeContentHash()));
        Assert.Equal(original.Space.Actors.Select(actor => (actor.ActorId, actor.Revision, actor.Feet, actor.HomePlaceId,
                actor.Condition.ComputeContentHash())),
            current.Space.Actors.Select(actor => (actor.ActorId, actor.Revision, actor.Feet, actor.HomePlaceId,
                actor.Condition.ComputeContentHash())));
        Assert.Equal(original.Inventory.ContentHash, current.Inventory.ContentHash);
        Assert.Equal(original.Production!.ContentHash, current.Production!.ContentHash);
        Assert.Equal(original.Construction!.ContentHash, current.Construction!.ContentHash);
        Assert.Equal(original.Excavation!.ContentHash, current.Excavation!.ContentHash);
        Assert.Equal(original.Quests!.ContentHash, current.Quests!.ContentHash);
        Assert.Equal(original.Progression!.Currency.Accounts.Select(account => (account.AccountId, account.Balance)),
            current.Progression!.Currency.Accounts.Select(account => (account.AccountId, account.Balance)));
        Assert.Equal(original.Progression.Actors.Select(actor => (actor.ActorId, actor.Level, actor.Experience, actor.Stats)),
            current.Progression.Actors.Select(actor => (actor.ActorId, actor.Level, actor.Experience, actor.Stats)));
        Assert.Equal(original.Progression.GetActor(bystander).CurrentHp, current.Progression.GetActor(bystander).CurrentHp);
        Assert.DoesNotContain(current.Pacing!.Encounter!.Decisions, decision => decision.ActorId == bystander);
        Assert.DoesNotContain(current.EncounterActions!.Pending, command => command.ActorId == bystander);
    }
}
