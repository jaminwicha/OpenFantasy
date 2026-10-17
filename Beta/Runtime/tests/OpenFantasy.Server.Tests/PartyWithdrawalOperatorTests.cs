using OpenFantasy.Core.Worlds;
using OpenFantasy.Host;
using OpenFantasy.Hosting;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests;

public sealed class PartyWithdrawalOperatorTests
{
    [Fact]
    public void Prepared_request_pins_the_source_and_publishes_only_a_separate_verified_copy()
    {
        using var files = new ServerFiles();
        string source = Path.Combine(files.DirectoryPath, "source.sqlite");
        string destination = Path.Combine(files.DirectoryPath, "continued.sqlite");
        string requestPath = Path.Combine(files.DirectoryPath, "request.bin");
        var genesis = AdventureWorldAdmission.CreateEmbedded("withdrawal-operator", ["Rowan", "Mira"]);
        using (var authority = SqliteWorldAuthority.CreateNew(source, genesis)) { }
        byte[] original = File.ReadAllBytes(source);
        using var report = new StringWriter();
        var application = new HostApplication(report);
        Assert.Equal(0, application.Run(["--prepare-party-withdrawal", source, requestPath]));
        Assert.Equal(original, File.ReadAllBytes(source));
        var request = LocalAuthorityFiles.ReadPartyWithdrawalTransition(requestPath);
        Assert.Equal(genesis.ContentHash, request.ExpectedHeadHash);
        byte[] capturedRequest = File.ReadAllBytes(requestPath);
        Assert.Equal(1, application.Run(["--prepare-party-withdrawal", source, requestPath]));
        Assert.Equal(capturedRequest, File.ReadAllBytes(requestPath));
        Assert.Equal(0, application.Run(["--enable-party-withdrawal", source, destination, requestPath]));
        Assert.Equal(original, File.ReadAllBytes(source));
        using (var upgraded = SqliteWorldAuthority.OpenExisting(destination, WorldSnapshotCodec.Encode(genesis)))
        {
            var state = upgraded.InspectForHost().State;
            Assert.Equal(41, state.ProfileVersion);
            Assert.Equal(1UL, state.Revision);
            Assert.Equal(genesis.Calendar, state.Calendar);
            Assert.Equal(genesis.Inventory.ContentHash, state.Inventory.ContentHash);
            Assert.Equal(genesis.ContentHash, upgraded.ReadRecordedStateForHost(0).State.ContentHash);
        }
        byte[] published = File.ReadAllBytes(destination);
        Assert.Equal(1, application.Run(["--enable-party-withdrawal", source, destination, requestPath]));
        Assert.Equal(published, File.ReadAllBytes(destination));
        Assert.Equal(1, application.Run(["--enable-party-withdrawal", source, source, requestPath]));
        Assert.Equal(original, File.ReadAllBytes(source));
        string secondRequest = Path.Combine(files.DirectoryPath, "already-migrated.bin");
        Assert.Equal(1, application.Run(["--prepare-party-withdrawal", destination, secondRequest]));
        Assert.False(File.Exists(secondRequest));
        Assert.Equal(published, File.ReadAllBytes(destination));
    }

    [Fact]
    public void A_changed_source_refuses_the_old_request_without_publishing_or_repinning_it()
    {
        using var files = new ServerFiles();
        string source = Path.Combine(files.DirectoryPath, "source.sqlite");
        string destination = Path.Combine(files.DirectoryPath, "continued.sqlite");
        string request = Path.Combine(files.DirectoryPath, "request.bin");
        var genesis = AdventureWorldAdmission.CreateEmbedded("withdrawal-stale-operator", ["Rowan"]);
        using (var authority = SqliteWorldAuthority.CreateNew(source, genesis)) { }
        using var output = new StringWriter();
        var app = new HostApplication(output);
        Assert.Equal(0, app.Run(["--prepare-party-withdrawal", source, request]));
        byte[] captured = File.ReadAllBytes(request);
        using (var authority = SqliteWorldAuthority.OpenExisting(source))
            authority.Commit(new(new(0x4f50455241544f52, 1), genesis.WorldId, genesis.BranchId, 0, 0,
                WorldBatchKind.AdvancingStep, [], [], genesis.ProfileVersion));
        byte[] changed = File.ReadAllBytes(source);
        Assert.Equal(1, app.Run(["--enable-party-withdrawal", source, destination, request]));
        Assert.False(File.Exists(destination));
        Assert.Equal(changed, File.ReadAllBytes(source));
        Assert.Equal(captured, File.ReadAllBytes(request));
        Assert.Contains("names another retained head", output.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1025)]
    public void Malformed_request_is_rejected_before_publication(int length)
    {
        using var files = new ServerFiles();
        string request = Path.Combine(files.DirectoryPath, "request.bin");
        string destination = Path.Combine(files.DirectoryPath, "continued.sqlite");
        File.WriteAllBytes(request, new byte[length]);
        using var output = new StringWriter();
        Assert.Equal(1, new HostApplication(output).Run(["--enable-party-withdrawal",
            Path.Combine(files.DirectoryPath, "absent.sqlite"), destination, request]));
        Assert.False(File.Exists(destination));
        Assert.Contains("format bounds", output.ToString());
    }
}
