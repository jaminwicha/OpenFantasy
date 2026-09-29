using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Production;
using static OpenFantasy.Core.Tests.Production.ConsentedMaterialJourney;
using static OpenFantasy.Core.Tests.Worlds.ConsentedFormatFixture;
using Kind = OpenFantasy.Core.Authority.ConsentedMaterial.ConsentedMaterialLotKind;

namespace OpenFantasy.Core.Tests.Production;

/// <summary>
/// The ordered V10 consent operation at its only public boundary, the continued production frame.
/// Expected operation bytes come from the standalone generator; hostile frames are re-framed actual
/// bytes. Structural refusal and the mandatory authority replay are asserted separately.
/// </summary>
public sealed class ConsentedMaterialFormatTests
{
    private const int OperationsField = 12;
    private const int PayloadField = 5;
    private const string Lineage = "Continued production violates its closing context or lineage invariants: ";
    private const string OrderRefusal = "Material operations require bounded exact order and distinct permanent commands.";
    private const string ApprovalRefusal = "Consented transfers require their explicit capability and distinct permanent approval identities.";
    private const string BindingRefusal = "Material operation must bind exact canonical command content and order.";

    [Fact]
    public void Independent_vector_pins_the_ordered_consent_operation_inside_the_actual_production_frame()
    {
        var journey = ConsentedMaterialJourney.Create();
        Assert.Equal(WoodLot, journey.Gift(Kind.CountedSource, WoodLot, Giver, Receiver, 4, 50_001, 50_002));
        ProductionContinuationContext context = journey.Production.Continuation!;
        byte[] actual = ProductionCodecV10.Encode(journey.Production);
        byte[][] fields = ReadFrame(actual);
        Assert.Equal(13, fields.Length);
        Assert.Equal(Vector("operationsV10"), fields[OperationsField]);
        Assert.Equal(Vector("operationV10Raw"), ReadFrame(fields[OperationsField])[1]);
        // Only the context hash is an opaque import; every other byte of the frame is independent.
        Assert.Equal(Compose("productionV10", new() { ["contextHash"] = context.ContentHash.ToByteArray() }), actual);

        ProductionSnapshot decoded = ProductionCodecV10.Decode(actual, context, []);
        var operation = Assert.IsType<MaterialConsentedTransferOperation>(Assert.Single(decoded.MaterialHistory!.Operations));
        Assert.Equal((1UL, InstantCraftFixture.Id(50_001), InstantCraftFixture.Id(50_002), WoodLot, MaterialOperationKind.ConsentedTransfer),
            (operation.Sequence, operation.CommandId, operation.AcceptanceId, operation.MovedLotId, operation.Kind));
        Assert.Equal((InstantCraftFixture.Id(20), InstantCraftFixture.Id(30), InstantCraftFixture.Id(21), InstantCraftFixture.Id(31)),
            (operation.Approval.SenderDelivery.PrincipalId, operation.Approval.SenderDelivery.SessionId,
                operation.Approval.RecipientDelivery.PrincipalId, operation.Approval.RecipientDelivery.SessionId));
        Assert.True(operation.Approval.Acceptance.Binds(operation.Approval.Command));
        decoded.ValidateForAuthority(journey.Inventory);
    }

    [Fact]
    public void Hostile_order_identity_and_binding_frames_are_refused_with_their_exact_reason()
    {
        ConsentedMaterialJourney journey = TwoGifts(out byte[] actual);
        ProductionContinuationContext context = journey.Production.Continuation!;

        AssertRefused(context, OrderRefusal, Mutate(actual, operations =>
            operations[2] = Rewrite(operations[2], operation => operation[3] = U64(3))));
        AssertRefused(context, OrderRefusal, Mutate(actual, operations =>
            operations[1] = Rewrite(operations[1], operation => operation[3] = U64(0))));
        AssertRefused(context, OrderRefusal, Mutate(actual, operations =>
            (operations[1], operations[2]) = (operations[2], operations[1])));
        AssertRefused(context, OrderRefusal, Mutate(actual, operations =>
            operations[2] = Rewrite(operations[1], operation => operation[3] = U64(2))));
        AssertRefused(context, BindingRefusal, Mutate(actual, operations =>
            operations[1] = Rewrite(operations[1], operation => operation[4] = InstantCraftFixture.Id(59_999).ToByteArray())));
        // The outer frame names the acceptance instead of the command it must bind.
        AssertRefused(context, BindingRefusal, Mutate(actual, operations =>
            operations[1] = Rewrite(operations[1], operation => operation[4] = InstantCraftFixture.Id(50_002).ToByteArray())));
        AssertRefused(context, "Unknown material operation kind.", Mutate(actual, operations =>
            operations[1] = Rewrite(operations[1], operation => operation[2] = U64(11))));

        // The second approval is re-issued under each identity the first operation already holds.
        foreach (ulong occupied in new[] { 50_001UL, 50_002UL })
        {
            AssertRefused(context, ApprovalRefusal, Mutate(actual, operations =>
                operations[2] = Rewrite(operations[2], operation => operation[PayloadField] = Rewrite(
                    operation[PayloadField], payload =>
                    {
                        ConsentedMaterialCommand command = ConsentedMaterialCodecV1.DecodeCommand(payload[1]);
                        ConsentedMaterialAcceptance prior = ConsentedMaterialCodecV1.DecodeAcceptance(payload[4]);
                        payload[4] = ConsentedMaterialCodecV1.Encode(ConsentedMaterialAcceptance.Approving(command,
                            InstantCraftFixture.Id(occupied), prior.PrincipalId, prior.OriginSessionId));
                    }))));
        }

        // Frames of another operation kind or with an added field never decode as consent.
        foreach (byte[] hostile in new[]
        {
            Mutate(actual, operations => operations[1] = Rewrite(operations[1], operation => operation[2] = U64(4))),
            Mutate(actual, operations => operations[1] = Rewrite(operations[1], operation => operation[2] = U64(5))),
            Mutate(actual, operations => operations[1] = Rewrite(operations[1], operation =>
                operation[PayloadField] = WriteFrame([.. ReadFrame(operation[PayloadField]), WoodLot.ToByteArray()]))),
            Mutate(actual, operations => operations[1] = Rewrite(operations[1], operation =>
                operation[PayloadField] = WriteFrame(ReadFrame(operation[PayloadField])[..7]))),
            Mutate(actual, operations => operations[1] = WriteFrame([.. ReadFrame(operations[1]), U64(0)]))
        })
        {
            Assert.NotEqual(actual, hostile);
            Assert.Throws<ProductionFormatException>(() => ProductionCodecV10.Decode(hostile, context, []));
        }
        ProductionCodecV10.Decode(actual, context, []).ValidateForAuthority(journey.Inventory);
    }

    [Fact]
    public void Consent_frames_are_refused_by_every_other_context_and_interpretation()
    {
        ConsentedMaterialJourney journey = TwoGifts(out byte[] actual);
        ProductionContinuationContext context = journey.Production.Continuation!;
        Assert.True(context.SupportsConsentedTransfer && context.SupportsMaterialOperations);
        Assert.False(context.SupportsCompleteWorld || context.SupportsConsentedWorld);

        // Another consented journey with a different admitted storage has a different context.
        ProductionContinuationContext other = ConsentedMaterialJourney.Create(receiverCapacity: 2).Production.Continuation!;
        Assert.NotEqual(context.ContentHash, other.ContentHash);
        AssertRefused(other, "The supplied closing context does not match this production segment.", actual);

        // The context after a cut is a later ordinal; the closed segment's bytes never reopen under it.
        journey.Cut();
        ProductionContinuationContext later = journey.Production.Continuation!;
        Assert.True(later.SupportsConsentedTransfer);
        Assert.NotEqual(context.ContentHash, later.ContentHash);
        AssertRefused(later, "The supplied closing context does not match this production segment.", actual);
        byte[] continued = ProductionCodecV10.Encode(journey.Production);
        AssertRefused(context, "The supplied closing context does not match this production segment.", continued);

        Assert.StartsWith("Production V9 requires the coordinated complete-world context.",
            Assert.Throws<ArgumentException>(() => ProductionCodecV9.Decode(actual, context)).Message);
        Assert.StartsWith("Production V11 requires its exact consented world context.",
            Assert.Throws<ArgumentException>(() => ProductionCodecV11.Decode(actual, context)).Message);
        Assert.StartsWith("Production V9 requires the coordinated complete-world owner.",
            Assert.Throws<ArgumentException>(() => ProductionCodecV9.Encode(journey.Production)).Message);
        Assert.StartsWith("Production V11 requires the consented complete-world interpretation.",
            Assert.Throws<ArgumentException>(() => ProductionCodecV11.Encode(journey.Production)).Message);
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV3.Decode(actual, context));

        // Relabelling the consent frame as the interpretations V9 and V11 changes no capability.
        foreach (ulong version in new[] { 9UL, 11UL, 4UL })
        {
            byte[][] fields = ReadFrame(actual);
            fields[1] = U64(version);
            Assert.Throws<ProductionFormatException>(() => ProductionCodecV10.Decode(WriteFrame(fields), context, []));
        }
    }

    [Fact]
    public void Structurally_valid_moved_lot_and_approval_edits_are_refused_by_ordered_authority_replay()
    {
        ConsentedMaterialJourney journey = TwoGifts(out byte[] actual);
        ProductionContinuationContext context = journey.Production.Continuation!;
        byte[][] hostile =
        [
            // The recorded result names a lot that the approval never moved.
            Mutate(actual, operations => operations[2] = Rewrite(operations[2], operation =>
                operation[PayloadField] = Rewrite(operation[PayloadField],
                    payload => payload[7] = BerryLot.ToByteArray()))),
            // The recipient's recorded approval restates a different quantity than the command.
            Mutate(actual, operations => operations[2] = Rewrite(operations[2], operation =>
                operation[PayloadField] = Rewrite(operation[PayloadField], payload =>
                {
                    ConsentedMaterialAcceptance prior = ConsentedMaterialCodecV1.DecodeAcceptance(payload[4]);
                    ConsentedMaterialTerms terms = prior.Terms;
                    payload[4] = ConsentedMaterialCodecV1.Encode(new ConsentedMaterialAcceptance(prior.AcceptanceId,
                        prior.PrincipalId, prior.OriginSessionId, prior.CommandId, prior.CommandHash,
                        new(terms.WorldId, terms.BranchId, terms.SenderActorId, terms.RecipientActorId, terms.LotKind,
                            terms.LotId, terms.Definition, terms.SourceCustodianId, terms.DestinationCustodianId,
                            terms.Quantity - 1, terms.ExpectedLotRevision)));
                }))),
            // The recorded sender delivery is the recipient's session.
            Mutate(actual, operations => operations[2] = Rewrite(operations[2], operation =>
                operation[PayloadField] = Rewrite(operation[PayloadField],
                    payload => (payload[2], payload[3]) = (payload[5], payload[6]))))
        ];
        foreach (byte[] bytes in hostile)
        {
            Assert.NotEqual(actual, bytes);
            // These claims have canonical structure. Only replay with the actual inventory can
            // re-decide the approval at its ordered position; unrelated exceptions are failures.
            ProductionSnapshot decoded = ProductionCodecV10.Decode(bytes, context, []);
            ArgumentException refusal = Assert.Throws<ArgumentException>(
                () => decoded.ValidateForAuthority(journey.Inventory));
            Assert.Equal("The recorded consented transfer must reproduce its bilateral approval, lot revision, "
                + "capacity rules and exact result at its ordered position.", refusal.Message);
        }
        ProductionCodecV10.Decode(actual, context, []).ValidateForAuthority(journey.Inventory);
    }

    private static ConsentedMaterialJourney TwoGifts(out byte[] bytes)
    {
        var journey = ConsentedMaterialJourney.Create();
        journey.Gift(Kind.CountedSource, WoodLot, Giver, Receiver, 4, 50_001, 50_002);
        journey.Gift(Kind.CountedSource, WoodLot, Receiver, Giver, 4, 50_003, 50_004);
        bytes = ProductionCodecV10.Encode(journey.Production);
        ProductionCodecV10.Decode(bytes, journey.Production.Continuation!, []).ValidateForAuthority(journey.Inventory);
        Assert.Equal(3, ReadFrame(ReadFrame(bytes)[OperationsField]).Length);
        return journey;
    }

    private static void AssertRefused(ProductionContinuationContext context, string reason, byte[] bytes)
    {
        ProductionFormatException refusal = Assert.Throws<ProductionFormatException>(
            () => ProductionCodecV10.Decode(bytes, context, []));
        Assert.Equal(ProductionFormatFailure.InvalidValue, refusal.Failure);
        Assert.Equal(Lineage + reason, refusal.Message);
    }

    private static byte[] Mutate(byte[] production, Action<byte[][]> change)
    {
        byte[][] fields = ReadFrame(production);
        fields[OperationsField] = Rewrite(fields[OperationsField], change);
        return WriteFrame(fields);
    }

    private static byte[] Rewrite(byte[] frame, Action<byte[][]> change)
    {
        byte[][] fields = ReadFrame(frame);
        change(fields);
        return WriteFrame(fields);
    }
}
