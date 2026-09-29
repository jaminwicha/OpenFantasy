using System.Text.Json;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>
/// Authenticates the closed ledger of an actual native coordinated journey. Native input and rendering
/// evidence belongs to the runner; without the results variable this case is explicitly skipped.
/// </summary>
public sealed class CoordinatedNativeResultTests
{
    internal const string ResultsVariable = "OPENFANTASY_COORDINATED_NATIVE_RESULTS";
    private const string Label = "coordinated-revoke";
    private const string Mode = "revoke";
    private const string ReportKind = "native-restorative-v1";
    private const long ProbeLimitMilliseconds = 150_000;

    [Fact]
    public void Opt_in_coordinated_native_results_authenticate_the_complete_continued_ledger()
    {
        string? manifestPath = Environment.GetEnvironmentVariable(ResultsVariable);
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(manifestPath), "The actual native results manifest is required.");
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath!));
        JsonElement journey = Assert.Single(manifest.RootElement.GetProperty("journeys").EnumerateArray());
        Assert.Equal(Label, Text(journey, "label"));
        Assert.Equal(CoordinatedNativeJourney.FixtureName, Text(journey, "name"));
        string databasePath = Text(journey, "database");
        string fixturePath = Text(journey, "fixtureDirectory");
        Assert.True(Path.IsPathFullyQualified(databasePath));
        Assert.True(Path.IsPathFullyQualified(fixturePath));
        Assert.Equal(Path.Combine(fixturePath, "genesis.ofc"), Text(journey, "expectedGenesis"));

        // The runner pinned these inputs before and after both native processes.
        JsonElement pins = journey.GetProperty("fixtureSha256");
        foreach (string name in new[] { "before.sqlite3", "before.ofc", "metadata.json", "genesis.ofc" })
        {
            string path = Path.Combine(fixturePath, name);
            Assert.Equal(Text(pins, path), CoordinatedNativeJourney.Sha256(path));
        }
        Assert.Equal(Text(journey, "finalDatabaseSha256"), CoordinatedNativeJourney.Sha256(databasePath));

        using var active = JsonDocument.Parse(File.ReadAllBytes(Text(journey, "activeReport")));
        using var reopen = JsonDocument.Parse(File.ReadAllBytes(Text(journey, "reopenReport")));
        VerifyReport(active.RootElement, Mode);
        VerifyReport(reopen.RootElement, "reopen");
        Assert.Equal(Text(active.RootElement, "FinalHash"), Text(reopen.RootElement, "InitialHash"));
        Assert.Equal(Text(reopen.RootElement, "InitialHash"), Text(reopen.RootElement, "FinalHash"));

        CoordinatedNativeJourney.Authenticate(databasePath, fixturePath);
    }

    private static void VerifyReport(JsonElement report, string mode)
    {
        Assert.Equal(ReportKind, Text(report, "Kind"));
        Assert.Equal("passed", Text(report, "Status"));
        Assert.Equal(mode, Text(report, "Mode"));
        Assert.Equal(0, report.GetProperty("FocusRequests").GetInt32());
        Assert.True(report.GetProperty("IsLocallyPaused").GetBoolean());
        Assert.InRange(report.GetProperty("DurationMilliseconds").GetInt64(), 0, ProbeLimitMilliseconds - 1);
    }

    private static string Text(JsonElement value, string property) => CoordinatedNativeJourney.Text(value, property);
}
