using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenFantasy.Host;

/// <summary>Local operator configuration. Paths resolve beside this file; no setting grants an actor new control.</summary>
internal sealed record PlayerServerSettings(string Database, IPAddress ListenAddress, int Port, string PublicHost,
    string CertificatePath, string? CertificatePasswordFile, string InvitationDirectory, TimeSpan CredentialLifetime)
{
    internal const int MaximumBytes = 16 * 1024;

    internal static PlayerServerSettings Read(string path)
    {
        string full = Path.GetFullPath(path);
        using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is < 1 or > MaximumBytes) throw new InvalidDataException("Invalid server settings length.");
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) throw new InvalidDataException("Server settings changed while being read.");
        using JsonDocument json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 4 });
        string[] fields = json.RootElement.EnumerateObject().Select(property => property.Name).ToArray();
        if (fields.Distinct(StringComparer.Ordinal).Count() != fields.Length
            || new[] { "Database", "ListenAddress", "Port", "PublicHost", "CertificatePath", "InvitationDirectory" }
                .Any(required => !fields.Contains(required, StringComparer.Ordinal)))
            throw new InvalidDataException("Server settings require unique, complete explicit fields.");
        var data = JsonSerializer.Deserialize<SettingsData>(bytes, new JsonSerializerOptions
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 4
        }) ?? throw new InvalidDataException("Missing server settings.");
        if (!IPAddress.TryParse(data.ListenAddress, out IPAddress? address) || data.Port is < 0 or > 65535
            || string.IsNullOrWhiteSpace(data.PublicHost) || data.CredentialLifetimeSeconds is < 30 or > 86400)
            throw new InvalidDataException("Invalid listener or credential lifetime settings.");
        string directory = Path.GetDirectoryName(full)!;
        string Resolve(string value) => !string.IsNullOrWhiteSpace(value) ? Path.GetFullPath(value, directory)
            : throw new InvalidDataException("A configured local path is required.");
        return new(Resolve(data.Database), address, data.Port, data.PublicHost, Resolve(data.CertificatePath),
            data.CertificatePasswordFile is null ? null : Resolve(data.CertificatePasswordFile),
            Resolve(data.InvitationDirectory), TimeSpan.FromSeconds(data.CredentialLifetimeSeconds));
    }

    private sealed record SettingsData(string Database, string ListenAddress, int Port, string PublicHost,
        string CertificatePath, string? CertificatePasswordFile, string InvitationDirectory, int CredentialLifetimeSeconds = 600);
}
