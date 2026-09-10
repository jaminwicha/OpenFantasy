using System.Buffers.Binary;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Stable content identities and world-scoped instance roles, checked against every current world role.</summary>
internal sealed class QuestFarmIdentityScope(WorldSnapshot world)
{
    private readonly HashSet<EntityId> _newIdentities = [];
    private readonly byte[] _instanceSeed = EncodeScope(world.WorldId, world.BranchId);

    internal EntityId Definition(string key) => Register(EntityId.DeriveGenerated("openfantasy.authored-content"u8,
        "quest-farm.definitions.v1", key, "definition", 0));

    internal EntityId Instance(string role) => Register(EntityId.DeriveGenerated(_instanceSeed,
        "quest-farm.instances.v1", role, "instance", 0));

    private EntityId Register(EntityId identity)
    {
        if (world.IdentityIndex.Contains(identity) || !_newIdentities.Add(identity))
        {
            throw new InvalidDataException("A quest admission identity collides with existing world content or another new role.");
        }
        return identity;
    }

    private static byte[] EncodeScope(EntityId worldId, EntityId branchId)
    {
        ReadOnlySpan<byte> domain = "quest-farm.instance-scope"u8;
        // Exact three-field OFC v1 frame, matching the established Hosting seed framing.
        // Core's general encoder remains internal to its assembly.
        byte[] frame = new byte[56 + domain.Length];
        "OFC\0"u8.CopyTo(frame);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(8), 3);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(12), (uint)domain.Length);
        domain.CopyTo(frame.AsSpan(16));
        int worldOffset = 16 + domain.Length;
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(worldOffset), 16);
        worldId.ToByteArray().CopyTo(frame, worldOffset + 4);
        int branchOffset = worldOffset + 20;
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(branchOffset), 16);
        branchId.ToByteArray().CopyTo(frame, branchOffset + 4);
        return frame;
    }
}
