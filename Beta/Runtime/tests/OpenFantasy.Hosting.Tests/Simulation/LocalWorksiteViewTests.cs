using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class LocalWorksiteViewTests
{
    [Fact]
    public void Legacy_views_stay_without_a_worksite_until_both_excavation_and_physical_geometry_are_admitted()
    {
        WorldSnapshot original = WorldServiceFixture.Genesis();
        Assert.Null(new LocalWorldView(original, WorldServiceFixture.Id(10)).Worksite);
        var fixture = new ConstructedSupportWorldFixture();
        WorldSnapshot stepped = WorldSteppedSupportAdmission.CreateGenesis(fixture.Genesis);
        foreach (WorldSnapshot legacy in new[] { fixture.ExcavationGenesis, fixture.Genesis, stepped })
        {
            Assert.Null(new LocalWorldView(legacy, fixture.Player).Worksite);
        }
        WorldSnapshot physical = WorldConstructionPhysicalAdmission.CreateGenesis(stepped);
        LocalWorksiteView worksite = Assert.IsType<LocalWorksiteView>(new LocalWorldView(physical, fixture.Player).Worksite);
        Assert.Empty(worksite.Members);
        Assert.Single(worksite.ExcavationTargets);
        AssertTerrain(physical, worksite);
    }

    [Fact]
    public void Owned_work_targets_and_actual_tools_have_exact_revisions_without_private_material_or_NPC_custody()
    {
        var fixture = new CoupledSettlementWorldFixture(eventOrdered: true);
        WorldSnapshot state = fixture.Genesis;
        var view = new LocalWorldView(state, fixture.Player);
        LocalWorksiteView worksite = Assert.IsType<LocalWorksiteView>(view.Worksite);
        LocalExcavationView target = Assert.Single(worksite.ExcavationTargets);
        ExcavationSite site = state.Excavation!.Sites.Single();
        Assert.Equal(site.SiteId, target.SiteId);
        Assert.Equal(site.Cell, target.Cell);
        Assert.Equal(site.Stance, target.Stance);
        Assert.Equal(site.Revision, target.SiteRevision);
        Assert.Equal(state.Space.Regions.Single(region => region.Bounds.Contains(site.Cell)).Revision, target.TerrainRevision);
        Assert.Equal(site.RequiredTool.DefinitionId, target.RequiredToolDefinitionId);
        Assert.Equal(site.RequiredWork, target.RequiredWork);
        Assert.Equal(site.CompletedWork, target.CompletedWork);
        Assert.Equal(site.RequiredWork - site.CompletedWork, target.RemainingWork);
        Assert.False(target.IsComplete);
        Assert.Equal(view.PlayerInventory.Revision, target.DestinationRevision);
        LocalExcavationToolView tool = Assert.Single(worksite.Tools);
        CountedSourceLot actualTool = state.Inventory.Custody.GetLot(fixture.Tool);
        Assert.Equal(actualTool.LotId, tool.LotId);
        Assert.Equal(actualTool.Definition.DefinitionId, tool.DefinitionId);
        Assert.Equal(actualTool.Revision, tool.Revision);
        Assert.Equal(0U, worksite.PlayerEffort);
        Assert.Equal(GatherRulesV1.EffortPerSwing, worksite.EffortPerWork);
        Assert.True(worksite.CanManipulate);
        Assert.False(worksite.IsResolvingConsequences);
        AssertTerrain(state, worksite);
        AssertPrivateStateIsAbsent(state, worksite);

        LocalWorksiteView otherActor = Assert.IsType<LocalWorksiteView>(new LocalWorldView(state, fixture.Builder).Worksite);
        Assert.Empty(otherActor.ExcavationTargets);
        Assert.Empty(otherActor.Tools);
        Assert.Throws<NotSupportedException>(() => ((IList<LocalExcavationView>)worksite.ExcavationTargets).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<bool>)worksite.Terrain[0].Occupancy)[0] = false);
        CellCoordinate outside = worksite.Terrain[0].Bounds.Maximum.Offset(1, 0, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => worksite.Terrain[0].IsSolid(outside));
    }

    [Theory]
    [InlineData(LotProtection.None, false, true)]
    [InlineData(LotProtection.Equipped | LotProtection.Favorite, false, true)]
    [InlineData(LotProtection.UserReserved, false, false)]
    [InlineData(LotProtection.None, true, false)]
    public void Tool_projection_matches_excavation_reservation_rules(LotProtection protection, bool reserved, bool visible)
    {
        var fixture = new ConstructedSupportWorldFixture();
        WorldSnapshot original = WorldConstructionPhysicalAdmission.CreateGenesis(
            WorldSteppedSupportAdmission.CreateGenesis(fixture.Genesis));
        CountedSourceLot tool = original.Inventory.Custody.GetLot(fixture.Tool);
        EntityId? reservation = reserved ? original.Construction!.Projects.Single().ReservationId : null;
        var custody = new CustodySnapshot(original.WorldId, original.BranchId, original.Inventory.Custody.Revision,
            original.Inventory.Custody.Storages, original.Inventory.Custody.Lots.Select(lot => lot.LotId == tool.LotId
                ? new CountedSourceLot(lot.LotId, lot.CustodianId, lot.Definition, lot.Source, lot.Revision, protection, reservation) : lot));
        // This is fresh admitted test content; no played tool or actor is relocated or rewritten.
        var inventory = new CountedAuthorityState(custody, original.Inventory.AdmittedUnitCapacity,
            original.Inventory.Actors, original.Inventory.Controls, original.Inventory.Policies,
            original.Inventory.Sources, original.Inventory.ReservationIds);
        var state = new WorldSnapshot(inventory, original.Space, original.Residents,
            original.AdmissionVersion, original.Calendar, 0, 0, gathering: original.Gathering,
            production: original.Production, progression: original.Progression, quests: original.Quests,
            construction: original.Construction, excavation: original.Excavation, constructionPhysical: original.ConstructionPhysical);
        LocalWorksiteView worksite = Assert.IsType<LocalWorksiteView>(new LocalWorldView(state, fixture.Player).Worksite);
        if (visible)
        {
            Assert.Contains(worksite.Tools, value => value.LotId == fixture.Tool);
        }
        else
        {
            Assert.DoesNotContain(worksite.Tools, value => value.LotId == fixture.Tool);
        }
        AssertPrivateStateIsAbsent(state, worksite);
    }

    [Fact]
    public void Manipulation_availability_comes_from_the_retained_body_condition()
    {
        var fixture = new ConstructedSupportWorldFixture();
        WorldSnapshot original = WorldConstructionPhysicalAdmission.CreateGenesis(
            WorldSteppedSupportAdmission.CreateGenesis(fixture.Genesis));
        ActorPresence player = original.Space.GetActor(fixture.Player);
        var injuries = player.Condition.FunctionalSlots(BodyFunction.Manipulation).Select((slot, index) =>
            new BodyInjury(new(0x776f726b73697465, checked((ulong)index + 1)), new(0x776f726b73697465, 100),
                slot, BodyInjuryKind.FunctionLoss, BodyFunction.Manipulation));
        var condition = new BodyConditionSnapshot(player.Condition.FormInstanceId, player.Condition.Definition,
            player.Condition.Injuries.Concat(injuries));
        Assert.False(condition.SupportsFunction(BodyFunction.Manipulation));
        var space = new LocalSpatialSnapshot(original.Space.Scope, original.Space.Regions,
            original.Space.Actors.Select(actor => actor.ActorId == fixture.Player
                ? new ActorPresence(actor.Binding, condition, actor.Scope, actor.Feet, actor.Revision, actor.HomePlaceId) : actor),
            original.Space.Structures, original.Space.GroundSupport, original.Space.StructureSupport);
        // Supplied fresh condition metadata tests projection, not an implemented injury transition.
        var state = new WorldSnapshot(original.Inventory, space, original.Residents,
            original.AdmissionVersion, original.Calendar, 0, 0, gathering: original.Gathering,
            production: original.Production, progression: original.Progression, quests: original.Quests,
            construction: original.Construction, excavation: original.Excavation, constructionPhysical: original.ConstructionPhysical);
        LocalWorksiteView worksite = Assert.IsType<LocalWorksiteView>(new LocalWorldView(state, fixture.Player).Worksite);
        Assert.False(worksite.CanManipulate);
        Assert.Single(worksite.Tools);
    }

    [Fact]
    public void Funded_installed_geometry_stays_committed_through_pending_work_and_changes_only_at_publication()
    {
        var fixture = new CoupledSettlementWorldFixture(consequenceWorkBudget: 1, eventOrdered: true);
        using var files = new SessionFiles();
        WorldSnapshot before = fixture.PrepareBeforeExcavation(files.Path);
        var originalView = new LocalWorldView(before, fixture.Player);
        LocalWorksiteView original = Assert.IsType<LocalWorksiteView>(originalView.Worksite);
        Assert.Equal(3, original.Members.Count);
        Assert.All(original.Members, member => Assert.Equal(ConstructionMemberDisposition.Standing, member.Disposition));
        AssertMembers(before, original);
        AssertTerrain(before, original);
        AssertPrivateStateIsAbsent(before, original);

        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path);
        WorldSnapshot state = authority.Commit(fixture.NextExcavation(before)).Candidate;
        Assert.NotNull(state.PendingExcavation);
        ulong maximum = state.PendingExcavation.MaximumContinuationCount;
        for (ulong count = 0; state.PendingExcavation is not null; count++)
        {
            Assert.True(count < maximum);
            var pendingView = new LocalWorldView(state, fixture.Player);
            LocalWorksiteView pending = Assert.IsType<LocalWorksiteView>(pendingView.Worksite);
            Assert.True(pendingView.IsResolvingConsequences);
            Assert.True(pending.IsResolvingConsequences);
            Assert.Equal(originalView.Calendar, pendingView.Calendar);
            Assert.Equal(original.PlayerEffort, pending.PlayerEffort);
            Assert.Equal(original.Members, pending.Members);
            Assert.Equal(original.ExcavationTargets, pending.ExcavationTargets);
            Assert.Equal(original.Tools, pending.Tools);
            AssertTerrain(before, pending);
            AssertPrivateStateIsAbsent(state, pending);
            state = authority.Commit(fixture.Continue(state)).Candidate;
        }

        var finalView = new LocalWorldView(state, fixture.Player);
        LocalWorksiteView final = Assert.IsType<LocalWorksiteView>(finalView.Worksite);
        Assert.False(finalView.IsResolvingConsequences);
        Assert.False(final.IsResolvingConsequences);
        AssertMembers(state, final);
        AssertTerrain(state, final);
        AssertPrivateStateIsAbsent(state, final);
        Assert.All(final.Members, member =>
        {
            LocalWorksiteMemberView previous = Assert.Single(original.Members,
                value => value.StructureId == member.StructureId && value.MemberIndex == member.MemberIndex);
            Assert.Equal(previous.Origin.Offset(0, 0, -256), member.Origin);
            Assert.Equal(previous.Revision + 1, member.Revision);
            Assert.Equal(previous.ConditionParts, member.ConditionParts);
            Assert.Equal(previous.Geometry, member.Geometry);
            Assert.Equal(ConstructionMemberDisposition.Resting, member.Disposition);
        });
        LocalExcavationView finished = Assert.Single(final.ExcavationTargets);
        LocalExcavationView initial = Assert.Single(original.ExcavationTargets);
        Assert.True(finished.IsComplete);
        Assert.Equal(0UL, finished.RemainingWork);
        Assert.Equal(initial.SiteRevision + 1, finished.SiteRevision);
        Assert.Equal(initial.TerrainRevision + 1, finished.TerrainRevision);
        Assert.Equal(initial.DestinationRevision + 1, finished.DestinationRevision);
        Assert.False(final.Terrain.Single(region => region.Bounds.Contains(finished.Cell)).IsSolid(finished.Cell));
        Assert.True(original.Terrain.Single(region => region.Bounds.Contains(initial.Cell)).IsSolid(initial.Cell));
        Assert.False(original.IsResolvingConsequences);
        Assert.False(initial.IsComplete);
        Assert.Equal(Math.Min(GatherRulesV1.MaximumEffort, original.PlayerEffort + GatherRulesV1.EffortPerAdvancingStep)
            - GatherRulesV1.EffortPerSwing, final.PlayerEffort);
    }

    private static void AssertTerrain(WorldSnapshot state, LocalWorksiteView worksite)
    {
        Assert.Equal(state.Space.Regions.Count, worksite.Terrain.Count);
        foreach (TerrainRegionSnapshot expected in state.Space.Regions)
        {
            LocalWorksiteTerrainView actual = Assert.Single(worksite.Terrain, region => region.Bounds == expected.Bounds);
            Assert.Equal(expected.Revision, actual.Revision);
            Assert.Equal(expected.Bounds.CellCount, actual.Occupancy.Count);
            Assert.Equal(expected.Cells.Select(cell => cell.IsSolid), actual.Occupancy);
        }
    }

    private static void AssertMembers(WorldSnapshot state, LocalWorksiteView worksite)
    {
        Assert.Equal(state.ConstructionPhysical!.Members.Count, worksite.Members.Count);
        foreach (LocalWorksiteMemberView member in worksite.Members)
        {
            var physical = state.ConstructionPhysical.GetMember(member.StructureId, member.MemberIndex);
            var project = Assert.Single(state.Construction!.Projects, value => value.Installed?.StructureId == member.StructureId);
            SubunitPosition origin = project.SiteFeet.Offset(physical.Displacement.X, physical.Displacement.Y, physical.Displacement.Z);
            Assert.Equal(origin, member.Origin);
            Assert.Equal(physical.Revision, member.Revision);
            Assert.Equal(physical.ConditionParts, member.ConditionParts);
            Assert.Equal(physical.Disposition, member.Disposition);
            Assert.Equal(project.Definition.Members[(int)member.MemberIndex], member.Geometry);
            Assert.Equal(member.Geometry.At(origin), member.Bounds);
            Assert.Contains(member.Bounds, state.Space.Structures.Single(value => value.StructureId == member.StructureId).Boxes);
        }
    }

    private static void AssertPrivateStateIsAbsent(WorldSnapshot state, LocalWorksiteView worksite)
    {
        string json = JsonSerializer.Serialize(worksite);
        IEnumerable<EntityId> hidden = state.Excavation!.Sites.SelectMany(site => new[] { site.TerrainSourceId, site.CountedSourceId })
            .Concat(state.Inventory.Sources.Select(source => source.SourceId))
            .Concat(state.Inventory.Custody.Lots.Where(lot => worksite.Tools.All(tool => tool.LotId != lot.LotId)).Select(lot => lot.LotId))
            .Concat(state.Production!.Lots.Select(lot => lot.LotId))
            .Concat(state.Construction!.Projects.Where(project => project.Installed is not null).Select(project => project.Installed!.GuardLotId));
        foreach (EntityId identity in hidden)
        {
            Assert.DoesNotContain(JsonSerializer.Serialize(identity), json, StringComparison.Ordinal);
        }
        foreach (string forbidden in new[] { "Source", "Claim", "Custodian", "Inventory", "Command", "Receipt", "Journal", "BasisHash", "InputPin" })
        {
            Assert.DoesNotContain(forbidden, json, StringComparison.Ordinal);
        }
    }
}
