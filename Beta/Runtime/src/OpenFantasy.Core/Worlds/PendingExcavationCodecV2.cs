using OpenFantasy.Core.Content;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Worlds;

public static class PendingExcavationCodecV2
{
    public const int MaximumEncodedByteLength = WorldActionCodec.MaximumCommandByteLength + 2048;

    public static byte[] Encode(PendingExcavation pending) => CanonicalEncodingV1.Frame("world.pending-excavation.v2",
        WorldActionCodec.Encode(pending.Command), pending.TriggerCauseId.ToByteArray(),
        CanonicalEncodingV1.Unsigned64(pending.BasisRevision), pending.BasisHash.ToByteArray(),
        CanonicalEncodingV1.Unsigned64(pending.ContinuationCount), ConstructionSettlementResolutionCodecV2.Encode(pending.Resolution));

    public static PendingExcavation Decode(ReadOnlySpan<byte> bytes, WorldSnapshot stable)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 7);
            reader.Tag("world.pending-excavation.v2"u8);
            var command = WorldActionCodec.Decode(reader.Field(WorldActionCodec.MaximumCommandByteLength));
            var cause = reader.Identity();
            ulong revision = reader.Number();
            var hash = new ContentHash(Convert.ToHexString(reader.Exact(32)).ToLowerInvariant());
            ulong continuationCount = reader.Number();
            var resolutionBytes = reader.Field(ConstructionSettlementResolutionCodecV2.MaximumEncodedByteLength);
            reader.Complete();
            if (stable.ProfileVersion != 17 || stable.PendingExcavation is not null || command.Intent is not ExcavateIntent intent
                || revision >= stable.Revision || continuationCount >= PendingExcavation.MaximumContinuations(stable.ConsequencePolicy!, stable.CoupledSettlementPolicy!.MaximumWorkUnits)
                || checked(revision + 1 + continuationCount) != stable.Revision)
            {
                throw new FormatException("The pending excavation has no exact stable predecessor.");
            }
            var basis = stable.WithPending(null, revision);
            if (basis.ContentHash != hash) { throw new FormatException("The pending excavation basis was changed."); }
            var site = basis.Excavation!.Sites.Single(s => s.SiteId == intent.SiteId);
            var resolution = ConstructionSettlementResolutionCodecV2.Decode(resolutionBytes, basis,
                ExcavationPlanner.CandidateRegions(basis.Space, site));
            var pending = new PendingExcavation(command, cause, revision, hash, continuationCount, resolution, stable.ConsequencePolicy!);
            if (!Encode(pending).AsSpan().SequenceEqual(bytes)) { throw new FormatException("Noncanonical pending excavation."); }
            return pending;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The pending excavation violates its complete dependency contract.", error);
        }
    }
}
