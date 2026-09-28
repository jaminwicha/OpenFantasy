using System.Text;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Production;

/// <summary>Bounded encode-only structural observation; no decode path grants archived material rights.</summary>
internal static class ProductionContinuationBasisEncoding
{
    internal static byte[] Encode(ProductionContinuationBasis basis) => Frame("production.continuation-basis", Number(1),
        basis.WorldId.ToByteArray(), basis.BranchId.ToByteArray(), basis.InventoryHash.ToByteArray(),
        basis.ProductionHash.ToByteArray(), Number(basis.ProductionRevision), Number(basis.CustodyRevision),
        Collection("continuation.raw-lots.v1", basis.RawLots.Select(RawLot), ProductionContinuationBasis.MaximumLiveLots),
        Collection("continuation.transformed-lots.v1", basis.TransformedLots.Select(ProductionCodecV1.EncodeLot),
            ProductionContinuationBasis.MaximumLiveLots),
        Collection("continuation.raw-retirements.v1", basis.RawRetirements.Select(RawRetirement),
            ProductionContinuationBasis.MaximumRawRetirementRanges),
        Collection("continuation.output-budgets.v1", basis.OutputBudgets.Select(OutputBudget),
            ProductionContinuationBasis.MaximumOutputBudgets),
        Collection("continuation.batch-witnesses.v1", basis.BatchWitnesses.Select(BatchWitness),
            ProductionContinuationBasis.MaximumBatchWitnesses));

    private static byte[] RawLot(CountedSourceLot lot) => Frame("continuation.raw-lot.v1", lot.LotId.ToByteArray(),
        lot.CustodianId.ToByteArray(), lot.Definition.DefinitionId.ToByteArray(), lot.Definition.Version.ToByteArray(),
        lot.Source.SourceId.ToByteArray(), Number(lot.Source.Start), Number(lot.Source.End), Number(lot.Revision),
        Number((ulong)lot.Protection), lot.ReservedBy?.ToByteArray() ?? []);

    private static byte[] RawRetirement(DirectRawRetirementRange value) => Frame("continuation.raw-retirement.v1",
        value.CustodianId.ToByteArray(), value.Definition.DefinitionId.ToByteArray(), value.Definition.Version.ToByteArray(),
        value.Source.SourceId.ToByteArray(), Number(value.Source.Start), Number(value.Source.End));

    private static byte[] OutputBudget(ImportedOutputBudget value) => Frame("continuation.output-budget.v1",
        value.BatchId.ToByteArray(), Number(value.OutputIndex), value.Definition.DefinitionId.ToByteArray(),
        value.Definition.Version.ToByteArray(), Collection("continuation.output-intervals.v1",
            value.Intervals.Select(interval => Frame("continuation.output-interval.v1", interval.BatchId.ToByteArray(),
                Number(interval.OutputIndex), Number(interval.Start), Number(interval.End))),
            ProductionContinuationBasis.MaximumLiveLots));

    private static byte[] BatchWitness(ProductionBatchWitness value) => Frame("continuation.batch-witness.v1",
        value.BatchId.ToByteArray(), value.CommandId.ToByteArray(), value.ActorId.ToByteArray(), value.CustodianId.ToByteArray(),
        value.RecipeId.ToByteArray(), value.RecipeVersion.ToByteArray(), value.CanonicalBatchHash.ToByteArray());

    private static byte[] Collection(string tag, IEnumerable<byte[]> entries, int maximumCount)
    {
        List<ReadOnlyMemory<byte>> fields = [];
        long length = HeaderLength(tag);
        foreach (byte[] entry in entries)
        {
            if (fields.Count == maximumCount)
                throw new ArgumentException("The continuation observation collection exceeds its count envelope.");
            length = checked(length + sizeof(uint) + entry.Length);
            RequireLength(length);
            fields.Add(entry);
        }
        return Frame(tag, fields.ToArray());
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);

    private static byte[] Frame(string tag, params ReadOnlyMemory<byte>[] fields)
    {
        long length = HeaderLength(tag);
        foreach (var field in fields)
        {
            length = checked(length + sizeof(uint) + field.Length);
            RequireLength(length);
        }
        return CanonicalEncodingV1.Frame(tag, fields);
    }

    private static long HeaderLength(string tag) => 4 * sizeof(uint) + Encoding.UTF8.GetByteCount(tag);

    private static void RequireLength(long length)
    {
        if (length > ProductionContinuationBasis.MaximumEncodedByteLength)
            throw new ArgumentException("The continuation observation exceeds its encoded byte envelope.");
    }
}
