using System.Collections.Frozen;
using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Simulation;

internal sealed record LocalMaterialGiftSelection(IReadOnlyList<WorldActionCommand> Actions,
    IReadOnlySet<EntityId> ParticipatingActors);

/// <summary>Advises exact finite gifts and recipient choices under each configured NPC's own control.</summary>
internal sealed class LocalMaterialGiftController
{
    private readonly ReadOnlyCollection<LocalMaterialGiftPolicy> _policies;

    internal LocalMaterialGiftController(WorldSnapshot state, IEnumerable<LocalMaterialGiftPolicy> policies)
    {
        ArgumentNullException.ThrowIfNull(policies);
        var ordered = new SortedDictionary<EntityId, LocalMaterialGiftPolicy>();
        foreach (LocalMaterialGiftPolicy policy in policies)
        {
            ArgumentNullException.ThrowIfNull(policy);
            if (ordered.Count == WorldStepBatch.MaximumActorsPerBatch || !ordered.TryAdd(policy.ActorId, policy)
                || !LocalProfileBindings.AdmitsSavedProposals(state) || state.Care?.Ordinary is not { } ordinary
                || !state.Residents.Any(value => value.ActorId == policy.ActorId && value.ControllerKind == ResidentControllerKind.Host)
                || !state.Residents.Any(value => value.ActorId == policy.PartnerActorId)
                || LocalCareController.HostControl(state, policy.ActorId) is null
                || !ordinary.Definitions.Any(value => value.DefinitionId == policy.SelfUseDefinitionId
                    && value.Version == policy.SelfUseVersion && value.DeliveryMode == RestorativeDeliveryMode.SelfUse))
            {
                throw new AuthorityDispatchException("Gift preferences require unique admitted host actors, actual partners and exact self-use definitions.");
            }
        }
        _policies = Array.AsReadOnly(ordered.Values.ToArray());
    }

    /// <summary>A configured giver waits for an answer before volunteering direct treatment.</summary>
    internal IEnumerable<EntityId> DeferredAssistanceActors(WorldSnapshot state)
    {
        if (state.Coordination?.Proposals is not { } book)
        {
            yield break;
        }
        foreach (LocalMaterialGiftPolicy policy in _policies)
        {
            if (policy.OffersMedicine && book.Open.Any(value => value.Terms.SenderActorId == policy.ActorId
                && value.Terms.RecipientActorId == policy.PartnerActorId && !value.IsDue(state.AdvancingSteps)))
            {
                yield return policy.ActorId;
            }
        }
    }

    internal LocalMaterialGiftSelection SelectActions(WorldSnapshot state, Func<EntityId, bool> isOccupied,
        IReadOnlySet<EntityId> excludedActors, Func<WorldActionCommand, EntityId?> reserveValidSender)
    {
        List<WorldActionCommand> selected = [];
        HashSet<EntityId> participating = new(excludedActors);
        HashSet<EntityId> identities = [];
        if (!LocalProfileBindings.AdmitsSavedProposals(state) || state.HasReadingHold || state.PendingExcavation is not null
            || state.Care?.Pending is not null || state.EncounterLifecycle?.Phase != EncounterLifecyclePhase.Exploration)
        {
            return new(selected.AsReadOnly(), participating.ToFrozenSet());
        }
        foreach (LocalMaterialGiftPolicy policy in _policies)
        {
            if (participating.Contains(policy.ActorId)
                || !WorldActorAvailability.CanAct(state, policy.ActorId)
                || LocalCareController.HostControl(state, policy.ActorId) is not { } control)
            {
                continue;
            }
            var choice = Choose(state, policy);
            if (choice is null)
            {
                continue;
            }
            if (choice.Value.Intent is AcceptMaterialProposalIntent && participating.Contains(policy.PartnerActorId))
            {
                continue;
            }
            // An occupied preferred decision does not fall through to another offer, lot or partner.
            EntityId commandId = choice.Value.Identity;
            EntityId[] created = choice.Value.Intent is ProposeMaterialIntent
                ? [commandId, WorldMaterialProposal.DeriveIdentity(commandId), EntityId.DeriveSpawned(commandId, 0)]
                : [commandId];
            if (created.Distinct().Count() != created.Length || created.Any(identity => identities.Contains(identity)
                || state.IdentityIndex.Contains(identity) || isOccupied(identity)))
            {
                continue;
            }
            var command = new WorldActionCommand(commandId, control.PrincipalId, control.SessionId, policy.ActorId,
                state.WorldId, state.BranchId, state.Space.GetActor(policy.ActorId).Revision, choice.Value.Intent);
            participating.Add(policy.ActorId);
            if (reserveValidSender(command) is { } sender)
            {
                participating.Add(sender);
            }
            selected.Add(command);
            identities.UnionWith(created);
        }
        return new(selected.AsReadOnly(), participating.ToFrozenSet());
    }

    private static (EntityId Identity, MaterialProposalIntent Intent)? Choose(WorldSnapshot state, LocalMaterialGiftPolicy policy)
    {
        var book = state.Coordination!.Proposals!;
        var definition = state.Care!.Ordinary!.GetDefinition(policy.SelfUseDefinitionId);
        var remedy = state.Care.GetRemedy(definition.RemedyDefinitionId);
        WorldMaterialProposal? incoming = policy.AnswersOffers ? book.Open.FirstOrDefault(value => value.Terms.RecipientActorId == policy.ActorId
            && value.Terms.SenderActorId == policy.PartnerActorId && !value.IsDue(state.AdvancingSteps)) : null;
        if (incoming is not null)
        {
            ulong held = OwnedDoses(state, policy.ActorId, remedy.Item);
            bool wanted = incoming.Terms.Definition == remedy.Item && incoming.Terms.Quantity <= policy.DesiredDoses
                && held <= policy.DesiredDoses - incoming.Terms.Quantity
                && SupportsBody(state, definition, policy.ActorId);
            MaterialProposalIntent answer = wanted
                ? AcceptMaterialProposalIntent.Approving(incoming, state.Space.GetActor(policy.PartnerActorId).Revision)
                : new DeclineMaterialProposalIntent(incoming.ProposalId);
            return (LocalMaterialGiftIdentity.Answer(state, policy, incoming.ProposalId), answer);
        }
        if (!policy.OffersMedicine || book.Open.Any(value => value.Terms.SenderActorId == policy.ActorId
            && value.Terms.RecipientActorId == policy.PartnerActorId))
        {
            return null;
        }
        var patient = state.Progression!.GetActor(policy.PartnerActorId);
        if (!LocalRestorativeController.FitsFullDose(patient.CurrentHp, patient.Stats.MaxHp, remedy.RestoredHp)
            || !SupportsBody(state, definition, policy.PartnerActorId)
            || !state.Space.GetActor(policy.ActorId).Condition.SupportsFunction(BodyFunction.Manipulation)
            || state.Space.QueryInteraction(policy.ActorId, policy.PartnerActorId) != SpatialInteraction.Reachable
            || state.Space.QueryInteraction(policy.PartnerActorId, policy.ActorId) != SpatialInteraction.Reachable
            || OwnedDoses(state, policy.ActorId, remedy.Item) <= policy.RetainedDoses)
        {
            return null;
        }
        ConsentedMaterialTerms? terms = PreferredDose(state, policy, remedy.Item);
        if (terms is null || state.AdvancingSteps > ulong.MaxValue - 1 - policy.OfferLifetimeSteps)
        {
            return null;
        }
        return (LocalMaterialGiftIdentity.Offer(state, policy, terms),
            new ProposeMaterialIntent(terms, state.AdvancingSteps + 1 + policy.OfferLifetimeSteps));
    }

    private static bool SupportsBody(WorldSnapshot state, RestorativeUseDefinition definition, EntityId actor)
    {
        var presence = state.Space.GetActor(actor);
        return definition.SupportsForm(presence.Binding.DefinitionId, presence.Binding.DefinitionVersion)
            && presence.Condition.SupportsFunction(BodyFunction.Manipulation);
    }

    private static ulong OwnedDoses(WorldSnapshot state, EntityId actor, CountedItemDefinition item)
    {
        EntityId bag = state.GetResident(actor).InventoryCustodianId;
        return state.Inventory.Custody.Lots.Where(value => value.CustodianId == bag && value.Definition == item
                && value.ReservedBy is null && value.Protection == LotProtection.None).Select(value => value.Quantity)
            .Concat(state.Production!.Lots.Where(value => value.CustodianId == bag && value.Definition == item
                && value.ReservedBy is null && value.Protection == LotProtection.None)
                .Select(value => value.Quantity)).Aggregate(0UL, (total, quantity) => checked(total + quantity));
    }

    private static ConsentedMaterialTerms? PreferredDose(WorldSnapshot state, LocalMaterialGiftPolicy policy, CountedItemDefinition item)
    {
        EntityId source = state.GetResident(policy.ActorId).InventoryCustodianId;
        EntityId destination = state.GetResident(policy.PartnerActorId).InventoryCustodianId;
        var raw = state.Inventory.Custody.Lots.Where(value => value.CustodianId == source && value.Definition == item
            && value.Quantity > 0 && value.ReservedBy is null && value.Protection == LotProtection.None).OrderBy(value => value.LotId).FirstOrDefault();
        if (raw is not null)
        {
            return new(state.WorldId, state.BranchId, policy.ActorId, policy.PartnerActorId, ConsentedMaterialLotKind.CountedSource,
                raw.LotId, item, source, destination, 1, raw.Revision);
        }
        var produced = state.Production!.Lots.Where(value => value.CustodianId == source && value.Definition == item
            && value.Quantity > 0 && value.ReservedBy is null && value.Protection == LotProtection.None)
            .OrderBy(value => value.LotId).FirstOrDefault();
        return produced is null ? null : new(state.WorldId, state.BranchId, policy.ActorId, policy.PartnerActorId,
            ConsentedMaterialLotKind.Produced, produced.LotId, item, source, destination, 1, produced.Revision);
    }
}
