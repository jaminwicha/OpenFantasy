using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Content.NativeCargo;
using OpenFantasy.Hosting.Content.PlayerRoster;
using OpenFantasy.Hosting.Content.RoadThreat;

namespace OpenFantasy.Hosting.Tests.Content.PlayerRoster;

public sealed class PlayerRosterAdmissionTests
{
    internal const string Seed = "production-player-roster";
    internal static readonly string[] Names = ["Rowan", "Mira", "Ash", "Pip"];
    private static readonly Lazy<WorldSnapshot> SoloValue = new(() => CargoOutcomeWorldAdmission.CreateEmbedded(Seed, Names[0]));
    private static readonly Lazy<WorldSnapshot> FourValue = new(() => PlayerRosterAdmission.CreateEmbedded(Seed, Names));
    internal static WorldSnapshot Solo => SoloValue.Value;
    internal static WorldSnapshot Four => FourValue.Value;

    [Fact]
    public void One_name_preserves_exact_solo_bytes_and_multi_rosters_bind_order_names_and_count()
    {
        Assert.Equal(WorldSnapshotCodec.Encode(Solo), WorldSnapshotCodec.Encode(PlayerRosterAdmission.CreateEmbedded(Seed, [Names[0]])));
        Assert.Equal(WorldSnapshotCodec.Encode(Four), WorldSnapshotCodec.Encode(PlayerRosterAdmission.CreateEmbedded(Seed, Names)));
        Assert.Equal(WorldSnapshotCodec.Encode(Four), WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(Four))));
        Assert.NotEqual(Four.AdmissionVersion, PlayerRosterAdmission.CreateEmbedded(Seed, [Names[0], Names[2], Names[1], Names[3]]).AdmissionVersion);
        Assert.NotEqual(Four.AdmissionVersion, PlayerRosterAdmission.CreateEmbedded(Seed, [Names[0], Names[1], Names[2], "Changed"]).AdmissionVersion);
        Assert.NotEqual(Four.AdmissionVersion, PlayerRosterAdmission.CreateEmbedded(Seed, [Names[0], Names[1], Names[2]]).AdmissionVersion);
        Assert.Equal(12, Four.EncounterLifecycle!.Scenarios.Count);
        Assert.Equal((0UL, 0UL), (Four.Revision, Four.AdvancingSteps));
    }

    [Fact]
    public void Invalid_names_counts_and_mutable_input_are_handled_before_admission()
    {
        Assert.Throws<ArgumentException>(() => new PlayerRosterDefinition([]));
        Assert.Throws<ArgumentException>(() => new PlayerRosterDefinition(["1", "2", "3", "4", "5"]));
        foreach (string invalid in new[] { "", " ", "line\nbreak", new string('x', 129), "\ud800" })
            Assert.ThrowsAny<ArgumentException>(() => new PlayerRosterDefinition(["First", invalid]));
        string[] names = ["First", "Second"];
        var roster = new PlayerRosterDefinition(names);
        names[1] = "Changed";
        Assert.Equal("Second", roster.PlayerNames[1]);
        Assert.Equal(new PlayerRosterDefinition(["First", "Second"]).Version, roster.Version);
    }

    [Fact]
    public void Every_old_source_lot_currency_reserve_and_npc_remains_with_its_owner()
    {
        foreach (var source in Solo.Inventory.Sources)
        {
            var kept = Assert.Single(Four.Inventory.Sources, value => value.SourceId == source.SourceId);
            Assert.Equal((source.Definition, source.UnitCapacity), (kept.Definition, kept.UnitCapacity));
        }
        foreach (var lot in Solo.Inventory.Custody.Lots)
        {
            var kept = Four.Inventory.Custody.GetLot(lot.LotId);
            Assert.Equal((lot.CustodianId, lot.Definition, lot.Source, lot.Quantity, lot.Revision, lot.ReservedBy, lot.Protection),
                (kept.CustodianId, kept.Definition, kept.Source, kept.Quantity, kept.Revision, kept.ReservedBy, kept.Protection));
        }
        Assert.Equal(Solo.Progression!.Currency.AdmittedSupply, Four.Progression!.Currency.AdmittedSupply);
        foreach (var account in Solo.Progression.Currency.Accounts)
        {
            var kept = Four.Progression.Currency.GetAccount(account.AccountId);
            Assert.Equal((account.Kind, account.ActorId, account.Balance, account.Revision),
                (kept.Kind, kept.ActorId, kept.Balance, kept.Revision));
        }
        Assert.Equal(Solo.Quests!.ContentHash, Four.Quests!.ContentHash);
        Assert.Equal(Solo.Production!.ContentHash, Four.Production!.ContentHash);
        foreach (var resident in Solo.Residents.Where(value => value.ControllerKind == ResidentControllerKind.Host))
        {
            Assert.Equal(ResidentControllerKind.Host, Four.GetResident(resident.ActorId).ControllerKind);
            Assert.Equal(resident.DisplayName, Four.GetResident(resident.ActorId).DisplayName);
            Assert.Equal(Solo.Space.GetActor(resident.ActorId).Feet, Four.Space.GetActor(resident.ActorId).Feet);
            Assert.Equal(Solo.Progression.GetActor(resident.ActorId).CurrentHp, Four.Progression.GetActor(resident.ActorId).CurrentHp);
        }
        Assert.Equal(NativeCargoWorld.Stalls(Solo), NativeCargoWorld.Stalls(Four));
        Assert.NotNull(CargoOutcomeWorldAdmission.Find(Four, NativeCargoWorld.Sources, RoadThreatSources.LoadEmbedded()));
    }

    [Fact]
    public void Additional_players_own_complete_facets_and_only_two_explicit_finite_tool_units_each()
    {
        EntityId first = Solo.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        var added = Four.Residents.Where(value => value.ControllerKind == ResidentControllerKind.Player && value.ActorId != first).ToArray();
        Assert.Equal(3, added.Length);
        Assert.Equal(Solo.Inventory.AdmittedUnitCapacity + 6UL, Four.Inventory.AdmittedUnitCapacity);
        Assert.Equal(Solo.Inventory.Sources.Count + 6, Four.Inventory.Sources.Count);
        Assert.Equal(Solo.Inventory.Custody.Lots.Count + 6, Four.Inventory.Custody.Lots.Count);
        var controls = Four.Inventory.Controls.Where(value => value.ActorId == first || added.Any(actor => actor.ActorId == value.ActorId)).ToArray();
        Assert.Equal(4, controls.Select(value => value.PrincipalId).Distinct().Count());
        Assert.Equal(4, controls.Select(value => value.SessionId).Distinct().Count());
        foreach (var member in added)
        {
            var control = Assert.Single(controls, value => value.ActorId == member.ActorId);
            Assert.True(Four.Inventory.CanControl(new(control.PrincipalId, control.SessionId), member.ActorId));
            Assert.False(Four.Inventory.CanControl(Delivery(Four, first), member.ActorId));
            var own = Four.Inventory.Custody.Lots.Where(value => value.CustodianId == member.InventoryCustodianId).ToArray();
            Assert.Equal(2, own.Length);
            Assert.All(own, lot => Assert.Equal((1UL, 0UL, 1UL), (lot.Quantity, lot.Source.Start, lot.Source.End)));
            Assert.Equal(new[] { GatherToolKind.Axe, GatherToolKind.Sickle }, own.Select(lot =>
                Four.Gathering!.Tools.Single(tool => tool.Definition == lot.Definition).Kind).Order());
            Assert.All(own, lot => Assert.DoesNotContain(Solo.Inventory.Sources, source => source.SourceId == lot.Source.SourceId));
            var stats = Four.Progression!.GetActor(member.ActorId);
            Assert.Equal((stats.Stats.MaxHp, stats.Stats.MaxMp, 0UL), (stats.CurrentHp, stats.CurrentMp, stats.Experience));
            Assert.Equal(Solo.Progression!.GetActor(first).Profile.Version, stats.Profile.Version);
            Assert.Equal(0UL, Four.Progression.Currency.GetActorAccount(member.ActorId).Balance);
            Assert.Equal(Four.Gathering!.GetActorWork(first).Effort, Four.Gathering.GetActorWork(member.ActorId).Effort);
            Assert.Equal(0UL, Four.EncounterLifecycle!.GetDebt(member.ActorId).RemainingDelay);
            Assert.Single(Four.Care!.Agreements, value => value.PatientActorId == member.ActorId);
            Assert.Single(Four.Care.Ordinary!.Permissions, value => value.PatientActorId == member.ActorId);
            Assert.Equal(SpatialOccupancy.Clear, Four.Space.QueryOccupancy(member.ActorId, member.HomeFeet));
            foreach (var project in Four.Construction!.Projects)
            {
                var access = Assert.Single(project.RequiredAccess, value => value.ActorId == member.ActorId);
                Assert.Equal(member.HomeFeet, access.From);
                Assert.Equal(GroundRouteStatus.Reachable, Four.Space.QueryGroundRoute(member.ActorId, access.To).Status);
            }
        }
        Assert.Equal(Four.Residents.Count, Four.Space.Actors.Select(actor => actor.Feet).Distinct().Count());
        Assert.Equal(Four.Residents.Select(value => value.ActorId), Four.Gathering!.Actors.Select(value => value.ActorId));
        Assert.Equal(Four.Inventory.ContentHash, Four.Reservoirs!.Capacity.Inventory.ContentHash);
        Assert.Equal(NativeCargoWorld.Corridor(Solo).OriginApproach, NativeCargoWorld.Corridor(Four).OriginApproach);
    }

    [Fact]
    public void Roster_preserves_prior_escape_capability_and_exact_authored_construction_access()
    {
        (long X, long Y)[] directions = [(0, -256), (-256, 0), (256, 0), (0, 256)];
        foreach (var actor in Solo.Space.Actors)
        {
            var priorExits = directions.Select(offset => actor.Feet.Offset(offset.X, offset.Y, 0))
                .Where(exit => Solo.Space.QueryGroundRoute(actor.ActorId, exit).Status == GroundRouteStatus.Reachable).ToArray();
            if (priorExits.Length == 0) continue;
            Assert.Contains(priorExits, exit => Four.Space.QueryGroundRoute(actor.ActorId, exit).Status == GroundRouteStatus.Reachable);
        }
        foreach (var access in Solo.Construction!.Projects.SelectMany(project => project.RequiredAccess))
        {
            Assert.Contains(Four.Construction!.Projects.SelectMany(project => project.RequiredAccess), value => value == access);
            Assert.Equal(GroundRouteStatus.Reachable, Four.Space.QueryGroundRoute(access.ActorId, access.From, access.To).Status);
        }
    }

    [Fact]
    public void All_humans_have_cooperative_wolf_entries_and_chief_targets_without_changing_npc_grants()
    {
        var humans = Four.Residents.Where(actor => actor.ControllerKind == ResidentControllerKind.Player).Select(actor => actor.ActorId).ToArray();
        EntityId chief = CargoOutcomeWorldAdmission.Find(Four, NativeCargoWorld.Sources, RoadThreatSources.LoadEmbedded())!.ChiefActorId;
        foreach (EntityId human in humans)
        {
            Assert.Equal(2, Four.EncounterLifecycle!.Scenarios.Count(scenario => scenario.InitiatingActorId == human));
            Assert.Single(Four.EncounterLifecycle.Scenarios, scenario => scenario.InitiatingActorId == chief && scenario.TargetActorId == human);
        }
        foreach (var original in Solo.EncounterLifecycle!.Scenarios)
        {
            var kept = Four.EncounterLifecycle!.GetScenario(original.DefinitionId);
            Assert.Equal((original.InitiatingActorId, original.TargetActorId, original.EntryRangeSubunits),
                (kept.InitiatingActorId, kept.TargetActorId, kept.EntryRangeSubunits));
            foreach (var npc in original.Combatants.Where(actor => !humans.Contains(actor.ActorId)))
            {
                var combatant = kept.Combatants.Single(actor => actor.ActorId == npc.ActorId);
                Assert.Equal(npc.Side, combatant.Side);
                Assert.Equal(npc.AbilityIds, combatant.AbilityIds);
            }
        }
        foreach (var scenario in Four.EncounterLifecycle!.Scenarios)
        {
            Assert.Equal(Four.Residents.Select(actor => actor.ActorId), scenario.Combatants.Select(actor => actor.ActorId));
            var cooperative = scenario.Combatants.Where(actor => humans.Contains(actor.ActorId)).ToArray();
            Assert.All(cooperative, actor => Assert.True(actor.Side > 0));
            Assert.Single(cooperative.Select(actor => actor.Side).Distinct());
            Assert.All(cooperative, actor => Assert.Equal(cooperative[0].AbilityIds, actor.AbilityIds));
        }
    }

    [Fact]
    public void Extension_refuses_a_lived_root_without_resetting_its_history()
    {
        WorldSnapshot basis = new RoadThreatGenesisExtension(new CargoGenesisExtension(
            RestorativeJourneyAdmission.CreateEmbedded(Seed, Names[0]), NativeCargoWorld.Sources).Build(),
            RoadThreatSources.LoadEmbedded()).Build();
        var step = new WorldStepBatch(new(0x524f53544552, 1), basis.WorldId, basis.BranchId, 0, 0,
            WorldBatchKind.AdvancingStep, [], [], basis.ProfileVersion);
        WorldSnapshot lived = new WorldStepProcessor().Decide(basis, step).Candidate;
        var before = lived.ContentHash;
        Assert.Throws<ArgumentException>(() => new PlayerRosterGenesisExtension(lived, new(Names)).Build());
        Assert.Equal(before, lived.ContentHash);
        Assert.Throws<ArgumentException>(() => new PlayerRosterGenesisExtension(Four, new(Names)).Build());
    }

    internal static TrustedCommandDelivery Delivery(WorldSnapshot state, EntityId actor)
    {
        var control = state.Inventory.Controls.Single(value => value.ActorId == actor);
        return new(control.PrincipalId, control.SessionId);
    }
}
