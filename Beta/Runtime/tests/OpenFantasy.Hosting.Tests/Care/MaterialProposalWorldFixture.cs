using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>
/// One fresh saved-proposal world lived through the SQLite owner up to injured exploration with the
/// helper's actually crafted potions. Each test continues its own disposable copy of that save.
/// </summary>
public sealed class MaterialProposalWorldFixture : IDisposable
{
    internal const string EvidenceVariable = "OPENFANTASY_MATERIAL_PROPOSAL_EVIDENCE";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "openfantasy-material-proposals-" + Guid.NewGuid().ToString("N"));
    private readonly ConsentedWorldPublicationTests.DurableAccess _access = new();
    private string BasePath => Path.Combine(_directory, "base.sqlite3");

    public MaterialProposalWorldFixture()
    {
        Directory.CreateDirectory(_directory);
        var ordinary = new OrdinaryCareWorldFixture(_access.OrdinaryGenesis);
        Actions = ordinary.Journey;
        Player = Actions.Player;
        Helper = Actions.Helper;
        WorldSnapshot genesis = WorldMaterialProposalAdmission.CreateFresh(_access.OrdinaryGenesis);
        GenesisBytes = WorldSnapshotCodec.Encode(genesis);
        ConsentedGenesisBytes = WorldSnapshotCodec.Encode(WorldConsentedMaterialAdmission.CreateFresh(_access.OrdinaryGenesis));
        PlayerBag = genesis.GetResident(Player).InventoryCustodianId;
        HelperBag = genesis.GetResident(Helper).InventoryCustodianId;
        using SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(BasePath, genesis);
        var session = new CareWorldSession(authority);
        WorldSnapshot state = ordinary.ReachFullDoseInjuredExploration(session);
        CountedSourceLot berries = state.Inventory.Custody.Lots.Single(value => value.CustodianId == HelperBag
            && _access.IsBerries(value));
        WorldActionCommand craft = ConsentedWorldPublicationTests.DurableAccess.CraftFor(state, Actions, Helper,
            _access.Bulk, new(IngredientLotKind.Source, berries.LotId, berries.Revision, 12));
        state = CareRewardJourney.Commit(session, Actions.Batch(state, WorldBatchKind.AdvancingStep, [craft])).Candidate;
        ProductionBatch producer = state.Production!.Batches.Single(value => value.CommandId == craft.CommandId);
        Producer = producer.BatchId;
        Stock = state.Production.Lots.Single(value => value.Output.BatchId == producer.BatchId).LotId;
    }

    internal CareRewardJourney Actions { get; }
    internal EntityId Player { get; }
    internal EntityId Helper { get; }
    internal EntityId PlayerBag { get; }
    internal EntityId HelperBag { get; }
    internal EntityId Producer { get; }
    internal EntityId Stock { get; }
    internal byte[] GenesisBytes { get; }
    internal byte[] ConsentedGenesisBytes { get; }
    internal CraftRecipeDefinition Repack => _access.Repack;
    internal RestorativeUseDefinition Self => _access.Self;

    internal LiveWorld Open() => new(this, BasePath);

    internal EntityId NextCause(WorldSnapshot state) => Actions.Batch(state, WorldBatchKind.AdvancingStep).CauseId;

    internal TrustedCommandDelivery DeliveryOf(WorldSnapshot state, EntityId actor)
    {
        var control = state.Inventory.Controls.Single(value => value.ActorId == actor);
        return new(control.PrincipalId, control.SessionId);
    }

    /// <summary>
    /// One unit of any actual unreserved lot the sender holds, raw or produced. Saving an offer pins
    /// real current stock and needs no receiving capacity.
    /// </summary>
    internal ConsentedMaterialTerms PinnedRawTerms(WorldSnapshot state, EntityId sender, EntityId recipient)
    {
        EntityId source = state.GetResident(sender).InventoryCustodianId;
        EntityId destination = state.GetResident(recipient).InventoryCustodianId;
        CountedSourceLot? raw = state.Inventory.Custody.Lots.FirstOrDefault(value => value.CustodianId == source
            && value.Quantity >= 1 && value.ReservedBy is null && value.Protection == LotProtection.None);
        if (raw is not null)
        {
            return new(state.WorldId, state.BranchId, sender, recipient, ConsentedMaterialLotKind.CountedSource,
                raw.LotId, raw.Definition, source, destination, 1, raw.Revision);
        }
        TransformedLot produced = state.Production!.Lots.First(value => value.CustodianId == source && value.Quantity >= 1);
        return new(state.WorldId, state.BranchId, sender, recipient, ConsentedMaterialLotKind.Produced,
            produced.LotId, produced.Definition, source, destination, 1, produced.Revision);
    }

    /// <summary>
    /// Previews real candidate offers of finite non-recipe stock on the pure processor and selects
    /// the first split gift the current world would save and then settle. Nothing is published.
    /// </summary>
    internal ConsentedMaterialTerms SelectRawTerms(SqliteWorldAuthority authority)
    {
        WorldSnapshot state = authority.ReadStateForHost();
        foreach ((EntityId sender, EntityId recipient) in new[] { (Helper, Player), (Player, Helper) })
        {
            EntityId source = state.GetResident(sender).InventoryCustodianId;
            foreach (CountedSourceLot lot in state.Inventory.Custody.Lots.Where(value => value.CustodianId == source
                && value.Quantity > 1 && value.ReservedBy is null && value.Protection == LotProtection.None
                && !_access.IsBerries(value)))
            {
                var terms = new ConsentedMaterialTerms(state.WorldId, state.BranchId, sender, recipient,
                    ConsentedMaterialLotKind.CountedSource, lot.LotId, lot.Definition, source,
                    state.GetResident(recipient).InventoryCustodianId, 1, lot.Revision);
                if (WouldSettle(authority, state, terms)) return terms;
            }
        }
        throw new InvalidOperationException("The fresh world must hold one giftable finite raw lot beside its recipe berries.");
    }

    private bool WouldSettle(SqliteWorldAuthority authority, WorldSnapshot state, ConsentedMaterialTerms terms)
    {
        var processor = new WorldStepProcessor();
        WorldActionCommand create = Actions.Command(state, terms.SenderActorId,
            new ProposeMaterialIntent(terms, state.AdvancingSteps + 8));
        WorldStepDecision saved = processor.Decide(state, Actions.Batch(state, WorldBatchKind.AdvancingStep, [create]),
            authority.IsIdentityOccupiedForHost);
        if (saved.Receipts.Single().Rejection != WorldActionRejection.None) return false;
        WorldSnapshot next = saved.Candidate;
        WorldMaterialProposal proposal = next.Coordination!.Proposals!.Open.Single(value =>
            value.CreationCommandId == create.CommandId);
        WorldActionCommand accept = Actions.Command(next, terms.RecipientActorId,
            AcceptMaterialProposalIntent.Approving(proposal, next.Space.GetActor(terms.SenderActorId).Revision));
        HashSet<EntityId> reserved = saved.Identities.Select(value => value.Identity).ToHashSet();
        WorldStepDecision settled = processor.Decide(next, Actions.Batch(next, WorldBatchKind.AdvancingStep, [accept]),
            identity => reserved.Contains(identity) || authority.IsIdentityOccupiedForHost(identity));
        return settled.Receipts.Single().Rejection == WorldActionRejection.None;
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    /// <summary>One disposable continuation of the base save with its authenticated application boundary.</summary>
    internal sealed class LiveWorld : IDisposable
    {
        private readonly MaterialProposalWorldFixture _world;

        internal LiveWorld(MaterialProposalWorldFixture world, string basePath)
        {
            _world = world;
            Files = new StorageFiles();
            File.Copy(basePath, Files.Path);
            Authority = SqliteWorldAuthority.OpenExisting(Files.Path, world.GenesisBytes);
            Service = CreateService();
        }

        internal StorageFiles Files { get; }
        internal SqliteWorldAuthority Authority { get; private set; }
        internal MaterialProposalService Service { get; private set; }
        internal WorldSnapshot State => Authority.ReadStateForHost();
        internal WorldMaterialProposalBook Book => State.Coordination!.Proposals!;

        /// <summary>A real accepted commit between two submissions.</summary>
        internal void Step() => Authority.Commit(_world.Actions.Batch(State, WorldBatchKind.AdvancingStep));

        /// <summary>Actual disposal and complete recovery from the save's own journal.</summary>
        internal void Reopen()
        {
            Authority.Dispose();
            Authority = SqliteWorldAuthority.OpenExisting(Files.Path, _world.GenesisBytes);
            Service = CreateService();
        }

        internal (WorldActionCommand Command, TrustedCommandDelivery Delivery, EntityId ProposalId) Save(
            ConsentedMaterialTerms terms, ulong lifetime = 32)
        {
            WorldSnapshot state = State;
            WorldActionCommand create = _world.Actions.Command(state, terms.SenderActorId,
                new ProposeMaterialIntent(terms, state.AdvancingSteps + 1 + lifetime));
            TrustedCommandDelivery delivery = _world.Actions.Delivery(create);
            Assert.Equal(WorldActionRejection.None, Service.Submit(create, delivery).Rejection);
            return (create, delivery, WorldMaterialProposal.DeriveIdentity(create.CommandId));
        }

        internal WorldActionCommand Command(EntityId actor, WorldActionIntent intent) =>
            _world.Actions.Command(State, actor, intent);

        internal WorldActionRejection Submit(EntityId actor, WorldActionIntent intent)
        {
            WorldActionCommand command = Command(actor, intent);
            return Service.Submit(command, _world.Actions.Delivery(command)).Rejection;
        }

        private MaterialProposalService CreateService() => new(Authority, () => _world.NextCause(State));

        public void Dispose()
        {
            Authority.Dispose();
            Files.Dispose();
        }
    }
}
