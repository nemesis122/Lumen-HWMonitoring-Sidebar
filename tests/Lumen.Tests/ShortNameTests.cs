using Lumen.App;
using Xunit;

// Lumen is English only and shows short names in every hardware design.
public class ShortNameTests
{
    [Theory]
    [InlineData("Download", "DL")] [InlineData("Upload", "UP")] [InlineData("Link speed", "Link")] [InlineData("Power supply", "PSU")]
    [InlineData("Memory junction", "VRAM °C")] [InlineData("VRAM Temp.", "VRAM °C")] [InlineData("Temp.", "Temp")]
    [InlineData("Voltage", "Volt")] [InlineData("Read", "R")] [InlineData("Write", "W")] 
    [InlineData("Processor", "CPU")] [InlineData("Graphics", "GPU")] [InlineData("Memory", "RAM")] [InlineData("Cooling", "Fans")] [InlineData("Chipset", "Chip")]
    [InlineData("GPU 1 Memory junction", "GPU 1 VRAM °C")] [InlineData("Read C:", "R C:")] [InlineData("LINK SPEED", "LINK")]
    public void Labels_get_their_short_name(string label, string shorter) => Assert.Equal(shorter, ShortNames.Apply(label, false));

    [Theory]
    [InlineData("AMD Radeon Graphics")] [InlineData("Samsung SSD 970 EVO Plus 2TB")] [InlineData("NVIDIA GeForce RTX 3080")] [InlineData("Load")] [InlineData("Network")] [InlineData("Hotspot")] [InlineData("Clock")] [InlineData("Power")] [InlineData("CPU Power")]
    public void Names_and_other_words_stay(string label) => Assert.Equal(label, ShortNames.Apply(label, false));

    [Fact]
    public void Headings_keep_their_words_except_the_part_names()
    {
        Assert.Equal("CPU", ShortNames.Apply("Processor", true));
        Assert.Equal("Fans", ShortNames.Apply("Cooling", true));
        Assert.Equal("Network", ShortNames.Apply("Network", true));
        Assert.Equal("Clock", ShortNames.Apply("Clock", true)); // the clock widget's title
    }

    [Fact]
    public void The_app_speaks_english_whatever_was_saved()
    {
        L.Use("de"); Assert.Equal("en", L.Language);
        Assert.Equal("Load", L.T("Load"));
        Assert.Single(L.Choices);
    }
}
