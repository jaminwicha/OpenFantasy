using OpenFantasy.Core;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;

namespace OpenFantasy.Hosting.Networking.Selection;

internal static class SelectedObservationValidation
{
    internal static void Validate(PlayerSelectedObservation frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        Require(frame.StreamId != default && frame.Revision != 0 && frame.Observation is not null, "Missing selected observation identity.");
        var observed = frame.Observation;
        PlayerSelectionCodec.Validate(observed.Selection);
        var home = observed.HomeView;
        Require(home is not null && home.PlayerActorId == observed.Selection.HomeActorId, "Selected observation has a foreign home.");
        ObservationValidation.Validate(home);
        var control = observed.ControlStatus;
        Require(control is not null && Enum.IsDefined(control.State) && control.PortId != default
            && control.ActorId == observed.Selection.HomeActorId && control.Generation <= 1
            && (control.State != PlayerControlState.Pending || control.Generation == 0)
            && (control.State != PlayerControlState.Restored || control.Generation == 1)
            && (control.ObservationStreamId == default) == (control.ObservationRevision == 0)
            && (!observed.IsReady || control.State == PlayerControlState.Available), "Invalid selected home control status.");
        if (observed.Selection.ControlWitness is not { } witness)
        {
            Require(observed.ControlledView is null, "Home selection cannot carry another actor's private sections.");
            return;
        }
        var view = observed.ControlledView ?? throw new FormatException("Delegated selection requires its scoped observation.");
        Require(view.WorldId == home.WorldId && view.BranchId == home.BranchId && view.ActorId == observed.Selection.ActorId
            && view.Capabilities == witness.Capabilities && view.Calendar == home.Calendar
            && view.HasReadingHold == home.HasReadingHold && view.IsResolvingConsequences == home.IsResolvingConsequences,
            "Controlled observation disagrees with the captured world, actor or capability.");
        bool Has(WorldControlCapabilities scope) => (view.Capabilities & scope) == scope;
        Require(view.Capabilities != 0 && (view.Capabilities & ~WorldControlCapabilities.All) == 0, "Unknown controlled capabilities.");
        Require((view.Combat is not null) == Has(WorldControlCapabilities.Combat)
            && (view.Recovery is not null) == Has(WorldControlCapabilities.Recovery)
            && (view.CareDisposition is not null) == Has(WorldControlCapabilities.CareDisposition)
            && (view.Inventory is not null) == Has(WorldControlCapabilities.Inventory)
            && (view.Trade is not null) == Has(WorldControlCapabilities.Trade)
            && (view.Production is not null) == Has(WorldControlCapabilities.Production)
            && (view.Conversation is not null) == Has(WorldControlCapabilities.ConversationAndQuests)
            && (view.Retreat is null || Has(WorldControlCapabilities.Combat | WorldControlCapabilities.Traversal))
            && (view.MaterialProposals is null || Has(WorldControlCapabilities.Inventory)), "Controlled sections exceed or omit their declared capability.");
        // Each concrete section uses ordinary value validation; flags/witnesses have their own exact checks above.
        foreach (object? section in new object?[] { view.Actors, view.Regional, view.Combat, view.Retreat, view.Recovery,
            view.CareDisposition, view.Inventory, view.MaterialProposals, view.Trade, view.Production, view.Conversation })
            if (section is not null) ObservationValidation.ValidateValues(section);
        Unique(view.Actors.Select(actor => actor.ActorId));
        Require(view.Actors.Count is > 0 and <= LocalSpatialSnapshot.MaximumActors, "Invalid controlled actor collection.");
        var actor = view.Actors.SingleOrDefault(value => value.ActorId == view.ActorId)
            ?? throw new FormatException("Controlled actor is absent from its own observation.");
        foreach (var visible in view.Actors)
        {
            var shared = home.Actors.SingleOrDefault(value => value.ActorId == visible.ActorId);
            Require(shared is not null && shared.Feet == visible.Feet && shared.PresenceRevision == visible.PresenceRevision,
                "Home and selected observations do not share one public body boundary.");
        }
        if (view.Combat is { } combat)
        {
            Require(combat.Lifecycle?.ReleaseActionHash is null, "A controlled combat section contains terminal release history.");
            Unique(combat.Actors.Select(value => value.ActorId));
            Require(combat.Actors.All(value => view.Actors.Any(body => body.ActorId == value.ActorId)), "Unknown combat actor.");
            if (combat.Resources is { } combatResources)
                Require(combatResources.MaxHp > 0 && combatResources.Hp >= 0 && combatResources.Hp <= combatResources.MaxHp
                    && combatResources.MaxMp >= 0 && combatResources.Mp >= 0 && combatResources.Mp <= combatResources.MaxMp, "Invalid combat resources.");
            if (combat.Lifecycle is { } lifecycle)
            {
                Unique(lifecycle.Entries.Select(value => value.ScenarioId));
                Require(lifecycle.Entries.All(entry => view.Actors.Any(body => body.ActorId == entry.TargetActorId
                    && body.PresenceRevision == entry.TargetRevision)), "Encounter entry names another observed presence.");
            }
            if (combat.Actions is { } actions)
                Require(combat.Encounter is { } combatBoundary && actions.EncounterId == combatBoundary.EncounterId
                    && actions.TimingRevision == combatBoundary.TimingRevision && actions.ActorRevision == actor.PresenceRevision
                    && actions.AbilityOffers.All(offer => view.Actors.Any(body => body.ActorId == offer.TargetActorId)),
                    "Controlled combat choices disagree with their observed boundary.");
            if (combat.OutcomeAcknowledgment is { } acknowledgment)
                Require(combat.Encounter is { IsOutcome: true } outcome && acknowledgment.EncounterId == outcome.EncounterId,
                    "Outcome acknowledgment requires this current outcome.");
        }
        if (view.Retreat?.Offer is { } retreat)
            Require(view.Combat?.Encounter is { } retreatBoundary && retreat.EncounterId == retreatBoundary.EncounterId
                && retreat.TimingRevision == retreatBoundary.TimingRevision && retreat.ActorRevision == actor.PresenceRevision
                && retreat.Origin == actor.Feet, "Retreat offer belongs to another actor or boundary.");
        if (view.Recovery is { } recovery)
        {
            if (recovery.Health is { } health)
                Require(health.MaxHp > 0 && health.Hp >= 0 && health.Hp <= health.MaxHp
                    && (view.Combat?.Resources is not { } sharedCombatResources
                        || health.Hp == sharedCombatResources.Hp && health.MaxHp == sharedCombatResources.MaxHp),
                    "Inconsistent controlled health.");
            foreach (var offer in recovery.SelfUseOffers)
                Require(offer.Intent is null || offer.Intent.RecipientActorId == view.ActorId
                    && offer.Intent.UseDefinitionId == offer.DefinitionId, "Foreign restorative request.");
            ValidateCare(view, recovery.Care, true, home);
        }
        if (view.CareDisposition is { } disposition)
        {
            Require(disposition.PermissionOffers.All(value => value.IsActive
                && (value.Intent is null || value.Intent.PermissionId == value.PermissionId)), "Invalid current care permission.");
            ValidateCare(view, disposition.Care, false, home);
        }
        ValidateExchange(view);
        ValidatePreparation(view);
        ValidateRegion(view, actor);
    }

    private static void ValidateCare(LocalControlledActorView view, LocalCareView? care, bool recovery, LocalWorldView home)
    {
        if (care is null) return;
        Require(care.Closure is null && care.Status is not (LocalCareStatus.Treated or LocalCareStatus.Cancelled or LocalCareStatus.Declined),
            "Controlled care cannot contain closed history.");
        Require(recovery || care.Offers.Count == 0 && care.PendingAcknowledgmentId is not null, "Disposition exposes recovery offers.");
        if (care.Decision is not { } decision) return;
        Require(care.PendingAcknowledgmentId is not null && care.HelperActorId is not null
            && view.Actors.Any(value => value.ActorId == decision.PatientActorId)
            && (view.ActorId == decision.PatientActorId || view.ActorId == care.HelperActorId), "Foreign pending care request.");
        Require(recovery ? decision.Cancellation is null && decision.Decline is null : decision.Treatment is null,
            "Care choices cross capability scopes.");
        if (decision.Cancellation is { } cancel)
            Require(view.ActorId == decision.PatientActorId && cancel.AcknowledgmentId == care.PendingAcknowledgmentId
                && cancel.EncounterId == home.Encounter?.EncounterId, "Foreign care cancellation.");
        if (decision.Decline is { } decline)
            Require(view.ActorId == care.HelperActorId && decline.AcknowledgmentId == care.PendingAcknowledgmentId
                && decline.EncounterId == home.Encounter?.EncounterId, "Foreign care decline.");
        if (decision.Treatment is { } treatment)
            Require(view.ActorId == care.HelperActorId && treatment.AcknowledgmentId == care.PendingAcknowledgmentId,
                "Foreign care treatment.");
    }

    private static void ValidateExchange(LocalControlledActorView view)
    {
        if (view.Inventory is { } inventory) Unique(inventory.Lots.Select(lot => lot.LotId));
        if (view.MaterialProposals is { } proposals)
        {
            Unique(proposals.Offers.Select(value => value.ProposalId));
            foreach (var proposal in proposals.Offers)
            {
                Require(proposal.Status == MaterialProposalStatus.Open && proposal.ClosedAtSequence is null, "Closed proposal in controlled inventory.");
                _ = new PlayerQueryResult(view.WorldId, view.BranchId, view.ActorId, PlayerQueryKind.InspectMaterialProposal,
                    proposal.ProposalId, materialProposal: proposal);
            }
            if (proposals.Preparation is { } preparation)
            {
                Require(preparation.CapturedWorldId == view.WorldId && preparation.CapturedBranchId == view.BranchId
                    && preparation.CapturedSenderActorId == view.ActorId && preparation.CapturedSourceCustodianId == view.Inventory!.CustodianId,
                    "Foreign material preparation.");
                Require(preparation.CapturedDestinations.All(pair => pair.Key != view.ActorId
                    && view.Actors.Any(actor => actor.ActorId == pair.Key) && pair.Value != preparation.CapturedSourceCustodianId),
                    "Foreign proposal recipient.");
                Unique(preparation.Lots.Select(value => value.LotId));
                Require(preparation.Lots.All(lot => view.Inventory!.Lots.Any(owned => owned.LotId == lot.LotId
                    && owned.Revision == lot.Revision && owned.Quantity == lot.Quantity && owned.DefinitionId == lot.Definition.DefinitionId)),
                    "Proposal ingredients disagree with current inventory.");
            }
        }
        if (view.Trade is not { } trade) return;
        Require(trade.Wallet is null || !string.IsNullOrWhiteSpace(trade.Wallet.Unit), "Missing wallet denomination.");
        Unique(trade.Quotes.Select(quote => quote.QuoteId));
        foreach (var quote in trade.Quotes)
        {
            Require(quote.Status == PaidQuoteStatus.Open && quote.ClosedAtSequence is null, "Closed quote in controlled trade.");
            _ = new PlayerQueryResult(view.WorldId, view.BranchId, view.ActorId, PlayerQueryKind.InspectPaidQuote, quote.QuoteId, paidQuote: quote);
        }
        if (trade.Markets is not { } markets) return;
        Unique(markets.Stalls.Select(stall => stall.MarketId));
        foreach (var stall in markets.Stalls)
        {
            Require(stall.CapturedAdvancingStep == markets.AdvancingStep && stall.MaximumUnitsPerOffer > 0
                && stall.MaximumWindowSteps > 0 && view.Actors.Any(actor => actor.ActorId == stall.MerchantActorId)
                && (stall.OwnShowing is null || stall.OwnShowing.IsLive), "Invalid current market terms.");
            Unique(stall.OwnLots.Select(lot => lot.LotId));
        }
    }

    private static void ValidatePreparation(LocalControlledActorView view)
    {
        if (view.Production is { } production)
        {
            Unique(production.GatheringNodes.Select(value => value.NodeId));
            Unique(production.GatheringTools.Select(value => value.LotId));
            Unique(production.Construction.Select(value => value.ProjectId));
            Require(production.Construction.All(value => !value.IsCompleted), "Closed construction in controlled production.");
            Require((production.Crafting is null) == (production.CraftPreparation is null), "Missing crafting request capture.");
            if (production.CraftPreparation is { } craft)
            {
                Require(craft.ProductionRevision == production.Crafting!.Revision
                    && craft.Recipes.Count == production.Crafting.Recipes.Count
                    && craft.Recipes.Zip(production.Crafting.Recipes).All(pair => pair.First.RecipeId == pair.Second.RecipeId
                        && pair.First.Version == pair.Second.Version && pair.First.Key == pair.Second.Key
                        && pair.First.Terms.SequenceEqual(pair.Second.Terms)),
                    "Craft recipes disagree with their preparation.");
                Unique(craft.Recipes.Select(value => value.RecipeId)); Unique(craft.Ingredients.Select(value => value.LotId));
                Require(craft.Ingredients.All(lot => lot.AvailableForDelivery && craft.Recipes.Any(recipe => recipe.Terms.Any(term =>
                    term.Role == RecipeMaterialRole.Input && term.DefinitionId == lot.DefinitionId))), "Unrelated private craft ingredients.");
                if (view.Inventory is { } inventory)
                    Require(craft.CustodianId == inventory.CustodianId && craft.StorageRevision == inventory.Revision
                        && craft.Ingredients.All(lot => inventory.Lots.Contains(lot)), "Craft storage or ingredients disagree with inventory.");
            }
            Unique(production.ConstructionWork.Select(value => value.ProjectId));
            foreach (var work in production.ConstructionWork)
            {
                Require(production.Construction.Any(project => project.ProjectId == work.ProjectId && project.BuilderActorId == view.ActorId)
                    && work.Choices.All(choice => choice.ProjectId == work.ProjectId), "Foreign construction work capture.");
                Unique(work.Choices.Select(choice => choice.ToolLotId));
            }
            if (production.Worksite is { } worksite)
                Require(worksite.Terrain.All(region => region.Occupancy.Count == region.Bounds.CellCount)
                    && worksite.ExcavationTargets.All(target => target.CompletedWork <= target.RequiredWork), "Invalid worksite geometry or progress.");
        }
        if (view.Conversation is not { } conversation) return;
        Unique(conversation.Quests.Select(value => value.DefinitionId));
        Require(conversation.Quests.All(value => value.Status != ActorQuestStatus.Completed
            && view.Actors.Any(actor => actor.ActorId == value.GiverActorId)), "Foreign or completed quest.");
        if (conversation.Dialogue is { } dialogue)
            Require(view.Actors.Any(actor => actor.ActorId == dialogue.SpeakerActorId), "Unknown dialogue speaker.");
        Require(conversation.Requests is not null, "Missing quest preparation.");
        if (conversation.Requests is { } requests)
        {
            Require(requests.Quests.SequenceEqual(conversation.Quests), "Quest preparation differs from presented quests.");
            Unique(requests.DeliveryLots.Select(value => value.LotId));
            Require(requests.DeliveryLots.All(lot => lot.AvailableForDelivery && lot.Provenance == IngredientLotKind.Source
                && requests.Quests.Any(quest => quest.ItemDefinitionId == lot.DefinitionId)), "Unrelated private quest delivery lot.");
            Require(requests.CapturedGiverRevisions.All(pair => requests.Quests.Any(quest => quest.GiverActorId == pair.Key)
                && view.Actors.Any(actor => actor.ActorId == pair.Key && actor.PresenceRevision == pair.Value)), "Foreign quest giver pin.");
            Require(requests.Quests.All(quest => requests.CapturedGiverRevisions.ContainsKey(quest.GiverActorId)), "Missing quest giver pin.");
            if (view.Inventory is { } inventory)
                Require(requests.InventoryRevision == inventory.Revision && requests.DeliveryLots.All(lot => inventory.Lots.Contains(lot)),
                    "Quest source lots disagree with inventory.");
        }
    }

    private static void ValidateRegion(LocalControlledActorView view, LocalActorView actor)
    {
        Require(view.IsRegional || view.Regional is null, "Unexpected controlled regional geometry.");
        if (view.Regional is not { } regional) return;
        Require(regional.PlayerActorId == view.ActorId && regional.PlayerFeet == actor.Feet && regional.Contains(actor.Feet)
            && regional.PlayerFeet.Z % SubunitPosition.SubunitsPerCell == 0
            && regional.Regions.Count is > 0 and <= LocalSpatialSnapshot.MaximumRegions, "Foreign controlled geometry.");
        Unique(regional.Regions.Select(region => region.Bounds));
        var columns = new HashSet<(long, long)>();
        foreach (var region in regional.Regions)
        {
            long floor = regional.FloorFeetZ / SubunitPosition.SubunitsPerCell - 1;
            Require(region.Bounds.Minimum.Z <= floor && region.Bounds.Maximum.Z >= floor
                && region.Ground.Count == checked(region.Bounds.Width * region.Bounds.Depth)
                && region.OpeningDepth.Count == region.Ground.Count, "Invalid controlled region dimensions.");
            Require((Int128)region.Bounds.Minimum.X <= (Int128)actor.Feet.Cell.X + LocalRegionObserver.ObservationRadiusCells
                && (Int128)region.Bounds.Maximum.X >= (Int128)actor.Feet.Cell.X - LocalRegionObserver.ObservationRadiusCells
                && (Int128)region.Bounds.Minimum.Y <= (Int128)actor.Feet.Cell.Y + LocalRegionObserver.ObservationRadiusCells
                && (Int128)region.Bounds.Maximum.Y >= (Int128)actor.Feet.Cell.Y - LocalRegionObserver.ObservationRadiusCells,
                "Controlled region lies outside the observation window.");
            for (int index = 0; index < region.Ground.Count; index++)
                Require(columns.Add((region.Bounds.Minimum.X + index % region.Bounds.Width, region.Bounds.Minimum.Y + index / region.Bounds.Width))
                    && region.OpeningDepth[index] <= LocalRegionObserver.MaximumOpeningDepth
                    && (region.Ground[index] == LocalGroundKind.Open || region.OpeningDepth[index] == 0), "Invalid controlled region overlap or depth.");
        }
        Unique(regional.Water.Select(value => (value.CellX, value.CellY)));
        Require(regional.Water.All(value => value.VolumeMicrolitres > 0 && regional.RegionAt(value.CellX, value.CellY) is not null
            && (Int128)value.SurfaceSubunits >= (Int128)regional.FloorFeetZ
                - (LocalRegionObserver.MaximumOpeningDepth + 1) * SubunitPosition.SubunitsPerCell
            && (Int128)value.SurfaceSubunits <= (Int128)regional.FloorFeetZ + SubunitPosition.SubunitsPerCell),
            "Water lies outside controlled observation.");
    }
    private static void Unique<T>(IEnumerable<T> values) where T : notnull
    { var seen = new HashSet<T>(); Require(values.All(seen.Add), "Duplicate selected observation identity."); }
    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string message)
    { if (!condition) throw new FormatException(message); }
}
