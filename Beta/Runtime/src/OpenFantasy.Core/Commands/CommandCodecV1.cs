namespace OpenFantasy.Core.Commands;

/// <summary>Canonical, bounded command framing. Decoding validates shape, not gameplay legality.</summary>
public static class CommandCodecV1
{
    public const int MaximumEnvelopeByteLength = 16 * 1024;
    private const int MaximumRevisionSetByteLength = 12 * 1024;
    private const int MaximumRevisionByteLength = 128;
    private const int MaximumPayloadByteLength = 256;
    private const ulong ContractVersion = 1;

    public static byte[] Encode(CommandEnvelope command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CanonicalEncodingV1.Frame("command.envelope",
            CanonicalEncodingV1.Unsigned64(command.ProtocolVersion),
            command.CommandId.ToByteArray(), command.PrincipalId.ToByteArray(),
            command.OriginSessionId.ToByteArray(), command.ControlledActorId.ToByteArray(),
            command.WorldId.ToByteArray(), command.BranchId.ToByteArray(),
            EncodeRevisions(command.ExpectedRevisions), EncodePayload(command.Payload));
    }

    public static CommandEnvelope Decode(ReadOnlySpan<byte> bytes)
    {
        CanonicalCommandReader reader = new(bytes, MaximumEnvelopeByteLength, 11);
        reader.ExpectTag("command.envelope"u8);
        reader.ExpectVersion(CommandEnvelope.CurrentProtocolVersion);
        EntityId commandId = reader.ReadIdentity();
        EntityId principalId = reader.ReadIdentity();
        EntityId originSessionId = reader.ReadIdentity();
        EntityId actorId = reader.ReadIdentity();
        EntityId worldId = reader.ReadIdentity();
        EntityId branchId = reader.ReadIdentity();
        ExpectedRevisionSet revisions = DecodeRevisions(reader.ReadField(MaximumRevisionSetByteLength));
        TransferLotIntent payload = DecodePayload(reader.ReadField(MaximumPayloadByteLength));
        reader.Complete();

        try
        {
            return new CommandEnvelope(commandId, principalId, originSessionId, actorId,
                worldId, branchId, revisions, payload);
        }
        catch (ArgumentException)
        {
            throw CanonicalCommandReader.Failure(CommandFormatFailure.InvalidValue,
                "Command fields violate envelope invariants.");
        }
    }

    private static byte[] EncodeRevisions(ExpectedRevisionSet revisions)
    {
        List<ReadOnlyMemory<byte>> fields = [CanonicalEncodingV1.Unsigned64(ContractVersion)];
        foreach (ExpectedRevision revision in revisions)
        {
            fields.Add(CanonicalEncodingV1.Frame("command.expected-revision",
                CanonicalEncodingV1.Unsigned64(ContractVersion),
                CanonicalEncodingV1.Unsigned64((ulong)revision.TargetKind),
                revision.TargetId.ToByteArray(), CanonicalEncodingV1.Unsigned64(revision.Revision)));
        }

        return CanonicalEncodingV1.Frame("command.expected-revisions", fields.ToArray());
    }

    private static ExpectedRevisionSet DecodeRevisions(ReadOnlySpan<byte> bytes)
    {
        CanonicalCommandReader reader = new(bytes, MaximumRevisionSetByteLength,
            ExpectedRevisionSet.MaximumCount + 2);
        reader.ExpectTag("command.expected-revisions"u8);
        reader.ExpectVersion(ContractVersion);
        List<ExpectedRevision> revisions = [];
        while (reader.RemainingFields > 0)
        {
            ExpectedRevision revision = DecodeRevision(reader.ReadField(MaximumRevisionByteLength));
            if (revisions.Count > 0 && ExpectedRevisionSet.CompareTargets(revisions[^1], revision) >= 0)
            {
                throw CanonicalCommandReader.Failure(CommandFormatFailure.NonCanonical,
                    "Expected revision owners must be unique and strictly sorted.");
            }

            revisions.Add(revision);
        }

        reader.Complete();
        return new ExpectedRevisionSet(revisions);
    }

    private static ExpectedRevision DecodeRevision(ReadOnlySpan<byte> bytes)
    {
        CanonicalCommandReader reader = new(bytes, MaximumRevisionByteLength, 6);
        reader.ExpectTag("command.expected-revision"u8);
        reader.ExpectVersion(ContractVersion);
        ulong targetKind = reader.ReadUnsigned64();
        if (targetKind is not (ulong)RevisionTargetKind.Entity and not (ulong)RevisionTargetKind.Branch)
        {
            throw CanonicalCommandReader.Failure(CommandFormatFailure.UnknownType,
                "The revision owner kind is unsupported.");
        }

        EntityId targetId = reader.ReadIdentity();
        ulong revision = reader.ReadUnsigned64();
        reader.Complete();
        return new ExpectedRevision((RevisionTargetKind)targetKind, targetId, revision);
    }

    private static byte[] EncodePayload(TransferLotIntent payload) => CanonicalEncodingV1.Frame(
        "command.transfer-lot", CanonicalEncodingV1.Unsigned64(ContractVersion),
        payload.LotId.ToByteArray(), payload.SourceCustodianId.ToByteArray(),
        payload.DestinationCustodianId.ToByteArray(), CanonicalEncodingV1.Unsigned64(payload.Quantity));

    private static TransferLotIntent DecodePayload(ReadOnlySpan<byte> bytes)
    {
        CanonicalCommandReader reader = new(bytes, MaximumPayloadByteLength, 7);
        reader.ExpectTag("command.transfer-lot"u8);
        reader.ExpectVersion(ContractVersion);
        EntityId lotId = reader.ReadIdentity();
        EntityId sourceId = reader.ReadIdentity();
        EntityId destinationId = reader.ReadIdentity();
        ulong quantity = reader.ReadUnsigned64();
        reader.Complete();

        try
        {
            return new TransferLotIntent(lotId, sourceId, destinationId, quantity);
        }
        catch (ArgumentException)
        {
            throw CanonicalCommandReader.Failure(CommandFormatFailure.InvalidValue,
                "Transfer fields violate payload invariants.");
        }
    }
}
