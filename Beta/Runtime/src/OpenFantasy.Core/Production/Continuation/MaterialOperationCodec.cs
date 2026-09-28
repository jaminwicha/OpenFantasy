using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Construction;

namespace OpenFantasy.Core.Production;

internal static class MaterialOperationCodec
{
    internal const int MaximumByteLength = 32 * 1024;
    internal static byte[] Encode(MaterialOperation operation)
    {
        byte[] payload = operation switch
        {
            MaterialTerrainRepresentationOperation terrain => terrain.Evidence.ContentHash.ToByteArray(),
            MaterialConstructionOperation construction => construction.Evidence.ContentHash.ToByteArray(),
            MaterialCraftOperation craft => craft.Batch.BatchId.ToByteArray(),
            MaterialConsumptionOperation consume => consume.Claim.ConsumptionId.ToByteArray(),
            MaterialGatherOperation gather => gather.Evidence.ContentHash.ToByteArray(),
            MaterialTransferOperation transfer => CanonicalEncodingV1.Frame("material.transfer",
                CommandCodecV1.Encode(transfer.Command), transfer.Delivery.PrincipalId.ToByteArray(), transfer.Delivery.SessionId.ToByteArray()),
            MaterialDeliveryOperation delivery => CanonicalEncodingV1.Frame("material.delivery",
                delivery.ActorId.ToByteArray(), delivery.Delivery.PrincipalId.ToByteArray(), delivery.Delivery.SessionId.ToByteArray(), EncodeRequest(delivery.Request)),
            _ => throw new ArgumentException("Unsupported material operation.")
        };
        byte[] bytes = CanonicalEncodingV1.Frame("production.material-operation", Number(1), Number((ulong)operation.Kind),
            Number(operation.Sequence), operation.CommandId.ToByteArray(), payload);
        if (bytes.Length > MaximumByteLength) throw CanonicalProductionReader.Failure(ProductionFormatFailure.LimitExceeded, "Material operation exceeds its byte bound.");
        return bytes;
    }

    internal static MaterialOperation Decode(ReadOnlySpan<byte> bytes,
        IReadOnlyDictionary<EntityId, ProductionBatch> batches, IReadOnlyDictionary<EntityId, ConsumedItemClaim> claims,
        IReadOnlyDictionary<ContentHash, GatherIssueEvidence> evidence,
        IReadOnlyDictionary<ContentHash, ConstructionCompletionEvidence>? constructionEvidence = null,
        IReadOnlyDictionary<ContentHash, OpenFantasy.Core.Geography.TerrainRepresentationEvidence>? terrainEvidence = null)
    {
        var reader = new CanonicalProductionReader(bytes, MaximumByteLength, 6);
        reader.ExpectTag("production.material-operation"u8);
        reader.ExpectVersion(1);
        MaterialOperationKind kind = (MaterialOperationKind)reader.ReadUnsigned64();
        ulong sequence = reader.ReadUnsigned64();
        EntityId commandId = reader.ReadIdentity();
        MaterialOperation result;
        switch (kind)
        {
            case MaterialOperationKind.TerrainRepresentation:
                ContentHash terrainHash = ReadHash(ref reader);
                if (terrainEvidence is null || !terrainEvidence.TryGetValue(terrainHash, out var terrain))
                    throw new ArgumentException("Terrain decoding requires its actual opaque representation evidence.");
                result = new MaterialTerrainRepresentationOperation(sequence, terrain);
                break;
            case MaterialOperationKind.Construction:
                ContentHash constructionHash = ReadHash(ref reader);
                if (constructionEvidence is null || !constructionEvidence.TryGetValue(constructionHash, out ConstructionCompletionEvidence? construction)
                    || !batches.TryGetValue(construction.Batch.BatchId, out ProductionBatch? constructionBatch)
                    || !ProductionCodecV1.EncodeBatch(constructionBatch).AsSpan().SequenceEqual(ProductionCodecV1.EncodeBatch(construction.Batch)))
                    throw new ArgumentException("Construction decoding requires the actual matching opaque completion evidence and full batch.");
                result = new MaterialConstructionOperation(sequence, construction);
                break;
            case MaterialOperationKind.Craft:
                if (!batches.TryGetValue(reader.ReadIdentity(), out ProductionBatch? batch)) throw new ArgumentException("Craft operation requires its exact retained batch.");
                result = new MaterialCraftOperation(sequence, batch);
                break;
            case MaterialOperationKind.Consume:
                if (!claims.TryGetValue(reader.ReadIdentity(), out ConsumedItemClaim? claim)) throw new ArgumentException("Consume operation requires its exact retained claim.");
                result = new MaterialConsumptionOperation(sequence, claim);
                break;
            case MaterialOperationKind.Gather:
                if (!evidence.TryGetValue(ReadHash(ref reader), out GatherIssueEvidence? proof)) throw new ArgumentException("Gather decoding requires the actual matching opaque issuing evidence.");
                result = new MaterialGatherOperation(sequence, proof);
                break;
            case MaterialOperationKind.Transfer:
                var transfer = new CanonicalProductionReader(reader.ReadField(MaximumByteLength), MaximumByteLength, 4);
                transfer.ExpectTag("material.transfer"u8);
                CommandEnvelope envelope = CommandCodecV1.Decode(transfer.ReadField(CommandCodecV1.MaximumEnvelopeByteLength));
                var transferDelivery = new TrustedCommandDelivery(transfer.ReadIdentity(), transfer.ReadIdentity());
                transfer.Complete();
                result = new MaterialTransferOperation(sequence, envelope, transferDelivery);
                break;
            case MaterialOperationKind.SourceDelivery:
                var delivery = new CanonicalProductionReader(reader.ReadField(MaximumByteLength), MaximumByteLength, 5);
                delivery.ExpectTag("material.delivery"u8);
                EntityId actorId = delivery.ReadIdentity();
                var deliveredBy = new TrustedCommandDelivery(delivery.ReadIdentity(), delivery.ReadIdentity());
                SourceDeliveryRequest request = DecodeRequest(delivery.ReadField(MaximumByteLength));
                delivery.Complete();
                result = new MaterialDeliveryOperation(sequence, commandId, actorId, deliveredBy, request);
                break;
            default: throw new ArgumentException("Unknown material operation kind.");
        }
        reader.Complete();
        if (result.CommandId != commandId || !bytes.SequenceEqual(Encode(result)))
            throw new ArgumentException("Material operation must bind exact canonical command content and order.");
        return result;
    }

    private static byte[] EncodeRequest(SourceDeliveryRequest request) => CanonicalEncodingV1.Frame("material.delivery-request",
        request.SourceCustodianId.ToByteArray(), Number(request.ExpectedSourceRevision), request.DestinationCustodianId.ToByteArray(),
        request.RequiredItem.DefinitionId.ToByteArray(), request.RequiredItem.Version.ToByteArray(), Number(request.RequiredQuantity),
        request.ReservationId.ToByteArray(), CanonicalEncodingV1.Frame("material.delivery-selections", request.Selections.Select(value =>
            (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("material.delivery-selection", value.LotId.ToByteArray(),
                Number(value.ExpectedRevision), Number(value.Quantity))).ToArray()));

    private static SourceDeliveryRequest DecodeRequest(ReadOnlySpan<byte> bytes)
    {
        var reader = new CanonicalProductionReader(bytes, MaximumByteLength, 9);
        reader.ExpectTag("material.delivery-request"u8);
        EntityId source = reader.ReadIdentity();
        ulong revision = reader.ReadUnsigned64();
        EntityId destination = reader.ReadIdentity();
        var item = new CountedItemDefinition(reader.ReadIdentity(), ReadHash(ref reader));
        ulong quantity = reader.ReadUnsigned64();
        EntityId reservation = reader.ReadIdentity();
        var selections = new CanonicalProductionReader(reader.ReadField(MaximumByteLength), MaximumByteLength, SourceDeliveryRequest.MaximumSelections + 1);
        selections.ExpectTag("material.delivery-selections"u8);
        var values = new List<SourceLotSelection>();
        while (selections.RemainingFields != 0)
        {
            var selected = new CanonicalProductionReader(selections.ReadField(256), 256, 4);
            selected.ExpectTag("material.delivery-selection"u8);
            values.Add(new(selected.ReadIdentity(), selected.ReadUnsigned64(), selected.ReadUnsigned64()));
            selected.Complete();
        }
        selections.Complete();
        reader.Complete();
        return new(source, revision, destination, item, quantity, reservation, values);
    }

    internal static ContentHash ReadHash(ref CanonicalProductionReader reader)
    {
        ReadOnlySpan<byte> hash = reader.ReadField(32);
        if (hash.Length != 32) throw new ArgumentException("A material content hash requires exactly 32 bytes.");
        return new(Convert.ToHexString(hash).ToLowerInvariant());
    }
    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
}
