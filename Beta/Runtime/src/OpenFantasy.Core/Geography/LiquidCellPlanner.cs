using OpenFantasy.Core.Exposure;

namespace OpenFantasy.Core.Geography;

/// <summary>
/// Moves existing unit intervals through one finite domain. All proposals use starting quantities:
/// incoming stock cannot leave again, and outgoing stock cannot create receiving room in this step.
/// </summary>
public sealed class LiquidCellPlanner
{
    public LiquidCellPlan PlanAdvance(LiquidCellState state, WorldInstant nextBoundary, bool held = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (nextBoundary == state.Boundary)
        {
            return new(state, state);
        }
        if (nextBoundary != state.Boundary.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep))
        {
            throw new ArgumentException("Liquid placement advances exactly one selected calendar step.", nameof(nextBoundary));
        }
        if (held)
        {
            return new(state, state.AdvanceBoundary(nextBoundary));
        }

        var transfers = new TransferBudget(state);
        transfers.ProposeDownward();
        transfers.ProposeLateral();
        var portions = AllocateStartingPortions(state, transfers.Transfers);

        // At most three faces per domain cell are selected: one down and two unordered horizontal.
        // Splitting the original portions adds at most one segment per transfer. The final constructor
        // normalizes and checks the retained fragment bound before this plan can be published.
        var candidate = state.WithPlacement(nextBoundary, checked(state.Revision + 1), portions);
        return new(state, candidate);
    }

    private static List<LiquidCellPortion> AllocateStartingPortions(LiquidCellState state, List<Transfer> transfers)
    {
        var outgoing = transfers.GroupBy(t => t.Donor).ToDictionary(g => g.Key, g => g.ToArray());
        var retained = new List<LiquidCellPortion>();
        var incoming = new List<LiquidCellPortion>();
        foreach (var cell in state.Portions.GroupBy(p => p.Cell).OrderBy(g => g.Key))
        {
            var stock = cell.OrderBy(p => p.Origin).ThenBy(p => p.OriginId).ThenBy(p => p.Start).ToArray();
            int index = 0;
            ulong start = stock[0].Start;
            if (outgoing.TryGetValue(cell.Key, out var deliveries))
            {
                foreach (var delivery in deliveries)
                {
                    ulong remaining = delivery.Quantity;
                    while (remaining > 0)
                    {
                        var portion = stock[index];
                        ulong amount = Math.Min(remaining, portion.End - start);
                        ulong end = checked(start + amount);
                        incoming.Add(new(delivery.Receiver, portion.Origin, portion.OriginId, start, end));
                        remaining -= amount;
                        start = end;
                        if (start == portion.End)
                        {
                            index++;
                            if (index < stock.Length)
                            {
                                start = stock[index].Start;
                            }
                        }
                    }
                }
            }
            if (index < stock.Length)
            {
                var first = stock[index];
                retained.Add(new(first.Cell, first.Origin, first.OriginId, start, first.End));
                retained.AddRange(stock.Skip(index + 1));
            }
        }
        retained.AddRange(incoming);
        return retained;
    }

    private readonly record struct Transfer(LiquidCellKey Donor, LiquidCellKey Receiver, ulong Quantity);

    private readonly record struct HorizontalFace(LiquidCellKey First, LiquidCellKey Second) : IComparable<HorizontalFace>
    {
        public int CompareTo(HorizontalFace other)
        {
            int first = First.CompareTo(other.First);
            return first == 0 ? Second.CompareTo(other.Second) : first;
        }
    }

    private sealed class TransferBudget
    {
        private readonly LiquidCellState _state;
        private readonly HashSet<LiquidCellKey> _domain;
        private readonly LiquidCellKey[] _active;
        private readonly Dictionary<LiquidCellKey, ulong> _spent = [];
        private readonly Dictionary<LiquidCellKey, ulong> _incoming = [];

        internal TransferBudget(LiquidCellState state)
        {
            _state = state;
            _domain = state.DomainCells.ToHashSet();
            _active = state.Quantities.Where(p => p.Value > 0).Select(p => p.Key).Order().ToArray();
        }

        internal List<Transfer> Transfers { get; } = [];

        internal void ProposeDownward()
        {
            foreach (var donor in _active)
            {
                var coordinate = donor.Coordinate;
                if (coordinate.Z > long.MinValue)
                {
                    var receiver = new LiquidCellKey(donor.ReservoirId, coordinate with { Z = coordinate.Z - 1 });
                    if (_domain.Contains(receiver))
                    {
                        Propose(donor, receiver, _state.Profile.DownwardUnitsPerFace);
                    }
                }
            }
        }

        internal void ProposeLateral()
        {
            var faces = new SortedSet<HorizontalFace>();
            foreach (var cell in _active)
            {
                foreach (var neighbor in HorizontalNeighbors(cell))
                {
                    if (_domain.Contains(neighbor))
                    {
                        faces.Add(cell.CompareTo(neighbor) < 0 ? new(cell, neighbor) : new(neighbor, cell));
                    }
                }
            }
            foreach (var face in faces)
            {
                ulong first = Quantity(face.First);
                ulong second = Quantity(face.Second);
                if (first > second)
                {
                    Propose(face.First, face.Second, Math.Min((first - second) / 2, _state.Profile.LateralUnitsPerFace));
                }
                else if (second > first)
                {
                    Propose(face.Second, face.First, Math.Min((second - first) / 2, _state.Profile.LateralUnitsPerFace));
                }
            }
        }

        private void Propose(LiquidCellKey donor, LiquidCellKey receiver, ulong requested)
        {
            ulong spent = _spent.GetValueOrDefault(donor);
            ulong incoming = _incoming.GetValueOrDefault(receiver);
            ulong available = Quantity(donor) - spent;
            ulong room = _state.UnitsPerCell - Quantity(receiver) - incoming;
            ulong amount = Math.Min(requested, Math.Min(available, room));
            if (amount == 0)
            {
                return;
            }
            _spent[donor] = checked(spent + amount);
            _incoming[receiver] = checked(incoming + amount);
            Transfers.Add(new(donor, receiver, amount));
        }

        private ulong Quantity(LiquidCellKey key) => _state.Quantities.GetValueOrDefault(key);

        private static IEnumerable<LiquidCellKey> HorizontalNeighbors(LiquidCellKey cell)
        {
            var at = cell.Coordinate;
            if (at.X > long.MinValue) yield return new(cell.ReservoirId, at with { X = at.X - 1 });
            if (at.X < long.MaxValue) yield return new(cell.ReservoirId, at with { X = at.X + 1 });
            if (at.Y > long.MinValue) yield return new(cell.ReservoirId, at with { Y = at.Y - 1 });
            if (at.Y < long.MaxValue) yield return new(cell.ReservoirId, at with { Y = at.Y + 1 });
        }
    }
}
