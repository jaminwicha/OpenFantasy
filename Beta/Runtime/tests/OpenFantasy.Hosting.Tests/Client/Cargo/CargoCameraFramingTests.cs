using OpenFantasy.Client.Cargo;

namespace OpenFantasy.Hosting.Tests.Client.Cargo;

public sealed class CargoCameraFramingTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1023.99, 1023.99)]
    [InlineData(512, 512)]
    public void Observed_edges_frame_the_actor_between_the_real_hud_panels(double x, double y)
    {
        var viewport = new CargoCanvasRect(0, 0, 960, 540);
        var field = new CargoCanvasRect(0, 76, 960, 280);
        var framing = new CargoCameraFraming(new CargoCanvasRect(0, 0, 1024, 1024), viewport,
            field, new CargoCanvasPoint(2, 2));
        double cameraX = Math.Clamp(x + framing.FollowOffset.X, framing.Limits.X + 240, framing.Limits.Right - 240);
        double cameraY = Math.Clamp(y + framing.FollowOffset.Y, framing.Limits.Y + 135, framing.Limits.Bottom - 135);
        double screenX = 480 + (x - cameraX) * 2;
        double screenY = 270 + (y - cameraY) * 2;
        Assert.Equal(480, screenX, precision: 5);
        Assert.Equal(216, screenY, precision: 5);
        Assert.True(screenY - 80 >= field.Y && screenY < field.Bottom);
    }

    [Fact]
    public void Resizing_or_expanding_the_footer_changes_framing_without_moving_world_coordinates()
    {
        var terrain = new CargoCanvasRect(-512, -256, 2048, 4096);
        var wide = new CargoCameraFraming(terrain, new CargoCanvasRect(0, 0, 1280, 720),
            new CargoCanvasRect(0, 80, 1280, 400), new CargoCanvasPoint(2, 2));
        var tallFooter = new CargoCameraFraming(terrain, new CargoCanvasRect(0, 0, 1280, 720),
            new CargoCanvasRect(0, 80, 1280, 240), new CargoCanvasPoint(2, 2));
        Assert.Equal(40, wide.FollowOffset.Y);
        Assert.Equal(80, tallFooter.FollowOffset.Y);
        Assert.Equal(wide.Limits.Width, tallFooter.Limits.Width);
        Assert.Equal(wide.Limits.Height, tallFooter.Limits.Height);
        Assert.Equal(40, tallFooter.Limits.Y - wide.Limits.Y);
    }

    [Fact]
    public void An_invalid_field_or_zoom_is_refused()
    {
        var bounds = new CargoCanvasRect(0, 0, 1024, 1024);
        var viewport = new CargoCanvasRect(0, 0, 960, 540);
        Assert.Throws<ArgumentOutOfRangeException>(() => new CargoCameraFraming(bounds, viewport,
            new CargoCanvasRect(0, 500, 960, 100), new CargoCanvasPoint(2, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CargoCameraFraming(bounds, viewport,
            viewport, new CargoCanvasPoint(0, 2)));
    }
}
