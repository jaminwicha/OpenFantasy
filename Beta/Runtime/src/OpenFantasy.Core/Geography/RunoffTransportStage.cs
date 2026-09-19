using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Geography;

public enum RunoffTransportStatus { Scheduled, BlockedByHead, Completed, Canceled, BlockedByThroughput, BlockedByMaterial }

public sealed record RunoffTransportRoute(EntityId BasinId, EntityId ConnectionId);

/// <summary>One indivisible funded delivery and its retained outcome, distinct from the forcing interval.</summary>
public sealed class RunoffTransportStage
{
    internal RunoffTransportStage(EntityId stageId, EntityId reservationId, WorldInstant dueAt,
        ContentHash fundingHash, ulong units, IEnumerable<RunoffTransportRoute> routes,
        RunoffTransportStatus status = RunoffTransportStatus.Scheduled, WorldInstant? resolvedAt = null)
    {
        ArgumentNullException.ThrowIfNull(fundingHash);
        ArgumentNullException.ThrowIfNull(routes);
        var selected = routes.Take(PreparedWatershedNetwork.MaximumBasins + 1).ToArray();
        if (selected.Any(r => r is null) || selected.Length > PreparedWatershedNetwork.MaximumBasins
            || selected.Select(r => r.BasinId).Distinct().Count() != selected.Length || !Enum.IsDefined(status)
            || (status is RunoffTransportStatus.Completed or RunoffTransportStatus.Canceled) != (resolvedAt is not null)
            || status == RunoffTransportStatus.Completed && resolvedAt < dueAt)
        {
            throw new ArgumentException("A transport stage requires bounded unique routes and a consistent outcome.");
        }
        StageId = stageId;
        ReservationId = reservationId;
        DueAt = dueAt;
        FundingHash = fundingHash;
        Units = units;
        Routes = selected.OrderBy(r => r.BasinId).ToList().AsReadOnly();
        Status = status;
        ResolvedAt = resolvedAt;
    }

    public EntityId StageId { get; }
    public EntityId ReservationId { get; }
    public WorldInstant DueAt { get; }
    public ContentHash FundingHash { get; }
    public ulong Units { get; }
    public ReadOnlyCollection<RunoffTransportRoute> Routes { get; }
    public RunoffTransportStatus Status { get; }
    public WorldInstant? ResolvedAt { get; }
    public bool IsPending => Status is RunoffTransportStatus.Scheduled or RunoffTransportStatus.BlockedByHead
        or RunoffTransportStatus.BlockedByThroughput or RunoffTransportStatus.BlockedByMaterial;

    internal RunoffTransportStage WithStatus(RunoffTransportStatus status, WorldInstant? resolvedAt = null) =>
        new(StageId, ReservationId, DueAt, FundingHash, Units, Routes, status, resolvedAt);

    internal static ContentHash Fingerprint(WatershedRunoffRequest request) => ContentHash.Compute(CanonicalEncodingV1.Frame(
        "geography.runoff-funding.v1", request.ReservationId.ToByteArray(), WatershedRunoffCodecV1.EncodeInstant(request.Start),
        WatershedRunoffCodecV1.EncodeInstant(request.End), WatershedRunoffCodecV1.EncodeInputs(request.Inputs)));
}
