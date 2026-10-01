using Godot;
using OpenFantasy.Client.Presentation;
using OpenFantasy.Hosting.Content.NativeCargo;

namespace OpenFantasy.Client.Cargo;

/// <summary>
/// Composes each retained merchant's sprite sheet from its own source appearance fields, the exact copied predecessor
/// layer sheets bound by <c>merchant_layers.tres</c> and the copied source palette manifest. It is an exact import of
/// the original appearance, not new art and not production style acceptance; a missing layer or ramp is refused.
/// </summary>
internal sealed class CargoMerchantAppearances : IDisposable
{
    private const string LayerResource = "res://assets/cargo-circuit/merchant_layers.tres";
    private const string PaletteManifest = "res://assets/cargo-circuit/appearance.json";
    private readonly Dictionary<string, NativeCargoAppearance> _appearances;
    private readonly Dictionary<string, ImageTexture> _composed = [];
    private readonly Resource _layers;
    private readonly FarmPaperdollComposer _composer;

    internal CargoMerchantAppearances(IEnumerable<NativeCargoStall> stalls)
    {
        ArgumentNullException.ThrowIfNull(stalls);
        _appearances = stalls.ToDictionary(stall => stall.MerchantSourceId, stall => stall.Appearance);
        _layers = GD.Load<Resource>(LayerResource) ?? throw new InvalidDataException("The retained merchant layer binding is missing.");
        string palette = Godot.FileAccess.GetFileAsString(PaletteManifest);
        if (string.IsNullOrEmpty(palette)) throw new InvalidDataException("The retained merchant palette manifest is missing.");
        _composer = new FarmPaperdollComposer(palette);
    }

    /// <summary>The composed sheet of one retained merchant, by its source identity.</summary>
    internal Texture2D Texture(string merchantSourceId)
    {
        if (_composed.TryGetValue(merchantSourceId, out ImageTexture? texture)) return texture;
        if (!_appearances.TryGetValue(merchantSourceId, out NativeCargoAppearance? appearance))
            throw new InvalidDataException("No retained source appearance is bound for that merchant.");
        texture = _composer.Compose(CargoPaperdollPlan.Steps(appearance)
            .Select(step => new FarmPaperdollLayer(FarmVisualAssets.Texture(_layers, step.LayerStem), step.PaletteFamily,
                step.PaletteIndex)).ToArray());
        _composed.Add(merchantSourceId, texture);
        return texture;
    }

    public void Dispose()
    {
        foreach (ImageTexture texture in _composed.Values) texture.Dispose();
        _composed.Clear();
    }
}
