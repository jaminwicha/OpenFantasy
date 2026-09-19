using System.Collections.ObjectModel;
using System.Numerics;

namespace OpenFantasy.Core.Structures;

/// <summary>
/// Immutable bounded failure planning. It does not move actors, create debris or retire material.
/// Each call performs bounded node evaluations and stops at a pass boundary; array copies, failed-node
/// skips and an optional pass reset each cover at most 4096 elements once per call.
/// </summary>
public sealed class StructuralLoadResolution
{
    public const int DefaultMaximumEvaluations = 64;
    public const int MaximumEvaluationsPerAdvance = 256;
    private readonly bool[] _excludedAtPassStart;
    private readonly UInt128[] _loads;
    private readonly SortedDictionary<EntityId, StructuralFailure> _failures;
    private readonly bool _newFailuresThisPass;
    private readonly ReadOnlyDictionary<EntityId, UInt128>? _activeLoads;
    private readonly UInt128 _acceptedFoundationLoad;
    private readonly UInt128 _failedInputMass;

    private StructuralLoadResolution(StructuralLoadGraph graph, ReadOnlyCollection<EntityId> removedIds,
        bool[] excludedAtPassStart, UInt128[] loads, SortedDictionary<EntityId, StructuralFailure> failures,
        uint pass, int cursor, ulong totalEvaluations, bool newFailuresThisPass, bool isComplete)
    {
        Graph = graph;
        RemovedIds = removedIds;
        _excludedAtPassStart = excludedAtPassStart;
        _loads = loads;
        _failures = failures;
        _newFailuresThisPass = newFailuresThisPass;
        Pass = pass;
        Cursor = cursor;
        TotalEvaluations = totalEvaluations;
        IsComplete = isComplete;
        Failures = Array.AsReadOnly(failures.Values.ToArray());
        CurrentPassLoads = Array.AsReadOnly(loads);
        if (isComplete)
        {
            var active = new SortedDictionary<EntityId, UInt128>();
            for (int index = 0; index < graph.Elements.Count; index++)
            {
                StructuralElement element = graph.Elements[index];
                if (failures.ContainsKey(element.ElementId))
                {
                    _failedInputMass += element.InputMass;
                    continue;
                }
                active.Add(element.ElementId, loads[index]);
                if (element.IsFoundation)
                {
                    _acceptedFoundationLoad += loads[index];
                }
            }
            if (_acceptedFoundationLoad + _failedInputMass != graph.TotalInputMass)
            {
                throw new InvalidOperationException("Final structural accounting must conserve every input mass exactly once.");
            }
            _activeLoads = new(active);
        }
    }

    public StructuralLoadGraph Graph { get; }
    public ReadOnlyCollection<EntityId> RemovedIds { get; }
    public ReadOnlyCollection<StructuralFailure> Failures { get; }
    /// <summary>Provisional loads in Graph.Elements order. These are not final loads until IsComplete.</summary>
    public ReadOnlyCollection<UInt128> CurrentPassLoads { get; }
    public bool IsComplete { get; }
    public uint Pass { get; }
    /// <summary>The next position in Graph.TopologicalOrder; already failed positions consume no evaluation.</summary>
    public int Cursor { get; }
    public ulong TotalEvaluations { get; }
    public IReadOnlyDictionary<EntityId, UInt128> ActiveLoads => _activeLoads
        ?? throw new InvalidOperationException("A partial resolution has no stable final loads.");
    public UInt128 AcceptedFoundationLoad => IsComplete ? _acceptedFoundationLoad
        : throw new InvalidOperationException("A partial resolution has no final foundation accounting.");
    public UInt128 FailedInputMass => IsComplete ? _failedInputMass
        : throw new InvalidOperationException("A partial resolution has no final failed-input accounting.");

    public static StructuralLoadResolution Begin(StructuralLoadGraph graph, IEnumerable<EntityId> removedIds)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(removedIds);
        var failures = new SortedDictionary<EntityId, StructuralFailure>();
        var excluded = new bool[graph.Elements.Count];
        foreach (EntityId identity in removedIds)
        {
            int index = graph.IndexOf(identity);
            if (!failures.TryAdd(identity, new(identity, StructuralFailureReason.Removed, graph.Elements[index].InputMass, 0)))
            {
                throw new ArgumentException("An initial removal cannot repeat an element.", nameof(removedIds));
            }
            excluded[index] = true;
        }
        return new(graph, Array.AsReadOnly(failures.Keys.ToArray()), excluded, InitialLoads(graph, excluded), failures,
            1, 0, 0, false, false);
    }

    /// <summary>
    /// Validates retained work by replaying its bounded prefix and comparing every saved field.
    /// Validation may evaluate up to N * (N + 1) nodes, independently of the normal per-Advance bound.
    /// Cancellation is checked between replay slices; no supplied work state is trusted directly.
    /// </summary>
    public static StructuralLoadResolution Restore(StructuralLoadGraph graph, IEnumerable<EntityId> removedIds,
        uint pass, int cursor, ulong totalEvaluations, bool isComplete, IEnumerable<StructuralFailure> failures,
        IEnumerable<UInt128> currentPassLoads, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(failures);
        ArgumentNullException.ThrowIfNull(currentPassLoads);
        int count = graph.Elements.Count;
        ulong maximumEvaluations = (ulong)count * (ulong)(count + 1);
        if (pass is 0 || pass > count + 1 || cursor < 0 || cursor > count || totalEvaluations > maximumEvaluations)
        {
            throw new ArgumentException("Retained structural progress exceeds the graph's finite resolution bounds.");
        }
        StructuralFailure[] savedFailures = failures.Take(count + 1).ToArray();
        UInt128[] savedLoads = currentPassLoads.Take(count + 1).ToArray();
        if (savedFailures.Length > count || savedFailures.Any(failure => failure is null)
            || savedLoads.Length != count || savedLoads.Any(load => load > graph.TotalInputMass))
        {
            throw new ArgumentException("Retained structural failures and loads must fit the graph.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        StructuralLoadResolution replay = Begin(graph, removedIds);
        while (replay.TotalEvaluations < totalEvaluations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (replay.IsComplete)
            {
                throw new ArgumentException("Retained evaluations continue beyond the complete resolution.");
            }
            int budget = (int)Math.Min((ulong)MaximumEvaluationsPerAdvance, totalEvaluations - replay.TotalEvaluations);
            replay = replay.Advance(budget);
        }
        // A slice may end immediately after its last evaluation, before trailing failed positions.
        // Only those zero-evaluation boundaries may still be traversed to reach the retained state.
        for (int boundary = 0; boundary < 2 && !MatchesProgress(replay); boundary++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (replay.IsComplete)
            {
                break;
            }
            StructuralLoadResolution next = replay.Advance(1);
            if (next.TotalEvaluations != totalEvaluations)
            {
                break;
            }
            replay = next;
        }
        if (!MatchesProgress(replay) || !replay.Failures.SequenceEqual(savedFailures)
            || !replay.CurrentPassLoads.SequenceEqual(savedLoads))
        {
            throw new ArgumentException("Retained structural state does not match deterministic resolution replay.");
        }
        return replay;

        bool MatchesProgress(StructuralLoadResolution candidate) => candidate.Pass == pass && candidate.Cursor == cursor
            && candidate.TotalEvaluations == totalEvaluations && candidate.IsComplete == isComplete;
    }

    public StructuralLoadResolution Advance(int maximumEvaluations = DefaultMaximumEvaluations)
    {
        if (maximumEvaluations is < 1 or > MaximumEvaluationsPerAdvance)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumEvaluations));
        }
        if (IsComplete)
        {
            return this;
        }
        var loads = (UInt128[])_loads.Clone();
        var failures = new SortedDictionary<EntityId, StructuralFailure>(_failures);
        int cursor = Cursor;
        int evaluations = 0;
        bool changed = _newFailuresThisPass;
        while (cursor < Graph.Elements.Count && evaluations < maximumEvaluations)
        {
            int index = Graph.IndexAtCursor(cursor++);
            if (_excludedAtPassStart[index])
            {
                continue;
            }
            evaluations++;
            StructuralElement element = Graph.Elements[index];
            StructuralBearing[] active = Graph.Outgoing(index)
                .Where(bearing => !_excludedAtPassStart[Graph.IndexOf(bearing.SupportingElementId)]).ToArray();
            StructuralFailureReason? reason = element.ConditionParts == 0 ? StructuralFailureReason.ConditionLost
                : !element.IsFoundation && active.Length < element.MinimumSupportCount ? StructuralFailureReason.Unsupported
                : loads[index] > element.EffectiveCapacity ? StructuralFailureReason.Overloaded : null;
            if (reason is { } failureReason)
            {
                failures.Add(element.ElementId, new(element.ElementId, failureReason, loads[index], Pass));
                changed = true;
            }
            else if (!element.IsFoundation)
            {
                Distribute(loads[index], active, loads);
            }
        }
        ulong total = checked(TotalEvaluations + (ulong)evaluations);
        if (cursor < Graph.Elements.Count)
        {
            return new(Graph, RemovedIds, _excludedAtPassStart, loads, failures, Pass, cursor, total, changed, false);
        }
        if (!changed)
        {
            return new(Graph, RemovedIds, _excludedAtPassStart, loads, failures, Pass, cursor, total, false, true);
        }
        bool[] excluded = Graph.Elements.Select(element => failures.ContainsKey(element.ElementId)).ToArray();
        return new(Graph, RemovedIds, excluded, InitialLoads(Graph, excluded), failures, checked(Pass + 1), 0, total, false, false);
    }

    private static UInt128[] InitialLoads(StructuralLoadGraph graph, bool[] excluded) =>
        graph.Elements.Select((element, index) => excluded[index] ? (UInt128)0 : element.InputMass).ToArray();

    private void Distribute(UInt128 load, IReadOnlyList<StructuralBearing> active, UInt128[] loads)
    {
        UInt128 weight = active.Aggregate((UInt128)0, (total, bearing) => total + bearing.Weight);
        var shares = new UInt128[active.Count];
        UInt128 distributed = 0;
        for (int index = 0; index < active.Count; index++)
        {
            shares[index] = WeightedFloor(load, active[index].Weight, weight);
            distributed += shares[index];
        }
        UInt128 remainder = load - distributed;
        if (remainder >= (UInt128)active.Count)
        {
            throw new InvalidOperationException("Weighted floor allocation leaves fewer units than bearing edges.");
        }
        for (int index = 0; index < active.Count; index++)
        {
            UInt128 share = shares[index] + ((UInt128)index < remainder ? (UInt128)1 : 0);
            int support = Graph.IndexOf(active[index].SupportingElementId);
            loads[support] = checked(loads[support] + share);
        }
    }

    private static UInt128 WeightedFloor(UInt128 load, ulong weight, UInt128 totalWeight)
    {
        // Wider transient arithmetic keeps the weighted product exact. Retained loads remain UInt128.
        BigInteger share = BigInteger.CreateChecked(load) * weight / BigInteger.CreateChecked(totalWeight);
        return UInt128.CreateChecked(share);
    }
}
