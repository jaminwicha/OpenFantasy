namespace OpenFantasy.Core.Content;

/// <summary>A dependency pinned to its complete manifest version, not a mutable release label.</summary>
public sealed record PackageDependency
{
    public PackageId Id { get; }
    public ContentHash Version { get; }

    public PackageDependency(PackageId id, ContentHash version)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(version);
        Id = id;
        Version = version;
    }
}
