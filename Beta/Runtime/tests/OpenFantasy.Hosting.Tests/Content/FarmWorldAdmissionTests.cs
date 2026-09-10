using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Language;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.Content;

public sealed class FarmWorldAdmissionTests
{
    [Fact]
    public void Creates_source_wren_and_authored_player_inside_the_actual_farm_terrain()
    {
        WorldSnapshot world = Create();
        WorldResident wren = Assert.Single(world.Residents, resident => resident.ControllerKind == ResidentControllerKind.Host);
        WorldResident player = Assert.Single(world.Residents, resident => resident.ControllerKind == ResidentControllerKind.Player);
        Assert.Equal("Wren", wren.DisplayName);
        Assert.Equal("Orin", player.DisplayName);
        Assert.Equal(new SubunitPosition(26496, 82560, 256), wren.HomeFeet);
        Assert.Equal(new SubunitPosition(25984, 83328, 256), player.HomeFeet);
        Assert.Equal(2, wren.WanderRadiusCells);
        Assert.Equal(0, player.WanderRadiusCells);
        Assert.Equal("Just me, the rows, and the crows. Suits me.", wren.OpeningUtterance!.Text);
        Assert.Null(player.OpeningUtterance);
        Assert.Equal(SpatialInteraction.OutOfRange, world.Space.QueryInteraction(player.ActorId, wren.ActorId));
        TerrainRegionSnapshot terrain = Assert.Single(world.Space.Regions);
        Assert.Equal(new CellCoordinate(96, 320, 0), terrain.Bounds.Minimum);
        Assert.Equal(new CellCoordinate(127, 351, 15), terrain.Bounds.Maximum);
        Assert.Equal(16384, terrain.Cells.Count);
        Assert.Equal(1024, terrain.Cells.Count(cell => cell.IsSolid));
        Assert.All(terrain.Cells.Where(cell => cell.IsSolid), cell => Assert.Equal(0, cell.Coordinate.Z));
        Assert.All(world.Space.Actors, actor => Assert.Equal(SpatialOccupancy.Clear, world.Space.QueryOccupancy(actor.ActorId, actor.Feet)));
        Assert.Equal(world.Space.Actors[0].HomePlaceId, world.Space.Actors[1].HomePlaceId);
    }

    [Fact]
    public void Both_actors_have_full_shared_anatomy_and_individually_admitted_spoken_comprehension()
    {
        WorldSnapshot world = Create();
        BodyDefinition body = world.Space.Actors[0].Condition.Definition;
        Assert.Same(body, world.Space.Actors[1].Condition.Definition);
        Assert.Equal(new BodyDimensions(180, 140, 464), body.ClearanceDimensions);
        Assert.Equal(15, body.Parts.Count);
        Assert.All(world.Space.Actors, actor =>
        {
            Assert.True(actor.Condition.SupportsFunction(BodyFunction.Vocalization));
            Assert.True(actor.Condition.SupportsFunction(BodyFunction.Hearing));
            Assert.True(actor.Condition.SupportsFunction(BodyFunction.Sight));
            Assert.True(actor.Condition.SupportsFunction(BodyFunction.Manipulation));
            Assert.True(actor.Condition.CanLocomote(LocomotionMode.Ground));
            Assert.Empty(actor.Condition.Injuries);
        });
        Assert.Equal(world.Residents[0].Comprehension.RulesVersion, world.Residents[1].Comprehension.RulesVersion);
        foreach (WorldResident resident in world.Residents)
        {
            LanguageComprehensionEntry entry = Assert.Single(resident.Comprehension.Entries);
            Assert.Equal(resident.ActorId, resident.Comprehension.ActorId);
            Assert.Equal(CommunicationMode.Spoken, entry.Channel.Mode);
            Assert.Equal(ComprehensionBand.Conversational, entry.Band);
            Assert.True(resident.Comprehension.Query(resident.ActorId, resident.Comprehension.RulesVersion,
                new ComprehensionRequirement(entry.Channel, entry.DefinitionVersion, ComprehensionBand.Conversational)).MeetsBandRequirement);
            foreach (CommunicationMode other in new[] { CommunicationMode.Signed, CommunicationMode.Written })
            {
                var otherChannel = new LanguageChannelKey(entry.Channel.LanguageId, entry.Channel.RegisterId, other);
                ComprehensionQueryResult query = resident.Comprehension.Query(resident.ActorId, resident.Comprehension.RulesVersion,
                    new ComprehensionRequirement(otherChannel, entry.DefinitionVersion, ComprehensionBand.Practical));
                Assert.False(query.HasRecordedEntry);
                Assert.Equal(ComprehensionBand.Unfamiliar, query.AvailableBand);
                Assert.False(query.MeetsBandRequirement);
            }
            WorldResident otherResident = world.Residents.Single(other => other.ActorId != resident.ActorId);
            Assert.Throws<ArgumentException>(() => resident.Comprehension.Query(otherResident.ActorId, resident.Comprehension.RulesVersion,
                new ComprehensionRequirement(entry.Channel, entry.DefinitionVersion, ComprehensionBand.Practical)));
        }
    }

    [Fact]
    public void Declares_one_finite_player_owned_eight_wood_source_and_explicit_control_for_both_residents()
    {
        WorldSnapshot world = Create();
        WorldResident player = world.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player);
        WorldResident wren = world.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Host);
        var lot = Assert.Single(world.Inventory.Custody.Lots);
        AdmittedCountedSource source = Assert.Single(world.Inventory.Sources);
        Assert.Equal(8UL, lot.Quantity);
        Assert.Equal(8UL, source.UnitCapacity);
        Assert.Equal(8UL, world.Inventory.AdmittedUnitCapacity);
        Assert.Equal(source.SourceId, lot.Source.SourceId);
        Assert.Equal(0UL, lot.Source.Start);
        Assert.Equal(8UL, lot.Source.End);
        Assert.Equal(player.InventoryCustodianId, lot.CustodianId);
        Assert.DoesNotContain(world.Inventory.Custody.Lots, value => value.CustodianId == wren.InventoryCustodianId);
        Assert.Empty(world.Inventory.ReservationIds);
        Assert.Equal(2, world.Inventory.Controls.Count);
        foreach (WorldResident resident in world.Residents)
        {
            ActorControlBinding control = world.Inventory.Controls.Single(value => value.ActorId == resident.ActorId);
            Assert.True(world.Inventory.CanControl(new TrustedCommandDelivery(control.PrincipalId, control.SessionId), resident.ActorId));
            ActorControlBinding other = world.Inventory.Controls.Single(value => value.ActorId != resident.ActorId);
            Assert.False(world.Inventory.CanControl(new TrustedCommandDelivery(other.PrincipalId, other.SessionId), resident.ActorId));
            Assert.Equal(resident.ActorId, world.Inventory.Policies.Single(value => value.CustodianId == resident.InventoryCustodianId).OwnerActorId);
        }
        Assert.Equal(CustodyDepositRule.OwnerOnly, world.Inventory.Policies.Single(value => value.CustodianId == player.InventoryCustodianId).DepositRule);
        Assert.Equal(CustodyDepositRule.AnyControlledActor, world.Inventory.Policies.Single(value => value.CustodianId == wren.InventoryCustodianId).DepositRule);
        EntityId[] instances = InstanceIdentities(world);
        Assert.Equal(instances.Length, instances.Distinct().Count());
    }

    [Fact]
    public void Begins_at_nine_am_with_no_elapsed_time_revisions_or_active_dialogue()
    {
        WorldSnapshot world = Create();
        Assert.Equal(new WorldInstant(0, 32_400_000), world.Calendar);
        Assert.Equal(0UL, world.Revision);
        Assert.Equal(0UL, world.AdvancingSteps);
        Assert.Equal(0UL, world.Inventory.Custody.Revision);
        Assert.Empty(world.Dialogues);
        Assert.False(world.HasReadingHold);
        Assert.All(world.Residents, resident => Assert.Equal((UInt128)0, resident.ExperiencedTime.TotalMilliseconds));
        Assert.All(world.Space.Actors, actor => Assert.Equal(0UL, actor.Revision));
        Assert.All(world.Space.Regions, region => Assert.Equal(0UL, region.Revision));
    }

    [Fact]
    public void Same_exact_inputs_produce_identical_state_and_seed_changes_only_instance_lineage()
    {
        WorldSnapshot first = Create("river", "Orin");
        WorldSnapshot repeated = Create("river", "Orin");
        WorldSnapshot otherSeed = Create("river ", "Orin");
        Assert.Equal(WorldSnapshotCodecV1.Encode(first), WorldSnapshotCodecV1.Encode(repeated));
        Assert.NotEqual(first.WorldId, otherSeed.WorldId);
        Assert.Empty(InstanceIdentities(first).Intersect(InstanceIdentities(otherSeed)));
        Assert.Equal(first.AdmissionVersion, otherSeed.AdmissionVersion);
        Assert.Equal(first.Space.Actors[0].Binding.DefinitionId, otherSeed.Space.Actors[0].Binding.DefinitionId);
        Assert.Equal(first.Space.Actors[0].Binding.DefinitionVersion, otherSeed.Space.Actors[0].Binding.DefinitionVersion);
        Assert.Equal(first.Inventory.Sources[0].Definition, otherSeed.Inventory.Sources[0].Definition);
        Assert.Equal(first.Space.Regions[0].Rules.Version, otherSeed.Space.Regions[0].Rules.Version);
        Assert.Equal(first.Residents[0].Comprehension.Entries[0].Channel, otherSeed.Residents[0].Comprehension.Entries[0].Channel);
        Assert.NotEqual(first.Space.Regions[0].Cells.First(cell => cell.IsSolid).SourceId,
            otherSeed.Space.Regions[0].Cells.First(cell => cell.IsSolid).SourceId);
    }

    [Fact]
    public void Full_world_codec_restores_admitted_presence_body_language_and_finite_custody()
    {
        WorldSnapshot original = Create();
        byte[] bytes = WorldSnapshotCodecV1.Encode(original);
        WorldSnapshot restored = WorldSnapshotCodecV1.Decode(bytes);
        Assert.Equal(bytes, WorldSnapshotCodecV1.Encode(restored));
        Assert.Equal(original.AdmissionVersion, restored.AdmissionVersion);
        Assert.Equal(8UL, Assert.Single(restored.Inventory.Custody.Lots).Quantity);
        foreach (ActorPresence actor in restored.Space.Actors)
        {
            Assert.True(actor.Condition.SupportsFunction(BodyFunction.Vocalization));
            Assert.Equal(original.Space.GetActor(actor.ActorId).Feet, actor.Feet);
            Assert.Equal(SpatialOccupancy.Clear, restored.Space.QueryOccupancy(actor.ActorId, actor.Feet));
        }
        WorldResident wren = restored.Residents.Single(resident => resident.DisplayName == "Wren");
        Assert.Equal("Just me, the rows, and the crows. Suits me.", wren.OpeningUtterance!.Text);
        Assert.Empty(restored.Dialogues);
    }

    [Fact]
    public void Preserves_exact_names_and_accepts_utf8_limits_without_normalizing_seed_or_name()
    {
        WorldSnapshot world = Create(new string('é', 128), new string('é', 64));
        Assert.Equal(new string('é', 64), world.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player).DisplayName);
        WorldSnapshot spaced = Create("river", " Orin ");
        WorldSnapshot original = Create("river", "Orin");
        Assert.Equal(original.WorldId, spaced.WorldId);
        Assert.Equal(" Orin ", spaced.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player).DisplayName);
        Assert.NotEqual(ContentHash.Compute(WorldSnapshotCodecV1.Encode(original)), ContentHash.Compute(WorldSnapshotCodecV1.Encode(spaced)));
    }

    [Fact]
    public void Rejects_invalid_or_oversized_text_before_any_content_read()
    {
        foreach (string invalid in new[] { "", " ", "a\nb", "a\0b", "\ud800", new string('a', 257), new string('é', 129) })
        {
            Assert.ThrowsAny<ArgumentException>(() => FarmWorldAdmission.Create("missing-content", invalid, "Orin"));
        }
        foreach (string invalid in new[] { "", "\t", "a\nb", "\ud800", new string('a', 129), new string('é', 65) })
        {
            Assert.ThrowsAny<ArgumentException>(() => FarmWorldAdmission.Create("missing-content", "river", invalid));
        }
        Assert.Throws<ArgumentNullException>(() => FarmWorldAdmission.Create("missing-content", null!, "Orin"));
        Assert.Throws<ArgumentNullException>(() => FarmWorldAdmission.Create("missing-content", "river", null!));
        Assert.Throws<ArgumentException>(() => FarmWorldAdmission.Create(" ", "river", "Orin"));
    }

    [Fact]
    public void Selected_payload_hashes_match_independent_frozen_source_pins()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(FarmContentFixture.InstalledContentRoot(),
            "legacy", "aldermere-first-loop", "records.json")));
        var pins = new Dictionary<string, string>
        {
            ["farmer_wren"] = "f0374a9cd788d14de3c45233cf31c6b67904ed209a6a5f8d1a0b8de547dd1f30",
            ["farm_verdance_small"] = "fc79c253de2d851941d669687dfb8253e79d37f17f74dfce4ce9023f75d7c884",
            ["wood"] = "613f7643e208f8da2a49c4fac27cc3671a6b6fbc1b1a0e39a7e3f0f29af0505d",
            ["wren_orchard"] = "bbb996bb2ca6ca4e27b942f7f40f9ea624813909e037ec86980ed8aaa4ab86cb"
        };
        foreach ((string id, string pin) in pins)
        {
            JsonElement record = document.RootElement.GetProperty("records").EnumerateArray().Single(record =>
                record.GetProperty("legacyId").ValueKind == JsonValueKind.String && record.GetProperty("legacyId").GetString() == id);
            Assert.Equal(pin, FarmCanonicalJson.Hash(record.GetProperty("sourcePayload")).Value);
        }
    }

    [Fact]
    public void Embedded_content_creates_the_same_exact_world_as_verified_filesystem_content()
    {
        WorldSnapshot files = Create();
        WorldSnapshot embedded = FarmWorldAdmission.CreateEmbedded("river", "Orin");
        Assert.Equal(WorldSnapshotCodecV1.Encode(files), WorldSnapshotCodecV1.Encode(embedded));
        string[] names = typeof(FarmWorldAdmission).Assembly.GetManifestResourceNames()
            .Select(name => name.Replace('\\', '/')).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        foreach (string path in new[] { "native-farm/admission-v1.json", "native-farm/presentation-v1.json",
            "packages/aldermere-first-loop.manifest.json", "legacy/aldermere-first-loop/records.json",
            "legacy/aldermere-first-loop/source-support/data/npcs.json", "legacy/aldermere-first-loop/assets/layers/body.png",
            "legacy/aldermere-first-loop/source-support/src/Client/SpriteComposer.cs.txt",
            "legacy/aldermere-first-loop/source-support/src/Sim/Appearance.cs.txt" })
        {
            Assert.Contains("OpenFantasy.Content/" + path, names);
        }
    }

    private static WorldSnapshot Create(string seed = "river", string name = "Orin") =>
        FarmWorldAdmission.Create(FarmContentFixture.InstalledContentRoot(), seed, name);

    private static EntityId[] InstanceIdentities(WorldSnapshot world) =>
        [world.WorldId, world.BranchId, world.Space.Scope.RealmId, world.Space.Actors[0].HomePlaceId,
            .. world.Space.Actors.SelectMany(actor => new[] { actor.ActorId, actor.Binding.FormInstanceId, actor.Binding.PhysicalBodyId!.Value }),
            .. world.Inventory.Controls.SelectMany(control => new[] { control.PrincipalId, control.SessionId }),
            .. world.Inventory.Custody.Storages.Select(storage => storage.CustodianId),
            .. world.Inventory.Sources.Select(source => source.SourceId),
            .. world.Inventory.Custody.Lots.Select(lot => lot.LotId)];
}
