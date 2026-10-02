using Microsoft.Data.Sqlite;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

internal sealed partial class WorldDatabase
{
    internal ControllerRecoveryState ReadControllerRecovery()
    {
        using var activation = _lease.CreateCommand("SELECT policy,provenance,hash FROM controller_recovery_activation WHERE singleton=1");
        ControllerRecoveryActivation provenance;
        using (var reader = activation.ExecuteReader())
        {
            RequireRow(reader, "Missing controller recovery activation.");
            byte[] bytes = WorldStorageBinary.ReadBlob(reader, 1, 43, 235);
            if (WorldStorageBinary.ReadHash(reader, 0) != ControllerRecoveryState.PolicyPin
                || WorldStorageBinary.ReadHash(reader, 2) != ContentHash.Compute(bytes))
                throw new InvalidDataException("Controller recovery activation checksum or policy mismatch.");
            provenance = ControllerRecoveryState.DecodeActivation(bytes);
        }
        var rows = new List<ControllerAllowanceProgress>();
        using (var command = _lease.CreateCommand("SELECT actor,revision,episode,elapsed FROM controller_allowances ORDER BY actor"))
        using (var reader = command.ExecuteReader())
            while (reader.Read())
            {
                if (rows.Count == 4) throw new InvalidDataException("Too many controller progress rows.");
                rows.Add(new(WorldStorageBinary.ReadIdentity(reader, 0), WorldStorageBinary.ReadNumber(reader, 1),
                    WorldStorageBinary.ReadIdentity(reader, 2), WorldStorageBinary.ReadNumber(reader, 3)));
            }
        using var segment = _lease.CreateCommand("SELECT generation,hash FROM controller_recovery_segment WHERE singleton=1");
        using var current = segment.ExecuteReader();
        RequireRow(current, "Missing controller recovery segment.");
        var result = new ControllerRecoveryState(provenance, WorldStorageBinary.ReadNumber(current, 0), rows.AsReadOnly());
        if (result.Hash != WorldStorageBinary.ReadHash(current, 1)) throw new InvalidDataException("Controller recovery progress checksum mismatch.");
        return result;
    }

    internal void InitializeControllerRecovery(WorldSnapshot state, bool upgraded, SqliteTransaction transaction)
    {
        var rows = ControllerRecoveryState.FromWorld(state);
        var activation = new ControllerRecoveryActivation(upgraded, state.Revision, state.ContentHash, upgraded ? rows : Array.Empty<ControllerAllowanceProgress>());
        byte[] bytes = ControllerRecoveryState.EncodeActivation(activation);
        ChangeExactlyOne("INSERT INTO controller_recovery_activation VALUES(1,$policy,$source,$hash)", transaction,
            ("$policy", ControllerRecoveryState.PolicyPin.ToByteArray()), ("$source", bytes), ("$hash", ContentHash.Compute(bytes).ToByteArray()));
        var recovery = new ControllerRecoveryState(activation, 0, rows);
        ChangeExactlyOne("INSERT INTO controller_recovery_segment VALUES(1,$generation,$hash)", transaction,
            ("$generation", WorldStorageBinary.Number(0)), ("$hash", recovery.Hash.ToByteArray()));
        WriteControllerRecovery(recovery, transaction);
    }

    internal void WriteControllerRecovery(ControllerRecoveryState state, SqliteTransaction transaction)
    {
        using (var clear = _lease.CreateCommand("DELETE FROM controller_allowances", transaction)) clear.ExecuteNonQuery();
        foreach (var row in state.Allowances)
            ChangeExactlyOne("INSERT INTO controller_allowances VALUES($actor,$revision,$episode,$elapsed)", transaction,
                ("$actor", row.ActorId.ToByteArray()), ("$revision", WorldStorageBinary.Number(row.ControlRevision)),
                ("$episode", row.DisconnectId.ToByteArray()), ("$elapsed", WorldStorageBinary.Number(row.ElapsedHostMilliseconds)));
        ChangeExactlyOne("UPDATE controller_recovery_segment SET generation=$generation,hash=$hash WHERE singleton=1", transaction,
            ("$generation", WorldStorageBinary.Number(state.Generation)), ("$hash", state.Hash.ToByteArray()));
    }

    internal void ActivateControllerRecovery(WorldSnapshot state)
    {
        if (SchemaVersion != WorldDatabaseSchemaNine.Version) throw new InvalidOperationException("Recovery activation requires schema nine.");
        using (var transaction = BeginWrite())
        {
            foreach (var definition in WorldDatabaseSchemaTen.Tables.Where(pair => !WorldDatabaseSchemaNine.Tables.ContainsKey(pair.Key)))
            { using var command = _lease.CreateCommand(definition.Value, transaction); command.ExecuteNonQuery(); }
            InitializeControllerRecovery(state, true, transaction);
            _lease.StageControllerRecoverySchema(transaction);
            transaction.Commit();
        }
        _lease.AcceptControllerRecoverySchema();
        _lease.ValidateSchema(WorldDatabaseSchemaTen.Tables, WorldDatabaseSchemaTen.Indexes);
    }
}
