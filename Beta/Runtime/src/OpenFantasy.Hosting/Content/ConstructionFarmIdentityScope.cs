using System.Buffers.Binary;
using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>New construction roles and independent reproduction of retained admission identities.</summary>
internal sealed class ConstructionFarmIdentityScope(WorldSnapshot world)
{
    private readonly HashSet<EntityId> _newIdentities = [];

    internal EntityId Definition(string key) => Register(DefinitionId("construction-farm", key));
    internal EntityId Instance(string role) => Register(InstanceId(world, "construction-farm", role));

    internal static EntityId DefinitionId(string profile, string key) => EntityId.DeriveGenerated(
        "openfantasy.authored-content"u8, profile + ".definitions.v1", key, "definition", 0);

    internal static EntityId InstanceId(WorldSnapshot state, string profile, string role) => EntityId.DeriveGenerated(
        EncodeScope(profile + ".instance-scope", state.WorldId, state.BranchId), profile + ".instances.v1", role, "instance", 0);

    private EntityId Register(EntityId identity)
    {
        if (world.IdentityIndex.Contains(identity) || !_newIdentities.Add(identity))
        {
            throw new InvalidDataException("A construction identity collides with retained content or another new role.");
        }
        return identity;
    }

    private static byte[] EncodeScope(string domainName, EntityId worldId, EntityId branchId)
    {
        byte[] domain = Encoding.UTF8.GetBytes(domainName);
        byte[] frame = new byte[56 + domain.Length];
        "OFC\0"u8.CopyTo(frame);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(8), 3);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(12), (uint)domain.Length);
        domain.CopyTo(frame, 16);
        int offset = 16 + domain.Length;
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(offset), 16);
        worldId.ToByteArray().CopyTo(frame, offset + 4);
        offset += 20;
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(offset), 16);
        branchId.ToByteArray().CopyTo(frame, offset + 4);
        return frame;
    }
}
