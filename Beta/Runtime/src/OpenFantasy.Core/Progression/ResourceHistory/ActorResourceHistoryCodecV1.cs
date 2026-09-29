namespace OpenFantasy.Core.Progression.ResourceHistory;

/// <summary>Canonical continuity evidence decoded against a separately admitted progression basis.</summary>
public static class ActorResourceHistoryCodecV1
{
    public const int MaximumEncodedByteLength = 2 * 1024 * 1024;
    private const int MaximumBatchByteLength = 256 * 1024;
    private const int MaximumChangeByteLength = 1536;
    private const int MaximumCauseByteLength = 192;

    public static byte[] Encode(ActorResourceHistory history)
    {
        ArgumentNullException.ThrowIfNull(history);
        return Frame("progression.resource-history", Number(1), history.Basis.WorldId.ToByteArray(),
            history.Basis.BranchId.ToByteArray(), Number(history.BasisWorldSequence),
            history.Basis.ContentHash.ToByteArray(),
            Frame("progression.resource-batches", history.Batches.Select(batch => batch.EncodingV1).ToArray()));
    }

    public static ActorResourceHistory Decode(ReadOnlySpan<byte> bytes, ProgressionSnapshot admittedBasis,
        ulong expectedBasisWorldSequence)
    {
        ArgumentNullException.ThrowIfNull(admittedBasis);
        try
        {
            var reader = new ProgressionFrameReader(bytes, MaximumEncodedByteLength, 7);
            reader.Tag("progression.resource-history"u8);
            reader.Version();
            var world = reader.Identity();
            var branch = reader.Identity();
            ulong sequence = reader.Number();
            var basisHash = reader.Hash();
            if (world != admittedBasis.WorldId || branch != admittedBasis.BranchId
                || sequence != expectedBasisWorldSequence || basisHash != admittedBasis.ContentHash)
                throw new ProgressionFormatException("Resource history requires the exact admitted basis and world boundary.");
            var batches = new ProgressionFrameReader(reader.Field(MaximumEncodedByteLength),
                MaximumEncodedByteLength, ActorResourceHistory.MaximumBatches + 1);
            batches.Tag("progression.resource-batches"u8);
            List<ResourceChangeBatch> values = [];
            int changeCount = 0;
            while (batches.RemainingFields != 0)
            {
                var batch = DecodeBatch(batches.Field(MaximumBatchByteLength), admittedBasis);
                changeCount += batch.Changes.Count;
                if (changeCount > ActorResourceHistory.MaximumChanges)
                    throw new ProgressionFormatException("Resource history exceeds its change envelope.");
                values.Add(batch);
            }
            batches.Complete();
            reader.Complete();
            var result = new ActorResourceHistory(admittedBasis, sequence, values);
            if (!bytes.SequenceEqual(Encode(result)))
                throw new ProgressionFormatException("Resource history must use its canonical encoding.");
            return result;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new ProgressionFormatException($"Invalid resource history: {error.Message}");
        }
    }

    internal static byte[] EncodeBatch(ResourceChangeBatch batch) => Frame("progression.resource-batch",
        Number(1), Number(batch.WorldSequence), Number(batch.CommandOrdinal), batch.CommandId.ToByteArray(),
        Collection("progression.resource-changes", batch.Changes.Select(EncodeChange)));

    private static ResourceChangeBatch DecodeBatch(ReadOnlySpan<byte> bytes, ProgressionSnapshot basis)
    {
        var reader = new ProgressionFrameReader(bytes, MaximumBatchByteLength, 6);
        reader.Tag("progression.resource-batch"u8);
        reader.Version();
        ulong sequence = reader.Number();
        uint ordinal = checked((uint)reader.Number());
        var command = reader.Identity();
        var changes = new ProgressionFrameReader(reader.Field(MaximumBatchByteLength),
            MaximumBatchByteLength, ResourceChangeBatch.MaximumChanges + 1);
        changes.Tag("progression.resource-changes"u8);
        List<ActorResourceChange> values = [];
        while (changes.RemainingFields != 0)
            values.Add(DecodeChange(changes.Field(MaximumChangeByteLength), basis));
        changes.Complete();
        reader.Complete();
        return new(sequence, ordinal, command, values);
    }

    private static byte[] EncodeChange(ActorResourceChange change) => Frame("progression.resource-change",
        Number(1), Number((ulong)change.Ordinal), EncodeCause(change.Cause), Number((ulong)change.Operation),
        Number(change.RequestedAmount), ProgressionCodecV1.EncodeActor(change.Before),
        ProgressionCodecV1.EncodeActor(change.After));

    private static ActorResourceChange DecodeChange(ReadOnlySpan<byte> bytes, ProgressionSnapshot basis)
    {
        var reader = new ProgressionFrameReader(bytes, MaximumChangeByteLength, 8);
        reader.Tag("progression.resource-change"u8);
        reader.Version();
        int ordinal = checked((int)reader.Number());
        var cause = DecodeCause(reader.Field(MaximumCauseByteLength));
        var operation = (ActorResourceOperation)checked((int)reader.Number());
        ulong requested = reader.Number();
        var before = ProgressionCodecV1.DecodeActor(reader.Field(ProgressionCodecV1.MaximumActorByteLength), basis.Profiles, basis.Curves);
        var after = ProgressionCodecV1.DecodeActor(reader.Field(ProgressionCodecV1.MaximumActorByteLength), basis.Profiles, basis.Curves);
        reader.Complete();
        return new(ordinal, cause, operation, requested, before, after);
    }

    private static byte[] EncodeCause(ResourceChangeCause cause) => Frame("progression.resource-cause",
        Number(1), cause.EvidenceId.ToByteArray(), cause.DefinitionId.ToByteArray(), cause.DefinitionVersion.ToByteArray());

    private static ResourceChangeCause DecodeCause(ReadOnlySpan<byte> bytes)
    {
        var reader = new ProgressionFrameReader(bytes, MaximumCauseByteLength, 5);
        reader.Tag("progression.resource-cause"u8);
        reader.Version();
        var result = new ResourceChangeCause(reader.Identity(), reader.Identity(), reader.Hash());
        reader.Complete();
        return result;
    }

    private static byte[] Frame(string tag, params ReadOnlyMemory<byte>[] fields) => CanonicalEncodingV1.Frame(tag, fields);
    private static byte[] Collection(string tag, IEnumerable<byte[]> values) => Frame(tag,
        values.Select(value => (ReadOnlyMemory<byte>)value).ToArray());
    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
}
