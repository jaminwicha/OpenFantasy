using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Geography;

/// <summary>Validates exact funded inputs and physical/counting compatibility before routing any contribution.</summary>
internal sealed class WatershedRunoffFunding
{
    private readonly WatershedRunoffRequest _request;

    internal WatershedRunoffFunding(WatershedRunoffRequest request, CancellationToken cancellation)
    {
        _request = request;
        ValidateScope();
        Dictionary<EntityId, PreparedWatershed> basins = request.Network.Basins.ToDictionary(b => b.BasinId);
        if (request.Inputs.Count != basins.Count || request.Inputs.Any(i => !basins.ContainsKey(i.BasinId))
            || request.Terminals.Count != request.Network.Terminals.Count
            || request.Terminals.Any(t => !request.Network.Terminals.Any(n => n.BasinId == t.BasinId)))
        {
            throw new ArgumentException("Runoff requires every basin, including dry ones, and every terminal exactly once.");
        }
        foreach (WatershedRunoffInput input in request.Inputs)
        {
            cancellation.ThrowIfCancellationRequested();
            PreparedWatershed basin = basins[input.BasinId];
            request.Mapping.ValidateFootprint(basin.Request.Bounds);
            ulong units = UnitsPerColumn(input);
            PerColumn.Add(input.BasinId, units);
            ulong total = checked(units * (ulong)basin.Columns.Count);
            TotalUnits = checked(TotalUnits + total);
            if (input.Source is null)
            {
                continue;
            }
            if (input.Source.Quantity != total)
            {
                throw new ArgumentException("Effective runoff depth must have exact whole-basin source funding.");
            }
            ValidateOwner(input.CustodianId!.Value);
            ValidateCoverage(input);
        }
        ValidateExclusiveReservation();
        HashSet<EntityId> sources = request.Inputs.Where(i => i.CustodianId is not null).Select(i => i.CustodianId!.Value).ToHashSet();
        foreach (WatershedRunoffTerminal terminal in request.Terminals)
        {
            ValidateOwner(terminal.CustodianId);
            if (sources.Contains(terminal.CustodianId))
            {
                throw new ArgumentException("Forcing source and receiving storage must have distinct roles.");
            }
            if (request.Inventory.Custody.Lots.Any(l => l.CustodianId == terminal.CustodianId && l.Definition != request.Material.Item)
                || (request.Production?.Lots.Any(l => l.CustodianId == terminal.CustodianId && l.Definition != request.Material.Item) ?? false))
            {
                throw new ArgumentException("Every existing receiver item needs the exact runoff calibration to establish volume headroom.");
            }
        }
    }

    internal Dictionary<EntityId, ulong> PerColumn { get; } = [];
    internal ulong TotalUnits { get; }

    private void ValidateScope()
    {
        WatershedRunoffDomain.From(_request).ValidateAuthority(_request.Inventory, _request.Production, _request.ContinuationQualification);
        if (!_request.Inventory.ReservationIds.Contains(_request.ReservationId))
        {
            throw new ArgumentException("The forcing reservation must be admitted by the complete authority.");
        }
    }

    private ulong UnitsPerColumn(WatershedRunoffInput input)
    {
        // One square metre under one micrometre of effective runoff is exactly 1,000 microlitres.
        UInt128 side = (uint)_request.Mapping.CellsPerColumn;
        UInt128 volume = side * side * input.DepthMicrometres * 1000;
        ulong unitVolume = _request.Material.Physical.VolumePerUnit.Value;
        if (volume % unitVolume != 0)
        {
            throw new ArgumentException("The forcing profile cannot round fractional counted water units.");
        }
        return checked((ulong)(volume / unitVolume));
    }

    private void ValidateOwner(EntityId custodian)
    {
        CustodianAccessPolicy? policy = _request.Inventory.Policies.SingleOrDefault(p => p.CustodianId == custodian);
        if (policy is null || policy.OwnerActorId != _request.OwnerActorId)
        {
            throw new ArgumentException("Every source and receiver requires the budget's admitted owner.");
        }
    }

    private void ValidateCoverage(WatershedRunoffInput input)
    {
        SourceInterval claim = input.Source!;
        ulong cursor = claim.Start;
        foreach (CountedSourceLot lot in _request.Inventory.Custody.Lots.Where(l => l.Source.SourceId == claim.SourceId)
            .OrderBy(l => l.Source.Start))
        {
            ulong start = Math.Max(claim.Start, lot.Source.Start), end = Math.Min(claim.End, lot.Source.End);
            if (start >= end)
            {
                continue;
            }
            if (start != cursor || lot.CustodianId != input.CustodianId || lot.Definition != _request.Material.Item
                || lot.ReservedBy != _request.ReservationId || lot.Protection != LotProtection.None)
            {
                throw new ArgumentException("Every forcing unit must be actual eligible raw stock reserved for this budget.");
            }
            cursor = end;
        }
        if (cursor != claim.End)
        {
            throw new ArgumentException("Runoff preparation cannot replace missing or retired source stock.");
        }
    }

    private void ValidateExclusiveReservation()
    {
        WatershedRunoffInput[] wet = _request.Inputs.Where(i => i.Source is not null).ToArray();
        for (int i = 0; i < wet.Length; i++)
        {
            for (int j = i + 1; j < wet.Length; j++)
            {
                SourceInterval a = wet[i].Source!, b = wet[j].Source!;
                if (a.SourceId == b.SourceId && a.Start < b.End && b.Start < a.End)
                {
                    throw new ArgumentException("Two forcing inputs cannot spend the same source interval.");
                }
            }
        }
        foreach (CountedSourceLot lot in _request.Inventory.Custody.Lots.Where(l => l.ReservedBy == _request.ReservationId))
        {
            ulong covered = 0;
            foreach (WatershedRunoffInput input in wet.Where(i => i.Source!.SourceId == lot.Source.SourceId && i.CustodianId == lot.CustodianId))
            {
                ulong start = Math.Max(lot.Source.Start, input.Source!.Start), end = Math.Min(lot.Source.End, input.Source.End);
                if (start < end)
                {
                    covered = checked(covered + end - start);
                }
            }
            if (covered != lot.Quantity)
            {
                throw new ArgumentException("The budget must describe all raw stock held by its reservation.");
            }
        }
        if (_request.Production?.Lots.Any(l => l.ReservedBy == _request.ReservationId) ?? false)
        {
            throw new ArgumentException("A raw forcing reservation cannot also claim derived output stock.");
        }
    }
}
