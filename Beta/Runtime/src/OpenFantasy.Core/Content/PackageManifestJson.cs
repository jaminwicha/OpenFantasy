using System.Text.Json;

namespace OpenFantasy.Core.Content;

/// <summary>Strict bounded interchange JSON; manifest identity comes from canonical binary bytes.</summary>
public static class PackageManifestJson
{
    public const int MaximumDocumentBytes = 65_536;

    public static PackageManifest Read(ReadOnlyMemory<byte> utf8)
    {
        if (utf8.Length > MaximumDocumentBytes)
        {
            throw new FormatException("The manifest exceeds its byte limit.");
        }
        using JsonDocument document = JsonDocument.Parse(utf8, new JsonDocumentOptions { MaxDepth = 8 });
        JsonElement root = document.RootElement;
        RequireProperties(root, "schemaVersion", "id", "payloadSha256", "dependencies");
        JsonElement schema = root.GetProperty("schemaVersion");
        if (schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out int version)
            || version != PackageManifest.SchemaVersion)
        {
            throw new FormatException("Unsupported package manifest schema.");
        }
        var id = new PackageId(ReadString(root, "id"));
        var payloadHash = new ContentHash(ReadString(root, "payloadSha256"));
        JsonElement entries = root.GetProperty("dependencies");
        if (entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() > PackageManifest.MaximumDependencies)
        {
            throw new FormatException("Invalid or excessive package dependencies.");
        }
        var dependencies = new List<PackageDependency>();
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            RequireProperties(entry, "id", "versionSha256");
            dependencies.Add(new PackageDependency(new PackageId(ReadString(entry, "id")),
                new ContentHash(ReadString(entry, "versionSha256"))));
        }
        return new PackageManifest(id, payloadHash, dependencies);
    }

    public static byte[] Write(PackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", PackageManifest.SchemaVersion);
            writer.WriteString("id", manifest.Id.Value);
            writer.WriteString("payloadSha256", manifest.PayloadHash.Value);
            writer.WriteStartArray("dependencies");
            foreach (PackageDependency dependency in manifest.Dependencies)
            {
                writer.WriteStartObject();
                writer.WriteString("id", dependency.Id.Value);
                writer.WriteString("versionSha256", dependency.Version.Value);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    private static string ReadString(JsonElement element, string name)
    {
        JsonElement value = element.GetProperty(name);
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new FormatException("A manifest text field has the wrong type: " + name);
        }
        return value.GetString()!;
    }

    private static void RequireProperties(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("A manifest record must be an object.");
        }
        var remaining = new HashSet<string>(names, StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!remaining.Remove(property.Name))
            {
                throw new FormatException("Unknown or duplicate manifest field: " + property.Name);
            }
        }
        if (remaining.Count != 0)
        {
            throw new FormatException("A manifest record is missing required fields.");
        }
    }
}
