using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Worlds;

/// <summary>One gathering node's issue cursor at the admitted genesis.</summary>
public sealed record WorldSourceCursor(EntityId NodeId, EntityId SourceId, ulong NextUnissuedUnit);

/// <summary>
/// Actual raw rights admitted at the coordinated genesis: the allocated source intervals, every
/// gathering cursor and the sites already extracted. Later issuance is read from the actual gathering
/// and excavation owners. Source capacity is a bound, never a grant.
/// </summary>
public sealed class WorldSourceIssuanceBaseline
{
    public const int MaximumAllocations = CustodySnapshot.MaximumLots;
    public const int MaximumCursors = 256;
    public const int MaximumExtractedSources = 256;
    public const int MaximumEncodedByteLength = 512 * 1024;
    private const int FieldsPerInterval = 3;
    private readonly byte[] _bytes;

    private WorldSourceIssuanceBaseline(IEnumerable<SourceInterval> allocations, IEnumerable<WorldSourceCursor> cursors,
        IEnumerable<EntityId> extractedSources)
    {
        Allocations = Array.AsReadOnly(Normalize(allocations, "Admitted genesis allocations must be disjoint."));
        Cursors = Array.AsReadOnly(cursors.OrderBy(value => value.NodeId).ToArray());
        ExtractedSources = Array.AsReadOnly(extractedSources.Order().ToArray());
        if (Allocations.Count > MaximumAllocations || Cursors.Count > MaximumCursors
            || ExtractedSources.Count > MaximumExtractedSources
            || Cursors.Select(value => value.NodeId).Distinct().Count() != Cursors.Count
            || Cursors.Select(value => value.SourceId).Distinct().Count() != Cursors.Count
            || ExtractedSources.Distinct().Count() != ExtractedSources.Count)
        {
            throw new ArgumentException("The admitted source baseline requires bounded unique cursors and sites.");
        }
        _bytes = Compute();
        if (_bytes.Length > MaximumEncodedByteLength)
        {
            throw new ArgumentException("The admitted source baseline exceeds its encoded byte bound.");
        }
        ContentHash = ContentHash.Compute(_bytes);
    }

    /// <summary>Merged only where contiguous; original holes remain holes.</summary>
    public ReadOnlyCollection<SourceInterval> Allocations { get; }
    public ReadOnlyCollection<WorldSourceCursor> Cursors { get; }
    public ReadOnlyCollection<EntityId> ExtractedSources { get; }
    public ContentHash ContentHash { get; }

    public byte[] Encode() => (byte[])_bytes.Clone();

    internal static WorldSourceIssuanceBaseline Capture(WorldSnapshot genesis)
    {
        var baseline = new WorldSourceIssuanceBaseline(genesis.Inventory.Custody.Lots.Select(value => value.Source),
            genesis.Gathering!.Nodes.Select(value => new WorldSourceCursor(value.NodeId, value.SourceId, value.NextUnissuedUnit)),
            genesis.Excavation!.Sites.Where(value => value.IsComplete).Select(value => value.CountedSourceId));
        if (baseline.Cursors.Any(cursor => baseline.Allocations.Any(value =>
                value.SourceId == cursor.SourceId && value.End > cursor.NextUnissuedUnit)))
        {
            throw new ArgumentException("A genesis allocation cannot lie beyond its gathering source cursor.");
        }
        return baseline;
    }

    public static WorldSourceIssuanceBaseline Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            int maximumFields = 8 + FieldsPerInterval * (MaximumAllocations + MaximumCursors) + MaximumExtractedSources;
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, maximumFields);
            reader.Tag("world.source-issuance.v1"u8);
            List<SourceInterval> allocations = [];
            for (ulong count = Bounded(reader.Number(), MaximumAllocations); count != 0; count--)
            {
                allocations.Add(new(reader.Identity(), reader.Number(), reader.Number()));
            }
            List<WorldSourceCursor> cursors = [];
            for (ulong count = Bounded(reader.Number(), MaximumCursors); count != 0; count--)
            {
                cursors.Add(new(reader.Identity(), reader.Identity(), reader.Number()));
            }
            List<EntityId> extracted = [];
            for (ulong count = Bounded(reader.Number(), MaximumExtractedSources); count != 0; count--)
            {
                extracted.Add(reader.Identity());
            }
            reader.Complete();
            var restored = new WorldSourceIssuanceBaseline(allocations, cursors, extracted);
            if (!restored._bytes.AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The admitted source baseline is not canonical.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The admitted source baseline violates its issuance contract.", error);
        }
    }

    /// <summary>
    /// Archived direct raw retirement, active direct raw retirement and current raw rights must be
    /// disjoint and together equal the rights actually issued. A cursor bound alone is not coverage.
    /// </summary>
    internal void ValidatePartition(WorldSnapshot world)
    {
        ProductionSnapshot production = world.Production!;
        IEnumerable<SourceInterval> claimed = world.Inventory.Custody.Lots.Select(value => value.Source)
            .Concat(ProductionRetirementHistory.CurrentRetirements(production)
                .Where(value => value.Ingredient.Source is not null).Select(value => value.Ingredient.Source!))
            .Concat(production.Continuation?.Basis.RawRetirements.Select(value => value.Source) ?? []);
        SourceInterval[] claims = Normalize(claimed, "Live raw rights and direct raw retirement must be disjoint.");
        SourceInterval[] issued = Normalize(Issued(world), "Admitted source issuance must be disjoint.");
        if (claims.Length != issued.Length || claims.Where((value, index) => !Same(value, issued[index])).Any()
            || issued.Any(value => !world.Inventory.Sources.Any(source =>
                source.SourceId == value.SourceId && value.End <= source.UnitCapacity)))
        {
            throw new ArgumentException("Archived and active direct raw retirement plus current raw rights must exactly equal the admitted issued rights.");
        }
    }

    private IEnumerable<SourceInterval> Issued(WorldSnapshot world)
    {
        foreach (SourceInterval allocation in Allocations)
        {
            yield return allocation;
        }
        var nodes = world.Gathering!.Nodes;
        if (nodes.Count != Cursors.Count)
        {
            throw new ArgumentException("The coordinated world retains exactly its admitted gathering sources.");
        }
        foreach (var node in nodes)
        {
            WorldSourceCursor? cursor = Cursors.FirstOrDefault(value => value.NodeId == node.NodeId);
            if (cursor is null || cursor.SourceId != node.SourceId || node.NextUnissuedUnit < cursor.NextUnissuedUnit)
            {
                throw new ArgumentException("A gathering source must retain its admitted monotonic issue cursor.");
            }
            if (node.NextUnissuedUnit != cursor.NextUnissuedUnit)
            {
                yield return new SourceInterval(node.SourceId, cursor.NextUnissuedUnit, node.NextUnissuedUnit);
            }
        }
        foreach (var site in world.Excavation!.Sites)
        {
            bool extractedAtGenesis = ExtractedSources.Contains(site.CountedSourceId);
            if (extractedAtGenesis && !site.IsComplete)
            {
                throw new ArgumentException("An extracted site cannot return to unissued terrain.");
            }
            if (site.IsComplete && !extractedAtGenesis)
            {
                yield return new SourceInterval(site.CountedSourceId, 0, site.Quantity);
            }
        }
    }

    private static SourceInterval[] Normalize(IEnumerable<SourceInterval> intervals, string overlap)
    {
        List<SourceInterval> merged = [];
        foreach (SourceInterval next in intervals.OrderBy(value => value.SourceId).ThenBy(value => value.Start))
        {
            SourceInterval? previous = merged.Count == 0 ? null : merged[^1];
            if (previous is null || previous.SourceId != next.SourceId)
            {
                merged.Add(next);
                continue;
            }
            if (next.Start < previous.End)
            {
                throw new ArgumentException(overlap);
            }
            if (next.Start == previous.End)
            {
                merged[^1] = new(previous.SourceId, previous.Start, next.End);
                continue;
            }
            merged.Add(next);
        }
        return merged.ToArray();
    }

    private static bool Same(SourceInterval left, SourceInterval right) =>
        left.SourceId == right.SourceId && left.Start == right.Start && left.End == right.End;

    private static ulong Bounded(ulong count, int maximum) => count <= (ulong)maximum ? count
        : throw new FormatException("The admitted source baseline exceeds its finite bound.");

    private byte[] Compute()
    {
        List<ReadOnlyMemory<byte>> fields = [Number((ulong)Allocations.Count)];
        foreach (SourceInterval value in Allocations)
        {
            fields.Add(value.SourceId.ToByteArray());
            fields.Add(Number(value.Start));
            fields.Add(Number(value.End));
        }
        fields.Add(Number((ulong)Cursors.Count));
        foreach (WorldSourceCursor value in Cursors)
        {
            fields.Add(value.NodeId.ToByteArray());
            fields.Add(value.SourceId.ToByteArray());
            fields.Add(Number(value.NextUnissuedUnit));
        }
        fields.Add(Number((ulong)ExtractedSources.Count));
        foreach (EntityId value in ExtractedSources)
        {
            fields.Add(value.ToByteArray());
        }
        return CanonicalEncodingV1.Frame("world.source-issuance.v1", fields.ToArray());
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
}
