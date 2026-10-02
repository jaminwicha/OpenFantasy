using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using OpenFantasy.Host;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests;

public sealed class DedicatedHostTests
{
    [Fact]
    public async Task Dedicated_command_opens_production_world_provisions_actual_package_and_shuts_down_without_advancement()
    {
        using var files = new ServerFiles();
        var genesis = CargoOutcomeWorldAdmission.CreateEmbedded(CargoOutcomeWorldAdmission.SeedText, "Hosted Player");
        string database = Path.Combine(files.DirectoryPath, "world.sqlite3");
        using (var authority = SqliteWorldAuthority.CreateNew(database, genesis)) { }
        using var certificate = ServerWorld.CreateCertificate();
        const string testPassword = "ephemeral-test-key";
        File.WriteAllBytes(Path.Combine(files.DirectoryPath, "server.pfx"), certificate.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx, testPassword));
        File.WriteAllText(Path.Combine(files.DirectoryPath, "password.txt"), testPassword + "\n");
        string settings = Path.Combine(files.DirectoryPath, "settings.json");
        File.WriteAllText(settings, JsonSerializer.Serialize(new
        {
            Database = "world.sqlite3", ListenAddress = "127.0.0.1", Port = 0, PublicHost = "localhost",
            CertificatePath = "server.pfx", CertificatePasswordFile = "password.txt",
            InvitationDirectory = "invitations", CredentialLifetimeSeconds = 60
        }));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ServerWorld.Cancellation);
        using var output = new ReadyOutput();
        using var errors = new StringWriter();
        Task<int> running = new ServeCargoWorldCommand(output, errors).RunAsync(settings, stop.Token);
        try
        {
            Task startup = await Task.WhenAny(output.Ready.Task, running).WaitAsync(TimeSpan.FromSeconds(10), ServerWorld.Cancellation);
            Assert.True(startup == output.Ready.Task, "Dedicated startup ended before readiness: " + errors);
            string endpoint = await output.Ready.Task.WaitAsync(TimeSpan.FromSeconds(10), ServerWorld.Cancellation);
            string invitationPath = Assert.Single(Directory.GetFiles(Path.Combine(files.DirectoryPath, "invitations")));
            var invitation = PlayerInvitationFile.Read(invitationPath);
            Assert.Equal(new Uri(endpoint), invitation.Endpoint);
            Assert.Equal(PlayerWorldCompatibility.ResolvePackages(FarmPackageCatalog.LoadEmbedded()), invitation.Compatibility.PackagesVersion);
            Assert.NotEqual(PlayerWorldCompatibility.ResolvePackages([]), invitation.Compatibility.PackagesVersion);
            Assert.Equal(genesis.WorldId, invitation.Compatibility.WorldId);
            using var socket = new ClientWebSocket();
            socket.Options.AddSubProtocol(PlayerWebSocketServer.Subprotocol);
            socket.Options.RemoteCertificateValidationCallback = (_, cert, _, policy) => cert is not null
                && (policy & ~System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors) == 0
                && cert.GetCertHashString(System.Security.Cryptography.HashAlgorithmName.SHA256)
                    == certificate.GetCertHashString(System.Security.Cryptography.HashAlgorithmName.SHA256);
            await socket.ConnectAsync(invitation.Endpoint, ServerWorld.Cancellation);
            await using var transport = new WebSocketPlayerTransport(socket);
            await ServerWorld.Send(transport, PlayerHandshakeCodec.Hello(1, invitation.Compatibility, invitation.ExportCredential()));
            Assert.Equal(PlayerMessageKind.Welcome, (await ServerWorld.Receive(transport)).Kind);
            await ServerWorld.Send(transport, new(PlayerMessageKind.Ping, 2, []));
            Assert.Equal(PlayerMessageKind.Pong, (await ServerWorld.Receive(transport)).Kind);
            Assert.DoesNotContain(Convert.ToHexString(invitation.ExportCredential()).ToLowerInvariant(), output.Text);
        }
        finally
        {
            stop.Cancel();
            await running.WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
        }
        Assert.Equal(0, await running.WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation));
        Assert.Empty(errors.ToString());
        using (var reopened = SqliteWorldAuthority.OpenExisting(database))
            Assert.Equal(genesis.ContentHash, reopened.ReadStateForHost().ContentHash);
        // Explicit existing invitation files are never silently replaced on another host start.
        byte[] retained = File.ReadAllBytes(Assert.Single(Directory.GetFiles(Path.Combine(files.DirectoryPath, "invitations"))));
        Assert.Equal(1, await new ServeCargoWorldCommand(new StringWriter(), new StringWriter()).RunAsync(settings, ServerWorld.Cancellation)
            .WaitAsync(TimeSpan.FromSeconds(10), ServerWorld.Cancellation));
        Assert.Equal(retained, File.ReadAllBytes(Assert.Single(Directory.GetFiles(Path.Combine(files.DirectoryPath, "invitations")))));
    }

    [Fact]
    public async Task Missing_or_incompatible_world_is_never_created_or_reinterpreted()
    {
        using var files = new ServerFiles();
        string path = Path.Combine(files.DirectoryPath, "missing.sqlite3");
        await Assert.ThrowsAsync<FileNotFoundException>(() => CargoPlayerHost.OpenExistingAsync(path, ServerWorld.Cancellation));
        Assert.False(File.Exists(path));
        var old = FarmWorldAdmission.CreateEmbedded("older", "Player");
        using (var authority = SqliteWorldAuthority.CreateNew(path, old)) { }
        await Assert.ThrowsAsync<InvalidDataException>(() => CargoPlayerHost.OpenExistingAsync(path, ServerWorld.Cancellation));
        using var reopened = SqliteWorldAuthority.OpenExisting(path);
        Assert.Equal(old.ContentHash, reopened.ReadStateForHost().ContentHash);
    }

    [Fact]
    public void Settings_resolve_local_paths_and_reject_ambiguous_or_unknown_fields()
    {
        using var files = new ServerFiles();
        string path = Path.Combine(files.DirectoryPath, "host.json");
        const string json = "{\"Database\":\"save.sqlite3\",\"ListenAddress\":\"127.0.0.1\",\"Port\":0,\"PublicHost\":\"localhost\",\"CertificatePath\":\"tls.pfx\",\"InvitationDirectory\":\"private\"}";
        File.WriteAllText(path, json);
        Assert.Equal(Path.Combine(files.DirectoryPath, "save.sqlite3"), PlayerServerSettings.Read(path).Database);
        File.WriteAllText(path, json.Replace("\"Port\":0,", "\"Port\":0,\"Port\":1,", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => PlayerServerSettings.Read(path));
        File.WriteAllText(path, json.Replace("\"Port\":0,", "\"Port\":0,\"Password\":\"not-a-setting\",", StringComparison.Ordinal));
        Assert.Throws<JsonException>(() => PlayerServerSettings.Read(path));
    }

    private sealed class ReadyOutput : TextWriter
    {
        private readonly StringBuilder _text = new();
        public override Encoding Encoding => Encoding.UTF8;
        internal TaskCompletionSource<string> Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal string Text { get { lock (_text) { return _text.ToString(); } } }
        public override Task WriteLineAsync(string? value)
        {
            lock (_text) { _text.AppendLine(value); }
            if (value?.StartsWith("Listening: ", StringComparison.Ordinal) == true) Ready.TrySetResult(value[11..]);
            return Task.CompletedTask;
        }
    }
}
