using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;

namespace OpenFantasy.Core.Worlds;

/// <summary>Bounded quest upgrade bytes retained within the existing 256 KiB journal request limit.</summary>
public static class WorldQuestMigrationCodecV1
{
    public const int MaximumMigrationByteLength = 256 * 1024;
    public const int MaximumAdmissionByteLength = 255 * 1024;
    private const int MaximumProjectByteLength = 256;

    public static byte[] Encode(WorldQuestMigration request)
    {
        ArgumentNullException.ThrowIfNull(request);
        byte[] bytes = Frame("world.quest-migration", Number(1), Id(request.CauseId), Id(request.WorldId), Id(request.BranchId),
            Number(request.ExpectedRevision), Number(request.ExpectedAdvancingSteps), request.ExpectedHeadHash.ToByteArray(),
            request.SourceAdmissionVersion.ToByteArray(), request.TargetAdmissionVersion.ToByteArray(), EncodeAdmission(request.Admission));
        RequireBound(bytes, MaximumMigrationByteLength);
        return bytes;
    }

    public static WorldQuestMigration Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumMigrationByteLength, 11);
            reader.Tag("world.quest-migration"u8);
            reader.Version();
            var restored = new WorldQuestMigration(reader.Identity(), reader.Identity(), reader.Identity(), reader.Number(),
                reader.Number(), Hash(ref reader), Hash(ref reader), Hash(ref reader),
                DecodeAdmission(reader.Field(MaximumAdmissionByteLength)));
            reader.Complete();
            RequireCanonical(bytes, Encode(restored));
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The quest migration violates its exact admission invariants.", error);
        }
    }

    public static byte[] EncodeAdmission(WorldQuestAdmission admission)
    {
        ArgumentNullException.ThrowIfNull(admission);
        byte[] bytes = Frame("world.quest-admission", Number(1), admission.ContentVersion.ToByteArray(),
            ProgressionCodecV1.Encode(admission.Progression), QuestCodecV1.Encode(admission.Quests),
            Frame("world.quest-projects", admission.Projects.Select(project => (ReadOnlyMemory<byte>)EncodeProject(project)).ToArray()));
        RequireBound(bytes, MaximumAdmissionByteLength);
        return bytes;
    }

    public static WorldQuestAdmission DecodeAdmission(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumAdmissionByteLength, 6);
            reader.Tag("world.quest-admission"u8);
            reader.Version();
            ContentHash content = Hash(ref reader);
            ProgressionSnapshot progression = ProgressionCodecV1.Decode(reader.Field(ProgressionCodecV1.MaximumSnapshotByteLength));
            QuestSnapshot quests = QuestCodecV1.Decode(reader.Field(QuestCodecV1.MaximumSnapshotByteLength));
            var projects = new WorldFrameReader(reader.Field(MaximumAdmissionByteLength), MaximumAdmissionByteLength,
                WorldQuestAdmission.MaximumProjects + 1);
            projects.Tag("world.quest-projects"u8);
            List<QuestProjectCustody> values = [];
            while (projects.Remaining != 0)
            {
                values.Add(DecodeProject(projects.Field(MaximumProjectByteLength)));
            }
            projects.Complete();
            reader.Complete();
            var restored = new WorldQuestAdmission(content, progression, quests, values);
            RequireCanonical(bytes, EncodeAdmission(restored));
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The quest admission violates its fresh state or project invariants.", error);
        }
    }

    public static byte[] EncodeOutcome(WorldQuestMigrationDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return Frame("world.quest-migration-outcome", Number(1), Id(decision.CauseId), Number(decision.Candidate.Revision),
            Number(decision.Candidate.ProfileVersion), decision.Candidate.ContentHash.ToByteArray(),
            Frame("world.quest-definitions", decision.AdmittedDefinitionIds.Select(value => (ReadOnlyMemory<byte>)Id(value)).ToArray()),
            Frame("world.quest-entities", decision.AdmittedEntityIds.Select(value => (ReadOnlyMemory<byte>)Id(value)).ToArray()));
    }

    private static byte[] EncodeProject(QuestProjectCustody project) => Frame("world.quest-project", Number(1),
        Id(project.CustodianId), Id(project.OwnerActorId), Number(project.UnitCapacity), Id(project.ReservationId),
        Number((ulong)project.DepositRule));

    private static QuestProjectCustody DecodeProject(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumProjectByteLength, 7);
        reader.Tag("world.quest-project"u8);
        reader.Version();
        var result = new QuestProjectCustody(reader.Identity(), reader.Identity(), reader.Number(), reader.Identity(),
            (CustodyDepositRule)checked((int)reader.Number()));
        reader.Complete();
        return result;
    }

    private static byte[] Frame(string tag, params ReadOnlyMemory<byte>[] fields) => CanonicalEncodingV1.Frame(tag, fields);
    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
    private static byte[] Id(EntityId value) => value.ToByteArray();
    private static ContentHash Hash(ref WorldFrameReader reader) => new(Convert.ToHexString(reader.Exact(32)).ToLowerInvariant());

    private static void RequireBound(byte[] bytes, int maximum)
    {
        if (bytes.Length > maximum)
        {
            throw new ArgumentException("The initial quest admission exceeds the retained journal byte bound.");
        }
    }

    private static void RequireCanonical(ReadOnlySpan<byte> original, ReadOnlySpan<byte> encoded)
    {
        if (!original.SequenceEqual(encoded))
        {
            throw new FormatException("The quest migration is not canonically encoded.");
        }
    }
}
