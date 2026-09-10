using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Deterministic bounded home amble; accepted samples use the same world movement rules as players.</summary>
public sealed class LocalNpcController
{
    public const string PolicyVersion = "local.home-amble.v1";
    private static readonly byte[] Domain = Encoding.ASCII.GetBytes(PolicyVersion);
    public IReadOnlyList<WorldMovementInput> SelectMovement(WorldSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        List<WorldMovementInput> inputs = [];
        if (state.HasReadingHold)
        {
            return inputs.AsReadOnly();
        }
        foreach (WorldResident resident in state.Residents)
        {
            if (resident.ControllerKind != ResidentControllerKind.Host || resident.WanderRadiusCells == 0)
            {
                continue;
            }
            // Sixteen possible movement samples, then forty-eight idle steps. No wall-clock seed.
            if (state.AdvancingSteps % 64 >= 16)
            {
                continue;
            }
            int direction = Direction(state.WorldId, resident.ActorId, state.AdvancingSteps / 64);
            long x = direction == 0 ? 32 : direction == 1 ? -32 : 0;
            long y = direction == 2 ? 32 : direction == 3 ? -32 : 0;
            ActorPresence actor = state.Space.GetActor(resident.ActorId);
            Int128 nextX = (Int128)actor.Feet.X + x;
            Int128 nextY = (Int128)actor.Feet.Y + y;
            Int128 radius = resident.WanderRadiusCells * SubunitPosition.SubunitsPerCell;
            Int128 fromHomeX = nextX - resident.HomeFeet.X;
            Int128 fromHomeY = nextY - resident.HomeFeet.Y;
            if (Int128.Abs(fromHomeX) > radius || Int128.Abs(fromHomeY) > radius
                || fromHomeX * fromHomeX + fromHomeY * fromHomeY > radius * radius)
            {
                continue;
            }
            ActorControlBinding binding = state.Inventory.Controls.Single(value => value.ActorId == resident.ActorId);
            inputs.Add(new WorldMovementInput(new TrustedCommandDelivery(binding.PrincipalId, binding.SessionId),
                resident.ActorId, x, y));
        }
        return inputs.AsReadOnly();
    }

    private static int Direction(EntityId worldId, EntityId actorId, ulong cycle)
    {
        Span<byte> input = stackalloc byte[Domain.Length + 40];
        Domain.CopyTo(input);
        worldId.WriteBytes(input[Domain.Length..]);
        actorId.WriteBytes(input[(Domain.Length + 16)..]);
        BinaryPrimitives.WriteUInt64BigEndian(input[(Domain.Length + 32)..], cycle);
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(input, digest);
        return digest[0] % 4;
    }
}
