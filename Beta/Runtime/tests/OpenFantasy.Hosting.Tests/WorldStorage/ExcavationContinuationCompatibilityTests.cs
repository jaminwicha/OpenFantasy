using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ExcavationContinuationCompatibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Actual_legacy_player_and_npc_work_retain_exact_archives(bool npc)
    {
        var fixture = new ExcavationWorldFixture(npc: npc);
        var world = fixture.Genesis;
        Save("initial", world);
        world = fixture.Earn(world);
        var partial = fixture.Step(world, fixture.Command(world));
        Assert.Equal(WorldActionRejection.None, Assert.Single(partial.Receipts).Rejection);
        world = partial.Candidate;
        Assert.Equal(1UL, Assert.Single(world.Excavation!.Sites).CompletedWork);
        Assert.DoesNotContain(world.Inventory.Custody.Lots, lot => lot.Source.SourceId == fixture.Site.CountedSourceId);
        Save("partial", world);
        for (int work = 1; work < 3; work++)
        {
            world = fixture.Earn(world);
            var decision = fixture.Step(world, fixture.Command(world));
            Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
            world = decision.Candidate;
        }
        Assert.True(Assert.Single(world.Excavation!.Sites).IsComplete);
        var extracted = Assert.Single(world.Inventory.Custody.Lots, lot => lot.Source.SourceId == fixture.Site.CountedSourceId);
        Assert.Equal(new SourceInterval(fixture.Site.CountedSourceId, 0, fixture.Site.Quantity), extracted.Source);
        Assert.True(world.Space.Regions.Single(region => region.Bounds.Contains(fixture.Site.Cell)).GetCell(fixture.Site.Cell).IsExcavated);
        Save("complete", world);

        void Save(string stage, WorldSnapshot value)
        {
            byte[] worldBytes = WorldSnapshotCodec.Encode(value);
            Assert.Equal(worldBytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(worldBytes)));
            byte[] excavationV1 = ExcavationCodecV1.Encode(value.Excavation!);
            byte[] excavationV2 = ExcavationCodecV2.Encode(value.Excavation!);
            Assert.Equal(excavationV1, ExcavationCodecV1.Encode(ExcavationCodecV1.Decode(excavationV1)));
            Assert.Equal(excavationV2, ExcavationCodecV2.Encode(ExcavationCodecV2.Decode(excavationV2)));
            string? folder = Environment.GetEnvironmentVariable("OPENFANTASY_EXCAVATION_EVIDENCE");
            if (folder is null) return;
            Directory.CreateDirectory(folder);
            string prefix = Path.Combine(folder, (npc ? "npc" : "player") + "-" + stage);
            File.WriteAllBytes(prefix + "-world.bin", worldBytes);
            File.WriteAllBytes(prefix + "-excavation-v1.bin", excavationV1);
            File.WriteAllBytes(prefix + "-excavation-v2.bin", excavationV2);
            File.WriteAllBytes(prefix + "-production.bin", ProductionCodecV1.Encode(value.Production!));
        }
    }
}
