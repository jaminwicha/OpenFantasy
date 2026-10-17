using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Explicit fresh selection and structural admission of the paid world interpretation (profile 32): the
/// saved-proposal world plus a paid spot-exchange owner. It never upgrades a lived world and never
/// relabels a root of another interpretation. Structural admission authenticates no history.
/// </summary>
public static class WorldPaidExchangeAdmission
{
    public const ushort ProfileVersion = 32;

    public static WorldSnapshot CreateFresh(WorldSnapshot ordinaryCareGenesis)
    {
        // The original fresh admission checks every unearned counter and all complete-world owners.
        WorldSnapshot basis = WorldCoordinatedAdmission.CreateFresh(ordinaryCareGenesis);
        var admission = ContentHash.Compute(CanonicalEncodingV1.Frame("world.paid-exchange-admission.v1",
            basis.ContentHash.ToByteArray()));
        var world = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle,
            care: basis.Care, resourceHistory: WorldResourceHistory.CreateFresh(admission, basis.Progression!),
            coordination: new WorldCoordinatedContinuation(basis.Coordination!.Sources,
                consentedMaterial: new WorldConsentedMaterialHistory([]),
                proposals: new WorldMaterialProposalBook([], []), paidExchange: WorldPaidExchangeBook.Empty));
        _ = WorldSnapshotCodec.Encode(world);
        return world;
    }

    /// <summary>
    /// Shape and time of every quote and outcome against the admitted world. Raw rights stay inside actual
    /// issuance; fills name both participants under their admitted control bindings.
    /// </summary>
    internal static void Validate(WorldSnapshot world, WorldPaidExchangeBook book, ulong boundary)
    {
        foreach (WorldPaidQuote quote in book.Open)
        {
            ValidateQuote(world, quote);
        }
        foreach (WorldPaidQuoteOutcome outcome in book.Outcomes)
        {
            ValidateQuote(world, outcome.Quote);
            if (outcome.WorldSequence <= boundary || outcome.WorldSequence > world.Revision)
            {
                throw new ArgumentException("A quote outcome must follow its boundary within the current world.");
            }
            if (outcome.Kind == WorldPaidQuoteOutcomeKind.Expired)
            {
                // The earliest expiring commit follows the issuance by one commit for every remaining step.
                WorldPaidQuote expired = outcome.Quote;
                if (expired.ExpiresAtAdvancingStep > world.AdvancingSteps
                    || outcome.WorldSequence - expired.CreatedSequence < expired.ExpiresAtAdvancingStep - expired.CreatedAdvancingStep)
                {
                    throw new ArgumentException("An expired quote cannot precede its absolute expiry step.");
                }
            }
            if (outcome.Settlement is { } settled)
            {
                ValidateFill(world, outcome.Quote, settled);
                if (world.ProfileVersion is WorldDelegatedControlAdmission.ProfileVersion or WorldPartyWithdrawalAdmission.ProfileVersion)
                    RequireRecorded(world, outcome.Quote.Quote.CounterpartyActorId, settled.CounterpartyDelivery,
                        settled.ControlWitness, outcome.WorldSequence);
            }
            // Like fills, this compares recorded deliveries with the admitted bindings. Bindings are immutable
            // until an explicit session or roster transition exists; that transition must replace this check.
            if (outcome.Withdrawal is { } withdrawal)
            {
                if (world.ProfileVersion is WorldDelegatedControlAdmission.ProfileVersion or WorldPartyWithdrawalAdmission.ProfileVersion)
                    RequireRecorded(world, outcome.Quote.Quote.IssuerActorId, withdrawal.Delivery,
                        withdrawal.ControlWitness, outcome.WorldSequence);
                else if (withdrawal.ControlWitness is not null
                    || !world.Inventory.CanControl(withdrawal.Delivery, outcome.Quote.Quote.IssuerActorId))
                    throw new ArgumentException("A withdrawal requires the issuer under its admitted control binding.");
            }
        }
        if (world.Coordination?.Anchor is null)
        {
            // A fresh segment starts every issuer at market revision zero.
            ValidateIssuerRevisions(book, _ => 0, []);
            ValidateLifecycle(book, _ => 0, 0);
        }
    }

    /// <summary>Each issuer's market revision equals its boundary revision plus this segment's fills.</summary>
    internal static void ValidateIssuerRevisions(WorldPaidExchangeBook book, Func<EntityId, ulong> basis,
        IEnumerable<EntityId> basisIssuers)
    {
        var expected = basisIssuers.ToDictionary(issuer => issuer, basis);
        foreach (WorldPaidQuoteOutcome filled in book.Fills)
        {
            EntityId issuer = filled.Quote.Quote.IssuerActorId;
            if (filled.Quote.Quote.MarketRevision != expected.GetValueOrDefault(issuer))
            {
                throw new ArgumentException("A filled quote must use its issuer's market revision at its own causal boundary.");
            }
            expected[issuer] = checked(expected.GetValueOrDefault(issuer) + 1);
        }
        if (!expected.Where(value => value.Value != 0).OrderBy(value => value.Key)
                .SequenceEqual(book.IssuerRevisions))
        {
            throw new ArgumentException("Issuer market revisions must equal their boundary revisions plus recorded fills.");
        }
    }

    /// <summary>
    /// Replays one segment's quote lifecycle per issuer after <see cref="ValidateIssuerRevisions"/>: each quote
    /// issued after <paramref name="boundary"/> names its issuer's revision at issuance (a carried quote names the
    /// boundary revision); each fill supersedes exactly the issuer's quotes still open at that fill; and the
    /// issuer submits at most one command per step and none in a step that settles its quote. This proves a
    /// self-consistent causal segment from the root alone. It does not prove that any command was delivered:
    /// only the durable journal replay authenticates history.
    /// </summary>
    internal static void ValidateLifecycle(WorldPaidExchangeBook book, Func<EntityId, ulong> basis, ulong boundary)
    {
        var position = new Dictionary<EntityId, int>();
        for (int index = 0; index < book.Outcomes.Count; index++)
        {
            position.Add(book.Outcomes[index].Quote.QuoteId, index);
        }
        WorldPaidQuoteOutcome[] fills = book.Fills.ToArray();
        var acted = new HashSet<(EntityId Issuer, ulong Sequence)>();
        foreach (WorldPaidQuoteOutcome withdrawn in book.Outcomes.Where(value => value.Withdrawal is not null))
        {
            if (!acted.Add((withdrawn.Quote.Quote.IssuerActorId, withdrawn.WorldSequence)))
            {
                throw new ArgumentException("An issuer submits at most one command in one step.");
            }
        }
        foreach (WorldPaidQuote quote in book.Open.Concat(book.Outcomes.Select(value => value.Quote)))
        {
            EntityId issuer = quote.Quote.IssuerActorId;
            bool carried = quote.CreatedSequence <= boundary;
            if (!carried && !acted.Add((issuer, quote.CreatedSequence)))
            {
                throw new ArgumentException("An issuer submits at most one command in one step.");
            }
            WorldPaidQuoteOutcome[] issuerFills = fills.Where(value => value.Quote.Quote.IssuerActorId == issuer).ToArray();
            ulong expected = carried ? basis(issuer)
                : checked(basis(issuer) + (ulong)issuerFills.Count(value => value.WorldSequence < quote.CreatedSequence));
            if (quote.Quote.MarketRevision != expected)
            {
                throw new ArgumentException("A quote must name its issuer's market revision at its own issuance.");
            }
            int closedAt = position.TryGetValue(quote.QuoteId, out int index) ? index : int.MaxValue;
            WorldPaidQuoteOutcome? outcome = closedAt == int.MaxValue ? null : book.Outcomes[closedAt];
            foreach (WorldPaidQuoteOutcome fill in issuerFills.Where(value => value.Quote.QuoteId != quote.QuoteId
                && value.WorldSequence > quote.CreatedSequence && position[value.Quote.QuoteId] < closedAt))
            {
                // The quote was still open when this fill advanced its issuer's revision, so the fill closed it.
                if (outcome?.Supersession?.AcceptanceId != fill.Settlement!.Acceptance.AcceptanceId)
                {
                    throw new ArgumentException("A fill supersedes every other quote its issuer still had open.");
                }
            }
        }
        foreach (WorldPaidQuoteOutcome fill in fills)
        {
            if (acted.Contains((fill.Quote.Quote.IssuerActorId, fill.WorldSequence)))
            {
                throw new ArgumentException("An issuer submits no command in a step that settles its quote.");
            }
        }
    }

    /// <summary>
    /// The currency-only causes of this segment: every fill with its world position and the exact credits
    /// the shared spot arithmetic pays. Resource admission orders them with actor resource batches.
    /// </summary>
    internal static IEnumerable<WorldPaidCurrencyCause> CurrencyCauses(WorldSnapshot world) =>
        world.Coordination?.PaidExchange?.Fills.Select(value => new WorldPaidCurrencyCause(value.WorldSequence,
            value.Settlement!.ActionIndex, value.Quote.Quote, value.Settlement.Acceptance)) ?? [];

    /// <summary>Proposal and paid produced movements merged in authoritative world order for partition replay.</summary>
    internal static IEnumerable<ProducedCustodyMovement> OrderedProducedMovements(WorldMaterialProposalBook proposals,
        WorldPaidExchangeBook paid)
    {
        var merged = proposals.Outcomes
            .Where(value => value.Settlement is not null)
            .Select(value => (value.WorldSequence, value.Settlement!.AcceptanceActionIndex, Movement: value.Settlement!.ToProducedMovement()))
            .Where(value => value.Movement is not null)
            .Select(value => (Sequence: value.WorldSequence, Index: value.AcceptanceActionIndex, Movement: value.Movement!))
            .Concat(paid.ProducedMovements)
            .OrderBy(value => value.Sequence).ThenBy(value => value.Index).ToList();
        for (int index = 1; index < merged.Count; index++)
        {
            if (merged[index].Sequence == merged[index - 1].Sequence && merged[index].Index == merged[index - 1].Index)
            {
                throw new ArgumentException("One world command position cannot settle two custody movements.");
            }
        }
        return merged.Select(value => value.Movement);
    }

    private static void ValidateQuote(WorldSnapshot world, WorldPaidQuote proposal)
    {
        SpotQuote quote = proposal.Quote;
        if (world.ProfileVersion is WorldDelegatedControlAdmission.ProfileVersion or WorldPartyWithdrawalAdmission.ProfileVersion)
            RequireRecorded(world, quote.IssuerActorId, proposal.IssuerDelivery, proposal.ControlWitness, proposal.CreatedSequence);
        else if (proposal.ControlWitness is not null)
            throw new ArgumentException("Delegated quote provenance requires its explicit world profile.");
        // A commit publishes at most one advancing step, so no count can exceed its own revision.
        // An open quote's recorded delivery is history: it stays representable when it is stale or due.
        if (quote.WorldId != world.WorldId || quote.BranchId != world.BranchId
            || proposal.CreatedSequence > world.Revision || proposal.CreatedAdvancingStep > world.AdvancingSteps
            || proposal.CreatedAdvancingStep > proposal.CreatedSequence
            || !WorldPaidExchangeResolver.OwnsSettlementEnds(world.Inventory, world.Progression!, quote))
        {
            throw new ArgumentException("A paid quote requires this world's scope, time and admitted participants.");
        }
    }

    private static void RequireRecorded(WorldSnapshot world, EntityId actor, TrustedCommandDelivery delivery,
        WorldControlWitness? witness, ulong sequence)
    {
        if (!WorldActorCommandAuthority.ValidateRecordedEvidence(world.Inventory, actor, delivery, witness, WorldControlCapabilities.Trade)
            || witness is not null && witness.AcquiredSequence >= sequence)
            throw new ArgumentException("Paid history must retain the actual operator and its earlier assignment evidence.");
    }

    private static void ValidateFill(WorldSnapshot world, WorldPaidQuote proposal, WorldPaidSettlement settled)
    {
        SpotQuote quote = proposal.Quote;
        bool Controls(TrustedCommandDelivery delivery, EntityId actor, WorldControlWitness? witness) =>
            world.ProfileVersion is WorldDelegatedControlAdmission.ProfileVersion or WorldPartyWithdrawalAdmission.ProfileVersion
                ? WorldActorCommandAuthority.ValidateRecordedEvidence(world.Inventory, actor, delivery, witness, WorldControlCapabilities.Trade)
                : witness is null && world.Inventory.CanControl(delivery, actor);
        if (!Controls(proposal.IssuerDelivery, quote.IssuerActorId, proposal.ControlWitness)
            || !Controls(settled.CounterpartyDelivery, quote.CounterpartyActorId, settled.ControlWitness))
        {
            throw new ArgumentException("A fill requires both participants under their admitted control bindings.");
        }
        if (quote.LotKind == SpotLotKind.CountedSource)
        {
            var source = world.Inventory.Sources.SingleOrDefault(value => value.SourceId == settled.RightsOwnerId);
            if (source is null || source.Definition != quote.Definition || settled.MovedEnd > source.UnitCapacity)
            {
                throw new ArgumentException("A raw fill must name its actual admitted source and bounded rights.");
            }
            var node = world.Gathering!.Nodes.FirstOrDefault(value => value.SourceId == source.SourceId);
            if (node is not null && settled.MovedEnd > node.NextUnissuedUnit)
            {
                throw new ArgumentException("A raw fill cannot exceed actual source issuance.");
            }
        }
    }
}

/// <summary>
/// One currency-only cause: a fill's world position and exact terms. It owns no balance; replaying its
/// credits through the shared ledger transfer reconstructs the ledger and progression revisions it caused.
/// </summary>
internal sealed record WorldPaidCurrencyCause(ulong WorldSequence, uint CommandOrdinal, SpotQuote Quote,
    SpotAcceptance Acceptance);
