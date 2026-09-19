using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class StructuralConstructionWorldTests
{
    [Fact]
    public void Real_funded_members_and_occupants_redistribute_after_one_support_is_lost()
    {
        using var files = new StorageFiles();
        var fixture = new StairNavigationWorldFixture();
        WorldSnapshot world = OccupiedDeck(fixture, files.Path);
        ConstructionBearingModel model = Bind(world);
        var installed = world.Construction!.Projects.Single().Installed!;
        ulong materialMass = world.Construction.Projects.Single().Definition.MaterialTransform
            .Bindings.Single(value => value.Item == world.Production!.Lots.Single(lot => lot.LotId == installed.GuardLotId).Definition)
            .Physical.MassPerUnit.Value;
        Assert.Equal(materialMass, model.Members.Aggregate(0UL, (sum, member) => checked(sum + member.Mass.Value)));
        Assert.All(model.Members, member => Assert.Equal(installed.GuardLotId, member.InstalledLotId));
        var player = model.Actors.Single(value => value.ActorId == fixture.Player);
        Assert.True(player.BodyAndCarriedMass.Value >= 70_000_000);
        EntityId left = Foundation(model, 108, 327);
        StructuralLoadResolution intact = Finish(StructuralLoadResolution.Begin(model.Graph, []));
        StructuralLoadResolution changed = Finish(StructuralLoadResolution.Begin(model.Graph, [left]));
        EntityId deck = Deck(model, world);
        Assert.Empty(intact.Failures);
        Assert.Contains(changed.Failures, value => value.ElementId == left && value.Reason == StructuralFailureReason.Removed);
        Assert.DoesNotContain(changed.Failures, value => value.ElementId == deck || value.ElementId == player.ElementId);
        Assert.Equal(model.Graph.TotalInputMass, changed.AcceptedFoundationLoad + changed.FailedInputMass);
        Assert.Equal(world.ContentHash, model.WorldHash);
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(model.WorldHash, reopened.InspectForHost().State.ContentHash);
    }

    [Fact]
    public void Redistributed_overload_reaches_the_actual_upper_occupant_and_restarts_mid_resolution()
    {
        using var files = new StorageFiles();
        var fixture = new StairNavigationWorldFixture();
        WorldSnapshot world = OccupiedDeck(fixture, files.Path);
        ConstructionBearingModel model = Bind(world, rightLegCapacity: 50_000_000);
        Assert.Empty(Finish(StructuralLoadResolution.Begin(model.Graph, [])).Failures);
        EntityId left = Foundation(model, 108, 327);
        StructuralLoadResolution pending = StructuralLoadResolution.Begin(model.Graph, [left]);
        while (pending.Pass == 1) { pending = pending.Advance(7); }
        Assert.False(pending.IsComplete);
        byte[] archive = StructuralLoadCodec.Encode(pending, model.InputPin);
        StructuralLoadResolution restored = StructuralLoadCodec.Decode(archive, model, TestContext.Current.CancellationToken);
        Assert.Equal(archive, StructuralLoadCodec.Encode(restored, model.InputPin));
        StructuralLoadResolution result = Finish(restored);
        Assert.Equal(StructuralLoadCodec.Encode(Finish(pending), model.InputPin), StructuralLoadCodec.Encode(result, model.InputPin));
        Assert.Contains(result.Failures, value => value.Reason == StructuralFailureReason.Overloaded);
        Assert.Contains(result.Failures, value => value.ElementId == Deck(model, world) && value.Reason == StructuralFailureReason.Unsupported);
        Assert.Contains(result.Failures, value => value.ElementId == ConstructionBearingModel.ActorIdentity(fixture.Player));
        Assert.DoesNotContain(result.Failures, value => value.ElementId == ConstructionBearingModel.ActorIdentity(fixture.Builder));
        Assert.Equal(model.Graph.TotalInputMass, result.AcceptedFoundationLoad + result.FailedInputMass);
        Retain(files.Path, model, archive, result);
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(model.WorldHash, reopened.InspectForHost().State.ContentHash);
        Assert.NotEmpty(reopened.InspectForHost().State.Space.Structures);
    }

    [Fact]
    public void Last_support_removal_reaches_all_dependent_member_and_actor_loads()
    {
        using var files = new StorageFiles();
        var fixture = new StairNavigationWorldFixture();
        WorldSnapshot world = OccupiedDeck(fixture, files.Path);
        ConstructionBearingModel model = Bind(world);
        StructuralLoadResolution result = Finish(StructuralLoadResolution.Begin(model.Graph,
            [Foundation(model, 108, 327), Foundation(model, 109, 327)]));
        Assert.Contains(result.Failures, value => value.ElementId == Deck(model, world));
        Assert.Contains(result.Failures, value => value.ElementId == ConstructionBearingModel.ActorIdentity(fixture.Player));
        Assert.True(result.Pass >= 3);
        Assert.Equal(model.Graph.TotalInputMass, result.AcceptedFoundationLoad + result.FailedInputMass);
    }

    [Fact]
    public void Unqualified_anchors_templates_and_member_damage_cannot_hide_behind_contact()
    {
        using var files = new StorageFiles();
        var fixture = new StairNavigationWorldFixture();
        WorldSnapshot world = fixture.FundAndBuild(files.Path);
        ConstructionBearingModel model = Bind(world);
        Assert.Throws<ArgumentException>(() => ConstructionBearingModel.Bind(world, [], model.Foundations, Catalog(world)));
        StableTerrainBearing source = model.Foundations.First();
        var falseSource = new StableTerrainBearing(source.ElementId, source.Cell, new(ulong.MaxValue, 9), source.Material, source.Capacity);
        Assert.Throws<ArgumentException>(() => ConstructionBearingModel.Bind(world, model.Templates,
            model.Foundations.Select(value => value.ElementId == source.ElementId ? falseSource : value), Catalog(world)));
        var missing = model.Foundations.Where(value => value.Cell.X != 108 || value.Cell.Y != 327);
        Assert.Throws<ArgumentException>(() => ConstructionBearingModel.Bind(world, model.Templates, missing, Catalog(world)));
        StructuralMemberReference deck = model.Members.Single(value => value.ElementId == Deck(model, world));
        ConstructionBearingModel damaged = ConstructionBearingModel.Bind(world, model.Templates, model.Foundations, Catalog(world),
            [new(deck.StructureId, deck.MemberIndex, 0)]);
        Assert.NotEqual(model.InputPin, damaged.InputPin);
        Assert.Contains(Finish(StructuralLoadResolution.Begin(damaged.Graph, [])).Failures,
            value => value.ElementId == deck.ElementId && value.Reason == StructuralFailureReason.ConditionLost);
    }

    [Fact]
    public void Exact_definition_span_and_real_joint_faces_are_required_before_load_evaluation()
    {
        using var files = new StorageFiles();
        WorldSnapshot world = new StairNavigationWorldFixture().FundAndBuild(files.Path);
        ConstructionBearingModel model = Bind(world);
        ConstructionBearingTemplate template = model.Templates.Single();
        var wrongVersion = new ConstructionBearingTemplate(template.DefinitionId, ContentHash.Compute("different-geometry"u8),
            template.Members, template.Joints);
        var shortSpan = new ConstructionBearingTemplate(template.DefinitionId, template.DefinitionVersion,
            template.Members.Select(member => member with { MaximumSpanMillimetres = 1 }), template.Joints);
        var wrongFace = new ConstructionBearingTemplate(template.DefinitionId, template.DefinitionVersion, template.Members,
            template.Joints.Select(joint => joint with { Kind = StructuralJointKind.BracedFace }));
        foreach (ConstructionBearingTemplate invalid in new[] { wrongVersion, shortSpan, wrongFace })
        {
            Assert.Throws<ArgumentException>(() => ConstructionBearingModel.Bind(world, [invalid], model.Foundations, Catalog(world)));
        }
    }

    private static WorldSnapshot OccupiedDeck(StairNavigationWorldFixture fixture, string path)
    {
        WorldSnapshot world = fixture.FundAndBuild(path);
        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(path);
        world = fixture.WalkTo(authority, world, fixture.Player, StairNavigationWorldFixture.StairApproach);
        return fixture.WalkTo(authority, world, fixture.Player, StairNavigationWorldFixture.UpperDeck);
    }

    private static ConstructionBearingModel Bind(WorldSnapshot world, ulong rightLegCapacity = 100_000_000)
    {
        ConstructionDefinition definition = world.Construction!.Projects.Single().Definition;
        var ratings = definition.Members.Select((member, index) => new StructuralMemberRating((uint)index,
            new(member.MinX == 3625 && member.MaxZ == 2000 ? rightLegCapacity : 100_000_000), 4000)).ToArray();
        uint deck = (uint)definition.Members.Select((member, index) => (member, index)).Single(value => value.member.MinZ == 2000).index;
        var joints = definition.Members.Select((member, index) => (member, index))
            .Where(value => value.member.MaxZ == 2000 && value.member.MinY == 3750)
            .Select(value => new StructuralMemberJoint(deck, (uint)value.index, StructuralJointKind.VerticalBearing));
        var template = new ConstructionBearingTemplate(definition.DefinitionId, definition.Version, ratings, joints);
        var anchors = world.Space.Regions.SelectMany(region => region.Cells).Where(cell => cell.IsSolid && cell.Coordinate.Z == 0)
            .Select(cell => new StableTerrainBearing(EntityId.DeriveSpawned(cell.SourceId!.Value, 900000), cell.Coordinate,
                cell.SourceId.Value, cell.Material!, new(1_000_000_000)));
        return ConstructionBearingModel.Bind(world, [template], anchors, Catalog(world));
    }

    private static StructuralMassCatalog Catalog(WorldSnapshot world)
    {
        var bodies = world.Space.Actors.Select(actor => actor.Condition.Definition).DistinctBy(value => value.DefinitionId)
            .Select(body => new StructuralBodyMass(body.DefinitionId, body.Version, new(70_000_000)));
        var known = world.Production!.Recipes.SelectMany(recipe => recipe.Bindings).Select(binding => binding.Item).ToHashSet();
        // This authored fixture explicitly rates its remaining tool definitions at one kilogram each.
        var tools = world.Inventory.Sources.Select(source => source.Definition).Where(item => !known.Contains(item)).Distinct()
            .Select(item => new StructuralItemMass(item, new(1_000_000)));
        return new(bodies, tools);
    }

    private static EntityId Foundation(ConstructionBearingModel model, long x, long y) =>
        model.Foundations.Single(value => value.Cell.X == x && value.Cell.Y == y && value.Cell.Z == 0).ElementId;

    private static EntityId Deck(ConstructionBearingModel model, WorldSnapshot world) => model.Members.Single(value =>
        world.Construction!.Projects.Single().Definition.Members[(int)value.MemberIndex].MinZ == 2000).ElementId;

    private static StructuralLoadResolution Finish(StructuralLoadResolution state)
    {
        while (!state.IsComplete) { state = state.Advance(); }
        return state;
    }

    private static void Retain(string path, ConstructionBearingModel model, byte[] pending, StructuralLoadResolution completed)
    {
        string? output = Environment.GetEnvironmentVariable("OPENFANTASY_STRUCTURAL_LOAD_EVIDENCE");
        if (string.IsNullOrWhiteSpace(output)) { return; }
        Directory.CreateDirectory(output);
        File.Copy(path, Path.Combine(output, "occupied-world.sqlite"), overwrite: true);
        File.WriteAllBytes(Path.Combine(output, "pending.bin"), pending);
        File.WriteAllBytes(Path.Combine(output, "completed.bin"), StructuralLoadCodec.Encode(completed, model.InputPin));
        File.WriteAllBytes(Path.Combine(output, "graph.bin"), StructuralLoadCodec.EncodeGraph(model.Graph));
        File.WriteAllText(Path.Combine(output, "expected.json"), JsonSerializer.Serialize(new
        {
            Scope = "Read-only structural failure plan bound to actual funded construction and occupied deck; no world collapse is committed",
            InputPin = model.InputPin.Value,
            WorldHash = model.WorldHash.Value,
            TotalInputMass = model.Graph.TotalInputMass.ToString(),
            AcceptedFoundationLoad = completed.AcceptedFoundationLoad.ToString(),
            FailedInputMass = completed.FailedInputMass.ToString(),
            completed.Pass,
            completed.TotalEvaluations,
            Members = model.Members,
            Actors = model.Actors,
            Failures = completed.Failures.Select(value => new { value.ElementId, value.Reason, Load = value.Load.ToString(), value.Pass })
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
