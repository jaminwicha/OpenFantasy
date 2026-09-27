using System.Buffers.Binary;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed class WorldResourceJourneyTests
{
    [Fact]
    public void Later_complete_world_failure_rolls_back_history_XP_materials_and_currency_together()
    {
        var fixture = new CareWorldFixture(rewardExperience: 1000, invalidNormalizedSpeedOnReward: true);
        var session = new CareWorldSession(fixture.Genesis);
        var command = fixture.PrepareQuestCompletion(session);
        var before = session.ReadStateForHost();
        var prepared = new DeliveryQuestPlanner().Plan(before, before.Inventory, before.Production!, before.Progression!,
            before.Quests!, command, before.Dialogues);
        Assert.Equal(WorldActionRejection.None, prepared.Rejection);
        Assert.True((ulong)prepared.CandidateProgression.GetActor(fixture.Player).Stats.Speed * 10 > uint.MaxValue);
        var result = session.Commit(fixture.Batch(before, WorldBatchKind.HeldInteraction, [command]));
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(result.Receipts).Rejection);
        Assert.Same(before.ResourceHistory, result.Candidate.ResourceHistory);
        Assert.Equal(before.Progression!.ContentHash, result.Candidate.Progression!.ContentHash);
        Assert.Equal(before.Inventory.ContentHash, result.Candidate.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, result.Candidate.Production!.ContentHash);
        Assert.Equal(before.Quests!.ContentHash, result.Candidate.Quests!.ContentHash);
        Assert.Equal(before.Reservoirs!.ContentHash, result.Candidate.Reservoirs!.ContentHash);
        RoundTrip(result.Candidate);
    }

    [Theory]
    [InlineData(0UL, false)]
    [InlineData(1UL, false)]
    [InlineData(1000UL, true)]
    public void Actual_funded_quest_retains_zero_XP_and_level_refill_after_combat(ulong experience, bool levelGain)
    {
        var fixture = new CareWorldFixture(rewardExperience: experience);
        var session = new CareWorldSession(fixture.Genesis);
        var won = fixture.PrepareVictory(session);
        var before = won.Progression!.GetActor(fixture.Player);
        Assert.True(before.CurrentHp < before.Stats.MaxHp);
        var rewarded = fixture.CompleteQuest(session);
        var batch = rewarded.ResourceHistory!.History.Batches[^1];
        var change = Assert.Single(batch.Changes);
        Assert.IsType<WorldRewardResourceEvidence>(rewarded.ResourceHistory.Evidence[^1]);
        Assert.Equal(ActorResourceOperation.ExperienceReward, change.Operation);
        Assert.Equal(experience, change.RequestedAmount);
        Assert.Equal(before.Revision + 1, change.After.Revision);
        Assert.Equal(levelGain, change.After.Level > before.Level);
        Assert.Equal(levelGain ? change.After.Stats.MaxHp : before.CurrentHp, change.After.CurrentHp);
        Assert.Equal(levelGain ? change.After.Stats.MaxMp : before.CurrentMp, change.After.CurrentMp);
        Assert.Equal(won.EncounterLifecycle!.Actions!.LastResolution!.TargetHpAfter,
            rewarded.EncounterLifecycle!.Actions!.LastResolution!.TargetHpAfter);
        Assert.Equal(6UL, rewarded.Inventory.Custody.GetStoredQuantity(rewarded.Construction!.Projects.Single().ProjectCustodianId));
        Assert.Equal(won.Progression.Currency.GetActorAccount(fixture.Player).Balance
            + rewarded.Quests!.Definitions.Single().Reward.CurrencyAmount,
            rewarded.Progression!.Currency.GetActorAccount(fixture.Player).Balance);
        RoundTrip(rewarded);
    }

    [Fact]
    public void Component_valid_wrong_rule_cause_fails_world_admission()
    {
        var fixture = new CareWorldFixture();
        var world = fixture.PrepareDefeat(new(fixture.Genesis));
        var history = world.ResourceHistory!;
        var original = history.History.Batches[0];
        var old = Assert.Single(original.Changes);
        var remedy = world.Care!.GetRemedy(fixture.Remedy);
        var forged = new ActorResourceChange(0, new(original.CommandId, remedy.DefinitionId, remedy.Version),
            old.Operation, old.RequestedAmount, old.Before, old.After);
        var batches = history.History.Batches.ToArray();
        batches[0] = new(original.WorldSequence, original.CommandOrdinal, original.CommandId, [forged]);
        var changed = new WorldResourceHistory(history.AnchorAdmission, new(history.History.Basis, 0, batches), history.Evidence);
        changed.History.ValidateCurrent(world.Progression!, world.Revision);
        Assert.Contains("complete ordered effects", Assert.Throws<ArgumentException>(() => Copy(world, changed)).Message);
    }

    [Fact]
    public void Earned_level_changes_the_stats_used_by_later_actual_attacks()
    {
        var fixture = new CareWorldFixture(rewardExperience: 1000);
        var session = new CareWorldSession(fixture.Genesis);
        var rewarded = fixture.CompleteQuest(session);
        var actor = rewarded.Progression!.GetActor(fixture.Player);
        Assert.True(actor.Level > fixture.Genesis.Progression!.GetActor(fixture.Player).Level);
        var close = fixture.Command(rewarded, fixture.Player, new CloseDialogueIntent(rewarded.Dialogues.Single().DialogueId));
        CareWorldFixture.Commit(session, fixture.Batch(rewarded, WorldBatchKind.HeldInteraction, [close]));
        var won = fixture.PrepareVictory(session);
        var history = won.ResourceHistory!;
        Assert.IsType<WorldRewardResourceEvidence>(history.Evidence[0]);
        var attack = history.Evidence.OfType<WorldEncounterResourceEvidence>().First(value => value.Request.ActorId == fixture.Player);
        var ability = won.EncounterLifecycle!.Abilities.Single(value => value.DefinitionId == attack.Resolution.AbilityDefinitionId);
        Assert.Equal(ability.Delay(checked((uint)actor.Stats.Speed * 10)), attack.Resolution.Delay);
        Assert.True(history.History.Batches.Count > 1);
        RoundTrip(won);
    }

    [Fact]
    public void Altered_claimed_random_draw_fails_even_when_actor_continuity_is_unchanged()
    {
        var fixture = new CareWorldFixture();
        var world = fixture.PrepareDefeat(new(fixture.Genesis));
        var history = world.ResourceHistory!;
        var original = Assert.IsType<WorldEncounterResourceEvidence>(history.Evidence[0]);
        var r = original.Resolution;
        var changed = new EncounterActionResolution(r.WorldSequence, r.TimingRevision, r.ActorId, r.DecisionCommandId,
            r.TargetActorId, r.AbilityDefinitionId, r.Status, r.Damage, r.TargetHpBefore, r.TargetHpAfter,
            r.ManaSpent, r.Delay, r.VariationPermille == 900 ? 901U : 900U, r.CancelledDecisionIds);
        var evidence = history.Evidence.ToArray();
        evidence[0] = new WorldEncounterResourceEvidence(original.ScenarioId, original.Request, changed);
        Assert.Contains("seeded draw", Assert.Throws<ArgumentException>(() => Copy(world,
            new(history.AnchorAdmission, history.History, evidence))).Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Paired_attack_cannot_omit_damage_or_magic_by_reverting_the_corresponding_current_actor(bool omitDamage)
    {
        var fixture = new CareWorldFixture(strikeMana: 1);
        var world = fixture.PrepareVictory(new(fixture.Genesis));
        var history = world.ResourceHistory!;
        var last = history.History.Batches[^1];
        Assert.Equal(2, last.Changes.Count);
        Assert.Equal(ActorResourceOperation.Damage, last.Changes[0].Operation);
        Assert.Equal(ActorResourceOperation.MagicExpenditure, last.Changes[1].Operation);
        RoundTrip(world);
        int missing = omitDamage ? 0 : 1;
        var keep = last.Changes[1 - missing];
        var remaining = new ActorResourceChange(0, keep.Cause, keep.Operation, keep.RequestedAmount, keep.Before, keep.After);
        var batches = history.History.Batches.ToArray();
        batches[^1] = new(last.WorldSequence, last.CommandOrdinal, last.CommandId, [remaining]);
        var reverted = last.Changes[missing].Before;
        var progression = world.Progression!;
        var forgedActors = new ProgressionSnapshot(progression.WorldId, progression.BranchId, progression.Revision,
            progression.Profiles, progression.Curves, progression.Actors.Select(actor => actor.ActorId == reverted.ActorId ? reverted : actor),
            progression.Currency);
        var forgedHistory = new WorldResourceHistory(history.AnchorAdmission, new(history.History.Basis, 0, batches), history.Evidence);
        forgedHistory.History.ValidateCurrent(forgedActors, world.Revision);
        Assert.Contains("complete ordered effects", Assert.Throws<ArgumentException>(() => Copy(world, forgedHistory, forgedActors)).Message);
    }

    [Fact]
    public void Admission_pin_and_basis_boundary_cannot_be_substituted()
    {
        var fixture = new CareWorldFixture();
        var world = fixture.PrepareDefeat(new(fixture.Genesis));
        var history = world.ResourceHistory!;
        var wrongPin = ContentHash.Compute("different admission"u8);
        Assert.Throws<ArgumentException>(() => Copy(world, new(wrongPin, history.History, history.Evidence)));
        var shifted = new ActorResourceHistory(history.History.Basis, 1, history.History.Batches);
        shifted.ValidateCurrent(world.Progression!, world.Revision);
        Assert.Throws<ArgumentException>(() => Copy(world, new(history.AnchorAdmission, shifted, history.Evidence)));
    }

    [Fact]
    public void Funded_XP_cannot_retain_an_unpaid_reward_or_invent_a_progression_revision()
    {
        var fixture = new CareWorldFixture(rewardExperience: 1);
        var session = new CareWorldSession(fixture.Genesis);
        var rewarded = fixture.CompleteQuest(session);
        var progression = rewarded.Progression!;
        var unpaid = new ProgressionSnapshot(progression.WorldId, progression.BranchId, progression.Revision,
            progression.Profiles, progression.Curves, progression.Actors, fixture.Genesis.Progression!.Currency);
        rewarded.ResourceHistory!.History.ValidateCurrent(unpaid, rewarded.Revision);
        Assert.Contains("funded currency", Assert.Throws<ArgumentException>(() => Copy(rewarded, rewarded.ResourceHistory, unpaid)).Message);
        var extraRevision = new ProgressionSnapshot(progression.WorldId, progression.BranchId, progression.Revision + 1,
            progression.Profiles, progression.Curves, progression.Actors, progression.Currency);
        Assert.Throws<ArgumentException>(() => Copy(rewarded, rewarded.ResourceHistory, extraRevision));
    }

    [Fact]
    public void World_resource_archive_refuses_truncation_trailing_fields_and_unknown_cause_kind()
    {
        var fixture = new CareWorldFixture();
        var state = fixture.PrepareDefeat(new(fixture.Genesis));
        byte[] bytes = WorldResourceHistoryCodecV1.Encode(state.ResourceHistory!);
        foreach (int length in new[] { 0, 3, bytes.Length - 1 })
            Assert.Throws<FormatException>(() => WorldResourceHistoryCodecV1.Decode(bytes.AsSpan(0, length)));
        Assert.Throws<FormatException>(() => WorldResourceHistoryCodecV1.Decode([.. bytes, 0]));
        byte[] altered = bytes.ToArray();
        int tag = altered.AsSpan().IndexOf("world.resource-attack.v1"u8);
        Assert.True(tag >= 0);
        altered[tag] = (byte)'?';
        Assert.Throws<FormatException>(() => WorldResourceHistoryCodecV1.Decode(altered));
    }

    [Fact]
    public void Reward_uses_actual_sorted_command_position_among_rejected_non_resource_actions()
    {
        var fixture = new CareWorldFixture(rewardExperience: 0);
        var session = new CareWorldSession(fixture.Genesis);
        var completion = fixture.PrepareQuestCompletion(session);
        var before = session.ReadStateForHost();
        // None of these residents owns the player's reading hold, so their close requests reject.
        var others = before.Residents.Where(resident => resident.ActorId != fixture.Player)
            .Select(resident => fixture.Command(before, resident.ActorId,
                new CloseDialogueIntent(before.Dialogues.Single().DialogueId))).ToArray();
        var batch = fixture.Batch(before, WorldBatchKind.HeldInteraction, [completion, .. others.Reverse()]);
        var result = session.Commit(batch);
        Assert.Equal(WorldActionRejection.None, result.Receipts.Single(receipt => receipt.CommandId == completion.CommandId).Rejection);
        Assert.All(result.Receipts.Where(receipt => receipt.CommandId != completion.CommandId),
            receipt => Assert.Equal(CommandReceiptStatus.Rejected, receipt.Status));
        int ordinal = batch.Actions.ToList().FindIndex(command => command.CommandId == completion.CommandId);
        Assert.True(ordinal > 0, "This seed must exercise a reward after a non-resource command.");
        var resourceBatch = Assert.Single(result.Candidate.ResourceHistory!.History.Batches);
        Assert.Equal((uint)ordinal, resourceBatch.CommandOrdinal);
        Assert.Equal(completion.CommandId, resourceBatch.CommandId);
        RoundTrip(result.Candidate);
    }

    [Fact]
    public void Released_episode_cannot_be_relabelled_as_a_fresh_caller_selected_random_stream()
    {
        var fixture = new CareWorldFixture();
        var world = fixture.PrepareVictory(new(fixture.Genesis));
        var lifecycle = world.EncounterLifecycle!;
        var actions = lifecycle.Actions!;
        var forgedActions = EncounterActionSnapshot.Begin(new(world.WorldId, world.BranchId, new EntityId(0xfeed, 99)),
            lifecycle.Seed, actions.Abilities, actions.Combatants);
        var forgedLifecycle = new EncounterLifecycleSnapshot(lifecycle.WorldId, lifecycle.BranchId, lifecycle.Seed,
            lifecycle.Revision, lifecycle.ExplorationSteps, lifecycle.TacticalTicks, lifecycle.EntryTacticalTicks,
            lifecycle.EncounterCount, lifecycle.Phase, lifecycle.Abilities, lifecycle.Scenarios, lifecycle.Debts,
            lifecycle.ScenarioId, forgedActions);
        Assert.Contains("authority-derived", Assert.Throws<ArgumentException>(() => Copy(world,
            world.ResourceHistory!, lifecycle: forgedLifecycle)).Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Actual_attacks_treatment_and_resumed_combat_keep_complete_causal_history(bool crafted)
    {
        var fixture = new CareWorldFixture(crafted);
        var session = new CareWorldSession(fixture.Genesis);
        RoundTrip(fixture.Genesis);
        var defeated = fixture.PrepareDefeat(session);
        Assert.NotEmpty(defeated.ResourceHistory!.Evidence.OfType<WorldEncounterResourceEvidence>());
        RoundTrip(defeated);
        var last = defeated.EncounterActions!.LastResolution!;
        int batches = defeated.ResourceHistory.History.Batches.Count;
        var acknowledgment = fixture.Acknowledge(defeated);
        var acknowledged = CareWorldFixture.Commit(session, fixture.Batch(defeated, WorldBatchKind.Care, [acknowledgment])).Candidate;
        Assert.Same(defeated.ResourceHistory, acknowledged.ResourceHistory);
        var administer = fixture.Administer(acknowledged);
        var treated = CareWorldFixture.Commit(session, fixture.Batch(acknowledged, WorldBatchKind.Care, [administer])).Candidate;
        Assert.Equal(40, treated.Progression!.GetActor(fixture.Player).CurrentHp);
        Assert.Equal(batches + 1, treated.ResourceHistory!.History.Batches.Count);
        var treatment = Assert.IsType<WorldTreatmentResourceEvidence>(treated.ResourceHistory.Evidence[^1]);
        Assert.Equal(administer.CommandId, treatment.CommandId);
        Assert.Single(treated.Production!.ConsumedItems);
        Assert.Equal(last.TargetHpAfter, treated.EncounterActions!.LastResolution!.TargetHpAfter);
        Assert.Equal(0U, last.TargetHpAfter);
        Assert.Equal(EncounterLifecyclePhase.Active, treated.EncounterLifecycle!.Phase);
        RoundTrip(treated);

        var after = treated;
        for (int i = 0; after.ResourceHistory!.History.Batches.Count == batches + 1; i++)
        {
            Assert.True(i < 128, "A resumed attack must resolve within the fixture's existing readiness bound.");
            after = fixture.NextCombatBoundary(session, playerAttacks: true);
        }
        Assert.IsType<WorldEncounterResourceEvidence>(after.ResourceHistory.Evidence[^1]);
        RoundTrip(after);
        Assert.Equal(treated.Production.ContentHash, after.Production!.ContentHash);
        Assert.All(after.ResourceHistory.History.Batches.SelectMany(batch => batch.Changes), change =>
            Assert.Equal(change.Before.Revision + 1, change.After.Revision));
    }

    [Fact]
    public void Released_episode_cannot_substitute_another_admitted_scenario_at_the_same_ordinal()
    {
        var fixture = new CareWorldFixture();
        var world = fixture.PrepareVictory(new(fixture.Genesis));
        var lifecycle = world.EncounterLifecycle!;
        var other = lifecycle.Scenarios.First(scenario => scenario.DefinitionId != lifecycle.ScenarioId);
        var identity = EncounterEpisodeIdentity.Derive(fixture.Genesis.EncounterLifecycle!, other.DefinitionId);
        var actions = EncounterActionSnapshot.Begin(new(world.WorldId, world.BranchId, identity),
            lifecycle.Seed, lifecycle.Abilities, other.Combatants);
        var forged = new EncounterLifecycleSnapshot(lifecycle.WorldId, lifecycle.BranchId, lifecycle.Seed,
            lifecycle.Revision, lifecycle.ExplorationSteps, lifecycle.TacticalTicks, lifecycle.EntryTacticalTicks,
            lifecycle.EncounterCount, lifecycle.Phase, lifecycle.Abilities, lifecycle.Scenarios, lifecycle.Debts,
            other.DefinitionId, actions);
        Assert.Contains("historical owner", Assert.Throws<ArgumentException>(() => Copy(world,
            world.ResourceHistory!, lifecycle: forged)).Message);
    }

    [Fact]
    public void Unknown_reward_participant_is_a_malformed_world_archive()
    {
        var fixture = new CareWorldFixture(rewardExperience: 1);
        var world = fixture.CompleteQuest(new(fixture.Genesis));
        var quests = world.Quests!;
        var participation = Assert.Single(quests.Participations);
        var unknown = new ActorQuestState(new EntityId(0xfeed, 100), participation.QuestId,
            participation.AcceptanceCommandId, participation.AcceptedAt,
            participation.CompletionCommandId, participation.CompletedAt);
        var changed = new QuestSnapshot(quests.WorldId, quests.BranchId, quests.Revision, quests.Definitions, [unknown]);
        Assert.Equal(QuestCodecV1.Encode(changed), QuestCodecV1.Encode(QuestCodecV1.Decode(QuestCodecV1.Encode(changed))));
        Assert.Contains("admitted progression basis", Assert.Throws<ArgumentException>(() => Copy(world,
            world.ResourceHistory!, quests: changed)).Message);

        byte[] bytes = WorldSnapshotCodec.Encode(world);
        var fields = ReadFrame(bytes);
        fields[14] = QuestCodecV1.Encode(changed);
        var malformed = WriteFrame(fields);
        var error = Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(malformed));
        Assert.IsType<ArgumentException>(error.InnerException);
    }

    [Fact]
    public void Stale_treatment_rejection_retains_resource_history_and_dose()
    {
        var fixture = new CareWorldFixture();
        var session = new CareWorldSession(fixture.Genesis);
        var defeated = fixture.PrepareDefeat(session);
        var acknowledged = CareWorldFixture.Commit(session,
            fixture.Batch(defeated, WorldBatchKind.Care, [fixture.Acknowledge(defeated)])).Candidate;
        var good = fixture.Administer(acknowledged);
        var stale = new WorldActionCommand(good.CommandId, good.PrincipalId, good.OriginSessionId,
            good.ActorId, good.WorldId, good.BranchId, good.ExpectedActorRevision + 1, good.Intent);
        var rejected = session.Commit(fixture.Batch(acknowledged, WorldBatchKind.Care, [stale]));
        Assert.Equal(CommandReceiptStatus.Rejected, Assert.Single(rejected.Receipts).Status);
        Assert.Same(acknowledged.ResourceHistory, rejected.Candidate.ResourceHistory);
        Assert.Equal(acknowledged.Progression!.ContentHash, rejected.Candidate.Progression!.ContentHash);
        Assert.Equal(acknowledged.Production!.ContentHash, rejected.Candidate.Production!.ContentHash);
        Assert.Equal(acknowledged.Inventory.ContentHash, rejected.Candidate.Inventory.ContentHash);
        RoundTrip(rejected.Candidate);
    }

    internal static void RoundTrip(WorldSnapshot world)
    {
        byte[] bytes = WorldSnapshotCodec.Encode(world);
        var restored = WorldSnapshotCodec.Decode(bytes);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(restored));
        Assert.Equal(world.Progression!.ContentHash, restored.Progression!.ContentHash);
        Assert.Equal(world.ResourceHistory!.ContentHash, restored.ResourceHistory!.ContentHash);
    }

    internal static WorldSnapshot Copy(WorldSnapshot world, WorldResourceHistory resources, ProgressionSnapshot? progression = null,
        EncounterLifecycleSnapshot? lifecycle = null, QuestSnapshot? quests = null) =>
        new(world.Inventory, world.Space, world.Residents, world.AdmissionVersion, world.Calendar, world.Revision, world.AdvancingSteps,
            world.Dialogues, world.Gathering, world.Production, progression ?? world.Progression, quests ?? world.Quests, world.Construction,
            reservoirs: world.Reservoirs, reservoirSeals: world.ReservoirSeals, liquidCells: world.LiquidCells,
            excavation: world.Excavation, pacing: world.Pacing, encounterLifecycle: lifecycle ?? world.EncounterLifecycle,
            care: world.Care, resourceHistory: resources);

    private static byte[][] ReadFrame(byte[] bytes)
    {
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8)));
        var fields = new byte[count][];
        int offset = 12;
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
            fields[index] = bytes.AsSpan(offset + 4, length).ToArray();
            offset += 4 + length;
        }
        Assert.Equal(bytes.Length, offset);
        return fields;
    }

    private static byte[] WriteFrame(byte[][] fields)
    {
        byte[] bytes = new byte[12 + fields.Sum(field => 4 + field.Length)];
        "OFC\0"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), checked((uint)fields.Length));
        int offset = 12;
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset), checked((uint)field.Length));
            field.CopyTo(bytes, offset + 4);
            offset += 4 + field.Length;
        }
        return bytes;
    }
}
