using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Stored closing bytes and the successor commit that publishes them atomically.</summary>
internal sealed record StoredWorldMaterialCareArchive(WorldMaterialCareArchiveReference Reference,
    ulong PublicationSequence, byte[] State);
