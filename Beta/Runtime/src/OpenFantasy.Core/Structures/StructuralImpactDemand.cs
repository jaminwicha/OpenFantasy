using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Structures;

/// <summary>
/// Gravitational energy delivered by one intact body's descent from rest. This is an input to a
/// response policy, not a damage, impulse, fracture, healing or recoverable-material entitlement.
/// </summary>
public sealed class StructuralImpactDemand
{
    public const ulong EnergyDenominator = SubunitPosition.SubunitsPerCell;

    internal StructuralImpactDemand(EntityId bodyId, bool isActor, ulong massMilligrams,
        long dropSubunits, StructuralSettlementPolicy policy)
    {
        if (dropSubunits <= 0 || dropSubunits > policy.MaximumDropSubunits)
        {
            throw new ArgumentOutOfRangeException(nameof(dropSubunits));
        }
        BodyId = bodyId;
        IsActor = isActor;
        MassMilligrams = massMilligrams;
        DropSubunits = dropSubunits;
        // mg * (mm / s²) * (1000 mm / 256 subunits) is pJ. Keep the fractional pJ:
        // rounding each separate actor/member/group could create or lose energy on repartition.
        EnergyNumeratorPicojoules = checked((UInt128)massMilligrams
            * policy.GravityMillimetresPerSecondSquared * (ulong)dropSubunits * 1000);
        FlightMilliseconds = policy.FlightMilliseconds(dropSubunits);
        IsHarmless = dropSubunits <= (isActor ? policy.SafeActorDropSubunits : policy.SafeMemberDropSubunits);
    }

    public EntityId BodyId { get; }
    public bool IsActor { get; }
    public ulong MassMilligrams { get; }
    public long DropSubunits { get; }
    public UInt128 EnergyNumeratorPicojoules { get; }
    public ulong FlightMilliseconds { get; }
    public bool IsHarmless { get; }
}
