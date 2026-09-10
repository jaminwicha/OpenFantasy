using System.Text.Json;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Construction.ConstructionFixture;

namespace OpenFantasy.Core.Tests.Construction;

public sealed class ConstructionGoldenTests
{
    [Fact]
    public void Independent_python_bytes_match_geometry_progress_actions_and_finite_admission()
    {
        using JsonDocument json = Read();
        ConstructionDefinition definition = Definition();
        AssertVector(json, "definition", ConstructionCodecV1.EncodeDefinition(definition));
        AssertVector(json, "fresh", ConstructionCodecV1.Encode(Snapshot(0)));
        AssertVector(json, "partial", ConstructionCodecV1.Encode(Snapshot(40)));
        AssertVector(json, "completed", ConstructionCodecV1.Encode(Snapshot(200)));
        WorldActionCommand action = Action();
        AssertVector(json, "action", WorldActionCodec.Encode(action));
        var second = new WorldActionCommand(Id(95), action.PrincipalId, action.OriginSessionId, Id(4),
            action.WorldId, action.BranchId, action.ExpectedActorRevision, action.Intent);
        var movement = new WorldMovementInput(new TrustedCommandDelivery(Id(91), Id(92)), Id(2), -32, 0);
        var batch = new WorldStepBatch(Id(96), Id(1), Id(3), 50, 45, WorldBatchKind.AdvancingStep,
            [movement], [second, action], 5);
        AssertVector(json, "batch", WorldStepCodec.Encode(batch));
        WorldConstructionAdmission admission = Admission();
        AssertVector(json, "admission", WorldConstructionMigrationCodecV1.EncodeAdmission(admission));
        ContentHash source = ContentHash.Compute("construction-source-profile"u8);
        ContentHash target = WorldConstructionMigration.ComputeTargetAdmissionVersion(source, admission);
        Assert.Equal(json.RootElement.GetProperty("targetAdmissionVersion").GetString(), target.Value);
        var request = new WorldConstructionMigration(Id(97), Id(1), Id(3), 123, 100,
            ContentHash.Compute("construction-source-head"u8), source, target, admission);
        AssertVector(json, "migration", WorldConstructionMigrationCodecV1.Encode(request));
    }

    [Fact]
    public void Every_independent_positive_roundtrips_and_every_prefix_is_rejected()
    {
        using JsonDocument json = Read();
        foreach (string key in new[] { "definition", "fresh", "partial", "completed", "action", "batch", "admission", "migration" })
        {
            JsonElement example = json.RootElement.GetProperty(key);
            string kind = example.GetProperty("kind").GetString()!;
            byte[] bytes = Convert.FromHexString(example.GetProperty("hex").GetString()!);
            Assert.Equal(bytes, Roundtrip(kind, bytes));
            for (int length = 0; length < bytes.Length; length++)
            {
                byte[] prefix = bytes[..length];
                Assert.ThrowsAny<FormatException>(() => Roundtrip(kind, prefix));
            }
        }
    }

    [Fact]
    public void Independently_mutated_frames_fail_closed_and_over_limit_inputs_never_decode()
    {
        using JsonDocument json = Read();
        foreach (JsonElement example in json.RootElement.GetProperty("malformed").EnumerateArray())
        {
            string kind = example.GetProperty("kind").GetString()!;
            byte[] bytes = Convert.FromHexString(example.GetProperty("hex").GetString()!);
            Exception? failure = Record.Exception(() => Roundtrip(kind, bytes));
            Assert.True(failure is FormatException,
                $"{kind}/{example.GetProperty("name").GetString()}: expected FormatException, got {failure?.GetType().Name ?? "accepted"}");
        }
        foreach ((string kind, int limit) in new[] {
            ("definition", ConstructionCodecV1.MaximumDefinitionByteLength),
            ("snapshot", ConstructionCodecV1.MaximumSnapshotByteLength),
            ("action", WorldActionCodec.MaximumCommandByteLength),
            ("batch", WorldStepCodecV5.MaximumBatchByteLength),
            ("admission", WorldConstructionMigrationCodecV1.MaximumAdmissionByteLength),
            ("migration", WorldConstructionMigrationCodecV1.MaximumMigrationByteLength) })
        {
            Assert.ThrowsAny<FormatException>(() => Roundtrip(kind, new byte[limit + 1]));
        }
    }

    [Fact]
    public void Installed_output_cannot_be_renamed_or_revised_before_restoration()
    {
        ConstructionProject completed = Snapshot(200).Projects.Single();
        InstalledStructure installed = completed.Installed!;
        Assert.Throws<ArgumentException>(() => Project(200, installed with { GuardLotId = Id(98) }));
        Assert.Throws<ArgumentException>(() => Project(200, installed with { GuardLotRevision = 1 }));
        Assert.Equal(EntityId.DeriveSpawned(Id(90), 1), installed.GuardLotId);
        Assert.Equal(0UL, installed.GuardLotRevision);
    }

    private static ConstructionProject Project(ulong units, InstalledStructure? installation = null)
    {
        ConstructionDefinition definition = Definition();
        var progress = ExposureProgress.Begin(Id(70), definition.WorkRate)
            .Advance(LocalExposureDuration.FromMilliseconds(checked(units * 1800)));
        if (units == 200)
        {
            installation ??= new InstalledStructure(EntityId.DeriveSpawned(Id(90), 3), EntityId.DeriveSpawned(Id(90), 1),
                0, EntityId.DeriveSpawned(Id(90), 0), Id(90));
        }
        return new ConstructionProject(Id(70), definition, Id(2), Id(2), Id(71), Id(72), Id(73),
            new(660, -256, 256), new(512, 0, 256),
            [new(Id(2), new(512, 0, 256), new(768, 512, 256)), new(Id(2), new(512, 0, 256), new(768, 256, 256))],
            units, progress.Capture(), installation);
    }

    private static ConstructionSnapshot Snapshot(ulong units)
    {
        ConstructionProject project = Project(units);
        return new ConstructionSnapshot(Id(1), Id(3), units, [project.Definition], [project]);
    }

    private static WorldConstructionAdmission Admission()
    {
        ConstructionSnapshot construction = Snapshot(0);
        var tool = new ConstructionToolAdmission(Id(2), Id(74),
            new AdmittedCountedSource(Id(81), construction.Definitions.Single().RequiredTool.Item, 1), Id(80));
        return new WorldConstructionAdmission(Pin, construction, [tool]);
    }

    private static WorldActionCommand Action() => new(Id(90), Id(91), Id(92), Id(2), Id(1), Id(3), 8,
        new WorkConstructionIntent(Id(70), 40, 7, Id(80), 0));

    private static byte[] Roundtrip(string kind, byte[] bytes) => kind switch
    {
        "definition" => ConstructionCodecV1.EncodeDefinition(ConstructionCodecV1.DecodeDefinition(bytes)),
        "snapshot" => ConstructionCodecV1.Encode(ConstructionCodecV1.Decode(bytes)),
        "action" => WorldActionCodecV5.Encode(WorldActionCodecV5.Decode(bytes)),
        "batch" => WorldStepCodecV5.Encode(WorldStepCodecV5.Decode(bytes)),
        "admission" => WorldConstructionMigrationCodecV1.EncodeAdmission(WorldConstructionMigrationCodecV1.DecodeAdmission(bytes)),
        "migration" => WorldConstructionMigrationCodecV1.Encode(WorldConstructionMigrationCodecV1.Decode(bytes)),
        _ => throw new InvalidOperationException("Unknown test vector kind.")
    };

    private static void AssertVector(JsonDocument json, string key, byte[] actual)
    {
        JsonElement expected = json.RootElement.GetProperty(key);
        Assert.Equal(Convert.FromHexString(expected.GetProperty("hex").GetString()!), actual);
        Assert.Equal(expected.GetProperty("bytes").GetInt32(), actual.Length);
        Assert.Equal(expected.GetProperty("sha256").GetString(), ContentHash.Compute(actual).Value);
    }

    private static JsonDocument Read() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Construction", "construction-v1.json")));
}
