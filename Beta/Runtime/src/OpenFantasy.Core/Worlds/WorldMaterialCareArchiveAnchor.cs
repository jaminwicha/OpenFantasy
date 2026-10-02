using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Bounded joint context of one coordinated closing boundary. It retains no closing root, parent
/// anchor or completed-use list. Its hash identifies the context; it grants and authenticates nothing.
/// </summary>
public sealed class WorldMaterialCareArchiveAnchor
{
    /// <param name="transition">
    /// Non-null only for the anchor of an explicit proposal transition. The closing root is then the
    /// exact lived coordinated head, and the anchor selects consent and an empty saved-proposal roster.
    /// </param>
    internal WorldMaterialCareArchiveAnchor(WorldMaterialCareArchiveReference reference, WorldSnapshot closing,
        ProductionContinuationContext production, CareContinuationContext care, GeographyProductionDependencies geography,
        WorldProposalTransition? transition = null)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(closing);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(care);
        ArgumentNullException.ThrowIfNull(geography);
        if (closing.Coordination is not { } coordination || reference.WorldId != closing.WorldId
            || reference.BranchId != closing.BranchId || reference.BoundarySequence != closing.Revision
            || closing.Progression is not { } progression || closing.Quests is not { } quests
            || closing.EncounterLifecycle is not { } lifecycle || !production.SupportsCompleteWorld
            || geography.Context.ContentHash != production.ContentHash
            || care.Frontier.WorldSequence > closing.Revision)
        {
            throw new ArgumentException("A coordinated anchor requires its complete scoped closing boundary.");
        }
        if (transition is not null && (coordination.ConsentedMaterial is not null || coordination.Proposals is not null
            || !production.SupportsConsentedWorld || transition.ExpectedRevision != closing.Revision
            || transition.WorldId != closing.WorldId || transition.BranchId != closing.BranchId))
        {
            throw new ArgumentException("A transition anchor closes one exact lived coordinated head into consent.");
        }
        Reference = reference;
        AdmissionVersion = closing.AdmissionVersion;
        Production = production;
        Care = care;
        Progression = progression;
        Quests = quests;
        Lifecycle = lifecycle;
        Geography = geography;
        SourceBaselineHash = coordination.Sources.ContentHash;
        // A transition adopts the saved-proposal capability with nothing to carry: the lived head had no offers.
        Proposals = coordination.Proposals is { } book ? WorldMaterialProposalContinuationContext.Capture(book)
            : transition is null ? null : WorldMaterialProposalContinuationContext.Capture(new WorldMaterialProposalBook([], []));
        TransitionHash = transition?.ContentHash;
        PaidExchange = coordination.PaidExchange is { } paid ? WorldPaidExchangeContinuationContext.Capture(paid) : null;
        Markets = coordination.Markets is { } markets
            ? WorldMarketContinuationContext.Capture(markets, coordination.PaidExchange!.ContinueAcrossCut()) : null;
        List<ReadOnlyMemory<byte>> fields =
        [
            reference.Encode(), AdmissionVersion.ToByteArray(), CanonicalEncodingV1.Unsigned64(LatestCareSequence),
            production.ContentHash.ToByteArray(), care.ContentHash.ToByteArray(), progression.ContentHash.ToByteArray(),
            quests.ContentHash.ToByteArray(), ContentHash.Compute(EncounterLifecycleCodecV1.Encode(lifecycle)).ToByteArray(),
            SourceBaselineHash.ToByteArray()
        ];
        if (Proposals is not null)
        {
            // Appended only with the capability, so every anchor without it keeps its exact bytes.
            fields.Add(Proposals.ContentHash.ToByteArray());
        }
        if (PaidExchange is not null)
        {
            // Appended only with the paid capability, so every other anchor keeps its exact bytes. A
            // transition never selects it, so the two optional fields cannot both appear.
            fields.Add(PaidExchange.ContentHash.ToByteArray());
        }
        if (Markets is not null)
        {
            // Appended only with the market capability, which always follows the paid field, so every
            // anchor without it keeps its exact bytes.
            fields.Add(Markets.ContentHash.ToByteArray());
        }
        if (TransitionHash is not null)
        {
            // Appended only by the transition anchor, so every other anchor keeps its exact bytes and
            // each successor's binding names the exact authenticated transition request.
            fields.Add(TransitionHash.ToByteArray());
        }
        ContentHash = ContentHash.Compute(CanonicalEncodingV1.Frame("world.material-care-anchor.v1", fields.ToArray()));
    }

    /// <summary>
    /// Digest of the explicit proposal transition that produced this anchor, or null for every ordinary
    /// cut. It is the bounded transition context: no source root or earlier anchor is retained.
    /// </summary>
    public ContentHash? TransitionHash { get; }

    public WorldMaterialCareArchiveReference Reference { get; }
    public ContentHash AdmissionVersion { get; }

    /// <summary>Revision of the complete closing world root.</summary>
    public ulong ClosingWorldSequence => Reference.BoundarySequence;

    /// <summary>Latest closed care operation. It can predate the closing world revision.</summary>
    public ulong LatestCareSequence => Care.Frontier.WorldSequence;

    public ProductionContinuationContext Production { get; }
    public CareContinuationContext Care { get; }

    /// <summary>Exact resource basis of the causal segment that follows this boundary.</summary>
    public ProgressionSnapshot Progression { get; }
    public ContentHash SourceBaselineHash { get; }

    /// <summary>Null when the closing world interprets no saved proposals; otherwise its exact open offers.</summary>
    public WorldMaterialProposalContinuationContext? Proposals { get; }

    /// <summary>Null when the closing world interprets no paid quotes; otherwise its open quotes and market revisions.</summary>
    public WorldPaidExchangeContinuationContext? PaidExchange { get; }

    /// <summary>Null when the closing world has no local markets; otherwise its bindings, open records and knowledge.</summary>
    public WorldMarketContinuationContext? Markets { get; }
    public ContentHash ContentHash { get; }
    internal QuestSnapshot Quests { get; }
    internal EncounterLifecycleSnapshot Lifecycle { get; }
    internal GeographyProductionDependencies Geography { get; }

    internal byte[] EncodeBinding() => CanonicalEncodingV1.Frame("world.material-care-binding.v1",
        Reference.Encode(), ContentHash.ToByteArray());

    internal void ValidateSuccessor(WorldSnapshot world)
    {
        if (world.WorldId != Reference.WorldId || world.BranchId != Reference.BranchId
            || world.AdmissionVersion != AdmissionVersion || world.Revision < Reference.BoundarySequence
            || Production.SupportsConsentedWorld != (world.Coordination?.ConsentedMaterial is not null)
            || (Proposals is not null) != (world.Coordination?.Proposals is not null)
            || (PaidExchange is not null) != (world.Coordination?.PaidExchange is not null)
            || (Markets is not null) != (world.Coordination?.Markets is not null)
            || world.Quests is not { } quests || world.EncounterLifecycle is not { } lifecycle)
        {
            throw new ArgumentException("A coordinated successor must retain its sealed world's scope and admission.");
        }
        Proposals?.ValidateSuccessor(world.Coordination!.Proposals!, ClosingWorldSequence);
        PaidExchange?.ValidateSuccessor(world.Coordination!.PaidExchange!, ClosingWorldSequence);
        Markets?.ValidateSuccessor(world.Coordination!.Markets!, ClosingWorldSequence);
        ValidateQuests(quests);
        ValidateCareChronology(world.Care!);
        if (lifecycle.WorldId != Lifecycle.WorldId || lifecycle.BranchId != Lifecycle.BranchId
            || lifecycle.Seed != Lifecycle.Seed || lifecycle.Revision < Lifecycle.Revision
            || lifecycle.EncounterCount < Lifecycle.EncounterCount
            || lifecycle.ExplorationSteps < Lifecycle.ExplorationSteps || lifecycle.TacticalTicks < Lifecycle.TacticalTicks
            || lifecycle.CatalogVersion != Lifecycle.CatalogVersion
            || !Lifecycle.Abilities.Select(value => (value.DefinitionId, value.Version))
                .SequenceEqual(lifecycle.Abilities.Select(value => (value.DefinitionId, value.Version)))
            || !Lifecycle.Scenarios.Select(value => (value.DefinitionId, value.Version))
                .SequenceEqual(lifecycle.Scenarios.Select(value => (value.DefinitionId, value.Version))))
        {
            throw new ArgumentException("A coordinated successor retains its encounter seed and monotonic lifecycle history.");
        }
    }

    private void ValidateCareChronology(CareSnapshot successor)
    {
        foreach (var acknowledgment in successor.Acknowledgments)
        {
            bool carried = Care.Pending is { } pending
                && CareCodecV1.EncodeAcknowledgment(pending).AsSpan()
                    .SequenceEqual(CareCodecV1.EncodeAcknowledgment(acknowledgment));
            if (!carried && acknowledgment.WorldSequence <= ClosingWorldSequence)
            {
                throw new ArgumentException("A new acknowledgment must follow the complete closing world boundary.");
            }
        }
        if (successor.Treatments.Any(value => value.WorldSequence <= ClosingWorldSequence)
            || successor.Ordinary!.Uses.Any(value => value.WorldSequence <= ClosingWorldSequence))
        {
            throw new ArgumentException("A new care effect must follow the complete closing world boundary.");
        }
        foreach (var permission in Care.Permissions!)
        {
            var current = successor.Ordinary.GetPermission(permission.PermissionId);
            if (permission.Revocation is null && current.Revocation is { } added
                && added.WorldSequence <= ClosingWorldSequence)
            {
                throw new ArgumentException("A new revocation must follow the complete closing world boundary.");
            }
        }
    }

    private void ValidateQuests(QuestSnapshot successor)
    {
        if (successor.Revision < Quests.Revision
            || !Quests.Definitions.Select(value => (value.DefinitionId, value.Version))
                .SequenceEqual(successor.Definitions.Select(value => (value.DefinitionId, value.Version))))
        {
            throw new ArgumentException("A coordinated successor cannot replace admitted quest definitions or regress quest history.");
        }
        foreach (var previous in Quests.Participations)
        {
            var current = successor.FindParticipation(previous.ActorId, previous.QuestId);
            if (current is null || current.AcceptanceCommandId != previous.AcceptanceCommandId
                || current.AcceptedAt != previous.AcceptedAt
                || previous.Status == ActorQuestStatus.Completed
                    && (current.CompletionCommandId != previous.CompletionCommandId || current.CompletedAt != previous.CompletedAt))
            {
                throw new ArgumentException("A coordinated successor preserves quest acceptance and every completed participation exactly.");
            }
        }
    }
}
