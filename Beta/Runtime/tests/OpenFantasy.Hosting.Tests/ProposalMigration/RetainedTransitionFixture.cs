using System.Security.Cryptography;
using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.ProposalMigration;

/// <summary>Every retained-transition class shares one real upgrade and runs serially.</summary>
[CollectionDefinition(Name)]
public sealed class RetainedTransitionCollection : ICollectionFixture<RetainedTransitionFixture>
{
    public const string Name = "Retained proposal transition";
}

/// <summary>
/// One disposable copy of the retained lived profile-27 save and one real explicit transition of it.
/// Complete replays of this 1,579-commit world are costly, so tests share the published destination
/// and mutate only their own copies. A missing retained input fails; it is never skipped or replaced.
/// </summary>
public sealed class RetainedTransitionFixture : IDisposable
{
    internal const string SourceSha256 = "814f0afc093031d1c9308d3e1c89683b2afbdda0076b2c636b084d2e4fb23773";
    internal const string SourceHeadHash = "bf989710d34c27cc1a9a3d78ea5475214a5423e44fa68d81c823a3c5135fcee0";
    internal const ulong SourceRevision = 1579;
    internal const ulong SourceSteps = 808;
    internal const ulong SourceOrdinaryUses = 520;
    private readonly string _directory;

    public RetainedTransitionFixture()
    {
        OriginalSource = LocateSource();
        Assert.Equal(SourceSha256, Sha256(OriginalSource));
        _directory = Path.Combine(Path.GetTempPath(), "openfantasy-proposal-transition-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        SourcePath = Path.Combine(_directory, "source.sqlite3");
        UpgradedPath = Path.Combine(_directory, "upgraded.sqlite3");
        File.Copy(OriginalSource, SourcePath);
        // Ordinary opening recovers the lived head and never transitions it.
        using (SqliteWorldAuthority source = SqliteWorldAuthority.OpenExisting(SourcePath))
        {
            SourceHead = source.ReadStateForHost();
            SourceUsage = source.InspectForHost().Journal;
        }
        using (WorldDatabase database = WorldDatabase.Open(SourcePath, create: false))
        {
            GenesisBytes = database.ReadGenesis();
        }
        Request = WorldProposalTransition.ForHead(SourceHead, Cause("transition"));
        using SqliteWorldAuthority upgraded = SqliteWorldAuthority.UpgradeCoordinatedToProposals(SourcePath, UpgradedPath, Request);
        UpgradedHead = upgraded.ReadStateForHost();
    }

    internal string OriginalSource { get; }
    internal string SourcePath { get; }
    internal string UpgradedPath { get; }
    internal WorldSnapshot SourceHead { get; }
    internal WorldJournalUsage SourceUsage { get; }
    internal byte[] GenesisBytes { get; }
    internal WorldProposalTransition Request { get; }
    internal WorldSnapshot UpgradedHead { get; }

    internal static EntityId Cause(string label) =>
        EntityId.FromBytes(SHA256.HashData(Encoding.UTF8.GetBytes("openfantasy.test.proposal-transition." + label)).AsSpan(0, 16));

    /// <summary>A private copy of the published destination, including a non-empty WAL if one exists.</summary>
    internal StorageFiles CopyUpgraded() => Copy(UpgradedPath);

    internal StorageFiles CopySource() => Copy(SourcePath);

    internal static string Sha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static StorageFiles Copy(string path)
    {
        var files = new StorageFiles();
        File.Copy(path, files.Path);
        if (File.Exists(path + "-wal") && new FileInfo(path + "-wal").Length != 0)
        {
            File.Copy(path + "-wal", files.Path + "-wal");
        }
        return files;
    }

    private static string LocateSource()
    {
        string? configured = Environment.GetEnvironmentVariable("OPENFANTASY_RETAINED_PROPOSAL_SOURCE");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "OPENFANTASY_RETAINED_PROPOSAL_SOURCE must name the isolated retained source; this required case is not skipped.");
        }
        string path = Directory.Exists(configured) ? Path.Combine(configured, "final.sqlite3") : configured;
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The retained lived source is missing; this required case is not skipped.", path);
        }
        return path;
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
