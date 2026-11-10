using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class DesignValueFaceTests
{
    [Theory]
    [InlineData(WidgetStyle.Thermostat)]
    [InlineData(WidgetStyle.SciFi)]
    public void One_two_and_three_digit_temperatures_stay_on_the_gauge_centre(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo();
        var card = Cards.Create(WidgetKind.Drives, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), new Settings());
        var window = new Window { Content = card, Width = 300, Height = 1000 };
        try
        {
            window.Show();
            foreach (double temperature in new[] { 9d, 41d, 100d, 0d })
            {
                ((IWidget)card).Update(snapshot with { Disks = snapshot.Disks.Select(d => d with { Temperature = temperature }).ToArray() }, history);
                Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame();
                var faces = Assert.Single(card.GetLogicalDescendants().OfType<DesignValueFaces>());
                foreach (var cell in faces.Children)
                {
                    var number = cell.GetLogicalDescendants().OfType<TextBlock>().Single(t => SensorDetails.KeyOf(t).EndsWith(":temp"));
                    var ring = cell.GetLogicalDescendants().OfType<Control>().Single(c => c is KnobRing or TickRing);
                    double numberCentre = number.TranslatePoint(new Point(number.Bounds.Width / 2, 0), faces)!.Value.X;
                    double ringCentre = ring.TranslatePoint(new Point(ring.Bounds.Width / 2, 0), faces)!.Value.X;
                    Assert.InRange(Math.Abs(numberCentre - ringCentre), 0, 1);
                }
            }
        }
        finally { window.Close(); }
        return true;
    });
    [Theory]
    [InlineData(WidgetStyle.Dial)]
    [InlineData(WidgetStyle.HistoryGraph)]
    [InlineData(WidgetStyle.RingPair)]
    public void Drive_focus_uses_native_dials_histories_or_double_rings(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo(); history.Record(snapshot);
        foreach (bool dark in new[] { true, false })
        foreach (double width in new[] { 280d, 375d })
        {
            var settings = new Settings();
            settings.Widgets.Single(w => w.Kind == WidgetKind.Drives).HiddenSensors.Remove("drives.used");
            var card = Cards.Create(WidgetKind.Drives, style, 0, Palette.For(dark, false), Metrics.For(Density.Fit), settings);
            var window = new Window { Content = card, Width = width, Height = 1800 };
            try
            {
                window.Show(); ((IWidget)card).Update(snapshot, history); Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame();
                var faces = Assert.Single(card.GetLogicalDescendants().OfType<DesignValueFaces>());
                var values = faces.GetLogicalDescendants().OfType<TextBlock>().Where(t => SensorDetails.KeyOf(t).EndsWith(":temp")).ToArray();
                Assert.Equal(snapshot.Disks.Count, values.Length);
                Assert.InRange(Math.Abs(values[0].TranslatePoint(default, faces)!.Value.Y - values[1].TranslatePoint(default, faces)!.Value.Y), 0, 1);
                foreach (var value in values) Assert.True(value.FontSize >= 30);
                Assert.Equal(snapshot.Disks.Count, faces.GetLogicalDescendants().OfType<Control>().Count(c => style == WidgetStyle.Dial ? c is DialArc : style == WidgetStyle.RingPair ? c is RingPair : c is AreaChart));
                foreach (var disk in snapshot.Disks)
                {
                    Assert.Contains(disk.Temperature, history.Read("disk." + disk.Id + ".temp").Select(x => (double?)x));
                    Assert.Single(card.GetLogicalDescendants().OfType<TextBlock>().Where(t => SensorDetails.KeyOf(t) == "drive:" + disk.Id + ":temp" && SensorDetails.Visible(t, card)));
                    Assert.Single(card.GetLogicalDescendants().OfType<TextBlock>().Where(t => SensorDetails.KeyOf(t) == "drive:" + disk.Id + ":used" && SensorDetails.Visible(t, card)));
                }
                settings.Widgets.Single(w => w.Kind == WidgetKind.Drives).HiddenSensors.Add("drives.temp");
                ((IWidget)card).Update(snapshot, history);
                Assert.DoesNotContain(SensorDetails.VisibleKeys(card), k => k.EndsWith(":temp"));
                Assert.Contains(SensorDetails.VisibleKeys(card), k => k.EndsWith(":used"));
            }
            finally { window.Close(); }
        }
        return true;
    });
    public static IEnumerable<object[]> Cases() => new[] { WidgetStyle.Thermostat, WidgetStyle.PurpleDash, WidgetStyle.SciFi }
        .SelectMany(style => new[] { WidgetKind.Drives, WidgetKind.Network }.SelectMany(kind => new[] { true, false }.Select(dark => new object[] { style, kind, dark })));

    [Theory, MemberData(nameof(Cases))]
    public void Native_circles_show_each_device_or_direction_once_and_side_by_side(WidgetStyle style, WidgetKind kind, bool dark) => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo();
        foreach (double width in new[] { 280d, 375d })
        {
            var settings = new Settings(); var palette = Palette.For(dark, false);
            var card = Cards.Create(kind, style, 0, palette, Metrics.For(Density.Fit), settings);
            var window = new Window { Content = card, Width = width, Height = 1400 };
            try
            {
                window.Show(); ((IWidget)card).Update(snapshot, history); Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame();
                var faces = Assert.Single(card.GetLogicalDescendants().OfType<DesignValueFaces>());
                var values = faces.GetLogicalDescendants().OfType<TextBlock>().Where(t => SensorDetails.KeyOf(t).Length > 0).ToArray();
                Assert.All(values, v => Assert.Equal(Avalonia.Media.TextAlignment.Center, v.TextAlignment));
                Assert.Equal(kind == WidgetKind.Drives ? snapshot.Disks.Count : 2, values.Length);
                Assert.Equal(values.Length, values.Select(SensorDetails.KeyOf).Distinct().Count());
                var points = values.Take(2).Select(v => v.TranslatePoint(default, faces)!.Value).ToArray();
                Assert.InRange(Math.Abs(points[0].Y - points[1].Y), 0, 1); Assert.True(points[1].X > points[0].X);
                var graphics = faces.GetLogicalDescendants().OfType<Control>().Where(c => c is KnobRing or SweepRing or TickRing).ToArray();
                Assert.Equal(values.Length, graphics.Length);
                Assert.All(graphics, g => Assert.Equal(style == WidgetStyle.Thermostat ? typeof(KnobRing) : style == WidgetStyle.PurpleDash ? typeof(SweepRing) : typeof(TickRing), g.GetType()));
                foreach (var value in values) Assert.Single(card.GetLogicalDescendants().OfType<TextBlock>().Where(t => SensorDetails.KeyOf(t) == SensorDetails.KeyOf(value) && SensorDetails.Visible(t, card)));
                settings.Widgets.Single(w => w.Kind == kind).HiddenSensors.Add(kind == WidgetKind.Drives ? "drives.temp" : "net.up");
                ((IWidget)card).Update(snapshot, history);
                Assert.DoesNotContain(SensorDetails.VisibleKeys(card), key => kind == WidgetKind.Drives ? key.EndsWith(":temp") : key == "net.up");
            }
            finally { window.Close(); }
        }
        return true;
    });

    [Theory]
    [InlineData(WidgetStyle.Meridian)]
    [InlineData(WidgetStyle.Pure)]
    public void Drive_temperature_numbers_share_one_row(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo();
        foreach (double width in new[] { 280d, 375d })
        {
            var card = Cards.Create(WidgetKind.Drives, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), new Settings());
            var window = new Window { Content = card, Width = width, Height = 1600 };
            try
            {
                window.Show(); ((IWidget)card).Update(snapshot, history); Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame();
                var values = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => SensorDetails.KeyOf(t).EndsWith(":temp") && SensorDetails.Visible(t, card)).ToArray();
                Assert.Equal(snapshot.Disks.Count, values.Length);
                var a = values[0].TranslatePoint(default, card)!.Value; var b = values[1].TranslatePoint(default, card)!.Value;
                Assert.InRange(Math.Abs(a.Y - b.Y), 0, 1); Assert.True(b.X > a.X);
                if (style == WidgetStyle.Pure) Assert.All(values, v => Assert.Equal(36, v.FontSize));
            }
            finally { window.Close(); }
        }
        return true;
    });
}
