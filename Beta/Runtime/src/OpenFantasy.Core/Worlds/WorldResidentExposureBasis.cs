using System.Collections.ObjectModel;
using OpenFantasy.Core.Exposure;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Exact earned exploration time for an unchanged resident membership segment. This is a transient
/// representation of the existing resident law, not a clock selector or a persisted owner format.
/// </summary>
internal sealed class WorldResidentExposureBasis
{
    private readonly ReadOnlyCollection<WorldResident> _residents;
    private readonly LocalExposureDuration _maximumBaseExposure;
    private readonly LocalExposureDuration _earned;

    internal WorldResidentExposureBasis(ReadOnlyCollection<WorldResident> residents, ulong advancingSteps)
    {
        ArgumentNullException.ThrowIfNull(residents);
        _residents = residents;
        _maximumBaseExposure = residents.Count == 0 ? default
            : residents.MaxBy(value => value.ExperiencedTime.TotalMilliseconds)!.ExperiencedTime;
        AdvancingSteps = advancingSteps;
    }

    private WorldResidentExposureBasis(WorldResidentExposureBasis source, ulong advancingSteps,
        LocalExposureDuration earned)
    {
        _residents = source._residents;
        _maximumBaseExposure = source._maximumBaseExposure;
        _earned = earned;
        AdvancingSteps = advancingSteps;
    }

    internal ulong AdvancingSteps { get; }

    internal WorldResidentExposureBasis AdvanceTo(ulong advancingSteps)
    {
        if (advancingSteps != checked(AdvancingSteps + 1))
            throw new InvalidOperationException("Resident exposure requires exactly one selected exploration boundary.");
        var earned = _earned.Add(ExposurePacingV1.FromExplorationSteps(1));
        // Check the same representable resident limit now, rather than deferring overflow until reading.
        _ = _maximumBaseExposure.Add(earned);
        return new(this, advancingSteps, earned);
    }

    internal EntityId InventoryCustodian(EntityId actorId)
    {
        foreach (var resident in _residents)
            if (resident.ActorId == actorId) return resident.InventoryCustodianId;
        throw new InvalidOperationException("The actor is outside this resident membership segment.");
    }

    internal ReadOnlyCollection<WorldResident> Materialize() => _earned == LocalExposureDuration.Zero
        ? _residents : Array.AsReadOnly(_residents.Select(value => value.Experience(_earned)).ToArray());
}
