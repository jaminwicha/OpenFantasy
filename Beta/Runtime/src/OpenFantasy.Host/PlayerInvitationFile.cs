using OpenFantasy.Hosting.Networking;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace OpenFantasy.Host;

/// <summary>Explicit local provisioning with create-only files; the caller distributes them over a trusted channel.</summary>
internal static class PlayerInvitationFile
{
    internal static void Create(string path, PlayerConnectionInvitation invitation)
    {
        byte[] bytes = PlayerInvitationCodec.Encode(invitation);
        using FileStream stream = OperatingSystem.IsWindows() ? CreateWindowsFile(path) : CreateUnixFile(path);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    [UnsupportedOSPlatform("windows")]
    private static FileStream CreateUnixFile(string path) => new(path, new FileStreamOptions
    {
        Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
        Options = FileOptions.WriteThrough, UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
    });

    [SupportedOSPlatform("windows")]
    private static FileStream CreateWindowsFile(string path)
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        SecurityIdentifier owner = identity.User ?? throw new UnauthorizedAccessException("No current user identity.");
        var security = new FileSecurity();
        security.SetOwner(owner);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl, AccessControlType.Allow));
        // Apply the protected DACL at creation, before any credential bytes exist on disk.
        FileStream stream = new FileInfo(path).Create(FileMode.CreateNew, FileSystemRights.FullControl,
            FileShare.None, 4096, FileOptions.WriteThrough, security);
        try
        {
            FileSecurity actual = stream.GetAccessControl();
            var rules = actual.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier));
            if (!actual.AreAccessRulesProtected || !owner.Equals(actual.GetOwner(typeof(SecurityIdentifier)))
                || rules.Count != 1 || rules[0] is not FileSystemAccessRule rule || rule.IsInherited
                || !owner.Equals(rule.IdentityReference) || rule.AccessControlType != AccessControlType.Allow
                || rule.FileSystemRights != FileSystemRights.FullControl)
                throw new UnauthorizedAccessException("Invitation file access is not private to its owner.");
            return stream;
        }
        catch { stream.Dispose(); throw; }
    }

    internal static PlayerConnectionInvitation Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is < 1 or > PlayerInvitationCodec.MaximumBytes) throw new InvalidDataException("Invalid invitation file length.");
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) throw new InvalidDataException("Invitation changed while being read.");
        return PlayerInvitationCodec.Decode(bytes);
    }
}
