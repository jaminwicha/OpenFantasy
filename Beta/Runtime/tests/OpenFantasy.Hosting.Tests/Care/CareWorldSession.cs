using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>Exercises the real processor without claiming durable host admission for an unqualified profile.</summary>
internal sealed class CareWorldSession
{
    private readonly Func<WorldSnapshot> _read;
    private readonly Func<WorldStepBatch, WorldStepDecision> _commit;
    private readonly List<WorldStepBatch> _batches = [];
    private readonly List<WorldResourceArchiveSegment> _archives = [];
    private readonly HashSet<EntityId> _occupied = [];

    internal CareWorldSession(WorldSnapshot genesis, IEnumerable<EntityId>? priorIdentities = null)
    {
        var current = genesis;
        if (priorIdentities is not null) _occupied.UnionWith(priorIdentities);
        var processor = new WorldStepProcessor();
        _read = () => current;
        _commit = batch =>
        {
            var decision = processor.Decide(current, batch, _occupied.Contains);
            foreach (var identity in decision.Identities) Assert.True(_occupied.Add(identity.Identity));
            if (decision.SealedResourceSegment is { } segment) _archives.Add(segment);
            _batches.Add(batch);
            current = decision.Candidate;
            return decision;
        };
    }

    internal CareWorldSession(SqliteWorldAuthority authority)
    {
        _read = authority.ReadStateForHost;
        _commit = batch => authority.Commit(batch);
    }

    internal WorldSnapshot ReadStateForHost() => _read();
    internal WorldStepDecision Commit(WorldStepBatch batch) => _commit(batch);
    internal IReadOnlyList<WorldStepBatch> Batches => _batches;
    internal IReadOnlyList<WorldResourceArchiveSegment> Archives => _archives;
    internal IEnumerable<EntityId> OccupiedIdentities => _occupied;
}
