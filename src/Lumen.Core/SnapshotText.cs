using System.Globalization;
using System.Linq;
using System.Text;

namespace Lumen.Core;

public static class SnapshotText
{
	public static string Describe(Snapshot s)
	{
		StringBuilder stringBuilder = new StringBuilder();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(8, 1, stringBuilder2);
		handler.AppendLiteral("Access: ");
		handler.AppendFormatted(s.Access);
		stringBuilder3.AppendLine(ref handler);
		CpuInfo cpu = s.Cpu;
		if ((object)cpu != null)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(59, 8, stringBuilder2);
			handler.AppendLiteral("CPU  ");
			handler.AppendFormatted(cpu.Name);
			handler.AppendLiteral(": temp ");
			handler.AppendFormatted(N(cpu.Temperature, " °C"));
			handler.AppendLiteral(", load ");
			handler.AppendFormatted(N(cpu.Load, " %"));
			handler.AppendLiteral(", cores ");
			handler.AppendFormatted(cpu.CoreLoads.Count);
			handler.AppendLiteral(", clock ");
			handler.AppendFormatted(N(cpu.ClockMhz, " MHz"));
			handler.AppendLiteral(", power ");
			handler.AppendFormatted(N(cpu.PowerW, " W"));
			handler.AppendLiteral(", voltage ");
			handler.AppendFormatted(N(cpu.Voltage, " V"));
			handler.AppendLiteral(", fan ");
			handler.AppendFormatted(N(cpu.FanRpm, " rpm"));
			stringBuilder4.AppendLine(ref handler);
		}
		else
		{
			stringBuilder.AppendLine("CPU  not found");
		}
		foreach (GpuInfo gpu in s.Gpus)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder5 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(62, 10, stringBuilder2);
			handler.AppendLiteral("GPU  ");
			handler.AppendFormatted(gpu.Name);
			handler.AppendFormatted(gpu.Integrated ? " (integrated)" : "");
			handler.AppendLiteral(": temp ");
			handler.AppendFormatted(N(gpu.Temperature, " °C"));
			handler.AppendLiteral(", hot spot ");
			handler.AppendFormatted(N(gpu.HotSpot, " °C"));
			handler.AppendLiteral(", load ");
			handler.AppendFormatted(N(gpu.Load, " %"));
			handler.AppendLiteral(", clock ");
			handler.AppendFormatted(N(gpu.ClockMhz, " MHz"));
			handler.AppendLiteral(", power ");
			handler.AppendFormatted(N(gpu.PowerW, " W"));
			handler.AppendLiteral(", fan ");
			handler.AppendFormatted(N(gpu.FanRpm, " rpm"));
			handler.AppendLiteral(", VRAM ");
			handler.AppendFormatted(N(gpu.MemoryUsedMb, " MB"));
			handler.AppendLiteral(" / ");
			handler.AppendFormatted(N(gpu.MemoryTotalMb, " MB"));
			stringBuilder5.AppendLine(ref handler);
		}
		if (s.Gpus.Count == 0)
		{
			stringBuilder.AppendLine("GPU  not found");
		}
		MemoryInfo memory = s.Memory;
		if ((object)memory != null)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder6 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(9, 2, stringBuilder2);
			handler.AppendLiteral("RAM  ");
			handler.AppendFormatted(N(memory.UsedGb, " GB"));
			handler.AppendLiteral(" of ");
			handler.AppendFormatted(N(memory.TotalGb, " GB"));
			stringBuilder6.AppendLine(ref handler);
		}
		foreach (DiskInfo disk in s.Disks)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder7 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(37, 7, stringBuilder2);
			handler.AppendLiteral("DISK ");
			handler.AppendFormatted(disk.Name);
			handler.AppendFormatted((disk.Letters.Length > 0) ? (" [" + disk.Letters + "]") : " [no letter]");
			handler.AppendLiteral(" (");
			handler.AppendFormatted(disk.Id);
			handler.AppendLiteral("): temp ");
			handler.AppendFormatted(N(disk.Temperature, " °C"));
			handler.AppendLiteral(", used ");
			handler.AppendFormatted(N(disk.UsedPercent, " %"));
			handler.AppendLiteral(", read ");
			handler.AppendFormatted(N(disk.ReadBytesPerSecond, " B/s"));
			handler.AppendLiteral(", write ");
			handler.AppendFormatted(N(disk.WriteBytesPerSecond, " B/s"));
			stringBuilder7.AppendLine(ref handler);
		}
		NetworkInfo network = s.Network;
		if ((object)network != null)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder8 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(26, 5, stringBuilder2);
			handler.AppendLiteral("NET  ");
			handler.AppendFormatted(network.Name);
			handler.AppendLiteral(": down ");
			handler.AppendFormatted(N(network.DownloadBytesPerSecond, " B/s"));
			handler.AppendLiteral(", up ");
			handler.AppendFormatted(N(network.UploadBytesPerSecond, " B/s"));
			handler.AppendLiteral(", link ");
			handler.AppendFormatted(N(network.LinkBitsPerSecond, " bit/s"));
			handler.AppendLiteral(", ");
			handler.AppendFormatted(network.Address);
			stringBuilder8.AppendLine(ref handler);
		}
		if (s.BoardName != null)
		{
			stringBuilder.AppendLine("BOARD " + s.BoardName + ": " + string.Join(", ", s.BoardTemps.Select((TempReading t) => t.Name + " " + N(t.Celsius, " °C"))));
		}
		foreach (FanInfo fan in s.Fans)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder9 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(7, 2, stringBuilder2);
			handler.AppendLiteral("FAN  ");
			handler.AppendFormatted(fan.Name);
			handler.AppendLiteral(": ");
			handler.AppendFormatted(N(fan.Rpm, " rpm"));
			stringBuilder9.AppendLine(ref handler);
		}
		BatteryInfo battery = s.Battery;
		if ((object)battery != null)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder10 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(38, 5, stringBuilder2);
			handler.AppendLiteral("BATTERY ");
			handler.AppendFormatted(N(battery.Percent, " %"));
			handler.AppendLiteral(", AC ");
			handler.AppendFormatted(battery.OnAc);
			handler.AppendLiteral(", charging ");
			handler.AppendFormatted(battery.Charging);
			handler.AppendLiteral(", rate ");
			handler.AppendFormatted(N(battery.PowerW, " W"));
			handler.AppendLiteral(", wear ");
			handler.AppendFormatted(N(battery.WearPercent, " %"));
			stringBuilder10.AppendLine(ref handler);
		}
		return stringBuilder.ToString();
		static string N(double? v, string unit = "")
		{
			if (v.HasValue)
			{
				double valueOrDefault = v.GetValueOrDefault();
				if (double.IsFinite(valueOrDefault))
				{
					return valueOrDefault.ToString("0.##", CultureInfo.InvariantCulture) + unit;
				}
			}
			return "—";
		}
	}
}
