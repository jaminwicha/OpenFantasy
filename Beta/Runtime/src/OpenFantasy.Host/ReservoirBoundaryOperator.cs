using System.Globalization;
using System.Text.Json;
using OpenFantasy.Core.Geography;
using OpenFantasy.Hosting;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Host;

/// <summary>Read-only physical boundary assessment pinned to an exact recorded world and external policy.</summary>
internal sealed class ReservoirBoundaryOperator
{
    internal string Inspect(string databasePath, string commitText, string policyPath)
    {
        if (!ulong.TryParse(commitText, NumberStyles.None, CultureInfo.InvariantCulture, out ulong revision))
        {
            throw new ArgumentException("The recorded commit must be an unsigned decimal integer.", nameof(commitText));
        }
        ReservoirSealPolicy policy = LocalAuthorityFiles.ReadReservoirSealPolicy(policyPath);
        RecordedWorldState recorded = RecordedWorldReader.ReadForHost(databasePath, revision);
        var assessment = new ReservoirBoundaryEvaluator().Assess(recorded.State, policy);
        return JsonSerializer.Serialize(new
        {
            Contract = "reservoir-boundary-assessment-v1",
            WorldId = recorded.State.WorldId.ToString(),
            BranchId = recorded.State.BranchId.ToString(),
            recorded.SourceHeadRevision,
            SourceHeadHash = recorded.SourceHeadHash.Value,
            RecordedRevision = recorded.State.Revision,
            StateHash = recorded.StateHash.Value,
            recorded.State.ProfileVersion,
            WorldHash = assessment.WorldHash.Value,
            ReservoirHash = assessment.ReservoirHash.Value,
            PolicyHash = assessment.PolicyHash.Value,
            assessment.Completed,
            assessment.IsContained,
            Results = assessment.Results.Select(result => new
            {
                ReservoirId = result.ReservoirId.ToString(),
                result.Completed,
                result.IsContained,
                result.InteriorCells,
                result.BoundaryFaces,
                result.TerrainSealedFaces,
                result.ConstructedSealedFaces,
                Failures = result.Failures.ToDictionary(failure => failure.Key.ToString(), failure => failure.Value, StringComparer.Ordinal),
                Samples = result.Samples.Select(sample => new
                {
                    Failure = sample.Failure.ToString(),
                    sample.Cell,
                    Surface = sample.Surface.ToString()
                }).ToArray()
            }).ToArray()
        });
    }
}
