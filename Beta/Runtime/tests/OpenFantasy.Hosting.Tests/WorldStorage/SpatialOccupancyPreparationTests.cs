using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class SpatialOccupancyPreparationTests
{
    [Fact]
    public void Two_hundred_archived_presences_need_only_local_pages_and_one_actor_detail_for_the_move()
    {
        var directory = Directory.CreateTempSubdirectory("openfantasy-occupancy-");
        try
        {
            // Returns metadata only. Each authored presence has passed local placement admission;
            // no complete 200-actor LocalSpatialSnapshot or WorldSnapshot is constructed.
            var catalog = WriteCatalog(directory.FullName, 200);
            var moverReference = catalog.Actors[0];
            var mover = SpatialActorArchiveV1.Decode(File.ReadAllBytes(ActorPath(directory.FullName, moverReference)), moverReference);
            var footprint = MovementTerrainFootprint.Create(mover, GroundSupportMode.TerrainOnly, -32, 0);
            var pageReads = new List<SpatialOccupancyPageKey>();
            SpatialOccupancyPage? ReadPage(SpatialOccupancyPageKey key)
            {
                pageReads.Add(key);
                return catalog.Pages.ReadPage(key);
            }
            var occupancy = new SpatialOccupancyReadPlan(mover.Scope, footprint.Bounds, ReadPage, 8, 4, 0);
            Assert.Equal(4, pageReads.Count);
            Assert.Single(occupancy.Actors);
            var actorReads = new List<EntityId>();
            int actorBytes = 0;
            var detail = new SpatialOccupancyReadPreparation((reference, limit) =>
            {
                actorReads.Add(reference.EntityId);
                string path = ActorPath(directory.FullName, reference);
                Assert.InRange(new FileInfo(path).Length, 1, limit);
                byte[] bytes = File.ReadAllBytes(path);
                actorBytes += bytes.Length;
                return bytes;
            }, _ => throw new InvalidOperationException("No structure payload should be requested."))
                .Load(occupancy, SpatialActorArchiveV1.MaximumByteLength);
            Assert.Equal(new[] { mover.ActorId }, actorReads);
            var terrainPlan = new TerrainReadPlan(mover.Scope, footprint.Bounds, catalog.Terrain, 1);
            int terrainReads = 0;
            var terrain = new TerrainReadPreparation((reference, limit) =>
            {
                terrainReads++;
                string path = TerrainPath(directory.FullName, reference);
                Assert.InRange(new FileInfo(path).Length, 1, limit);
                return File.ReadAllBytes(path);
            }).Load(terrainPlan, TerrainRegionCodecV1.MaximumEncodedByteLength, 16 * 8 * 6);
            Assert.Equal(1, terrainReads);
            var prepared = SpatialMovementDependencies.FromDirectory(footprint, terrainPlan, detail, StructureSupportMode.TerrainOnly);
            var result = prepared.Evaluate(terrain);
            Assert.Equal(SpatialMoveStatus.Prepared, result.Status);
            Assert.Equal(mover.Feet.Offset(-32, 0, 0), result.Candidate!.Feet);
            Assert.Single(prepared.Actors);
            prepared.ValidateCurrentDirectory(catalog.Pages.ReadPage, catalog.Terrain,
                GroundSupportMode.TerrainOnly, StructureSupportMode.TerrainOnly);
            // Removing a distant metadata reference changes every overlapping page atomically,
            // without loading that actor's detailed archive or invalidating this local read.
            var distant = catalog.Actors[^1];
            var changed = catalog.Pages.Apply(0, [new(distant, null)]);
            Assert.Null(changed.Find(distant.EntityId));
            Assert.Equal(distant, catalog.Pages.Find(distant.EntityId));
            occupancy.ValidateCurrent(changed.ReadPage);
            string? evidence = Environment.GetEnvironmentVariable("OPENFANTASY_SPATIAL_DIRECTORY_EVIDENCE");
            if (evidence is not null)
                File.WriteAllText(evidence, JsonSerializer.Serialize(new {
                    archivedActors = catalog.Actors.Length, directoryPages = catalog.Pages.RetainedPageCount,
                    metadataPagesRead = pageReads.Count, selectedActorArchivesRead = actorReads.Count,
                    moverDiscoveryReads = 1, selectedActorBytes = actorBytes, terrainArchivesRead = terrainReads,
                    scope = "Geometric population and selected I/O only; no global actor owner, deep NPC, world memory or partition simulation claim."
                }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public void Missing_stale_corrupt_and_over_budget_detail_cannot_return_a_partial_read()
    {
        var world = WorldServiceFixture.Genesis();
        var actor = world.Space.Actors[0];
        var reference = SpatialOccupancyReference.Capture(actor);
        byte[] bytes = SpatialActorArchiveV1.Encode(actor);
        var pages = Pages([reference]);
        var plan = new SpatialOccupancyReadPlan(actor.Scope, reference.Bounds, key => pages.GetValueOrDefault(key), 8, 1, 0);
        Assert.Throws<InvalidDataException>(() => new SpatialOccupancyReadPreparation((_, _) => null, _ => null).Load(plan, bytes.Length));
        Assert.Throws<InvalidDataException>(() => new SpatialOccupancyReadPreparation((_, _) => bytes, _ => null).Load(plan, bytes.Length - 1));
        bool read = false;
        Assert.Throws<InvalidOperationException>(() => new SpatialOccupancyReadPreparation((_, _) => { read = true; return bytes; }, _ => null).Load(plan, 0));
        Assert.False(read);
        var changed = new ActorPresence(actor.Binding, actor.Condition, actor.Scope, actor.Feet.Offset(1, 0, 0),
            actor.Revision + 1, actor.HomePlaceId);
        Assert.Throws<InvalidDataException>(() => new SpatialOccupancyReadPreparation((_, _) => SpatialActorArchiveV1.Encode(changed), _ => null)
            .Load(plan, SpatialActorArchiveV1.MaximumByteLength));
        byte[] corrupted = (byte[])bytes.Clone();
        corrupted[^1] ^= 1;
        Assert.Throws<InvalidDataException>(() => new SpatialOccupancyReadPreparation((_, _) => corrupted, _ => null).Load(plan, bytes.Length));
        var both = world.Space.Actors.Select(SpatialOccupancyReference.Capture).ToArray();
        var pairPages = Pages(both);
        var pairPlan = new SpatialOccupancyReadPlan(actor.Scope, new(new(-1, -1, 0), 5, 2, 4), key => pairPages.GetValueOrDefault(key), 8, 2, 0);
        int calls = 0;
        Assert.Throws<InvalidOperationException>(() => new SpatialOccupancyReadPreparation((expected, _) =>
        {
            calls++;
            return SpatialActorArchiveV1.Encode(world.Space.GetActor(expected.EntityId));
        }, _ => null).Load(pairPlan, bytes.Length));
        Assert.Equal(1, calls);
    }

    private static (SpatialOccupancyReference[] Actors, TerrainRegionReference[] Terrain,
        SpatialOccupancyDirectory Pages) WriteCatalog(string directory, int count)
    {
        var basis = WorldServiceFixture.Genesis();
        var original = basis.Space.Regions.Single();
        var definition = basis.Space.Actors[0].Condition.Definition;
        var actors = new List<SpatialOccupancyReference>();
        var terrain = new List<TerrainRegionReference>();
        for (int index = 0; index < count; index++)
        {
            var region = new StratifiedTerrainGenerator().Generate(original.Scope, original.Seed, original.Rules,
                new(new(index * 16 - 4, -4, -1), 16, 8, 6));
            var binding = ActorFormBinding.Corporeal(WorldServiceFixture.Id((ulong)(5000 + index * 4)),
                WorldServiceFixture.Id((ulong)(5001 + index * 4)), WorldServiceFixture.Id((ulong)(5002 + index * 4)), definition);
            var actor = new ActorPresence(binding, new(binding.FormInstanceId, definition, []), original.Scope,
                new(index * 16 * 256, 0, 256), 0, WorldServiceFixture.Id((ulong)(5003 + index * 4)));
            _ = new LocalSpatialSnapshot(original.Scope, [region], [actor]);
            var actorReference = SpatialOccupancyReference.Capture(actor);
            var terrainReference = TerrainRegionReference.Capture(region);
            File.WriteAllBytes(ActorPath(directory, actorReference), SpatialActorArchiveV1.Encode(actor));
            File.WriteAllBytes(TerrainPath(directory, terrainReference), TerrainRegionCodecV1.Encode(region));
            actors.Add(actorReference); terrain.Add(terrainReference);
        }
        return (actors.ToArray(), terrain.ToArray(), SpatialOccupancyDirectory.Create(original.Scope, actors, count, count * 4));
    }

    private static Dictionary<SpatialOccupancyPageKey, SpatialOccupancyPage> Pages(IEnumerable<SpatialOccupancyReference> references)
    {
        var entries = references.ToArray();
        var pages = new Dictionary<SpatialOccupancyPageKey, List<SpatialOccupancyReference>>();
        foreach (var entry in entries)
        {
            var minimum = SpatialOccupancyPageKey.At(entry.Bounds.Minimum);
            var maximum = SpatialOccupancyPageKey.At(entry.Bounds.Maximum);
            for (long z = minimum.Z; z <= maximum.Z; z++)
                for (long y = minimum.Y; y <= maximum.Y; y++)
                    for (long x = minimum.X; x <= maximum.X; x++)
                    {
                        var key = new SpatialOccupancyPageKey(x, y, z);
                        if (!pages.TryGetValue(key, out var bucket)) pages[key] = bucket = [];
                        bucket.Add(entry);
                    }
        }
        return pages.ToDictionary(pair => pair.Key, pair => new SpatialOccupancyPage(entries[0].Scope, pair.Key, 0, pair.Value));
    }

    private static string ActorPath(string directory, SpatialOccupancyReference reference) => Path.Combine(directory, "actor-" + reference.ContentHash.Value);
    private static string TerrainPath(string directory, TerrainRegionReference reference) => Path.Combine(directory, "terrain-" + reference.ContentHash.Value);
}
