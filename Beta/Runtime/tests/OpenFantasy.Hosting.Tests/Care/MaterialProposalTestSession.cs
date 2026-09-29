using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>Actual fresh admission and SQLite publication for the remaining proposal contrasts.</summary>
internal sealed class MaterialProposalTestSession : IDisposable
{
    private readonly StorageFiles _files = new();
    private readonly byte[] _genesis;

    internal MaterialProposalTestSession(WorldSnapshot ordinary)
    {
        Assert.Equal((0UL, 0UL), (ordinary.Revision, ordinary.AdvancingSteps));
        WorldSnapshot genesis = WorldMaterialProposalAdmission.CreateFresh(ordinary);
        _genesis = WorldSnapshotCodec.Encode(genesis);
        Actions = new CareRewardJourney(false, genesis);
        Authority = SqliteWorldAuthority.CreateNew(_files.Path, genesis);
        Service = CreateService();
    }

    internal CareRewardJourney Actions { get; }
    internal SqliteWorldAuthority Authority { get; private set; }
    internal MaterialProposalService Service { get; private set; }
    internal WorldSnapshot State => Authority.ReadStateForHost();
    internal WorldMaterialProposalBook Book => State.Coordination!.Proposals!;
    internal TrustedCommandDelivery Delivery(EntityId actor)
    {
        ActorControlBinding binding = State.Inventory.Controls.Single(value => value.ActorId == actor);
        return new(binding.PrincipalId, binding.SessionId);
    }

    internal ConsentedMaterialTerms Terms(EntityId sender, EntityId recipient, EntityId? lotId = null)
    {
        WorldSnapshot state = State;
        EntityId storage = state.GetResident(sender).InventoryCustodianId;
        CountedSourceLot lot = state.Inventory.Custody.Lots.First(value => value.CustodianId == storage
            && value.ReservedBy is null && value.Protection == LotProtection.None
            && (lotId is null || value.LotId == lotId));
        return new(state.WorldId, state.BranchId, sender, recipient, ConsentedMaterialLotKind.CountedSource,
            lot.LotId, lot.Definition, storage, state.GetResident(recipient).InventoryCustodianId, 1, lot.Revision);
    }

    internal WorldMaterialProposal Save(ConsentedMaterialTerms terms, ulong lifetime = 4096)
    {
        WorldActionCommand command = Actions.Command(State, terms.SenderActorId,
            new ProposeMaterialIntent(terms, State.AdvancingSteps + 1 + lifetime));
        Assert.Equal(WorldActionRejection.None, Service.Submit(command, Delivery(terms.SenderActorId)).Rejection);
        return Book.FindOpen(WorldMaterialProposal.DeriveIdentity(command.CommandId))!;
    }

    internal WorldActionCommand Acceptance(WorldMaterialProposal proposal) =>
        Actions.Command(State, proposal.Terms.RecipientActorId,
            Service.Find(proposal.ProposalId, proposal.Terms.RecipientActorId,
                Delivery(proposal.Terms.RecipientActorId))!.PrepareAcceptance());

    internal WorldActionReceipt Accept(WorldMaterialProposal proposal)
    {
        WorldActionCommand command = Acceptance(proposal);
        return Service.Submit(command, Delivery(command.ActorId));
    }

    internal void Step() => Authority.Commit(Actions.Batch(State, WorldBatchKind.AdvancingStep));

    internal void Reopen()
    {
        Authority.Dispose();
        Authority = SqliteWorldAuthority.OpenExisting(_files.Path, _genesis);
        Service = CreateService();
    }

    private MaterialProposalService CreateService() => new(Authority,
        () => Actions.Batch(State, WorldBatchKind.AdvancingStep).CauseId);

    public void Dispose()
    {
        Authority.Dispose();
        _files.Dispose();
    }

    /// <summary>
    /// Authors shared control before geography, encounters and care bind a fresh farm. It changes
    /// no lived actor, stock, body or rule, and the changed admission is committed as genesis.
    /// </summary>
    internal static WorldSnapshot CreateSharedControlGenesis()
    {
        FarmContentFiles files = FarmContentFiles.Embedded();
        CareFarmSources supplies = CareFarmSources.Load(files);
        WorldSnapshot journey = EncounterJourneyAdmission.Create(files, null,
            EncounterJourneyWorldFixture.SeedText, EncounterJourneyWorldFixture.PlayerName, farm =>
            {
                WorldSnapshot stocked = new CareFarmBuilder(farm, supplies, splitStock: true).Build();
                Assert.Equal((0UL, 0UL), (stocked.Revision, stocked.AdvancingSteps));
                EntityId player = stocked.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
                EntityId helper = stocked.Quests!.Definitions.Single().GiverActorId;
                ActorControlBinding owner = stocked.Inventory.Controls.Single(value => value.ActorId == player);
                var inventory = new CountedAuthorityState(stocked.Inventory.Custody, stocked.Inventory.AdmittedUnitCapacity,
                    stocked.Inventory.Actors, stocked.Inventory.Controls.Select(value => value.ActorId == helper
                        ? new ActorControlBinding(owner.PrincipalId, owner.SessionId, helper) : value),
                    stocked.Inventory.Policies, stocked.Inventory.Sources, stocked.Inventory.ReservationIds);
                ContentHash admission = ContentHash.Compute(System.Text.Encoding.UTF8.GetBytes("test.shared-control-admission.v1")
                    .Concat(stocked.ContentHash.ToByteArray()).Concat(inventory.ContentHash.ToByteArray()).ToArray());
                return new WorldSnapshot(inventory, stocked.Space, stocked.Residents, admission, stocked.Calendar, 0, 0,
                    gathering: stocked.Gathering, production: stocked.Production, progression: stocked.Progression,
                    quests: stocked.Quests, construction: stocked.Construction);
            });
        EntityId patient = journey.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        EntityId companion = journey.Quests!.Definitions.Single().GiverActorId;
        var item = new CountedItemDefinition(CareJourneyAdmission.Definition("items/potion"), supplies.PotionVersion);
        var remedy = new RestorativeRemedyDefinition(CareJourneyAdmission.Definition("remedies/potion"), item,
            supplies.RestoredHp, CareJourneyAdmission.RemedyRangeSubunits);
        WorldSnapshot care = WorldCareAdmission.CreateFresh(journey, new CareSnapshot(journey.WorldId, journey.BranchId, 0,
            [remedy], [new(CareJourneyAdmission.Instance(journey, "care.agreement"), patient, companion,
                remedy.DefinitionId, remedy.Version)], [], []));
        var form = care.Space.GetActor(patient).Binding;
        var supported = new RestorativeFormBinding(form.DefinitionId, form.DefinitionVersion);
        var self = new RestorativeUseDefinition(CareJourneyAdmission.Definition("ordinary/self-potion"), remedy.DefinitionId,
            remedy.Version, RestorativeDeliveryMode.SelfUse, [supported]);
        var assist = new RestorativeUseDefinition(CareJourneyAdmission.Definition("ordinary/assisted-potion"), remedy.DefinitionId,
            remedy.Version, RestorativeDeliveryMode.LivingAssistance, [supported]);
        var permission = new CarePermission(CareJourneyAdmission.Instance(care, "ordinary.assistance"), patient, companion,
            assist.DefinitionId, assist.Version);
        return WorldRestorativeAdmission.CreateFresh(care, new OrdinaryCareSnapshot([self, assist], [permission], []));
    }
}
