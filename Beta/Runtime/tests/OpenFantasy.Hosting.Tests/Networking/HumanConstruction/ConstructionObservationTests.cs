using System.Text;
using System.Text.Json.Nodes;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Networking.Selection;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation.HumanConstruction;

namespace OpenFantasy.Hosting.Tests.Networking.HumanConstruction;

public sealed class ConstructionObservationTests
{
    [Fact]
    public void Home_six_and_selected_three_roundtrip_while_older_representations_omit_extensions()
    {
        using var fixture = new HumanConstructionViewFixture();
        var view = new LocalWorldView(fixture.State, fixture.Owner);
        foreach (byte[] old in new[] { PlayerViewCodec.Encode(view), PlayerViewCodec.EncodeWithMaterialDescriptions(view) })
        {
            var decoded = PlayerViewCodec.Decode(old);
            Assert.Empty(decoded.ConstructionWork);
            Assert.Null(decoded.Structures);
            Assert.Equal(old, JsonNode.Parse(Expand(old))!["Version"]!.GetValue<int>() == 5
                ? PlayerViewCodec.EncodeWithMaterialDescriptions(decoded) : PlayerViewCodec.Encode(decoded));
        }
        byte[] current = PlayerViewCodec.EncodeWithConstruction(view);
        var restored = PlayerViewCodec.Decode(current);
        Assert.Equal(6, JsonNode.Parse(Expand(current))!["Version"]!.GetValue<int>());
        Assert.Equal(current, PlayerViewCodec.EncodeWithConstruction(restored));
        Assert.Equal(Assert.Single(view.ConstructionWork).Choices, Assert.Single(restored.ConstructionWork).Choices);
        Assert.NotNull(restored.Structures);

        var witness = fixture.Delegate(WorldControlCapabilities.Production);
        var selected = new PlayerSelectedObservation(new(0xc057, 30), 1,
            new(new(new(0xc057, 31), fixture.Helper, fixture.Owner, 1, witness),
                new LocalWorldView(fixture.State, fixture.Helper),
                LocalControlledActorView.Project(fixture.State, fixture.Helper, fixture.Delivery(fixture.Helper), witness),
                new(PlayerControlState.Available, new(0xc057, 31), fixture.Helper, 0), false));
        foreach (var frame in new[] { selected, selected.WithMaterialDescriptions(), selected.WithConstruction() })
        {
            byte[] encoded = PlayerSelectedObservationCodec.Encode(frame);
            var replica = PlayerSelectedObservationCodec.Decode(encoded);
            Assert.Equal(frame.PresentationVersion, replica.PresentationVersion);
            Assert.Equal(encoded, PlayerSelectedObservationCodec.Encode(replica));
            var production = replica.Observation.ControlledView!.Production!;
            if (frame.PresentationVersion == 3)
            {
                Assert.Single(production.ConstructionWorkViews);
                Assert.NotNull(replica.Observation.ControlledView.Structures);
            }
            else
            {
                Assert.Empty(production.ConstructionWorkViews);
                Assert.Null(replica.Observation.ControlledView.Structures);
            }
        }
    }

    [Fact]
    public void New_frames_reject_old_version_extensions_and_contradictory_work_capture()
    {
        using var fixture = new HumanConstructionViewFixture();
        var view = new LocalWorldView(fixture.State, fixture.Owner);
        var original = JsonNode.Parse(Expand(PlayerViewCodec.EncodeWithConstruction(view)))!;
        var downgraded = original.DeepClone(); downgraded["Version"] = 5; Refuse(downgraded);
        var missing = original.DeepClone(); missing["View"]!.AsObject().Remove("Structures"); Refuse(missing);
        var forged = original.DeepClone(); forged["View"]!["ConstructionWork"]![0]!["ActorId"] =
            System.Text.Json.JsonSerializer.SerializeToNode(fixture.Helper); Refuse(forged);
        var stale = original.DeepClone(); stale["View"]!["ConstructionWork"]![0]!["ProjectRevision"] = 99; Refuse(stale);
        var unblocked = original.DeepClone(); unblocked["View"]!["ConstructionWork"]![0]!["Blocker"] = 0; Refuse(unblocked);
    }

    private static byte[] Expand(byte[] bytes) => ObservationCompression.IsCompressed(bytes) ? ObservationCompression.Decode(bytes) : bytes;
    private static void Refuse(JsonNode value)
    {
        byte[] json = Encoding.UTF8.GetBytes(value.ToJsonString());
        Assert.Throws<FormatException>(() => PlayerViewCodec.Decode(json.Length > PlayerViewCodec.MaximumBodyBytes
            ? ObservationCompression.Encode(json) : json));
    }
}
