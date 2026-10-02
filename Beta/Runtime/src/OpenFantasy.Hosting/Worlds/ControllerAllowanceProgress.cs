using System.Buffers.Binary;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

public sealed record ControllerAllowanceProgress(EntityId ActorId, ulong ControlRevision, EntityId DisconnectId,
    ulong ElapsedHostMilliseconds);

public sealed record ControllerRecoverySegment(ulong Generation, IReadOnlyList<ControllerAllowanceProgress> Allowances);

/// <summary>Immutable admission provenance, independent of subsequent episode closure or elapsed checkpoints.</summary>
internal sealed record ControllerRecoveryActivation(bool Upgraded, ulong SourceRevision, ContentHash SourceHash,
    IReadOnlyList<ControllerAllowanceProgress> UnknownPriorElapsed);

internal sealed record ControllerRecoveryState(ControllerRecoveryActivation Activation, ulong Generation,
    IReadOnlyList<ControllerAllowanceProgress> Allowances)
{
    internal static readonly ContentHash PolicyPin = ContentHash.Compute(System.Text.Encoding.UTF8.GetBytes(
        "controller.recovery.lower-bound.v1; explicit activation; exact episode and control revision; " +
        "only persisted measured host milliseconds survive restart; unknown prior elapsed is zero lower bound; " +
        "no UTC or offline time; fenced segments; expiry requires durable 30000 milliseconds; no world advancement"));

    internal static IReadOnlyList<ControllerAllowanceProgress> FromWorld(WorldSnapshot state) => Array.AsReadOnly(
        (state.Controllers ?? throw new InvalidDataException("Controller recovery requires a controller world."))
        .Assignments.Where(row => row.Mode == WorldControllerMode.ReconnectAllowance)
        .Select(row => new ControllerAllowanceProgress(row.ActorId, row.Revision, row.DisconnectId!.Value, 0)).ToArray());

    internal void ValidateHead(WorldSnapshot state)
    {
        _ = EncodeRows(Allowances);
        var expected = FromWorld(state);
        if (Allowances.Count != expected.Count || Allowances.Where((row, index) =>
            row.ActorId != expected[index].ActorId || row.ControlRevision != expected[index].ControlRevision
            || row.DisconnectId != expected[index].DisconnectId).Any())
            throw new InvalidDataException("Controller progress does not match the complete current allowance set.");
    }

    internal ControllerRecoveryState AfterCommit(WorldSnapshot candidate, WorldStepBatch batch)
    {
        if (batch.ControllerEvent is { Kind: WorldControllerEventKind.AllowanceExpired } expiry
            && !Allowances.Any(row => row.ActorId == expiry.ActorId && row.DisconnectId == expiry.DisconnectId
                && row.ControlRevision == expiry.ExpectedControlRevision && row.ElapsedHostMilliseconds == 30_000
                && expiry.ElapsedHostMilliseconds <= row.ElapsedHostMilliseconds))
            throw new InvalidOperationException("Allowance expiry requires its complete persisted measured lower bound.");
        var rows = FromWorld(candidate).Select(row => Allowances.FirstOrDefault(old => old.ActorId == row.ActorId
            && old.ControlRevision == row.ControlRevision && old.DisconnectId == row.DisconnectId) ?? row).ToArray();
        return rows.SequenceEqual(Allowances) ? this : this with { Allowances = Array.AsReadOnly(rows) };
    }

    internal static byte[] EncodeRows(IReadOnlyList<ControllerAllowanceProgress> rows)
    {
        if (rows.Count > WorldControllerState.MaximumActors) throw new InvalidDataException("Too many controller progress rows.");
        byte[] bytes = new byte[1 + rows.Count * 48]; bytes[0] = checked((byte)rows.Count);
        var episodes = new HashSet<EntityId>();
        for (int index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (row is null || row.ControlRevision == 0 || row.ElapsedHostMilliseconds > 30_000
                || row.ActorId == row.DisconnectId || !episodes.Add(row.DisconnectId)
                || index > 0 && rows[index - 1].ActorId.CompareTo(row.ActorId) >= 0)
                throw new InvalidDataException("Controller progress rows are not their bounded canonical set.");
            Span<byte> target = bytes.AsSpan(1 + index * 48, 48);
            row.ActorId.WriteBytes(target); BinaryPrimitives.WriteUInt64BigEndian(target[16..], row.ControlRevision);
            row.DisconnectId.WriteBytes(target[24..]); BinaryPrimitives.WriteUInt64BigEndian(target[40..], row.ElapsedHostMilliseconds);
        }
        return bytes;
    }

    internal static IReadOnlyList<ControllerAllowanceProgress> DecodeRows(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty || bytes[0] > 4 || bytes.Length != 1 + bytes[0] * 48)
            throw new InvalidDataException("Invalid controller progress encoding.");
        var rows = new ControllerAllowanceProgress[bytes[0]];
        for (int index = 0; index < rows.Length; index++)
        {
            ReadOnlySpan<byte> row = bytes.Slice(1 + index * 48, 48);
            rows[index] = new(EntityId.FromBytes(row[..16]), BinaryPrimitives.ReadUInt64BigEndian(row[16..]),
                EntityId.FromBytes(row.Slice(24, 16)), BinaryPrimitives.ReadUInt64BigEndian(row[40..]));
        }
        var result = Array.AsReadOnly(rows);
        if (!bytes.SequenceEqual(EncodeRows(result))) throw new InvalidDataException("Noncanonical controller progress.");
        return result;
    }

    internal static byte[] EncodeActivation(ControllerRecoveryActivation activation)
    {
        byte[] rows = EncodeRows(activation.UnknownPriorElapsed);
        if ((!activation.Upgraded && (activation.SourceRevision != 0 || activation.UnknownPriorElapsed.Count != 0))
            || activation.UnknownPriorElapsed.Any(row => row.ElapsedHostMilliseconds != 0))
            throw new InvalidDataException("Invalid controller recovery activation provenance.");
        byte[] bytes = new byte[42 + rows.Length]; bytes[0] = 1; bytes[1] = activation.Upgraded ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(2), activation.SourceRevision);
        activation.SourceHash.ToByteArray().CopyTo(bytes, 10); rows.CopyTo(bytes, 42);
        return bytes;
    }

    internal static ControllerRecoveryActivation DecodeActivation(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 43 || bytes[0] != 1 || bytes[1] > 1) throw new InvalidDataException("Invalid recovery activation.");
        var value = new ControllerRecoveryActivation(bytes[1] == 1, BinaryPrimitives.ReadUInt64BigEndian(bytes[2..]),
            new ContentHash(Convert.ToHexString(bytes.Slice(10, 32)).ToLowerInvariant()), DecodeRows(bytes[42..]));
        if (!bytes.SequenceEqual(EncodeActivation(value))) throw new InvalidDataException("Noncanonical recovery activation.");
        return value;
    }

    internal ContentHash Hash => ContentHash.Compute(PolicyPin.ToByteArray()
        .Concat(EncodeActivation(Activation)).Concat(WorldStorageBinary.Number(Generation)).Concat(EncodeRows(Allowances)).ToArray());
}
