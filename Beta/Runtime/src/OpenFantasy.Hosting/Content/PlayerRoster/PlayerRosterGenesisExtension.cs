using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Language;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;

namespace OpenFantasy.Hosting.Content.PlayerRoster;

/// <summary>Adds real human residents to the unplayed chief/cargo basis before market and outcome admission.</summary>
internal sealed partial class PlayerRosterGenesisExtension(WorldSnapshot basis, PlayerRosterDefinition roster)
{
    private const string IdentityProfile = "cargo-player-roster";
    private readonly HashSet<EntityId> _newIds = [];
    private static readonly (long X, long Y)[] Cardinal = [(0, -256), (-256, 0), (256, 0), (0, 256)];

    internal WorldSnapshot Build()
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(roster);
        if (basis.ProfileVersion != 26 || basis.Revision != 0 || basis.AdvancingSteps != 0
            || basis.Coordination is not null || basis.Dialogues.Count != 0
            || basis.EncounterLifecycle is not { EncounterCount: 0, HasEncounter: false }
            || basis.Care is not { Revision: 0, Ordinary.Revision: 0 })
            throw new ArgumentException("A roster extends only fresh ordinary-care cargo before market admission.");
        WorldResident first = basis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player);
        if (first.DisplayName != roster.PlayerNames[0])
            throw new ArgumentException("The first roster name must match the retained original player.");
        if (roster.PlayerNames.Count == 1) return basis;
        ActorPresence template = basis.Space.GetActor(first.ActorId);
        LocalSpatialSnapshot space = basis.Space;
        List<Member> added = [];
        for (int slot = 2; slot <= roster.PlayerNames.Count; slot++)
        {
            EntityId actor = Instance(slot, "actor");
            ActorFormBinding form = ActorFormBinding.Corporeal(actor, Instance(slot, "form"),
                Instance(slot, "physical-body"), template.Condition.Definition);
            ActorPresence presence = FindSpawn(space, template, form);
            space = space.WithActors(space.Actors.Append(presence));
            EntityId bag = Instance(slot, "storage");
            var comprehension = new ActorComprehensionProfile(actor, 0, first.Comprehension.RulesVersion, first.Comprehension.Entries);
            var resident = new WorldResident(actor, bag, roster.PlayerNames[slot - 1], ResidentControllerKind.Player,
                presence.Feet, 0, comprehension, first.ExperiencedTime);
            added.Add(new Member(slot, presence, resident, new ActorControlBinding(Instance(slot, "principal"),
                Instance(slot, "session"), actor), Instance(slot, "wallet")));
        }
        CountedAuthorityState inventory = ExtendInventory(first, added);
        ProgressionSnapshot progression = ExtendProgression(first, added);
        GatheringSnapshot gathering = basis.Gathering!;
        gathering = new(gathering.WorldId, gathering.BranchId, 0, gathering.Nodes, gathering.Tools,
            gathering.Actors.Concat(added.Select(member => new GatherActorWork(member.ActorId,
                basis.Gathering!.GetActorWork(first.ActorId).Effort))));
        var construction = ExtendConstruction(space, added);
        WorldResident[] residents = basis.Residents.Concat(added.Select(member => member.Resident)).ToArray();
        var lifecycle = ExtendLifecycle(first.ActorId, residents, added);
        CareSnapshot care = ExtendCare(first.ActorId, added);
        List<byte[]> fields = [basis.ContentHash.ToByteArray(), roster.Version.ToByteArray(), inventory.ContentHash.ToByteArray(),
            progression.ContentHash.ToByteArray(), gathering.ContentHash.ToByteArray(), construction.ContentHash.ToByteArray(),
            lifecycle.ContentHash.ToByteArray(), care.ContentHash.ToByteArray()];
        foreach (Member member in added)
        {
            fields.AddRange([member.ActorId.ToByteArray(), member.Presence.Binding.FormInstanceId.ToByteArray(),
                member.Presence.Binding.PhysicalBodyId!.Value.ToByteArray(), member.Presence.Binding.DefinitionVersion.ToByteArray(),
                CargoCanonicalFrame.Signed64(member.Presence.Feet.X), CargoCanonicalFrame.Signed64(member.Presence.Feet.Y),
                CargoCanonicalFrame.Signed64(member.Presence.Feet.Z)]);
        }
        ContentHash admission = CargoCanonicalFrame.Hash("cargo-player-roster.genesis.v1", fields);
        var history = new WorldResourceHistory(admission, new ActorResourceHistory(progression, 0, []), []);
        var water = CargoWaterRebinding.Rebind(basis, inventory, space);
        var world = new WorldSnapshot(inventory, space, residents, admission, basis.Calendar, 0, 0, basis.Dialogues,
            gathering, basis.Production, progression, basis.Quests, construction, reservoirs: water.Reservoirs,
            reservoirSeals: basis.ReservoirSeals, liquidCells: water.Cells, excavation: basis.Excavation,
            pacing: basis.Pacing, encounterLifecycle: lifecycle, care: care, resourceHistory: history);
        _ = WorldSnapshotCodec.Encode(world);
        return world;
    }

    private ActorPresence FindSpawn(LocalSpatialSnapshot space, ActorPresence first, ActorFormBinding form)
    {
        // A locally clear spawn can still seal the original player's only exit. Preserve the
        // escape capability of every actor that has one before this individual placement.
        var retainedExits = space.Actors.Select(actor => (actor.ActorId, Exits: Cardinal
            .Select(offset => actor.Feet.Offset(offset.X, offset.Y, 0))
            .Where(feet => space.QueryGroundRoute(actor.ActorId, feet).Status == GroundRouteStatus.Reachable)
            .ToArray())).Where(value => value.Exits.Length != 0).ToArray();
        var requiredAccess = basis.Construction!.Projects.SelectMany(project => project.RequiredAccess).ToArray();
        var reserved = basis.Construction!.Projects.SelectMany(project => project.RequiredAccess
            .SelectMany(access => new[] { access.From, access.To }))
            .Concat(basis.Construction.Projects.SelectMany(project => new[] { project.SiteFeet, project.WorkFeet }))
            .Concat(basis.Gathering!.Nodes.Select(node => node.Feet)).ToHashSet();
        for (int radius = 1; radius <= 6; radius++)
        for (int x = -radius; x <= radius; x++)
        for (int y = -radius; y <= radius; y++)
        {
            if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius) continue;
            SubunitPosition feet = first.Feet.Offset(x * 256L, y * 256L, 0);
            if (reserved.Contains(feet) || space.QueryOccupancy(first.ActorId, feet) != SpatialOccupancy.Clear
                || space.QueryGroundRoute(first.ActorId, feet).Status != GroundRouteStatus.Reachable) continue;
            var presence = new ActorPresence(form, new BodyConditionSnapshot(form.FormInstanceId, first.Condition.Definition, []),
                space.Scope, feet, 0, first.HomePlaceId);
            LocalSpatialSnapshot candidate = space.WithActors(space.Actors.Append(presence));
            if (retainedExits.Any(actor => !actor.Exits.Any(exit => candidate.QueryGroundRoute(actor.ActorId, exit).Status
                    == GroundRouteStatus.Reachable))
                || requiredAccess.Any(access => candidate.QueryGroundRoute(access.ActorId, access.From, access.To).Status
                    != GroundRouteStatus.Reachable)) continue;
            if (Cardinal.Any(offset => candidate.QueryGroundRoute(form.ActorId, feet.Offset(offset.X, offset.Y, 0)).Status
                == GroundRouteStatus.Reachable)) return presence;
        }
        throw new InvalidDataException("The admitted farm has no bounded valid grounded roster spawn.");
    }

    private CountedAuthorityState ExtendInventory(WorldResident first, IReadOnlyList<Member> added)
    {
        CountedAuthorityState original = basis.Inventory;
        var storages = original.Custody.Storages.ToList();
        var lots = original.Custody.Lots.ToList();
        var sources = original.Sources.ToList();
        GatherToolDefinition[] kit = basis.Gathering!.Tools.Where(tool => original.Custody.Lots.Any(lot =>
            lot.CustodianId == first.InventoryCustodianId && lot.Definition == tool.Definition)).OrderBy(tool => tool.Kind).ToArray();
        if (kit.Length != 2 || kit[0].Kind != GatherToolKind.Axe || kit[1].Kind != GatherToolKind.Sickle)
            throw new InvalidDataException("The production roster requires the retained source-pinned axe and sickle kit.");
        foreach (Member member in added)
        {
            storages.Add(new CustodianStorage(member.Resident.InventoryCustodianId,
                original.Custody.GetStorage(first.InventoryCustodianId).UnitCapacity, 0));
            foreach (GatherToolDefinition tool in kit)
            {
                string role = tool.Kind == GatherToolKind.Axe ? "axe" : "sickle";
                EntityId source = Instance(member.Slot, role + ".source");
                sources.Add(new AdmittedCountedSource(source, tool.Definition, 1));
                lots.Add(new CountedSourceLot(Instance(member.Slot, role + ".lot"), member.Resident.InventoryCustodianId,
                    tool.Definition, new SourceInterval(source, 0, 1), 0));
            }
        }
        var custody = new CustodySnapshot(original.WorldId, original.BranchId, 0, storages, lots);
        CustodyDepositRule policy = original.Policies.Single(value => value.CustodianId == first.InventoryCustodianId).DepositRule;
        return new CountedAuthorityState(custody, checked(original.AdmittedUnitCapacity + (ulong)added.Count * 2),
            original.Actors.Concat(added.Select(member => member.ActorId)), original.Controls.Concat(added.Select(member => member.Control)),
            original.Policies.Concat(added.Select(member => new CustodianAccessPolicy(member.Resident.InventoryCustodianId,
                member.ActorId, policy))), sources, original.ReservationIds);
    }

    private ProgressionSnapshot ExtendProgression(WorldResident first, IReadOnlyList<Member> added)
    {
        ProgressionSnapshot original = basis.Progression!;
        ActorProgressionState template = original.GetActor(first.ActorId);
        CurrencyLedger currency = original.Currency;
        currency = new(currency.WorldId, currency.BranchId, currency.Revision, currency.Currency, currency.AdmittedSupply,
            currency.Accounts.Concat(added.Select(member => new CurrencyAccount(member.AccountId, CurrencyAccountKind.Actor,
                member.ActorId, 0, 0))));
        return new(original.WorldId, original.BranchId, 0, original.Profiles, original.Curves,
            original.Actors.Concat(added.Select(member => new ActorProgressionState(member.ActorId, 0, template.Profile,
                template.Curve, template.Level, 0, template.Stats.MaxHp, template.Stats.MaxMp))), currency);
    }

    private CareSnapshot ExtendCare(EntityId first, IReadOnlyList<Member> added)
    {
        CareSnapshot care = basis.Care!;
        CareAgreement agreement = care.Agreements.Single(value => value.PatientActorId == first);
        OrdinaryCareSnapshot ordinary = care.Ordinary!;
        CarePermission permission = ordinary.Permissions.Single(value => value.PatientActorId == first);
        ordinary = new(ordinary.Definitions, ordinary.Permissions.Concat(added.Select(member =>
            new CarePermission(Instance(member.Slot, "care.permission"), member.ActorId, permission.HelperActorId,
                permission.UseDefinitionId, permission.UseDefinitionVersion))), []);
        return new(basis.WorldId, basis.BranchId, 0, care.Remedies, care.Agreements.Concat(added.Select(member =>
            new CareAgreement(Instance(member.Slot, "care.agreement"), member.ActorId, agreement.HelperActorId,
                agreement.RemedyDefinitionId, agreement.RemedyVersion))), [], [], ordinary);
    }

    private EntityId Instance(int slot, string role) => Register(ConstructionFarmIdentityScope.InstanceId(basis,
        IdentityProfile, "player-" + slot.ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + role));

    private EntityId Register(EntityId identity)
    {
        if (basis.IdentityIndex.Contains(identity) || !_newIds.Add(identity))
            throw new InvalidDataException("A roster identity collides with retained content or another roster role.");
        return identity;
    }

    private sealed record Member(int Slot, ActorPresence Presence, WorldResident Resident, ActorControlBinding Control, EntityId AccountId)
    {
        internal EntityId ActorId => Presence.ActorId;
    }
}
