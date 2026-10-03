using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Worlds;

public enum WorldMaterialProposalOutcomeKind { Accepted = 1, Declined = 2, Withdrawn = 3, Expired = 4 }

/// <summary>
/// One saved offer: the sender's submitted grant with its exact terms, the delivery recorded when
/// the creation was accepted, and the absolute expiry step. The recorded delivery is historical
/// evidence for this proposal only; it never authenticates a caller or controls anything.
/// A proposal owns no stock and reserves none.
/// </summary>
public sealed class WorldMaterialProposal
{
    public const int MaximumByteLength = 1_280;

    /// <summary>Development envelope: an offer outlives at most this many advancing steps.</summary>
    public const ulong MaximumLifetimeSteps = 65_536;

    // Distinct from the split child ordinal, so one creation command names two separate identities.
    private const ulong IdentityOrdinal = 0x4D50_0001;

    public WorldMaterialProposal(ConsentedMaterialCommand grant, TrustedCommandDelivery senderDelivery,
        ulong createdSequence, ulong createdAdvancingStep, ulong expiresAtAdvancingStep, WorldControlWitness? controlWitness = null)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(senderDelivery);
        if (createdSequence == 0 || createdAdvancingStep == 0
            || grant.PrincipalId != senderDelivery.PrincipalId || grant.OriginSessionId != senderDelivery.SessionId
            || expiresAtAdvancingStep <= createdAdvancingStep
            || expiresAtAdvancingStep - createdAdvancingStep > MaximumLifetimeSteps)
        {
            throw new ArgumentException("A saved proposal requires its creating delivery and a bounded later expiry step.");
        }
        ControlWitness = controlWitness;
        Grant = grant;
        SenderDelivery = senderDelivery;
        CreatedSequence = createdSequence;
        CreatedAdvancingStep = createdAdvancingStep;
        ExpiresAtAdvancingStep = expiresAtAdvancingStep;
        ProposalId = DeriveIdentity(grant.CommandId);
        GrantHash = grant.ComputeContentHash();
        ContentHash = ContentHash.Compute(Encode());
    }

    public static EntityId DeriveIdentity(EntityId creationCommandId) =>
        EntityId.DeriveSpawned(creationCommandId, IdentityOrdinal);

    public EntityId ProposalId { get; }
    public ConsentedMaterialCommand Grant { get; }
    public EntityId CreationCommandId => Grant.CommandId;
    public ConsentedMaterialTerms Terms => Grant.Terms;
    public CommandContentHash GrantHash { get; }
    public TrustedCommandDelivery SenderDelivery { get; }
    public WorldControlWitness? ControlWitness { get; }

    /// <summary>The world revision published by the step that accepted the creation.</summary>
    public ulong CreatedSequence { get; }

    /// <summary>The advancing-step count published by the creating step.</summary>
    public ulong CreatedAdvancingStep { get; }

    /// <summary>The first advancing-step count at which the proposal can no longer be accepted.</summary>
    public ulong ExpiresAtAdvancingStep { get; }

    /// <summary>The pinned identity of a partial amount's child lot, spawned from the creation command.</summary>
    public EntityId SplitChildId => EntityId.DeriveSpawned(Grant.CommandId, CustodyTransferPlanner.SplitChildOrdinal);
    public ContentHash ContentHash { get; }

    public bool IsDue(ulong advancingSteps) => advancingSteps >= ExpiresAtAdvancingStep;

    /// <summary>Only the sender and the recipient take part; taking part grants no control.</summary>
    public bool HasParticipant(EntityId actorId) =>
        Terms.SenderActorId == actorId || Terms.RecipientActorId == actorId;

    public byte[] Encode() => ControlWitness is null ? CanonicalEncodingV1.Frame("world.material-proposal.v1",
        ConsentedMaterialCodecV1.Encode(Grant), SenderDelivery.PrincipalId.ToByteArray(),
        SenderDelivery.SessionId.ToByteArray(), CanonicalEncodingV1.Unsigned64(CreatedSequence),
        CanonicalEncodingV1.Unsigned64(CreatedAdvancingStep), CanonicalEncodingV1.Unsigned64(ExpiresAtAdvancingStep))
        : CanonicalEncodingV1.Frame("world.material-proposal.v2", ConsentedMaterialCodecV1.Encode(Grant),
            SenderDelivery.PrincipalId.ToByteArray(), SenderDelivery.SessionId.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(CreatedSequence), CanonicalEncodingV1.Unsigned64(CreatedAdvancingStep),
            CanonicalEncodingV1.Unsigned64(ExpiresAtAdvancingStep), WorldControlWitnessCodecV1.Encode(ControlWitness));
}

/// <summary>
/// Evidence of the one movement an accepted proposal executed: the recipient's own claims and
/// delivery, the exact moved rights and the material owner hashes before and after. It contains no
/// sender command of the settling step, because the sender submitted none.
/// </summary>
public sealed class WorldMaterialProposalSettlement
{
    public WorldMaterialProposalSettlement(uint acceptanceActionIndex, ConsentedMaterialAcceptance acceptance,
        TrustedCommandDelivery recipientDelivery, EntityId movedLotId, EntityId rightsOwnerId, uint outputIndex,
        ulong movedStart, ulong movedEnd, ContentHash originalInventoryHash, ContentHash originalProductionHash,
        ContentHash candidateInventoryHash, ContentHash candidateProductionHash, WorldControlWitness? controlWitness = null)
    {
        ArgumentNullException.ThrowIfNull(acceptance);
        ArgumentNullException.ThrowIfNull(recipientDelivery);
        if (acceptanceActionIndex >= WorldStepBatch.MaximumActorsPerBatch || movedEnd <= movedStart
            || movedEnd - movedStart != acceptance.Terms.Quantity
            || acceptance.Terms.LotKind == ConsentedMaterialLotKind.CountedSource && outputIndex != 0
            || acceptance.PrincipalId != recipientDelivery.PrincipalId
            || acceptance.OriginSessionId != recipientDelivery.SessionId
            || movedLotId == acceptance.AcceptanceId || movedLotId == acceptance.CommandId)
        {
            throw new ArgumentException("A settlement requires the recipient's own delivery and the exact moved interval.");
        }
        ControlWitness = controlWitness;
        AcceptanceActionIndex = acceptanceActionIndex;
        Acceptance = acceptance;
        RecipientDelivery = recipientDelivery;
        MovedLotId = movedLotId;
        RightsOwnerId = rightsOwnerId;
        OutputIndex = outputIndex;
        MovedStart = movedStart;
        MovedEnd = movedEnd;
        OriginalInventoryHash = originalInventoryHash;
        OriginalProductionHash = originalProductionHash;
        CandidateInventoryHash = candidateInventoryHash;
        CandidateProductionHash = candidateProductionHash;
    }

    public uint AcceptanceActionIndex { get; }
    public ConsentedMaterialAcceptance Acceptance { get; }
    public TrustedCommandDelivery RecipientDelivery { get; }
    public WorldControlWitness? ControlWitness { get; }
    public EntityId MovedLotId { get; }

    /// <summary>The producing batch of produced stock, or the admitted source of raw stock.</summary>
    public EntityId RightsOwnerId { get; }
    public uint OutputIndex { get; }
    public ulong MovedStart { get; }
    public ulong MovedEnd { get; }
    public ContentHash OriginalInventoryHash { get; }
    public ContentHash OriginalProductionHash { get; }
    public ContentHash CandidateInventoryHash { get; }
    public ContentHash CandidateProductionHash { get; }

    internal WorldCustodyWitness Witness => new(Acceptance.Terms.LotKind == ConsentedMaterialLotKind.Produced,
        OriginalInventoryHash, OriginalProductionHash, CandidateInventoryHash, CandidateProductionHash);

    /// <summary>The material facts production replays; absent for raw stock, which custody owns.</summary>
    public ProducedCustodyMovement? ToProducedMovement()
    {
        ConsentedMaterialTerms terms = Acceptance.Terms;
        return terms.LotKind != ConsentedMaterialLotKind.Produced ? null
            : new ProducedCustodyMovement(terms.LotId, terms.ExpectedLotRevision, MovedLotId,
                terms.SourceCustodianId, terms.DestinationCustodianId, terms.Quantity,
                new ProductionOutputInterval(RightsOwnerId, OutputIndex, MovedStart, MovedEnd), terms.Definition);
    }

    public byte[] Encode()
    {
        List<ReadOnlyMemory<byte>> fields =
        [
            CanonicalEncodingV1.Unsigned64(AcceptanceActionIndex), ConsentedMaterialCodecV1.Encode(Acceptance),
            RecipientDelivery.PrincipalId.ToByteArray(), RecipientDelivery.SessionId.ToByteArray(),
            MovedLotId.ToByteArray(), RightsOwnerId.ToByteArray(), CanonicalEncodingV1.Unsigned64(OutputIndex),
            CanonicalEncodingV1.Unsigned64(MovedStart), CanonicalEncodingV1.Unsigned64(MovedEnd),
            OriginalInventoryHash.ToByteArray(), OriginalProductionHash.ToByteArray(),
            CandidateInventoryHash.ToByteArray(), CandidateProductionHash.ToByteArray()
        ];
        if (ControlWitness is not null) fields.Add(WorldControlWitnessCodecV1.Encode(ControlWitness));
        return CanonicalEncodingV1.Frame(ControlWitness is null
            ? "world.material-proposal-settlement.v1" : "world.material-proposal-settlement.v2", fields.ToArray());
    }
}

/// <summary>
/// The single terminal outcome of one proposal. It retains the complete proposal, so the closing
/// archive holds the whole lifecycle; the creation receipt stays a separate immutable fact.
/// </summary>
public sealed class WorldMaterialProposalOutcome
{
    public const int MaximumByteLength = 4_096;

    public WorldMaterialProposalOutcome(WorldMaterialProposal proposal, WorldMaterialProposalOutcomeKind kind,
        ulong worldSequence, EntityId? terminalCommandId, WorldMaterialProposalSettlement? settlement,
        WorldMaterialProposalTermination? termination = null)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        bool split = settlement is not null && settlement.MovedLotId != proposal.Terms.LotId;
        if (!Enum.IsDefined(kind) || worldSequence <= proposal.CreatedSequence
            || (kind == WorldMaterialProposalOutcomeKind.Expired) != (terminalCommandId is null)
            || (kind == WorldMaterialProposalOutcomeKind.Accepted) != (settlement is not null)
            || terminalCommandId == proposal.ProposalId || terminalCommandId == proposal.CreationCommandId
            || settlement is not null && (!settlement.Acceptance.Binds(proposal.Grant)
                || settlement.Acceptance.AcceptanceId != terminalCommandId
                || split && settlement.MovedLotId != proposal.SplitChildId
                || settlement.MovedLotId == proposal.ProposalId))
        {
            throw new ArgumentException("A proposal outcome requires one later terminal cause and its exact bound settlement.");
        }
        if (termination is not null && (kind is not WorldMaterialProposalOutcomeKind.Declined and not WorldMaterialProposalOutcomeKind.Withdrawn
            || termination.CommandId != terminalCommandId))
            throw new ArgumentException("Only a matching decline or withdrawal carries terminal operator evidence.");
        Termination = termination;
        Proposal = proposal;
        Kind = kind;
        WorldSequence = worldSequence;
        TerminalCommandId = terminalCommandId;
        Settlement = settlement;
        ContentHash = ContentHash.Compute(Encode());
    }

    public WorldMaterialProposal Proposal { get; }
    public WorldMaterialProposalOutcomeKind Kind { get; }

    /// <summary>The world revision published by the step that closed the proposal.</summary>
    public ulong WorldSequence { get; }

    /// <summary>The accepting, declining or withdrawing command; absent when world time expired the offer.</summary>
    public EntityId? TerminalCommandId { get; }
    public WorldMaterialProposalSettlement? Settlement { get; }
    public WorldMaterialProposalTermination? Termination { get; }
    public ContentHash ContentHash { get; }

    public byte[] Encode() => Termination is null
        ? CanonicalEncodingV1.Frame("world.material-proposal-outcome.v1", Proposal.Encode(),
            CanonicalEncodingV1.Unsigned64((ulong)Kind), CanonicalEncodingV1.Unsigned64(WorldSequence),
            TerminalCommandId?.ToByteArray() ?? Array.Empty<byte>(), Settlement?.Encode() ?? Array.Empty<byte>())
        : CanonicalEncodingV1.Frame("world.material-proposal-outcome.v2", Proposal.Encode(),
            CanonicalEncodingV1.Unsigned64((ulong)Kind), CanonicalEncodingV1.Unsigned64(WorldSequence),
            TerminalCommandId?.ToByteArray() ?? Array.Empty<byte>(), Settlement?.Encode() ?? Array.Empty<byte>(), Termination.Encode());
}

/// <summary>Owner hashes around one custody movement, for the reservoir's custody reconciliation.</summary>
internal readonly record struct WorldCustodyWitness(bool Produced, ContentHash OriginalInventoryHash,
    ContentHash OriginalProductionHash, ContentHash CandidateInventoryHash, ContentHash CandidateProductionHash);

/// <summary>The actual principal closing an actor's offer, independent of its original grant principal.</summary>
public sealed class WorldMaterialProposalTermination
{
    public const int MaximumByteLength = WorldControlWitnessCodecV1.MaximumByteLength + 128;
    public WorldMaterialProposalTermination(EntityId commandId, TrustedCommandDelivery delivery, WorldControlWitness? controlWitness = null)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        CommandId = commandId; Delivery = delivery; ControlWitness = controlWitness;
    }
    public EntityId CommandId { get; }
    public TrustedCommandDelivery Delivery { get; }
    public WorldControlWitness? ControlWitness { get; }
    public byte[] Encode() => CanonicalEncodingV1.Frame("world.material-proposal-termination.v1", CommandId.ToByteArray(),
        Delivery.PrincipalId.ToByteArray(), Delivery.SessionId.ToByteArray(),
        ControlWitness is null ? [] : WorldControlWitnessCodecV1.Encode(ControlWitness));
}
