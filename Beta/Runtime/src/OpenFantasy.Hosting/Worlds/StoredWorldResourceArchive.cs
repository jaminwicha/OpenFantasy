using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Stored closing bytes and the successor commit that publishes them atomically.</summary>
internal sealed record StoredWorldResourceArchive(WorldResourceArchiveReference Reference,
    ulong PublicationSequence, byte[] State);
