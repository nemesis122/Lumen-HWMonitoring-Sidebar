using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

// Integrated graphics in every design: shown, with every value they report, and without empty "—" rows
// for values they do not have (temperature, hot spot, fan, own memory size of many Intel graphics).
public class IntegratedGraphicsDesignTests
{
    private static readonly GpuInfo Intel = new("intel/igpu", "Intel(R) UHD Graphics 770", null, null, 7, 1450, 2.4, null, 812, null) { Integrated = true };

    public static IEnumerable<object[]> Designs() => Settings.Selectable.Select(style => new object[] { style });

    private static List<string> Texts(Control root) => root.GetLogicalDescendants().OfType<TextBlock>()
        .Where(t => t.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(t.Text) && t.Text != "⁠").Select(t => t.Text!.Trim()).ToList();

    [Theory, MemberData(nameof(Designs))]
    public void Integrated_graphics_show_their_values_and_no_empty_rows(WidgetStyle style) => UiHost.Run(() =>
    {
        var (demo, h) = Render.Demo();
        var snap = demo with { Gpus = new[] { demo.Gpus[0], Intel } };
        var settings = Settings.Starter(); settings.Language = "en"; settings.ShowIntegratedGpu = true;
        foreach (var w in settings.Widgets.Where(w => Settings.HasStyles(w.Kind))) w.Style = style;
        if (AioDesigns.Contains(style))
        {
            // The "Integrated graphics" switch alone brings the card into every AIO design.
            var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Overview); slot.Style = style; slot.Visible = true;
            var card = new AioTile(style, Palette.For(true, false), Metrics.For(Density.Fit), settings);
            card.Own(slot, 0); card.Update(snap, h);
            var keys = SensorDetails.VisibleKeys(card);
            Assert.Contains("aio.gpu.intel/igpu.load", keys);
            Assert.Contains("aio.gpu.intel/igpu.power", keys);
            Assert.DoesNotContain(AioReadings.For(snap, settings.ForSlot(slot)), r => r.Device == Intel.Id && r.Value.StartsWith("—"));
            return true;
        }
        Unreported.Signature(snap, settings);
        var gpu = Cards.Create(WidgetKind.Gpu, style, 1, Palette.For(true, false), Metrics.For(Density.Fit), settings);
        ((IWidget)gpu).Update(snap, h);
        var window = new Window { Content = gpu, Width = 300, Height = 1200 };
        try
        {
            window.Show(); ((IWidget)gpu).Update(snap, h); Dispatcher.UIThread.RunJobs();
            var visible = SensorDetails.VisibleKeys(gpu);
            foreach (var key in new[] { "gpu.load", "gpu.power", "gpu.clock" }) Assert.Contains(key, visible);
            foreach (var key in new[] { "gpu.temp", "gpu.hotspot", "gpu.fan", "gpu.vram.total" }) Assert.DoesNotContain(key, visible);
            Assert.DoesNotContain(Texts(gpu), t => t == "—" || t.EndsWith("/ —"));
            // No caption left over for a temperature the graphics do not have.
            Assert.DoesNotContain(Texts(gpu), t => t.Equals("Temp", StringComparison.OrdinalIgnoreCase));
        }
        finally { window.Close(); }
        return true;
    }, seconds: 60);

    [Fact]
    public void Aio_choices_list_the_integrated_graphics_and_a_value_brings_the_card()
    {
        var (demo, _) = Render.Demo();
        var snap = demo with { Gpus = new[] { demo.Gpus[0], Intel } };
        Assert.Contains(AioReadings.Options(snap), o => o.Key == GpuGroup.ExtraKey(Intel));
        var s = Settings.Starter();
        var slot = s.Widgets.Single(w => w.Kind == WidgetKind.Overview); slot.Style = WidgetStyle.AioTypeBlock;
        Assert.DoesNotContain(AioReadings.For(snap, s.ForSlot(slot)), r => r.Device == Intel.Id);
        WidgetSensors.Select(s, slot, "aio.gpu.intel/igpu.load", true, snap);
        Assert.Contains(AioReadings.For(snap, s.ForSlot(slot)), r => r.Key == "aio.gpu.intel/igpu.load");
    }

    [Fact]
    public void Open_hardware_monitor_integrated_card_is_not_mixed_into_the_dedicated_card()
    {
        var local = new[] { new GpuInfo("nv", "NVIDIA GeForce RTX 4080", 50, null, 30, null, null, null, null, null),
            new GpuInfo("amd", "AMD Radeon(TM) Graphics", null, null, 2, null, null, null, null, null) { Integrated = true } };
        var ohm = new[] { new GpuInfo("ohm:amd", "AMD Radeon(TM) Graphics ", 41, null, 2, 2200, 5, null, 512, 512) { Integrated = true } };
        var merged = SensorMerge.Gpus(local, ohm);
        Assert.Equal(2, merged.Count);
        Assert.Null(merged.Single(g => g.Id == "nv").ClockMhz);
        Assert.Equal(41, merged.Single(g => g.Id == "amd").Temperature);
    }

    [Fact]
    public void Aida_card_left_over_belongs_to_the_integrated_graphics()
    {
        var local = new List<GpuInfo>
        {
            new("/gpu-intel/0", "Intel(R) UHD Graphics 770", null, null, null, null, null, null, null, null) { Integrated = true },
            new("/gpu-nvidia/0", "NVIDIA GeForce RTX 3070 Ti", null, null, null, null, null, null, null, 8192)
        };
        var ext = new List<GpuInfo>
        {
            new("aida/gpu/1", "GPU 1", null, null, 6, 1300, null, null, null, null),
            new("aida/gpu/2", "GPU 2", 64, 78, 82, 2610, 214, 1480, 7240, null)
        };
        var m = SensorMerge.PreferGpus(ext, local, "AIDA64");
        Assert.Equal(2, m.Count);
        Assert.Equal(64, m.Single(g => !g.Integrated).Temperature);
        var igpu = m.Single(g => g.Integrated);
        Assert.Equal(("Intel(R) UHD Graphics 770", 6d), (igpu.Name, igpu.Load!.Value));
    }

    [Theory]
    [InlineData("AMD Radeon(TM) Graphics", true)]
    [InlineData("AMD Radeon 780M", true)]
    [InlineData("AMD Radeon(TM) 8060S Graphics", true)]
    [InlineData("AMD Radeon RX Vega 11 Graphics", true)]
    [InlineData("AMD Radeon Vega 8 Graphics", true)]
    [InlineData("AMD Radeon RX 7900 XTX", false)]
    [InlineData("AMD Radeon RX 6600M", false)]
    [InlineData("AMD Radeon RX Vega 64", false)]
    public void Amd_processor_graphics_are_recognised(string name, bool integrated) =>
        Assert.Equal(integrated, SensorPicker.IsIntegrated(new SensorNode("GpuAmd", name, "x", Array.Empty<SensorValue>(), Array.Empty<SensorNode>())));

    [Fact]
    public void Hwinfo_direct3d_usage_is_the_load_of_integrated_graphics()
    {
        const string gpu = "GPU [#1]: Intel UHD Graphics 770:";
        var nodes = External.HwinfoNodes(new[] { new HwinfoReading(gpu, 1, 7, "GPU D3D Usage", "%", 12), new HwinfoReading(gpu, 1, 5, "GPU Power", "W", 3) });
        var g = SensorPicker.Gpu(nodes.Single(n => n.IsGpu));
        Assert.True(g.Integrated);
        Assert.Equal(12, g.Load);
    }
}
