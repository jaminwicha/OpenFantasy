using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Worlds;

/// <summary>Exact construction admission and outcome under the unchanged 256 KiB journal record limit.</summary>
public static class WorldConstructionMigrationCodecV1
{
    public const int MaximumMigrationByteLength = 256 * 1024;
    public const int MaximumAdmissionByteLength = 255 * 1024;

    public static byte[] Encode(WorldConstructionMigration request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Bounded(Frame("world.construction-migration", Number(1), Id(request.CauseId), Id(request.WorldId), Id(request.BranchId),
            Number(request.ExpectedRevision), Number(request.ExpectedAdvancingSteps), request.ExpectedHeadHash.ToByteArray(),
            request.SourceAdmissionVersion.ToByteArray(), request.TargetAdmissionVersion.ToByteArray(), EncodeAdmission(request.Admission)),
            MaximumMigrationByteLength);
    }

    public static WorldConstructionMigration Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumMigrationByteLength, 11);
            reader.Tag("world.construction-migration"u8);
            reader.Version();
            var result = new WorldConstructionMigration(reader.Identity(), reader.Identity(), reader.Identity(), reader.Number(),
                reader.Number(), Hash(ref reader), Hash(ref reader), Hash(ref reader), DecodeAdmission(reader.Field(MaximumAdmissionByteLength)));
            reader.Complete();
            Canonical(bytes, Encode(result));
            return result;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("Invalid construction migration.", error);
        }
    }

    public static byte[] EncodeAdmission(WorldConstructionAdmission admission)
    {
        ArgumentNullException.ThrowIfNull(admission);
        return Bounded(Frame("world.construction-admission", Number(1), admission.ContentVersion.ToByteArray(),
            ConstructionCodecV1.Encode(admission.Construction), Frame("world.construction-tools", admission.Tools.Select(tool =>
                (ReadOnlyMemory<byte>)Frame("world.construction-tool-supply", Number(1), Id(tool.OwnerActorId), Id(tool.CustodianId),
                    Id(tool.Source.SourceId), Id(tool.Source.Definition.DefinitionId), tool.Source.Definition.Version.ToByteArray(),
                    Number(tool.Source.UnitCapacity), Id(tool.LotId), Number(tool.StorageCapacityIncrease))).ToArray())),
            MaximumAdmissionByteLength);
    }

    public static WorldConstructionAdmission DecodeAdmission(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumAdmissionByteLength, 5);
            reader.Tag("world.construction-admission"u8);
            reader.Version();
            ContentHash content = Hash(ref reader);
            ConstructionSnapshot construction = ConstructionCodecV1.Decode(reader.Field(MaximumAdmissionByteLength));
            var tools = new WorldFrameReader(reader.Field(16 * 1024), 16 * 1024, WorldConstructionAdmission.MaximumTools + 1);
            tools.Tag("world.construction-tools"u8);
            List<ConstructionToolAdmission> supplies = [];
            while (tools.Remaining != 0)
            {
                var tool = new WorldFrameReader(tools.Field(384), 384, 10);
                tool.Tag("world.construction-tool-supply"u8);
                tool.Version();
                EntityId owner = tool.Identity();
                EntityId custodian = tool.Identity();
                EntityId source = tool.Identity();
                var item = new CountedItemDefinition(tool.Identity(), Hash(ref tool));
                ulong capacity = tool.Number();
                EntityId lot = tool.Identity();
                if (tool.Number() != 1)
                {
                    throw new FormatException("A kit requires its exact one-unit additional carrying allowance.");
                }
                tool.Complete();
                supplies.Add(new ConstructionToolAdmission(owner, custodian, new AdmittedCountedSource(source, item, capacity), lot));
            }
            tools.Complete();
            reader.Complete();
            var result = new WorldConstructionAdmission(content, construction, supplies);
            Canonical(bytes, EncodeAdmission(result));
            return result;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("Invalid fresh construction admission.", error);
        }
    }

    public static byte[] EncodeOutcome(WorldConstructionMigrationDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return Frame("world.construction-migration-outcome", Number(1), Id(decision.CauseId), Number(decision.Candidate.Revision),
            Number(decision.Candidate.ProfileVersion), decision.Candidate.ContentHash.ToByteArray(),
            Frame("world.construction-definitions", decision.AdmittedDefinitionIds.Select(value => (ReadOnlyMemory<byte>)Id(value)).ToArray()),
            Frame("world.construction-entities", decision.AdmittedEntityIds.Select(value => (ReadOnlyMemory<byte>)Id(value)).ToArray()));
    }

    private static byte[] Bounded(byte[] bytes, int maximum) => bytes.Length <= maximum ? bytes
        : throw new ArgumentException("Initial construction admission exceeds the frozen journal byte limit.");
    private static void Canonical(ReadOnlySpan<byte> original, ReadOnlySpan<byte> encoded)
    {
        if (!original.SequenceEqual(encoded))
        {
            throw new FormatException("Construction admission is not canonically encoded.");
        }
    }
    private static ContentHash Hash(ref WorldFrameReader reader) => new(Convert.ToHexString(reader.Exact(32)).ToLowerInvariant());
    private static byte[] Id(EntityId value) => value.ToByteArray();
    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
    private static byte[] Frame(string tag, params ReadOnlyMemory<byte>[] fields) => CanonicalEncodingV1.Frame(tag, fields);
}
