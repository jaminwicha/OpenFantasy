using Godot;
using OpenFantasy.Core.Gathering;

namespace OpenFantasy.Client.Presentation;

/// <summary>Exportable original textures and declared ground-only adaptation, loaded once per farm view.</summary>
internal sealed class FarmVisualAssets : IDisposable
{
    private readonly ImageTexture _wren;
    private readonly Texture2D _squire;
    private readonly int[] _groundIds;
    private Texture2D? _oak;
    private Texture2D? _berry;

    internal FarmVisualAssets()
    {
        Resource resource = GD.Load<Resource>("res://assets/native-farm/farm_visual.tres")
            ?? throw new InvalidDataException("The retained farm visual resource is missing.");
        Atlas = Texture(resource, "tiles");
        _squire = Texture(resource, "squire");
        _groundIds = resource.GetMeta("ground_ids").AsInt32Array();
        if (Atlas.GetWidth() != 256 || Atlas.GetHeight() != 144
            || _squire.GetWidth() != 48 || _squire.GetHeight() != 96 || _groundIds.Length != 1024
            || !resource.GetMeta("source_appearance").AsInt32Array().SequenceEqual(new[] { 0, 5, 5, 13, 8, 10, 10, 3 }))
        {
            throw new InvalidDataException("The farm visual resource differs from its declared source profile.");
        }
        var composer = new FarmPaperdollComposer(resource.GetMeta("palette_json").AsString());
        _wren = composer.ComposeWren(resource);
    }

    internal Texture2D Atlas { get; }
    internal int GroundAt(int x, int y) => _groundIds[y * FarmCanvasCoordinates.RegionWidth + x];
    internal Texture2D ActorTexture(FarmSpriteKind kind) => kind switch
    {
        FarmSpriteKind.Wren => _wren,
        FarmSpriteKind.Squire => _squire,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    internal Texture2D GatherNodeTexture(GatherToolKind kind)
    {
        if (_oak is null || _berry is null)
        {
            Resource resource = GD.Load<Resource>("res://assets/adventure-farm/nodes.tres")
                ?? throw new InvalidDataException("The retained resource-node textures are missing.");
            _oak = Texture(resource, "oak");
            _berry = Texture(resource, "berry");
            if (_oak.GetWidth() != 48 || _oak.GetHeight() != 96 || _berry.GetWidth() != 48 || _berry.GetHeight() != 96)
            {
                throw new InvalidDataException("A retained resource-node sheet has incompatible frame geometry.");
            }
        }
        return kind switch
        {
            GatherToolKind.Axe => _oak,
            GatherToolKind.Sickle => _berry,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    internal static Texture2D Texture(Resource resource, StringName key) => resource.GetMeta(key).AsGodotObject() as Texture2D
        ?? throw new InvalidDataException("A retained farm texture reference is missing: " + key);

    public void Dispose() => _wren.Dispose();
}
