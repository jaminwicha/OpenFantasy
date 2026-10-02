using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using OpenFantasy.Core;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;

namespace OpenFantasy.Hosting.Networking.ObservationViews;

/// <summary>Validates observation shape and cross-field consistency, without inferring any authority permission.</summary>
internal static class ObservationValidation
{
    private static readonly ConcurrentDictionary<Type, PropertyContract[]> Contracts = new();

    internal static void Validate(LocalWorldView view)
    {
        int values = 0;
        ValidateValue(view, null, 0, ref values);
        Require(view.Actors.Count is > 0 and <= LocalSpatialSnapshot.MaximumActors,
            "An observation needs its player within the admitted actor bound.");
        Unique(view.Actors.Select(actor => actor.ActorId), "actor");
        LocalActorView player = view.Actors.SingleOrDefault(actor => actor.ActorId == view.PlayerActorId)
            ?? throw new FormatException("The player must occur exactly once in its observation.");
        var actors = view.Actors.Select(actor => actor.ActorId).ToHashSet();
        Unique(view.EncounterActors.Select(actor => actor.ActorId), "encounter actor");
        Require(view.EncounterActors.All(actor => actors.Contains(actor.ActorId)), "Unknown encounter actor.");
        Unique(view.PlayerInventory.Lots.Select(lot => lot.LotId), "inventory lot");
        Unique(view.GatheringNodes.Select(node => node.NodeId), "gathering node");
        Unique(view.GatheringTools.Select(tool => tool.LotId), "gathering tool");
        Unique(view.Quests.Select(quest => quest.DefinitionId), "quest");
        Unique(view.Construction.Select(project => project.ProjectId), "construction project");
        Require(view.Actors.All(actor => !string.IsNullOrWhiteSpace(actor.DisplayName)), "Missing actor name.");
        if (view.Dialogue is { } dialogue)
        {
            Require(actors.Contains(dialogue.SpeakerActorId), "Unknown dialogue speaker.");
        }
        if (view.EncounterActions is { } actions)
        {
            Require(view.Encounter is { } encounter && actions.EncounterId == encounter.EncounterId
                && actions.TimingRevision == encounter.TimingRevision && actions.ActorRevision == player.PresenceRevision,
                "Encounter choices disagree with the observed boundary.");
            Require(actions.AbilityOffers.All(offer => actors.Contains(offer.TargetActorId)), "Unknown ability target.");
        }
        if (view.Retreat is { } retreat)
        {
            Require(retreat.Last is null || retreat.Last.ActorId == view.PlayerActorId, "Another actor's retreat record.");
            if (retreat.Offer is { } offer)
            {
                Require(view.Encounter is { } encounter && offer.EncounterId == encounter.EncounterId
                    && offer.TimingRevision == encounter.TimingRevision && offer.ActorRevision == player.PresenceRevision,
                    "Retreat choices disagree with the observed boundary.");
            }
        }
        if (view.EncounterOutcome is { } outcome)
        {
            Unique(outcome.Participants.Select(value => value.ActorId), "outcome participant");
            Require(outcome.Participants.All(value => actors.Contains(value.ActorId))
                && outcome.Participants.All(value => value.IsObserver == (value.ActorId == view.PlayerActorId)),
                "Outcome participants disagree with this observer.");
            Require(outcome.Acknowledgment is null || outcome.Acknowledgment.EncounterId == outcome.EncounterId,
                "Outcome acknowledgment names another encounter.");
        }
        ValidateExchange(view, actors);
        ValidateTerrain(view, player);
        if (view.Restorative is { } restorative)
        {
            foreach (var offer in restorative.SelfUseOffers)
            {
                if (offer.Intent is { } intent)
                {
                    Require(intent.RecipientActorId == view.PlayerActorId && intent.UseDefinitionId == offer.DefinitionId,
                        "A self-use selection must belong to this player and offer.");
                }
            }
        }
    }

    private static void ValidateExchange(LocalWorldView view, HashSet<EntityId> actors)
    {
        Unique(view.PaidQuotes.Select(quote => quote.QuoteId), "paid quote");
        foreach (PaidQuoteView quote in view.PaidQuotes)
        {
            Require(quote.IssuerActorId != quote.CounterpartyActorId
                && (quote.ViewerIsIssuer ? quote.IssuerActorId : quote.CounterpartyActorId) == view.PlayerActorId,
                "A quote must belong to this participant.");
            Require(quote.QuantityAllowance > 0 && quote.ExpiresAtStep > quote.IssuedAtStep,
                "Invalid fixed quote window or allowance.");
            Require(quote.ObservedIssuerRevision is null || !quote.ViewerIsIssuer && quote.Status == PaidQuoteStatus.Open,
                "Issuer observation is disclosed only to an open quote's counterparty.");
        }
        if (view.MaterialProposals is { } proposals)
        {
            Unique(proposals.Offers.Select(offer => offer.ProposalId), "material proposal");
            foreach (var offer in proposals.Offers)
            {
                Require(offer.Terms.WorldId == view.WorldId && offer.Terms.BranchId == view.BranchId
                    && (offer.ViewerIsSender ? offer.Terms.SenderActorId : offer.Terms.RecipientActorId) == view.PlayerActorId,
                    "A material proposal must belong to this world and participant.");
            }
            if (proposals.Preparation is { } preparation)
            {
                Require(preparation.CapturedWorldId == view.WorldId && preparation.CapturedBranchId == view.BranchId
                    && preparation.CapturedSenderActorId == view.PlayerActorId
                    && preparation.CapturedSourceCustodianId == view.PlayerInventory.CustodianId,
                    "Proposal preparation must use the player's captured scope and storage.");
                Require(preparation.ExpiresAtAdvancingStep > LocalMaterialProposalPreparation.OfferLifetimeSteps,
                    "Proposal preparation needs a valid advancing-step lifetime.");
                foreach (var destination in preparation.CapturedDestinations)
                {
                    Require(destination.Key != view.PlayerActorId && actors.Contains(destination.Key)
                        && destination.Value != preparation.CapturedSourceCustodianId,
                        "Invalid proposal recipient destination.");
                }
                Unique(preparation.Lots.Select(lot => lot.LotId), "proposal preparation lot");
                foreach (var lot in preparation.Lots)
                {
                    Require(lot.Quantity > 0 && view.PlayerInventory.Lots.Any(owned => owned.LotId == lot.LotId
                        && owned.DefinitionId == lot.Definition.DefinitionId && owned.Revision == lot.Revision
                        && owned.Quantity == lot.Quantity), "Proposal preparation must name an owned observed lot.");
                }
            }
        }
        if (view.Markets is { } markets)
        {
            Unique(markets.Stalls.Select(stall => stall.MarketId), "market stall");
            foreach (var stall in markets.Stalls)
            {
                Require(actors.Contains(stall.MerchantActorId) && stall.CapturedAdvancingStep == markets.AdvancingStep,
                    "A stall must share the market observation's advancing step and resident merchant.");
                Require(stall.MaximumUnitsPerOffer > 0 && stall.MaximumWindowSteps > 0, "Invalid stall limits.");
                Unique(stall.OwnLots.Select(lot => lot.LotId), "market lot");
            }
        }
    }

    private static void ValidateTerrain(LocalWorldView view, LocalActorView player)
    {
        Require(view.IsRegional || view.Regional is null, "Nonregional views cannot carry regional geometry.");
        if (view.Regional is { } regional)
        {
            Require(regional.PlayerActorId == view.PlayerActorId && regional.PlayerFeet == player.Feet
                && regional.PlayerFeet.Z % SubunitPosition.SubunitsPerCell == 0
                && regional.Regions.Count is > 0 and <= LocalSpatialSnapshot.MaximumRegions,
                "Regional geometry must belong to the player's observed standing floor.");
            Require(regional.Contains(player.Feet), "Regional geometry must include the player's column.");
            Unique(regional.Regions.Select(region => region.Bounds), "observed region");
            var columns = new HashSet<(long X, long Y)>();
            foreach (var region in regional.Regions)
            {
                long floorCell = regional.FloorFeetZ / SubunitPosition.SubunitsPerCell - 1;
                Require(region.Bounds.Minimum.Z <= floorCell && region.Bounds.Maximum.Z >= floorCell
                    && region.Ground.Count == checked(region.Bounds.Width * region.Bounds.Depth)
                    && region.OpeningDepth.Count == region.Ground.Count, "Invalid observed region dimensions or floor.");
                Require((Int128)region.Bounds.Minimum.X <= (Int128)player.Feet.Cell.X + LocalRegionObserver.ObservationRadiusCells
                    && (Int128)region.Bounds.Maximum.X >= (Int128)player.Feet.Cell.X - LocalRegionObserver.ObservationRadiusCells
                    && (Int128)region.Bounds.Minimum.Y <= (Int128)player.Feet.Cell.Y + LocalRegionObserver.ObservationRadiusCells
                    && (Int128)region.Bounds.Maximum.Y >= (Int128)player.Feet.Cell.Y - LocalRegionObserver.ObservationRadiusCells,
                    "Region lies outside the observer's bounded window.");
                for (int i = 0; i < region.Ground.Count; i++)
                {
                    Require(columns.Add((checked(region.Bounds.Minimum.X + i % region.Bounds.Width),
                        checked(region.Bounds.Minimum.Y + i / region.Bounds.Width))), "Observed regions overlap on this floor.");
                    Require(region.OpeningDepth[i] <= LocalRegionObserver.MaximumOpeningDepth
                        && (region.Ground[i] == LocalGroundKind.Open || region.OpeningDepth[i] == 0),
                        "Opening depths must describe bounded open columns.");
                }
            }
            Unique(regional.Water.Select(water => (water.CellX, water.CellY)), "observed wet column");
            Require(regional.Water.All(water => water.VolumeMicrolitres > 0
                && regional.RegionAt(water.CellX, water.CellY) is not null
                && (Int128)water.SurfaceSubunits >= (Int128)regional.FloorFeetZ
                    - (LocalRegionObserver.MaximumOpeningDepth + 1) * SubunitPosition.SubunitsPerCell
                && (Int128)water.SurfaceSubunits <= (Int128)regional.FloorFeetZ + SubunitPosition.SubunitsPerCell),
                "Water lies outside observed columns or vertical envelope.");
        }
        if (view.Worksite is { } worksite)
        {
            Require(worksite.Terrain.Count <= LocalSpatialSnapshot.MaximumRegions
                && worksite.Terrain.All(terrain => terrain.Occupancy.Count == terrain.Bounds.CellCount),
                "Worksite occupancy dimensions disagree.");
            Unique(worksite.ExcavationTargets.Select(target => target.SiteId), "excavation target");
            Require(worksite.ExcavationTargets.All(target => target.CompletedWork <= target.RequiredWork),
                "Excavation progress exceeds its requirement.");
        }
        if (view.Liquids is { } liquids)
        {
            Require(liquids.Terrain.Count <= LocalSpatialSnapshot.MaximumRegions
                && liquids.Terrain.All(terrain => terrain.Occupancy.Count == terrain.Bounds.CellCount),
                "Liquid terrain occupancy dimensions disagree.");
            Unique(liquids.Cells.Select(cell => cell.Coordinate), "liquid cell");
            Require(liquids.Cells.All(cell => cell.VolumeMicrolitres > 0
                && cell.VolumeMicrolitres <= LocalLiquidView.CellCapacityMicrolitres), "Invalid occupied liquid volume.");
        }
    }

    // Public immutable values are traversed only for nullability, enum and size validation. Wire shapes and
    // reconstruction remain explicit typed contracts; reflection neither reads private state nor sets a property.
    private static void ValidateValue(object? value, NullabilityInfo? expected, int depth, ref int count)
    {
        if (value is null)
        {
            Require(expected?.ReadState != NullabilityState.NotNull, "A required observation value is null.");
            return;
        }
        Require(depth <= PlayerViewCodec.MaximumDepth && ++count <= PlayerViewCodec.MaximumBodyBytes,
            "Observation values exceed their traversal bound.");
        Type type = value.GetType();
        if (type.IsEnum)
        {
            Require(Enum.IsDefined(type, value), "Unknown observation enum value.");
            return;
        }
        if (type.IsPrimitive || value is decimal) return;
        if (value is string text)
        {
            Require(text.Length <= PlayerViewCodec.MaximumStringBytes, "Observation text is too long.");
            return;
        }
        if (value is IEnumerable sequence)
        {
            int items = 0;
            NullabilityInfo? element = expected?.ElementType ?? expected?.GenericTypeArguments.FirstOrDefault();
            foreach (object? item in sequence)
            {
                Require(++items <= PlayerViewCodec.MaximumCollectionItems && item is not null,
                    "An observation collection is oversized or contains null.");
                ValidateValue(item, element, depth + 1, ref count);
            }
            return;
        }
        foreach (PropertyContract property in Contracts.GetOrAdd(type, Describe))
        {
            ValidateValue(property.Property.GetValue(value), property.Nullability, depth + 1, ref count);
        }
    }

    private static PropertyContract[] Describe(Type type)
    {
        var context = new NullabilityInfoContext();
        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetIndexParameters().Length == 0)
            .Select(property => new PropertyContract(property, context.Create(property))).ToArray();
    }

    private static void Unique<T>(IEnumerable<T> values, string description) where T : notnull
    {
        var seen = new HashSet<T>();
        Require(values.All(seen.Add), "Duplicate " + description + " identity.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new FormatException(message);
    }

    private sealed record PropertyContract(PropertyInfo Property, NullabilityInfo Nullability);
}
