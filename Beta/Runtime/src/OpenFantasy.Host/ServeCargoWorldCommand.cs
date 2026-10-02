using System.Security.Cryptography.X509Certificates;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Server;

namespace OpenFantasy.Host;

/// <summary>A dedicated composition of the same saved authority, player ports and protocol used by local play.</summary>
internal sealed class ServeCargoWorldCommand(TextWriter output, TextWriter errorOutput)
{
    internal async Task<int> RunAsync(string settingsPath, CancellationToken cancellationToken)
    {
        try
        {
            PlayerServerSettings settings = PlayerServerSettings.Read(settingsPath);
            string? password = settings.CertificatePasswordFile is null ? null : ReadPassword(settings.CertificatePasswordFile);
            // macOS imports PFX keys through a temporary keychain and rejects EphemeralKeySet.
            X509KeyStorageFlags keyStorage = OperatingSystem.IsMacOS()
                ? X509KeyStorageFlags.DefaultKeySet : X509KeyStorageFlags.EphemeralKeySet;
            using var certificate = X509CertificateLoader.LoadPkcs12FromFile(settings.CertificatePath, password,
                keyStorage);
            await using var host = await CargoPlayerHost.OpenExistingAsync(settings.Database, cancellationToken).ConfigureAwait(false);
            await using var server = await PlayerWebSocketServer.StartAsync(host.Admission,
                new(settings.ListenAddress, settings.Port, certificate, settings.PublicHost), cancellationToken).ConfigureAwait(false);
            if (OperatingSystem.IsWindows()) Directory.CreateDirectory(settings.InvitationDirectory);
            else Directory.CreateDirectory(settings.InvitationDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            foreach (var actor in host.Players)
            {
                var invitation = new PlayerConnectionInvitation(server.Endpoint, host.Admission.Compatibility, actor,
                    host.Issue(actor, settings.CredentialLifetime).Export());
                PlayerInvitationFile.Create(Path.Combine(settings.InvitationDirectory, actor + ".player.json"), invitation);
            }
            await output.WriteLineAsync("Listening: " + server.Endpoint).ConfigureAwait(false);
            await output.WriteLineAsync("Owner-protected player invitation files written.").ConfigureAwait(false);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            await server.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return 0; }
        catch (Exception error) when (error is not OutOfMemoryException and not StackOverflowException)
        {
            // Paths, certificate parser details, request text and credentials never enter public diagnostics.
            await errorOutput.WriteLineAsync("Player host stopped: " + error.GetType().Name).ConfigureAwait(false);
            return 1;
        }
    }

    private static string ReadPassword(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 4096) throw new InvalidDataException("Certificate password file is too long.");
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) throw new InvalidDataException("Certificate password file changed while being read.");
        return new System.Text.UTF8Encoding(false, true).GetString(bytes).TrimEnd('\r', '\n');
    }
}
