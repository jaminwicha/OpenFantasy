using System.Text.Json;

namespace OpenFantasy.Core.Tests;

internal static class ReferenceVectors
{
    public static JsonElement Read() => JsonDocument.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference-v1.json"))).RootElement.Clone();

    public static ulong Unsigned(JsonElement value) => ulong.Parse(
        value.GetString()!, System.Globalization.CultureInfo.InvariantCulture);
}
