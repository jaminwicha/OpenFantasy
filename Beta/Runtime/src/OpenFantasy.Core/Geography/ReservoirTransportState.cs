using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Geography;

/// <summary>Exclusive physical ownership, authored closed gates and a finite deterministic stage journal.</summary>
public sealed class ReservoirTransportState
{
    public const int MaximumConnections = 256;
    public const int MaximumStages = WatershedRunoffCapacityState.MaximumClosed;
    private readonly Lazy<ContentHash> _contentHash;

    internal ReservoirTransportState(LiquidReservoirState reservoirs, IEnumerable<ReservoirConnection> connections,
        IEnumerable<RunoffTransportStage> stages, ulong revision, WorldInstant boundary,
        TransportMaterialContinuation? continuation = null)
    {
        ArgumentNullException.ThrowIfNull(reservoirs);
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(stages);
        var gates = connections.Take(MaximumConnections + 1).ToArray();
        var journal = stages.Take(MaximumStages + 1).ToArray();
        if (gates.Length is 0 or > MaximumConnections || gates.Any(c => c is null)
            || journal.Length > MaximumStages || journal.Any(s => s is null))
        {
            throw new ArgumentException("The transport catalog and stage journal must remain finite.");
        }
        Reservoirs = reservoirs;
        Connections = gates.OrderBy(c => c.ConnectionId).ToList().AsReadOnly();
        Stages = journal.OrderBy(s => s.StageId).ToList().AsReadOnly();
        Revision = revision;
        Boundary = boundary;
        ContinuationQualification = continuation;
        if (reservoirs.Capacity.Production?.IsContinued == true)
        {
            if (reservoirs.Capacity.Production.Continuation?.SupportsReservedTransport != true || continuation is null)
                throw new ArgumentException("Continued transport requires an actual complete-owner V7 cut.", nameof(reservoirs));
            continuation.Validate(this);
        }
        else if (continuation is not null)
            throw new ArgumentException("Transport qualification cannot replace its explicit material interpretation.");
        Validate();
        _contentHash = new(() => ContentHash.Compute(Reservoirs.Capacity.Production?.IsContinued == true ? ReservoirTransportCodecV3.Encode(this)
            : Reservoirs.Capacity.Production?.SupportsConsumption == true
            ? ReservoirTransportCodecV2.Encode(this) : ReservoirTransportCodecV1.Encode(this)));
    }

    public LiquidReservoirState Reservoirs { get; }
    public ReadOnlyCollection<ReservoirConnection> Connections { get; }
    public ReadOnlyCollection<RunoffTransportStage> Stages { get; }
    public ulong Revision { get; }
    public WorldInstant Boundary { get; }
    public ContentHash ContentHash => _contentHash.Value;
    internal TransportMaterialContinuation? ContinuationQualification { get; }

    public static ReservoirTransportState Create(LiquidReservoirState reservoirs, IEnumerable<ReservoirConnection> connections,
        WorldInstant boundary) => new(reservoirs, connections, [], 0, boundary);

    public ReservoirTransportPlan PlanSchedule(EntityId stageId, EntityId reservationId, WorldInstant dueAt,
        IEnumerable<RunoffTransportRoute> routes, Func<EntityId, bool>? historicallyOccupied = null)
    {
        if (Reservoirs.Capacity.Production?.IsContinued == true)
        {
            ArgumentNullException.ThrowIfNull(historicallyOccupied);
            EntityId cause = ReservoirTransportEvidence.DeliveryCause(stageId);
            if (ContainsIdentity(stageId) || historicallyOccupied(stageId)
                || cause == stageId || ContainsIdentity(cause) || historicallyOccupied(cause))
                throw new ArgumentException("A continued schedule requires fresh stage and delivery identities.");
        }
        var runoff = Reservoirs.Capacity.Pending.SingleOrDefault(p => p.Request.ReservationId == reservationId)
            ?? throw new ArgumentException("A transport stage requires an active funded commitment.");
        if (dueAt < runoff.Request.End || dueAt < Boundary.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep))
        {
            throw new ArgumentException("Arrival must be explicit, after forcing ends, and no earlier than the next step boundary.");
        }
        var stage = new RunoffTransportStage(stageId, reservationId, dueAt, RunoffTransportStage.Fingerprint(runoff.Request),
            runoff.TotalInputUnits, routes);
        return new(this, new(Reservoirs, Connections, Stages.Append(stage), checked(Revision + 1), Boundary, ContinuationQualification), [stageId]);
    }

    /// <summary>Explicitly selects reserved transport continuation from this actual complete owner.</summary>
    public ReservoirTransportPlan PlanContinueTransport(GatheringSnapshot? gathering = null,
        MaterialConstructionState? construction = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ProductionSnapshot production = Reservoirs.Capacity.Production
            ?? throw new ArgumentException("Reserved transport continuation requires an explicit production owner.");
        var next = production.ContinueTransport(Reservoirs.Capacity.Inventory, TransportMaterialBoundary.Capture(this), construction, gathering);
        var capacityPlan = Reservoirs.Capacity.PlanContinuation(next, cancellationToken);
        var physical = Reservoirs.PlanApply(capacityPlan).ApplyTo(Reservoirs);
        var qualification = TransportMaterialContinuation.Capture(this, next);
        var candidate = new ReservoirTransportState(physical, Connections, Stages, Revision, Boundary, qualification);
        cancellationToken.ThrowIfCancellationRequested();
        return new(this, candidate);
    }

    /// <summary>Closes the current qualified material segment without changing stage, physical or calendar facts.</summary>
    public ReservoirTransportPlan PlanContinue(CancellationToken cancellationToken = default)
    {
        if (Reservoirs.Capacity.Production?.Continuation?.SupportsReservedTransport != true)
            throw new ArgumentException("Select reserved transport continuation explicitly before retaining further cuts.");
        return PlanContinueTransport(cancellationToken: cancellationToken);
    }

    /// <summary>Applies an exact physical-owner plan while preserving every scheduled funding obligation.</summary>
    public ReservoirTransportPlan PlanApply(LiquidReservoirPlan physicalPlan)
    {
        ArgumentNullException.ThrowIfNull(physicalPlan);
        return PlanRebind(physicalPlan.ApplyTo(Reservoirs));
    }

    internal ReservoirTransportPlan PlanRebind(LiquidReservoirState reservoirs)
    {
        if (reservoirs.ContentHash == Reservoirs.ContentHash)
        {
            return new(this, this);
        }
        var prior = Reservoirs.Capacity;
        var next = reservoirs.Capacity;
        if (next.Domain.ContentHash != prior.Domain.ContentHash || next.Revision <= prior.Revision
            || !Reservoirs.Geometries.Select(g => g.ContentHash).SequenceEqual(reservoirs.Geometries.Select(g => g.ContentHash))
            || prior.Closed.Any(id => !next.Closed.Contains(id))
            || prior.Pending.Any(p => !next.Closed.Contains(p.Request.ReservationId)
                && !next.Pending.Any(n => n.Request.ReservationId == p.Request.ReservationId
                    && RunoffTransportStage.Fingerprint(n.Request) == RunoffTransportStage.Fingerprint(p.Request))))
        {
            throw new ArgumentException("Transport rebinding preserves fixed domain, geometry and all retained commitment history.");
        }
        return new(this, new(reservoirs, Connections, Stages, checked(Revision + 1), Boundary, ContinuationQualification));
    }

    public ReservoirTransportPlan PlanCancel(EntityId reservationId)
    {
        var capacity = Reservoirs.Capacity.PlanCancel(reservationId);
        var physical = Reservoirs.PlanApply(capacity).ApplyTo(Reservoirs);
        var journal = Stages.Select(s => s.ReservationId == reservationId && s.IsPending
            ? s.WithStatus(RunoffTransportStatus.Canceled, Boundary) : s);
        return new(this, new(physical, Connections, journal, checked(Revision + 1), Boundary, ContinuationQualification));
    }

    /// <summary>Discovers pending due stages without moving stock, consuming throughput or advancing time.</summary>
    public ReservoirTransportDiscovery DiscoverDue(WorldInstant boundary) => new(this, boundary);

    public ReservoirTransportPlan PlanAdvance(WorldInstant boundary, bool readingHold = false,
        Func<EntityId, bool>? historicallyOccupied = null) =>
        new ReservoirTransportPlanner().Advance(this, boundary, readingHold, historicallyOccupied);

    internal bool ContainsIdentity(EntityId identity) => Reservoirs.Capacity.Inventory.ContainsIdentity(identity)
        || Reservoirs.Capacity.Domain.Network.MaterialIdentities.Contains(identity)
        || Reservoirs.Capacity.Domain.Material.Physical.DefinitionId == identity
        || Reservoirs.Capacity.Domain.Material.Item.DefinitionId == identity
        || (Reservoirs.Capacity.Production?.ContainsIdentity(identity) ?? false)
        || Reservoirs.Geometries.Any(g => g.ReservoirId == identity)
        || Connections.Any(c => c.ConnectionId == identity) || Stages.Any(s => s.StageId == identity);

    private void Validate()
    {
        var owned = Connections.Select(c => c.ConnectionId).Concat(Stages.Select(s => s.StageId)).ToArray();
        if (owned.Distinct().Count() != owned.Length || Stages.Select(s => s.ReservationId).Distinct().Count() != Stages.Count)
        {
            throw new ArgumentException("Gate and stage identities are unique; a commitment can have only one retained transport outcome.");
        }
        foreach (var id in owned)
        {
            if (Reservoirs.Capacity.Inventory.ContainsIdentity(id) || Reservoirs.Capacity.Domain.Network.MaterialIdentities.Contains(id)
                || Reservoirs.Capacity.Domain.Material.Physical.DefinitionId == id || Reservoirs.Capacity.Domain.Material.Item.DefinitionId == id
                || (Reservoirs.Capacity.Production?.ContainsIdentity(id) ?? false) || Reservoirs.Geometries.Any(g => g.ReservoirId == id)
                || id == Reservoirs.Capacity.Domain.Network.Scope.RealmId)
            {
                throw new ArgumentException("Transport identities cannot alias admitted material, geometry or scope roles.");
            }
        }
        var edges = new HashSet<(long X, long Y, long EndX, long EndY)>();
        foreach (var connection in Connections)
        {
            connection.Validate(Reservoirs);
            bool forward = connection.SourceX < connection.ReceiverX || connection.SourceX == connection.ReceiverX && connection.SourceY < connection.ReceiverY;
            var edge = forward ? (connection.SourceX, connection.SourceY, connection.ReceiverX, connection.ReceiverY)
                : (connection.ReceiverX, connection.ReceiverY, connection.SourceX, connection.SourceY);
            if (!edges.Add(edge))
            {
                throw new ArgumentException("One shared face cannot have duplicate or opposing gate aliases in this profile.");
            }
        }
        foreach (var stage in Stages)
        {
            if (stage.Routes.Any(r => !Connections.Any(c => c.ConnectionId == r.ConnectionId)) || stage.ResolvedAt > Boundary
                || stage.Status is RunoffTransportStatus.BlockedByHead or RunoffTransportStatus.BlockedByMaterial or RunoffTransportStatus.BlockedByThroughput
                    && stage.DueAt > Boundary)
            {
                throw new ArgumentException("Stage references and resolved time must agree with their retained owner.");
            }
            if (stage.IsPending)
            {
                var runoff = Reservoirs.Capacity.Pending.SingleOrDefault(p => p.Request.ReservationId == stage.ReservationId);
                if (runoff is null || stage.FundingHash != RunoffTransportStage.Fingerprint(runoff.Request)
                    || stage.Units != runoff.TotalInputUnits || stage.DueAt < runoff.Request.End)
                {
                    throw new ArgumentException("A pending stage must retain its exact active funding and explicit arrival contract.");
                }
                _ = RunoffTransportRouting.Resolve(this, runoff, stage.Routes);
            }
            else if (!Reservoirs.Capacity.Closed.Contains(stage.ReservationId))
            {
                throw new ArgumentException("A terminal stage requires a closed capacity commitment.");
            }
        }
    }
}
