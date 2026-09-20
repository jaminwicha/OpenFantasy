using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Tests.Spatial;
using static OpenFantasy.Core.Tests.Construction.ConstructionFixture;

namespace OpenFantasy.Core.Tests.Construction;

public sealed class ConstructionPhysicalTests
{
    [Fact]
    public void Genesis_covers_every_installed_member_once_and_keeps_exact_material_backing()
    {
        ConstructionSnapshot construction = Snapshot(Project(1, 200), Project(2, 40));
        ConstructionPhysicalSnapshot physical = ConstructionPhysicalSnapshot.Create(construction);
        Assert.Same(construction, physical.Construction);
        Assert.Equal(construction.ContentHash, physical.ConstructionHash);
        Assert.Equal(construction.WorldId, physical.WorldId);
        Assert.Equal(construction.BranchId, physical.BranchId);
        Assert.Equal(0UL, physical.Revision);
        Assert.Equal(2, physical.Members.Count);
        Assert.Single(physical.GetStructures());
        ConstructionProject completed = construction.Projects.Single(value => value.Installed is not null);
        ulong mass = 0;
        ulong volume = 0;
        foreach (ConstructionMemberState state in physical.Members)
        {
            Assert.Equal(0UL, state.Revision);
            Assert.Equal(10_000U, state.ConditionParts);
            Assert.Equal(default, state.Displacement);
            Assert.Equal(ConstructionMemberDisposition.Standing, state.Disposition);
            Assert.Equal(ConstructionBearingModel.MemberIdentity(state.StructureId, state.MemberIndex), state.ElementId);
            StructuralMemberReference reference = physical.GetMemberReference(state.StructureId, state.MemberIndex);
            Assert.Equal(completed.ProjectId, reference.ProjectId);
            Assert.Equal(completed.Installed!.GuardLotId, reference.InstalledLotId);
            Assert.Equal(state.ElementId, reference.ElementId);
            Assert.Equal(completed.Definition.Members[(int)state.MemberIndex].VolumeMicrolitres, reference.Volume.Value);
            mass += reference.Mass.Value;
            volume += reference.Volume.Value;
        }
        Assert.Equal(270_000UL, mass);
        Assert.Equal(540_000UL, volume);
        Assert.Equal(construction.GetStructures(true).Single().Boxes, physical.GetStructures().Single().Boxes);
        Assert.Equal(construction.GetStructures(true).Single().BearingSurfaces, physical.GetStructures().Single().BearingSurfaces);
    }

    [Fact]
    public void Completeness_rejects_missing_duplicate_unknown_and_unbuilt_members()
    {
        ConstructionSnapshot construction = Snapshot(Project(1, 200));
        ConstructionPhysicalSnapshot complete = ConstructionPhysicalSnapshot.Create(construction);
        ConstructionMemberState first = complete.Members[0];
        Assert.Throws<ArgumentException>(() => new ConstructionPhysicalSnapshot(construction, 0, complete.Members.Take(1)));
        Assert.Throws<ArgumentException>(() => new ConstructionPhysicalSnapshot(construction, 0, complete.Members.Append(first)));
        Assert.Throws<ArgumentException>(() => new ConstructionPhysicalSnapshot(construction, 0,
            complete.Members.Append(new(first.StructureId, 2, 0, 10_000, default, ConstructionMemberDisposition.Standing))));
        Assert.Throws<ArgumentException>(() => new ConstructionPhysicalSnapshot(construction, 0,
            [new(Id(999), 0, 0, 10_000, default, ConstructionMemberDisposition.Standing), complete.Members[1]]));
        ConstructionSnapshot unfinished = Snapshot(Project(1, 199));
        Assert.Empty(ConstructionPhysicalSnapshot.Create(unfinished).Members);
        Assert.Throws<ArgumentException>(() => new ConstructionPhysicalSnapshot(unfinished, 0, complete.Members));
        Assert.Throws<ArgumentException>(() => complete.GetMember(first.StructureId, 2));
        Assert.Throws<ArgumentException>(() => complete.GetMemberReference(Id(999), 0));
    }

    [Fact]
    public void Member_transitions_are_immutable_revision_checked_and_cannot_hide_noops()
    {
        var pristine = new ConstructionMemberState(Id(900), 3, 0, 10_000, default, ConstructionMemberDisposition.Standing);
        ConstructionMemberState fallen = pristine.WithState(0, 5000, new(0, 0, -64), ConstructionMemberDisposition.Falling);
        Assert.Equal(pristine.ElementId, fallen.ElementId);
        Assert.Equal(0UL, pristine.Revision);
        Assert.Equal(default, pristine.Displacement);
        Assert.Equal(1UL, fallen.Revision);
        Assert.Equal(5000U, fallen.ConditionParts);
        Assert.Throws<ArgumentException>(() => pristine.WithState(1, 5000, default, ConstructionMemberDisposition.Standing));
        Assert.Throws<ArgumentException>(() => pristine.WithState(0, 10_000, default, ConstructionMemberDisposition.Standing));
        Assert.Throws<ArgumentException>(() => pristine.WithState(0, 10_001, default, ConstructionMemberDisposition.Standing));
        Assert.Throws<ArgumentException>(() => pristine.WithState(0, 9000, new(1, 0, 0), ConstructionMemberDisposition.Standing));
        Assert.Throws<ArgumentException>(() => pristine.WithState(0, 9000, default, (ConstructionMemberDisposition)0));
        Assert.Throws<ArgumentException>(() => new ConstructionMemberState(Id(900), 0, 0, 9999, default, ConstructionMemberDisposition.Standing));
        Assert.Throws<ArgumentException>(() => new ConstructionMemberState(Id(900), 0, 0, 10_000, default, ConstructionMemberDisposition.Resting));
        var exhausted = new ConstructionMemberState(Id(900), 0, ulong.MaxValue, 10_000, default, ConstructionMemberDisposition.Standing);
        Assert.Throws<OverflowException>(() => exhausted.WithState(ulong.MaxValue, 9000, default, ConstructionMemberDisposition.Standing));
    }

    [Fact]
    public void Failed_and_falling_members_keep_collision_and_original_mass_but_supply_no_footing()
    {
        ConstructionPhysicalSnapshot initial = ConstructionPhysicalSnapshot.Create(Snapshot(Project(1, 200)));
        ConstructionMemberState first = initial.Members[0];
        ConstructionMemberState second = initial.Members[1];
        ConstructionPhysicalSnapshot changed = initial.WithMembers(0,
            [first.WithState(0, 9000, new(-32, 0, -64), ConstructionMemberDisposition.Falling),
             second.WithState(0, 0, default, ConstructionMemberDisposition.Standing)]);
        Assert.Equal(1UL, changed.Revision);
        Assert.All(changed.Members, member => Assert.Equal(1UL, member.Revision));
        SpatialStructure geometry = Assert.Single(changed.GetStructures());
        Assert.Equal(2, geometry.Boxes.Count);
        Assert.Empty(geometry.BearingSurfaces);
        Assert.Equal(2, initial.GetStructures().Single().BearingSurfaces.Count);
        Assert.Equal(initial.ConstructionHash, changed.ConstructionHash);
        Assert.NotEqual(initial.ContentHash, changed.ContentHash);
        Assert.All(initial.Members, state => Assert.Equal(
            initial.GetMemberReference(state.StructureId, state.MemberIndex),
            changed.GetMemberReference(state.StructureId, state.MemberIndex)));

        ConstructionMemberState falling = changed.GetMember(first.StructureId, first.MemberIndex);
        ConstructionPhysicalSnapshot resting = changed.WithMember(1,
            falling.WithState(1, 9000, falling.Displacement, ConstructionMemberDisposition.Resting));
        Assert.Equal(2, resting.GetStructures().Single().Boxes.Count);
        Assert.Single(resting.GetStructures().Single().BearingSurfaces);
        Assert.Equal(geometry.Boxes, resting.GetStructures().Single().Boxes);
        Assert.Equal(ConstructionMemberDisposition.Falling, falling.Disposition);
    }

    [Fact]
    public void Batch_updates_validate_final_geometry_once_and_do_not_serialize_atomic_member_changes()
    {
        ConstructionPhysicalSnapshot initial = ConstructionPhysicalSnapshot.Create(Snapshot(Project(1, 200)));
        ConstructionMemberState[] translated = initial.Members.Select(member => member.WithState(0, 10_000,
            new(8, 0, 0), ConstructionMemberDisposition.Falling)).ToArray();
        // Moving only the first 30 mm block by 31.25 mm penetrates the original second block.
        Assert.Throws<ArgumentException>(() => initial.WithMember(0, translated[0]));
        ConstructionPhysicalSnapshot together = initial.WithMembers(0, translated);
        Assert.Equal(1UL, together.Revision);
        Assert.All(together.Members, member => Assert.Equal(1UL, member.Revision));
        Assert.Equal(2, together.GetStructures().Single().Boxes.Count);
        Assert.Equal(0UL, initial.Revision);
        Assert.All(initial.Members, member => Assert.Equal(default, member.Displacement));
    }

    [Fact]
    public void Owner_updates_require_unique_changed_members_and_exact_revisions()
    {
        ConstructionPhysicalSnapshot initial = ConstructionPhysicalSnapshot.Create(Snapshot(Project(1, 200)));
        ConstructionMemberState first = initial.Members[0];
        ConstructionMemberState changed = first.WithState(0, 9000, default, ConstructionMemberDisposition.Standing);
        Assert.Throws<ArgumentException>(() => initial.WithMember(1, changed));
        Assert.Throws<ArgumentException>(() => initial.WithMember(0, first));
        Assert.Throws<ArgumentException>(() => initial.WithMembers(0, []));
        Assert.Throws<ArgumentException>(() => initial.WithMembers(0, [changed, changed]));
        Assert.Throws<ArgumentException>(() => initial.WithMember(0,
            new(first.StructureId, first.MemberIndex, 1, 10_000, default, ConstructionMemberDisposition.Standing)));
        Assert.Throws<ArgumentException>(() => initial.WithMember(0,
            new(first.StructureId, first.MemberIndex, 2, 9000, default, ConstructionMemberDisposition.Standing)));
        Assert.Throws<ArgumentException>(() => new ConstructionPhysicalSnapshot(initial.Construction, 0,
            [changed, initial.Members[1]]));
        var source = initial.Members.Reverse().ToList();
        var copied = new ConstructionPhysicalSnapshot(initial.Construction, 0, source);
        source.Clear();
        Assert.Equal(initial.ContentHash, copied.ContentHash);
        Assert.Equal(2, copied.Members.Count);
    }

    [Fact]
    public void Exact_material_intersection_is_rejected_but_rounded_adjacent_envelopes_remain_a_union()
    {
        ConstructionPhysicalSnapshot initial = ConstructionPhysicalSnapshot.Create(Snapshot(Project(1, 200)));
        SpatialStructure geometry = initial.GetStructures().Single();
        Assert.True(geometry.Boxes[0].Maximum.X > geometry.Boxes[1].Minimum.X);
        ConstructionMemberState first = initial.Members[0];
        Assert.Throws<ArgumentException>(() => initial.WithMember(0,
            first.WithState(0, 9000, new(1, 0, 0), ConstructionMemberDisposition.Resting)));
        ConstructionPhysicalSnapshot apart = initial.WithMember(0,
            first.WithState(0, 9000, new(-1, 0, 0), ConstructionMemberDisposition.Resting));
        Assert.Equal(2, apart.GetStructures().Single().Boxes.Count);
        Assert.Equal(2, apart.GetStructures().Single().BearingSurfaces.Count);
    }

    [Fact]
    public void Projection_refuses_numeric_overflow_and_existing_span_limit_without_losing_members()
    {
        ConstructionPhysicalSnapshot initial = ConstructionPhysicalSnapshot.Create(Snapshot(Project(1, 200)));
        ConstructionMemberState first = initial.Members[0];
        Assert.Throws<ArgumentException>(() => initial.WithMember(0,
            first.WithState(0, 9000, new(-2048, 0, 0), ConstructionMemberDisposition.Falling)));
        Assert.Throws<OverflowException>(() => initial.WithMember(0,
            first.WithState(0, 9000, new(long.MaxValue, 0, 0), ConstructionMemberDisposition.Falling)));
        Assert.Equal(2, initial.Members.Count);
        Assert.Equal(2, initial.GetStructures().Single().Boxes.Count);
    }

    [Fact]
    public void Reconciliation_keeps_moved_members_rebinds_work_progress_and_initializes_only_new_completions()
    {
        ConstructionSnapshot basis = Snapshot(Project(1, 200), Project(2, 0));
        ConstructionPhysicalSnapshot initial = ConstructionPhysicalSnapshot.Create(basis);
        ConstructionMemberState first = initial.Members[0];
        ConstructionPhysicalSnapshot moved = initial.WithMember(0,
            first.WithState(0, 8000, new(-32, 0, 0), ConstructionMemberDisposition.Resting));
        ConstructionSnapshot worked = Snapshot(Project(1, 200), Project(2, 40));
        ConstructionPhysicalSnapshot rebound = moved.ReconcileCompletions(worked);
        Assert.Equal(1UL, rebound.Revision);
        Assert.Equal(worked.ContentHash, rebound.ConstructionHash);
        Assert.NotEqual(moved.ContentHash, rebound.ContentHash);
        Assert.Same(moved.GetMember(first.StructureId, first.MemberIndex), rebound.GetMember(first.StructureId, first.MemberIndex));
        Assert.Same(rebound, rebound.ReconcileCompletions(worked));

        ConstructionSnapshot finished = Snapshot(Project(1, 200), Project(2, 200));
        ConstructionPhysicalSnapshot reconciled = rebound.ReconcileCompletions(finished);
        Assert.Equal(2UL, reconciled.Revision);
        Assert.Equal(4, reconciled.Members.Count);
        Assert.Equal(2, reconciled.GetStructures().Count);
        Assert.Same(rebound.GetMember(first.StructureId, first.MemberIndex), reconciled.GetMember(first.StructureId, first.MemberIndex));
        EntityId newlyInstalled = finished.Projects.Single(project => project.ProjectId == Id(302)).Installed!.StructureId;
        Assert.All(reconciled.Members.Where(member => member.StructureId == newlyInstalled), member =>
        {
            Assert.Equal(0UL, member.Revision);
            Assert.Equal(10_000U, member.ConditionParts);
            Assert.Equal(ConstructionMemberDisposition.Standing, member.Disposition);
        });
        Assert.Equal(540_000UL, reconciled.Members.Aggregate(0UL, (sum, member) => sum
            + reconciled.GetMemberReference(member.StructureId, member.MemberIndex).Mass.Value));
    }

    [Fact]
    public void Reconciliation_rejects_scope_and_installed_definition_site_or_source_substitution()
    {
        ConstructionProject project = Project(1, 200);
        ConstructionPhysicalSnapshot physical = ConstructionPhysicalSnapshot.Create(Snapshot(project));
        Assert.Throws<ArgumentException>(() => physical.ReconcileCompletions(
            new ConstructionSnapshot(Id(9), physical.BranchId, 200, [project.Definition], [project])));
        Assert.Throws<ArgumentException>(() => physical.ReconcileCompletions(Snapshot(Project(1, 199))));
        Assert.Throws<ArgumentException>(() => physical.ReconcileCompletions(Snapshot(Project(2, 200))));
        Assert.Throws<ArgumentException>(() => physical.ReconcileCompletions(Snapshot(Project(1, 200, site: new(4096, 0, 256)))));
        ConstructionDefinition definition = project.Definition;
        var changedDefinition = new ConstructionDefinition(definition.DefinitionId, definition.Key, "Changed retained definition",
            definition.MaterialTransform, definition.InstalledOutputIndex, definition.RequiredTool, definition.Members,
            definition.RequiredWork, definition.WorkRate);
        Assert.Throws<ArgumentException>(() => physical.ReconcileCompletions(Snapshot(Project(1, 200, definition: changedDefinition))));
        Assert.Throws<ArgumentException>(() => physical.ReconcileCompletions(Snapshot(Project(1, 200,
            installed: project.Installed! with { StructureId = Id(999) }))));
        EntityId otherCause = Id(998);
        var otherSource = new InstalledStructure(project.Installed!.StructureId, EntityId.DeriveSpawned(otherCause, 1),
            0, EntityId.DeriveSpawned(otherCause, 0), otherCause);
        Assert.Throws<ArgumentException>(() => physical.ReconcileCompletions(Snapshot(Project(1, 200, installed: otherSource))));
    }

    [Fact]
    public void Distinct_projects_cannot_duplicate_the_same_material_backing_or_interpenetrate_exactly()
    {
        ConstructionProject first = Project(1, 200);
        ConstructionProject second = Project(2, 200, installed: first.Installed! with { StructureId = Id(999) });
        Assert.Throws<ArgumentException>(() => ConstructionPhysicalSnapshot.Create(Snapshot(first, second)));
        Assert.Throws<ArgumentException>(() => ConstructionPhysicalSnapshot.Create(
            Snapshot(first, Project(2, 200, site: first.SiteFeet))));
    }

    [Fact]
    public void Reconciliation_pins_unfinished_projects_and_cannot_hide_work_rewind_in_another_projects_progress()
    {
        ConstructionSnapshot construction = Snapshot(Project(1, 40), Project(2, 0));
        ConstructionPhysicalSnapshot physical = ConstructionPhysicalSnapshot.Create(construction);
        Assert.Empty(physical.Members);
        Assert.Throws<ArgumentException>(() => physical.ReconcileCompletions(Snapshot(Project(1, 20), Project(2, 40))));
        Assert.Throws<ArgumentException>(() => physical.ReconcileCompletions(Snapshot(Project(1, 40))));
        Assert.Throws<ArgumentException>(() => physical.ReconcileCompletions(Snapshot(Project(1, 40), Project(2, 0), Project(3, 0))));
        Assert.Throws<ArgumentException>(() => physical.ReconcileCompletions(Snapshot(Project(1, 40), Project(3, 0))));
        Assert.Throws<ArgumentException>(() => physical.ReconcileCompletions(
            Snapshot(Project(1, 40, site: new(2048, 256, 256)), Project(2, 0))));
        ConstructionProject original = construction.Projects[0];
        var changedAccess = new ConstructionProject(original.ProjectId, original.Definition, original.OwnerActorId,
            original.BuilderActorId, original.QuestId, original.ProjectCustodianId, original.ReservationId,
            original.SiteFeet, original.WorkFeet,
            [new(original.BuilderActorId, original.WorkFeet, original.WorkFeet.Offset(512, 0, 0))],
            original.Revision, original.Progress);
        Assert.Throws<ArgumentException>(() => physical.ReconcileCompletions(Snapshot(changedAccess, Project(2, 0))));
        ConstructionPhysicalSnapshot worked = physical.ReconcileCompletions(Snapshot(Project(1, 41), Project(2, 0)));
        Assert.Equal(0UL, worked.Revision);
        Assert.Empty(worked.Members);
        Assert.NotEqual(physical.ConstructionHash, worked.ConstructionHash);
    }

    [Fact]
    public void Detached_resting_member_cannot_inherit_the_original_assemblys_lower_ground_contact()
    {
        ConstructionPhysicalSnapshot initial = ConstructionPhysicalSnapshot.Create(Snapshot(Project(1, 200, definition: StackedAssembly())));
        ConstructionMemberState upper = initial.Members[1];
        ConstructionPhysicalSnapshot floating = initial.WithMember(0,
            upper.WithState(0, 9000, new(0, 0, 64), ConstructionMemberDisposition.Resting));
        // The older assembly-level check admits the other member's base and only tests clearance above it.
        LocalSpatialSnapshot space = PhysicalSpace(floating);
        Assert.Equal(2, space.Structures.Single().Boxes.Count);
        Assert.Throws<ArgumentException>(() => floating.ValidateStableGeometry(space));
        initial.ValidateStableGeometry(PhysicalSpace(initial));
    }

    [Fact]
    public void Stable_ground_contact_retains_existing_outward_quantization_tolerance()
    {
        ConstructionPhysicalSnapshot initial = ConstructionPhysicalSnapshot.Create(Snapshot(Project(1, 200, definition: StackedAssembly())));
        ConstructionMemberState upper = initial.Members[1];
        // The upper member's 300 mm base minus 76 subunits is 3.125 mm above terrain:
        // less than one 3.90625 mm subunit, so its conservative base remains on the floor plane.
        ConstructionPhysicalSnapshot lowered = initial.WithMember(0,
            upper.WithState(0, 9000, new(32, 0, -76), ConstructionMemberDisposition.Resting));
        lowered.ValidateStableGeometry(PhysicalSpace(lowered));
        Assert.All(lowered.GetStructures().Single().Boxes, box => Assert.Equal(256L, box.Minimum.Z));
    }

    [Fact]
    public void Falling_archive_state_is_rejected_by_stable_world_contact_even_when_currently_grounded()
    {
        ConstructionPhysicalSnapshot initial = ConstructionPhysicalSnapshot.Create(Snapshot(Project(1, 200)));
        ConstructionMemberState first = initial.Members[0];
        ConstructionPhysicalSnapshot falling = initial.WithMember(0,
            first.WithState(0, 9000, default, ConstructionMemberDisposition.Falling));
        Assert.Equal(2, falling.GetStructures().Single().Boxes.Count);
        Assert.Throws<ArgumentException>(() => falling.ValidateStableGeometry(PhysicalSpace(falling)));
    }

    [Fact]
    public void Separately_grounded_detached_components_retain_valid_contact_and_all_material()
    {
        ConstructionPhysicalSnapshot initial = ConstructionPhysicalSnapshot.Create(Snapshot(Project(1, 200)));
        ConstructionMemberState first = initial.Members[0];
        ConstructionPhysicalSnapshot separated = initial.WithMember(0,
            first.WithState(0, 9000, new(-32, 0, 0), ConstructionMemberDisposition.Resting));
        separated.ValidateStableGeometry(PhysicalSpace(separated));
        Assert.Equal(2, separated.GetStructures().Single().Boxes.Count);
        Assert.Equal(2, separated.GetStructures().Single().BearingSurfaces.Count);
        Assert.Equal(initial.GetMemberReference(first.StructureId, first.MemberIndex),
            separated.GetMemberReference(first.StructureId, first.MemberIndex));
    }

    private static LocalSpatialSnapshot PhysicalSpace(ConstructionPhysicalSnapshot physical)
    {
        var scope = new TerrainScope(physical.WorldId, physical.BranchId, Id(9999));
        return new(scope, [SpatialFixture.Terrain(scope: scope)], [], physical.GetStructures(),
            GroundSupportMode.SteppedTerrainAndConstruction);
    }

    private static ConstructionDefinition SmallAssembly() => Definition(
        [new(0, 0, 0, 30, 30, 300), new(30, 0, 0, 60, 30, 300)]);

    private static ConstructionDefinition StackedAssembly() => Definition(
        [new(0, 0, 0, 30, 30, 300), new(0, 0, 300, 30, 30, 600)]);

    private static ConstructionProject Project(ulong ordinal, ulong work, SubunitPosition? site = null,
        ConstructionDefinition? definition = null, InstalledStructure? installed = null)
    {
        definition ??= SmallAssembly();
        EntityId projectId = Id(300 + ordinal);
        EntityId builder = Id(200 + ordinal);
        SubunitPosition origin = site ?? new(checked((long)ordinal * 2048), 0, 256);
        SubunitPosition workFeet = origin.Offset(0, -256, 0);
        var progress = ExposureProgress.Begin(projectId, definition.WorkRate)
            .Advance(LocalExposureDuration.FromMilliseconds(work * 1800));
        if (work == definition.RequiredWork.Value)
        {
            EntityId cause = Id(700 + ordinal);
            installed ??= new(EntityId.DeriveSpawned(cause, 3), EntityId.DeriveSpawned(cause, 1),
                0, EntityId.DeriveSpawned(cause, 0), cause);
        }
        return new(projectId, definition, builder, builder, Id(400 + ordinal), Id(500 + ordinal), Id(600 + ordinal),
            origin, workFeet, [new(builder, workFeet, workFeet.Offset(256, 0, 0))], work, progress.Capture(), installed);
    }

    private static ConstructionSnapshot Snapshot(params ConstructionProject[] projects) => new(Id(1), Id(3),
        projects.Aggregate(0UL, (sum, project) => sum + project.Revision),
        projects.Select(project => project.Definition).DistinctBy(definition => definition.DefinitionId), projects);
}
