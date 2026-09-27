using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>Exercises the real processor without claiming durable host admission for an unqualified profile.</summary>
internal sealed class CareWorldSession
{
    private readonly Func<WorldSnapshot> _read;
    private readonly Func<WorldStepBatch, WorldStepDecision> _commit;

    internal CareWorldSession(WorldSnapshot genesis)
    {
        var current = genesis;
        HashSet<EntityId> occupied = [];
        var processor = new WorldStepProcessor();
        _read = () => current;
        _commit = batch =>
        {
            var decision = processor.Decide(current, batch, occupied.Contains);
            foreach (var identity in decision.Identities) Assert.True(occupied.Add(identity.Identity));
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
}
