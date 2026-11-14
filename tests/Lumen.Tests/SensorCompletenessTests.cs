using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class SensorCompletenessTests
{
    public static IEnumerable<object[]> Designs() => Settings.Selectable.Select(style => new object[] { style });
    // Independent expectations: intentionally not generated from CompleteReadings or its RenderedKeys.
    private static readonly Dictionary<WidgetKind, string[]> Expected = new()
    {
        [WidgetKind.Cpu] = new[] { "cpu.temp", "cpu.load", "cpu.clock", "cpu.power", "cpu.voltage", "cpu.fan" },
        [WidgetKind.Gpu] = new[] { "gpu.temp", "gpu.load", "gpu.clock", "gpu.power", "gpu.hotspot", "gpu.fan", "gpu.vram.used", "gpu.vram.total", "gpu.junction" },
        [WidgetKind.Memory] = new[] { "mem.load", "mem.used", "mem.free", "mem.total", "mem.peak" },
        [WidgetKind.Network] = new[] { "net.down", "net.up", "net.link", "net.use", "net.peak", "net.address", "net.external", "net.quality" },
        [WidgetKind.Battery] = new[] { "battery.level", "battery.state", "battery.time", "battery.rate", "battery.health" },
        [WidgetKind.Fps] = new[] { "fps.value", "fps.low", "fps.frametime", "fps.app" },
        [WidgetKind.Ping] = new[] { "ping.latency", "ping.router", "ping.jitter", "ping.loss", "ping.target" },
        [WidgetKind.Power] = new[] { "power.watts", "power.energy", "power.today", "power.week", "power.month" },
        [WidgetKind.System] = new[] { "system.uptime", "system.os", "system.boot", "system.processes", "system.threads" },
        [WidgetKind.Clock] = new[] { "clock.time", "clock.seconds", "clock.date", "clock.week", "clock.day" },
        [WidgetKind.Calendar] = new[] { "calendar.date", "calendar.week" },
        [WidgetKind.Weather] = new[] { "weather.temp", "weather.place", "weather.sky", "weather.feels", "weather.wind", "weather.humidity" },
        [WidgetKind.Media] = new[] { "media.title", "media.artist", "media.source", "media.progress" },
        [WidgetKind.DataUsage] = new[] { "data.down", "data.up", "data.period", "data.week.down", "data.week.up", "data.month.down", "data.month.up" }
    };
    private static readonly string[] DriveMetrics = { "temp", "model", "used", "read", "write", "readactivity", "writeactivity", "totalactivity", "free", "total", "powercount", "hours", "life" };

    [Theory, MemberData(nameof(Designs))]
    public void All_selected_sensor_ids_have_real_visible_controls_in_every_design(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snap, history) = Render.Demo();
        snap = snap with { Battery = new BatteryInfo(72, false, false, -15, 120, 8) };
        foreach (var edge in new[] { Edge.Right, Edge.Bottom })
        foreach (var pair in Expected)
        {
            var settings = new Settings { Edge = edge };
            settings.Widgets.ForEach(w => w.HiddenSensors.Clear());
            var card = Cards.Create(pair.Key, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
            for (int tick = 0; tick < 3; tick++)
            {
                ((IWidget)card).Update(snap with { Time = snap.Time.AddSeconds(tick) }, history);
                card.Measure(new Size(375, double.PositiveInfinity)); card.Arrange(new Rect(0, 0, 375, card.DesiredSize.Height));
                var actual = SensorDetails.VisibleKeys(card);
                Assert.True(pair.Value.All(actual.Contains), $"{style}/{pair.Key}/{edge}: missing {string.Join(",", pair.Value.Except(actual))}");
            }
        }
        foreach (bool dark in new[] { true, false })
        {
            var settings = new Settings(); settings.Widgets.ForEach(w => w.HiddenSensors.Clear());
            var card = Cards.Create(WidgetKind.Drives, style, 0, Palette.For(dark, false), Metrics.For(Density.Fit), settings);
            ((IWidget)card).Update(snap, history);
            card.Measure(new Size(300, double.PositiveInfinity)); card.Arrange(new Rect(0, 0, 300, card.DesiredSize.Height));
            var actual = SensorDetails.VisibleKeys(card);
            foreach (var disk in snap.Disks)
                Assert.All(DriveMetrics, metric => Assert.Contains("drive:" + disk.Id + ":" + metric, actual));
            Assert.Contains("drives.count", actual);
        }
        return true;
    });

    [Theory, MemberData(nameof(Designs))]
    public void Every_drive_statistic_can_be_enabled_on_its_own_even_with_group_switches_off(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snap, history) = Render.Demo();
        foreach (string metric in DriveMetrics.Where(m => m != "model"))
        {
            var settings = new Settings();
            var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Drives);
            slot.HiddenSensors = WidgetSensors.Options(WidgetKind.Drives, snap).Select(o => o.Key).Where(k => !k.StartsWith("drive:")).ToList();
            slot.HiddenSensors.Remove("drives." + metric);
            var card = Cards.Create(WidgetKind.Drives, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
            ((IWidget)card).Update(snap, history);
            card.Measure(new Size(280, double.PositiveInfinity)); card.Arrange(new Rect(0, 0, 280, card.DesiredSize.Height));
            Assert.All(snap.Disks, disk => Assert.Contains("drive:" + disk.Id + ":" + metric, SensorDetails.VisibleKeys(card)));
        }
        return true;
    });

    [Fact]
    public void Default_selection_is_compact_but_existing_explicit_choices_survive_serialization()
    {
        var fresh = new Settings { SensorSelectionVersion = SensorDefaults.SelectionVersion };
        var drives = fresh.ForWidget(WidgetKind.Drives);
        Assert.All(new[] { "drives.temp", "drives.model", "drives.read", "drives.write" }, key => Assert.True(drives.Shows(key)));
        Assert.All(SensorDefaults.Optional.Where(k => k.StartsWith("drives.")), key => Assert.False(drives.Shows(key)));
        var slot = fresh.Widgets.Single(w => w.Kind == WidgetKind.Drives); slot.HiddenSensors.Clear();
        var loaded = Settings.Normalize(JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(fresh, Settings.Json), Settings.Json)!);
        Assert.All(SensorDefaults.Optional.Where(k => k.StartsWith("drives.")), key => Assert.True(loaded.ForWidget(WidgetKind.Drives).Shows(key)));
    }

    [Fact]
    public void Group_selection_never_silently_blocks_a_child_and_preserves_siblings()
    {
        var settings = new Settings(); var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Drives);
        WidgetSensors.Select(settings, slot, "drives.io", false, null);
        Assert.False(settings.ForWidget(WidgetKind.Drives).Shows("drives.read"));
        WidgetSensors.Select(settings, slot, "drives.read", true, null);
        Assert.True(settings.ForWidget(WidgetKind.Drives).Shows("drives.io"));
        Assert.True(settings.ForWidget(WidgetKind.Drives).Shows("drives.read"));
        Assert.False(settings.ForWidget(WidgetKind.Drives).Shows("drives.write"));
        WidgetSensors.Select(settings, slot, "drives.io", true, null);
        Assert.True(settings.ForWidget(WidgetKind.Drives).Shows("drives.write"));
    }

    [Theory, MemberData(nameof(Designs))]
    public void Older_all_checked_drive_settings_start_compact_in_every_design_and_allow_later_opt_in(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snap, history) = Render.Demo();
        foreach (int oldVersion in new[] { 0, 1 })
        {
            var old = new Settings { SensorSelectionVersion = oldVersion };
            var slot = old.Widgets.Single(w => w.Kind == WidgetKind.Drives);
            slot.Style = style; slot.Name = "Storage"; slot.ValueScale = 1.25; slot.HiddenSensors.Clear();
            var loaded = Settings.Normalize(JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(old, Settings.Json), Settings.Json)!);
            var scoped = loaded.ForWidget(WidgetKind.Drives);
            Assert.All(SensorDefaults.For(WidgetKind.Drives), key => Assert.False(scoped.Shows(key)));
            Assert.All(new[] { "drives.model", "drives.temp", "drives.read", "drives.write" }, key => Assert.True(scoped.Shows(key)));
            var card = Cards.Create(WidgetKind.Drives, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), loaded);
            ((IWidget)card).Update(snap, history);
            var keys = SensorDetails.VisibleKeys(card);
            foreach (var disk in snap.Disks)
            {
                Assert.DoesNotContain("drive:" + disk.Id + ":hours", keys);
                Assert.DoesNotContain("drive:" + disk.Id + ":life", keys);
                Assert.DoesNotContain("drive:" + disk.Id + ":readactivity", keys);
            }
            var migratedSlot = loaded.Widgets.Single(w => w.Kind == WidgetKind.Drives);
            Assert.Equal("Storage", migratedSlot.Name); Assert.Equal(1.25, migratedSlot.ValueScale);
            WidgetSensors.Select(loaded, migratedSlot, "drives.hours", true, snap);
            var restarted = Settings.Normalize(JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(loaded, Settings.Json), Settings.Json)!);
            Assert.True(restarted.ForWidget(WidgetKind.Drives).Shows("drives.hours"));
            Assert.False(restarted.ForWidget(WidgetKind.Drives).Shows("drives.life"));
            restarted.Widgets.Single(w => w.Kind == WidgetKind.Drives).Style = WidgetStyle.Standard;
            Assert.True(Settings.Normalize(restarted).ForWidget(WidgetKind.Drives).Shows("drives.hours"));
        }
        return true;
    });

    [Fact]
    public void Old_hidden_groups_keep_their_effective_selection_after_migration()
    {
        var old = new Settings(); old.HiddenValues.Add("gpu.vram");
        old.Widgets.Single(w => w.Kind == WidgetKind.Drives).HiddenSensors.Add("drives.io");
        var migrated = Settings.Normalize(old);
        Assert.False(migrated.ForWidget(WidgetKind.Drives).Shows("drives.read"));
        Assert.False(migrated.ForWidget(WidgetKind.Drives).Shows("drives.write"));
        Assert.False(migrated.ForWidget(WidgetKind.Gpu).Shows("gpu.vram.used"));
        WidgetSensors.Select(migrated, migrated.Widgets.Single(w => w.Kind == WidgetKind.Drives), "drives.read", true, null);
        Settings.Normalize(migrated);
        Assert.True(migrated.ForWidget(WidgetKind.Drives).Shows("drives.read"));
        Assert.False(migrated.ForWidget(WidgetKind.Drives).Shows("drives.write"));
    }

    [Theory, MemberData(nameof(Designs))]
    public void Every_scalar_sensor_can_be_enabled_individually(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snap, history) = Render.Demo(); snap = snap with { Battery = new BatteryInfo(70, false, false, -12, 90, 5) };
        foreach (var pair in Expected)
        foreach (string key in pair.Value)
        {
            var settings = new Settings(); var slot = settings.Widgets.Single(w => w.Kind == pair.Key);
            slot.HiddenSensors = WidgetSensors.Options(pair.Key, snap).Select(o => o.Key).ToList();
            WidgetSensors.Select(settings, slot, key, true, snap);
            var card = Cards.Create(pair.Key, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
            ((IWidget)card).Update(snap, history);
            Assert.True(SensorDetails.VisibleKeys(card).Contains(key), $"{style}/{pair.Key}: {key} cannot be enabled alone");
        }
        return true;
    }, seconds: 120);

    [Fact]
    public void Canonical_drive_values_match_hardware_and_partition_capacity_without_guessed_zeroes() => UiHost.Run(() =>
    {
        var (snap, history) = Render.Demo();
        var disk = snap.Disks[0] with { Letters = "C: D:", PowerOnHours = 1234, Life = 87, ReadActivity = 12.5 };
        snap = snap with { Disks = new[] { disk }, Volumes = new[] { new VolumeInfo("C:\\", "one", 100, 40), new VolumeInfo("D:", "two", 200, 60), new VolumeInfo("E:", "other", 999, 999) } };
        var settings = new Settings(); settings.Widgets.Single(w => w.Kind == WidgetKind.Drives).HiddenSensors.Clear();
        var rows = CompleteReadings.For(WidgetKind.Drives, 0, snap, history, settings.ForWidget(WidgetKind.Drives), Palette.For(true, false)).ToDictionary(r => r.Key, r => r.Value);
        string prefix = "drive:" + disk.Id + ":";
        Assert.Equal("1234 h", rows[prefix + "hours"]); Assert.Equal(Format.Percent(87), rows[prefix + "life"]);
        Assert.Equal(Format.Percent(12.5), rows[prefix + "readactivity"]);
        Assert.Equal(Format.Capacity(100), rows[prefix + "free"]); Assert.Equal(Format.Capacity(300), rows[prefix + "total"]);
        var absent = snap with { Time = snap.Time.AddMinutes(5), Disks = new[] { disk with { PowerOnHours = null } } };
        var clean = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings, Settings.Json), Settings.Json)!;
        Assert.Equal("—", CompleteReadings.For(WidgetKind.Drives, 0, absent, history, clean.ForWidget(WidgetKind.Drives), Palette.For(true, false)).Single(r => r.Key == prefix + "hours").Value);
        return true;
    });

    [Fact]
    public void The_running_fans_count_is_off_by_default_and_once_for_existing_settings()
    {
        var fresh = Settings.Starter();
        Assert.Contains("cooling.fancount", fresh.Widgets.Single(w => w.Kind == WidgetKind.Cooling).HiddenSensors);
        var old = new Settings { SensorSelectionVersion = 2 };
        var cooling = old.Widgets.Single(w => w.Kind == WidgetKind.Cooling); cooling.HiddenSensors.Remove("cooling.fancount");
        Settings.Normalize(old);
        Assert.Contains("cooling.fancount", cooling.HiddenSensors);
        // Switched back on by the user, it stays on.
        cooling.HiddenSensors.Remove("cooling.fancount"); Settings.Normalize(old);
        Assert.DoesNotContain("cooling.fancount", cooling.HiddenSensors);
    }
}
