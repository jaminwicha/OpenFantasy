namespace OpenFantasy.Hosting.Tests.Content;

internal sealed class FarmContentFixture : IDisposable
{
    internal FarmContentFixture()
    {
        Directory.CreateDirectory(ContentRoot);
        string source = InstalledContentRoot();
        CopyDirectory(Path.Combine(source, "native-farm"), Path.Combine(ContentRoot, "native-farm"));
        CopyDirectory(Path.Combine(source, "legacy", "aldermere-first-loop"),
            Path.Combine(ContentRoot, "legacy", "aldermere-first-loop"));
        Directory.CreateDirectory(Path.Combine(ContentRoot, "packages"));
        File.Copy(Path.Combine(source, "packages", "aldermere-first-loop.manifest.json"),
            Path.Combine(ContentRoot, "packages", "aldermere-first-loop.manifest.json"));
    }

    internal string ContentRoot { get; } = Path.Combine(Path.GetTempPath(), "openfantasy-farm-content-" + Guid.NewGuid().ToString("N"));
    internal string PathFor(string relative) => Path.Combine(ContentRoot, relative.Replace('/', Path.DirectorySeparatorChar));

    internal static string InstalledContentRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "content");
            if (File.Exists(Path.Combine(candidate, "native-farm", "admission-v1.json")))
            {
                return candidate;
            }
        }
        throw new DirectoryNotFoundException("The farm content tests require the checked-out runtime content directory.");
    }

    public void Dispose() => Directory.Delete(ContentRoot, recursive: true);

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }
        foreach (string directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }
}
