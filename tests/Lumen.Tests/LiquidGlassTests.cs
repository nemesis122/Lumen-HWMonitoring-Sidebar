using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Lumen.App;
using Lumen.Core;
using Xunit;

/// <summary>Liquid Glass (5.9.0, the first animated sidebar design): one tube per part, the level glides, the liquid moves.</summary>
public class LiquidGlassTests
{
    [Theory]
    [InlineData("demo")] [InlineData("big-rig")] [InlineData("apu-laptop")] [InlineData("intel-nvidia")] [InlineData("minimal")]
    public void Every_part_gets_a_tube_filled_to_its_reading(string hardware) => UiHost.Run(() =>
    {
        var (card, window) = NeoDesignTests.Show(WidgetStyle.AioLiquidGlass, hardware);
        try
        {
            var tubes = card.GetLogicalDescendants().OfType<ArtLiquidTube>().ToList();
            Assert.True(tubes.Count >= 3, $"{hardware}: only {tubes.Count} tubes");
            Assert.All(tubes, t => { Assert.InRange(t.Level, 0, 1); Assert.Equal(t.Target, t.Level, 3); });
            Assert.Contains(tubes, t => t.Level > .05);
        }
        finally { window.Close(); }
        return true;
    });

    [Fact]
    public void The_level_glides_to_a_new_reading_instead_of_jumping() => UiHost.Run(() =>
    {
        bool motion = Motion.Enabled; Motion.Enabled = true;
        try
        {
            var tube = new ArtLiquidTube(Colors.Teal, 1);
            tube.Set(.2); Assert.Equal(.2, tube.Level, 3); // the first reading is shown at once
            tube.Set(.8); Assert.Equal(.2, tube.Level, 3);
            tube.Advance(1.0 / ArtLiquidTube.FramesPerSecond);
            Assert.InRange(tube.Level - .2, .001, .1);
            for (int i = 0; i < 300; i++) tube.Advance(1.0 / ArtLiquidTube.FramesPerSecond);
            Assert.Equal(.8, tube.Level, 2);
            tube.Set(null); for (int i = 0; i < 300; i++) tube.Advance(1.0 / ArtLiquidTube.FramesPerSecond);
            Assert.Equal(0, tube.Level, 2);
            // Motion switched off: no gliding, the new level at once.
            Motion.Enabled = false; tube.Set(.5); Assert.Equal(.5, tube.Level, 3);
        }
        finally { Motion.Enabled = motion; }
        return true;
    });

    [Fact]
    public void The_liquid_moves_between_frames() => UiHost.Run(() =>
    {
        var tube = new ArtLiquidTube(Colors.Teal, 2) { Width = 44, Height = 210 };
        tube.Set(.6);
        var window = new Window { Width = 100, Height = 260, Content = tube };
        window.Show();
        try
        {
            byte[] Shot()
            {
                using var bitmap = new RenderTargetBitmap(new PixelSize(44, 210));
                bitmap.Render(tube);
                using var stream = new MemoryStream(); bitmap.Save(stream); return stream.ToArray();
            }
            var before = Shot();
            for (int i = 0; i < 10; i++) tube.Advance(1.0 / ArtLiquidTube.FramesPerSecond);
            Assert.NotEqual(before, Shot());
        }
        finally { window.Close(); }
        return true;
    });
}
