using System.Text.Json;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Host;

/// <summary>Privileged offline operations under the existing exclusive composed-world authority.</summary>
internal sealed class ComposedWorldOperator
{
    internal string CreateReservoirTransportWorld(string database, string basisPath, string transportPath)
    {
        WorldSnapshot genesis = WorldReservoirTransportAdmission.CreateGenesis(
            LocalAuthorityFiles.ReadComposedGenesis(basisPath), LocalAuthorityFiles.ReadReservoirTransport(transportPath));
        using SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(database, genesis);
        return Report(authority);
    }

    internal string CreateReservoirWorld(string database, string basisPath, string reservoirPath)
    {
        WorldSnapshot genesis = WorldReservoirAdmission.CreateGenesis(
            LocalAuthorityFiles.ReadComposedGenesis(basisPath), LocalAuthorityFiles.ReadLiquidReservoirs(reservoirPath));
        using SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(database, genesis);
        return Report(authority);
    }

    internal string CreateWatershedNetworkWorld(string database, string basisPath, string networkPath)
    {
        WorldSnapshot genesis = WorldWatershedNetworkAdmission.CreateGenesis(
            LocalAuthorityFiles.ReadComposedGenesis(basisPath), LocalAuthorityFiles.ReadWatershedNetwork(networkPath));
        using SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(database, genesis);
        return Report(authority);
    }

    internal string CreateWatershedWorld(string database, string basisPath, string watershedPath)
    {
        WorldSnapshot genesis = WorldWatershedAdmission.CreateGenesis(
            LocalAuthorityFiles.ReadComposedGenesis(basisPath), LocalAuthorityFiles.ReadWatershed(watershedPath));
        using SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(database, genesis);
        return Report(authority);
    }

    internal string ExecuteStep(string database, string stepPath, string? crashBoundary = null)
    {
        WorldStepBatch step = LocalAuthorityFiles.ReadWorldStep(stepPath);
        AuthorityCommitBoundary? requested = crashBoundary switch
        {
            null => null,
            "before" => AuthorityCommitBoundary.BeforeCommit,
            "after" => AuthorityCommitBoundary.AfterCommit,
            _ => throw new ArgumentException("The crash probe boundary must be before or after.")
        };
        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(database);
        authority.Commit(step, reached =>
        {
            if (reached != requested)
            {
                return;
            }
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = reached.ToString() }));
            Console.Out.Flush();
            // External qualification terminates this process without Dispose/finally.
            Thread.Sleep(Timeout.Infinite);
        });
        return Report(authority);
    }

    private static string Report(SqliteWorldAuthority authority)
    {
        WorldSnapshot state = authority.InspectForHost().State;
        return JsonSerializer.Serialize(new
        {
            Contract = "composed-world-operator-v1", WorldId = state.WorldId.ToString(),
            BranchId = state.BranchId.ToString(), state.Revision, state.ProfileVersion,
            StateHash = state.ContentHash.Value,
            ConstructionPhysical = state.ConstructionPhysical is { } physical ? new ConstructionPhysicalDiagnosticReport(physical) : null,
            StructuralPolicy = state.StructuralPolicy is { } policy ? new ConstructionLoadPolicyDiagnosticReport(policy) : null,
            Watershed = state.Watershed is { } plan ? new WatershedDiagnosticReport(plan) : null,
            WatershedNetwork = state.WatershedNetwork is { } network ? new WatershedNetworkDiagnosticReport(network) : null,
            Reservoirs = state.Reservoirs is { } reservoirs ? new LiquidReservoirDiagnosticReport(reservoirs) : null,
            Transport = state.Transport is { } transport ? new ReservoirTransportDiagnosticReport(transport) : null
        });
    }
}
