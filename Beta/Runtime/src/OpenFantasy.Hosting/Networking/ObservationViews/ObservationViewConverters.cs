using System.Text.Json;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;
using OpenFantasy.Hosting.Simulation.EncounterOutcome;
using OpenFantasy.Hosting.Simulation.Retreat;

namespace OpenFantasy.Hosting.Networking.ObservationViews;

internal static class ObservationViewConverters
{
    internal static void AddTo(JsonSerializerOptions options, bool includeMaterialDescriptions = false, bool includeConstruction = false)
    {
        options.Converters.Add(new ObservationValueConverter<LocalWorldView, WorldViewData>(includeConstruction ? WorldViewData.FromConstruction : includeMaterialDescriptions ? WorldViewData.FromDescribed : WorldViewData.From, value => value.ToView()));
        ConstructionObservationConverters.AddTo(options);
        options.Converters.Add(new ObservationValueConverter<LocalMaterialDescriptions, MaterialDescriptionsData>(
            value => new(value.Items), value => new(value.Items)));
        options.Converters.Add(new ObservationValueConverter<LocalCraftRecipeView, CraftRecipeViewData>(CraftRecipeViewData.From, value => value.ToView()));
        options.Converters.Add(new ObservationValueConverter<LocalCraftingView, CraftingViewData>(CraftingViewData.From, value => value.ToView()));
        options.Converters.Add(new ObservationValueConverter<LocalEncounterActionView, EncounterActionViewData>(EncounterActionViewData.From, value => value.ToView()));
        options.Converters.Add(new ObservationValueConverter<LocalMaterialProposalView, MaterialProposalViewData>(MaterialProposalViewData.From, value => value.ToView()));
        options.Converters.Add(new ObservationValueConverter<LocalMarketStallView, MarketStallViewData>(MarketStallViewData.From, value => value.ToView()));
        options.Converters.Add(new ObservationValueConverter<LocalWorksiteView, WorksiteViewData>(WorksiteViewData.From, value => value.ToView()));
        options.Converters.Add(new ObservationValueConverter<LocalWorksiteTerrainView, WorksiteTerrainViewData>(WorksiteTerrainViewData.From, value => value.ToView()));
        options.Converters.Add(new ObservationValueConverter<LocalLiquidTerrainView, LiquidTerrainViewData>(LiquidTerrainViewData.From, value => value.ToView()));
        options.Converters.Add(new ObservationValueConverter<LocalLiquidView, LiquidViewData>(LiquidViewData.From, value => value.ToView()));
        options.Converters.Add(new ObservationValueConverter<LocalRetreatView, RetreatViewData>(RetreatViewData.From, value => value.ToView()));
        options.Converters.Add(new ObservationValueConverter<PaidQuoteView, PaidQuoteViewData>(PaidQuoteViewData.From, value => value.ToView()));
        options.Converters.Add(new ObservationValueConverter<LocalInventoryView, InventoryViewData>(InventoryViewData.From, value => value.ToView()));
        options.Converters.Add(new ObservationValueConverter<LocalCareView, CareViewData>(CareViewData.From, value => value.ToView()));
        options.Converters.Add(new ObservationValueConverter<LocalRestorativeView, RestorativeViewData>(RestorativeViewData.From, value => value.ToView()));
        options.Converters.Add(new ObservationValueConverter<LocalMarketView, MarketViewData>(MarketViewData.From, value => value.ToView()));
        options.Converters.Add(new ObservationValueConverter<LocalEncounterOutcomeView, EncounterOutcomeViewData>(EncounterOutcomeViewData.From, value => value.ToView()));
        options.Converters.Add(new ObservationValueConverter<LocalObservedRegion, ObservedRegionData>(ObservedRegionData.From, value => value.ToView()));
        options.Converters.Add(new ObservationValueConverter<LocalRegionObservation, RegionObservationData>(RegionObservationData.From, value => value.ToView()));
    }
}

internal sealed record MaterialDescriptionsData(System.Collections.ObjectModel.ReadOnlyCollection<LocalMaterialDescription> Items);
