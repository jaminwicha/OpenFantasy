using OpenFantasy.Core.Production;
using OpenFantasy.Core.Structures;

namespace OpenFantasy.Core.Tests.Structures;

public sealed class StructuralLoadTests
{
    [Fact]
    public void Failed_bearing_redistributes_to_safe_alternate_without_losing_its_carried_load()
    {
        var graph = new StructuralLoadGraph([Element(1, 80), Foundation(2, 30, own: 1), Foundation(3, 100)],
            [Bearing(20, 1, 2), Bearing(10, 1, 3)]);
        StructuralLoadResolution first = StructuralLoadResolution.Begin(graph, []).Advance();
        Assert.False(first.IsComplete);
        Assert.Equal(2U, first.Pass);
        StructuralFailure failure = Assert.Single(first.Failures);
        Assert.Equal(Id(2), failure.ElementId);
        Assert.Equal(StructuralFailureReason.Overloaded, failure.Reason);
        Assert.Equal((UInt128)41, failure.Load);
        Assert.Equal(1U, failure.Pass);

        StructuralLoadResolution final = Finish(first);
        Assert.Equal((UInt128)80, final.ActiveLoads[Id(1)]);
        Assert.Equal((UInt128)80, final.ActiveLoads[Id(3)]);
        Assert.Equal(failure, Assert.Single(final.Failures));
        Assert.Equal((UInt128)1, final.FailedInputMass);
        Conserves(final);
    }

    [Fact]
    public void Last_support_failure_propagates_on_the_following_pass()
    {
        var graph = new StructuralLoadGraph([Element(1, 10), Foundation(2, 5)], [Bearing(10, 1, 2)]);
        StructuralLoadResolution final = Finish(StructuralLoadResolution.Begin(graph, []));
        Assert.Equal(3U, final.Pass);
        Assert.Equal(StructuralFailureReason.Unsupported, final.Failures.Single(value => value.ElementId == Id(1)).Reason);
        Assert.Equal(2U, final.Failures.Single(value => value.ElementId == Id(1)).Pass);
        Assert.Equal((UInt128)10, final.Failures.Single(value => value.ElementId == Id(2)).Load);
        Assert.Empty(final.ActiveLoads);
        Assert.Equal((UInt128)10, final.FailedInputMass);
        Conserves(final);
    }

    [Fact]
    public void Redistributed_overload_can_fail_an_alternate_then_the_supported_element()
    {
        StructuralLoadResolution final = Finish(StructuralLoadResolution.Begin(Cascade(), []), 1);
        Assert.Equal(4U, final.Pass);
        Assert.Equal(6UL, final.TotalEvaluations);
        Assert.Equal((1U, (UInt128)40), FailureDetails(final, 2));
        Assert.Equal((2U, (UInt128)80), FailureDetails(final, 3));
        Assert.Equal((3U, (UInt128)80), FailureDetails(final, 1));
        Assert.Equal(StructuralFailureReason.Unsupported, final.Failures.Single(value => value.ElementId == Id(1)).Reason);
        Conserves(final);
    }

    [Fact]
    public void Weighted_floor_remainders_follow_bearing_identity_rather_than_element_or_input_order()
    {
        var graph = new StructuralLoadGraph([Foundation(2, 100), Element(1, 11), Foundation(3, 100), Foundation(4, 100)],
            [Bearing(30, 1, 2, 1), Bearing(10, 1, 4, 3), Bearing(20, 1, 3, 2)]);
        StructuralLoadResolution final = Finish(StructuralLoadResolution.Begin(graph, []));
        Assert.Equal((UInt128)6, final.ActiveLoads[Id(4)]);
        Assert.Equal((UInt128)4, final.ActiveLoads[Id(3)]);
        Assert.Equal((UInt128)1, final.ActiveLoads[Id(2)]);
        Assert.Empty(final.Failures);
        Conserves(final);
    }

    [Fact]
    public void Removed_supports_preserve_input_mass_but_receive_no_transferred_load()
    {
        var graph = new StructuralLoadGraph([Element(1, 80), Foundation(2, 100, own: 7), Foundation(3, 100)],
            [Bearing(10, 1, 2), Bearing(20, 1, 3)]);
        StructuralLoadResolution final = Finish(StructuralLoadResolution.Begin(graph, [Id(2)]));
        StructuralFailure removed = Assert.Single(final.Failures);
        Assert.Equal(StructuralFailureReason.Removed, removed.Reason);
        Assert.Equal(0U, removed.Pass);
        Assert.Equal((UInt128)7, removed.Load);
        Assert.Equal((UInt128)80, final.AcceptedFoundationLoad);
        Assert.Equal(new[] { Id(2) }, final.RemovedIds);
        Conserves(final);
    }

    [Fact]
    public void Bracing_requires_distinct_remaining_supports_even_if_one_has_enough_capacity()
    {
        var braced = new StructuralElement(Id(1), new(10), new(0), new(100), 10_000, false, 2);
        var graph = new StructuralLoadGraph([braced, Foundation(2, 100), Foundation(3, 100)],
            [Bearing(10, 1, 2), Bearing(20, 1, 3)]);
        StructuralLoadResolution final = Finish(StructuralLoadResolution.Begin(graph, [Id(2)]));
        Assert.Equal(StructuralFailureReason.Unsupported, final.Failures.Single(value => value.ElementId == Id(1)).Reason);
        Assert.Equal((UInt128)0, final.ActiveLoads[Id(3)]);
        Conserves(final);
        Assert.Throws<ArgumentException>(() => new StructuralLoadGraph(graph.Elements,
            [Bearing(10, 1, 2), Bearing(20, 1, 2)]));
    }

    [Theory]
    [InlineData(50UL, false)]
    [InlineData(51UL, true)]
    public void Condition_scales_capacity_with_integer_floor(ulong applied, bool fails)
    {
        var damaged = new StructuralElement(Id(1), new(0), new(applied), new(101), 5000, false);
        var graph = new StructuralLoadGraph([damaged, Foundation(2, 100)], [Bearing(10, 1, 2)]);
        Assert.Equal((UInt128)50, damaged.EffectiveCapacity);
        StructuralLoadResolution final = Finish(StructuralLoadResolution.Begin(graph, []));
        Assert.Equal(fails, final.Failures.Any());
        if (fails)
        {
            Assert.Equal(StructuralFailureReason.Overloaded, Assert.Single(final.Failures).Reason);
        }
        Conserves(final);
    }

    [Fact]
    public void Zero_condition_fails_even_with_zero_load_and_foundations_require_explicit_terminal_rules()
    {
        var broken = new StructuralElement(Id(1), new(0), new(0), new(100), 0, true, 0);
        StructuralLoadResolution final = Finish(StructuralLoadResolution.Begin(new([broken], []), []));
        Assert.Equal(StructuralFailureReason.ConditionLost, Assert.Single(final.Failures).Reason);
        Conserves(final);
        Assert.Throws<ArgumentException>(() => new StructuralElement(Id(2), new(0), new(0), new(1), 10_001, false));
        Assert.Throws<ArgumentException>(() => new StructuralElement(Id(2), new(0), new(0), new(0), 10_000, false));
        Assert.Throws<ArgumentException>(() => new StructuralElement(Id(2), new(0), new(0), new(1), 10_000, false, 0));
        Assert.Throws<ArgumentException>(() => new StructuralElement(Id(2), new(0), new(0), new(1), 10_000, false, 33));
        Assert.Throws<ArgumentException>(() => new StructuralElement(Id(2), new(0), new(0), new(1), 10_000, true));
        Assert.Throws<ArgumentException>(() => new StructuralLoadGraph([Foundation(1, 1), Foundation(2, 1)], [Bearing(10, 1, 2)]));
    }

    [Fact]
    public void Graph_rejects_cycles_missing_endpoints_and_identity_aliases()
    {
        Assert.Throws<ArgumentException>(() => new StructuralLoadGraph([], []));
        Assert.Throws<ArgumentException>(() => new StructuralLoadGraph([Element(1), Element(1)], []));
        Assert.Throws<ArgumentException>(() => new StructuralLoadGraph([Element(1), Element(2)],
            [Bearing(10, 1, 2), Bearing(20, 2, 1)]));
        Assert.Throws<ArgumentException>(() => new StructuralLoadGraph([Element(1)], [Bearing(10, 1, 2)]));
        Assert.Throws<ArgumentException>(() => new StructuralLoadGraph([Element(1), Foundation(2, 1)], [Bearing(1, 1, 2)]));
        Assert.Throws<ArgumentException>(() => new StructuralLoadGraph([Element(1), Element(2), Foundation(3, 1)],
            [Bearing(10, 1, 3), Bearing(10, 2, 3)]));
        Assert.Throws<ArgumentException>(() => Bearing(10, 1, 1));
        Assert.Throws<ArgumentException>(() => Bearing(10, 1, 2, 0));
        var unsupported = new StructuralLoadGraph([Element(1, 1)], []);
        Assert.Equal(StructuralFailureReason.Unsupported,
            Assert.Single(Finish(StructuralLoadResolution.Begin(unsupported, [])).Failures).Reason);
    }

    [Fact]
    public void Graph_bounds_and_initial_removal_bounds_are_explicit()
    {
        Assert.Throws<ArgumentException>(() => new StructuralLoadGraph(
            Enumerable.Range(1, StructuralLoadGraph.MaximumElements + 1).Select(index => Element((ulong)index)), []));
        StructuralElement[] manySupports = Enumerable.Range(2, 33).Select(index => Foundation((ulong)index, 100)).Prepend(Element(1)).ToArray();
        Assert.Throws<ArgumentException>(() => new StructuralLoadGraph(manySupports,
            Enumerable.Range(2, 33).Select(index => Bearing((ulong)index + 100, 1, (ulong)index))));
        var graph = new StructuralLoadGraph([Foundation(1, 100)], []);
        Assert.Throws<ArgumentException>(() => StructuralLoadResolution.Begin(graph, [Id(1), Id(1)]));
        Assert.Throws<ArgumentException>(() => StructuralLoadResolution.Begin(graph, [Id(2)]));
    }

    [Fact]
    public void Maximum_element_masses_accumulate_above_ulong_without_overflow_or_double_counting()
    {
        int count = StructuralLoadGraph.MaximumElements;
        StructuralElement[] elements = Enumerable.Range(1, count - 1)
            .Select(index => Element((ulong)index, ulong.MaxValue, ulong.MaxValue))
            .Append(new StructuralElement(Id((ulong)count), new(ulong.MaxValue), new(ulong.MaxValue),
                new(ulong.MaxValue), 10_000, true, 0)).ToArray();
        var graph = new StructuralLoadGraph(elements, Enumerable.Range(1, count - 1)
            .Select(index => Bearing((ulong)count + (ulong)index, (ulong)index, (ulong)count)));
        StructuralLoadResolution final = Finish(StructuralLoadResolution.Begin(graph, []), 256);
        UInt128 expected = (UInt128)ulong.MaxValue * ((uint)count + 1);
        Assert.Equal(expected, graph.TotalInputMass);
        Assert.Equal(expected, final.Failures.Single(value => value.ElementId == Id((ulong)count)).Load);
        Assert.Equal(expected, final.FailedInputMass);
        Assert.Equal(count, final.Failures.Count);
        Conserves(final);
    }

    [Fact]
    public void Maximum_weights_share_mass_exactly_with_deterministic_small_remainders()
    {
        var supports = Enumerable.Range(2, 32).Select(index => Foundation((ulong)index, ulong.MaxValue));
        var graph = new StructuralLoadGraph(supports.Prepend(Element(1, ulong.MaxValue, ulong.MaxValue)),
            Enumerable.Range(2, 32).Reverse().Select(index => Bearing((ulong)index + 100, 1, (ulong)index, ulong.MaxValue)));
        StructuralLoadResolution final = Finish(StructuralLoadResolution.Begin(graph, []));
        UInt128 quotient = (UInt128)ulong.MaxValue / 32;
        for (ulong identity = 2; identity <= 33; identity++)
        {
            Assert.Equal(quotient + (identity < 33 ? (UInt128)1 : 0), final.ActiveLoads[Id(identity)]);
        }
        Assert.Empty(final.Failures);
        Conserves(final);
    }

    [Fact]
    public void Slicing_and_input_permutation_preserve_first_failures_final_loads_and_evaluation_count()
    {
        StructuralLoadGraph graph = Cascade();
        var reordered = new StructuralLoadGraph(graph.Elements.Reverse(), graph.Bearings.Reverse());
        Assert.Equal(graph.Elements.Select(value => value.ElementId), reordered.Elements.Select(value => value.ElementId));
        Assert.Equal(graph.Bearings.Select(value => value.BearingId), reordered.Bearings.Select(value => value.BearingId));
        Assert.Equal(graph.TopologicalOrder, reordered.TopologicalOrder);
        StructuralLoadResolution reference = Finish(StructuralLoadResolution.Begin(graph, []), 256);
        foreach (int slice in new[] { 1, 2, 3, 64, 256 })
        {
            StructuralLoadResolution result = Finish(StructuralLoadResolution.Begin(reordered, []), slice);
            Assert.Equal(reference.Failures, result.Failures);
            Assert.Equal(reference.ActiveLoads, result.ActiveLoads);
            Assert.Equal(reference.Pass, result.Pass);
            Assert.Equal(reference.TotalEvaluations, result.TotalEvaluations);
            Conserves(result);
        }
    }

    [Fact]
    public void Partial_work_is_immutable_bounded_and_cannot_expose_final_accounting()
    {
        var graph = new StructuralLoadGraph([Element(1, 5), Foundation(2, 100)], [Bearing(10, 1, 2)]);
        StructuralLoadResolution initial = StructuralLoadResolution.Begin(graph, []);
        StructuralLoadResolution advanced = initial.Advance(1);
        Assert.False(initial.IsComplete);
        Assert.Equal(0, initial.Cursor);
        Assert.Equal(0UL, initial.TotalEvaluations);
        Assert.Empty(initial.Failures);
        Assert.Equal(1, advanced.Cursor);
        Assert.Equal(1UL, advanced.TotalEvaluations);
        Assert.Throws<InvalidOperationException>(() => advanced.ActiveLoads);
        Assert.Throws<InvalidOperationException>(() => advanced.AcceptedFoundationLoad);
        Assert.Throws<InvalidOperationException>(() => advanced.FailedInputMass);
        Assert.Throws<ArgumentOutOfRangeException>(() => initial.Advance(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => initial.Advance(257));
        StructuralLoadResolution final = Finish(advanced, 1);
        Assert.Same(final, final.Advance());
        Conserves(final);
    }

    [Fact]
    public void Every_retained_slice_restores_exact_progress_loads_and_first_failures()
    {
        foreach (int slice in new[] { 1, 2, 3, 64, 256 })
        {
            StructuralLoadResolution state = StructuralLoadResolution.Begin(Cascade(), []);
            while (true)
            {
                StructuralLoadResolution restored = Restore(state);
                Assert.Equal(state.Pass, restored.Pass);
                Assert.Equal(state.Cursor, restored.Cursor);
                Assert.Equal(state.TotalEvaluations, restored.TotalEvaluations);
                Assert.Equal(state.IsComplete, restored.IsComplete);
                Assert.Equal(state.Failures, restored.Failures);
                Assert.Equal(state.CurrentPassLoads, restored.CurrentPassLoads);
                if (state.IsComplete)
                {
                    Conserves(restored);
                    break;
                }
                state = state.Advance(slice);
            }
        }
    }

    [Fact]
    public void Restore_accepts_zero_evaluation_completion_and_trailing_failed_pass_boundaries()
    {
        var graph = new StructuralLoadGraph([Foundation(1, 100), Foundation(2, 100)], []);
        StructuralLoadResolution allRemoved = StructuralLoadResolution.Begin(graph, [Id(2), Id(1)]).Advance(1);
        Assert.True(allRemoved.IsComplete);
        Assert.Equal(0UL, allRemoved.TotalEvaluations);
        Assert.True(Restore(allRemoved).IsComplete);

        StructuralLoadResolution trailingRemoved = StructuralLoadResolution.Begin(graph, [Id(2)]);
        StructuralLoadResolution sliced = trailingRemoved.Advance(1);
        StructuralLoadResolution completed = trailingRemoved.Advance(2);
        Assert.False(sliced.IsComplete);
        Assert.Equal(1, sliced.Cursor);
        Assert.True(completed.IsComplete);
        Assert.Equal(sliced.TotalEvaluations, completed.TotalEvaluations);
        Assert.False(Restore(sliced).IsComplete);
        Assert.True(Restore(completed).IsComplete);

        var unsupported = new StructuralLoadGraph([Element(1, 1), Foundation(2, 100)], []);
        StructuralLoadResolution reset = StructuralLoadResolution.Begin(unsupported, [Id(2)]).Advance(2);
        Assert.Equal(2U, reset.Pass);
        Assert.Equal(0, reset.Cursor);
        Assert.False(Restore(reset).IsComplete);
        StructuralLoadResolution emptyFinalPass = reset.Advance(1);
        Assert.Equal(reset.TotalEvaluations, emptyFinalPass.TotalEvaluations);
        Assert.True(Restore(emptyFinalPass).IsComplete);
    }

    [Fact]
    public void Restore_rejects_forged_progress_loads_and_failures_and_supports_cancellation()
    {
        StructuralLoadResolution state = StructuralLoadResolution.Begin(Cascade(), []).Advance(2);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Assert.Throws<ArgumentException>(() => StructuralLoadResolution.Restore(state.Graph, state.RemovedIds,
            state.Pass, state.Cursor - 1, state.TotalEvaluations, state.IsComplete, state.Failures, state.CurrentPassLoads, cancellationToken));
        Assert.Throws<ArgumentException>(() => StructuralLoadResolution.Restore(state.Graph, state.RemovedIds,
            state.Pass, state.Cursor, ulong.MaxValue, state.IsComplete, state.Failures, state.CurrentPassLoads, cancellationToken));
        Assert.Throws<ArgumentException>(() => StructuralLoadResolution.Restore(state.Graph, state.RemovedIds,
            0, state.Cursor, state.TotalEvaluations, state.IsComplete, state.Failures, state.CurrentPassLoads, cancellationToken));
        Assert.Throws<ArgumentException>(() => StructuralLoadResolution.Restore(state.Graph, state.RemovedIds,
            state.Pass, state.Cursor, state.TotalEvaluations, !state.IsComplete, state.Failures, state.CurrentPassLoads, cancellationToken));
        UInt128[] changedLoads = state.CurrentPassLoads.ToArray();
        changedLoads[0]--;
        Assert.Throws<ArgumentException>(() => StructuralLoadResolution.Restore(state.Graph, state.RemovedIds,
            state.Pass, state.Cursor, state.TotalEvaluations, state.IsComplete, state.Failures, changedLoads, cancellationToken));
        StructuralFailure first = Assert.Single(state.Failures);
        var changedFailure = new StructuralFailure(first.ElementId, first.Reason, first.Load + 1, first.Pass);
        Assert.Throws<ArgumentException>(() => StructuralLoadResolution.Restore(state.Graph, state.RemovedIds,
            state.Pass, state.Cursor, state.TotalEvaluations, state.IsComplete, [changedFailure], state.CurrentPassLoads, cancellationToken));
        Assert.Throws<ArgumentException>(() => StructuralLoadResolution.Restore(state.Graph, state.RemovedIds,
            state.Pass, state.Cursor, state.TotalEvaluations, state.IsComplete, [], state.CurrentPassLoads, cancellationToken));
        Assert.Throws<ArgumentException>(() => StructuralLoadResolution.Restore(state.Graph, state.RemovedIds,
            state.Pass, state.Cursor, state.TotalEvaluations, state.IsComplete, state.Failures, [], cancellationToken));
        Assert.Throws<OperationCanceledException>(() => StructuralLoadResolution.Restore(state.Graph, state.RemovedIds,
            state.Pass, state.Cursor, state.TotalEvaluations, state.IsComplete, state.Failures, state.CurrentPassLoads,
            new CancellationToken(canceled: true)));
    }

    private static StructuralLoadResolution Restore(StructuralLoadResolution state) =>
        StructuralLoadResolution.Restore(state.Graph, state.RemovedIds, state.Pass, state.Cursor, state.TotalEvaluations,
            state.IsComplete, state.Failures, state.CurrentPassLoads, TestContext.Current.CancellationToken);

    private static StructuralLoadGraph Cascade() => new([Element(1, 80), Foundation(2, 30), Foundation(3, 60)],
        [Bearing(10, 1, 2), Bearing(20, 1, 3)]);

    private static StructuralLoadResolution Finish(StructuralLoadResolution state, int slice = 64)
    {
        int calls = 0;
        while (!state.IsComplete)
        {
            Assert.True(calls++ <= StructuralLoadGraph.MaximumElements * (StructuralLoadGraph.MaximumElements + 1),
                "Monotonic failure passes must reach a fixed point.");
            ulong previous = state.TotalEvaluations;
            state = state.Advance(slice);
            Assert.InRange(state.TotalEvaluations - previous, 0UL, (ulong)slice);
        }
        return state;
    }

    private static void Conserves(StructuralLoadResolution final) =>
        Assert.Equal(final.Graph.TotalInputMass, final.AcceptedFoundationLoad + final.FailedInputMass);

    private static (uint Pass, UInt128 Load) FailureDetails(StructuralLoadResolution resolution, ulong identity)
    {
        StructuralFailure failure = resolution.Failures.Single(value => value.ElementId == Id(identity));
        return (failure.Pass, failure.Load);
    }

    private static EntityId Id(ulong value) => new(0x737472756374, value);
    private static StructuralElement Element(ulong identity, ulong own = 0, ulong capacity = 100) =>
        new(Id(identity), new MassMilligrams(own), new(0), new(capacity), 10_000, false);
    private static StructuralElement Foundation(ulong identity, ulong capacity, ulong own = 0) =>
        new(Id(identity), new(own), new(0), new(capacity), 10_000, true, 0);
    private static StructuralBearing Bearing(ulong identity, ulong supported, ulong supporting, ulong weight = 1) =>
        new(Id(identity), Id(supported), Id(supporting), weight);
}
