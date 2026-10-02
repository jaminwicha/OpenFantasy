using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.Simulation.Retreat;
using OpenFantasy.Hosting.Tests.Simulation.RoadThreat;
using static OpenFantasy.Hosting.Tests.Worlds.EncounterOutcome.EncounterOutcomeJourneyTests;

namespace OpenFantasy.Hosting.Tests.Client.EncounterOutcome;

/// <summary>
/// The client's passive submission seam over an actual durable outcome world: it reports the exact selected offer object,
/// the command built from it (only the identity is new) with its canonical bytes, and the receipt the authority journals
/// for that command. A retry of the same offer reports the original command and receipt task; a distinct, stale
/// presentation is sent unchanged and keeps the host's ordinary rejection, with no retry or fresher substitution.
/// </summary>
public sealed class FarmSubmissionSeamTests
{
    [Fact]
    public async Task TheSeamReportsTheExactSelectionKeepsRetryIdentityAndRecordsTheStaleRejection()
    {
        string path = NewSave("submission-seam");
        WorldSnapshot threatened = RoadThreatWorldAdmission.CreateEmbedded("submission-seam", RoadThreatJourney.PlayerName);
        EntityId player = threatened.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        // Declared calibration, as the managed decline proof: the player starts at 1 HP beside the first wolf.
        WorldSnapshot genesis = WorldEncounterOutcomeAdmission.CreateCalibratedFresh(threatened,
            new EncounterOutcomeCalibration(new Dictionary<EntityId, int> { [player] = 1 }, []));
        try
        {
            await using var world = new RetreatJourneyWorld(path, genesis, reopen: false);
            List<FarmSubmission> observed = [];
            world.Client.Submitted += observed.Add;
            await world.ApproachFirstWolf();
            Assert.Equal(WorldActionRejection.None, (await world.EnterFirstWolf()).Rejection);
            await FightUntilOutcome(world, Wait);
            // Direct authority commands never pass the client seam.
            Assert.Empty(observed);

            LocalWorldView view = world.Session.ReadView();
            FarmActionOffer selected = world.Client.CreateOffers(view)[FarmSemanticAction.ReleaseEncounter];
            FarmActionOffer laterPresentation = world.Client.CreateOffers(view)[FarmSemanticAction.ReleaseEncounter];
            Assert.NotSame(selected, laterPresentation);
            Assert.Equal(view.EncounterOutcome!.Acknowledgment, selected.Intent);

            // A failing observer stays passive: the queued command keeps its receipt task and the failure stays visible.
            var observerFailure = new InvalidOperationException("observer failure");
            world.Client.Submitted += _ => throw observerFailure;
            Task<WorldActionReceipt> first = world.Client.SubmitOffer(selected);
            Assert.Same(observerFailure, world.Client.ObservationFailure);
            Task<WorldActionReceipt> retry = world.Client.SubmitOffer(selected);
            Assert.Same(first, retry);
            Assert.Equal(2, observed.Count);
            Assert.All(observed, value => Assert.Same(selected, value.Offer));
            Assert.Equal((false, true), (observed[0].IsRetry, observed[1].IsRetry));
            Assert.Same(observed[0].Command, observed[1].Command);
            Assert.Same(first, observed[0].Receipt);
            Assert.Same(first, observed[1].Receipt);

            // The command is the selected offer's exact scope and intent; only its identity is new.
            WorldActionCommand command = observed[0].Command;
            FarmActionScope scope = selected.Scope;
            Assert.Equal((scope.PrincipalId, scope.SessionId, scope.ActorId, scope.WorldId, scope.BranchId, scope.ExpectedActorRevision),
                (command.PrincipalId, command.OriginSessionId, command.ActorId, command.WorldId, command.BranchId, command.ExpectedActorRevision));
            Assert.Same(selected.Intent, command.Intent);
            Assert.Equal(WorldActionCodec.Encode(command), observed[0].CanonicalCommandBytes());
            // Each read is a copy: altering it cannot alter the captured evidence.
            byte[] altered = observed[0].CanonicalCommandBytes();
            altered[^1] ^= 0x01;
            Assert.Equal(WorldActionCodec.Encode(command), observed[1].CanonicalCommandBytes());
            Assert.Equal(WorldActionCodec.Encode(command), observed[0].CanonicalCommandBytes());

            await world.Tick();
            await SessionFixture.Until(() => first.IsCompleted);
            WorldActionReceipt applied = await first;
            Assert.Equal((CommandReceiptStatus.Applied, WorldActionRejection.None), (applied.Status, applied.Rejection));
            Assert.Equal((command.CommandId, command.ContentHash, command.PrincipalId), (applied.CommandId, applied.CommandHash, applied.PrincipalId));
            // The authority's journal holds exactly these command bytes (it refuses different content under this identity)
            // and exactly this receipt.
            Assert.Equal(WorldReceiptCodecV1.Encode(applied), WorldReceiptCodecV1.Encode(world.Authority.FindReceipt(command, world.Delivery)!));

            // The read outcome completes at the next ordinary cutoff; the earlier, distinct presentation is now stale.
            await world.Tick();
            Assert.Equal(EncounterLifecyclePhase.Exploration, world.State.EncounterLifecycle!.Phase);
            Task<WorldActionReceipt> stale = world.Client.SubmitOffer(laterPresentation);
            Assert.Equal(3, observed.Count);
            FarmSubmission staleSubmission = observed[2];
            Assert.Same(laterPresentation, staleSubmission.Offer);
            Assert.False(staleSubmission.IsRetry);
            Assert.NotEqual(command.CommandId, staleSubmission.CommandId);
            // Sent unchanged: the captured scope revision and intent, never a fresher offer in its place.
            Assert.Same(laterPresentation.Intent, staleSubmission.Command.Intent);
            Assert.Equal(laterPresentation.Scope.ExpectedActorRevision, staleSubmission.Command.ExpectedActorRevision);
            await world.Tick();
            await SessionFixture.Until(() => stale.IsCompleted);
            WorldActionReceipt refused = await stale;
            Assert.Equal((CommandReceiptStatus.Rejected, WorldActionRejection.Unavailable), (refused.Status, refused.Rejection));
            Assert.Equal(staleSubmission.CommandId, refused.CommandId);
            Assert.Equal(WorldReceiptCodecV1.Encode(refused),
                WorldReceiptCodecV1.Encode(world.Authority.FindReceipt(staleSubmission.Command, world.Delivery)!));
            // No retry or reselection followed the rejection, and the completed world is unchanged by it.
            Assert.Equal(3, observed.Count);
            Assert.Equal(1UL, world.State.EncounterOutcome!.FinalizedCount);
        }
        finally { Remove(path); }
    }
}
