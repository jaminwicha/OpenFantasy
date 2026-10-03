using OpenFantasy.Core.Content;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Tests.Content;

namespace OpenFantasy.Hosting.Tests.Networking.PublicMaterialDescriptions;

public sealed class RetainedMaterialViewTests
{
    [Fact]
    public void Actual_prior_native_frame_decodes_and_preserves_its_original_canonical_bytes()
    {
        string path = Path.Combine(FarmContentFixture.InstalledContentRoot(), "..", "tests",
            "OpenFantasy.Hosting.Tests", "Networking", "PublicMaterialDescriptions", "fixtures", "home-v4.json");
        byte[] original = File.ReadAllBytes(path);
        Assert.Equal("b76410c98e4fbfb8fd98633c4e3b9f5980dfc82c37bd70dd0372ea523ba3616c", ContentHash.Compute(original).Value);
        var restored = PlayerViewCodec.Decode(original);
        Assert.Null(restored.MaterialDescriptions);
        Assert.Equal(original, PlayerViewCodec.Encode(restored));
    }
}
