using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>New profile additions use distinct role domains and reject aliases with the admitted base world.</summary>
internal sealed class GatheringFarmIdentityScope
{
    private readonly byte[] _seed;
    private readonly WorldIdentityIndex _existing;
    private readonly Dictionary<EntityId, string> _roles = [];

    internal GatheringFarmIdentityScope(GatheringFarmInput input, WorldSnapshot basis)
    {
        _seed = Encoding.UTF8.GetBytes(input.BaseSeedText);
        _existing = basis.IdentityIndex;
    }

    internal EntityId Definition(string key) => Register(EntityId.DeriveGenerated(
        "openfantasy.authored-content"u8, "adventure-farm.definitions.v1", key, "definition", 0), "definition:" + key);

    internal EntityId Instance(string role) => Register(EntityId.DeriveGenerated(
        _seed, "adventure-farm.instances.v1", role, "instance", 0), "instance:" + role);

    private EntityId Register(EntityId identity, string role)
    {
        if (_existing.Contains(identity)
            || (_roles.TryGetValue(identity, out string? other) && other != role))
        {
            throw new InvalidDataException("A gathering profile identity collides with another admitted role.");
        }
        _roles[identity] = role;
        return identity;
    }
}
