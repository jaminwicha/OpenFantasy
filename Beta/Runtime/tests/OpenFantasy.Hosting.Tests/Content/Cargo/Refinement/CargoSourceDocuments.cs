using System.Text;
using System.Text.Json;
using OpenFantasy.Core.Markets;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.Cargo;

namespace OpenFantasy.Hosting.Tests.Content.Cargo.Refinement;

/// <summary>Parsed copies of the digest-checked retained files, optionally edited in memory for one refusal case.</summary>
internal sealed class CargoSourceDocuments : IDisposable
{
    private const string Data = "legacy/aldermere-first-loop/source-support/data/";
    private readonly JsonDocument _npcs;
    private readonly JsonDocument _items;
    private readonly JsonDocument _world;

    private CargoSourceDocuments(JsonDocument npcs, JsonDocument items, JsonDocument world)
    {
        _npcs = npcs;
        _items = items;
        _world = world;
    }

    internal static CargoSourceDocuments Retained(Func<string, string>? npcs = null, Func<string, string>? world = null)
    {
        FarmContentFiles files = FarmContentFiles.Embedded();
        string Text(string name, string digest) => Encoding.UTF8.GetString(files.Read(Data + name, 131_072, digest));
        return new CargoSourceDocuments(
            JsonDocument.Parse((npcs ?? (text => text))(Text("npcs.json", CargoCircuitSources.NpcsDigest))),
            JsonDocument.Parse(Text("items.json", CargoCircuitSources.ItemsDigest)),
            JsonDocument.Parse((world ?? (text => text))(Text("world.json", CargoCircuitSources.WorldDigest))));
    }

    internal CargoCircuitSources Bind() =>
        CargoCircuitSources.Bind(_npcs.RootElement, _items.RootElement, _world.RootElement, MarketCircuitPolicies.Rations);

    public void Dispose()
    {
        _npcs.Dispose();
        _items.Dispose();
        _world.Dispose();
    }
}
