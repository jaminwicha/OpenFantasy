using System.Security.AccessControl;
using System.Security.Principal;
using OpenFantasy.Host;

namespace OpenFantasy.Server.Tests;

public sealed class WindowsInvitationTests
{
    public static bool IsWindows => OperatingSystem.IsWindows();

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows DACL inheritance requires a Windows filesystem.")]
    public void Invitation_created_under_readable_parent_has_only_protected_owner_access()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        using var files = new ServerFiles();
        var directory = new DirectoryInfo(files.DirectoryPath);
        DirectorySecurity permissions = directory.GetAccessControl();
        var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        permissions.AddAccessRule(new FileSystemAccessRule(users, FileSystemRights.ReadAndExecute,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        directory.SetAccessControl(permissions);
        string inherited = Path.Combine(files.DirectoryPath, "ordinary.txt");
        File.WriteAllText(inherited, "public fixture");
        bool inheritedReaders = false;
        foreach (FileSystemAccessRule rule in new FileInfo(inherited).GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier)))
            inheritedReaders |= rule.IsInherited && users.Equals(rule.IdentityReference);
        Assert.True(inheritedReaders);

        string path = Path.Combine(files.DirectoryPath, "join.json");
        PlayerInvitationFile.Create(path, PlayerInvitationTests.Invitation());
        FileSecurity actual = new FileInfo(path).GetAccessControl();
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        Assert.True(actual.AreAccessRulesProtected);
        Assert.Equal(identity.User, actual.GetOwner(typeof(SecurityIdentifier)));
        FileSystemAccessRule only = Assert.Single(actual.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>());
        Assert.False(only.IsInherited);
        Assert.Equal(identity.User, only.IdentityReference);
        Assert.Equal(AccessControlType.Allow, only.AccessControlType);
        Assert.Equal(FileSystemRights.FullControl, only.FileSystemRights);
        Assert.Equal(PlayerInvitationTests.Invitation().ExportCredential(), PlayerInvitationFile.Read(path).ExportCredential());
    }
}
