using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Content.NativeCargo;

namespace OpenFantasy.Hosting.Networking;

/// <summary>Bounded, immutable presentation identities supplied by the admitted host composition.</summary>
public sealed class PlayerPresentationBootstrap
{
    public const int MaximumCargoStalls = 16;
    public const int MaximumLabelBytes = 128;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public PlayerPresentationBootstrap(EntityId wrenActorId, EntityId woodDefinitionId, EntityId? berryDefinitionId,
        bool isRegional, IReadOnlyList<NativeCargoStall> cargoStalls)
    {
        ArgumentNullException.ThrowIfNull(cargoStalls);
        if (wrenActorId == woodDefinitionId || berryDefinitionId == wrenActorId || berryDefinitionId == woodDefinitionId
            || cargoStalls.Count > MaximumCargoStalls || !isRegional && cargoStalls.Count != 0)
            throw new ArgumentException("The presentation identities or cargo bounds are inconsistent.");
        var copy = new List<NativeCargoStall>(cargoStalls.Count);
        var identities = new HashSet<EntityId> { wrenActorId, woodDefinitionId };
        if (berryDefinitionId is { } berry) identities.Add(berry);
        var sources = new HashSet<string>(StringComparer.Ordinal);
        foreach (NativeCargoStall stall in cargoStalls)
        {
            if (copy.Count == MaximumCargoStalls || stall is null || stall.Appearance is null || !identities.Add(stall.MarketId)
                || !identities.Add(stall.MerchantActorId) || !sources.Add(stall.MerchantSourceId))
                throw new ArgumentException("Cargo stalls require unique market, merchant and source identities.");
            ValidateLabel(stall.MerchantSourceId);
            ValidateLabel(stall.MerchantName);
            ValidateLabel(stall.PlaceSourceId);
            ValidateLabel(stall.PlaceName);
            NativeCargoAppearance appearance = stall.Appearance;
            foreach (int field in new[] { appearance.Race, appearance.Skin, appearance.HairStyle, appearance.HairColor,
                appearance.Outfit, appearance.OutfitA, appearance.OutfitB, appearance.Accessory })
                if (field is < 0 or > 255) throw new ArgumentException("A cargo appearance field exceeds its presentation bound.");
            if (copy.Count != 0 && copy[^1].MarketId.CompareTo(stall.MarketId) >= 0)
                throw new ArgumentException("Cargo stalls must be in strictly increasing market identity order.");
            copy.Add(stall);
        }
        WrenActorId = wrenActorId;
        WoodDefinitionId = woodDefinitionId;
        BerryDefinitionId = berryDefinitionId;
        IsRegional = isRegional;
        CargoStalls = copy.AsReadOnly();
    }

    public EntityId WrenActorId { get; }
    public EntityId WoodDefinitionId { get; }
    public EntityId? BerryDefinitionId { get; }
    public bool IsRegional { get; }
    public IReadOnlyList<NativeCargoStall> CargoStalls { get; }

    private static void ValidateLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLabelBytes || value.Any(char.IsControl)
            || StrictUtf8.GetByteCount(value) > MaximumLabelBytes)
            throw new ArgumentException("A cargo presentation label is missing or exceeds its text bound.");
    }
}
