using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Host;

/// <summary>Privileged offline operations under the existing exclusive composed-world authority.</summary>
internal sealed class ComposedWorldOperator
{
    internal string CreateEncounterWorld(string database, string seed, string playerName)
    {
        var genesis = OpenFantasy.Hosting.Content.EncounterFarmAdmission.CreateEmbedded(seed, playerName);
        using SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(database, genesis);
        return Report(authority);
    }

    internal string CreatePacedWorld(string database, string basisPath, string encounterText)
    {
        WorldSnapshot genesis = WorldPacingAdmission.CreateFresh(LocalAuthorityFiles.ReadComposedGenesis(basisPath),
            encounterText == "none" ? null : EntityId.Parse(encounterText));
        using SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(database, genesis);
        return Report(authority);
    }

    internal string CreateLiquidCellWorld(string database, string basisPath, string liquidCellsPath)
    {
        WorldSnapshot basis = LocalAuthorityFiles.ReadComposedGenesis(basisPath);
        if (basis.ProfileVersion != 19 || basis.Reservoirs is null)
        {
            throw new ArgumentException("Detailed liquid creation requires a profile-nineteen contained reservoir basis.");
        }
        WorldSnapshot genesis = WorldLiquidCellAdmission.CreateGenesis(basis,
            LocalAuthorityFiles.ReadLiquidCells(liquidCellsPath, basis.Reservoirs));
        using SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(database, genesis);
        return Report(authority);
    }

    internal string CreateContainedReservoirWorld(string database, string basisPath, string sealPolicyPath)
    {
        WorldSnapshot genesis = WorldReservoirContainmentAdmission.CreateGenesis(
            LocalAuthorityFiles.ReadComposedGenesis(basisPath), LocalAuthorityFiles.ReadReservoirSealPolicy(sealPolicyPath));
        using SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(database, genesis);
        return Report(authority);
    }

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
            state.Calendar, state.AdvancingSteps,
            Pacing = state.Pacing is { } pacing ? new WorldPacingDiagnosticReport(pacing, state) : null,
            EncounterActions = state.EncounterActions is not null ? new EncounterActionDiagnosticReport(state) : null,
            ConstructionPhysical = state.ConstructionPhysical is { } physical ? new ConstructionPhysicalDiagnosticReport(physical) : null,
            StructuralPolicy = state.StructuralPolicy is { } policy ? new ConstructionLoadPolicyDiagnosticReport(policy) : null,
            StructuralSettlementPolicy = state.StructuralSettlementPolicy is { } settlement ? new StructuralSettlementPolicyDiagnosticReport(settlement) : null,
            Watershed = state.Watershed is { } plan ? new WatershedDiagnosticReport(plan) : null,
            WatershedNetwork = state.WatershedNetwork is { } network ? new WatershedNetworkDiagnosticReport(network) : null,
            Reservoirs = state.LiquidCells is null && state.Reservoirs is { } reservoirs ? new LiquidReservoirDiagnosticReport(reservoirs) : null,
            LiquidCells = state.LiquidCells is { } cells ? new LiquidCellDiagnosticReport(cells) : null,
            ReservoirSeals = state.ReservoirSeals is { } seals ? new
            {
                ContentHash = seals.Version.Value,
                LiquidDefinitionId = seals.Liquid.DefinitionId.ToString(),
                LiquidVersion = seals.Liquid.Version.Value,
                RuleCount = seals.Rules.Count
            } : null,
            Transport = state.Transport is { } transport ? new ReservoirTransportDiagnosticReport(transport) : null
        });
    }
}
