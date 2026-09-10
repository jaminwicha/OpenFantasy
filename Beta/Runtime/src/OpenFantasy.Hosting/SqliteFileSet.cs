namespace OpenFantasy.Hosting;

/// <summary>Admission for a fresh SQLite filename, including files SQLite may recover on first opening.</summary>
internal static class SqliteFileSet
{
    internal static void RequireUnused(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        foreach (string suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            if (Exists(path + suffix))
            {
                throw new IOException("A new world requires an unused database filename and sidecar paths.");
            }
        }
    }

    private static bool Exists(string path)
    {
        try
        {
            _ = File.GetAttributes(path);
            return true;
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }
}
