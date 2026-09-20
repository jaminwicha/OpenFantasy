using System.Text.Json;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ConstructionDescentWorldTests
{
    [Fact]
    public void Funded_material_and_actual_upstairs_occupant_require_distinct_complete_fall_contacts()
    {
        var fixture = new StairNavigationWorldFixture(physicalConstruction: true);
        using var files = new StorageFiles();
        WorldSnapshot world = fixture.FundAndBuild(files.Path);
        var observations = new List<object>();
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            world = fixture.WalkTo(authority, world, fixture.Player, StairNavigationWorldFixture.UpperDeck);
            world = fixture.WalkTo(authority, world, fixture.Builder, StairNavigationWorldFixture.PlayerParking);
            ConstructionProject project = Assert.Single(world.Construction!.Projects);
            SpatialStructure intact = Assert.Single(world.Space.Structures);
            MillimetreBox deck = project.Definition.Members.Single(value => value.MinZ == 2000);
            MillimetreBox[] legs = project.Definition.Members.Where(value => value.MinY == 3750 && value.MaxZ == 2000).ToArray();
            Assert.Equal(2, legs.Length);
            SubunitBox deckBox = deck.At(project.SiteFeet);
            byte[] original = WorldSnapshotCodec.Encode(world);
            SpatialStructure withoutDeck = Omit(project, intact, [deck]);
            Assert.Throws<ArgumentException>(() => world.Space.WithStructures([withoutDeck]));

            // Candidate scene queries omit only explicitly selected moving material. They do not
            // remove that material from the physical owner or authorize an intermediate world.
            var actorDescent = Scene(world, withoutDeck).QueryActor(fixture.Player, 1024);
            Assert.Equal(VerticalDescentStatus.Landed, actorDescent.Status);
            Assert.Equal(576L, actorDescent.DropSubunits);
            Assert.Equal(256L, actorDescent.ReachedVolume!.Minimum.Z);
            observations.Add(new { Case = "actor_without_deck", actorDescent.Status, actorDescent.DropSubunits, actorDescent.ReachedVolume });

            // The same assembly still has two legs. Ignoring its entire identity would falsely
            // let its deck pass through their surviving contact and settle on the ground.
            VerticalDescentResult retainedLegs = Scene(world, withoutDeck).QueryBox(deckBox, 1024);
            Assert.Equal(VerticalDescentStatus.IncompleteSupport, retainedLegs.Status);
            Assert.Equal(0L, retainedLegs.DropSubunits);
            SpatialStructure withoutDeckAndLegs = Omit(project, intact, legs.Append(deck));
            VerticalDescentResult deckDescent = Scene(world, withoutDeckAndLegs).QueryBox(deckBox, 1024);
            Assert.Equal(VerticalDescentStatus.Landed, deckDescent.Status);
            Assert.Equal(512L, deckDescent.DropSubunits);
            SubunitBox groundedDeck = Assert.IsType<SubunitBox>(deckDescent.ReachedVolume);
            Assert.Equal(256L, groundedDeck.Minimum.Z);
            Assert.Equal(320L, groundedDeck.Maximum.Z);
            observations.Add(new { Case = "deck_without_legs", deckDescent.Status, deckDescent.DropSubunits, deckDescent.ReachedVolume });

            // Reintroduce exactly the deck's material at its checked endpoint before asking for
            // the actor's contact. Landing on the bare ground would now erase a real dependency.
            StructureBearingSurface top = Surface(project.SiteFeet, deck);
            var restingDeck = new SpatialStructure(intact.StructureId, withoutDeckAndLegs.Boxes.Append(groundedDeck),
                withoutDeckAndLegs.BearingSurfaces.Append(new(top.Bounds,
                    top.TopTicks - (Int128)deckDescent.DropSubunits * StructureBearingSurface.TicksPerSubunit)));
            VerticalDescentResult onDebris = Scene(world, restingDeck).QueryActor(fixture.Player, 1024);
            Assert.Equal(VerticalDescentStatus.Landed, onDebris.Status);
            Assert.Equal(512L, onDebris.DropSubunits);
            Assert.Equal(320L, onDebris.ReachedVolume!.Minimum.Z);
            observations.Add(new { Case = "actor_above_reintroduced_deck", onDebris.Status, onDebris.DropSubunits, onDebris.ReachedVolume });
            Assert.Equal(original, WorldSnapshotCodec.Encode(world));
            Assert.Equal(world.ContentHash, authority.InspectForHost().State.ContentHash);
            Assert.Equal(12, world.ConstructionPhysical!.Members.Count);

            // The second occupant reaches the lower floor through ordinary retained movement.
            // An actor is a collision dependency, never an invented load-bearing platform.
            world = fixture.WalkTo(authority, world, fixture.Builder, StairNavigationWorldFixture.UnderDeck);
            original = WorldSnapshotCodec.Encode(world);
            VerticalDescentResult person = Scene(world, withoutDeck).QueryActor(fixture.Player, 1024);
            VerticalDescentResult material = Scene(world, withoutDeckAndLegs).QueryBox(deckBox, 1024);
            long head = world.Space.GetActor(fixture.Builder).Feet.Z
                + world.Space.GetActor(fixture.Builder).Condition.Definition.ClearanceDimensions.HeightSubunits;
            Assert.Equal(VerticalDescentStatus.ActorContact, person.Status);
            Assert.Equal(head, person.ReachedVolume!.Minimum.Z);
            Assert.Equal(VerticalDescentStatus.ActorContact, material.Status);
            Assert.Equal(head, material.ReachedVolume!.Minimum.Z);
            observations.Add(new { Case = "actor_contact", person.Status, person.DropSubunits, person.ReachedVolume });
            observations.Add(new { Case = "material_actor_contact", material.Status, material.DropSubunits, material.ReachedVolume });
            Assert.Equal(original, WorldSnapshotCodec.Encode(world));
            Assert.Equal(world.ContentHash, authority.InspectForHost().State.ContentHash);
        }
        byte[] database = File.ReadAllBytes(files.Path);
        using (SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(world.ContentHash, reopened.InspectForHost().State.ContentHash);
            Assert.Equal(world.Production!.ContentHash, reopened.InspectForHost().State.Production!.ContentHash);
            Assert.Equal(world.ConstructionPhysical!.ContentHash, reopened.InspectForHost().State.ConstructionPhysical!.ContentHash);
        }
        Assert.Equal(database, File.ReadAllBytes(files.Path));
        string? directory = Environment.GetEnvironmentVariable("OPENFANTASY_COLLAPSE_MOTION_EVIDENCE");
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, "occupied-world.sqlite"), database);
            File.WriteAllText(Path.Combine(directory, "motion.json"), JsonSerializer.Serialize(new
            {
                Scope = "Read-only candidate geometry after actual funded construction and occupied travel; no collapse publication or material retirement",
                WorldHash = world.ContentHash.Value, world.Revision,
                PhysicalHash = world.ConstructionPhysical!.ContentHash.Value,
                MaterialHash = world.Production!.ContentHash.Value, Observations = observations
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static VerticalDescentQuery Scene(WorldSnapshot world, SpatialStructure structure) =>
        new(world.Space.Scope, world.Space.Regions, [structure], world.Space.Actors);

    private static SpatialStructure Omit(ConstructionProject project, SpatialStructure original,
        IEnumerable<MillimetreBox> removed)
    {
        MillimetreBox[] members = removed.ToArray();
        SubunitBox[] boxes = members.Select(value => value.At(project.SiteFeet)).ToArray();
        StructureBearingSurface[] surfaces = members.Select(value => Surface(project.SiteFeet, value)).ToArray();
        return new(original.StructureId, original.Boxes.Except(boxes), original.BearingSurfaces.Except(surfaces));
    }

    private static StructureBearingSurface Surface(SubunitPosition origin, MillimetreBox member)
    {
        Int128 X(long millimetres) => (Int128)origin.X * StructureBearingSurface.TicksPerSubunit
            + (Int128)millimetres * StructureBearingSurface.TicksPerMillimetre;
        Int128 Y(long millimetres) => (Int128)origin.Y * StructureBearingSurface.TicksPerSubunit
            + (Int128)millimetres * StructureBearingSurface.TicksPerMillimetre;
        return new(new BoundaryRectangle(X(member.MinX), Y(member.MinY), X(member.MaxX), Y(member.MaxY)),
            (Int128)origin.Z * StructureBearingSurface.TicksPerSubunit
            + (Int128)member.MaxZ * StructureBearingSurface.TicksPerMillimetre);
    }
}
