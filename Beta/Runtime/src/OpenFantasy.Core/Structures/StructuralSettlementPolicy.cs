using OpenFantasy.Core.Content;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Structures;

/// <summary>Authored harmless-drop calibration for intact rigid settling within one selected exploration step.</summary>
public sealed class StructuralSettlementPolicy
{
    public StructuralSettlementPolicy(long maximumDropSubunits, long safeActorDropSubunits,
        long safeMemberDropSubunits, ulong gravityMillimetresPerSecondSquared)
    {
        if (maximumDropSubunits is < 1 or > SubunitPosition.SubunitsPerCell
            || safeActorDropSubunits < 0 || safeActorDropSubunits > maximumDropSubunits
            || safeMemberDropSubunits < 0 || safeMemberDropSubunits > maximumDropSubunits
            || gravityMillimetresPerSecondSquared is < 1 or > 1000000)
        {
            throw new ArgumentException("Settlement calibration requires bounded short drops and positive gravity.");
        }
        MaximumDropSubunits = maximumDropSubunits;
        SafeActorDropSubunits = safeActorDropSubunits;
        SafeMemberDropSubunits = safeMemberDropSubunits;
        GravityMillimetresPerSecondSquared = gravityMillimetresPerSecondSquared;
        _ = FlightMilliseconds(maximumDropSubunits);
        ContentHash = ContentHash.Compute(StructuralSettlementPolicyCodecV1.Encode(this));
    }

    public long MaximumDropSubunits { get; }
    public long SafeActorDropSubunits { get; }
    public long SafeMemberDropSubunits { get; }
    public ulong GravityMillimetresPerSecondSquared { get; }
    public ContentHash ContentHash { get; }

    /// <summary>Ceiling of free-fall time from rest using integer arithmetic; never grants a second time step.</summary>
    public ulong FlightMilliseconds(long dropSubunits)
    {
        if (dropSubunits < 0 || dropSubunits > MaximumDropSubunits)
        {
            throw new ArgumentOutOfRangeException(nameof(dropSubunits));
        }
        UInt128 distance = (UInt128)dropSubunits * 2 * 1000 * 1000000;
        ulong low = 0;
        ulong high = ExposurePacingV1.ExplorationMillisecondsPerStep;
        bool Reaches(ulong milliseconds) => (UInt128)GravityMillimetresPerSecondSquared * milliseconds
            * milliseconds * SubunitPosition.SubunitsPerCell >= distance;
        if (!Reaches(high))
        {
            throw new ArgumentException("The fall exceeds the selected exploration time interval.");
        }
        while (low < high)
        {
            ulong middle = low + (high - low) / 2;
            if (Reaches(middle)) { high = middle; } else { low = middle + 1; }
        }
        return low;
    }
}
