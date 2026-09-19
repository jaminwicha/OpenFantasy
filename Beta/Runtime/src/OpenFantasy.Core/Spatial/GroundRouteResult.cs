using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Spatial;

public enum GroundRouteStatus
{
    Reachable,
    Blocked,
    Unavailable,
    ExpansionLimit,
    NoGroundLocomotion,
    DifferentFloor,
    NumericLimit,
    Unaligned
}

/// <summary>Checked cardinal waypoints, excluding the start; no movement is authorized or applied.</summary>
public sealed class GroundRouteResult
{
    internal GroundRouteResult(GroundRouteStatus status, int expandedNodes = 0,
        IEnumerable<SubunitPosition>? waypoints = null, int evaluatedTransitions = 0)
    {
        Status = status;
        ExpandedNodes = expandedNodes;
        EvaluatedTransitions = evaluatedTransitions;
        Waypoints = Array.AsReadOnly((waypoints ?? []).ToArray());
    }

    public GroundRouteStatus Status { get; }
    public int ExpandedNodes { get; }
    public int EvaluatedTransitions { get; }
    public ReadOnlyCollection<SubunitPosition> Waypoints { get; }
    public SubunitPosition? NextWaypoint => Waypoints.Count == 0 ? null : Waypoints[0];
}
