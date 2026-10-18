using System.Collections.ObjectModel;
using OpenFantasy.Core.Exposure;

namespace OpenFantasy.Core.Geography;

/// <summary>Due retained stages for one exact transport boundary, including previously blocked stages.</summary>
public sealed class ReservoirTransportDiscovery
{
    private readonly ReservoirTransportState _source;

    internal ReservoirTransportDiscovery(ReservoirTransportState source, WorldInstant boundary)
    {
        if (boundary != source.Boundary
            && boundary != source.Boundary.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep))
        {
            throw new ArgumentException("Transport discovery requires its current or next exact boundary.", nameof(boundary));
        }
        _source = source;
        Boundary = boundary;
        Stages = Array.AsReadOnly(source.Stages.Where(stage => stage.IsPending && stage.DueAt <= boundary)
            .OrderBy(stage => stage.DueAt).ThenBy(stage => stage.StageId).ToArray());
    }

    public WorldInstant Boundary { get; }
    public ReadOnlyCollection<RunoffTransportStage> Stages { get; }

    /// <summary>Reference affinity to the complete immutable source; stages remain subject to physical admission.</summary>
    public bool IsFor(ReservoirTransportState source) => ReferenceEquals(_source, source);
}
