using System.Globalization;
using System.Text.Json;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Worlds;

internal static class CommandFixture
{
    internal static EntityId Id(ulong value) => new(0x1020304050607080, value);
    internal static JsonElement Read() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "world-actions-v1.json"))).RootElement.Clone();
    internal static EntityId Identity(JsonElement value, string key) => EntityId.Parse(value.GetProperty(key).GetString());
    internal static ulong Number(JsonElement value, string key) => ulong.Parse(value.GetProperty(key).GetString()!, CultureInfo.InvariantCulture);
    internal static byte[] Bytes(JsonElement value) => Convert.FromHexString(value.GetProperty("canonicalHex").GetString()!);

    internal static WorldActionCommand Action(JsonElement value)
    {
        JsonElement intent = value.GetProperty("intent");
        WorldActionIntent payload = intent.GetProperty("type").GetString() switch
        {
            "speak" => new SpeakToActorIntent(Identity(intent, "targetActorId"), Number(intent, "expectedTargetRevision")),
            "close" => new CloseDialogueIntent(Identity(intent, "dialogueId")),
            "give" => new GiveItemIntent(Identity(intent, "targetActorId"), Number(intent, "expectedTargetRevision"),
                Identity(intent, "lotId"), Number(intent, "expectedLotRevision"), Identity(intent, "sourceCustodianId"),
                Number(intent, "expectedSourceRevision"), Number(intent, "quantity")),
            _ => throw new InvalidOperationException("Unknown qualification action.")
        };
        return new WorldActionCommand(Identity(value, "commandId"), Identity(value, "principalId"),
            Identity(value, "originSessionId"), Identity(value, "actorId"), Identity(value, "worldId"),
            Identity(value, "branchId"), Number(value, "expectedActorRevision"), payload);
    }

    internal static WorldStepBatch Batch(JsonElement value)
    {
        Dictionary<string, WorldActionCommand> actions = Read().GetProperty("actions").EnumerateArray()
            .ToDictionary(row => row.GetProperty("name").GetString()!, Action);
        return new WorldStepBatch(Identity(value, "causeId"), Identity(value, "worldId"), Identity(value, "branchId"),
            Number(value, "expectedRevision"), Number(value, "expectedAdvancingSteps"), (WorldBatchKind)value.GetProperty("kind").GetInt32(),
            value.GetProperty("movement").EnumerateArray().Select(row => new WorldMovementInput(
                new TrustedCommandDelivery(Identity(row, "principalId"), Identity(row, "sessionId")), Identity(row, "actorId"),
                row.GetProperty("deltaX").GetInt64(), row.GetProperty("deltaY").GetInt64())),
            value.GetProperty("actionNames").EnumerateArray().Select(name => actions[name.GetString()!]));
    }

    internal static WorldActionCommand Speak(ulong actor = 101, ulong command = 401) => new(Id(command), Id(201),
        Id(301), Id(actor), Id(1), Id(2), 7, new SpeakToActorIntent(Id(109), 9));
    internal static WorldMovementInput Move(ulong actor = 101, long x = 32, long y = 0) =>
        new(new TrustedCommandDelivery(Id(201), Id(302)), Id(actor), x, y);
    internal static WorldStepBatch Step(IEnumerable<WorldMovementInput>? movement = null,
        IEnumerable<WorldActionCommand>? actions = null, WorldBatchKind kind = WorldBatchKind.AdvancingStep) =>
        new(Id(801), Id(1), Id(2), 17, 11, kind, movement ?? [], actions ?? []);
}
