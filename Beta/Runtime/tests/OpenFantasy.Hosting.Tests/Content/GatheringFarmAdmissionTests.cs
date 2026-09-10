using System.Text.Json;
using System.Text.Json.Nodes;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.Content;

public sealed class GatheringFarmAdmissionTests
{
    [Fact]
    public void Domain_separates_user_seed_and_preserves_the_unchanged_base_composition()
    {
        const string derived = "b7079362de39c95808fed1ec4e86edb85fd7c257a4e5c36bbd66ff0eb31d5461";
        Assert.Equal(derived, GatheringFarmInput.Create("river", "Orin").BaseSeedText);
        WorldSnapshot oldProfile = FarmWorldAdmission.CreateEmbedded("river", "Orin");
        byte[] oldBytes = WorldSnapshotCodecV1.Encode(oldProfile);
        WorldSnapshot basis = FarmWorldAdmission.CreateEmbedded(derived, "Orin");
        WorldSnapshot work = Create();
        Assert.NotEqual(oldProfile.WorldId, work.WorldId);
        Assert.NotEqual(oldProfile.AdmissionVersion, work.AdmissionVersion);
        Assert.Empty(oldProfile.Inventory.Actors.Intersect(work.Inventory.Actors));
        Assert.Equal(basis.WorldId, work.WorldId);
        Assert.Equal(basis.BranchId, work.BranchId);
        Assert.Equal(basis.Inventory.Actors, work.Inventory.Actors);
        Assert.Equal(basis.Calendar, work.Calendar);
        Assert.Equal(basis.Space.Scope, work.Space.Scope);
        Assert.Equal(basis.Space.Actors.Select(actor => actor.Feet), work.Space.Actors.Select(actor => actor.Feet));
        CountedSourceLot originalWood = Assert.Single(basis.Inventory.Custody.Lots);
        Assert.Equal(originalWood, work.Inventory.Custody.GetLot(originalWood.LotId));
        Assert.Equal(oldBytes, WorldSnapshotCodecV1.Encode(oldProfile));
        Assert.Null(oldProfile.Gathering);
        Assert.Equal((ushort)2, work.ProfileVersion);
    }

    [Fact]
    public void Admits_two_representative_finite_nodes_with_exact_source_swing_and_renewal_counts()
    {
        WorldSnapshot world = Create();
        GatheringSnapshot gathering = world.Gathering!;
        Assert.Equal(2, gathering.Nodes.Count);
        GatherNodeState oak = Assert.Single(gathering.Nodes, node => node.Definition.Kind == GatherNodeKind.Oak);
        GatherNodeState berry = Assert.Single(gathering.Nodes, node => node.Definition.Kind == GatherNodeKind.Berry);
        Assert.Equal(new SubunitPosition(25_984, 83_840, 256), oak.Feet);
        Assert.Equal(new SubunitPosition(27_008, 85_120, 256), berry.Feet);
        Assert.Equal(5, oak.RemainingSwings);
        Assert.Equal(4, berry.RemainingSwings);
        Assert.Equal(160UL, oak.UnitCapacity);
        Assert.Equal(128UL, berry.UnitCapacity);
        Assert.Equal(900UL, oak.Definition.RegrowthDelaySteps);
        Assert.Equal(700UL, berry.Definition.RegrowthDelaySteps);
        Assert.All(gathering.Nodes, node =>
        {
            Assert.Equal(20UL, node.Definition.StumpSteps);
            Assert.Equal(0UL, node.NextUnissuedUnit);
            Assert.Equal(0UL, node.Revision);
            Assert.Null(node.DepletedAtStep);
            var source = Assert.Single(world.Inventory.Sources, source => source.SourceId == node.SourceId);
            Assert.Equal(node.Definition.YieldDefinition, source.Definition);
            Assert.Equal(node.UnitCapacity, source.UnitCapacity);
            Assert.DoesNotContain(world.Inventory.Custody.Lots, lot => lot.Source.SourceId == node.SourceId);
        });
    }

    [Fact]
    public void Supplied_tools_and_original_stock_are_owned_finite_lots_with_explicit_capacity()
    {
        WorldSnapshot world = Create();
        WorldResident player = Player(world);
        WorldResident wren = Assert.Single(world.Residents, resident => resident.ControllerKind == ResidentControllerKind.Host);
        Assert.Equal(298UL, world.Inventory.AdmittedUnitCapacity);
        Assert.Equal(298UL, world.Inventory.Sources.Aggregate(0UL, (total, source) => checked(total + source.UnitCapacity)));
        Assert.Equal(5, world.Inventory.Sources.Count);
        Assert.Equal(3, world.Inventory.Custody.Lots.Count);
        Assert.Equal(10UL, world.Inventory.Custody.GetStoredQuantity(player.InventoryCustodianId));
        Assert.Equal(0UL, world.Inventory.Custody.GetStoredQuantity(wren.InventoryCustodianId));
        Assert.Equal(32UL, world.Inventory.Custody.GetStorage(player.InventoryCustodianId).UnitCapacity);
        Assert.Equal(8UL, world.Inventory.Custody.GetStorage(wren.InventoryCustodianId).UnitCapacity);
        Assert.Equal(2, world.Gathering!.Tools.Count);
        Assert.Equal(new[] { GatherToolKind.Axe, GatherToolKind.Sickle }, world.Gathering.Tools.Select(tool => tool.Kind).Order());
        Assert.All(world.Gathering.Tools, tool =>
        {
            Assert.Equal(1U, tool.Power);
            CountedSourceLot lot = Assert.Single(world.Inventory.Custody.Lots, lot => lot.Definition == tool.Definition);
            Assert.Equal(player.InventoryCustodianId, lot.CustodianId);
            Assert.Equal(1UL, lot.Quantity);
            Assert.Equal(0UL, lot.Source.Start);
            Assert.Equal(1UL, Assert.Single(world.Inventory.Sources, source => source.SourceId == lot.Source.SourceId).UnitCapacity);
        });
    }

    [Fact]
    public void Initial_actor_work_and_node_sites_are_supported_without_advancing_or_opening_dialogue()
    {
        WorldSnapshot world = Create();
        Assert.Equal(0UL, world.Revision);
        Assert.Equal(0UL, world.AdvancingSteps);
        Assert.Equal(0UL, world.Gathering!.AdvancingStep);
        Assert.Equal(world.Inventory.Actors, world.Gathering.Actors.Select(actor => actor.ActorId).Order());
        Assert.All(world.Gathering.Actors, actor => Assert.Equal(0U, actor.Effort));
        Assert.All(world.Gathering.Nodes, node => Assert.Equal(SpatialOccupancy.Clear,
            world.Space.QueryOccupancy(Player(world).ActorId, node.Feet)));
        Assert.Empty(world.Dialogues);
        Assert.False(world.HasReadingHold);
        Assert.All(world.Residents, resident => Assert.Equal((UInt128)0, resident.ExperiencedTime.TotalMilliseconds));
        Assert.Equal("Just me, the rows, and the crows. Suits me.",
            world.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Host).OpeningUtterance!.Text);
    }

    [Fact]
    public void Same_input_is_byte_identical_while_other_seeds_have_distinct_instances_and_stable_definitions()
    {
        WorldSnapshot first = Create();
        WorldSnapshot repeated = Create();
        WorldSnapshot other = Create("river ");
        Assert.Equal(WorldSnapshotCodec.Encode(first), WorldSnapshotCodec.Encode(repeated));
        Assert.NotEqual(first.WorldId, other.WorldId);
        Assert.Equal(first.AdmissionVersion, other.AdmissionVersion);
        Assert.Empty(first.Gathering!.Nodes.Select(node => node.NodeId).Intersect(other.Gathering!.Nodes.Select(node => node.NodeId)));
        Assert.Empty(first.Gathering.Nodes.Select(node => node.SourceId).Intersect(other.Gathering.Nodes.Select(node => node.SourceId)));
        foreach (GatherNodeState node in first.Gathering.Nodes)
        {
            GatherNodeState counterpart = other.Gathering.Nodes.Single(value => value.Definition.Kind == node.Definition.Kind);
            Assert.Equal(node.Definition.DefinitionId, counterpart.Definition.DefinitionId);
            Assert.Equal(node.Definition.Version, counterpart.Definition.Version);
            Assert.Equal(node.Definition.YieldDefinition, counterpart.Definition.YieldDefinition);
        }
    }

    [Fact]
    public void Embedded_and_filesystem_profiles_restore_with_the_complete_world_codec()
    {
        WorldSnapshot embedded = Create();
        WorldSnapshot files = GatheringFarmAdmission.Create(FarmContentFixture.InstalledContentRoot(), "river", "Orin");
        byte[] encoded = WorldSnapshotCodec.Encode(embedded);
        Assert.Equal(encoded, WorldSnapshotCodec.Encode(files));
        WorldSnapshot restored = WorldSnapshotCodec.Decode(encoded);
        Assert.Equal(encoded, WorldSnapshotCodec.Encode(restored));
        Assert.Equal(2, restored.Gathering!.Nodes.Count);
        Assert.Equal(298UL, restored.Inventory.AdmittedUnitCapacity);
        Assert.Contains("OpenFantasy.Content/adventure-farm/admission-v1.json",
            typeof(GatheringFarmAdmission).Assembly.GetManifestResourceNames().Select(name => name.Replace('\\', '/')));
    }

    [Fact]
    public void Exact_unicode_seed_and_name_limits_are_preserved_before_derivation()
    {
        string seed = new('é', 128);
        string name = new('é', 64);
        Assert.Equal("1d160f1ecd419ce5e6371b58aeb4506cdf31b21d4c43efc947591e16282c90bf",
            GatheringFarmInput.Create(seed, name).BaseSeedText);
        Assert.Equal(name, Player(Create(seed, name)).DisplayName);
        WorldSnapshot spaced = Create("river", " Orin ");
        Assert.Equal(Create().WorldId, spaced.WorldId);
        Assert.Equal(" Orin ", Player(spaced).DisplayName);
        Assert.NotEqual(WorldSnapshotCodec.Encode(Create()), WorldSnapshotCodec.Encode(spaced));
    }

    [Fact]
    public void Invalid_user_text_rejects_before_any_filesystem_content_read()
    {
        foreach (string value in new[] { "", " ", "a\nb", "a\0b", "\ud800", new string('a', 257), new string('é', 129) })
        {
            Assert.ThrowsAny<ArgumentException>(() => GatheringFarmAdmission.Create("missing-content", value, "Orin"));
        }
        foreach (string value in new[] { "", "\t", "\ud800", "a\nb", new string('a', 129), new string('é', 65) })
        {
            Assert.ThrowsAny<ArgumentException>(() => GatheringFarmAdmission.Create("missing-content", "river", value));
        }
        Assert.Throws<ArgumentNullException>(() => GatheringFarmAdmission.Create("missing-content", null!, "Orin"));
        Assert.Throws<ArgumentNullException>(() => GatheringFarmAdmission.Create("missing-content", "river", null!));
    }

    [Theory]
    [InlineData("adventure-farm/admission-v1.json")]
    [InlineData("legacy/aldermere-first-loop/records.json")]
    [InlineData("legacy/aldermere-first-loop/closure-manifest.json")]
    [InlineData("legacy/aldermere-first-loop/source-support/src/Sim/World.Gathering.cs.txt")]
    [InlineData("legacy/aldermere-first-loop/source-support/data/items.json")]
    [InlineData("legacy/aldermere-first-loop/assets/units/node_oak.png")]
    public void Tampered_source_or_authored_content_is_rejected(string path)
    {
        using var fixture = new GatheringContentFixture();
        File.AppendAllText(fixture.PathFor(path), " ");
        Assert.ThrowsAny<InvalidDataException>(() => GatheringFarmAdmission.Create(fixture.ContentRoot, "river", "Orin"));
    }

    [Fact]
    public void Forged_local_source_pin_missing_profile_and_oversized_profile_are_rejected()
    {
        using var fixture = new GatheringContentFixture();
        string path = fixture.PathFor("adventure-farm/admission-v1.json");
        byte[] original = File.ReadAllBytes(path);
        JsonObject metadata = JsonNode.Parse(original)!.AsObject();
        metadata["sourceRecords"]![0]!["sourceIdentity"]!["payloadSha256"] = new string('0', 64);
        File.WriteAllText(path, metadata.ToJsonString());
        Assert.Throws<InvalidDataException>(() => GatheringFarmAdmission.Create(fixture.ContentRoot, "river", "Orin"));
        File.WriteAllBytes(path, new byte[16_385]);
        Assert.Throws<InvalidDataException>(() => GatheringFarmAdmission.Create(fixture.ContentRoot, "river", "Orin"));
        File.Delete(path);
        Assert.Throws<FileNotFoundException>(() => GatheringFarmAdmission.Create(fixture.ContentRoot, "river", "Orin"));
    }

    private static WorldSnapshot Create(string seed = "river", string name = "Orin") =>
        GatheringFarmAdmission.CreateEmbedded(seed, name);

    private static WorldResident Player(WorldSnapshot world) =>
        world.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player);

    private sealed class GatheringContentFixture : IDisposable
    {
        private readonly FarmContentFixture _base = new();

        internal GatheringContentFixture()
        {
            string destination = PathFor("adventure-farm");
            Directory.CreateDirectory(destination);
            File.Copy(Path.Combine(FarmContentFixture.InstalledContentRoot(), "adventure-farm", "admission-v1.json"),
                Path.Combine(destination, "admission-v1.json"));
        }

        internal string ContentRoot => _base.ContentRoot;
        internal string PathFor(string relative) => _base.PathFor(relative);
        public void Dispose() => _base.Dispose();
    }
}
