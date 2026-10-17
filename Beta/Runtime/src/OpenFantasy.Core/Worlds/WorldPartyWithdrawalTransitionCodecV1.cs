using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>Closed canonical request and outcome contracts for the 40 to 41 interpretation transition.</summary>
public static class WorldPartyWithdrawalTransitionCodecV1
{
    public const int MaximumByteLength = 1024;
    private const int FieldCount = 12;
    private const int HashByteLength = 32;

    public static byte[] Encode(WorldPartyWithdrawalTransition request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CanonicalEncodingV1.Frame("world.party-withdrawal-transition", Number(1),
            Number(WorldPartyWithdrawalTransition.SourceProfileVersion), Number(WorldPartyWithdrawalTransition.TargetProfileVersion),
            Id(request.CauseId), Id(request.WorldId), Id(request.BranchId), Number(request.ExpectedRevision),
            Number(request.ExpectedAdvancingSteps), request.ExpectedHeadHash.ToByteArray(),
            request.SourceAdmissionVersion.ToByteArray(), request.ExpectedAnchorHash?.ToByteArray() ?? []);
    }

    public static WorldPartyWithdrawalTransition Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumByteLength, FieldCount);
            reader.Tag("world.party-withdrawal-transition"u8);
            reader.Version();
            if (reader.Number() != WorldPartyWithdrawalTransition.SourceProfileVersion
                || reader.Number() != WorldPartyWithdrawalTransition.TargetProfileVersion)
            {
                throw new FormatException("The withdrawal transition names an unsupported interpretation pair.");
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
                throw new FormatException("The withdrawal transition anchor pin is not an exact digest.");
            }
            reader.Complete();
            var result = new WorldPartyWithdrawalTransition(cause, world, branch, revision, steps, head, admission,
                anchor.IsEmpty ? null : Hash(anchor));
            if (!bytes.SequenceEqual(Encode(result)))
            {
                throw new FormatException("The withdrawal transition is not canonically encoded.");
            }
            return result;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("Invalid withdrawal transition.", error);
        }
    }

    public static byte[] EncodeOutcome(WorldPartyWithdrawalTransitionDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return CanonicalEncodingV1.Frame("world.party-withdrawal-transition-outcome", Number(1), Id(decision.CauseId),
            Number(decision.Candidate.Revision), Number(decision.Candidate.ProfileVersion),
            decision.Candidate.ContentHash.ToByteArray());
    }

    private static ContentHash Hash(ReadOnlySpan<byte> bytes) => new(Convert.ToHexString(bytes).ToLowerInvariant());
    private static byte[] Id(EntityId value) => value.ToByteArray();
    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
}
