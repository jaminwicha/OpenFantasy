using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;

namespace OpenFantasy.Hosting.Tests.Simulation;

internal static class SessionFixture
{
    internal static WorldSnapshot WithAmblingNpc(WorldSnapshot state)
    {
        WorldResident[] residents = state.Residents.Select(resident => new WorldResident(resident.ActorId,
            resident.InventoryCustodianId, resident.DisplayName, resident.ControllerKind, resident.HomeFeet,
            resident.ControllerKind == ResidentControllerKind.Host ? 1 : 0, resident.Comprehension,
            resident.ExperiencedTime, resident.OpeningUtterance)).ToArray();
        return new WorldSnapshot(state.Inventory, state.Space, residents, state.AdmissionVersion, state.Calendar,
            state.Revision, state.AdvancingSteps, state.Dialogues);
    }

    internal static WorldSnapshot WithOccupiedAppliedResult(WorldSnapshot state, WorldActionCommand action)
    {
        byte[] Number(ulong value) { byte[] bytes = new byte[8]; BinaryPrimitives.WriteUInt64BigEndian(bytes, value); return bytes; }
        byte[][] fields = [Encoding.UTF8.GetBytes("world.action-receipt"), Number(1), action.CommandId.ToByteArray(),
            action.PrincipalId.ToByteArray(), action.WorldId.ToByteArray(), action.BranchId.ToByteArray(),
            action.ContentHash.ToByteArray(), Number(1), Number(1), Number(0)];
        using var encoded = new MemoryStream();
        encoded.Write("OFC\0"u8);
        byte[] valueBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(valueBytes, 1); encoded.Write(valueBytes);
        BinaryPrimitives.WriteUInt32BigEndian(valueBytes, (uint)fields.Length); encoded.Write(valueBytes);
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(valueBytes, (uint)field.Length); encoded.Write(valueBytes); encoded.Write(field);
        }
        EntityId result = EntityId.FromBytes(SHA256.HashData(encoded.ToArray()).AsSpan(0, 16));
        var old = state.Inventory;
        var inventory = new CountedAuthorityState(old.Custody, old.AdmittedUnitCapacity, old.Actors, old.Controls,
            old.Policies, old.Sources, [.. old.ReservationIds, result]);
        return new WorldSnapshot(inventory, state.Space, state.Residents, state.AdmissionVersion, state.Calendar,
            state.Revision, state.AdvancingSteps, state.Dialogues);
    }

    internal static WorldActionCommand Speak(WorldSnapshot state, ulong command = 201) => new(WorldServiceFixture.Id(command),
        WorldServiceFixture.Delivery.PrincipalId, WorldServiceFixture.Delivery.SessionId, WorldServiceFixture.Id(10),
        state.WorldId, state.BranchId, state.Space.GetActor(WorldServiceFixture.Id(10)).Revision,
        new SpeakToActorIntent(WorldServiceFixture.Id(11), state.Space.GetActor(WorldServiceFixture.Id(11)).Revision));

    internal static async Task Tick(ManualSessionTimeProvider time, TimeSpan? elapsed = null)
    {
        await Until(() => time.HasPendingTimer);
        time.Advance(elapsed ?? TimeSpan.FromMilliseconds(50));
        await Until(() => time.HasPendingTimer);
    }

    internal static async Task Until(Func<bool> predicate)
    {
        var watch = Stopwatch.StartNew();
        while (!predicate())
        {
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), "The controlled session did not reach its expected boundary.");
            await Task.Delay(1, TestContext.Current.CancellationToken);
        }
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), "The controlled session reached its boundary after the deadline.");
    }
}

internal sealed class SessionFiles : IDisposable
{
    private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "openfantasy-session-" + Guid.NewGuid().ToString("N"));
    internal SessionFiles() { Directory.CreateDirectory(_directory); }
    internal string Path => System.IO.Path.Combine(_directory, "world.sqlite");
    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
