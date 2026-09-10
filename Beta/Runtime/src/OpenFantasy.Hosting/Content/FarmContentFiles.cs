using System.Text.Json;
using System.Reflection;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Hosting.Content;

/// <summary>Bounded reads from the caller's installed content directory; no file is modified.</summary>
internal sealed class FarmContentFiles
{
    private const string ResourcePrefix = "OpenFantasy.Content/";
    private readonly string? _root;
    private readonly Assembly? _assembly;
    private readonly Dictionary<string, string>? _resourceNames;

    internal FarmContentFiles(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.GetFullPath(root);
    }

    private FarmContentFiles(Assembly assembly)
    {
        _assembly = assembly;
        // RecursiveDir metadata can use the build host's separator. Resolve a portable key
        // while retaining the exact emitted manifest name needed by GetManifestResourceStream.
        _resourceNames = assembly.GetManifestResourceNames().ToDictionary(name => name.Replace('\\', '/'),
            name => name, StringComparer.Ordinal);
    }

    internal static FarmContentFiles Embedded() => new(typeof(FarmContentFiles).Assembly);

    internal byte[] Read(string relativePath, int maximumBytes, string expectedHash)
    {
        if (Path.IsPathRooted(relativePath) || relativePath.Contains('\\')
            || relativePath.Split('/').Any(part => part is "" or "." or ".."))
        {
            throw new InvalidDataException("A farm content path must be a canonical relative path.");
        }
        using Stream stream = Open(relativePath);
        if (stream.Length is < 1 || stream.Length > maximumBytes)
        {
            throw new InvalidDataException("A farm content file exceeds its admitted byte limit: " + relativePath);
        }
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1 || ContentHash.Compute(bytes).Value != expectedHash)
        {
            throw new InvalidDataException("A farm content file differs from its exact admitted hash: " + relativePath);
        }
        return bytes;
    }

    private Stream Open(string relativePath)
    {
        if (_assembly is not null)
        {
            if (!_resourceNames!.TryGetValue(ResourcePrefix + relativePath, out string? manifestName))
            {
                throw new FileNotFoundException("An embedded farm content resource is missing: " + relativePath);
            }
            return _assembly.GetManifestResourceStream(manifestName)
                ?? throw new FileNotFoundException("An embedded farm content stream is unavailable: " + relativePath);
        }
        string path = Path.Combine(_root!, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    internal static JsonDocument Parse(byte[] bytes) => JsonDocument.Parse(bytes,
        new JsonDocumentOptions { MaxDepth = 32 });
}
