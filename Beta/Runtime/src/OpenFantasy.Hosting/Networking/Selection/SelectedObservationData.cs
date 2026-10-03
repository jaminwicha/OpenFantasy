using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;

namespace OpenFantasy.Hosting.Networking.Selection;

internal static class SelectedObservationOptions
{
    internal static JsonSerializerOptions Create(bool includeMaterialDescriptions = false)
    {
        var options = PlayerViewCodec.CreateOptions(includeMaterialDescriptions);
        options.Converters.Add(new WitnessConverter());
        options.Converters.Add(new ObservationValueConverter<LocalControlledActorView, ControlledActorData>(includeMaterialDescriptions ? ControlledActorData.FromDescribed : ControlledActorData.From, value => value.ToView()));
        options.Converters.Add(new ObservationValueConverter<LocalControlledCraftPreparation, CraftPreparationData>(CraftPreparationData.From, value => value.ToView()));
        options.Converters.Add(new ObservationValueConverter<LocalControlledQuestPreparation, QuestPreparationData>(QuestPreparationData.From, value => value.ToView()));
        options.Converters.Add(new ObservationValueConverter<LocalControlledConstructionPreparation, ConstructionPreparationData>(ConstructionPreparationData.From, value => value.ToView()));
        return options;
    }
    private sealed class WitnessConverter : JsonConverter<WorldControlWitness>
    {
        public override WorldControlWitness Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String || reader.ValueSpan.Length > 4 * ((WorldControlWitnessCodecV1.MaximumByteLength + 2) / 3))
                throw new FormatException("Oversized control witness.");
            byte[] bytes = reader.GetBytesFromBase64();
            return WorldControlWitnessCodecV1.Decode(bytes);
        }
        public override void Write(Utf8JsonWriter writer, WorldControlWitness value, JsonSerializerOptions options) =>
            writer.WriteBase64StringValue(WorldControlWitnessCodecV1.Encode(value));
    }
}

internal sealed record ControlledActorData(EntityId WorldId, EntityId BranchId, EntityId ActorId,
    WorldControlCapabilities Capabilities, WorldInstant Calendar, bool HasReadingHold, bool IsResolvingConsequences,
    bool IsRegional, ReadOnlyCollection<LocalActorView> Actors, LocalRegionObservation? Regional,
    LocalControlledCombatView? Combat, LocalControlledRetreatView? Retreat, LocalControlledRecoveryView? Recovery,
    LocalControlledCareDispositionView? CareDisposition, LocalInventoryView? Inventory,
    LocalMaterialProposalView? MaterialProposals, LocalControlledTradeView? Trade,
    LocalControlledProductionView? Production, LocalControlledConversationView? Conversation,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] LocalMaterialDescriptions? MaterialDescriptions = null)
{
    internal static ControlledActorData FromDescribed(LocalControlledActorView view) => From(view) with { MaterialDescriptions = view.MaterialDescriptions };
    internal static ControlledActorData From(LocalControlledActorView view) => new(view.WorldId, view.BranchId,
        view.ActorId, view.Capabilities, view.Calendar, view.HasReadingHold, view.IsResolvingConsequences, view.IsRegional,
        view.Actors, view.Regional, view.Combat, view.Retreat, view.Recovery, view.CareDisposition, view.Inventory,
        view.MaterialProposals, view.Trade, view.Production, view.Conversation);
    internal LocalControlledActorView ToView() => new(WorldId, BranchId, ActorId, Capabilities, Calendar,
        HasReadingHold, IsResolvingConsequences, IsRegional, Actors, Regional, Combat, Retreat, Recovery,
        CareDisposition, Inventory, MaterialProposals, Trade, Production, Conversation, MaterialDescriptions);
}

internal sealed record CraftPreparationData(ReadOnlyCollection<LocalCraftRecipeView> Recipes, ulong ProductionRevision,
    EntityId CustodianId, ulong StorageRevision, ReadOnlyCollection<LocalItemLotView> Ingredients)
{
    internal static CraftPreparationData From(LocalControlledCraftPreparation value) => new(value.Recipes,
        value.ProductionRevision, value.CustodianId, value.StorageRevision, value.Ingredients);
    internal LocalControlledCraftPreparation ToView() => new(Recipes, ProductionRevision, CustodianId, StorageRevision, Ingredients);
}

internal sealed record GiverRevisionData(EntityId ActorId, ulong Revision);
internal sealed record QuestPreparationData(ulong ProgressionRevision, ulong InventoryRevision,
    ReadOnlyCollection<LocalQuestView> Quests, ReadOnlyCollection<LocalItemLotView> DeliveryLots,
    ReadOnlyCollection<GiverRevisionData> GiverRevisions)
{
    internal static QuestPreparationData From(LocalControlledQuestPreparation value) => new(value.ProgressionRevision,
        value.InventoryRevision, value.Quests, value.DeliveryLots,
        Array.AsReadOnly(value.CapturedGiverRevisions.OrderBy(pair => pair.Key).Select(pair => new GiverRevisionData(pair.Key, pair.Value)).ToArray()));
    internal LocalControlledQuestPreparation ToView() => new(ProgressionRevision, InventoryRevision, Quests,
        DeliveryLots, GiverRevisions.ToDictionary(value => value.ActorId, value => value.Revision));
}

internal sealed record ConstructionPreparationData(EntityId ProjectId, ReadOnlyCollection<WorkConstructionIntent> Choices)
{
    internal static ConstructionPreparationData From(LocalControlledConstructionPreparation value) => new(value.ProjectId, value.Choices);
    internal LocalControlledConstructionPreparation ToView() => new(ProjectId, Choices);
}
