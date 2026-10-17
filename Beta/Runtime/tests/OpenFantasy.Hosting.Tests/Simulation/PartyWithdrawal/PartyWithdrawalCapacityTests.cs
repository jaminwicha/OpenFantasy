using System.Buffers.Binary;
using System.Reflection;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.PartyWithdrawal;

public sealed class PartyWithdrawalCapacityTests(ITestOutputHelper output)
{
    [Fact]
    public void Four_longest_routes_fit_the_complete_reserved_root_and_reopen_from_ordinary_history()
    {
        var run = WithdrawalWorldFixture.Active(4);
        foreach (var actor in run.Humans) run.Choose(run.Command(actor, LongestRoute(run, actor)));
        var group = Assert.Single(run.State.Retreat!.Withdrawal!.Parties);
        Assert.Equal(4, group.Members.Count);
        Assert.All(group.Members, member => Assert.InRange(member.Intent.Route.Count, 31, 32));
        byte[] bytes = WorldSnapshotCodecV41.Encode(run.State);
        int[] sizes = FieldSizes(bytes);
        Assert.Equal(31, sizes.Length);
        int coordinationMaximum = (int)typeof(WorldSnapshotCodecV35).GetField("MaximumCoordinationByteLength",
            BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
        // Inspect the existing complete-frame reservations, including all other mutable owners.
        // This is a current admitted party envelope, not a city/frontier workload qualification.
        (int Field, int Maximum)[] reserved =
        [
            (18, LiquidCellStateCodecV2.MaximumEncodedByteLength),
            (20, WorldPacingCodecV1.MaximumEncodedByteLength),
            (21, EncounterLifecycleCodecV1.MaximumEncodedByteLength),
            (22, coordinationMaximum),
            (23, WorldResourceHistoryCodec.MaximumEncodedByteLength),
            (24, EncounterRetreatCodecV2.MaximumEncodedByteLength),
            (25, EncounterOutcomeCodecV1.MaximumEncodedByteLength),
            (26, WorldControllerCodecV1.MaximumStateByteLength),
            (29, WorldControlPermissionCodecV1.MaximumStateByteLength),
            (30, WorldEffectiveControlCodecV1.MaximumStateByteLength)
        ];
        Assert.All(reserved, entry => Assert.InRange(sizes[entry.Field], 1, entry.Maximum));
        long reservedBytes = bytes.Length + reserved.Sum(entry => (long)entry.Maximum - sizes[entry.Field]);
        Assert.True(reservedBytes <= WorldSnapshotCodec.MaximumStateByteLength);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));
        using var files = new StorageFiles();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, run.Genesis))
        {
            foreach (var batch in run.Batches) authority.Commit(batch);
            Assert.Equal(bytes, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(run.Genesis));
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        output.WriteLine(JsonSerializer.Serialize(new
        {
            HumanCount = run.Humans.Length, RouteSamples = group.Members.Select(member => member.Intent.Route.Count),
            EncodedRootBytes = bytes.Length, RetreatBytes = sizes[24], ReservedCompleteBytes = reservedBytes,
            MaximumRootBytes = WorldSnapshotCodec.MaximumStateByteLength,
            RemainingReservedHeadroom = WorldSnapshotCodec.MaximumStateByteLength - reservedBytes
        }));
    }

    internal static QueueEncounterRetreatIntent LongestRoute(WithdrawalWorldFixture run, EntityId actor)
    {
        var advice = WorldEncounterRetreatAdvisor.Advise(run.State, actor);
        Assert.True(advice.IsFound);
        foreach (var (outward, back) in new[]
        {
            (EncounterRetreatDirection.North, EncounterRetreatDirection.South),
            (EncounterRetreatDirection.East, EncounterRetreatDirection.West),
            (EncounterRetreatDirection.South, EncounterRetreatDirection.North),
            (EncounterRetreatDirection.West, EncounterRetreatDirection.East)
        })
        {
            var prefix = Enumerable.Range(0, (EncounterRetreatRules.MaximumRouteSamples - advice.Route.Count) / 2)
                .SelectMany(_ => new[] { outward, back });
            var intent = new QueueEncounterRetreatIntent(run.State.Pacing!.Encounter!.EncounterId,
                run.State.Pacing.Encounter.Revision, advice.Destination, prefix.Concat(advice.Route));
            if (WorldEncounterRetreats.EvaluateQueue(run.State, actor, intent) == WorldActionRejection.None) return intent;
        }
        throw new InvalidOperationException("No admitted maximum-length route for this actual actor.");
    }

    private static int[] FieldSizes(byte[] bytes)
    {
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8)));
        int offset = 12;
        var sizes = new int[count];
        for (int index = 0; index < count; index++)
        {
            sizes[index] = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
            offset = checked(offset + 4 + sizes[index]);
        }
        Assert.Equal(bytes.Length, offset);
        return sizes;
    }
}
