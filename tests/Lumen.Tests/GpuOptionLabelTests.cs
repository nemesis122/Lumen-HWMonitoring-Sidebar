using Lumen.App;
using Lumen.Core;
using Xunit;

public class GpuOptionLabelTests
{
    [Theory]
    [InlineData("NVIDIA GeForce RTX 5090", "AMD Radeon(TM) Graphics")]
    [InlineData("AMD Radeon RX 9070 XT", "Intel UHD Graphics 770")]
    [InlineData("Intel Arc B580", "AMD Radeon 890M")]
    [InlineData("NVIDIA GeForce RTX 4080", "NVIDIA GeForce RTX 4080")]
    public void Sensor_choice_names_identify_each_detected_device_without_changing_sensor_keys(string first, string second)
    {
        var (snapshot, _) = Render.Demo();
        var a = new GpuInfo("discrete-one", first, 60, 70, 40, 2200, 180, 1100, 4000, 16000);
        var b = new GpuInfo("other-two", second, 45, null, 8, 400, 12, null, 300, 1000) { Integrated = first != second };
        var options = AioReadings.Options(snapshot with { Gpus = new[] { a, b }, HardwareSensors = Array.Empty<SensorNode>() }).ToArray();
        var loads = options.Where(o => o.Key is "aio.gpu.discrete-one.load" or "aio.gpu.other-two.load").ToArray();
        Assert.Equal(2, loads.Length); Assert.Equal(2, loads.Select(o => o.Label).Distinct().Count());
        Assert.Contains(loads, o => o.Key.Contains(a.Id) && o.Label.Contains(first) && o.Label.Contains("GPU 1"));
        Assert.Contains(loads, o => o.Key.Contains(b.Id) && o.Label.Contains(second) && o.Label.Contains("GPU 2"));
        var settings = Settings.Starter(); var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Overview); slot.Style = WidgetStyle.CaseCyanGlass;
        WidgetSensors.Select(settings, slot, "aio.gpu.other-two.load", false, snapshot);
        _ = AioReadings.Options(snapshot with { Gpus = new[] { b, a } }).ToArray();
        Assert.Contains("aio.gpu.other-two.load", slot.HiddenSensors);
        Assert.DoesNotContain("aio.gpu.discrete-one.load", slot.HiddenSensors);
    }
}
