using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Language;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldExchange;

/// <summary>
/// A fresh profile-32 genesis with a third capable human and authored constrained stock. Both join the
/// unplayed farm inside the encounter factory, after care supplies and before water, capacity, pacing,
/// encounters and care bind the complete graph. A same-seed original genesis lends only its read-only
/// remedy, agreement and ordinary-use definitions. Nothing is earned, moved or paid here, and tests
/// create and reopen the encoded genesis only through <see cref="SqliteWorldAuthority"/>.
/// </summary>
internal sealed class PaidTradeWorldFixture
{
    internal const ulong OrdinaryMerchantCapacity = 12;
    internal const ulong UnitsPerLot = 2;
    private const ulong Scope = 0x7A1D_57C0_0000_0000;
    private readonly HashSet<EntityId> _authored = [];
    private readonly ulong _merchantCapacity;
    private readonly bool _merchantSortsFirst;

    /// <param name="merchantCapacity">The third person's own pack capacity; it starts empty.</param>
    /// <param name="merchantSortsFirst">Authors the third person's fresh actor identity before or after the player's.</param>
    internal PaidTradeWorldFixture(ulong merchantCapacity = OrdinaryMerchantCapacity, bool merchantSortsFirst = false)
    {
        _merchantCapacity = merchantCapacity;
        _merchantSortsFirst = merchantSortsFirst;
        FarmContentFiles files = FarmContentFiles.Embedded();
        CareFarmSources supplies = CareFarmSources.Load(files);
        WorldSnapshot journey = EncounterJourneyAdmission.Create(files, null, EncounterJourneyWorldFixture.SeedText,
            EncounterJourneyWorldFixture.PlayerName, farm => Compose(new CareFarmBuilder(farm, supplies, splitStock: true).Build()));
        Genesis = WorldPaidExchangeAdmission.CreateFresh(AdmitOrdinaryCare(journey));
        Assert.Equal(WorldPaidExchangeAdmission.ProfileVersion, Genesis.ProfileVersion);
        Assert.Equal(Genesis.ContentHash, WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(Genesis)).ContentHash);
        Journey = new CareRewardJourney(false, Genesis);
    }

    internal WorldSnapshot Genesis { get; }
    internal CareRewardJourney Journey { get; }
    internal EntityId Player => Journey.Player;
    internal EntityId Helper => Journey.Helper;
    internal EntityId Merchant { get; private set; }
    /// <summary>Wren's three equal lots of one fresh source: ordinary, favorite-protected and reserved.</summary>
    internal EntityId OrdinaryLot { get; private set; }
    internal EntityId ProtectedLot { get; private set; }
    internal EntityId ReservedLot { get; private set; }
    internal EntityId Reservation { get; private set; }

    internal SqliteWorldAuthority Create(StorageFiles files) => SqliteWorldAuthority.CreateNew(files.Path, Genesis);

    internal SqliteWorldAuthority Reopen(StorageFiles files) =>
        SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(Genesis));

    private WorldSnapshot Compose(WorldSnapshot farm)
    {
        Assert.Equal((0UL, 0UL), (farm.Revision, farm.AdvancingSteps));
        WorldResident player = farm.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player);
        WorldResident helper = farm.GetResident(farm.Quests!.Definitions.Single().GiverActorId);
        Merchant = Register(farm, new EntityId(_merchantSortsFirst ? 1UL : ulong.MaxValue, Scope));
        Assert.Equal(_merchantSortsFirst, Merchant < player.ActorId);
        EntityId storage = Register(farm, Id(2));
        var control = new ActorControlBinding(Register(farm, Id(5)), Register(farm, Id(6)), Merchant);

        // The source-backed human anatomy already shared by the player and Wren; no body is authored.
        ActorPresence anchor = farm.Space.GetActor(player.ActorId);
        BodyDefinition body = anchor.Condition.Definition;
        BodyDefinition wren = farm.Space.GetActor(helper.ActorId).Condition.Definition;
        Assert.Equal((wren.DefinitionId, wren.Version), (body.DefinitionId, body.Version));
        var binding = ActorFormBinding.Corporeal(Merchant, Register(farm, Id(3)), Register(farm, Id(4)), body);
        SubunitPosition feet = anchor.Feet.Offset(0, 256, 0), exit = feet.Offset(256, 0, 0);
        var presence = new ActorPresence(binding, new BodyConditionSnapshot(binding.FormInstanceId, body, []),
            farm.Space.Scope, feet, 0, anchor.HomePlaceId);
        LocalSpatialSnapshot space = farm.Space.WithActors(farm.Space.Actors.Append(presence));
        // A clear grounded stance with living hands, actual reciprocal reach to both people and a real exit.
        Assert.Equal(SpatialOccupancy.Clear, space.QueryOccupancy(Merchant, feet));
        Assert.True(presence.Condition.SupportsFunction(BodyFunction.Manipulation));
        foreach (EntityId other in new[] { player.ActorId, helper.ActorId })
        {
            Assert.Equal(SpatialInteraction.Reachable, space.QueryInteraction(Merchant, other));
            Assert.Equal(SpatialInteraction.Reachable, space.QueryInteraction(other, Merchant));
        }
        Assert.Equal(GroundRouteStatus.Reachable, space.QueryGroundRoute(Merchant, exit).Status);
        var resident = new WorldResident(Merchant, storage, "Market trader", ResidentControllerKind.Host, feet, 0,
            new ActorComprehensionProfile(Merchant, 0, helper.Comprehension.RulesVersion, []), LocalExposureDuration.Zero);

        CountedAuthorityState inventory = AddInventory(farm, helper, storage, control);
        GatheringSnapshot gathering = farm.Gathering!;
        gathering = new(gathering.WorldId, gathering.BranchId, gathering.AdvancingStep, gathering.Nodes, gathering.Tools,
            gathering.Actors.Append(new GatherActorWork(Merchant, 0)));
        ProgressionSnapshot progression = AddProgression(farm, player.ActorId);
        ConstructionSnapshot construction = farm.Construction!;
        construction = new(construction.WorldId, construction.BranchId, construction.Revision, construction.Definitions,
            construction.Projects.Select(project => new ConstructionProject(project.ProjectId, project.Definition,
                project.OwnerActorId, project.BuilderActorId, project.QuestId, project.ProjectCustodianId,
                project.ReservationId, project.SiteFeet, project.WorkFeet,
                project.RequiredAccess.Append(new GroundAccessRequirement(Merchant, feet, exit)),
                project.Revision, project.Progress, project.Installed, project.StockLocation)));
        WorldSnapshot Root(ContentHash admission) => new(inventory, space, farm.Residents.Append(resident), admission,
            farm.Calendar, 0, 0, gathering: gathering, production: farm.Production, progression: progression,
            quests: farm.Quests, construction: construction);
        ContentHash version = ContentHash.Compute(Encoding.UTF8.GetBytes("test.paid-trade-fixture.admission.v1")
            .Concat(farm.AdmissionVersion.ToByteArray()).Concat(Root(farm.AdmissionVersion).ContentHash.ToByteArray()).ToArray());
        return Root(version);
    }

    /// <summary>
    /// The third person's own empty pack under its own control, plus finite fresh orchard stock in Wren's
    /// pack: one source split into ordinary, favorite-protected and reservation-held intervals.
    /// </summary>
    private CountedAuthorityState AddInventory(WorldSnapshot farm, WorldResident helper, EntityId storage,
        ActorControlBinding control)
    {
        CountedItemDefinition berries = farm.Gathering!.Nodes.Single(value => value.Definition.Kind == GatherNodeKind.Berry)
            .Definition.YieldDefinition;
        EntityId source = Register(farm, Id(10));
        Reservation = Register(farm, Id(11));
        OrdinaryLot = Register(farm, Id(12));
        ProtectedLot = Register(farm, Id(13));
        ReservedLot = Register(farm, Id(14));
        EntityId pack = helper.InventoryCustodianId;
        const ulong added = 3 * UnitsPerLot;
        CountedSourceLot[] stock =
        [
            new(OrdinaryLot, pack, berries, new(source, 0, UnitsPerLot), 0),
            new(ProtectedLot, pack, berries, new(source, UnitsPerLot, 2 * UnitsPerLot), 0, LotProtection.Favorite),
            new(ReservedLot, pack, berries, new(source, 2 * UnitsPerLot, added), 0, reservedBy: Reservation)
        ];
        CountedAuthorityState old = farm.Inventory;
        var custody = new CustodySnapshot(farm.WorldId, farm.BranchId, old.Custody.Revision,
            old.Custody.Storages.Select(value => value.CustodianId == pack
                    ? new CustodianStorage(pack, checked(value.UnitCapacity + added), value.Revision) : value)
                .Append(new CustodianStorage(storage, _merchantCapacity, 0)),
            old.Custody.Lots.Concat(stock));
        var inventory = new CountedAuthorityState(custody, checked(old.AdmittedUnitCapacity + added),
            old.Actors.Append(Merchant), old.Controls.Append(control),
            old.Policies.Append(new CustodianAccessPolicy(storage, Merchant, CustodyDepositRule.OwnerOnly)),
            old.Sources.Append(new AdmittedCountedSource(source, berries, added)), old.ReservationIds.Append(Reservation));
        farm.Production!.ValidateForAuthority(inventory);
        return inventory;
    }

    /// <summary>The player's source-backed human class profile and curve at the first level, with an empty wallet.</summary>
    private ProgressionSnapshot AddProgression(WorldSnapshot farm, EntityId player)
    {
        ProgressionSnapshot original = farm.Progression!;
        ActorProgressionState exemplar = original.GetActor(player);
        ActorStatValues stats = exemplar.Profile.Calculate(1);
        CurrencyLedger ledger = original.Currency;
        ledger = new(ledger.WorldId, ledger.BranchId, ledger.Revision, ledger.Currency, ledger.AdmittedSupply,
            ledger.Accounts.Append(new CurrencyAccount(Register(farm, Id(7)), CurrencyAccountKind.Actor, Merchant, 0, 0)));
        return new(original.WorldId, original.BranchId, original.Revision, original.Profiles, original.Curves,
            original.Actors.Append(new ActorProgressionState(Merchant, 0, exemplar.Profile, exemplar.Curve, 1, 0,
                stats.MaxHp, stats.MaxMp)), ledger);
    }

    /// <summary>
    /// Finishes care from the same-seed original's authored templates, only when their identities, participants,
    /// remedy stock and supported form match this composition and the template earned nothing.
    /// </summary>
    private static WorldSnapshot AdmitOrdinaryCare(WorldSnapshot journey)
    {
        WorldSnapshot template = new OrdinaryCareWorldFixture().Genesis;
        CareSnapshot care = template.Care!;
        OrdinaryCareSnapshot ordinary = care.Ordinary!;
        Assert.Equal((template.WorldId, template.BranchId), (journey.WorldId, journey.BranchId));
        Assert.Equal((0UL, 0UL, 0UL, 0UL), (template.Revision, template.AdvancingSteps, care.Revision, ordinary.Revision));
        Assert.Empty(care.Acknowledgments);
        Assert.Empty(care.Treatments);
        Assert.Null(care.Pending);
        Assert.Empty(ordinary.Uses);
        Assert.Empty(template.ResourceHistory!.History.Batches);
        EntityId player = journey.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        EntityId helper = journey.Quests!.Definitions.Single().GiverActorId;
        RestorativeRemedyDefinition remedy = Assert.Single(care.Remedies);
        CareAgreement agreement = Assert.Single(care.Agreements);
        Assert.Equal((player, helper, remedy.DefinitionId, remedy.Version),
            (agreement.PatientActorId, agreement.HelperActorId, agreement.RemedyDefinitionId, agreement.RemedyVersion));
        Assert.Contains(journey.Inventory.Custody.Lots, lot => lot.Definition == remedy.Item
            && lot.CustodianId == journey.GetResident(helper).InventoryCustodianId);
        ActorFormBinding form = journey.Space.GetActor(player).Binding;
        Assert.All(ordinary.Definitions, value => Assert.Equal(new RestorativeFormBinding(form.DefinitionId,
            form.DefinitionVersion), Assert.Single(value.SupportedForms)));
        Assert.All(ordinary.Permissions, value => Assert.Equal((player, helper), (value.PatientActorId, value.HelperActorId)));
        EntityId[] lent = [remedy.DefinitionId, agreement.AgreementId,
            .. ordinary.Definitions.Select(value => value.DefinitionId), .. ordinary.Permissions.Select(value => value.PermissionId)];
        Assert.DoesNotContain(lent, value => journey.IdentityIndex.Contains(value));
        // The third person receives nothing: total supply and every original wallet equal the original genesis.
        CurrencyLedger original = template.Progression!.Currency, composed = journey.Progression!.Currency;
        Assert.Equal(original.AdmittedSupply, composed.AdmittedSupply);
        Assert.Equal(original.Accounts.Select(value => (value.AccountId, value.Balance)), composed.Accounts
            .Where(value => original.Accounts.Any(prior => prior.AccountId == value.AccountId))
            .Select(value => (value.AccountId, value.Balance)));
        WorldSnapshot cared = WorldCareAdmission.CreateFresh(journey, new CareSnapshot(journey.WorldId, journey.BranchId, 0,
            care.Remedies, care.Agreements, [], []));
        return WorldRestorativeAdmission.CreateFresh(cared, new OrdinaryCareSnapshot(ordinary.Definitions, ordinary.Permissions, []));
    }

    private EntityId Register(WorldSnapshot farm, EntityId identity)
    {
        Assert.False(farm.IdentityIndex.Contains(identity), "An authored identity must not alias admitted authority.");
        Assert.True(_authored.Add(identity), "Each authored role needs its own identity.");
        return identity;
    }

    private static EntityId Id(ulong role) => new(Scope, role);
}
