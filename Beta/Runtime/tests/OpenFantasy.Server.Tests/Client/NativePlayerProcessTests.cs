using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Host;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests;

public sealed class NativePlayerProcessTests
{
    public static bool Configured => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENFANTASY_GODOT"));

    [Theory(SkipUnless = nameof(Configured), Skip = "Requires the explicit qualified Godot executable and native evidence directory.")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Godot_player_walks_and_talks_over_actual_tls_and_host_history_matches_its_receipts(bool rendered)
    {
        string output = Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_PLAYER_EVIDENCE")
            ?? throw new InvalidOperationException("The native proof requires a retained evidence directory.");
        output = Path.Combine(output, rendered ? "rendered" : "headless");
        Directory.CreateDirectory(output);
        await using var world = await RemoteSessionWorld.Open();
        string invitation = Path.Combine(world.Files.DirectoryPath, "player.invitation.json");
        PlayerInvitationFile.Create(invitation, world.Invitation);
        string certificate = Path.Combine(world.Files.DirectoryPath, "public.cer");
        File.WriteAllBytes(certificate, world.Certificate.Export(X509ContentType.Cert));
        string report = Path.Combine(output, "native-player.json");
        string client = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../client"));
        if (rendered) client = BackgroundProject(client, world.Files.DirectoryPath);
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("OPENFANTASY_GODOT")!)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = client
        };
        start.Environment["DOTNET_ROOT"] = Path.GetDirectoryName(Environment.GetEnvironmentVariable("OPENFANTASY_DOTNET"))!;
        if (!rendered) start.ArgumentList.Add("--headless");
        foreach (string argument in new[] { "--path", client, "res://scenes/NativePlayer.tscn", "--",
            rendered ? "--player-focus-probe" : "--player-probe", invitation, certificate, report }) start.ArgumentList.Add(argument);
        if (!rendered) start.ArgumentList.Add("--background-render");
        using Process process = Process.Start(start)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(ServerWorld.Cancellation);
        Task<string> stderr = process.StandardError.ReadToEndAsync(ServerWorld.Cancellation);
        try
        {
            await process.WaitForExitAsync(ServerWorld.Cancellation).WaitAsync(TimeSpan.FromSeconds(75), ServerWorld.Cancellation);
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(ServerWorld.Cancellation); }
            File.WriteAllText(Path.Combine(output, "godot.stdout.log"), await stdout);
            File.WriteAllText(Path.Combine(output, "godot.stderr.log"), await stderr);
        }
        Assert.True(File.Exists(report), "Native report missing: " + await stderr);
        using JsonDocument evidence = JsonDocument.Parse(File.ReadAllText(report));
        var root = evidence.RootElement;
        Assert.True(root.GetProperty("Status").GetString() == "passed", root.GetRawText());
        Assert.Equal(0, process.ExitCode);
        Assert.DoesNotContain("ERROR:", await stderr, StringComparison.Ordinal);
        if (rendered)
        {
            Assert.True(root.GetProperty("InitialFocusGuard").GetBoolean());
            Assert.False(root.GetProperty("ObservedNativeFocus").GetBoolean());
            Assert.True(File.Exists(Path.ChangeExtension(report, ".png")));
        }
        Assert.False(world.Host.Session.Completion.IsCompleted);
        await world.Server.DisposeAsync();
        await world.Host.DisposeAsync();
        using var authority = SqliteWorldAuthority.OpenExisting(world.Database);
        var control = world.Genesis.Inventory.Controls.Single(value => value.ActorId == world.Invitation.ActorId);
        var delivery = new TrustedCommandDelivery(control.PrincipalId, control.SessionId);
        Assert.Equal(world.Genesis.WorldId.ToString(), root.GetProperty("World").GetString());
        Assert.Equal(world.Invitation.ActorId.ToString(), root.GetProperty("Actor").GetString());
        var actions = root.GetProperty("Submissions").EnumerateArray().ToArray();
        Assert.Equal(2, actions.Length);
        foreach (var action in actions)
        {
            var command = WorldActionCodec.Decode(Convert.FromHexString(action.GetProperty("Command").GetString()!));
            var receipt = authority.FindReceipt(command, delivery);
            Assert.NotNull(receipt);
            Assert.Equal(Convert.FromHexString(action.GetProperty("Receipt").GetString()!), WorldReceiptCodecV1.Encode(receipt));
        }
        var state = authority.ReadStateForHost();
        var feet = state.Space.GetActor(world.Invitation.ActorId).Feet;
        Assert.Equal(new[] { feet.X, feet.Y, feet.Z }, root.GetProperty("FinalFeet").EnumerateArray().Select(value => value.GetInt64()));
        Assert.NotEqual(world.Genesis.Space.GetActor(world.Invitation.ActorId).Feet, feet);
        Assert.Empty(state.Dialogues);
        Assert.True(authority.ReadRecordedStateForHost(state.Revision).State.ContentHash == state.ContentHash);
    }

    private static string BackgroundProject(string source, string temporary)
    {
        string target = Path.Combine(temporary, "background-client");
        Directory.CreateDirectory(target);
        foreach (string entry in Directory.EnumerateFileSystemEntries(source))
        {
            string name = Path.GetFileName(entry);
            if (name == "project.godot") continue;
            if (Directory.Exists(entry)) Directory.CreateSymbolicLink(Path.Combine(target, name), entry);
            else File.CreateSymbolicLink(Path.Combine(target, name), entry);
        }
        string config = File.ReadAllText(Path.Combine(source, "project.godot"));
        Assert.DoesNotContain("window/size/no_focus", config, StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(target, "project.godot"),
            config.Replace("[display]", "[display]\nwindow/size/no_focus=true", StringComparison.Ordinal));
        return target;
    }
}
