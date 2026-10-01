using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.Retreat;

/// <summary>
/// Real history and normal coordinated archive policy across a walking retreat. The existing source-backed care
/// endurance fixture declares large initial HP/MP and short delays before any play; it is a stress genesis, not the
/// native cargo balance. The public fresh admissions compose ordinary care, cargo, markets and retreat. Every bite,
/// decision, tick and movement is then committed through the durable authority. No late root or counter is installed.
/// </summary>
public sealed class RetreatArchiveCutTests
{
    [Fact]
    public void Actual_bites_trigger_a_coordinated_archive_during_retreat_and_reopen_with_exact_continuation()
    {
        var fixture = new CareWorldFixture(enduranceProfile: true);
        WorldSnapshot genesis = Compose(fixture);
        string directory = Path.Combine(Path.GetTempPath(), "openfantasy-retreat-archive-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string save = Path.Combine(directory, "world.sqlite");
        string prefix = Path.Combine(directory, "prefix.sqlite");
        WorldSnapshot final;
        WorldSnapshot? closing = null;
        WorldMaterialCareArchiveSegment? archived = null;
        WorldActionCommand? retreat = null;
        WorldActionReceipt? receipt = null;
        WorldStepBatch? retreatBatch = null;
        ulong publication = 0;
        try
        {
            using (var authority = SqliteWorldAuthority.CreateNew(save, genesis))
            {
                var session = new CareWorldSession(authority);
                fixture.BeginCombat(session);
                var opponents = new LocalEncounterController();
                for (int boundary = 0; ; boundary++)
                {
                    Assert.True(boundary < 3_000, "The actual attack/retreat journey exceeded its boundary budget.");
                    WorldSnapshot state = authority.ReadStateForHost();
                    if (state.Retreat!.Last?.Status == EncounterRetreatStatus.Disengaged) break;
                    Assert.True(state.Progression!.GetActor(fixture.Player).CurrentHp > 0);
                    var timing = state.Pacing!.Encounter!;
                    var due = WorldEncounterActions.DueActors(state);
                    List<WorldActionCommand> actions = [];
                    if (due.Any(actor => actor.ActorId == fixture.Player)
                        && !timing.Decisions.Any(value => value.ActorId == fixture.Player)
                        && !WorldEncounterRetreats.HoldsActor(state, fixture.Player))
                    {
                        if (retreat is null && state.ResourceHistory!.History.Batches.Count >= 253)
                        {
                            Assert.InRange(state.ResourceHistory.History.Batches.Count, 253, 254);
                            var route = RouteWithOpeningContact(state, fixture.Player, fixture.Wolf);
                            retreat = fixture.Command(state, fixture.Player, new QueueEncounterRetreatIntent(
                                timing.EncounterId, timing.Revision,
                                QueueEncounterRetreatIntent.Endpoint(state.Space.GetActor(fixture.Player).Feet, route), route));
                            actions.Add(retreat);
                        }
                        else
                        {
                            actions.Add(fixture.Command(state, fixture.Player,
                                new QueueEncounterWaitIntent(timing.EncounterId, timing.Revision)));
                        }
                    }
                    actions.AddRange(opponents.SelectChoices(state, RetreatJourneyWorld.NewId));
                    var kind = actions.Count != 0 ? WorldBatchKind.EncounterDecision
                        : due.Count != 0 ? WorldBatchKind.EncounterResolution : WorldBatchKind.TacticalAdvance;
                    WorldStepBatch batch = fixture.Batch(state, kind, actions);
                    WorldStepDecision preview = authority.Preview(batch);
                    if (preview.SealedMaterialCareSegment is { } segment)
                    {
                        Assert.Null(archived);
                        Assert.NotNull(retreat);
                        var active = Assert.IsType<EncounterRetreatContinuation>(state.Retreat.Active);
                        Assert.True(active.HasStarted, "The cut must close a genuinely walking continuation.");
                        Assert.Equal(retreat.CommandId, active.Command.CommandId);
                        Assert.Equal(255, state.ResourceHistory!.History.Batches.Count);
                        Assert.Equal(0UL, authority.InspectForHost().Journal.MaterialCareArchiveCount);
                        authority.BackupForHost(prefix);
                        closing = state;
                        archived = segment;
                        Assert.Equal(WorldSnapshotCodec.Encode(state), segment.Encode());
                        Assert.Equal(EncounterRetreatCodecV1.Encode(state.Retreat),
                            EncounterRetreatCodecV1.Encode(segment.Continued.Retreat!));
                        Assert.Equal(state.ContentHash, authority.ReadStateForHost().ContentHash);
                    }
                    WorldStepDecision accepted = authority.Commit(batch);
                    Assert.All(accepted.Receipts, value => Assert.Equal(WorldActionRejection.None, value.Rejection));
                    if (actions.Any(value => value.CommandId == retreat?.CommandId))
                    {
                        retreatBatch = batch;
                        receipt = accepted.Receipts.Single(value => value.CommandId == retreat!.CommandId);
                    }
                    if (accepted.SealedMaterialCareSegment is { } published)
                    {
                        publication = accepted.Candidate.Revision;
                        Assert.Equal(archived!.Encode(), published.Encode());
                        Assert.Equal(1UL, authority.InspectForHost().Journal.MaterialCareArchiveCount);
                        Assert.NotNull(accepted.Candidate.Retreat!.Active);
                    }
                }
                final = authority.ReadStateForHost();
                Assert.NotNull(archived);
                Assert.True(final.Revision > publication);
                Assert.Equal(retreat!.CommandId, final.Retreat!.Last!.CommandId);
                Assert.Equal(EncounterLifecyclePhase.Exploration, final.EncounterLifecycle!.Phase);
                Assert.Equal(0UL, authority.InspectForHost().Journal.ResourceArchiveCount);
                Assert.Equal(EncounterRetreatRules.SampleDelay((uint)(final.Progression!.GetActor(fixture.Player).Stats.Speed * 10)),
                    final.EncounterLifecycle.GetDebt(fixture.Player).RemainingDelay);
            }

            // Complete pre-cut journal, receipts, identities and checkpoint prefix remains immutable.
            using (var before = WorldDatabase.Open(prefix, create: false))
            using (var after = WorldDatabase.Open(save, create: false))
            {
                ulong boundary = before.ReadHead().Sequence;
                Assert.Equal(closing!.Revision, boundary);
                Assert.Equal(before.ReadJournal().Select(Row), after.ReadJournal().Where(value => value.Sequence <= boundary).Select(Row));
                Assert.Equal(before.ReadCommandOutcomes().Select(CommandRow),
                    after.ReadCommandOutcomes().Where(value => value.Sequence <= boundary).Select(CommandRow));
                Assert.Subset(after.ReadIdentities().ToHashSet(), before.ReadIdentities().ToHashSet());
                Assert.Equal(before.ReadCheckpoints().Select(value => Convert.ToHexString(value.State)),
                    after.ReadCheckpoints().Where(value => value.Sequence <= boundary).Select(value => Convert.ToHexString(value.State)));
            }
            byte[] closed = File.ReadAllBytes(save);
            using (var recovered = SqliteWorldAuthority.OpenExisting(save, WorldSnapshotCodec.Encode(genesis)))
            {
                WorldSnapshot actual = recovered.ReadStateForHost();
                Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(actual));
                Assert.Equal(WorldSnapshotCodec.Encode(closing!),
                    WorldSnapshotCodec.Encode(recovered.ReadRecordedStateForHost(closing!.Revision).State));
                var retrieved = recovered.ReadMaterialCareArchiveForHost(0);
                Assert.Equal(archived!.Encode(), retrieved.Segment.Encode());
                Assert.Equal(archived.Reference, retrieved.Segment.Reference);
                Assert.Equal(publication, retrieved.PublicationSequence);
                Assert.Equal(closing.Revision + 1, publication);
                Assert.Equal((actual.Revision, actual.ContentHash), (retrieved.SourceHeadRevision, retrieved.SourceHeadHash));
                var delivery = new TrustedCommandDelivery(retreat!.PrincipalId, retreat.OriginSessionId);
                Assert.Equal(WorldReceiptCodecV1.Encode(receipt!),
                    WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(recovered.FindReceipt(retreat, delivery))));
                Assert.True(recovered.IsIdentityOccupiedForHost(retreat.CommandId));
                Assert.Throws<AuthorityDispatchException>(() => recovered.Commit(retreatBatch!));
                Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(recovered.ReadStateForHost()));
            }
            Assert.Equal(closed, File.ReadAllBytes(save));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static WorldSnapshot Compose(CareWorldFixture fixture)
    {
        var basis = fixture.Genesis;
        var remedy = basis.Care!.Remedies.Single();
        var form = basis.Space.GetActor(fixture.Player).Binding;
        var self = new RestorativeUseDefinition(new EntityId(0x7265747265617434, 1), remedy.DefinitionId, remedy.Version,
            RestorativeDeliveryMode.SelfUse, [new RestorativeFormBinding(form.DefinitionId, form.DefinitionVersion)]);
        WorldSnapshot ordinary = WorldRestorativeAdmission.CreateFresh(basis, new OrdinaryCareSnapshot([self], [], []));
        WorldSnapshot genesis = WorldEncounterRetreatAdmission.CreateFresh(CargoWorldAdmission.Compose(ordinary, CargoCircuitSources.LoadEmbedded()));
        Assert.Equal((0UL, 0UL), (genesis.Revision, genesis.AdvancingSteps));
        Assert.Empty(genesis.ResourceHistory!.History.Batches);
        return genesis;
    }

    /// <summary>
    /// A lawful captured route briefly gives ground toward the opponent and back before following the clear escape.
    /// The opening contact permits two actual bites while the retreat is walking; it never changes stats or positions.
    /// </summary>
    private static IReadOnlyList<EncounterRetreatDirection> RouteWithOpeningContact(WorldSnapshot state, EntityId player, EntityId wolf)
    {
        var advice = WorldEncounterRetreatAdvisor.Advise(state, player);
        Assert.True(advice.IsFound);
        foreach (var toward in Enum.GetValues<EncounterRetreatDirection>())
        {
            var back = toward switch
            {
                EncounterRetreatDirection.North => EncounterRetreatDirection.South,
                EncounterRetreatDirection.South => EncounterRetreatDirection.North,
                EncounterRetreatDirection.East => EncounterRetreatDirection.West,
                _ => EncounterRetreatDirection.East
            };
            LocalSpatialSnapshot? next = WorldEncounterRetreats.WalkSample(state.Space, player, toward, out _);
            if (next is null || next.QueryInteraction(wolf, player, state.EncounterActions!.Abilities.Max(value => value.RangeSubunits))
                != SpatialInteraction.Reachable) continue;
            var route = new[] { toward, back, toward, back, toward, back }.Concat(advice.Route).ToArray();
            if (route.Length > EncounterRetreatRules.MaximumRouteSamples) continue;
            var timing = state.Pacing!.Encounter!;
            var intent = new QueueEncounterRetreatIntent(timing.EncounterId, timing.Revision,
                QueueEncounterRetreatIntent.Endpoint(state.Space.GetActor(player).Feet, route), route);
            if (WorldEncounterRetreats.EvaluateQueue(state, player, intent) == WorldActionRejection.None) return route;
        }
        throw new InvalidOperationException("No legal contact-preserving opening and escape route was found.");
    }

    private static string Row(StoredWorldCommit value) => $"{value.Sequence}:{value.Kind}:{value.CauseId}:{value.AdvancingSteps}:"
        + $"{Convert.ToHexString(value.AcceptedBatch)}:{Convert.ToHexString(value.Outcomes)}:{value.BeforeHash}:{value.AfterHash}";

    private static string CommandRow(StoredWorldCommandOutcome value) => $"{value.PrincipalId}:{value.CommandId}:{value.Sequence}:"
        + $"{Convert.ToHexString(value.Command)}:{Convert.ToHexString(value.Receipt)}";
}
