using Microsoft.Data.Sqlite;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ReservoirTransportTamperTests
{
    [Theory]
    [InlineData("world_genesis")]
    [InlineData("world_checkpoints")]
    [InlineData("world_head")]
    public void IndependentlyValidGateSubstitutionWithMatchingStoredHashStillFailsJournalReplay(string table)
    {
        var f = new ReservoirTransportWorldFixture(nearby: false);
        var genesis = f.Genesis;
        var action = f.Materials.Command(genesis, f.Player, f.Schedule(genesis, steps: 1));
        WorldSnapshot recorded = genesis;
        using var files = new StorageFiles();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            var final = authority.Commit(f.Materials.Actions.Batch(genesis, [action], [])).Candidate;
            Assert.Equal(RunoffTransportStatus.Completed, final.Transport!.Stages[0].Status);
            if (table == "world_head") recorded = final;
        }
        var gate = f.Transport.Connections[0];
        var replacementTransport = ReservoirTransportState.Create(f.Transport.Reservoirs,
            [new(gate.ConnectionId, gate.SourceReservoirId, gate.ReceiverReservoirId, gate.SourceX, gate.SourceY,
                gate.ReceiverX, gate.ReceiverY, gate.SillZ, gate.MaximumUnitsPerStep + 1)], f.Basis.Calendar);
        if (table == "world_head")
        {
            var intent = (ScheduleRunoffTransportIntent)action.Intent;
            replacementTransport = replacementTransport.PlanSchedule(EntityId.DeriveSpawned(action.CommandId, 0),
                intent.ReservationId, intent.DueAt, intent.Routes).ApplyTo(replacementTransport);
            replacementTransport = replacementTransport.PlanAdvance(intent.DueAt).ApplyTo(replacementTransport);
        }
        var replacement = new WorldSnapshot(recorded.Inventory, recorded.Space, recorded.Residents,
            recorded.AdmissionVersion, recorded.Calendar, recorded.Revision, recorded.AdvancingSteps, recorded.Dialogues,
            recorded.Gathering, recorded.Production, recorded.Progression, recorded.Quests, recorded.Construction,
            transport: replacementTransport);
        byte[] bytes = WorldSnapshotCodec.Encode(replacement);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));
        Assert.NotEqual(recorded.ContentHash, replacement.ContentHash);
        Assert.Equal(recorded.Reservoirs!.ContentHash, replacement.Reservoirs!.ContentHash);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = files.Path, Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE " + table + " SET state=$state,hash=$hash";
            command.Parameters.AddWithValue("$state", bytes);
            command.Parameters.AddWithValue("$hash", ContentHash.Compute(bytes).ToByteArray());
            Assert.Equal(1, command.ExecuteNonQuery());
        }
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path));
    }
}
