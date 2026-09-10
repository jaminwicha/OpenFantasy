using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Worlds;

/// <summary>One exact upgrade interpreter retained alongside earlier world journal formats.</summary>
public static class WorldProductionMigrationCodecV1
{
    public const int MaximumMigrationByteLength = 256 * 1024;

    public static byte[] Encode(WorldProductionMigration request)
    {
        ArgumentNullException.ThrowIfNull(request);
        byte[] bytes = CanonicalEncodingV1.Frame("world.production-migration", Number(1), request.CauseId.ToByteArray(),
            request.WorldId.ToByteArray(), request.BranchId.ToByteArray(), Number(request.ExpectedRevision),
            Number(request.ExpectedAdvancingSteps), request.ExpectedHeadHash.ToByteArray(), request.SourceAdmissionVersion.ToByteArray(),
            request.TargetAdmissionVersion.ToByteArray(), ProductionCodecV1.Encode(request.Production));
        if (bytes.Length > MaximumMigrationByteLength)
        {
            throw new ArgumentException("The migration exceeds its retained byte bound.", nameof(request));
        }
        return bytes;
    }

    public static WorldProductionMigration Decode(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumMigrationByteLength, 11);
        reader.Tag("world.production-migration"u8);
        reader.Version();
        EntityId cause = reader.Identity();
        EntityId world = reader.Identity();
        EntityId branch = reader.Identity();
        ulong revision = reader.Number();
        ulong steps = reader.Number();
        ContentHash head = ReadHash(ref reader);
        ContentHash source = ReadHash(ref reader);
        ContentHash target = ReadHash(ref reader);
        ProductionSnapshot production = ProductionCodecV1.Decode(reader.Field(MaximumMigrationByteLength));
        reader.Complete();
        try
        {
            var restored = new WorldProductionMigration(cause, world, branch, revision, steps, head, source, target, production);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The production migration is not canonically encoded.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            throw new FormatException("The production migration violates its invariants.", error);
        }
    }

    public static byte[] EncodeOutcome(WorldProductionMigrationDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return CanonicalEncodingV1.Frame("world.production-migration-outcome", Number(1), decision.CauseId.ToByteArray(),
            Number(decision.Candidate.Revision), Number(decision.Candidate.ProfileVersion), decision.Candidate.ContentHash.ToByteArray(),
            CanonicalEncodingV1.Frame("world.production-definitions", decision.AdmittedDefinitionIds.Select(identity =>
                (ReadOnlyMemory<byte>)identity.ToByteArray()).ToArray()));
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
    private static ContentHash ReadHash(ref WorldFrameReader reader) => new(Convert.ToHexString(reader.Exact(32)).ToLowerInvariant());
}
