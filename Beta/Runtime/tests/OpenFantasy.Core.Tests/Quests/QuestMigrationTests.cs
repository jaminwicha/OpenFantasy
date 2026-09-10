using System.Buffers.Binary;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Tests.Worlds;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;

namespace OpenFantasy.Core.Tests.Quests;

public sealed class QuestMigrationTests
{
    [Fact]
    public void UpgradeAddsOnlyEmptyProjectsAndFreshComponentsPreservingTheHeldWorld()
    {
        QuestFixture fixture = QuestFixture.Create();
        WorldSnapshot initial = Original(fixture);
        WorldQuestAdmission admission = Admission(fixture);
        WorldQuestMigration request = Request(initial, admission);
        WorldQuestMigrationDecision result = new WorldQuestMigrationProcessor().Decide(initial, request);
        Assert.Equal((ushort)3, initial.ProfileVersion);
        Assert.Equal((ushort)4, result.Candidate.ProfileVersion);
        Assert.Equal(initial.Revision + 1, result.Candidate.Revision);
        Assert.Equal(initial.AdvancingSteps, result.Candidate.AdvancingSteps);
        Assert.Equal(initial.Calendar, result.Candidate.Calendar);
        Assert.True(result.Candidate.HasReadingHold);
        Assert.Same(initial.Space, result.Candidate.Space);
        Assert.Same(initial.Gathering, result.Candidate.Gathering);
        Assert.Same(initial.Production, result.Candidate.Production);
        Assert.Equal(initial.Residents, result.Candidate.Residents);
        Assert.Equal(initial.Dialogues, result.Candidate.Dialogues);
        Assert.Same(admission.Progression, result.Candidate.Progression);
        Assert.Same(admission.Quests, result.Candidate.Quests);
        Assert.Equal(request.TargetAdmissionVersion, result.Candidate.AdmissionVersion);
        Assert.Equal(initial.Inventory.Custody.Revision, result.Candidate.Inventory.Custody.Revision);
        Assert.Equal(initial.Inventory.AdmittedUnitCapacity, result.Candidate.Inventory.AdmittedUnitCapacity);
        Assert.Equal(initial.Inventory.Actors, result.Candidate.Inventory.Actors);
        Assert.Equal(initial.Inventory.Controls, result.Candidate.Inventory.Controls);
        Assert.Equal(initial.Inventory.Sources, result.Candidate.Inventory.Sources);
        Assert.Equal(initial.Inventory.Custody.Lots, result.Candidate.Inventory.Custody.Lots);
        foreach (CustodianStorage old in initial.Inventory.Custody.Storages)
        {
            Assert.Same(old, result.Candidate.Inventory.Custody.GetStorage(old.CustodianId));
        }
        foreach (CustodianAccessPolicy old in initial.Inventory.Policies)
        {
            Assert.Contains(old, result.Candidate.Inventory.Policies);
        }
        Assert.Equal(0UL, result.Candidate.Inventory.Custody.GetStoredQuantity(QuestFixture.Project));
        Assert.Equal(0UL, result.Candidate.Inventory.Custody.GetStorage(QuestFixture.Project).Revision);
        Assert.Contains(QuestFixture.Reservation, result.Candidate.Inventory.ReservationIds);
        Assert.Equal(new[] { Id(2200), Id(2201), Id(2202), Id(2203), Id(2210), Id(2211) }, result.AdmittedDefinitionIds);
        Assert.Equal(new[] { Id(2000), Id(2001), Id(2204), Id(2205), Id(2206) }, result.AdmittedEntityIds);
        Assert.DoesNotContain(Id(10), result.AdmittedEntityIds);
        Assert.DoesNotContain(Material.DefinitionId, result.AdmittedDefinitionIds);
        Assert.Equal(result.Candidate.ContentHash,
            WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(result.Candidate)).ContentHash);
        WorldQuestMigration restored = WorldQuestMigrationCodecV1.Decode(WorldQuestMigrationCodecV1.Encode(request));
        var replayed = new WorldQuestMigrationProcessor().Decide(initial, restored);
        Assert.Equal(WorldQuestMigrationCodecV1.EncodeOutcome(result), WorldQuestMigrationCodecV1.EncodeOutcome(replayed));
    }

    [Fact]
    public void ARealPriorCraftAndAdvancingStepRemainExactlyRetainedThroughUpgrade()
    {
        QuestFixture fixture = QuestFixture.Create(dialogue: false);
        WorldSnapshot basis = Original(fixture);
        WorldStepDecision crafted = new WorldStepProcessor().Decide(basis,
            ProductionWorldFixture.Batch(basis, 3001, ProductionWorldFixture.Craft(basis, 3002)));
        Assert.Equal(WorldActionRejection.None, Assert.Single(crafted.Receipts).Rejection);
        Assert.Single(crafted.Candidate.Production!.Batches);
        Assert.Equal(4UL, crafted.Candidate.Inventory.Custody.GetLot(Id(70)).Quantity);
        WorldQuestAdmission admission = Admission(fixture);
        WorldSnapshot upgraded = new WorldQuestMigrationProcessor().Decide(crafted.Candidate,
            Request(crafted.Candidate, admission)).Candidate;
        Assert.Same(crafted.Candidate.Production, upgraded.Production);
        Assert.Same(crafted.Candidate.Gathering, upgraded.Gathering);
        Assert.Equal(crafted.Candidate.Inventory.Custody.Lots, upgraded.Inventory.Custody.Lots);
        Assert.Equal(crafted.Candidate.Inventory.Sources, upgraded.Inventory.Sources);
        Assert.Equal(crafted.Candidate.Calendar, upgraded.Calendar);
        Assert.Equal(crafted.Candidate.AdvancingSteps, upgraded.AdvancingSteps);
        Assert.Equal(crafted.Candidate.Residents.Select(value => value.ExperiencedTime),
            upgraded.Residents.Select(value => value.ExperiencedTime));
        Assert.Equal(WorldQuestMigration.ComputeTargetAdmissionVersion(basis.AdmissionVersion, admission), upgraded.AdmissionVersion);
    }

    [Fact]
    public void TargetAdmissionPinDependsOnContentAndAdmissionRatherThanTheRetainedHeadProgress()
    {
        QuestFixture fixture = QuestFixture.Create(dialogue: false);
        WorldSnapshot basis = Original(fixture);
        WorldQuestAdmission admission = Admission(fixture);
        WorldSnapshot stepped = new WorldStepProcessor().Decide(basis,
            ProductionWorldFixture.Batch(basis, 3001)).Candidate;
        var first = Request(basis, admission);
        var later = Request(stepped, admission);
        Assert.NotEqual(first.ExpectedHeadHash, later.ExpectedHeadHash);
        Assert.Equal(first.TargetAdmissionVersion, later.TargetAdmissionVersion);
        var changed = new WorldQuestAdmission(ContentHash.Compute("other-manifest"u8),
            admission.Progression, admission.Quests, admission.Projects);
        Assert.NotEqual(first.TargetAdmissionVersion,
            WorldQuestMigration.ComputeTargetAdmissionVersion(basis.AdmissionVersion, changed));
    }

    [Fact]
    public void StaleHashRevisionStepsScopeAndSourcePinCannotReplaceTheCurrentHead()
    {
        QuestFixture fixture = QuestFixture.Create(dialogue: false);
        WorldSnapshot basis = Original(fixture);
        WorldQuestAdmission admission = Admission(fixture);
        WorldQuestMigration request = Request(basis, admission);
        var processor = new WorldQuestMigrationProcessor();
        WorldSnapshot stepped = new WorldStepProcessor().Decide(basis,
            ProductionWorldFixture.Batch(basis, 3001)).Candidate;
        Assert.Throws<InvalidOperationException>(() => processor.Decide(stepped, request));
        Assert.Throws<InvalidOperationException>(() => processor.Decide(basis,
            Request(basis, admission, head: ContentHash.Compute("wrong-head"u8))));
        Assert.Throws<InvalidOperationException>(() => processor.Decide(basis,
            Request(basis, admission, revision: basis.Revision + 1)));
        Assert.Throws<InvalidOperationException>(() => processor.Decide(basis,
            Request(basis, admission, steps: basis.AdvancingSteps + 1)));
        Assert.Throws<InvalidOperationException>(() => processor.Decide(basis,
            Request(basis, admission, source: ContentHash.Compute("wrong-source"u8))));
        Assert.Throws<ArgumentException>(() => new WorldQuestMigration(Id(3000), basis.WorldId, Id(9999), basis.Revision,
            basis.AdvancingSteps, basis.ContentHash, basis.AdmissionVersion, request.TargetAdmissionVersion, admission));
        WorldSnapshot upgraded = processor.Decide(basis, request).Candidate;
        Assert.Throws<InvalidOperationException>(() => processor.Decide(upgraded, Request(upgraded, admission)));
    }

    [Fact]
    public void EveryNewDefinitionEntityAndCauseIsCheckedAgainstHistoricalOccupancy()
    {
        QuestFixture fixture = QuestFixture.Create();
        WorldSnapshot basis = Original(fixture);
        WorldQuestAdmission admission = Admission(fixture);
        WorldQuestMigration request = Request(basis, admission);
        var processor = new WorldQuestMigrationProcessor();
        var accepted = processor.Decide(basis, request);
        EntityId[] additions = [request.CauseId, .. accepted.AdmittedDefinitionIds, .. accepted.AdmittedEntityIds];
        foreach (EntityId occupied in additions)
        {
            Assert.Throws<InvalidOperationException>(() => processor.Decide(basis, request, id => id == occupied));
        }
        foreach (EntityId occupied in accepted.AdmittedDefinitionIds.Concat(accepted.AdmittedEntityIds))
        {
            Assert.Throws<InvalidOperationException>(() => processor.Decide(basis, Request(basis, admission, cause: occupied)));
        }
        Assert.Throws<InvalidOperationException>(() => processor.Decide(basis, Request(basis, admission, cause: Id(10))));
        WorldQuestAdmission aliasesOldStorage = Admission(fixture, project: Id(60));
        Assert.Throws<InvalidOperationException>(() => processor.Decide(basis, Request(basis, aliasesOldStorage)));
        Assert.Equal(2, basis.Inventory.Custody.Storages.Count);
        Assert.Null(basis.Progression);
    }

    [Fact]
    public void ExistingSourcePinsMustMatchAndNewMetadataCannotMintRawSupply()
    {
        QuestFixture fixture = QuestFixture.Create();
        WorldSnapshot basis = Original(fixture);
        DeliveryQuestDefinition original = fixture.Definition;
        var wrongItem = new CountedItemDefinition(original.Item.DefinitionId, ContentHash.Compute("wrong-item-pin"u8));
        var changed = new DeliveryQuestDefinition(original.DefinitionId, original.Key, original.Title, original.Description,
            original.GiverActorId, original.MinimumLevel, wrongItem, original.Quantity, original.DestinationCustodianId,
            original.MaterialReservationId, original.Reward);
        var quests = new QuestSnapshot(basis.WorldId, basis.BranchId, 0, [changed], []);
        WorldQuestAdmission admission = new(ContentHash.Compute("manifest"u8), fixture.Progression, quests,
            [new(QuestFixture.Project, Id(11), 6, QuestFixture.Reservation, CustodyDepositRule.AnyControlledActor)]);
        Assert.Throws<ArgumentException>(() => new WorldQuestMigrationProcessor().Decide(basis, Request(basis, admission)));
        Assert.Equal(6UL, basis.Inventory.Custody.GetLot(Id(70)).Quantity);
        Assert.Null(basis.Quests);
    }

    [Fact]
    public void FreshAdmissionRejectsProgressedRevisionsParticipationsAndIdentityAliases()
    {
        QuestFixture fixture = QuestFixture.Create();
        WorldQuestAdmission admission = Admission(fixture);
        ProgressionSnapshot original = fixture.Progression;
        var revised = new ProgressionSnapshot(original.WorldId, original.BranchId, 1,
            original.Profiles, original.Curves, original.Actors, original.Currency);
        Assert.Throws<ArgumentException>(() => new WorldQuestAdmission(admission.ContentVersion, revised,
            fixture.Quests, admission.Projects));
        var currency = new CurrencyLedger(original.WorldId, original.BranchId, 1, original.Currency.Currency,
            original.Currency.AdmittedSupply, original.Currency.Accounts);
        var currencyRevised = new ProgressionSnapshot(original.WorldId, original.BranchId, 0,
            original.Profiles, original.Curves, original.Actors, currency);
        Assert.Throws<ArgumentException>(() => new WorldQuestAdmission(admission.ContentVersion, currencyRevised,
            fixture.Quests, admission.Projects));
        var participating = new QuestSnapshot(original.WorldId, original.BranchId, 1, fixture.Quests.Definitions,
            [new(Id(10), fixture.Definition.DefinitionId, Id(3009), fixture.World.Calendar)]);
        Assert.Throws<ArgumentException>(() => new WorldQuestAdmission(admission.ContentVersion, original,
            participating, admission.Projects));
        Assert.Throws<ArgumentException>(() => Admission(fixture, project: Id(2204)));
        Assert.Throws<ArgumentException>(() => new WorldQuestAdmission(admission.ContentVersion, original,
            fixture.Quests, admission.Projects.Concat(admission.Projects)));
        Assert.Throws<ArgumentException>(() => new QuestProjectCustody(Id(2000), Id(11), 0, Id(2001), CustodyDepositRule.OwnerOnly));
        Assert.Throws<ArgumentException>(() => new QuestProjectCustody(Id(2000), Id(11), 6, Id(2001), (CustodyDepositRule)99));
    }

    [Fact]
    public void CodecRejectsAllPrefixesWrongVersionsDuplicateProjectsAndOversizedRequests()
    {
        QuestFixture fixture = QuestFixture.Create();
        WorldQuestAdmission admission = Admission(fixture);
        byte[] request = WorldQuestMigrationCodecV1.Encode(Request(Original(fixture), admission));
        byte[] encodedAdmission = WorldQuestMigrationCodecV1.EncodeAdmission(admission);
        for (int length = 0; length < request.Length; length++)
        {
            byte[] prefix = request[..length];
            Assert.ThrowsAny<FormatException>(() => WorldQuestMigrationCodecV1.Decode(prefix));
        }
        for (int length = 0; length < encodedAdmission.Length; length++)
        {
            byte[] prefix = encodedAdmission[..length];
            Assert.ThrowsAny<FormatException>(() => WorldQuestMigrationCodecV1.DecodeAdmission(prefix));
        }
        Assert.ThrowsAny<FormatException>(() => WorldQuestMigrationCodecV1.Decode([.. request, 0]));
        Assert.ThrowsAny<FormatException>(() => WorldQuestMigrationCodecV1.Decode(Change(request, 1, Number(2))));
        Assert.ThrowsAny<FormatException>(() => WorldQuestMigrationCodecV1.Decode(Change(request, 9, new byte[32])));
        Assert.ThrowsAny<FormatException>(() => WorldQuestMigrationCodecV1.Decode(
            new byte[WorldQuestMigrationCodecV1.MaximumMigrationByteLength + 1]));
        Assert.ThrowsAny<FormatException>(() => WorldQuestMigrationCodecV1.DecodeAdmission(
            new byte[WorldQuestMigrationCodecV1.MaximumAdmissionByteLength + 1]));
        var projects = Fields(Fields(encodedAdmission)[5]);
        byte[] duplicate = Frame(projects[0], projects[1], projects[1]);
        Assert.ThrowsAny<FormatException>(() => WorldQuestMigrationCodecV1.DecodeAdmission(Change(encodedAdmission, 5, duplicate)));
        Assert.Equal(256 * 1024, WorldQuestMigrationCodecV1.MaximumMigrationByteLength);
    }

    [Fact]
    public void InitialAdmissionByteLimitRejectsExcessMetadataBeforeMigrationPublication()
    {
        QuestFixture fixture = QuestFixture.Create();
        DeliveryQuestDefinition source = fixture.Definition;
        var definitions = Enumerable.Range(0, QuestSnapshot.MaximumDefinitions).Select(index =>
            new DeliveryQuestDefinition(Id((ulong)(4000 + index)), "quest-" + index,
                new string('t', 256), new string('d', 2048), source.GiverActorId, source.MinimumLevel,
                source.Item, source.Quantity, source.DestinationCustodianId, source.MaterialReservationId, source.Reward));
        var quests = new QuestSnapshot(fixture.World.WorldId, fixture.World.BranchId, 0, definitions, []);
        Assert.Throws<ArgumentException>(() => new WorldQuestAdmission(ContentHash.Compute("large-manifest"u8),
            fixture.Progression, quests,
            [new(QuestFixture.Project, Id(11), 6, QuestFixture.Reservation, CustodyDepositRule.AnyControlledActor)]));
    }

    [Fact]
    public void WorldRevisionExhaustionCannotPartiallyInstallAdmission()
    {
        QuestFixture fixture = QuestFixture.Create();
        WorldSnapshot old = Original(fixture);
        WorldSnapshot maximum = new(old.Inventory, old.Space, old.Residents, old.AdmissionVersion, old.Calendar,
            ulong.MaxValue, old.AdvancingSteps, old.Dialogues, old.Gathering, old.Production);
        Assert.Throws<OverflowException>(() => new WorldQuestMigrationProcessor().Decide(maximum,
            Request(maximum, Admission(fixture))));
        Assert.Null(maximum.Progression);
        Assert.Equal(2, maximum.Inventory.Custody.Storages.Count);
    }

    private static WorldSnapshot Original(QuestFixture fixture)
    {
        WorldSnapshot world = fixture.World;
        CountedAuthorityState old = world.Inventory;
        var inventory = new CountedAuthorityState(new CustodySnapshot(world.WorldId, world.BranchId, old.Custody.Revision,
            old.Custody.Storages.Where(value => value.CustodianId != QuestFixture.Project), old.Custody.Lots),
            old.AdmittedUnitCapacity, old.Actors, old.Controls,
            old.Policies.Where(value => value.CustodianId != QuestFixture.Project), old.Sources,
            old.ReservationIds.Where(value => value != QuestFixture.Reservation));
        return new WorldSnapshot(inventory, world.Space, world.Residents, world.AdmissionVersion,
            world.Calendar, world.Revision, world.AdvancingSteps, world.Dialogues, world.Gathering, world.Production);
    }

    private static WorldQuestAdmission Admission(QuestFixture fixture, EntityId? project = null)
    {
        EntityId destination = project ?? QuestFixture.Project;
        DeliveryQuestDefinition old = fixture.Definition;
        var definition = new DeliveryQuestDefinition(old.DefinitionId, old.Key, old.Title, old.Description,
            old.GiverActorId, old.MinimumLevel, old.Item, old.Quantity, destination, old.MaterialReservationId, old.Reward);
        var quests = new QuestSnapshot(fixture.World.WorldId, fixture.World.BranchId, 0, [definition], []);
        return new WorldQuestAdmission(ContentHash.Compute("quest-manifest-test"u8), fixture.Progression, quests,
            [new(destination, old.GiverActorId, 6, old.MaterialReservationId, CustodyDepositRule.AnyControlledActor)]);
    }

    private static WorldQuestMigration Request(WorldSnapshot state, WorldQuestAdmission admission,
        EntityId? cause = null, ulong? revision = null, ulong? steps = null,
        ContentHash? head = null, ContentHash? source = null)
    {
        ContentHash sourcePin = source ?? state.AdmissionVersion;
        return new WorldQuestMigration(cause ?? Id(3000), state.WorldId, state.BranchId,
            revision ?? state.Revision, steps ?? state.AdvancingSteps, head ?? state.ContentHash, sourcePin,
            WorldQuestMigration.ComputeTargetAdmissionVersion(sourcePin, admission), admission);
    }

    private static List<byte[]> Fields(byte[] frame)
    {
        uint count = BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(8));
        int position = 12;
        List<byte[]> fields = [];
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(position)));
            position += 4;
            fields.Add(frame.AsSpan(position, length).ToArray());
            position += length;
        }
        return fields;
    }

    private static byte[] Change(byte[] original, int field, byte[] value)
    {
        List<byte[]> fields = Fields(original);
        fields[field] = value;
        return Frame(fields.ToArray());
    }

    private static byte[] Frame(params byte[][] fields)
    {
        using var stream = new MemoryStream();
        stream.Write("OFC\0"u8);
        byte[] integer = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(integer, 1);
        stream.Write(integer);
        BinaryPrimitives.WriteUInt32BigEndian(integer, (uint)fields.Length);
        stream.Write(integer);
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(integer, (uint)field.Length);
            stream.Write(integer);
            stream.Write(field);
        }
        return stream.ToArray();
    }

    private static byte[] Number(ulong value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }
}
