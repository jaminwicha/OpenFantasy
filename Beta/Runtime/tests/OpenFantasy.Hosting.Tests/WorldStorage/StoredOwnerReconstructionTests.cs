using System.Buffers.Binary;
using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Construction.Ownership;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed partial class SeparatedTerrainStorageTests
{
    [Fact]
    public void Stored_owners_reopen_partial_work_then_complete_the_same_saved_project()
    {
        using var files = new StorageFiles();
        var ready = Start(files.Path, typed: true);
        byte[] partial;
        using (var authority = ready.Authority)
        {
            var locality = ready.Locality;
            for (int i = 0; i < 40; i++) locality = Work(authority, ready.Fixture, locality).Candidate;
            partial = WorldSnapshotCodec.Encode(authority.ReadStateForHost());
        }
        byte[] final;
        using (var reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            var world = reopened.ReadStateForHost();
            Assert.Equal(partial, WorldSnapshotCodec.Encode(world));
            Assert.Equal(ready.GenesisHash, reopened.ReadRecordedStateForHost(0).State.ContentHash);
            var directory = ConstructionOwnerDirectory.Capture(world);
            var payloads = directory.Owners.ToDictionary(value => value.Hash, value => value.CopyBytes());
            int ownerReads = 0, terrainReads = 0;
            var independentlyDecoded = ConstructionOwnerReconstruction.Decode(directory.CopyBytes(), directory.Hash,
                hash => { ownerReads++; return payloads[hash]; },
                reference => { terrainReads++; return world.Space.Regions.Single(value => TerrainRegionReference.Capture(value) == reference); });
            Assert.Equal(directory.Owners.Count, ownerReads);
            Assert.Equal(world.Space.Regions.Count, terrainReads);
            Assert.Equal(partial, WorldSnapshotCodec.Encode(independentlyDecoded));
            Assert.NotSame(world.Inventory, independentlyDecoded.Inventory);
            Assert.NotSame(world.Construction, independentlyDecoded.Construction);
            var locality = reopened.EvictConstructionTerrain(world.Construction!.Projects.Single().ProjectId, 512);
            for (int i = 40; i < 200; i++) locality = Work(reopened, ready.Fixture, locality).Candidate;
            var completed = reopened.ReadStateForHost();
            AssertCompleted(completed);
            final = WorldSnapshotCodec.Encode(completed);
            Assert.Equal(final, WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost(completed.Revision).State));
        }
        using var recovered = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(final, WorldSnapshotCodec.Encode(recovered.ReadStateForHost()));
    }

    [Theory]
    [InlineData("missing-owner")]
    [InlineData("duplicate-entry")]
    [InlineData("reordered-entries")]
    [InlineData("unknown-role")]
    [InlineData("unsupported-profile")]
    [InlineData("wrong-world")]
    [InlineData("wrong-branch")]
    [InlineData("wrong-subject")]
    [InlineData("wrong-role")]
    [InlineData("extra-payload-field")]
    [InlineData("wrong-realm")]
    [InlineData("missing-resident")]
    [InlineData("construction-membership")]
    [InlineData("invalid-capacity")]
    [InlineData("invalid-time")]
    [InlineData("payload-hash")]
    public void Rehashed_owner_mutations_cannot_bypass_complete_admission(string mutation)
    {
        var world = ConstructionFarmAdmission.CreateGenesis(new ConstructionUpgradeFixture().QuestGenesis());
        var directory = ConstructionOwnerDirectory.Capture(world);
        var payloads = directory.Owners.ToDictionary(value => value.Hash, value => value.CopyBytes());
        var root = OwnerFrameFields(directory.CopyBytes());
        var entries = OwnerFrameFields(root[8]);
        if (mutation == "missing-owner") entries.RemoveAt(1);
        else if (mutation == "duplicate-entry") entries.Insert(1, entries[1]);
        else if (mutation == "reordered-entries") (entries[1], entries[2]) = (entries[2], entries[1]);
        else if (mutation == "unknown-role")
        {
            var entry = OwnerFrameFields(entries[1]); entry[1] = OwnerNumber(999); entries[1] = OwnerFrame(entry);
        }
        else if (mutation == "unsupported-profile") root[2] = OwnerNumber(41);
        else
        {
            var kind = mutation is "wrong-realm" or "missing-resident" ? ConstructionOwnerKind.Actor
                : mutation == "construction-membership" ? ConstructionOwnerKind.ConstructionCatalog
                : mutation == "invalid-time" ? ConstructionOwnerKind.Timing : ConstructionOwnerKind.InventoryBasis;
            int index = entries.FindIndex(1, value => BinaryPrimitives.ReadUInt64BigEndian(OwnerFrameFields(value)[1]) == (ulong)kind);
            Assert.True(index > 0);
            var entry = OwnerFrameFields(entries[index]);
            var oldHash = new ContentHash(Convert.ToHexString(entry[3]).ToLowerInvariant());
            var fields = OwnerFrameFields(payloads[oldHash]);
            byte[] alienId = Enumerable.Repeat((byte)0x7e, 16).ToArray();
            switch (mutation)
            {
                case "wrong-world": fields[3] = alienId; break;
                case "wrong-branch": fields[4] = alienId; break;
                case "wrong-subject": fields[5] = alienId; break;
                case "wrong-role": fields[2] = OwnerNumber(999); break;
                case "extra-payload-field": fields.Add([]); break;
                case "wrong-realm": fields[6] = alienId; break;
                case "missing-resident": fields[8] = OwnerTaggedFrame("world.residents"); break;
                case "construction-membership": fields[7] = OwnerTaggedFrame("owner.construction-keys"); break;
                case "invalid-capacity": fields[7] = OwnerNumber(0); break;
                case "invalid-time": fields[7] = OwnerNumber(WorldInstant.MillisecondsPerDay); break;
                case "payload-hash": fields[6][0] ^= 0xff; break;
                default: throw new InvalidOperationException(mutation);
            }
            byte[] changed = OwnerFrame(fields);
            var hash = mutation == "payload-hash" ? oldHash : ContentHash.Compute(changed);
            payloads[hash] = changed;
            entry[3] = hash.ToByteArray(); entries[index] = OwnerFrame(entry);
        }
        root[8] = OwnerFrame(entries);
        byte[] bytes = OwnerFrame(root);
        Assert.ThrowsAny<FormatException>(() => ConstructionOwnerReconstruction.Decode(bytes, ContentHash.Compute(bytes),
            hash => payloads[hash], reference => world.Space.Regions.Single(value => TerrainRegionReference.Capture(value) == reference)));
    }

    [Fact]
    public void Directory_binding_refuses_stale_history_before_reading_any_payload()
    {
        var world = ConstructionFarmAdmission.CreateGenesis(new ConstructionUpgradeFixture().QuestGenesis());
        var directory = ConstructionOwnerDirectory.Capture(world);
        var fields = OwnerFrameFields(directory.CopyBytes());
        fields[6] = OwnerNumber(world.Revision + 1);
        byte[] stale = OwnerFrame(fields);
        int reads = 0;
        Assert.Throws<FormatException>(() => ConstructionOwnerReconstruction.Decode(stale, directory.Hash,
            _ => { reads++; throw new InvalidOperationException("Must not load unbound owners"); },
            _ => throw new InvalidOperationException("Must not load unbound terrain")));
        Assert.Equal(0, reads);
    }

    [Fact]
    public void Loaded_terrain_must_match_the_directory_scope_and_hash()
    {
        var fixture = new ConstructionUpgradeFixture();
        var world = ConstructionFarmAdmission.CreateGenesis(fixture.QuestGenesis());
        var other = ConstructionFarmAdmission.CreateGenesis(fixture.QuestGenesis("foreign-owner-world"));
        var directory = ConstructionOwnerDirectory.Capture(world);
        var payloads = directory.Owners.ToDictionary(value => value.Hash, value => value.CopyBytes());
        Assert.Throws<FormatException>(() => ConstructionOwnerReconstruction.Decode(directory.CopyBytes(), directory.Hash,
            hash => payloads[hash], _ => other.Space.Regions[0]));
    }

    private static List<byte[]> OwnerFrameFields(byte[] bytes)
    {
        Assert.True(bytes.AsSpan(0, 4).SequenceEqual("OFC\0"u8));
        uint count = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8));
        int offset = 12;
        List<byte[]> fields = [];
        for (uint i = 0; i < count; i++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
            offset += 4;
            fields.Add(bytes.AsSpan(offset, length).ToArray());
            offset += length;
        }
        Assert.Equal(bytes.Length, offset);
        return fields;
    }

    private static byte[] OwnerNumber(ulong value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] OwnerTaggedFrame(string tag) => OwnerFrame([System.Text.Encoding.UTF8.GetBytes(tag)]);

    private static byte[] OwnerFrame(List<byte[]> fields)
    {
        byte[] bytes = new byte[12 + fields.Sum(value => 4 + value.Length)];
        "OFC\0"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), (uint)fields.Count);
        int offset = 12;
        foreach (var field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset), (uint)field.Length);
            field.CopyTo(bytes, offset + 4);
            offset += 4 + field.Length;
        }
        return bytes;
    }
}
