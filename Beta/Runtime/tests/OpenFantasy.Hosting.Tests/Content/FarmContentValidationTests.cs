using System.Text.Json;
using System.Text.Json.Nodes;
using OpenFantasy.Core.Content;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.Content;

public sealed class FarmContentValidationTests
{
    [Theory]
    [InlineData("packages/aldermere-first-loop.manifest.json")]
    [InlineData("legacy/aldermere-first-loop/closure-manifest.json")]
    [InlineData("legacy/aldermere-first-loop/records.json")]
    [InlineData("legacy/aldermere-first-loop/source-support/data/npcs.json")]
    [InlineData("legacy/aldermere-first-loop/assets/layers/body.png")]
    [InlineData("legacy/aldermere-first-loop/assets/units/veh_ship.png")]
    [InlineData("native-farm/admission-v1.json")]
    [InlineData("native-farm/presentation-v1.json")]
    public void Any_changed_pinned_package_or_admission_file_rejects(string relativePath)
    {
        using var fixture = new FarmContentFixture();
        string path = fixture.PathFor(relativePath);
        byte[] bytes = File.ReadAllBytes(path);
        bytes[bytes.Length / 2] ^= 1;
        File.WriteAllBytes(path, bytes);
        Assert.Throws<InvalidDataException>(() => FarmWorldAdmission.Create(fixture.ContentRoot, "river", "Orin"));
    }

    [Fact]
    public void Missing_required_source_asset_has_no_fallback_or_partial_world()
    {
        using var fixture = new FarmContentFixture();
        File.Delete(fixture.PathFor("legacy/aldermere-first-loop/assets/layers/hair_05.png"));
        Assert.Throws<FileNotFoundException>(() => FarmWorldAdmission.Create(fixture.ContentRoot, "river", "Orin"));
        Assert.Empty(Directory.EnumerateFiles(fixture.ContentRoot, "*.sqlite", SearchOption.AllDirectories));
    }

    [Fact]
    public void Oversized_authored_metadata_rejects_before_json_expansion()
    {
        using var fixture = new FarmContentFixture();
        File.WriteAllBytes(fixture.PathFor("native-farm/admission-v1.json"), new byte[32_769]);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => FarmWorldAdmission.Create(fixture.ContentRoot, "river", "Orin"));
        Assert.Contains("byte limit", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Recomputed_local_pins_cannot_admit_an_unreviewed_source_replacement()
    {
        using var fixture = new FarmContentFixture();
        string recordsPath = fixture.PathFor("legacy/aldermere-first-loop/records.json");
        JsonNode records = JsonNode.Parse(File.ReadAllBytes(recordsPath))!;
        JsonNode wren = records["records"]!.AsArray().Single(record => record!["legacyId"] is JsonValue id
            && id.TryGetValue<string>(out string? text) && text == "farmer_wren")!;
        wren["sourcePayload"]!["dialog"] = "This changed statement has not been admitted.";
        using JsonDocument payload = JsonDocument.Parse(wren["sourcePayload"]!.ToJsonString());
        string changedPayloadHash = FarmCanonicalJson.Hash(payload.RootElement).Value;
        wren["sourceIdentity"]!["payloadSha256"] = changedPayloadHash;
        File.WriteAllText(recordsPath, records.ToJsonString());
        string admissionPath = fixture.PathFor("native-farm/admission-v1.json");
        JsonNode admission = JsonNode.Parse(File.ReadAllBytes(admissionPath))!;
        admission["recordsSha256"] = ContentHash.Compute(File.ReadAllBytes(recordsPath)).Value;
        JsonNode reference = admission["sourceRecords"]!.AsArray().Single(record =>
            record!["targetKey"]!.GetValue<string>() == "openfantasy.aldermere:npcs/farmer_wren")!;
        reference["sourceIdentity"]!["payloadSha256"] = changedPayloadHash;
        File.WriteAllText(admissionPath, admission.ToJsonString());
        Assert.Throws<InvalidDataException>(() => FarmWorldAdmission.Create(fixture.ContentRoot, "river", "Orin"));
    }

    [Fact]
    public void Successful_genesis_is_read_only_and_preserves_original_appearance_witnesses()
    {
        using var fixture = new FarmContentFixture();
        Dictionary<string, ContentHash> before = FileHashes(fixture.ContentRoot);
        FarmWorldAdmission.Create(fixture.ContentRoot, "river", "Orin");
        Dictionary<string, ContentHash> after = FileHashes(fixture.ContentRoot);
        Assert.Equal(before.Keys.Order(), after.Keys.Order());
        Assert.All(before, pair => Assert.Equal(pair.Value, after[pair.Key]));
        using JsonDocument presentation = JsonDocument.Parse(File.ReadAllBytes(fixture.PathFor("native-farm/presentation-v1.json")));
        JsonElement appearance = presentation.RootElement.GetProperty("sourceAppearance");
        var expected = new Dictionary<string, int> { ["race"] = 0, ["skin"] = 5, ["hairStyle"] = 5, ["hairColor"] = 13,
            ["outfit"] = 8, ["outfitA"] = 10, ["outfitB"] = 10, ["acc"] = 3 };
        Assert.Equal(expected.Count, appearance.EnumerateObject().Count());
        Assert.All(expected, pair => Assert.Equal(pair.Value, appearance.GetProperty(pair.Key).GetInt32()));
        Assert.Equal("", presentation.RootElement.GetProperty("sourceSprite").GetString());
        Assert.Equal(5, presentation.RootElement.GetProperty("assets").GetArrayLength());
        Assert.Equal(3, presentation.RootElement.GetProperty("sourceSemantics").GetArrayLength());
    }

    private static Dictionary<string, ContentHash> FileHashes(string root) => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(root, path), path => ContentHash.Compute(File.ReadAllBytes(path)), StringComparer.Ordinal);
}
