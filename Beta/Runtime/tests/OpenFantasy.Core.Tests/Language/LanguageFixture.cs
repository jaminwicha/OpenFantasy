using System.Globalization;
using System.Text.Json;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Language;

namespace OpenFantasy.Core.Tests.Language;

internal static class LanguageFixture
{
    private static readonly JsonElement _fixture = Load();

    internal static JsonElement Read() => _fixture;
    internal static ActorComprehensionProfile Profile(string name) => Profile(_fixture.GetProperty("profiles")
        .EnumerateArray().Single(value => value.GetProperty("name").GetString() == name));

    internal static ActorComprehensionProfile Profile(JsonElement value) => new(
        EntityId.Parse(value.GetProperty("actorId").GetString()),
        ulong.Parse(value.GetProperty("revision").GetString()!, CultureInfo.InvariantCulture),
        new ContentHash(value.GetProperty("rulesVersion").GetString()!),
        value.GetProperty("entries").EnumerateArray().Select(entry => new LanguageComprehensionEntry(
            Channel(entry.GetProperty("channel")), new ContentHash(entry.GetProperty("definitionVersion").GetString()!),
            (ComprehensionBand)entry.GetProperty("band").GetInt32())));

    internal static LanguageChannelKey Channel(JsonElement value) => new(
        EntityId.Parse(value.GetProperty("languageId").GetString()),
        EntityId.Parse(value.GetProperty("registerId").GetString()),
        (CommunicationMode)value.GetProperty("mode").GetInt32());

    private static JsonElement Load()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "language-v1.json")));
        return document.RootElement.Clone();
    }
}
