namespace OpenFantasy.Core.Geography;

/// <summary>Resolves each original source claim through its unique drainage path to its admitted physical gate.</summary>
internal static class RunoffTransportRouting
{
    internal static Dictionary<EntityId, ReservoirConnection> Resolve(ReservoirTransportState state, PreparedWatershedRunoff runoff,
        IReadOnlyCollection<RunoffTransportRoute> routes)
    {
        var wet = runoff.Request.Inputs.Where(i => i.Source is not null).ToArray();
        if (routes.Count != wet.Length || routes.Select(r => r.BasinId).Distinct().Count() != routes.Count)
        {
            throw new ArgumentException("Every funded basin requires exactly one transport route; dry basins require none.");
        }
        var result = new Dictionary<EntityId, ReservoirConnection>();
        var network = runoff.Request.Network;
        foreach (var input in wet)
        {
            var route = routes.SingleOrDefault(r => r.BasinId == input.BasinId);
            var connection = state.Connections.SingleOrDefault(c => c.ConnectionId == route?.ConnectionId);
            if (connection is null)
            {
                throw new ArgumentException("Transport requires an admitted named gate for each source basin.");
            }
            EntityId terminal = input.BasinId;
            // The prepared network has already proved finiteness and acyclicity.
            for (int depth = 0; ; depth++)
            {
                if (depth >= network.Basins.Count)
                {
                    throw new ArgumentException("The transport path does not terminate in its finite drainage network.");
                }
                var closure = network.Closures.Single(c => c.BasinId == terminal);
                if (closure.ReceivingBasinId is not { } next)
                {
                    break;
                }
                terminal = next;
            }
            EntityId receiver = runoff.Request.Terminals.Single(t => t.BasinId == terminal).CustodianId;
            if (state.Reservoirs.Geometries.Single(g => g.ReservoirId == connection.SourceReservoirId).CustodianId != input.CustodianId
                || state.Reservoirs.Geometries.Single(g => g.ReservoirId == connection.ReceiverReservoirId).CustodianId != receiver)
            {
                throw new ArgumentException("A gate must connect the exact funded source to its drainage terminal's receiver.");
            }
            result.Add(input.BasinId, connection);
        }
        foreach (var group in wet.GroupBy(i => result[i.BasinId].ConnectionId))
        {
            ulong units = group.Aggregate(0UL, (sum, i) => checked(sum + i.Source!.Quantity));
            if (units > state.Connections.Single(c => c.ConnectionId == group.Key).MaximumUnitsPerStep)
            {
                throw new ArgumentException("The indivisible funded stage exceeds the gate's per-step throughput.");
            }
        }
        return result;
    }
}
