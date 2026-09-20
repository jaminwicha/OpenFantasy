using OpenFantasy.Core.Content;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Worlds;

/// <summary>Exact event-ordered excavation command and dependency cursor within one reserved advancing interval.</summary>
public static class PendingExcavationCodecV3
{
    public const int MaximumEncodedByteLength = WorldActionCodec.MaximumCommandByteLength + 2048;

    public static byte[] Encode(PendingExcavation pending)
    {
        ArgumentNullException.ThrowIfNull(pending);
        return CanonicalEncodingV1.Frame("world.pending-excavation.v3",
            WorldActionCodec.Encode(pending.Command), pending.TriggerCauseId.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(pending.BasisRevision), pending.BasisHash.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(pending.ContinuationCount), ConstructionSettlementResolutionCodecV3.Encode(pending.Resolution));
    }

    public static PendingExcavation Decode(ReadOnlySpan<byte> bytes, WorldSnapshot stable,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stable);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 7);
            reader.Tag("world.pending-excavation.v3"u8);
            var command = WorldActionCodec.Decode(reader.Field(WorldActionCodec.MaximumCommandByteLength));
            var cause = reader.Identity();
            ulong revision = reader.Number();
            var hash = new ContentHash(Convert.ToHexString(reader.Exact(32)).ToLowerInvariant());
            ulong continuationCount = reader.Number();
            var resolutionBytes = reader.Field(ConstructionSettlementResolutionCodecV3.MaximumEncodedByteLength);
            reader.Complete();
            if (stable.ProfileVersion != 18 || stable.PendingExcavation is not null || command.Intent is not ExcavateIntent intent
                || revision >= stable.Revision || checked(revision + 1 + continuationCount) != stable.Revision)
            {
                throw new FormatException("The pending event excavation has no exact stable predecessor.");
            }
            var basis = stable.WithPending(null, revision);
            if (basis.ContentHash != hash)
            {
                throw new FormatException("The pending event excavation basis was changed.");
            }
            var site = basis.Excavation!.Sites.Single(site => site.SiteId == intent.SiteId);
            var resolution = ConstructionSettlementResolutionCodecV3.Decode(resolutionBytes, basis,
                ExcavationPlanner.CandidateRegions(basis.Space, site), cancellationToken);
            if (continuationCount >= PendingExcavation.MaximumContinuations(stable.ConsequencePolicy!, resolution.MaximumTotalWorkUnits)
                || continuationCount != 0 && resolution.IsComplete
                || resolution.CompletedWorkUnits != checked(continuationCount * (ulong)stable.ConsequencePolicy!.WorkUnitsPerContinuation))
            {
                throw new FormatException("The pending event excavation violates its actual work bound or exact continuation boundary.");
            }
            var pending = new PendingExcavation(command, cause, revision, hash, continuationCount, resolution, stable.ConsequencePolicy!);
            if (!Encode(pending).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("Noncanonical pending event excavation.");
            }
            return pending;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The pending event excavation violates its complete dependency contract.", error);
        }
    }
}
