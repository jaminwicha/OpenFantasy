using System.Text;
using OpenFantasy.Core.Content;
using OpenFantasy.Host;
using OpenFantasy.Hosting.Networking;

namespace OpenFantasy.Server.Tests;

public sealed class PlayerInvitationTests
{
    internal static PlayerConnectionInvitation Invitation() => new(new Uri("wss://localhost:9443/player"),
        new(new(1, 1), new(1, 2), 35, ContentHash.Compute("admission"u8), ContentHash.Compute("package"u8)),
        new(1, 3), Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());

    [Fact]
    public void Private_invitation_round_trips_copies_secret_and_omits_it_from_diagnostic_strings()
    {
        var original = Invitation();
        byte[] bytes = PlayerInvitationCodec.Encode(original);
        var decoded = PlayerInvitationCodec.Decode(bytes);
        Assert.Equal(original.Endpoint, decoded.Endpoint);
        Assert.Equal(original.Compatibility, decoded.Compatibility);
        Assert.Equal(original.ActorId, decoded.ActorId);
        Assert.Equal(original.ExportCredential(), decoded.ExportCredential());
        decoded.ExportCredential()[0] = 0;
        Assert.Equal(1, decoded.ExportCredential()[0]);
        Assert.DoesNotContain(Convert.ToHexString(original.ExportCredential()).ToLowerInvariant(), original.ToString());
        Assert.Equal(bytes, PlayerInvitationCodec.Encode(decoded));
    }

    [Theory]
    [InlineData("ws://localhost/player")]
    [InlineData("wss://secret@localhost/player")]
    [InlineData("wss://localhost/player?credential=secret")]
    [InlineData("wss://localhost/player#secret")]
    [InlineData("wss://localhost/elsewhere")]
    public void Invitation_refuses_non_tls_or_embedded_secret_locations(string uri)
    {
        var source = Invitation();
        Assert.Throws<ArgumentException>(() => new PlayerConnectionInvitation(new Uri(uri), source.Compatibility,
            source.ActorId, source.ExportCredential()));
    }

    [Fact]
    public void Private_file_is_bounded_create_only_and_owner_readable_on_unix()
    {
        using var files = new ServerFiles();
        string path = Path.Combine(files.DirectoryPath, "join.json");
        var invitation = Invitation();
        PlayerInvitationFile.Create(path, invitation);
        byte[] before = File.ReadAllBytes(path);
        Assert.Throws<IOException>(() => PlayerInvitationFile.Create(path, invitation));
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(invitation.Compatibility, PlayerInvitationFile.Read(path).Compatibility);
        if (!OperatingSystem.IsWindows()) Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        string oversized = Path.Combine(files.DirectoryPath, "oversized.json");
        File.WriteAllBytes(oversized, new byte[PlayerInvitationCodec.MaximumBytes + 1]);
        Assert.Throws<InvalidDataException>(() => PlayerInvitationFile.Read(oversized));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("version")]
    [InlineData("credential")]
    public void Invitation_rejects_ambiguous_or_incomplete_files(string mutation)
    {
        string json = Encoding.UTF8.GetString(PlayerInvitationCodec.Encode(Invitation()));
        json = mutation switch
        {
            "duplicate" => json.Replace("\"Version\":1,", "\"Version\":1,\"Version\":1,", StringComparison.Ordinal),
            "missing" => json.Replace("\"Version\":1,", "", StringComparison.Ordinal),
            "unknown" => json.Replace("\"Version\":1,", "\"Version\":1,\"Unknown\":1,", StringComparison.Ordinal),
            "version" => json.Replace("\"Version\":1,", "\"Version\":2,", StringComparison.Ordinal),
            _ => json.Replace("\"Credential\":\"", "\"Credential\":\"ff", StringComparison.Ordinal)
        };
        Assert.Throws<FormatException>(() => PlayerInvitationCodec.Decode(Encoding.UTF8.GetBytes(json)));
    }
}

internal sealed class ServerFiles : IDisposable
{
    internal string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "openfantasy-host-files-" + Guid.NewGuid().ToString("N"));
    internal ServerFiles() => Directory.CreateDirectory(DirectoryPath);
    public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);
}
