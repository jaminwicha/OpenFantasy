using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Construction.ConstructionWorldFixture;

namespace OpenFantasy.Core.Tests.Construction;

public sealed class ConstructionMigrationTests
{
    [Fact]
    public void Upgrade_preserves_real_delivery_reward_and_clocks_while_explicitly_adding_one_kit()
    {
        WorldSnapshot basis = Basis(held: true);
        WorldConstructionMigration request = Request(basis);
        WorldConstructionMigrationDecision decision = new WorldConstructionMigrationProcessor().Decide(basis, request);
        WorldSnapshot candidate = decision.Candidate;
        Assert.Equal((ushort)5, candidate.ProfileVersion);
        Assert.Equal(basis.Revision + 1, candidate.Revision);
        Assert.Equal(basis.AdvancingSteps, candidate.AdvancingSteps);
        Assert.Equal(basis.Calendar, candidate.Calendar);
        Assert.Equal(basis.Dialogues, candidate.Dialogues);
        Assert.Equal(basis.Space.Actors, candidate.Space.Actors);
        Assert.Equal(basis.Gathering!.ContentHash, candidate.Gathering!.ContentHash);
        Assert.Equal(basis.Progression!.ContentHash, candidate.Progression!.ContentHash);
        Assert.Equal(basis.Quests!.ContentHash, candidate.Quests!.ContentHash);
        Assert.Equal(basis.Production!.Revision, candidate.Production!.Revision);
        Assert.Equal(basis.Production.Batches, candidate.Production.Batches);
        Assert.Equal(basis.Production.Lots, candidate.Production.Lots);
        Assert.Equal(basis.Inventory.Custody.Revision + 1, candidate.Inventory.Custody.Revision);
        Assert.All(basis.Inventory.Custody.Lots, lot => Assert.Contains(lot, candidate.Inventory.Custody.Lots));
        Assert.All(basis.Inventory.Sources, source => Assert.Contains(source, candidate.Inventory.Sources));
        Assert.Equal(basis.Inventory.AdmittedUnitCapacity + 1, candidate.Inventory.AdmittedUnitCapacity);
        var bag = basis.GetResident(Builder).InventoryCustodianId;
        Assert.Equal(basis.Inventory.Custody.GetStorage(bag).UnitCapacity + 1, candidate.Inventory.Custody.GetStorage(bag).UnitCapacity);
        Assert.Equal(basis.Inventory.Custody.GetStorage(bag).Revision + 1, candidate.Inventory.Custody.GetStorage(bag).Revision);
        Assert.Equal(0UL, candidate.Construction!.Revision);
        Assert.Empty(candidate.Space.Structures);
        Assert.Equal(candidate.ContentHash, WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(candidate)).ContentHash);
    }

    [Fact]
    public void Every_new_identity_is_checked_against_history_before_any_admission()
    {
        WorldSnapshot basis = Basis();
        var request = Request(basis);
        var processor = new WorldConstructionMigrationProcessor();
        var decision = processor.Decide(basis, request);
        foreach (var id in decision.AdmittedDefinitionIds.Concat(decision.AdmittedEntityIds).Append(request.CauseId))
        {
            Assert.Throws<InvalidOperationException>(() => processor.Decide(basis, request, value => value == id));
        }
        Assert.Throws<InvalidOperationException>(() => processor.Decide(decision.Candidate, request));
        Assert.Empty(basis.Space.Structures);
        Assert.DoesNotContain(basis.Inventory.Custody.Lots, value => value.LotId == KitLot);
    }

    [Fact]
    public void Exact_head_and_pins_cannot_be_substituted()
    {
        WorldSnapshot basis = Basis();
        var original = Request(basis);
        var wrong = new WorldConstructionMigration(original.CauseId, original.WorldId, original.BranchId,
            original.ExpectedRevision, original.ExpectedAdvancingSteps, ContentHash.Compute("wrong head"u8),
            original.SourceAdmissionVersion, original.TargetAdmissionVersion, original.Admission);
        Assert.Throws<InvalidOperationException>(() => new WorldConstructionMigrationProcessor().Decide(basis, wrong));
        Assert.Throws<ArgumentException>(() => new WorldConstructionMigration(original.CauseId, original.WorldId, original.BranchId,
            original.ExpectedRevision, original.ExpectedAdvancingSteps, original.ExpectedHeadHash,
            original.SourceAdmissionVersion, ContentHash.Compute("wrong target"u8), original.Admission));
        Assert.Throws<ArgumentException>(() => WorldSnapshotCodecV4.Encode(Create()));
    }

    [Fact]
    public void Every_truncation_and_oversized_admission_fails_closed()
    {
        var request = Request(Basis());
        byte[] bytes = WorldConstructionMigrationCodecV1.Encode(request);
        var restored = WorldConstructionMigrationCodecV1.Decode(bytes);
        Assert.Equal(bytes, WorldConstructionMigrationCodecV1.Encode(restored));
        for (int length = 0; length < bytes.Length; length++)
        {
            byte[] prefix = bytes[..length];
            Assert.ThrowsAny<FormatException>(() => WorldConstructionMigrationCodecV1.Decode(prefix));
        }
        Assert.ThrowsAny<FormatException>(() => WorldConstructionMigrationCodecV1.Decode(
            new byte[WorldConstructionMigrationCodecV1.MaximumMigrationByteLength + 1]));
        Assert.ThrowsAny<FormatException>(() => WorldConstructionMigrationCodecV1.Decode([.. bytes, 0]));
        byte[] wrongCount = (byte[])bytes.Clone();
        wrongCount[8] = 255;
        Assert.ThrowsAny<FormatException>(() => WorldConstructionMigrationCodecV1.Decode(wrongCount));
    }
}
