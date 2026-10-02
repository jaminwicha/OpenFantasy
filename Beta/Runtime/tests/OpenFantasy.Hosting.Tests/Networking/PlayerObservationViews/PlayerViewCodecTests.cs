using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;
using OpenFantasy.Hosting.Simulation.EncounterOutcome;
using OpenFantasy.Hosting.Simulation.Retreat;
using OpenFantasy.Hosting.Simulation.RoadThreat;
using OpenFantasy.Hosting.Tests.Client.Cargo;
using OpenFantasy.Hosting.Tests.WorldStorage;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.Networking.PlayerObservationViews;

public sealed class PlayerViewCodecTests(ITestOutputHelper output)
{
    [Fact]
    public void Ordinary_and_production_regional_projections_preserve_their_existing_diagnostic_bytes()
    {
        AssertParity(new LocalWorldView(Genesis(), Id(10)));
        WorldSnapshot cargo = CargoTestWorld.Genesis;
        EntityId player = CargoTestWorld.Player(cargo);
        LocalWorldView original = new(cargo, player, regions: new LocalRegionObserver(player));
        LocalWorldView decoded = AssertParity(original);
        output.WriteLine("ProductionRegionalBodyBytes=" + PlayerViewCodec.Encode(original).Length);
        Assert.True(decoded.IsRegional);
        Assert.NotNull(decoded.Regional);
        Assert.Equal(JsonSerializer.Serialize(original.Regional), JsonSerializer.Serialize(decoded.Regional));
        Assert.DoesNotContain("Regional", JsonSerializer.Serialize(decoded), StringComparison.Ordinal);
        Assert.DoesNotContain("Captured", JsonSerializer.Serialize(decoded), StringComparison.Ordinal);
        Assert.Throws<NotSupportedException>(() => ((IList<LocalActorView>)decoded.Actors).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<LocalObservedRegion>)decoded.Regional.Regions).Clear());
    }

    [Fact]
    public void Unsupported_regional_floor_is_distinct_from_nonregional_presentation()
    {
        LocalWorldView original = new(Genesis(), Id(10));
        LocalWorldView unsupported = (WorldViewData.From(original) with { IsRegional = true, Regional = null }).ToView();
        LocalWorldView decoded = AssertParity(unsupported);
        Assert.True(decoded.IsRegional);
        Assert.Null(decoded.Regional);
        Assert.False(AssertParity(original).IsRegional);
    }

    [Fact]
    public void Every_projection_family_and_prepared_action_retains_exact_terms()
    {
        LocalWorldView original = RichView();
        LocalWorldView decoded = AssertParity(original);
        Assert.NotNull(decoded.Worksite);
        Assert.NotNull(decoded.Liquids);
        Assert.NotNull(decoded.Crafting);
        Assert.NotNull(decoded.Care);
        Assert.NotNull(decoded.Retreat);
        Assert.NotNull(decoded.EncounterOutcome);
        Assert.Equal(original.Retreat!.Offer!.WalkingUnits, decoded.Retreat.Offer!.WalkingUnits);
        Assert.Equal(original.Worksite!.Terrain[0].IsSolid(new CellCoordinate(-2, -2, -1)),
            decoded.Worksite.Terrain[0].IsSolid(new CellCoordinate(-2, -2, -1)));
        Assert.Equal(original.Care!.Offers[0].Intent, decoded.Care.Offers[0].Intent);
        Assert.Equal(original.Restorative!.SelfUseOffers[0].Intent, decoded.Restorative!.SelfUseOffers[0].Intent);
        Assert.Equal(original.EncounterOutcome!.Acknowledgment, decoded.EncounterOutcome.Acknowledgment);

        LocalMaterialProposalPreparation before = original.MaterialProposals!.Preparation!;
        LocalMaterialProposalPreparation after = decoded.MaterialProposals!.Preparation!;
        var gift = before.Prepare(ConsentedMaterialLotKind.CountedSource, Id(70), Id(11), 2)!;
        Assert.Equal(gift, after.Prepare(ConsentedMaterialLotKind.CountedSource, Id(70), Id(11), 2));
        Assert.True(after.CanStillPropose(gift));
        Assert.Null(after.Prepare(ConsentedMaterialLotKind.CountedSource, Id(70), Id(10), 2));
        Assert.Equal(original.MaterialProposals.Offers[0].GrantHash, decoded.MaterialProposals.Offers[0].GrantHash);
        Assert.Equal(original.MaterialProposals.Offers[0].PrepareAcceptance(), decoded.MaterialProposals.Offers[0].PrepareAcceptance());
        Assert.Equal("{}", JsonSerializer.Serialize(decoded.MaterialProposals.Offers[0].GrantHash));

        var stallBefore = original.Markets!.Stalls[0];
        var stallAfter = decoded.Markets!.Stalls[0];
        Assert.Equal(stallBefore.PrepareShowing(Id(70), 2, 4), stallAfter.PrepareShowing(Id(70), 2, 4));
        Assert.NotNull(stallAfter.PrepareShowing(Id(70), 2, 4));
        Assert.Null(stallAfter.PrepareShowing(Id(70), 7, 4));
        var delivery = new TrustedCommandDelivery(Id(100), Id(110));
        byte[] PreparedAcceptance(LocalWorldView view) => WorldActionCodec.Encode(new WorldActionCommand(Id(999),
            delivery.PrincipalId, delivery.SessionId, view.PlayerActorId, view.WorldId, view.BranchId,
            view.Actors.Single(actor => actor.ActorId == view.PlayerActorId).PresenceRevision,
            view.PaidQuotes[0].PrepareAcceptance(Id(999), delivery, 2)));
        Assert.Equal(PreparedAcceptance(original), PreparedAcceptance(decoded));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("version")]
    [InlineData("null-name")]
    [InlineData("unknown-enum")]
    [InlineData("derived")]
    [InlineData("actor-scope")]
    [InlineData("proposal-scope")]
    [InlineData("regional-player")]
    [InlineData("regional-length")]
    [InlineData("regional-opening")]
    [InlineData("market-step")]
    [InlineData("route-overflow")]
    [InlineData("regional-overlap")]
    [InlineData("regional-water-height")]
    public void Malformed_or_contradictory_bodies_are_refused(string mutation)
    {
        byte[] bytes = PlayerViewCodec.Encode(RichView());
        string json = Encoding.UTF8.GetString(bytes);
        if (mutation == "duplicate")
        {
            bytes = Encoding.UTF8.GetBytes(json.Replace("\"Version\":1,", "\"Version\":1,\"Version\":1,", StringComparison.Ordinal));
        }
        else
        {
            JsonNode body = JsonNode.Parse(bytes)!;
            JsonNode view = body["View"]!;
            switch (mutation)
            {
                case "missing": view.AsObject().Remove("Calendar"); break;
                case "unknown": view["AuthorityRoot"] = "private"; break;
                case "version": body["Version"] = 999; break;
                case "null-name": view["Actors"]![0]!["DisplayName"] = null; break;
                case "unknown-enum": view["Actors"]![0]!["ControllerKind"] = 999; break;
                case "derived": view["Actors"]![0]!["Feet"]!["Cell"]!["X"] = 99; break;
                case "actor-scope": view["PlayerActorId"]!["Low"] = 999; break;
                case "proposal-scope": view["MaterialProposals"]!["Preparation"]!["BranchId"]!["Low"] = 999; break;
                case "regional-player": view["Regional"]!["PlayerActorId"]!["Low"] = 999; break;
                case "regional-length": view["Regional"]!["Regions"]![0]!["Ground"]!.AsArray().RemoveAt(0); break;
                case "regional-opening": view["Regional"]!["Regions"]![0]!["OpeningDepth"] = Convert.ToBase64String(Enumerable.Repeat((byte)99, 64).ToArray()); break;
                case "market-step": view["Markets"]!["Stalls"]![0]!["CapturedAdvancingStep"] = 999; break;
                case "route-overflow": view["Retreat"]!["Offer"]!["SampleDelay"] = ulong.MaxValue; break;
                case "regional-overlap": view["Regional"]!["Regions"]!.AsArray().Add(view["Regional"]!["Regions"]![0]!.DeepClone()); break;
                case "regional-water-height": view["Regional"]!["Water"]!.AsArray().Add(JsonSerializer.SerializeToNode(new LocalObservedWater(0, 0, 100, long.MaxValue))); break;
            }
            bytes = JsonSerializer.SerializeToUtf8Bytes(body);
        }
        Assert.Throws<FormatException>(() => PlayerViewCodec.Decode(bytes));
    }

    [Fact]
    public void Oversized_empty_deep_and_trailing_bodies_are_refused_before_publication()
    {
        Assert.Throws<FormatException>(() => PlayerViewCodec.Decode([]));
        Assert.Throws<FormatException>(() => PlayerViewCodec.Decode(new byte[PlayerViewCodec.MaximumBodyBytes + 1]));
        byte[] deep = Encoding.UTF8.GetBytes(new string('[', PlayerViewCodec.MaximumDepth + 1)
            + new string(']', PlayerViewCodec.MaximumDepth + 1));
        Assert.Throws<FormatException>(() => PlayerViewCodec.Decode(deep));
        byte[] encoded = PlayerViewCodec.Encode(new LocalWorldView(Genesis(), Id(10)));
        Assert.Throws<FormatException>(() => PlayerViewCodec.Decode(encoded.Concat(" "u8.ToArray()).ToArray()));
    }

    [Fact]
    public void Restorative_value_factory_validates_advice_without_creating_a_grant()
    {
        var blocked = RestorativeAvailability.FromObservation(RestorativeAvailabilityReason.FullHealth,
            WorldActionRejection.Unavailable, null);
        Assert.False(blocked.CanUse);
        Assert.Null(blocked.Intent);
        Assert.Throws<ArgumentException>(() => RestorativeAvailability.FromObservation(
            RestorativeAvailabilityReason.Available, WorldActionRejection.None, null));
        Assert.Throws<ArgumentException>(() => RestorativeAvailability.FromObservation(
            (RestorativeAvailabilityReason)999, WorldActionRejection.Unavailable, null));
        Assert.Throws<ArgumentException>(() => RestorativeAvailability.FromObservation(
            RestorativeAvailabilityReason.FullHealth, WorldActionRejection.None, null));
    }


    [Theory]
    [InlineData("length")]
    [InlineData("padding")]
    [InlineData("negative-count")]
    [InlineData("oversized-count")]
    [InlineData("missing-count")]
    [InlineData("alternate-base64")]
    public void Packed_occupancy_requires_exact_count_length_padding_and_canonical_spelling(string mutation)
    {
        LocalWorldView source = RichView();
        LocalWorksiteView work = source.Worksite!;
        var single = new LocalWorksiteTerrainView(new TerrainRegionBounds(new CellCoordinate(0, 0, 0), 1, 1, 1),
            1, Frozen(true));
        LocalWorldView view = (WorldViewData.From(source) with
        {
            Worksite = new LocalWorksiteView(Frozen(single), work.Members, work.ExcavationTargets, work.Tools,
                work.PlayerEffort, work.CanManipulate, work.IsResolvingConsequences, work.HasPhysicalConstruction,
                work.SupportsAssistedTraversal)
        }).ToView();
        byte[] encoded = PlayerViewCodec.Encode(view);
        Assert.True(PlayerViewCodec.Decode(encoded).Worksite!.Terrain[0].IsSolid(new CellCoordinate(0, 0, 0)));
        JsonNode body = JsonNode.Parse(encoded)!;
        JsonNode occupancy = body["View"]!["Worksite"]!["Terrain"]![0]!["Occupancy"]!;
        switch (mutation)
        {
            case "length": occupancy["Bits"] = ""; break;
            case "padding": occupancy["Bits"] = Convert.ToBase64String(new byte[] { 0x81 }); break;
            case "negative-count": occupancy["Count"] = -1; break;
            case "oversized-count": occupancy["Count"] = PlayerViewCodec.MaximumCollectionItems + 1; break;
            case "missing-count": occupancy.AsObject().Remove("Count"); break;
            // Decodes to the same single byte with a permissive base64 reader, but has noncanonical unused bits.
            case "alternate-base64": occupancy["Bits"] = "AR=="; break;
        }
        byte[] malformed = JsonSerializer.SerializeToUtf8Bytes(body);
        Assert.Throws<FormatException>(() => PlayerViewCodec.Decode(malformed));
    }

    private static LocalWorldView AssertParity(LocalWorldView original)
    {
        string diagnostic = JsonSerializer.Serialize(original);
        byte[] bytes = PlayerViewCodec.Encode(original);
        LocalWorldView decoded = PlayerViewCodec.Decode(bytes);
        Assert.Equal(diagnostic, JsonSerializer.Serialize(decoded));
        Assert.Equal(bytes, PlayerViewCodec.Encode(decoded));
        Assert.Equal(diagnostic, JsonSerializer.Serialize(original));
        return decoded;
    }

    private static ReadOnlyCollection<T> Frozen<T>(params T[] values) => Array.AsReadOnly(values);

    private static LocalWorldView RichView()
    {
        WorldSnapshot world = Genesis();
        var source = new LocalWorldView(world, Id(10), regions: new LocalRegionObserver(Id(10)));
        var player = source.Actors[0];
        ContentHash hash = ContentHash.Compute("observation-value-fixture"u8);
        var definition = new CountedItemDefinition(Id(80), hash);
        var bounds = world.Space.Regions[0].Bounds;
        var occupancy = Frozen(world.Space.Regions[0].Cells.Select(cell => cell.IsSolid).ToArray());
        var geometry = new MillimetreBox(0, 0, 0, 100, 200, 300);
        var use = new UseRestorativeItemIntent(Id(10), Id(800), hash, null, null, null, player.PresenceRevision,
            2, source.PlayerInventory.Revision, 3, new IngredientRequest(IngredientLotKind.Source, Id(70), 0, 1));
        var preparation = new LocalMaterialProposalPreparation(world.WorldId, world.BranchId, Id(10), Id(60),
            new Dictionary<EntityId, EntityId> { [Id(11)] = Id(61) },
            [new(ConsentedMaterialLotKind.CountedSource, Id(70), definition, 0, 6)], 1300);
        var terms = new ConsentedMaterialTerms(world.WorldId, world.BranchId, Id(11), Id(10),
            ConsentedMaterialLotKind.CountedSource, Id(720), definition, Id(61), Id(60), 1, 5);
        var proposal = new MaterialProposalView(Id(721), terms, CommandContentHash.FromBytes(hash.ToByteArray()),
            500, MaterialProposalStatus.Open, null, false, 0);
        var quote = new PaidQuoteView(Id(730), Id(740), Id(11), Id(10), false, SpotQuoteSide.IssuerSells,
            definition, SpotLotKind.CountedSource, Id(731), 3, Id(61), Id(60), Id(750), hash, Id(751), Id(752),
            4, Frozen(new SpotFee(Id(753), 1)), 6, 10, 70, hash, PaidQuoteStatus.Open, null, null, null, null, 0);
        return (WorldViewData.From(source) with
        {
            Encounter = new(Id(900), 77, 123, true, false, true),
            EncounterLifecycle = new(EncounterLifecyclePhase.Active, 1, 0, true, hash,
                Frozen(new LocalEncounterEntryView(Id(901), "Encounter", Id(11), 0, Id(900), true))),
            EncounterActions = new(Id(900), 77, player.PresenceRevision, 0, 20, 10, 100,
                LocalEncounterActionBlockedReason.None,
                Frozen(new LocalEncounterAbilityOffer(Id(902), "Strike", Id(11), 256, 2, 50, LocalEncounterActionBlockedReason.None))),
            EncounterActors = Frozen(new LocalEncounterActorView(Id(10), 20, 100, false, 1),
                new LocalEncounterActorView(Id(11), 0, 100, true, 2)),
            Retreat = new(new LocalRetreatOffer(Id(900), 77, player.PresenceRevision, player.Feet, player.Feet.Offset(32, 0, 0),
                Frozen(EncounterRetreatDirection.East), 10, 1, LocalRetreatBlockedReason.None), null, null, true),
            EncounterOutcome = new(LocalOutcomeStage.AwaitingYourReading, Id(900), EncounterOutcomeResult.Victory, 1,
                LocalOutcomeStanding.YourSideStands,
                [new(Id(10), player.DisplayName, 1, true, true, new LocalOutcomeCondition(20, 100)),
                    new(Id(11), "Resident 1", 2, false, false, null)],
                new AcknowledgeEncounterOutcomeIntent(Id(900), hash), 1, 1, false, LocalOutcomeRestriction.AwaitingCompletion, 10, null),
            RoadThreats = Frozen(new LocalRoadThreatView(Id(11), "Resident 1", 2, 50, 100, Id(901), "Encounter", 256, 512, LocalRoadThreatState.Watching)),
            Care = new([new(Id(11), "Resident 1", new AcknowledgeCareIntent(Id(780), Id(900), 4, hash), true, WorldActionRejection.None)], LocalCareStatus.Available),
            Restorative = new([new(Id(800), Id(80), "Potion", 40, 40, 1000,
                RestorativeAvailability.FromObservation(RestorativeAvailabilityReason.Available, WorldActionRejection.None, use))],
                [new(Id(801), Id(11), "Resident 1", true, true, RestorativeAvailabilityReason.Available,
                    WorldActionRejection.None, new RevokeCarePermissionIntent(Id(801), 5, hash))]),
            MaterialProposals = new(Frozen(proposal), preparation),
            PaidQuotes = Frozen(quote),
            Markets = new(20, [new LocalMarketStallView(Id(740), Id(11), definition, "Wood", 6, 50, true,
                new(SpotLotKind.CountedSource, Id(70), 0, 2, 15, 25, true, true),
                Frozen(new LocalMarketLotView(SpotLotKind.CountedSource, Id(70), definition, 0, 6)), Id(752), 20)]),
            Worksite = new(Frozen(new LocalWorksiteTerrainView(bounds, 4, occupancy)),
                Frozen(new LocalWorksiteMemberView(Id(810), 0, 2, 100, ConstructionMemberDisposition.Standing,
                    player.Feet, geometry, geometry.At(player.Feet))),
                Frozen(new LocalExcavationView(Id(811), new CellCoordinate(0, 0, 0), player.Feet, 1, 4, 10, 2, Id(812), 7)),
                Frozen(new LocalExcavationToolView(Id(813), Id(812), 2)), 100, true, false, true, true),
            Liquids = new(Frozen(new LocalLiquidCellView(new CellCoordinate(0, 0, 1), 100)),
                Frozen(new LocalLiquidTerrainView(bounds, 4, occupancy))),
            Crafting = new(3, Frozen(new LocalCraftRecipeView(Id(820), hash, "fixture", Frozen(new LocalCraftTermView(Id(80), 1, RecipeMaterialRole.Input))))),
            Dialogue = new(Id(830), Id(11), "Resident 1", "Hello."),
            Progression = new(3, "Adventurer", 2, 123, 20, 100, 10, 20, 50, "Coins")
        }).ToView();
    }
}
