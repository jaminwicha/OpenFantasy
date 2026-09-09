using System.Globalization;
using System.Text.Json;
using OpenFantasy.Core.Actors;

namespace OpenFantasy.Core.Tests.Actors;

internal static class ActorFixture
{
    private static readonly JsonElement _fixture = Load();

    internal static JsonElement Read() => _fixture;
    internal static BodyDefinition Definition(string name) => Definition(_fixture.GetProperty("definitions")
        .EnumerateArray().Single(value => value.GetProperty("name").GetString() == name));
    internal static BodyDefinition Definition(EntityId id) => Definition(_fixture.GetProperty("definitions")
        .EnumerateArray().Single(value => EntityId.Parse(value.GetProperty("definitionId").GetString()) == id));

    internal static BodyDefinition Definition(JsonElement value) => new(
        EntityId.Parse(value.GetProperty("definitionId").GetString()), (BodyNature)value.GetProperty("nature").GetInt32(),
        Dimensions(value.GetProperty("clearance")), value.GetProperty("parts").EnumerateArray().Select(part =>
            new BodyPartDefinition(new BodySlotKey(part.GetProperty("slot").GetString()!),
                part.GetProperty("parent").ValueKind == JsonValueKind.Null ? null : new BodySlotKey(part.GetProperty("parent").GetString()!),
                Dimensions(part.GetProperty("dimensions")), part.GetProperty("functions").EnumerateArray()
                    .Select(function => (BodyFunction)function.GetInt32()))),
        value.GetProperty("locomotion").EnumerateArray().Select(requirement =>
            new LocomotionRequirement((LocomotionMode)requirement[0].GetInt32(), requirement[1].GetInt32())));

    internal static BodyConditionSnapshot Condition(JsonElement value) => new(
        EntityId.Parse(value.GetProperty("formInstanceId").GetString()),
        Definition(EntityId.Parse(value.GetProperty("definitionId").GetString())),
        value.GetProperty("injuries").EnumerateArray().Select(injury => new BodyInjury(
            EntityId.Parse(injury.GetProperty("injuryId").GetString()), EntityId.Parse(injury.GetProperty("causeId").GetString()),
            new BodySlotKey(injury.GetProperty("slot").GetString()!), (BodyInjuryKind)injury.GetProperty("kind").GetInt32(),
            injury.GetProperty("lostFunction").ValueKind == JsonValueKind.Null ? null
                : (BodyFunction)injury.GetProperty("lostFunction").GetInt32())));

    internal static BodyPartDefinition Part(string slot, string? parent = null, params BodyFunction[] functions) =>
        new(new BodySlotKey(slot), parent is null ? null : new BodySlotKey(parent), new BodyDimensions(16, 16, 16), functions);

    internal static BodyDefinition Body(IEnumerable<BodyPartDefinition> parts, IEnumerable<LocomotionRequirement>? locomotion = null) =>
        new(new EntityId(0, 900), BodyNature.Corporeal, new BodyDimensions(256, 256, 512), parts, locomotion ?? []);

    private static BodyDimensions Dimensions(JsonElement value) => new(Parse(value[0]), Parse(value[1]), Parse(value[2]));
    private static long Parse(JsonElement value) => long.Parse(value.GetString()!, CultureInfo.InvariantCulture);

    private static JsonElement Load()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "actors-v1.json")));
        return document.RootElement.Clone();
    }
}
