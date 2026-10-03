using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Explicit fresh selection and structural admission of the saved-proposal world interpretation.
/// It never upgrades a lived world and never relabels a root of another interpretation.
/// </summary>
public static class WorldMaterialProposalAdmission
{
    public static WorldSnapshot CreateFresh(WorldSnapshot ordinaryCareGenesis)
    {
        // The original fresh admission checks every unearned counter and all complete-world owners.
        WorldSnapshot basis = WorldCoordinatedAdmission.CreateFresh(ordinaryCareGenesis);
        var admission = ContentHash.Compute(CanonicalEncodingV1.Frame("world.material-proposal-admission.v1",
            basis.ContentHash.ToByteArray()));
        var world = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle,
            care: basis.Care, resourceHistory: WorldResourceHistory.CreateFresh(admission, basis.Progression!),
            coordination: new WorldCoordinatedContinuation(basis.Coordination!.Sources,
                consentedMaterial: new WorldConsentedMaterialHistory([]),
                proposals: new WorldMaterialProposalBook([], [])));
        _ = WorldSnapshotCodec.Encode(world);
        return world;
    }

    internal static void Validate(WorldSnapshot world, WorldConsentedMaterialHistory pairs,
        WorldMaterialProposalBook book, ulong boundary)
    {
        if (pairs.Transfers.Count != 0)
        {
            throw new ArgumentException("The saved-proposal interpretation admits no simultaneous pair evidence.");
        }
        foreach (WorldMaterialProposal proposal in book.Open)
        {
            ValidateProposal(world, proposal);
        }
        foreach (WorldMaterialProposalOutcome outcome in book.Outcomes)
        {
            ValidateProposal(world, outcome.Proposal);
            if (outcome.WorldSequence <= boundary || outcome.WorldSequence > world.Revision)
            {
                throw new ArgumentException("A proposal outcome must follow its boundary within the current world.");
            }
            ValidateTerminalOperator(world, outcome);
            if (outcome.Kind == WorldMaterialProposalOutcomeKind.Expired)
            {
                ValidateExpiry(world, outcome);
            }
            if (outcome.Settlement is { } settled && !IsSettledByAdmittedParticipants(world, outcome.Proposal, settled))
            {
                throw new ArgumentException("A settlement requires both participants under their admitted control bindings.");
            }
            if (outcome.Settlement is { } settlement
                && outcome.Proposal.Terms.LotKind == ConsentedMaterialLotKind.CountedSource)
            {
                var source = world.Inventory.Sources.SingleOrDefault(value => value.SourceId == settlement.RightsOwnerId);
                if (source is null || source.Definition != outcome.Proposal.Terms.Definition
                    || settlement.MovedEnd > source.UnitCapacity)
                {
                    throw new ArgumentException("A raw settlement must name its actual admitted source and bounded rights.");
                }
                var node = world.Gathering!.Nodes.FirstOrDefault(value => value.SourceId == source.SourceId);
                if (node is not null && settlement.MovedEnd > node.NextUnissuedUnit)
                {
                    throw new ArgumentException("A raw settlement cannot exceed actual source issuance.");
                }
            }
        }
    }

    private static void ValidateProposal(WorldSnapshot world, WorldMaterialProposal proposal)
    {
        ConsentedMaterialTerms terms = proposal.Terms;
        if (world.ProfileVersion == WorldDelegatedControlAdmission.ProfileVersion)
            RequireRecorded(world, terms.SenderActorId, proposal.SenderDelivery, proposal.ControlWitness, proposal.CreatedSequence);
        else if (proposal.ControlWitness is not null)
            throw new ArgumentException("Delegated material provenance requires its explicit world profile.");
        bool Owns(EntityId actorId, EntityId custodianId) => world.Inventory.Policies.Any(policy =>
            policy.CustodianId == custodianId && policy.OwnerActorId == actorId);
        // A commit publishes at most one advancing step, so no count can exceed its own revision.
        // An open offer's recorded grant is history: it stays representable when it is stale or due.
        if (terms.WorldId != world.WorldId || terms.BranchId != world.BranchId
            || proposal.CreatedSequence > world.Revision || proposal.CreatedAdvancingStep > world.AdvancingSteps
            || proposal.CreatedAdvancingStep > proposal.CreatedSequence
            || !Owns(terms.SenderActorId, terms.SourceCustodianId)
            || !Owns(terms.RecipientActorId, terms.DestinationCustodianId))
        {
            throw new ArgumentException("A saved proposal requires this world's scope, time and admitted participants.");
        }
    }

    private static void ValidateExpiry(WorldSnapshot world, WorldMaterialProposalOutcome outcome)
    {
        WorldMaterialProposal proposal = outcome.Proposal;
        // The earliest expiring commit follows the creation by one commit for every remaining step.
        ulong remainingSteps = proposal.ExpiresAtAdvancingStep - proposal.CreatedAdvancingStep;
        if (proposal.ExpiresAtAdvancingStep > world.AdvancingSteps
            || outcome.WorldSequence - proposal.CreatedSequence < remainingSteps)
        {
            throw new ArgumentException("An expired proposal cannot precede its absolute expiry step.");
        }
    }

    // The recorded grant and the recipient's delivery are compared with the admitted bindings, as
    // pair evidence is. Neither comparison authenticates a caller or renews the recorded delivery.
    private static bool IsSettledByAdmittedParticipants(WorldSnapshot world, WorldMaterialProposal proposal,
        WorldMaterialProposalSettlement settlement)
    {
        if (world.ProfileVersion != WorldDelegatedControlAdmission.ProfileVersion)
            return settlement.ControlWitness is null && new ConsentedMaterialApproval(proposal.Grant, proposal.SenderDelivery,
                settlement.Acceptance, settlement.RecipientDelivery).IsApprovedByBothParticipants(world.Inventory);
        return WorldActorCommandAuthority.ValidateRecordedEvidence(world.Inventory, proposal.Terms.SenderActorId,
                proposal.SenderDelivery, proposal.ControlWitness, WorldControlCapabilities.Inventory)
            && WorldActorCommandAuthority.ValidateRecordedEvidence(world.Inventory, proposal.Terms.RecipientActorId,
                settlement.RecipientDelivery, settlement.ControlWitness, WorldControlCapabilities.Inventory);
    }

    private static void ValidateTerminalOperator(WorldSnapshot world, WorldMaterialProposalOutcome outcome)
    {
        if (world.ProfileVersion != WorldDelegatedControlAdmission.ProfileVersion)
        {
            if (outcome.Termination is not null || outcome.Settlement?.ControlWitness is not null)
                throw new ArgumentException("Delegated terminal provenance requires its explicit world profile.");
            return;
        }
        if (outcome.Settlement is { } settled)
            RequireRecorded(world, outcome.Proposal.Terms.RecipientActorId, settled.RecipientDelivery,
                settled.ControlWitness, outcome.WorldSequence);
        if (outcome.Kind is WorldMaterialProposalOutcomeKind.Declined or WorldMaterialProposalOutcomeKind.Withdrawn)
        {
            var terminal = outcome.Termination ?? throw new ArgumentException("A permission-aware closure retains its actual operator.");
            EntityId actor = outcome.Kind == WorldMaterialProposalOutcomeKind.Withdrawn
                ? outcome.Proposal.Terms.SenderActorId : outcome.Proposal.Terms.RecipientActorId;
            RequireRecorded(world, actor, terminal.Delivery, terminal.ControlWitness, outcome.WorldSequence);
        }
    }

    private static void RequireRecorded(WorldSnapshot world, EntityId actor, TrustedCommandDelivery delivery,
        WorldControlWitness? witness, ulong sequence)
    {
        if (!WorldActorCommandAuthority.ValidateRecordedEvidence(world.Inventory, actor, delivery, witness, WorldControlCapabilities.Inventory)
            || witness is not null && witness.AcquiredSequence >= sequence)
            throw new ArgumentException("Material history must retain the actual operator and its earlier assignment evidence.");
    }
}
