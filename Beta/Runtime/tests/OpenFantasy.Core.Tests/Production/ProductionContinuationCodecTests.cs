using System.Buffers.Binary;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using static OpenFantasy.Core.Tests.Production.InstantCraftFixture;

namespace OpenFantasy.Core.Tests.Production;

public sealed class ProductionContinuationCodecTests
{
    [Fact]
    public void Continued_bytes_require_the_exact_supplied_context_and_legacy_codecs_keep_their_contracts()
    {
        var journey = ProductionContinuationFixture.Crafted();
        var prior = journey.Production;
        byte[] oldBytes = ProductionCodecV2.Encode(prior);
        journey.Cut();
        var context = journey.Production.Continuation!;
        byte[] bytes = ProductionCodecV3.Encode(journey.Production);
        var separatelyCaptured = prior.Continue(journey.Inventory).Continuation!;
        Assert.Equal(context.ContentHash, separatelyCaptured.ContentHash);
        Assert.Equal(bytes, ProductionCodecV3.Encode(ProductionCodecV3.Decode(bytes, separatelyCaptured)));
        Assert.Throws<ArgumentNullException>(() => ProductionCodecV3.Decode(bytes, null!));
        Assert.Throws<ArgumentException>(() => ProductionCodecV1.Encode(journey.Production));
        Assert.Throws<ArgumentException>(() => ProductionCodecV2.Encode(journey.Production));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV1.Decode(bytes));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV2.Decode(bytes));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV3.Decode(oldBytes, context));
        Assert.Throws<ArgumentException>(() => ProductionCodecV3.Encode(prior));
        journey.Repack(Id(60000));
        journey.Cut();
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV3.Decode(bytes, journey.Production.Continuation!));
        Assert.Equal(oldBytes, ProductionCodecV2.Encode(prior));
        Assert.Equal(oldBytes, ProductionCodecV2.Encode(ProductionCodecV2.Decode(oldBytes)));
    }

    [Fact]
    public void A_legacy_crafting_owner_continues_without_silently_acquiring_consumption()
    {
        var journey = ProductionContinuationFixture.Crafted(consumptionEnabled: false);
        var prior = journey.Production;
        byte[] oldBytes = ProductionCodecV1.Encode(prior);
        journey.Cut();
        Assert.False(journey.Production.SupportsConsumption);
        Assert.False(journey.Production.Continuation!.SupportsConsumption);
        Assert.Equal(0UL, journey.Production.TotalConsumedItemCount);
        Assert.Equal(OpenFantasy.Core.Worlds.WorldActionRejection.Unavailable,
            journey.PlanConsumption(IngredientLotKind.Source, Id(70), Id(60001)).Rejection);
        journey.Repack(Id(60002));
        journey.Reopen();
        Assert.False(journey.Production.SupportsConsumption);
        Assert.Equal(oldBytes, ProductionCodecV1.Encode(prior));
        Assert.Equal(oldBytes, ProductionCodecV1.Encode(ProductionCodecV1.Decode(oldBytes)));
        Assert.Throws<ArgumentException>(() => ProductionCodecV1.Encode(journey.Production));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Scope_revision_context_and_cumulative_counters_cannot_be_rewritten(int field)
    {
        var journey = ProductionContinuationFixture.Crafted();
        journey.Cut();
        byte[] bytes = ProductionCodecV3.Encode(journey.Production);
        byte[][] root = Fields(bytes);
        root[field][^1] ^= 0x40;
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV3.Decode(Frame(root), journey.Production.Continuation!));
        Assert.Equal(bytes, ProductionCodecV3.Encode(journey.Production));
    }

    [Theory]
    [InlineData("spent-prefix")]
    [InlineData("missing-output")]
    [InlineData("duplicate-output")]
    public void Imported_output_partition_cannot_recreate_spent_units_or_drop_or_duplicate_live_units(string change)
    {
        var journey = ProductionContinuationFixture.Crafted();
        journey.Cut();
        byte[][] root = Fields(ProductionCodecV3.Encode(journey.Production));
        byte[][] lots = Fields(root[9]);
        var ration = Assert.Single(journey.Production.Lots, value => value.Definition == Rations);
        int selected = Array.FindIndex(lots, 1, value => EntityId.FromBytes(Fields(value)[2]) == ration.LotId);
        Assert.True(selected > 0);
        if (change == "spent-prefix")
        {
            byte[][] lot = Fields(lots[selected]);
            byte[][] output = Fields(lot[6]);
            output[4] = Number(0); // The prior actual consumption retired [0,1).
            lot[6] = Frame(output);
            lots[selected] = Frame(lot);
        }
        else if (change == "missing-output") lots = lots.Where((_, index) => index != selected).ToArray();
        else lots = [.. lots, lots[selected]];
        root[9] = Frame(lots);
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV3.Decode(Frame(root), journey.Production.Continuation!));
    }

    [Theory]
    [InlineData("hole")]
    [InlineData("custodian")]
    [InlineData("definition")]
    [InlineData("version")]
    [InlineData("revision")]
    [InlineData("protection")]
    [InlineData("reservation")]
    public void Diagnostic_imported_holes_and_exact_lot_designations_survive_decoding(string change)
    {
        // This explicitly supplied conserved partition tests structural rejection; it is not
        // represented as a replayed custody transfer or player action sequence.
        var diagnostic = ProductionContinuationBasisFixture.DiagnosticOutputPartition();
        var continued = diagnostic.Production.Continue(diagnostic.Inventory);
        byte[] bytes = ProductionCodecV3.Encode(continued);
        Assert.Equal(bytes, ProductionCodecV3.Encode(ProductionCodecV3.Decode(bytes, continued.Continuation!)));
        byte[][] root = Fields(bytes);
        byte[][] lots = Fields(root[9]);
        int selected = Array.FindIndex(lots, 1, value => EntityId.FromBytes(Fields(value)[2]) == Id(9101));
        Assert.True(selected > 0);
        byte[][] lot = Fields(lots[selected]);
        switch (change)
        {
            case "hole":
                byte[][] output = Fields(lot[6]);
                output[4] = Number(1); // The supplied partition explicitly retires [1,2).
                lot[6] = Frame(output);
                break;
            case "custodian": lot[3] = Id(61).ToByteArray(); break;
            case "definition": lot[4] = Offcuts.DefinitionId.ToByteArray(); break;
            case "version": lot[5] = ContentHash.Compute("changed definition"u8).ToByteArray(); break;
            case "revision": lot[7] = Number(6); break;
            case "protection": lot[8] = Number(0); break;
            case "reservation": lot[9] = []; break;
        }
        lots[selected] = Frame(lot);
        root[9] = Frame(lots);
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV3.Decode(Frame(root), continued.Continuation!));
        continued.ValidateForAuthority(diagnostic.Inventory);
    }

    [Theory]
    [InlineData("spent-prefix")]
    [InlineData("missing-tail")]
    [InlineData("revision")]
    [InlineData("custodian")]
    [InlineData("protection")]
    public void Supplied_inventory_must_preserve_exact_imported_raw_partition_and_lot_state(string change)
    {
        var journey = ProductionContinuationFixture.Crafted();
        journey.Cut();
        var owner = journey.Inventory;
        var original = owner.Custody.GetLot(Id(70));
        var source = new SourceInterval(original.Source.SourceId,
            change == "spent-prefix" ? original.Source.Start - 1 : original.Source.Start,
            change == "missing-tail" ? original.Source.End - 1 : original.Source.End);
        var changed = new CountedSourceLot(original.LotId, change == "custodian" ? Id(61) : original.CustodianId,
            original.Definition, source, change == "revision" ? original.Revision + 1 : original.Revision,
            change == "protection" ? LotProtection.Favorite : original.Protection, original.ReservedBy);
        var supplied = new CountedAuthorityState(new(owner.WorldId, owner.BranchId, owner.Custody.Revision,
            owner.Custody.Storages, owner.Custody.Lots.Select(lot => lot.LotId == original.LotId ? changed : lot)),
            owner.AdmittedUnitCapacity, owner.Actors, owner.Controls, owner.Policies, owner.Sources, owner.ReservationIds);
        Assert.Throws<ArgumentException>(() => journey.Production.ValidateForAuthority(supplied));
        journey.Production.ValidateForAuthority(owner);
    }

    [Theory]
    [InlineData("affected-reset")]
    [InlineData("affected-ahead")]
    [InlineData("untouched-ahead")]
    [InlineData("missing-storage")]
    [InlineData("added-storage")]
    [InlineData("affected-capacity")]
    [InlineData("untouched-capacity")]
    public void Storage_boundaries_retain_exact_ids_capacities_and_each_custodians_operation_revision(string change)
    {
        var journey = ProductionContinuationFixture.Crafted();
        journey.Cut();
        journey.Consume(IngredientLotKind.Source, Id(70), Id(60010));
        var owner = journey.Inventory;
        byte[] goodInventory = AuthorityEncodingV1.Encode(owner);
        byte[] goodProduction = ProductionCodecV3.Encode(journey.Production);
        journey.Production.ValidateForAuthority(owner);
        Assert.Equal(3UL, owner.Custody.GetStorage(Id(60)).Revision);
        Assert.Equal(0UL, owner.Custody.GetStorage(Id(61)).Revision);
        var validRequest = journey.ConsumptionRequest(IngredientLotKind.Source, Id(70));
        var executable = new ItemConsumptionPlanner().Plan(owner, journey.Production, Delivery, Id(10), Id(60011),
            validRequest, journey.IsOccupied);
        Assert.Equal(OpenFantasy.Core.Worlds.WorldActionRejection.None, executable.Rejection);

        // These supplied invalid proposals retain every actual material claim and global
        // custody revision. They must not silently reset or replace an individual storage.
        var storages = owner.Custody.Storages.ToList();
        var policies = owner.Policies.ToList();
        switch (change)
        {
            case "affected-reset": storages[0] = storages[0] with { Revision = 0 }; break;
            case "affected-ahead": storages[0] = storages[0] with { Revision = storages[0].Revision + 1 }; break;
            case "untouched-ahead": storages[1] = storages[1] with { Revision = 1 }; break;
            case "missing-storage":
                storages.RemoveAll(value => value.CustodianId == Id(61));
                policies.RemoveAll(value => value.CustodianId == Id(61));
                break;
            case "added-storage":
                storages.Add(new(Id(62), 64, 0));
                policies.Add(new(Id(62), Id(11), CustodyDepositRule.OwnerOnly));
                break;
            case "affected-capacity": storages[0] = storages[0] with { UnitCapacity = 65 }; break;
            case "untouched-capacity": storages[1] = storages[1] with { UnitCapacity = 65 }; break;
        }
        var supplied = new CountedAuthorityState(new(owner.WorldId, owner.BranchId, owner.Custody.Revision,
            storages, owner.Custody.Lots), owner.AdmittedUnitCapacity, owner.Actors, owner.Controls,
            policies, owner.Sources, owner.ReservationIds);
        Assert.Equal(owner.Custody.Revision, supplied.Custody.Revision);
        Assert.Equal(owner.Custody.Lots, supplied.Custody.Lots);
        Assert.Throws<ArgumentException>(() => journey.Production.ValidateForAuthority(supplied));
        var capturedInvalidRequest = new ConsumeItemRequest(validRequest.CustodianId,
            supplied.Custody.GetStorage(validRequest.CustodianId).Revision, validRequest.ExpectedProductionRevision,
            validRequest.RecipientActorId, validRequest.ExpectedDefinition, validRequest.Ingredient);
        Assert.Throws<ArgumentException>(() => new ItemConsumptionPlanner().Plan(supplied, journey.Production,
            Delivery, Id(10), Id(60011), capturedInvalidRequest, journey.IsOccupied));
        Assert.Equal(goodInventory, AuthorityEncodingV1.Encode(owner));
        Assert.Equal(goodProduction, ProductionCodecV3.Encode(journey.Production));
    }

    [Fact]
    public void Missing_active_retirement_and_noncanonical_or_excess_bytes_fail_closed()
    {
        var journey = ProductionContinuationFixture.Crafted();
        journey.Cut();
        journey.Consume(IngredientLotKind.Source, Id(70), Id(60003));
        byte[] bytes = ProductionCodecV3.Encode(journey.Production);
        byte[][] root = Fields(bytes);
        root[10] = Frame([Fields(root[10])[0]]);
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV3.Decode(Frame(root), journey.Production.Continuation!));
        // Even rewriting both operation counters cannot pair a fabricated pre-consumption
        // segment with the actual post-consumption inventory: its exact suffix is missing.
        root[4] = Number(journey.Production.Continuation!.BaseProductionRevision);
        root[7] = Number(journey.Production.Continuation.BaseConsumedItemCount);
        var falseEarlierSegment = ProductionCodecV3.Decode(Frame(root), journey.Production.Continuation);
        Assert.Throws<ArgumentException>(() => falseEarlierSegment.ValidateForAuthority(journey.Inventory));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV3.Decode([.. bytes, 0], journey.Production.Continuation!));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV3.Decode(bytes[..^1], journey.Production.Continuation!));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV3.Decode(
            new byte[ProductionCodecV3.MaximumSnapshotByteLength + 1], journey.Production.Continuation!));
        Assert.Equal(bytes, ProductionCodecV3.Encode(journey.Production));
    }

    private static byte[][] Fields(byte[] bytes)
    {
        Assert.True(bytes.AsSpan(0, 4).SequenceEqual("OFC\0"u8));
        Assert.Equal(1U, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(4, 4)));
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)));
        var fields = new byte[count][];
        int offset = 12;
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4)));
            offset += 4;
            fields[index] = bytes.AsSpan(offset, length).ToArray();
            offset += length;
        }
        Assert.Equal(bytes.Length, offset);
        return fields;
    }

    private static byte[] Number(ulong value)
    {
        byte[] bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    // Independent OFC frame writer permits precise corruption without exposing private runtime constructors.
    private static byte[] Frame(byte[][] fields)
    {
        using var stream = new MemoryStream();
        stream.Write("OFC\0"u8);
        Span<byte> number = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(number, 1);
        stream.Write(number);
        BinaryPrimitives.WriteUInt32BigEndian(number, checked((uint)fields.Length));
        stream.Write(number);
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(number, checked((uint)field.Length));
            stream.Write(number);
            stream.Write(field);
        }
        return stream.ToArray();
    }
}
