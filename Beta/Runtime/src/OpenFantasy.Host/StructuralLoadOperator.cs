using System.Globalization;
using System.Text.Json;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Structures;

namespace OpenFantasy.Host;

/// <summary>Resumes a standalone immutable failure plan without authenticating policy or publishing world changes.</summary>
internal sealed class StructuralLoadOperator
{
    internal string Inspect(string checkpointPath, string graphPath, string dependencyPinText)
    {
        var dependencyPin = new ContentHash(dependencyPinText);
        byte[] graphBytes = ReadBoundedFile(graphPath);
        StructuralLoadGraph graph = StructuralLoadCodec.DecodeGraph(graphBytes);
        byte[] checkpointBytes = ReadBoundedFile(checkpointPath);
        StructuralLoadResolution initial = StructuralLoadCodec.Decode(checkpointBytes, dependencyPin, graph);
        StructuralLoadResolution completed = initial;
        while (!completed.IsComplete)
        {
            completed = completed.Advance();
        }
        byte[] completedArchive = StructuralLoadCodec.Encode(completed, dependencyPin);
        return JsonSerializer.Serialize(new
        {
            Contract = "structural-load-resolution-v1",
            Scope = "standalone-failure-plan-diagnostic",
            Authority = "Caller-supplied graph and dependency pin are not authenticated world policy.",
            WorldConsequencesPublished = false,
            InputCheckpointHash = ContentHash.Compute(checkpointBytes).Value,
            ElementCount = graph.Elements.Count,
            BearingCount = graph.Bearings.Count,
            Initial = new
            {
                initial.Pass,
                initial.Cursor,
                initial.TotalEvaluations,
                initial.IsComplete
            },
            Result = new
            {
                GraphHash = ContentHash.Compute(graphBytes).Value,
                DependencyPin = dependencyPin.Value,
                CompletedArchiveHash = ContentHash.Compute(completedArchive).Value,
                completed.Pass,
                completed.Cursor,
                completed.TotalEvaluations,
                completed.IsComplete,
                AdditionalEvaluations = completed.TotalEvaluations - initial.TotalEvaluations
            },
            Conservation = new
            {
                TotalInputMassMilligrams = Mass(graph.TotalInputMass),
                AcceptedFoundationLoadMilligrams = Mass(completed.AcceptedFoundationLoad),
                FailedInputMassMilligrams = Mass(completed.FailedInputMass),
                AccountedInputMassMilligrams = Mass(completed.AcceptedFoundationLoad + completed.FailedInputMass)
            },
            Failures = completed.Failures.Select(failure => new
            {
                ElementId = failure.ElementId.ToString(),
                Reason = failure.Reason.ToString(),
                LoadMilligrams = Mass(failure.Load),
                failure.Pass
            }).ToArray(),
            ActiveLoads = completed.ActiveLoads.Select(load => new
            {
                ElementId = load.Key.ToString(),
                LoadMilligrams = Mass(load.Value)
            }).ToArray()
        });
    }

    private static string Mass(UInt128 value) => value.ToString(CultureInfo.InvariantCulture);

    private static byte[] ReadBoundedFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        long length = stream.Length;
        if (length > StructuralLoadCodec.MaximumArchiveBytes)
        {
            throw new InvalidDataException("Structural inspection input exceeds its byte limit.");
        }
        byte[] bytes = new byte[checked((int)length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1)
        {
            throw new InvalidDataException("Structural inspection input changed length while reading.");
        }
        return bytes;
    }
}
