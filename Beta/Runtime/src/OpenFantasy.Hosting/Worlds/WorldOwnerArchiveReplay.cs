using OpenFantasy.Core.Construction.Ownership;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Verifies every historical owner payload and exact archive membership during complete replay.</summary>
internal sealed class WorldOwnerArchiveReplay
{
    private readonly WorldDatabase _database;
    private readonly HashSet<ContentHash> _verified = [];
    private readonly (long Count, long Bytes) _stored;
    private long _bytes;

    internal WorldOwnerArchiveReplay(WorldDatabase database)
    {
        _database = database;
        _stored = database.SeparatesOwners ? database.ReadOwnerUsage() : (0, 0);
        if (_stored.Count > WorldDatabase.MaximumOwnerArchiveCount || _stored.Bytes > WorldDatabase.MaximumOwnerArchiveBytes)
            throw new InvalidDataException("Typed owner archives exceed their explicit recovery envelope.");
    }

    internal void Observe(ConstructionOwnerDirectory? directory)
    {
        if (directory is null)
        {
            if (_database.SeparatesOwners) throw new InvalidDataException("An owner directory is missing from typed replay.");
            return;
        }
        if (!_database.SeparatesOwners) throw new InvalidDataException("Typed owner images belong to another storage interpretation.");
        foreach (var owner in directory.Owners)
        {
            if (!_verified.Add(owner.Hash)) continue;
            _database.VerifyOwner(owner);
            _bytes = checked(_bytes + owner.ByteLength);
            if (_verified.Count > WorldDatabase.MaximumOwnerArchiveCount || _bytes > WorldDatabase.MaximumOwnerArchiveBytes)
                throw new InvalidDataException("Typed owner replay exceeds its bounded archive budget.");
        }
    }

    internal void Complete()
    {
        if (_verified.Count != _stored.Count || _bytes != _stored.Bytes)
            throw new InvalidDataException("Typed owner archives do not equal the complete retained original history.");
    }
}
