using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Closed canonical wire contract of the proposal transition. The frame names source profile 27 and
/// target profile 30 explicitly; every other pair, version, field count or non-canonical byte fails.
/// </summary>
public static class WorldProposalTransitionCodecV1
{
    public const int MaximumByteLength = 1024;
    private const int FieldCount = 13;
    private const int HashByteLength = 32;

    public static byte[] Encode(WorldProposalTransition request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CanonicalEncodingV1.Frame("world.proposal-transition", Number(1),
            Number(WorldProposalTransition.SourceProfileVersion), Number(WorldProposalTransition.TargetProfileVersion),
            Id(request.CauseId), Id(request.WorldId), Id(request.BranchId), Number(request.ExpectedRevision),
            Number(request.ExpectedAdvancingSteps), request.ExpectedHeadHash.ToByteArray(),
            request.SourceAdmissionVersion.ToByteArray(), request.ExpectedAnchorHash?.ToByteArray() ?? [],
            Number(request.TransitionOrdinal));
    }

    public static WorldProposalTransition Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumByteLength, FieldCount);
            reader.Tag("world.proposal-transition"u8);
            reader.Version();
            if (reader.Number() != WorldProposalTransition.SourceProfileVersion
                || reader.Number() != WorldProposalTransition.TargetProfileVersion)
            {
                throw new FormatException("The proposal transition names an unsupported interpretation pair.");
            }
            EntityId cause = reader.Identity();
            EntityId world = reader.Identity();
            EntityId branch = reader.Identity();
            ulong revision = reader.Number();
            ulong steps = reader.Number();
            ContentHash head = Hash(reader.Exact(HashByteLength));
            ContentHash admission = Hash(reader.Exact(HashByteLength));
            ReadOnlySpan<byte> anchor = reader.Field(HashByteLength);
            if (!anchor.IsEmpty && anchor.Length != HashByteLength)
            {
                throw new FormatException("The proposal transition anchor pin is not an exact digest.");
            }
            ulong ordinal = reader.Number();
            reader.Complete();
            var result = new WorldProposalTransition(cause, world, branch, revision, steps, head, admission,
                anchor.IsEmpty ? null : Hash(anchor), ordinal);
            if (!bytes.SequenceEqual(Encode(result)))
            {
                throw new FormatException("The proposal transition is not canonically encoded.");
            }
            return result;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("Invalid proposal transition.", error);
        }
    }

    public static byte[] EncodeOutcome(WorldProposalTransitionDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return CanonicalEncodingV1.Frame("world.proposal-transition-outcome", Number(1), Id(decision.CauseId),
            Number(decision.Candidate.Revision), Number(decision.Candidate.ProfileVersion),
            decision.Candidate.ContentHash.ToByteArray(), decision.Archive.Reference.Encode(),
            decision.Archive.Anchor.ContentHash.ToByteArray());
    }

    private static ContentHash Hash(ReadOnlySpan<byte> bytes) => new(Convert.ToHexString(bytes).ToLowerInvariant());
    private static byte[] Id(EntityId value) => value.ToByteArray();
    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
}
