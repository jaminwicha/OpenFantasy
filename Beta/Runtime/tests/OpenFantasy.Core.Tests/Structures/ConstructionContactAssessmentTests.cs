using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Structures;

/// <summary>Candidate endpoint/load proofs, not a live fall, impact calculation or world publication.</summary>
public sealed class ConstructionContactAssessmentTests
{
    [Fact]
    public void Funded_separate_installed_outputs_transfer_exact_member_and_carried_loads_through_a_foreign_assembly()
    {
        ConstructionContactFixture fixture = ConstructionContactFixture.Create();
        WorldSnapshot basis = fixture.Basis;
        ContentHash originalHash = basis.ContentHash;
        ConstructionContactScene scene = fixture.Stack();
        ConstructionBearingModel model = fixture.Policy.AssessContacts(scene);
        StructuralLoadResolution result = Finish(model);

        Assert.Equal((ushort)14, basis.ProfileVersion);
        Assert.Empty(result.Failures);
        Assert.Equal(basis.ContentHash, model.WorldHash);
        Assert.Equal(scene.InputPin, model.CandidateScenePin);
        Assert.Equal(fixture.Policy.ContentHash, model.PolicyPin);
        Assert.Equal(4, model.Members.Count);
        Assert.Equal(2, model.Members.Select(member => member.InstalledLotId).Distinct().Count());
        Assert.Equal(2, basis.Production!.Batches.Count);
        Assert.All(basis.Production.Batches, batch => Assert.Equal(6UL, Assert.Single(batch.Ingredients).Quantity));
        Assert.All(model.Members, member => Assert.Equal(135_000UL, member.Mass.Value));
        Assert.Equal(540_000UL, model.Members.Aggregate(0UL, (sum, member) => sum + member.Mass.Value));
        foreach (StructuralMemberReference member in model.Members)
        {
            Assert.Equal(ConstructionBearingModel.MemberIdentity(member.StructureId, member.MemberIndex), member.ElementId);
            Assert.Equal(basis.ConstructionPhysical!.GetMemberReference(member.StructureId, member.MemberIndex), member);
            Assert.Equal(1UL, basis.Production.Lots.Single(lot => lot.LotId == member.InstalledLotId).Quantity);
        }

        Assert.Equal(1_007UL, model.Actors.Single(actor => actor.ActorId == ConstructionContactFixture.Player).BodyAndCarriedMass.Value);
        // Two actual byproduct lots remain carried; neither installed output is counted a second time.
        Assert.Equal(61_011UL, model.Actors.Single(actor => actor.ActorId == ConstructionContactFixture.Builder).BodyAndCarriedMass.Value);
        Assert.Equal((UInt128)602_018, model.Graph.TotalInputMass);
        Assert.Equal(model.Graph.TotalInputMass, result.AcceptedFoundationLoad);
        Assert.Equal((UInt128)0, result.FailedInputMass);
        Assert.Equal((UInt128)541_007, result.ActiveLoads[fixture.LowerBase]);
        Assert.Equal((UInt128)406_007, result.ActiveLoads[fixture.LowerTop]);
        Assert.Equal((UInt128)271_007, result.ActiveLoads[fixture.UpperBase]);
        Assert.Equal((UInt128)136_007, result.ActiveLoads[fixture.UpperTop]);
        AssertBearing(model, fixture.UpperTop, fixture.UpperBase);
        StructuralBearing foreign = AssertBearing(model, fixture.UpperBase, fixture.LowerTop);
        Assert.Equal(65_536_000_000UL, foreign.Weight); // Exact 1m² in squared 1/256mm ticks.
        AssertBearing(model, fixture.LowerTop, fixture.LowerBase);
        AssertBearing(model, ConstructionBearingModel.ActorIdentity(ConstructionContactFixture.Player), fixture.UpperTop);
        Assert.DoesNotContain(model.Graph.Elements, element => element.ElementId == fixture.Policy.Foundations
            .Single(anchor => anchor.Cell == ConstructionContactFixture.Pedestal).ElementId);

        Assert.Equal(originalHash, basis.ContentHash);
        Assert.True(basis.Space.Regions.Single().GetCell(ConstructionContactFixture.Pedestal).IsSolid);
        Assert.All(basis.ConstructionPhysical!.Members, member => Assert.Equal(default, member.Displacement));
        Assert.Same(basis.Construction, scene.Physical.Construction);
        Assert.Same(basis.Inventory, scene.Basis.Inventory);
        Assert.Same(basis.Production, scene.Basis.Production);
        Assert.Equal(new SubunitPosition(128, 128, 576), basis.Space.GetActor(ConstructionContactFixture.Player).Feet);
    }

    [Fact]
    public void An_exact_member_capacity_accepts_the_unoccupied_stack_but_the_real_rider_overloads_its_lower_base()
    {
        ConstructionContactFixture fixture = ConstructionContactFixture.Create();
        ConstructionLoadPolicy policy = fixture.WithCapacity(540_000);
        ConstructionBearingModel empty = policy.AssessContacts(fixture.Stack(rider: false));
        StructuralLoadResolution unloaded = Finish(empty);
        Assert.Empty(unloaded.Failures);
        Assert.Equal((UInt128)540_000, unloaded.ActiveLoads[fixture.LowerBase]);

        ConstructionBearingModel occupied = policy.AssessContacts(fixture.Stack());
        StructuralLoadResolution loaded = Finish(occupied);
        StructuralFailure first = loaded.Failures.Single(failure => failure.ElementId == fixture.LowerBase);
        Assert.Equal(StructuralFailureReason.Overloaded, first.Reason);
        Assert.Equal((UInt128)541_007, first.Load);
        Assert.Equal(1U, first.Pass);
        Assert.Contains(loaded.Failures, failure => failure.ElementId == fixture.UpperBase
            && failure.Reason == StructuralFailureReason.Unsupported && failure.Pass > first.Pass);
        Assert.Contains(loaded.Failures, failure => failure.ElementId == ConstructionBearingModel.ActorIdentity(ConstructionContactFixture.Player)
            && failure.Reason == StructuralFailureReason.Unsupported);
        Assert.Equal(occupied.Graph.TotalInputMass, loaded.AcceptedFoundationLoad + loaded.FailedInputMass);
        Assert.Equal((UInt128)541_007, loaded.FailedInputMass);
        Assert.Equal(empty.Graph.TotalInputMass, occupied.Graph.TotalInputMass);
    }

    [Fact]
    public void Enumeration_order_does_not_change_scene_graph_or_full_dependency_pin()
    {
        ConstructionContactFixture fixture = ConstructionContactFixture.Create();
        ConstructionContactScene scene = fixture.Stack();
        var reversedPhysical = new ConstructionPhysicalSnapshot(scene.Physical.Construction,
            scene.Physical.Revision, scene.Physical.Members.Reverse());
        var reversedScene = new ConstructionContactScene(fixture.Basis, scene.Regions.Reverse(),
            reversedPhysical, scene.Actors.Reverse());
        var reversedPolicy = new ConstructionLoadPolicy(fixture.Policy.Scope, fixture.Policy.Templates.Reverse(),
            fixture.Policy.Foundations.Reverse(), new StructuralMassCatalog(fixture.Policy.Masses.Bodies.Reverse(),
                fixture.Policy.Masses.SupplementalItems.Reverse()));
        ConstructionBearingModel expected = fixture.Policy.AssessContacts(scene);
        ConstructionBearingModel actual = reversedPolicy.AssessContacts(reversedScene);
        Assert.Equal(scene.InputPin, reversedScene.InputPin);
        Assert.Equal(expected.InputPin, actual.InputPin);
        Assert.Equal(StructuralLoadCodec.EncodeGraph(expected.Graph), StructuralLoadCodec.EncodeGraph(actual.Graph));
        Assert.Equal(expected.Members, actual.Members);
        Assert.Equal(StructuralLoadCodec.Encode(Finish(expected), expected.InputPin),
            StructuralLoadCodec.Encode(Finish(actual), actual.InputPin));
    }

    [Fact]
    public void Unused_mass_calibration_still_changes_the_pin_and_invalidates_an_existing_partial_resolution()
    {
        ConstructionContactFixture fixture = ConstructionContactFixture.Create();
        ConstructionContactScene scene = fixture.Stack();
        ConstructionBearingModel original = fixture.Policy.AssessContacts(scene);
        var changedMasses = new StructuralMassCatalog(fixture.Policy.Masses.Bodies,
            fixture.Policy.Masses.SupplementalItems.Select(item => item.Item.DefinitionId == ConstructionContactFixture.Id(90)
                ? new StructuralItemMass(item.Item, new(4)) : item));
        var changedPolicy = new ConstructionLoadPolicy(fixture.Policy.Scope, fixture.Policy.Templates,
            fixture.Policy.Foundations, changedMasses);
        ConstructionBearingModel changed = changedPolicy.AssessContacts(scene);
        Assert.Equal(StructuralLoadCodec.EncodeGraph(original.Graph), StructuralLoadCodec.EncodeGraph(changed.Graph));
        Assert.NotEqual(original.InputPin, changed.InputPin);
        StructuralLoadResolution partial = StructuralLoadResolution.Begin(original.Graph, []).Advance(1);
        Assert.False(partial.IsComplete);
        byte[] bytes = StructuralLoadCodec.Encode(partial, original.InputPin);
        Assert.Equal(partial.TotalEvaluations,
            StructuralLoadCodec.Decode(bytes, original, TestContext.Current.CancellationToken).TotalEvaluations);
        Assert.Throws<FormatException>(() => StructuralLoadCodec.Decode(bytes, changed, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Clear_but_incomplete_member_contact_is_rejected_for_a_gap_and_a_half_width_ledge()
    {
        ConstructionContactFixture fixture = ConstructionContactFixture.Create();
        ConstructionContactScene gap = fixture.Stack(gap: 1);
        Assert.Throws<ArgumentException>(() => fixture.Policy.AssessContacts(gap));

        ConstructionContactFixture offset = ConstructionContactFixture.Create(upperOffsetX: 128);
        ConstructionContactScene halfLedge = offset.Stack();
        Assert.Equal(new SubunitPosition(128, 0, 64), halfLedge.OriginFor(offset.Upper));
        Assert.Throws<ArgumentException>(() => offset.Policy.AssessContacts(halfLedge));
    }

    [Fact]
    public void A_clear_actor_one_subunit_above_the_complete_floor_cannot_acquire_a_load_contact()
    {
        ConstructionContactFixture fixture = ConstructionContactFixture.Create();
        ConstructionContactScene supported = fixture.Stack();
        Assert.Empty(Finish(fixture.Policy.AssessContacts(supported)).Failures);
        ActorPresence[] floating = supported.Actors.Select(actor => actor.ActorId == ConstructionContactFixture.Player
            ? ConstructionContactFixture.At(actor, actor.Feet.Offset(0, 0, 1)) : actor).ToArray();
        var hovering = new ConstructionContactScene(fixture.Basis, supported.Regions, supported.Physical, floating);
        Assert.Throws<ArgumentException>(() => fixture.Policy.AssessContacts(hovering));
    }

    [Fact]
    public void Scene_substitution_cannot_remove_an_actor_replace_terrain_or_reset_member_condition()
    {
        ConstructionContactFixture fixture = ConstructionContactFixture.Create();
        ConstructionContactScene scene = fixture.Stack();
        Assert.Throws<ArgumentException>(() => new ConstructionContactScene(fixture.Basis, scene.Regions,
            scene.Physical, scene.Actors.Where(actor => actor.ActorId != ConstructionContactFixture.Player)));
        TerrainRegionSnapshot original = scene.Regions.Single();
        TerrainRegionSnapshot substituted = new StratifiedTerrainGenerator().Generate(original.Scope,
            ConstructionContactFixture.Id(91), original.Rules, original.Bounds);
        Assert.Throws<ArgumentException>(() => new ConstructionContactScene(fixture.Basis, [substituted], scene.Physical, scene.Actors));
        ConstructionMemberState member = scene.Physical.GetMember(fixture.Upper.Installed!.StructureId, 0);
        ConstructionPhysicalSnapshot changed = scene.Physical.WithMember(scene.Physical.Revision,
            member.WithState(member.Revision, 9_999, member.Displacement, member.Disposition));
        Assert.Throws<ArgumentException>(() => new ConstructionContactScene(fixture.Basis, scene.Regions, changed, scene.Actors));
        Assert.Throws<ArgumentException>(() => new ConstructionContactScene(fixture.Basis, [], scene.Physical, scene.Actors));
    }

    [Fact]
    public void Stable_binding_remains_terrain_only_and_the_new_scene_is_not_a_publishable_root()
    {
        ConstructionContactFixture fixture = ConstructionContactFixture.Create();
        byte[] original = WorldSnapshotCodec.Encode(fixture.Basis);
        ConstructionBearingModel stable = fixture.Policy.Bind(fixture.Basis);
        EntityId pedestal = fixture.Policy.Foundations.Single(anchor => anchor.Cell == ConstructionContactFixture.Pedestal).ElementId;
        AssertBearing(stable, fixture.UpperBase, pedestal);
        Assert.DoesNotContain(stable.Graph.Bearings, bearing => bearing.SupportedElementId == fixture.UpperBase
            && bearing.SupportingElementId == fixture.LowerTop);
        Assert.Null(stable.CandidateScenePin);
        ConstructionContactScene scene = fixture.Stack();
        Assert.Empty(Finish(fixture.Policy.AssessContacts(scene)).Failures);
        Assert.Throws<ArgumentException>(() => new LocalSpatialSnapshot(scene.Scope, scene.Regions, scene.Actors,
            scene.Physical.GetStructures(), GroundSupportMode.SteppedTerrainAndConstruction));
        Assert.Equal(original, WorldSnapshotCodec.Encode(fixture.Basis));
        Assert.Equal(original, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(original)));
    }

    [Fact]
    public void A_lower_member_change_selects_both_rigid_assemblies_and_the_rider_without_unrelated_ground_actors()
    {
        ConstructionContactFixture fixture = ConstructionContactFixture.Create();
        ConstructionBearingModel model = fixture.Policy.AssessContacts(fixture.Stack());
        ConstructionLoadNeighborhood neighborhood = ConstructionLoadNeighborhood.Create(model, [fixture.LowerBase]);
        Assert.Equal(model.InputPin, neighborhood.InputPin);
        Assert.Equal(new[] { fixture.Lower.Installed!.StructureId, fixture.Upper.Installed!.StructureId }.Order(),
            neighborhood.StructureIds);
        Assert.All(model.Members, member => Assert.Contains(member.ElementId, neighborhood.ElementIds));
        Assert.Equal(new[] { ConstructionContactFixture.Player }, neighborhood.ActorIds);
        Assert.DoesNotContain(ConstructionBearingModel.ActorIdentity(ConstructionContactFixture.Builder), neighborhood.ElementIds);
        Assert.Equal(6, neighborhood.ElementIds.Count); // Four members, rider and its bottom foundation.

        // Removing authored internal load edges leaves separate graph components. Rigid membership
        // must still pull in the other member, the foreign assembly it supports, and that rider.
        ConstructionBearingTemplate template = Assert.Single(fixture.Policy.Templates);
        var noJoints = new ConstructionBearingTemplate(template.DefinitionId, template.DefinitionVersion,
            template.Members, []);
        var unbraced = new ConstructionLoadPolicy(fixture.Policy.Scope, [noJoints],
            fixture.Policy.Foundations, fixture.Policy.Masses);
        ConstructionBearingModel disconnected = unbraced.AssessContacts(fixture.Stack());
        Assert.DoesNotContain(disconnected.Graph.Bearings, bearing => bearing.SupportedElementId == fixture.LowerTop);
        ConstructionLoadNeighborhood rigid = ConstructionLoadNeighborhood.Create(disconnected, [fixture.LowerBase]);
        Assert.Equal(neighborhood.ElementIds, rigid.ElementIds);
        Assert.Equal(neighborhood.StructureIds, rigid.StructureIds);
        Assert.Equal(neighborhood.ActorIds, rigid.ActorIds);
    }

    private static StructuralBearing AssertBearing(ConstructionBearingModel model, EntityId upper, EntityId lower) =>
        Assert.Single(model.Graph.Bearings, bearing => bearing.SupportedElementId == upper
            && bearing.SupportingElementId == lower);

    private static StructuralLoadResolution Finish(ConstructionBearingModel model)
    {
        StructuralLoadResolution result = StructuralLoadResolution.Begin(model.Graph, []);
        for (int calls = 0; !result.IsComplete; calls++)
        {
            Assert.InRange(calls, 0, 32);
            result = result.Advance();
        }
        return result;
    }
}
