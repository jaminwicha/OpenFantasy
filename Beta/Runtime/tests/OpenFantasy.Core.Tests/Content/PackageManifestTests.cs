using System.Globalization;
using System.Text;
using System.Text.Json;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Tests.Content;

public sealed class PackageManifestTests
{
    private static readonly ContentHash _payloadHash = ContentHash.Compute("payload"u8);

    [Fact]
    public void CanonicalBytesAndVersionsMatchIndependentPythonVectors()
    {
        using JsonDocument vectors = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "packages-v1.json")));
        JsonElement expected = vectors.RootElement;
        var hash = new ContentHash(expected.GetProperty("payloadSha256").GetString()!);
        var rules = new PackageManifest(new PackageId("openfantasy.rules"), hash, []);
        var area = new PackageManifest(new PackageId("openfantasy.aldermere"), hash,
            [new PackageDependency(rules.Id, rules.Version)]);

        Assert.Equal(expected.GetProperty("baseCanonicalHex").GetString(),
            Convert.ToHexString(rules.ToCanonicalBytes()).ToLowerInvariant());
        Assert.Equal(expected.GetProperty("baseVersion").GetString(), rules.Version.Value);
        Assert.Equal(expected.GetProperty("dependentCanonicalHex").GetString(),
            Convert.ToHexString(area.ToCanonicalBytes()).ToLowerInvariant());
        Assert.Equal(expected.GetProperty("dependentVersion").GetString(), area.Version.Value);
        rules.ValidatePayload(Encoding.UTF8.GetBytes(expected.GetProperty("payloadText").GetString()!));
    }

    [Fact]
    public void ManifestCopiesAndSortsDependenciesAndExposesNoMutableBackingCollection()
    {
        var source = new[] { Dependency("z.assets"), Dependency("a.rules") };
        var manifest = new PackageManifest(new PackageId("game.content"), _payloadHash, source);
        byte[] canonical = manifest.ToCanonicalBytes();
        source[0] = Dependency("changed.source");
        Assert.Equal(new[] { "a.rules", "z.assets" }, manifest.Dependencies.Select(item => item.Id.Value));
        Assert.Throws<NotSupportedException>(() => ((IList<PackageDependency>)manifest.Dependencies).Clear());
        canonical[0] = 0;
        Assert.NotEqual(canonical, manifest.ToCanonicalBytes());
    }

    [Fact]
    public void VersionBindsNamespacePayloadAndDependencyPins()
    {
        var first = new PackageManifest(new PackageId("game.content"), _payloadHash, [Dependency("a.rules")]);
        Assert.NotEqual(first.Version, new PackageManifest(new PackageId("game.other"), _payloadHash,
            first.Dependencies).Version);
        Assert.NotEqual(first.Version, new PackageManifest(first.Id, ContentHash.Compute("other"u8),
            first.Dependencies).Version);
        Assert.NotEqual(first.Version, new PackageManifest(first.Id, _payloadHash,
            [new PackageDependency(new PackageId("a.rules"), ContentHash.Compute("another pin"u8))]).Version);
        Assert.Throws<InvalidDataException>(() => first.ValidatePayload("wrong"u8));
    }

    [Fact]
    public void DuplicateDependenciesAndExcessiveListsReject()
    {
        Assert.Throws<ArgumentException>(() => new PackageManifest(new PackageId("game.content"), _payloadHash,
            [Dependency("a.rules"), Dependency("a.rules")]));
        Assert.Throws<ArgumentException>(() => new PackageManifest(new PackageId("game.content"), _payloadHash,
            Enumerable.Range(0, 65).Select(index => Dependency("a.rules" + index))));
    }

    [Fact]
    public void JsonRoundTripAndFormattingOrderDoNotChangeCanonicalIdentity()
    {
        var manifest = new PackageManifest(new PackageId("game.content"), _payloadHash,
            [Dependency("z.assets"), Dependency("a.rules")]);
        PackageManifest restored = PackageManifestJson.Read(PackageManifestJson.Write(manifest));
        Assert.Equal(manifest.Version, restored.Version);
        var reversed = new PackageManifest(manifest.Id, manifest.PayloadHash, manifest.Dependencies.Reverse());
        Assert.Equal(manifest.ToCanonicalBytes(), reversed.ToCanonicalBytes());
        string reorderedJson = "{\"dependencies\":[],\"payloadSha256\":\"" + _payloadHash.Value
            + "\",\"id\":\"game.content\",\"schemaVersion\":1}";
        Assert.Equal(new PackageManifest(manifest.Id, _payloadHash, []).Version,
            PackageManifestJson.Read(Encoding.UTF8.GetBytes(reorderedJson)).Version);
    }

    [Theory]
    [InlineData("\"schemaVersion\":1,\"schemaVersion\":1", "duplicate")]
    [InlineData("\"schemaVersion\":2", "unknown version")]
    [InlineData("\"schemaVersion\":\"1\"", "wrong schema type")]
    [InlineData("\"schemaVersion\":1,\"unrecognized\":true", "unknown field")]
    [InlineData("\"otherVersion\":1", "missing schema")]
    public void JsonRejectsMalformedMetadata(string schema, string reason)
    {
        string json = "{" + schema + ",\"id\":\"game.content\",\"payloadSha256\":\""
            + _payloadHash.Value + "\",\"dependencies\":[]}";
        Assert.NotEmpty(reason);
        Assert.Throws<FormatException>(() => PackageManifestJson.Read(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void JsonRejectsOversizedTruncatedAndMalformedDependencyData()
    {
        Assert.Throws<FormatException>(() => PackageManifestJson.Read(new byte[65_537]));
        Assert.ThrowsAny<JsonException>(() => PackageManifestJson.Read("{"u8.ToArray()));
        string json = "{\"schemaVersion\":1,\"id\":\"game.content\",\"payloadSha256\":\""
            + _payloadHash.Value + "\",\"dependencies\":[{\"id\":\"a.rules\",\"id\":\"a.rules\","
            + "\"versionSha256\":\"" + _payloadHash.Value + "\"}]}";
        Assert.Throws<FormatException>(() => PackageManifestJson.Read(Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Upper.Case")]
    [InlineData("a..b")]
    [InlineData("a.")]
    [InlineData("a/b")]
    [InlineData("a.2b")]
    [InlineData("a.é")]
    public void NamespacesRejectNoncanonicalValues(string value) =>
        Assert.Throws<ArgumentException>(() => new PackageId(value));

    [Fact]
    public void DependencyOrderingIsIndependentOfCurrentCulture()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var manifest = new PackageManifest(new PackageId("game.content"), _payloadHash,
                [Dependency("i.rules"), Dependency("a.rules"), Dependency("z.rules")]);
            Assert.Equal(new[] { "a.rules", "i.rules", "z.rules" }, manifest.Dependencies.Select(item => item.Id.Value));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static PackageDependency Dependency(string id) => new(new PackageId(id), _payloadHash);
}
