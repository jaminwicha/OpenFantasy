using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed partial class ConsentedWorldPublicationTests
{
    private const string LifecycleBoundary = "This encounter phase requires its lifecycle boundary.";
    private const string ActionBoundary = "This encounter requires an available explicit action boundary.";
    private const string CareBoundary = "An acknowledged care obligation must resolve before another world activity.";

    [Fact]
    public void Confrontation_and_staging_keep_exclusive_ownership_of_the_next_boundary()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var journey = fixture.Journey;
        var session = new CareWorldSession(WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis));
        journey.Approach(session);
        WorldSnapshot state = session.ReadStateForHost();
        var scenario = journey.First;
        var enter = journey.Command(state, journey.Player, new EnterEncounterIntent(scenario.DefinitionId,
            EncounterEpisodeIdentity.Derive(state.EncounterLifecycle!, scenario.DefinitionId),
            state.EncounterLifecycle!.EncounterCount, state.Space.GetActor(scenario.TargetActorId).Revision));
        state = CareRewardJourney.Commit(session, journey.Batch(state, WorldBatchKind.EncounterLifecycle, [enter])).Candidate;
        Assert.Equal(EncounterLifecyclePhase.Confrontation, state.EncounterLifecycle!.Phase);
        AssertPairHasNoProcessor(session, journey, LifecycleBoundary);
        state = CareRewardJourney.Commit(session, journey.Batch(state, WorldBatchKind.EncounterLifecycle)).Candidate;
        Assert.Equal(EncounterLifecyclePhase.Staging, state.EncounterLifecycle!.Phase);
        AssertPairHasNoProcessor(session, journey, LifecycleBoundary);
    }

    [Fact]
    public void Paired_intents_follow_the_actual_encounter_care_and_recovery_owners()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var journey = fixture.Journey;
        var session = new CareWorldSession(WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis));
        journey.Approach(session);
        // Positive control before any encounter: the same participants, reach and stock apply.
        WorldSnapshot calm = session.ReadStateForHost();
        var opening = Pair(calm, journey, RawTerms(calm, journey.Helper, journey.Player));
        CareRewardJourney.Commit(session, journey.Batch(calm, WorldBatchKind.AdvancingStep, [opening.Offer, opening.Acceptance]));

        bool observedCombat = false;
        WorldSnapshot defeated = journey.PrepareDefeat(session, combat =>
        {
            Assert.NotNull(combat.EncounterActions);
            AssertPairHasNoProcessor(session, journey, ActionBoundary);
            observedCombat = true;
        });
        Assert.True(observedCombat);
        Assert.Equal(EncounterLifecyclePhase.Outcome, defeated.EncounterLifecycle!.Phase);
        AssertPairHasNoProcessor(session, journey, LifecycleBoundary);

        WorldSnapshot pending = CareRewardJourney.Commit(session, journey.Batch(defeated, WorldBatchKind.Care,
            [journey.Acknowledge(defeated)])).Candidate;
        Assert.NotNull(pending.Care!.Pending);
        AssertPairHasNoProcessor(session, journey, CareBoundary);
        WorldSnapshot treated = CareRewardJourney.Commit(session, journey.Batch(pending, WorldBatchKind.Care,
            [journey.Treat(pending)])).Candidate;
        Assert.NotNull(treated.EncounterActions);
        AssertPairHasNoProcessor(session, journey, ActionBoundary);

        WorldSnapshot released = journey.WinAndRelease(session);
        Assert.False(WorldActorAvailability.CanAct(released, journey.Player)
            && WorldActorAvailability.CanAct(released, journey.Helper));
        // Exploration owns the step again: it advances time while unpaid encounter debt refuses the pair.
        var indebted = Pair(released, journey, RawTerms(released, journey.Helper, journey.Player));
        WorldStepDecision refused = session.Commit(journey.Batch(released, WorldBatchKind.AdvancingStep,
            [indebted.Offer, indebted.Acceptance]));
        Assert.Equal(2, refused.Receipts.Count);
        Assert.All(refused.Receipts, receipt => Assert.Equal(WorldActionRejection.Unavailable, receipt.Rejection));
        Assert.Equal(released.Inventory.ContentHash, refused.Candidate.Inventory.ContentHash);
        Assert.Equal(released.Coordination!.ConsentedMaterial!.ContentHash,
            refused.Candidate.Coordination!.ConsentedMaterial!.ContentHash);
        Assert.Equal(released.AdvancingSteps + 1, refused.Candidate.AdvancingSteps);

        WorldSnapshot state = refused.Candidate;
        for (int step = 0; !WorldActorAvailability.CanAct(state, journey.Player)
            || !WorldActorAvailability.CanAct(state, journey.Helper); step++)
        {
            Assert.True(step < 32);
            state = CareRewardJourney.Commit(session, journey.Batch(state, WorldBatchKind.AdvancingStep)).Candidate;
        }
        var recovered = Pair(state, journey, RawTerms(state, journey.Helper, journey.Player));
        var applied = CareRewardJourney.Commit(session, journey.Batch(state, WorldBatchKind.AdvancingStep,
            [recovered.Offer, recovered.Acceptance]));
        Assert.Equal(2, applied.Candidate.Coordination!.ConsentedMaterial!.Transfers.Count);
        Assert.Equal(recovered.Offer.CommandId, applied.Candidate.Coordination.ConsentedMaterial.Transfers[^1].CommandId);
    }

    /// <summary>No encounter, lifecycle or care boundary carries the pair, and the advancing step is not selected.</summary>
    private static void AssertPairHasNoProcessor(CareWorldSession session, CareRewardJourney journey, string owner)
    {
        WorldSnapshot state = session.ReadStateForHost();
        var pair = Pair(state, journey, RawTerms(state, journey.Helper, journey.Player));
        foreach (WorldBatchKind kind in new[] { WorldBatchKind.EncounterDecision, WorldBatchKind.EncounterResolution,
            WorldBatchKind.TacticalAdvance, WorldBatchKind.EncounterLifecycle, WorldBatchKind.Care })
        {
            Assert.Equal("Paired material transfer requires its explicit complete-world profile.",
                Assert.Throws<ArgumentException>(() => journey.Batch(state, kind, [pair.Offer, pair.Acceptance])).Message);
        }
        var refusal = Assert.Throws<InvalidOperationException>(() => session.Commit(journey.Batch(state,
            WorldBatchKind.AdvancingStep, [pair.Offer, pair.Acceptance])));
        Assert.Equal(owner, refusal.Message);
        Assert.Equal(state.ContentHash, session.ReadStateForHost().ContentHash);
        Assert.DoesNotContain(pair.Offer.CommandId, session.OccupiedIdentities);
        Assert.DoesNotContain(pair.Acceptance.CommandId, session.OccupiedIdentities);
    }
}
