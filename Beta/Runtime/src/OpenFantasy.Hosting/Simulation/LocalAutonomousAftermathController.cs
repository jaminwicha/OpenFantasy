using System.Security.Cryptography;
using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Pre-agreed aid and nonterminal outcome acceptance for autonomous human characters.</summary>
internal sealed class LocalAutonomousAftermathController
{
    private static readonly byte[] CareDomain = Encoding.ASCII.GetBytes("local.autonomous-care-selection.v1");
    private static readonly byte[] OutcomeDomain = Encoding.ASCII.GetBytes("local.autonomous-outcome-selection.v1");

    internal WorldActionCommand? SelectChoice(WorldSnapshot state, Func<EntityId, bool> isOccupied)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(isOccupied);
        if (state.Controllers is null || state.HasReadingHold || state.PendingExcavation is not null
            || state.Care?.Pending is not null || WorldEncounterOutcomes.Determine(state) is not { } outcome) return null;

        EntityId[] actors = outcome.RequiredHumans.Where(actor =>
            state.Controllers.Get(actor).Mode == WorldControllerMode.Autonomous
            && !state.EncounterOutcome!.HasAcknowledged(actor)).Order().ToArray();

        // A prior agreement is permission, not treatment. Exact-defeat admission also checks the
        // currently capable neutral helper, reach and a real owned dose before requesting help.
        if (state.Care is { } care)
        {
            foreach (EntityId actor in actors)
            {
                if (state.EncounterActions!.LastResolution is not { TargetHpAfter: 0 } defeat
                    || defeat.TargetActorId != actor || state.Progression!.GetActor(actor).CurrentHp != 0) continue;
                var candidates = care.Agreements.Where(value => value.PatientActorId == actor)
                    .OrderBy(value => value.AgreementId).Select(agreement => (Agreement: agreement,
                        CommandId: SelectionId(CareDomain, state, [actor, agreement.AgreementId, defeat.DecisionCommandId]))).ToArray();
                // Check prior selections even when their helper has since become ineligible.
                // A refused/colliding selection requires attention, not a different agreement.
                if (candidates.Any(candidate => isOccupied(candidate.CommandId))) return null;
                foreach (var candidate in candidates)
                {
                    if (LocalCareController.HostControl(state, candidate.Agreement.HelperActorId) is null) continue;
                    var intent = new AcknowledgeCareIntent(candidate.Agreement.AgreementId, outcome.EncounterId,
                        state.EncounterLifecycle!.Revision, outcome.ActionHash);
                    if (WorldCareActions.EvaluateAcknowledgment(state, actor, intent) == WorldActionRejection.None)
                        return Command(state, actor, candidate.CommandId, intent);
                }
            }
        }

        foreach (EntityId actor in actors)
        {
            // Only the explicit recovery law preserves treatment eligibility after completion.
            // Legacy zero-HP outcomes still require a human's terminal disposition.
            if (state.Progression!.GetActor(actor).CurrentHp == 0 && state.Recovery is null) continue;
            EntityId commandId = SelectionId(OutcomeDomain, state, [actor, outcome.EncounterId], outcome.ActionHash);
            if (isOccupied(commandId)) return null;
            return Command(state, actor, commandId, new AcknowledgeEncounterOutcomeIntent(outcome.EncounterId, outcome.ActionHash));
        }
        return null;
    }

    private static WorldActionCommand Command(WorldSnapshot state, EntityId actor, EntityId commandId, WorldActionIntent intent)
    {
        var binding = state.Inventory.Controls.Single(control => control.ActorId == actor);
        return new(commandId, binding.PrincipalId, binding.SessionId, actor, state.WorldId, state.BranchId,
            state.Space.GetActor(actor).Revision, intent);
    }

    private static EntityId SelectionId(byte[] domain, WorldSnapshot state, EntityId[] subjects, ContentHash? actionHash = null)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(domain);
        Span<byte> identity = stackalloc byte[EntityId.ByteLength];
        foreach (EntityId value in new[] { state.WorldId, state.BranchId }.Concat(subjects))
        {
            value.WriteBytes(identity);
            hash.AppendData(identity);
        }
        if (actionHash is not null) hash.AppendData(actionHash.ToByteArray());
        return EntityId.FromBytes(hash.GetHashAndReset().AsSpan(0, EntityId.ByteLength));
    }
}
