using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Construction;

/// <summary>Canonical finite assembly metadata, qualified work progress and exclusive installed output bindings.</summary>
public static class ConstructionCodecV1
{
    public const int MaximumSnapshotByteLength = 3 * 1024 * 1024;
    public const int MaximumDefinitionByteLength = 160 * 1024;
    public const int MaximumProjectByteLength = 12 * 1024;

    public static byte[] Encode(ConstructionSnapshot snapshot) => Bounded(Frame("construction.snapshot",
        Number(1), Id(snapshot.WorldId), Id(snapshot.BranchId), Number(snapshot.Revision),
        Collection("construction.definitions", snapshot.Definitions.Select(EncodeDefinition)),
        Collection("construction.projects", snapshot.Projects.Select(EncodeProject))), MaximumSnapshotByteLength);

    public static ConstructionSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumSnapshotByteLength, 7);
            reader.Tag("construction.snapshot"u8);
            reader.Version();
            EntityId world = reader.Identity();
            EntityId branch = reader.Identity();
            ulong revision = reader.Number();
            var definitions = new WorldFrameReader(reader.Field(MaximumSnapshotByteLength), MaximumSnapshotByteLength,
                ConstructionSnapshot.MaximumDefinitions + 1);
            definitions.Tag("construction.definitions"u8);
            List<ConstructionDefinition> values = [];
            while (definitions.Remaining != 0)
            {
                values.Add(DecodeDefinition(definitions.Field(MaximumDefinitionByteLength)));
            }
            definitions.Complete();
            var projects = new WorldFrameReader(reader.Field(MaximumSnapshotByteLength), MaximumSnapshotByteLength,
                ConstructionSnapshot.MaximumProjects + 1);
            projects.Tag("construction.projects"u8);
            List<ConstructionProject> entries = [];
            while (projects.Remaining != 0)
            {
                entries.Add(DecodeProject(projects.Field(MaximumProjectByteLength), values));
            }
            projects.Complete();
            reader.Complete();
            var result = new ConstructionSnapshot(world, branch, revision, values, entries);
            RequireCanonical(bytes, Encode(result));
            return result;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("Invalid construction snapshot.", error);
        }
    }

    public static byte[] EncodeDefinition(ConstructionDefinition definition) => Bounded(Frame("construction.definition",
        Number(1), Id(definition.DefinitionId), Text(definition.Key), Text(definition.DisplayName),
        ProductionCodecV1.EncodeRecipe(definition.MaterialTransform), Number(definition.InstalledOutputIndex),
        EncodeTool(definition.RequiredTool), Collection("construction.members", definition.Members.Select(member =>
            Frame("construction.member", Signed(member.MinX), Signed(member.MinY), Signed(member.MinZ),
                Signed(member.MaxX), Signed(member.MaxY), Signed(member.MaxZ)))),
        Number(definition.RequiredWork.Value), EncodeRate(definition.WorkRate), Number((ulong)definition.StockInterpretation)),
        MaximumDefinitionByteLength);

    public static ConstructionDefinition DecodeDefinition(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumDefinitionByteLength, 12);
            reader.Tag("construction.definition"u8);
            reader.Version();
            EntityId id = reader.Identity();
            string key = reader.Text(96);
            string name = reader.Text(512);
            CraftRecipeDefinition recipe = ProductionCodecV1.DecodeRecipe(reader.Field(ProductionCodecV1.MaximumRecipeByteLength));
            uint outputIndex = checked((uint)reader.Number());
            ConstructionToolDefinition tool = DecodeTool(reader.Field(512));
            var members = new WorldFrameReader(reader.Field(8 * 1024), 8 * 1024, ConstructionDefinition.MaximumMembers + 1);
            members.Tag("construction.members"u8);
            List<MillimetreBox> boxes = [];
            while (members.Remaining != 0)
            {
                var member = new WorldFrameReader(members.Field(128), 128, 7);
                member.Tag("construction.member"u8);
                boxes.Add(new MillimetreBox(member.Signed(), member.Signed(), member.Signed(),
                    member.Signed(), member.Signed(), member.Signed()));
                member.Complete();
            }
            members.Complete();
            var required = new WorkUnits(reader.Number());
            ExposureRate rate = DecodeRate(reader.Field(256));
            var stock = (ConstructionStockInterpretation)checked((int)reader.Number());
            reader.Complete();
            var result = new ConstructionDefinition(id, key, name, recipe, outputIndex, tool, boxes, required, rate, stock);
            RequireCanonical(bytes, EncodeDefinition(result));
            return result;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("Invalid construction definition.", error);
        }
    }

    public static byte[] EncodeTool(ConstructionToolDefinition tool) => Frame("construction.tool", Number(1),
        Id(tool.DefinitionId), Id(tool.Item.DefinitionId), tool.Item.Version.ToByteArray(), Number((ulong)tool.Operation));

    private static ConstructionToolDefinition DecodeTool(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, 512, 6);
        reader.Tag("construction.tool"u8);
        reader.Version();
        EntityId id = reader.Identity();
        var item = new CountedItemDefinition(reader.Identity(), Hash(ref reader));
        var operation = (ConstructionToolOperation)checked((int)reader.Number());
        reader.Complete();
        return new ConstructionToolDefinition(id, item, operation);
    }

    private static byte[] EncodeProject(ConstructionProject project) => Bounded(Frame("construction.project", Number(1),
        Id(project.ProjectId), Id(project.Definition.DefinitionId), project.Definition.Version.ToByteArray(),
        Id(project.OwnerActorId), Id(project.BuilderActorId), Id(project.QuestId), Id(project.ProjectCustodianId),
        Id(project.ReservationId), Position(project.SiteFeet), Position(project.WorkFeet),
        Collection("construction.access", project.RequiredAccess.Select(value => Frame("construction.access-route",
            Id(value.ActorId), Position(value.From), Position(value.To)))),
        Number(project.Revision), ExposureEncodingV1.Encode(project.Progress),
        project.Installed is { } installed ? Frame("construction.installed", Id(installed.StructureId),
            Id(installed.GuardLotId), Number(installed.GuardLotRevision), Id(installed.ProductionBatchId),
            Id(installed.CompletionCommandId)) : Array.Empty<byte>(),
        Number((ulong)project.StockLocation)), MaximumProjectByteLength);

    private static ConstructionProject DecodeProject(ReadOnlySpan<byte> bytes, IReadOnlyList<ConstructionDefinition> definitions)
    {
        var reader = new WorldFrameReader(bytes, MaximumProjectByteLength, 17);
        reader.Tag("construction.project"u8);
        reader.Version();
        EntityId id = reader.Identity();
        EntityId definitionId = reader.Identity();
        ContentHash version = Hash(ref reader);
        ConstructionDefinition definition = definitions.SingleOrDefault(value => value.DefinitionId == definitionId
            && value.Version == version) ?? throw new FormatException("Unknown construction definition pin.");
        EntityId owner = reader.Identity();
        EntityId builder = reader.Identity();
        EntityId quest = reader.Identity();
        EntityId custodian = reader.Identity();
        EntityId reservation = reader.Identity();
        SubunitPosition site = ReadPosition(reader.Field(96));
        SubunitPosition work = ReadPosition(reader.Field(96));
        var access = new WorldFrameReader(reader.Field(8 * 1024), 8 * 1024, ConstructionProject.MaximumAccessRequirements + 1);
        access.Tag("construction.access"u8);
        List<GroundAccessRequirement> requirements = [];
        while (access.Remaining != 0)
        {
            var route = new WorldFrameReader(access.Field(256), 256, 4);
            route.Tag("construction.access-route"u8);
            requirements.Add(new GroundAccessRequirement(route.Identity(), ReadPosition(route.Field(96)), ReadPosition(route.Field(96))));
            route.Complete();
        }
        access.Complete();
        ulong revision = reader.Number();
        ExposureProgressState progress = DecodeProgress(reader.Field(384));
        ReadOnlySpan<byte> installedBytes = reader.Field(192);
        InstalledStructure? installed = null;
        if (!installedBytes.IsEmpty)
        {
            var record = new WorldFrameReader(installedBytes, 192, 6);
            record.Tag("construction.installed"u8);
            installed = new InstalledStructure(record.Identity(), record.Identity(), record.Number(), record.Identity(), record.Identity());
            record.Complete();
        }
        var location = (ConstructionStockLocation)checked((int)reader.Number());
        reader.Complete();
        return new ConstructionProject(id, definition, owner, builder, quest, custodian, reservation,
            site, work, requirements, revision, progress, installed, location);
    }

    private static byte[] EncodeRate(ExposureRate rate) => Frame("construction.rate", Number(1), Id(rate.OutputUnitId),
        rate.RulesVersion.ToByteArray(), Number(rate.Numerator), Number(rate.Denominator));

    private static ExposureRate DecodeRate(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, 256, 6);
        reader.Tag("construction.rate"u8);
        reader.Version();
        var rate = new ExposureRate(reader.Identity(), Hash(ref reader), reader.Number(), reader.Number());
        reader.Complete();
        return rate;
    }

    private static ExposureProgressState DecodeProgress(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, 384, 10);
        reader.Tag("exposure.progress.v1"u8);
        EntityId subject = reader.Identity();
        var rate = new ExposureRate(reader.Identity(), Hash(ref reader), reader.Number(), reader.Number());
        var elapsed = new LocalExposureDuration(reader.Number(), checked((uint)reader.Number()));
        var state = new ExposureProgressState(subject, rate, elapsed, reader.Number(), reader.Number());
        reader.Complete();
        ExposureProgress.Restore(state, rate);
        return state;
    }

    private static byte[] Position(SubunitPosition value) => Frame("construction.position", Signed(value.X), Signed(value.Y), Signed(value.Z));
    private static SubunitPosition ReadPosition(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, 96, 4);
        reader.Tag("construction.position"u8);
        var result = new SubunitPosition(reader.Signed(), reader.Signed(), reader.Signed());
        reader.Complete();
        return result;
    }

    private static void RequireCanonical(ReadOnlySpan<byte> original, ReadOnlySpan<byte> encoded)
    {
        if (!original.SequenceEqual(encoded))
        {
            throw new FormatException("Construction fields are not in their exact canonical order.");
        }
    }

    private static byte[] Bounded(byte[] bytes, int maximum) => bytes.Length <= maximum ? bytes
        : throw new ArgumentException("The construction frame exceeds its qualified byte limit.");
    private static ContentHash Hash(ref WorldFrameReader reader) => new(Convert.ToHexString(reader.Exact(32)).ToLowerInvariant());
    private static byte[] Id(EntityId value) => value.ToByteArray();
    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
    private static byte[] Signed(long value) => WorldSpatialCodecV1.Signed(value);
    private static byte[] Text(string value) => CanonicalEncodingV1.Text(value, nameof(value));
    private static byte[] Collection(string tag, IEnumerable<byte[]> values) => Frame(tag, values.Select(value => (ReadOnlyMemory<byte>)value).ToArray());
    private static byte[] Frame(string tag, params ReadOnlyMemory<byte>[] fields) => CanonicalEncodingV1.Frame(tag, fields);
}
