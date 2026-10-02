using System.Diagnostics;
using System.Text.Json;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests;

public sealed class DedicatedHostProcessTests
{
    public static bool IsPosix => !OperatingSystem.IsWindows();

    [Fact(SkipUnless = nameof(IsPosix), Skip = "SIGTERM is a POSIX process contract.")]
    public async Task Actual_host_process_handles_sigterm_and_releases_its_saved_world()
    {
        using var files = new ServerFiles();
        string database = Path.Combine(files.DirectoryPath, "world.sqlite3");
        var genesis = CargoOutcomeWorldAdmission.CreateEmbedded(CargoOutcomeWorldAdmission.SeedText, "Process Player");
        using (var authority = SqliteWorldAuthority.CreateNew(database, genesis)) { }
        using var certificate = ServerWorld.CreateCertificate();
        File.WriteAllBytes(Path.Combine(files.DirectoryPath, "server.pfx"), certificate.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx));
        string settings = Path.Combine(files.DirectoryPath, "settings.json");
        File.WriteAllText(settings, JsonSerializer.Serialize(new
        {
            Database = "world.sqlite3", ListenAddress = "127.0.0.1", Port = 0, PublicHost = "localhost",
            CertificatePath = "server.pfx", InvitationDirectory = "invitations"
        }));
        string executable = Environment.GetEnvironmentVariable("OPENFANTASY_DOTNET")
            ?? throw new InvalidOperationException("The process proof requires its explicit qualified SDK executable.");
        string assembly = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll"));
        Assert.True(File.Exists(assembly));
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(assembly);
        start.ArgumentList.Add("--serve-cargo-world");
        start.ArgumentList.Add(settings);
        using Process process = Process.Start(start)!;
        Task<string> errors = process.StandardError.ReadToEndAsync(ServerWorld.Cancellation);
        try
        {
            string? ready = await process.StandardOutput.ReadLineAsync(ServerWorld.Cancellation).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(15), ServerWorld.Cancellation);
            if (ready is null)
                Assert.Fail("Dedicated process ended before readiness: " + await errors.WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation));
            Assert.StartsWith("Listening: wss://localhost:", ready);
            Assert.Single(Directory.GetFiles(Path.Combine(files.DirectoryPath, "invitations")));
            var terminate = new ProcessStartInfo("/bin/kill") { UseShellExecute = false };
            terminate.ArgumentList.Add("-TERM");
            terminate.ArgumentList.Add(process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            using Process signal = Process.Start(terminate)!;
            await signal.WaitForExitAsync(ServerWorld.Cancellation);
            Assert.Equal(0, signal.ExitCode);
            await process.WaitForExitAsync(ServerWorld.Cancellation).WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
            string diagnostic = await errors;
            Assert.True(process.ExitCode == 0, "Dedicated shutdown failed: " + diagnostic);
            Assert.Empty(diagnostic);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(ServerWorld.Cancellation);
            }
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(database);
        Assert.Equal(genesis.ContentHash, reopened.ReadStateForHost().ContentHash);
    }
}
