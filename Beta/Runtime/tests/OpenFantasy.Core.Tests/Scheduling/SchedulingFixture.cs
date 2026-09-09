using System.Globalization;
using System.Text.Json;
using OpenFantasy.Core.Scheduling;

namespace OpenFantasy.Core.Tests.Scheduling;

internal static class SchedulingFixture
{
    private static readonly JsonElement _fixture = Load();

    internal static JsonElement Read() => _fixture;

    internal static CalendarQueueScope Scope()
    {
        JsonElement scope = _fixture.GetProperty("scope");
        return new CalendarQueueScope(EntityId.Parse(scope.GetProperty("worldId").GetString()),
            EntityId.Parse(scope.GetProperty("branchId").GetString()),
            EntityId.Parse(scope.GetProperty("clockId").GetString()));
    }

    internal static DueWorkQueueSnapshot Create() => new(Scope(), Unsigned(_fixture.GetProperty("revision")),
        _fixture.GetProperty("entries").EnumerateArray().Select(value => new ScheduledWorkEntry(
            EntityId.Parse(value.GetProperty("eventId").GetString()),
            new WorldInstant(long.Parse(value.GetProperty("days").GetString()!, CultureInfo.InvariantCulture),
                value.GetProperty("milliseconds").GetInt32()),
            (ScheduledWorkPhase)value.GetProperty("phase").GetInt32(),
            EntityId.Parse(value.GetProperty("domainId").GetString()),
            EntityId.Parse(value.GetProperty("actorId").GetString()),
            EntityId.Parse(value.GetProperty("workItemId").GetString()),
            Unsigned(value.GetProperty("continuationOrdinal")))));

    internal static ScheduledWorkEntry Entry(ulong eventId, long dueDay = 0,
        ScheduledWorkPhase phase = ScheduledWorkPhase.StateTransition) => new(new EntityId(0, eventId),
            new WorldInstant(dueDay, 0), phase, new EntityId(0, 1), new EntityId(0, 1),
            new EntityId(0, eventId), 0);

    internal static CalendarClockCoordinate Cutoff(long days = 0) => new(Scope(), new WorldInstant(days, 0));
    internal static byte[] Bytes(string name) => Convert.FromHexString(_fixture.GetProperty(name).GetString()!);
    private static ulong Unsigned(JsonElement value) => ulong.Parse(value.GetString()!, CultureInfo.InvariantCulture);

    private static JsonElement Load()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "scheduling-v1.json")));
        return document.RootElement.Clone();
    }
}
