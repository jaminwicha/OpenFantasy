using System.Collections;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Networking.Selection;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Client.Cargo;
using OpenFantasy.Hosting.Tests.Content;
using OpenFantasy.Hosting.Tests.Networking.Selection;

namespace OpenFantasy.Hosting.Tests.Networking.PublicMaterialDescriptions;

public sealed class PublicMaterialDescriptionTests
{
    [Fact]
    public void Home_public_labels_use_exact_content_and_explicit_version_five_preserves_the_legacy_representation()
    {
        var world = CargoTestWorld.Genesis;
        var view = new LocalWorldView(world, CargoTestWorld.Player(world));
        var descriptions = Assert.IsType<LocalMaterialDescriptions>(view.MaterialDescriptions);
        foreach (string name in new[] { "Wood", "Berries", "Copper Axe", "Sickle", "Rations", "Wood offcuts", "Potion" })
            Assert.Contains(descriptions.Items, item => item.DisplayName == name);
        Assert.All(descriptions.Items, item => Assert.True(FarmMaterialDescriptionCatalog.Matches(item)));

        byte[] legacy = PlayerViewCodec.Encode(view);
        var oldReplica = PlayerViewCodec.Decode(legacy);
        Assert.Null(oldReplica.MaterialDescriptions);
        Assert.Equal(legacy, PlayerViewCodec.Encode(oldReplica));
        Assert.DoesNotContain("MaterialDescriptions", Encoding.UTF8.GetString(Expand(legacy)));
        Assert.Equal(JsonSerializer.Serialize(view), JsonSerializer.Serialize(oldReplica));

        byte[] described = PlayerViewCodec.EncodeWithMaterialDescriptions(view);
        Assert.Equal(5, JsonNode.Parse(Expand(described))!["Version"]!.GetValue<int>());
        var replica = PlayerViewCodec.Decode(described);
        Assert.Equal(descriptions.Items, replica.MaterialDescriptions!.Items);
        Assert.Equal(described, PlayerViewCodec.EncodeWithMaterialDescriptions(replica));
        Assert.Equal(legacy, PlayerViewCodec.Encode(replica));
        Assert.Equal(JsonSerializer.Serialize(view), JsonSerializer.Serialize(replica));
    }

    [Theory]
    [InlineData(WorldControlCapabilities.Production)]
    [InlineData(WorldControlCapabilities.Inventory)]
    [InlineData(WorldControlCapabilities.PrivateHistory)]
    public void Selected_descriptions_follow_only_disclosed_facets_and_retain_the_declared_presentation_version(
        WorldControlCapabilities capabilities)
    {
        using var fixture = new SelectedCodecFixture(capabilities);
        var controlled = fixture.Frame.Observation.ControlledView!;
        Assert.Null(controlled.Recovery);
        if (capabilities == WorldControlCapabilities.PrivateHistory)
        {
            Assert.Null(controlled.MaterialDescriptions);
            Assert.NotNull(fixture.Frame.Observation.HomeView.MaterialDescriptions);
        }
        else if (capabilities == WorldControlCapabilities.Inventory)
        {
            Assert.Null(controlled.Production);
            Assert.All(controlled.MaterialDescriptions!.Items, item =>
                Assert.Contains(controlled.Inventory!.Lots, lot => lot.DefinitionId == item.DefinitionId));
        }
        else
        {
            Assert.Null(controlled.Inventory);
            Assert.Contains(controlled.MaterialDescriptions!.Items, item => item.DisplayName == "Berries");
            Assert.Contains(controlled.MaterialDescriptions.Items, item => item.DisplayName == "Potion");
            Assert.Contains(controlled.MaterialDescriptions.Items, item => item.DisplayName == "Rations");
        }
        byte[] legacy = PlayerSelectedObservationCodec.Encode(fixture.Frame);
        var oldReplica = PlayerSelectedObservationCodec.Decode(legacy);
        Assert.Equal(1, oldReplica.PresentationVersion);
        Assert.Null(oldReplica.Observation.HomeView.MaterialDescriptions);
        Assert.Null(oldReplica.Observation.ControlledView!.MaterialDescriptions);
        Assert.Equal(legacy, PlayerSelectedObservationCodec.Encode(oldReplica));

        var describedFrame = fixture.Frame.WithMaterialDescriptions();
        byte[] described = PlayerSelectedObservationCodec.Encode(describedFrame);
        Assert.Equal(described, PlayerSelectedObservationCodec.EncodeWithMaterialDescriptions(fixture.Frame));
        var replica = PlayerSelectedObservationCodec.Decode(described);
        Assert.Equal(2, replica.PresentationVersion);
        Assert.Equal(described, PlayerSelectedObservationCodec.Encode(replica));
        Assert.Equal(controlled.MaterialDescriptions?.Items, replica.Observation.ControlledView!.MaterialDescriptions?.Items);
        Assert.Same(fixture.State, fixture.Authority.ReadStateForHost());
    }

    [Fact]
    public void Described_home_frames_reject_legacy_extension_spelling_wrong_versions_and_false_metadata()
    {
        var world = CargoTestWorld.Genesis;
        var view = new LocalWorldView(world, CargoTestWorld.Player(world));
        var original = JsonNode.Parse(Expand(PlayerViewCodec.EncodeWithMaterialDescriptions(view)))!;
        var oldVersion = original.DeepClone();
        oldVersion["Version"] = JsonNode.Parse(Expand(PlayerViewCodec.Encode(view)))!["Version"]!.DeepClone();
        RefuseHome(oldVersion);
        var future = original.DeepClone(); future["Version"] = 6; RefuseHome(future);
        var renamed = original.DeepClone();
        renamed["View"]!["MaterialDescriptions"]!["Items"]![0]!["DisplayName"] = "Invented item";
        RefuseHome(renamed);
        var repinned = original.DeepClone();
        repinned["View"]!["MaterialDescriptions"]!["Items"]![0]!["ItemVersion"]!["Value"] = new string('0', 64);
        RefuseHome(repinned);
        var duplicate = original.DeepClone();
        var items = duplicate["View"]!["MaterialDescriptions"]!["Items"]!.AsArray();
        items.Add(items[0]!.DeepClone()); RefuseHome(duplicate);
        var reordered = original.DeepClone();
        var ordered = reordered["View"]!["MaterialDescriptions"]!["Items"]!.AsArray();
        var first = ordered[0]!.DeepClone(); ordered.RemoveAt(0); ordered.Add(first); RefuseHome(reordered);
        var effect = original.DeepClone();
        effect["View"]!["MaterialDescriptions"]!["Items"]![0]!["HealHp"] = 40; RefuseHome(effect);
    }

    [Fact]
    public void Selected_frames_reject_home_metadata_borrowing_and_extensions_in_legacy_frames()
    {
        using var fixture = new SelectedCodecFixture(WorldControlCapabilities.PrivateHistory);
        var original = JsonNode.Parse(Expand(PlayerSelectedObservationCodec.EncodeWithMaterialDescriptions(fixture.Frame)))!;
        var downgraded = original.DeepClone(); downgraded["Version"] = 1; RefuseSelected(downgraded);
        var leak = original.DeepClone();
        var observation = leak["Value"]!["Observation"]!;
        observation["ControlledView"]!["MaterialDescriptions"] = observation["HomeView"]!["MaterialDescriptions"]!.DeepClone();
        RefuseSelected(leak);
    }

    [Fact]
    public void Descriptor_values_are_bounded_copied_and_do_not_guess_unknown_item_pins()
    {
        var known = FarmMaterialDescriptionCatalog.Load(FarmContentFiles.Embedded()).Values.First();
        Assert.Null(FarmMaterialDescriptionCatalog.Find(new(known.DefinitionId, ContentHash.Compute("other version"u8))));
        Assert.Null(FarmMaterialDescriptionCatalog.Find(new(new EntityId(0xdace, 100), known.ItemVersion)));
        var input = new List<LocalMaterialDescription> { known };
        var captured = new LocalMaterialDescriptions(input); input.Clear();
        Assert.Equal(known, Assert.Single(captured.Items));
        Assert.Throws<NotSupportedException>(() => ((IList)captured.Items).Clear());
        Assert.Throws<ArgumentException>(() => new LocalMaterialDescriptions([known, known]));
        Assert.Throws<ArgumentException>(() => new LocalMaterialDescription(known.DefinitionId, known.ItemVersion, "Items/Bad", "Name"));
        Assert.Throws<ArgumentException>(() => new LocalMaterialDescription(known.DefinitionId, known.ItemVersion, "items/test", "Bad\nName"));
        var maximum = Enumerable.Range(1, LocalMaterialDescriptions.MaximumItems).Select(index =>
            new LocalMaterialDescription(new EntityId(0xdace, (ulong)index), known.ItemVersion, "items/test", "Name")).ToArray();
        Assert.Equal(LocalMaterialDescriptions.MaximumItems, new LocalMaterialDescriptions(maximum).Items.Count);
        Assert.Throws<ArgumentException>(() => new LocalMaterialDescriptions(maximum.Append(
            new(new EntityId(0xdace, 999), known.ItemVersion, "items/test", "Name"))));
    }

    [Fact]
    public void Catalog_refuses_changed_authored_metadata_instead_of_admitting_a_label_from_an_unpinned_file()
    {
        using var files = new FarmContentFixture();
        foreach (string relative in new[] { GatheringFarmDefinition.RelativePath, ProductionFarmDefinition.RelativePath })
        {
            string target = files.PathFor(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(FarmContentFixture.InstalledContentRoot(), relative), target);
        }
        Assert.Equal(7, FarmMaterialDescriptionCatalog.Load(new(files.ContentRoot)).Count);
        string path = files.PathFor(ProductionFarmDefinition.RelativePath);
        string text = File.ReadAllText(path);
        Assert.Contains("\"displayName\": \"Rations\"", text);
        File.WriteAllText(path, text.Replace("\"displayName\": \"Rations\"", "\"displayName\": \"Magic Rations\"", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => FarmMaterialDescriptionCatalog.Load(new(files.ContentRoot)));
    }

    private static byte[] Expand(byte[] bytes) => ObservationCompression.IsCompressed(bytes) ? ObservationCompression.Decode(bytes) : bytes;
    private static byte[] Wire(JsonNode value)
    {
        byte[] json = Encoding.UTF8.GetBytes(value.ToJsonString());
        return json.Length > PlayerViewCodec.MaximumBodyBytes ? ObservationCompression.Encode(json) : json;
    }
    private static void RefuseHome(JsonNode value) => Assert.Throws<FormatException>(() => PlayerViewCodec.Decode(Wire(value)));
    private static void RefuseSelected(JsonNode value) => Assert.Throws<FormatException>(() => PlayerSelectedObservationCodec.Decode(Wire(value)));
}
