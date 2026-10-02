using System.Collections;
using System.Collections.ObjectModel;
using System.Text.Json;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Tests.Networking.PlayerObservationViews;

public sealed class ObservationOccupancyValidationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(PlayerViewCodec.MaximumCollectionItems, true)]
    [InlineData(PlayerViewCodec.MaximumCollectionItems + 1, false)]
    [InlineData(PlayerViewCodec.MaximumCollectionItems * 4, false)]
    public void Collection_limits_match_generic_boolean_traversal(int cells, bool accepted)
    {
        AssertEquivalent(Occupancy(cells, readOnly: true), Occupancy(cells, readOnly: false), accepted);
    }

    [Theory]
    [InlineData(PlayerViewCodec.MaximumDepth - 1, 1, true)]
    [InlineData(PlayerViewCodec.MaximumDepth, 1, false)]
    [InlineData(PlayerViewCodec.MaximumDepth, 0, true)]
    [InlineData(PlayerViewCodec.MaximumDepth + 1, 0, false)]
    [InlineData(PlayerViewCodec.MaximumDepth, PlayerViewCodec.MaximumCollectionItems + 1, false)]
    public void Child_depth_and_empty_collection_depth_match_generic_traversal(int collectionDepth, int cells, bool accepted)
    {
        AssertEquivalent(Nested(Occupancy(cells, readOnly: true), collectionDepth),
            Nested(Occupancy(cells, readOnly: false), collectionDepth), accepted);
    }

    [Theory]
    [InlineData(PlayerViewCodec.MaximumBodyBytes - 1, true)]
    [InlineData(PlayerViewCodec.MaximumBodyBytes, true)]
    [InlineData(PlayerViewCodec.MaximumBodyBytes + 1, false)]
    public void All_boolean_cells_and_collection_owners_consume_the_shared_value_budget(int values, bool accepted)
    {
        // Includes the root collection, every child collection and every boolean cell. No one
        // collection reaches its size limit early, so only the shared traversal budget can reject.
        AssertEquivalent(CountedGraph(values, readOnly: true), CountedGraph(values, readOnly: false), accepted);
    }

    [Theory]
    [InlineData(PlayerViewCodec.MaximumCollectionItems - 1, "Observation values exceed their traversal bound.")]
    [InlineData(PlayerViewCodec.MaximumCollectionItems, "An observation collection is oversized or contains null.")]
    public void Competing_global_and_collection_budgets_preserve_the_first_generic_failure(int remainingValues, string message)
    {
        object Graph(bool readOnly) => new object[]
        {
            // Reserve one value for the outer root and one for the oversized collection's owner.
            CountedGraph(PlayerViewCodec.MaximumBodyBytes - remainingValues - 2, readOnly),
            Occupancy(PlayerViewCodec.MaximumCollectionItems + 1, readOnly)
        };

        object generic = Graph(readOnly: false);
        FormatException reference = Assert.Throws<FormatException>(() => ObservationValidation.ValidateValues(generic));
        Assert.Equal(message, reference.Message);
        AssertEquivalent(Graph(readOnly: true), generic, accepted: false);
    }

    [Theory]
    [InlineData("enum")]
    [InlineData("required-null")]
    [InlineData("collection-null")]
    public void Bulk_occupancy_validation_does_not_skip_invalid_siblings(string invalid)
    {
        object Graph(bool readOnly)
        {
            var siblings = new Siblings(Occupancy(32, readOnly),
                invalid == "enum" ? (SampleKind)99 : SampleKind.Valid,
                invalid == "required-null" ? null! : "present");
            return invalid == "collection-null" ? new object[] { siblings, null! } : siblings;
        }

        AssertEquivalent(Graph(readOnly: true), Graph(readOnly: false), accepted: false);
    }

    [Fact]
    public void Readonly_boolean_validation_eliminates_per_cell_boxing_after_warmup()
    {
        const int iterations = 8;
        object readOnly = Occupancy(PlayerViewCodec.MaximumCollectionItems, readOnly: true);
        object generic = Occupancy(PlayerViewCodec.MaximumCollectionItems, readOnly: false);
        for (int iteration = 0; iteration < 4; iteration++)
        {
            ObservationValidation.ValidateValues(readOnly);
            ObservationValidation.ValidateValues(generic);
        }

        long readOnlyBytes = AllocatedBytes(readOnly, iterations);
        long genericBytes = AllocatedBytes(generic, iterations);
        long cells = (long)PlayerViewCodec.MaximumCollectionItems * iterations;
        output.WriteLine($"ValidatedCells={cells}; OwnedOccupancyAllocatedBytes={readOnlyBytes}; GenericBooleanArrayAllocatedBytes={genericBytes}");
        Assert.True(genericBytes >= cells * 16,
            $"The generic control must exercise per-cell boxing; observed {genericBytes} bytes for {cells} cells.");
        Assert.True(readOnlyBytes <= genericBytes / 20,
            $"Typed readonly occupancy allocated {readOnlyBytes} bytes versus generic traversal's {genericBytes} bytes.");
    }

    [Fact]
    public void Arbitrary_readonly_boolean_wrappers_still_validate_their_actual_enumeration()
    {
        var values = new ReadOnlyCollection<bool>(new DeceptiveBooleans());
        // Test the claimed count separately from the deliberately inconsistent enumeration.
        int claimedCount = values.Count;
        Assert.Equal(1, claimedCount);
        Assert.True(values[0]);
        FormatException error = Assert.Throws<FormatException>(() => ObservationValidation.ValidateValues(values));
        Assert.Equal("An observation collection is oversized or contains null.", error.Message);
    }

    [Fact]
    public void Terrain_occupancy_defensively_copies_cells_and_retains_declared_property_and_wire_types()
    {
        bool[] cells = [true, false, true, false, true, false, true, false, true, false];
        ReadOnlyCollection<bool> original = Array.AsReadOnly(cells);
        var bounds = new TerrainRegionBounds(new CellCoordinate(0, 0, 0), cells.Length, 1, 1);
        var worksite = new LocalWorksiteTerrainView(bounds, 3, original);
        var liquid = new LocalLiquidTerrainView(bounds, 3, original);
        Assert.IsType<LocalTerrainOccupancy>(worksite.Occupancy);
        Assert.IsType<LocalTerrainOccupancy>(liquid.Occupancy);
        Assert.Equal(typeof(ReadOnlyCollection<bool>), typeof(LocalWorksiteTerrainView).GetProperty("Occupancy")!.PropertyType);
        Assert.Equal(typeof(ReadOnlyCollection<bool>), typeof(LocalLiquidTerrainView).GetProperty("Occupancy")!.PropertyType);

        JsonSerializerOptions options = PlayerViewCodec.CreateOptions();
        byte[] expected = JsonSerializer.SerializeToUtf8Bytes(original, options);
        Assert.Equal(expected, JsonSerializer.SerializeToUtf8Bytes(worksite.Occupancy, options));
        Assert.Equal(expected, JsonSerializer.SerializeToUtf8Bytes(liquid.Occupancy, options));
        Assert.Equal("{\"Count\":10,\"Bits\":\"VQE=\"}", System.Text.Encoding.UTF8.GetString(expected));
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(worksite.Occupancy));
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(liquid.Occupancy));
        cells[0] = false;
        Assert.True(worksite.Occupancy[0]);
        Assert.True(liquid.Occupancy[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<bool>)worksite.Occupancy)[0] = false);
        Assert.Throws<NotSupportedException>(() => ((IList<bool>)liquid.Occupancy)[0] = false);
    }

    private static long AllocatedBytes(object value, int iterations)
    {
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < iterations; iteration++)
            ObservationValidation.ValidateValues(value);
        return GC.GetAllocatedBytesForCurrentThread() - start;
    }

    private static object Occupancy(int cells, bool readOnly)
    {
        bool[] values = new bool[cells];
        for (int index = 0; index < values.Length; index++) values[index] = index % 2 == 0;
        return readOnly ? new LocalTerrainOccupancy(values) : values;
    }

    private static object Nested(object value, int depth)
    {
        for (int index = 0; index < depth; index++) value = new object[] { value };
        return value;
    }

    private static object CountedGraph(int totalValues, bool readOnly)
    {
        var children = new List<object>();
        int remaining = totalValues - 1; // The root collection itself consumes one value.
        while (remaining > 0)
        {
            int cells = Math.Min(PlayerViewCodec.MaximumCollectionItems, remaining - 1);
            children.Add(Occupancy(cells, readOnly));
            remaining -= cells + 1;
        }
        return children.ToArray();
    }

    private static void AssertEquivalent(object readOnly, object generic, bool accepted)
    {
        Exception? referenceError = Record.Exception(() => ObservationValidation.ValidateValues(generic));
        Exception? optimizedError = Record.Exception(() => ObservationValidation.ValidateValues(readOnly));
        if (accepted)
        {
            Assert.Null(referenceError);
            Assert.Null(optimizedError);
        }
        else
        {
            var reference = Assert.IsType<FormatException>(referenceError);
            var optimized = Assert.IsType<FormatException>(optimizedError);
            Assert.Equal(reference.Message, optimized.Message);
        }
    }

    private enum SampleKind { Valid }
    private sealed record Siblings(object Occupancy, SampleKind Kind, string RequiredText);

    private sealed class DeceptiveBooleans : List<bool>, IEnumerable
    {
        internal DeceptiveBooleans() : base([true]) { }
        IEnumerator IEnumerable.GetEnumerator()
        {
            yield return null;
        }
    }
}
