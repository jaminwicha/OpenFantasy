using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Deterministic bounded home amble; accepted samples use the same world movement rules as players.</summary>
public sealed class LocalNpcController
{
    public const string PolicyVersion = "local.home-amble.v1";
    private static readonly byte[] Domain = Encoding.ASCII.GetBytes(PolicyVersion);
    private readonly LocalConstructionController _construction = new();
    private readonly LocalCognitionController _cognition = new();

    /// <summary>
    /// The resident's funded commitment. In the cognition interpretation it comes from the builder's own intention,
    /// learned from a delivery it received; earlier profiles keep the construction controller's quest read.
    /// </summary>
    private ConstructionProject? Commitment(WorldSnapshot state, EntityId actorId) => LocalCognitionController.Applies(state)
        ? LocalCognitionController.FundedCommitment(state, actorId) : _construction.FindCommitment(state, actorId);
    public IReadOnlyList<WorldMovementInput> SelectMovement(WorldSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        List<WorldMovementInput> inputs = [];
        if (state.HasReadingHold || state.Pacing?.Encounter is not null)
        {
            return inputs.AsReadOnly();
        }
        foreach (WorldResident resident in state.Residents)
        {
            if (resident.ControllerKind != ResidentControllerKind.Host || !WorldActorAvailability.CanAct(state, resident.ActorId))
            {
                continue;
            }
            if (Commitment(state, resident.ActorId) is { } project)
            {
                WorldMovementInput? workApproach = _construction.SelectMovement(state, project);
                if (workApproach is not null)
                {
                    inputs.Add(workApproach);
                }
                continue;
            }
            if (LocalCognitionController.Applies(state) && _cognition.SelectMovement(state, resident) is { } gatherApproach)
            {
                inputs.Add(gatherApproach);
                continue;
            }
            if (_construction.NeedsReturnHome(state, resident))
            {
                WorldMovementInput? returnHome = _construction.SelectReturnHome(state, resident);
                if (returnHome is not null)
                {
                    inputs.Add(returnHome);
                }
                continue;
            }
            if (resident.WanderRadiusCells == 0)
            {
                continue;
            }
            ulong cadence = state.EncounterLifecycle is null ? state.AdvancingSteps : state.ProcessIntervals;
            // Sixteen possible movement samples, then forty-eight idle steps. No wall-clock seed.
            if (cadence % 64 >= 16)
            {
                continue;
            }
            int direction = Direction(state.WorldId, resident.ActorId, cadence / 64);
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

    internal IReadOnlyList<WorldActionCommand> SelectActions(WorldSnapshot state, Func<EntityId> newIdentity,
        IReadOnlySet<EntityId>? excludedActorIds = null)
    {
        List<WorldActionCommand> actions = [];
        if (state.HasReadingHold || state.Pacing?.Encounter is not null || state.Construction is null)
        {
            return actions.AsReadOnly();
        }
        foreach (WorldResident resident in state.Residents)
        {
            if (resident.ControllerKind != ResidentControllerKind.Host || excludedActorIds?.Contains(resident.ActorId) == true
                || !WorldActorAvailability.CanAct(state, resident.ActorId))
            {
                continue;
            }
            // A due reconsideration, a gathering swing or a look comes before ordinary funded work.
            WorldActionCommand? action = LocalCognitionController.Applies(state)
                ? _cognition.SelectAction(state, resident, newIdentity) : null;
            if (action is null && Commitment(state, resident.ActorId) is { } project)
            {
                action = _construction.SelectWork(state, project, newIdentity);
            }
            if (action is not null)
            {
                actions.Add(action);
            }
        }
        return actions.AsReadOnly();
    }

    internal IReadOnlyList<WorldActionCommand> SelectEncounterChoices(WorldSnapshot state, Func<EntityId> newIdentity,
        Retreat.LocalRetreatConfiguration? retreat = null) =>
        new LocalEncounterController(retreat).SelectChoices(state, newIdentity);

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
