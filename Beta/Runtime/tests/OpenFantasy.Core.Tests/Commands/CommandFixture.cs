using System.Globalization;
using System.Text.Json;
using OpenFantasy.Core.Commands;

namespace OpenFantasy.Core.Tests.Commands;

internal static class CommandFixture
{
    private static readonly JsonElement _fixture = Load();

    internal static JsonElement Read() => _fixture;

    private static JsonElement Load()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "commands-v1.json")));
        return document.RootElement.Clone();
    }

    internal static EntityId Id(string name) => EntityId.Parse(Read().GetProperty("ids").GetProperty(name).GetString());

    internal static CommandEnvelope Create(ulong quantity = 7, EntityId? originSession = null,
        EntityId? commandId = null, EntityId? principalId = null, EntityId? worldId = null,
        EntityId? branchId = null, EntityId? actorId = null,
        IEnumerable<ExpectedRevision>? revisions = null)
    {
        JsonElement fixture = Read();
        revisions ??= fixture.GetProperty("expectedRevisions").EnumerateArray().Select(value =>
            new ExpectedRevision((RevisionTargetKind)value.GetProperty("kind").GetInt32(),
                EntityId.Parse(value.GetProperty("id").GetString()),
                ulong.Parse(value.GetProperty("revision").GetString()!, CultureInfo.InvariantCulture)));
        return new CommandEnvelope(commandId ?? Id("command"), principalId ?? Id("principal"),
            originSession ?? Id("originSession"), actorId ?? Id("actor"), worldId ?? Id("world"),
            branchId ?? Id("branch"), revisions,
            new TransferLotIntent(Id("lot"), Id("source"), Id("destination"), quantity));
    }

    internal static byte[] Bytes(string name) => Convert.FromHexString(Read().GetProperty(name).GetString()!);
}
