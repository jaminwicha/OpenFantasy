using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Networking.Selection;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Client.Cargo;
using OpenFantasy.Hosting.Tests.Networking.Selection;

namespace OpenFantasy.Hosting.Tests.Networking.CarePermissionDescriptions;

public sealed class CarePermissionDescriptionTests
{
    [Fact]
    public void Patient_terms_come_from_each_exact_grant_and_remedy_without_supplier_inventory()
    {
        using var fixture = new SelectedCodecFixture(WorldControlCapabilities.CareDisposition);
        var permissions = fixture.Frame.Observation.ControlledView!.CareDisposition!.PermissionOffers;
        Assert.True(permissions.Count >= 3);
        Assert.Contains(permissions, permission => permission.Description!.DeliveryMode == RestorativeDeliveryMode.LivingAssistance);
        Assert.Contains(permissions, permission => permission.Description!.DeliveryMode == RestorativeDeliveryMode.IncapacitatedAssistance);
        foreach (var permission in permissions)
        {
            var grant = fixture.State.Care!.Ordinary!.GetPermission(permission.PermissionId);
            var definition = fixture.State.Care.Ordinary.GetDefinition(grant.UseDefinitionId);
            var remedy = fixture.State.Care.GetRemedy(definition.RemedyDefinitionId);
            var description = Assert.IsType<LocalCarePermissionDescription>(permission.Description);
            Assert.Equal(definition.DefinitionId, description.UseDefinitionId);
            Assert.Equal(remedy.Item.DefinitionId, description.ItemDefinitionId);
            Assert.Equal(remedy.RestoredHp, description.HealHitPoints);
            Assert.Equal(definition.ExplorationMilliseconds, description.ExplorationMilliseconds);
            Assert.Equal(definition.DeliveryMode, description.DeliveryMode);
            Assert.Equal("Potion", description.ItemName);
            Assert.Equal(grant.HelperActorId, permission.HelperActorId);
        }
        Assert.Null(fixture.Frame.Observation.ControlledView.Inventory);
        Assert.Null(fixture.Frame.Observation.ControlledView.Recovery);
        Assert.Null(fixture.Frame.Observation.ControlledView.Combat);
        Assert.Null(fixture.Frame.Observation.ControlledView.Production);
        Assert.Same(fixture.State, fixture.Authority.ReadStateForHost());
    }

    [Fact]
    public void Home_seven_retains_known_terms_while_legacy_encoders_and_diagnostics_keep_their_shape()
    {
        var world = CargoTestWorld.Genesis;
        var view = new LocalWorldView(world, CargoTestWorld.Player(world));
        Assert.NotEmpty(view.Restorative!.PermissionOffers);
        foreach (var encode in new Func<LocalWorldView, byte[]>[]
        {
            PlayerViewCodec.Encode, PlayerViewCodec.EncodeWithMaterialDescriptions, PlayerViewCodec.EncodeWithConstruction
        })
        {
            byte[] bytes = encode(view);
            var restored = PlayerViewCodec.Decode(bytes);
            Assert.All(restored.Restorative!.PermissionOffers, permission => Assert.Null(permission.Description));
            Assert.Equal(bytes, encode(restored));
            Assert.Equal(JsonSerializer.Serialize(view), JsonSerializer.Serialize(restored));
        }
        byte[] described = PlayerViewCodec.EncodeWithCarePermissionDescriptions(view);
        Assert.Equal(7, JsonNode.Parse(Expand(described))!["Version"]!.GetValue<int>());
        var replica = PlayerViewCodec.Decode(described);
        Assert.Equal(view.Restorative.PermissionOffers, replica.Restorative!.PermissionOffers);
        Assert.NotNull(replica.MaterialDescriptions);
        Assert.NotNull(replica.Structures);
        Assert.Equal(described, PlayerViewCodec.EncodeWithCarePermissionDescriptions(replica));
        Assert.Equal(PlayerViewCodec.Encode(view), PlayerViewCodec.Encode(replica));
        Assert.Equal(JsonSerializer.Serialize(view), JsonSerializer.Serialize(replica));
    }

    [Theory]
    [InlineData(WorldControlCapabilities.Recovery)]
    [InlineData(WorldControlCapabilities.CareDisposition)]
    [InlineData(WorldControlCapabilities.Recovery | WorldControlCapabilities.CareDisposition)]
    [InlineData(WorldControlCapabilities.PrivateHistory)]
    public void Selected_terms_follow_care_disposition_independently_of_recovery_and_other_facets(
        WorldControlCapabilities capabilities)
    {
        using var fixture = new SelectedCodecFixture(capabilities);
        var frame = fixture.Frame.WithConstruction().WithCarePermissionDescriptions();
        byte[] bytes = PlayerSelectedObservationCodec.Encode(frame);
        var replica = PlayerSelectedObservationCodec.Decode(bytes);
        Assert.Equal(bytes, PlayerSelectedObservationCodec.Encode(replica));
        var view = replica.Observation.ControlledView!;
        bool care = capabilities.HasFlag(WorldControlCapabilities.CareDisposition);
        Assert.Equal(care, view.CareDisposition is not null);
        Assert.Equal(capabilities.HasFlag(WorldControlCapabilities.Recovery), view.Recovery is not null);
        if (care)
        {
            Assert.Equal(4, replica.PresentationVersion);
            Assert.NotEmpty(view.CareDisposition!.PermissionOffers);
            Assert.All(view.CareDisposition.PermissionOffers, permission => Assert.NotNull(permission.Description));
        }
        Assert.Null(view.Inventory);
        Assert.Null(view.Production);
        Assert.Null(view.Combat);
        foreach (var old in new[] { fixture.Frame, fixture.Frame.WithMaterialDescriptions(), fixture.Frame.WithConstruction() })
        {
            byte[] legacy = PlayerSelectedObservationCodec.Encode(old);
            var restored = PlayerSelectedObservationCodec.Decode(legacy);
            Assert.Equal(old.PresentationVersion, restored.PresentationVersion);
            Assert.Equal(legacy, PlayerSelectedObservationCodec.Encode(restored));
            if (restored.Observation.ControlledView!.CareDisposition is { } disposition)
                Assert.All(disposition.PermissionOffers, permission => Assert.Null(permission.Description));
        }
    }

    [Fact]
    public void Care_versions_reject_downgrades_missing_terms_foreign_withdrawal_and_unsupported_delivery()
    {
        var world = CargoTestWorld.Genesis;
        var original = JsonNode.Parse(Expand(PlayerViewCodec.EncodeWithCarePermissionDescriptions(
            new(world, CargoTestWorld.Player(world)))))!;
        foreach (Action<JsonNode> mutate in new Action<JsonNode>[]
        {
            node => node["Version"] = 6,
            node => Permission(node).AsObject().Remove("Description"),
            node => Permission(node)["Description"]!["DeliveryMode"] = (int)RestorativeDeliveryMode.SelfUse,
            node => Permission(node)["Description"]!["HealHitPoints"] = 0,
            node => Permission(node)["Description"]!["ExplorationMilliseconds"] = 1,
            node => Permission(node)["Description"]!["ItemName"] = " ",
            node => Permission(node)["Intent"]!["PermissionId"] = Permission(node)["HelperActorId"]!.DeepClone()
        })
        {
            var altered = original.DeepClone();
            mutate(altered);
            Assert.Throws<FormatException>(() => PlayerViewCodec.Decode(Wire(altered)));
        }
        using var fixture = new SelectedCodecFixture(WorldControlCapabilities.CareDisposition);
        var selected = JsonNode.Parse(Expand(PlayerSelectedObservationCodec.Encode(
            fixture.Frame.WithConstruction().WithCarePermissionDescriptions())))!;
        var downgraded = selected.DeepClone(); downgraded["Version"] = 3;
        Assert.Throws<FormatException>(() => PlayerSelectedObservationCodec.Decode(Wire(downgraded)));
        var missing = selected.DeepClone();
        missing["Value"]!["Observation"]!["ControlledView"]!["CareDisposition"]!["PermissionOffers"]![0]!.AsObject().Remove("Description");
        Assert.Throws<FormatException>(() => PlayerSelectedObservationCodec.Decode(Wire(missing)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Duplicate_choice_identities_are_rejected_before_they_reach_menu_keys(bool selected, bool use)
    {
        JsonNode body;
        JsonArray rows;
        if (selected)
        {
            using var fixture = new SelectedCodecFixture(WorldControlCapabilities.Recovery | WorldControlCapabilities.CareDisposition);
            body = JsonNode.Parse(Expand(PlayerSelectedObservationCodec.Encode(
                fixture.Frame.WithConstruction().WithCarePermissionDescriptions())))!;
            rows = body["Value"]!["Observation"]!["ControlledView"]![use ? "Recovery" : "CareDisposition"]!
                [use ? "SelfUseOffers" : "PermissionOffers"]!.AsArray();
        }
        else
        {
            var world = CargoTestWorld.Genesis;
            body = JsonNode.Parse(Expand(PlayerViewCodec.EncodeWithCarePermissionDescriptions(
                new(world, CargoTestWorld.Player(world)))))!;
            rows = body["View"]!["Restorative"]![use ? "SelfUseOffers" : "PermissionOffers"]!.AsArray();
        }
        Assert.NotEmpty(rows);
        rows.Add(rows[0]!.DeepClone());
        var error = selected
            ? Assert.Throws<FormatException>(() => PlayerSelectedObservationCodec.Decode(Wire(body)))
            : Assert.Throws<FormatException>(() => PlayerViewCodec.Decode(Wire(body)));
        Assert.Equal(use ? "Duplicate care use definition." : "Duplicate care permission.", error.Message);
    }

    [Fact]
    public void Republishing_older_views_does_not_invent_known_terms_or_omitted_construction()
    {
        var world = CargoTestWorld.Genesis;
        var view = new LocalWorldView(world, CargoTestWorld.Player(world));
        byte[] earlier = PlayerViewCodec.EncodeWithMaterialDescriptions(view);
        var old = PlayerViewCodec.Decode(earlier);
        Assert.Null(old.Structures);
        Assert.Equal(earlier, PlayerViewCodec.EncodeWithCarePermissionDescriptions(old));
        using var fixture = new SelectedCodecFixture(WorldControlCapabilities.CareDisposition);
        byte[] selected = PlayerSelectedObservationCodec.Encode(fixture.Frame.WithMaterialDescriptions());
        var restored = PlayerSelectedObservationCodec.Decode(selected);
        Assert.Equal(selected, PlayerSelectedObservationCodec.Encode(restored.WithCarePermissionDescriptions()));
        Assert.Null(restored.Observation.ControlledView!.Structures);
    }

    private static JsonNode Permission(JsonNode node) => node["View"]!["Restorative"]!["PermissionOffers"]![0]!;
    private static byte[] Expand(byte[] bytes) => ObservationCompression.IsCompressed(bytes) ? ObservationCompression.Decode(bytes) : bytes;
    private static byte[] Wire(JsonNode value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value.ToJsonString());
        return bytes.Length <= PlayerViewCodec.MaximumBodyBytes ? bytes : ObservationCompression.Encode(bytes);
    }
}
