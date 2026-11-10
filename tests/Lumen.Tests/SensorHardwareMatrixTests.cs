using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class SensorHardwareMatrixTests
{
    public static IEnumerable<object[]> Designs() => Settings.Selectable.Select(style => new object[] { style });

    [Theory, MemberData(nameof(Designs))]
    public void Hardware_shapes_render_without_model_specific_assumptions(WidgetStyle style) => UiHost.Run(() =>
    {
        var (baseline, _) = Render.Demo();
        var integrated = new GpuInfo("intel/igpu", "Intel integrated graphics", null, null, 5, 400, null, null, 512, null) { Integrated = true };
        var gpuA = new GpuInfo("amd/gpu-a", "AMD Radeon", 48, 64, 10, 600, 28, 0, 1024, 16384) { MemoryJunction = 55 };
        var gpuB = gpuA with { Id = "amd/gpu-b", Temperature = 62, Load = 70, FanRpm = 1600 };
        var disks = Enumerable.Range(0, 6).Select(i => new DiskInfo("disk/" + i, "Identical SSD model", i % 2 == 0 ? 40 + i : null, 10 + i * 10, i * 1000, i * 2000)
            { Letters = ((char)('C' + i)) + ":", Life = i == 0 ? 95 : null, PowerOnHours = i == 0 ? 900 : null }).ToArray();
        var manyCore = baseline.Cpu! with { Name = "Many-core workstation", CoreLoads = Enumerable.Range(0, 192).Select(i => i % 11 == 0 ? double.NaN : i % 101d).ToArray() };
        var shapes = new[]
        {
            baseline,
            baseline with { Cpu = baseline.Cpu! with { Name = "Intel laptop", Temperature = null, FanRpm = null, Voltage = null }, Gpus = new[] { integrated }, Disks = Array.Empty<DiskInfo>(), Network = null, Fans = Array.Empty<FanInfo>(), BoardTemps = Array.Empty<TempReading>() },
            baseline with { Cpu = manyCore, Gpus = new[] { gpuA, gpuB, integrated }, Disks = disks },
            baseline with { Cpu = null, Gpus = Array.Empty<GpuInfo>(), Memory = null, Network = null, Disks = Array.Empty<DiskInfo>() }
        };
        foreach (var snap in shapes)
        foreach (var kind in new[] { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Drives, WidgetKind.Memory, WidgetKind.Network })
        {
            var settings = new Settings { ShowIntegratedGpu = true };
            settings.Widgets.ForEach(w => w.HiddenSensors.Clear());
            int instances = kind == WidgetKind.Gpu ? Math.Max(1, snap.Gpus.Count) : 1;
            for (int index = 0; index < instances; index++)
            {
                var history = new History(); history.Record(snap);
                var card = Cards.Create(kind, style, index, Palette.For(true, false), Metrics.For(Density.Fit), settings);
                ((IWidget)card).Update(snap, history);
                var window = new Window { Content = card, Width = 300, Height = 900 };
                try
                {
                    window.Show(); Dispatcher.UIThread.RunJobs(); using var frame = window.CaptureRenderedFrame();
                    Assert.NotNull(frame);
                    if (kind == WidgetKind.Gpu && snap.Gpus.Count > 1)
                    {
                        var actual = CompleteReadings.For(kind, index, snap, history, settings.ForWidget(kind), Palette.For(true, false));
                        var expectedGpu = GpuGroup.Visible(snap, settings)[index];
                        Assert.Equal(Format.Percent(expectedGpu.Load), actual.Single(r => r.Key == "gpu.load").Value);
                    }
                    if (kind == WidgetKind.Drives)
                        Assert.All(snap.Disks, disk => Assert.Contains("drive:" + disk.Id + ":hours", SensorDetails.VisibleKeys(card)));
                }
                finally { window.Close(); }
            }
        }
        return true;
    }, seconds: 120);
}
