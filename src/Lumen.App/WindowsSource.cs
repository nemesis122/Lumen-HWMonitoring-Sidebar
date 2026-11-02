using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using LibreHardwareMonitor.Hardware;
using Lumen.Core;
using OpenHardwareMonitor.Hardware;

namespace Lumen.App;

public sealed class WindowsSource : ISnapshotSource, IDisposable, ISensorReport
{
	private struct PowerStatus
	{
		public byte ACLineStatus;

		public byte BatteryFlag;

		public byte BatteryLifePercent;

		public byte SystemStatusFlag;

		public int BatteryLifeTime;

		public int BatteryFullLifeTime;
	}

	private struct MemoryStatus
	{
		public uint Length;

		public uint MemoryLoad;

		public ulong TotalPhys;

		public ulong AvailPhys;

		public ulong TotalPageFile;

		public ulong AvailPageFile;

		public ulong TotalVirtual;

		public ulong AvailVirtual;

		public ulong AvailExtendedVirtual;
	}

	private readonly Computer computer;

	private readonly DriverlessComputer? ohm;

	private string? ohmError;

	private readonly List<string> mergeLog = new List<string>();

	private ulong lastIdle;

	private ulong lastKernel;

	private ulong lastUser;

	private readonly bool admin;

	private readonly Stopwatch clock = Stopwatch.StartNew();

	private string? nicId;

	private long lastRx;

	private long lastTx;

	private double lastTime;

	private readonly ProcessSampler processes = new ProcessSampler();

	private double lastSlow;

	private SystemInfo? systemInfo;

	private IReadOnlyList<VolumeInfo> volumes = Array.Empty<VolumeInfo>();

	private readonly HashSet<string> spinning = new HashSet<string>();

	private readonly Func<SensorSource> sourceChoice;

	private readonly ExternalSensors external = new ExternalSensors();

	private string? externalName;

	private readonly HardwareCombiner combiner = new HardwareCombiner();

	private double? acpi;

	private double acpiAt = -100.0;

	private Dictionary<int, string> letters = new Dictionary<int, string>();

	private double lettersAt = -100.0;

	private List<SensorMerge.PhysicalDisk> physical = new List<SensorMerge.PhysicalDisk>();

	private double physicalAt = -100.0;

	private readonly Dictionary<int, (long read, long write, double at)> diskCounters = new Dictionary<int, (long, long, double)>();

	public bool Paused { get; private set; }

	public bool WantsProcesses { get; set; } = true;

	public bool WantsSystem { get; set; } = true;

	public static Regex PerCore => SensorPicker.PerCore;

	public WindowsSource(bool admin, Func<SensorSource>? sourceChoice = null)
	{
		this.admin = admin;
		this.sourceChoice = sourceChoice ?? ((Func<SensorSource>)(() => SensorSource.Builtin));
		computer = new Computer
		{
			IsCpuEnabled = true,
			IsGpuEnabled = true,
			IsStorageEnabled = true,
			IsMotherboardEnabled = true,
			IsBatteryEnabled = true,
			IsControllerEnabled = true
		};
		try
		{
			computer.Open();
		}
		catch
		{
		}
		try
		{
			ohm = new DriverlessComputer();
			ohm.Open();
		}
		catch (Exception ex)
		{
			ohmError = ex.Message;
			ohm = null;
		}
	}

	public Snapshot Read()
	{
		mergeLog.Clear();
		(string, List<SensorNode>)? tuple = external.Read(sourceChoice());
		Paused = tuple.HasValue && combiner.HasIdentity;
		List<SensorNode> list = null;
		List<SensorNode> list2 = new List<SensorNode>();
		if (!Paused)
		{
			list = new List<SensorNode>();
			foreach (LibreHardwareMonitor.Hardware.IHardware item in computer.Hardware)
			{
				try
				{
					item.Update();
					LibreHardwareMonitor.Hardware.IHardware[] subHardware = item.SubHardware;
					for (int i = 0; i < subHardware.Length; i++)
					{
						subHardware[i].Update();
					}
				}
				catch
				{
				}
				try
				{
					list.Add(Node(item));
				}
				catch
				{
				}
			}
			if (ohm != null)
			{
				list2 = ReadOhm();
			}
		}
		if (clock.Elapsed.TotalSeconds - lastSlow > 5.0 || (object)systemInfo == null)
		{
			lastSlow = clock.Elapsed.TotalSeconds;
			try
			{
				if (WantsSystem || (object)systemInfo == null)
				{
					systemInfo = SystemReader.Read();
				}
			}
			catch
			{
			}
			try
			{
				volumes = SystemReader.Volumes();
			}
			catch
			{
			}
		}
		HardwareCombiner hardwareCombiner = combiner;
		List<SensorNode> builtin = list;
		List<SensorNode> list3 = list2;
		(string, IReadOnlyList<SensorNode>)? obj5;
		if (tuple.HasValue)
		{
			(string, List<SensorNode>) valueOrDefault = tuple.GetValueOrDefault();
			obj5 = (valueOrDefault.Item1, valueOrDefault.Item2);
		}
		else
		{
			obj5 = null;
		}
		HardwareView hardwareView = hardwareCombiner.Combine(new HardwareInput(builtin, list3, obj5, PhysicalDisks(), DiskLetters(), volumes, SystemLoad(), AcpiTemperature));
		mergeLog.AddRange(combiner.Log);
		externalName = tuple?.Item1;
		CpuInfo cpu = hardwareView.Cpu;
		List<GpuInfo> gpus = hardwareView.Gpus;
		List<DiskInfo> disks = hardwareView.Disks.Select(delegate(DiskInfo d)
		{
			if (!d.ReadBytesPerSecond.HasValue && !d.WriteBytesPerSecond.HasValue)
			{
				int? num = DiskNumber(d.Id);
				if (num.HasValue)
				{
					int valueOrDefault2 = num.GetValueOrDefault();
					(double?, double?)? tuple2 = DiskRates(valueOrDefault2);
					if (tuple2.HasValue)
					{
						var (readBytesPerSecond, writeBytesPerSecond) = tuple2.GetValueOrDefault();
						return d with
						{
							ReadBytesPerSecond = readBytesPerSecond,
							WriteBytesPerSecond = writeBytesPerSecond
						};
					}
				}
			}
			return d;
		}).ToList();
		SensorAccess access = ((!(cpu?.Temperature).HasValue) ? SensorAccess.Limited : SensorAccess.Full);
		if (!admin && externalName == null)
		{
			access = SensorAccess.Limited;
		}
		List<SensorNode> hardware = hardwareView.Nodes.ToList();
		IReadOnlyList<ProcessInfo> readOnlyList3;
		try
		{
			IReadOnlyList<ProcessInfo> readOnlyList2;
			if (!WantsProcesses)
			{
				IReadOnlyList<ProcessInfo> readOnlyList = Array.Empty<ProcessInfo>();
				readOnlyList2 = readOnlyList;
			}
			else
			{
				readOnlyList2 = processes.Sample();
			}
			readOnlyList3 = readOnlyList2;
		}
		catch
		{
			readOnlyList3 = Array.Empty<ProcessInfo>();
		}
		return new Snapshot(DateTime.Now, cpu, gpus, ReadMemory(), disks, ReadNetwork(), access)
		{
			System = (((object)systemInfo == null) ? null : systemInfo with
			{
				Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64)
			}),
			Volumes = volumes,
			BoardName = hardwareView.BoardName,
			Fans = Spinning(hardwareView.Fans),
			BoardTemps = hardwareView.BoardTemps,
			Battery = ReadBattery(hardware),
			Processes = readOnlyList3
		};
	}

	private static SensorNode Node(LibreHardwareMonitor.Hardware.IHardware h)
	{
		return new SensorNode(h.HardwareType.ToString(), h.Name, h.Identifier.ToString(), h.Sensors.Select(delegate(LibreHardwareMonitor.Hardware.ISensor s)
		{
			string type = s.SensorType.ToString();
			string name = s.Name;
			float? value = s.Value;
			double? value2;
			if (value.HasValue)
			{
				float valueOrDefault = value.GetValueOrDefault();
				if (float.IsFinite(valueOrDefault))
				{
					value2 = valueOrDefault;
					goto IL_004c;
				}
			}
			value2 = null;
			goto IL_004c;
			IL_004c:
			return new Lumen.Core.SensorValue(type, name, value2, s.Index, s.Identifier.ToString());
		}).ToList(), h.SubHardware.Select(Node).ToList());
	}

	private IReadOnlyList<FanInfo> Spinning(IReadOnlyList<SensorPicker.FanReading> fans)
	{
		List<FanInfo> list = new List<FanInfo>();
		foreach (SensorPicker.FanReading fan in fans)
		{
			if (fan.Rpm > 0.0)
			{
				spinning.Add(fan.Key);
			}
			if (spinning.Contains(fan.Key))
			{
				list.Add(new FanInfo(fan.Name, fan.Rpm, fan.Percent));
			}
		}
		return list;
	}

	[DllImport("kernel32.dll")]
	private static extern bool GetSystemPowerStatus(out PowerStatus status);

	private static BatteryInfo? ReadBattery(List<SensorNode> hardware)
	{
		if (!GetSystemPowerStatus(out var status) || (status.BatteryFlag & 0x80) != 0 || status.BatteryFlag == byte.MaxValue || status.BatteryLifePercent > 100)
		{
			return null;
		}
		bool flag = status.ACLineStatus == 1;
		bool flag2 = (status.BatteryFlag & 8) != 0;
		SensorNode sensorNode = hardware.FirstOrDefault((SensorNode h) => h.Type == "Battery");
		double? powerW = null;
		double? wearPercent = null;
		double? minutesLeft = ((status.BatteryLifeTime > 0) ? new double?((double)status.BatteryLifeTime / 60.0) : ((double?)null));
		if (sensorNode != null)
		{
			List<Lumen.Core.SensorValue> sensors = sensorNode.All.ToList();
			double? num = SensorPicker.Find(sensors, "Power", "Charge Rate");
			if (num.HasValue)
			{
				double valueOrDefault = num.GetValueOrDefault();
				powerW = valueOrDefault;
			}
			else
			{
				num = SensorPicker.Find(sensors, "Power", "Discharge Rate");
				if (num.HasValue)
				{
					double valueOrDefault2 = num.GetValueOrDefault();
					powerW = 0.0 - Math.Abs(valueOrDefault2);
				}
				else
				{
					num = SensorPicker.Find(sensors, "Power", "Charge/Discharge Rate");
					if (num.HasValue)
					{
						double valueOrDefault3 = num.GetValueOrDefault();
						powerW = ((flag2 || flag) ? Math.Abs(valueOrDefault3) : (0.0 - Math.Abs(valueOrDefault3)));
					}
				}
			}
			wearPercent = SensorPicker.Find(sensors, "Level", "Degradation Level");
			if (!minutesLeft.HasValue)
			{
				num = SensorPicker.Find(sensors, "TimeSpan", "Remaining Time (Estimated)");
				if (num.HasValue)
				{
					double valueOrDefault4 = num.GetValueOrDefault();
					if (valueOrDefault4 > 0.0)
					{
						minutesLeft = valueOrDefault4 / 60.0;
					}
				}
			}
		}
		return new BatteryInfo((int)status.BatteryLifePercent, flag, flag2, powerW, minutesLeft, wearPercent);
	}

	private double? AcpiTemperature()
	{
		if (!admin || !OperatingSystem.IsWindows())
		{
			return null;
		}
		if (clock.Elapsed.TotalSeconds - acpiAt < 5.0)
		{
			return acpi;
		}
		acpiAt = clock.Elapsed.TotalSeconds;
		try
		{
			ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher("root\\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
			try
			{
				double? num = null;
				foreach (ManagementBaseObject item in managementObjectSearcher.Get())
				{
					double num2 = Convert.ToDouble(item["CurrentTemperature"]) / 10.0 - 273.15;
					if (num2 > 30.0 && num2 < 105.0 && (!num.HasValue || num2 > num))
					{
						num = Math.Round(num2, 1);
					}
				}
				acpi = num;
			}
			finally
			{
				((IDisposable)managementObjectSearcher)?.Dispose();
			}
		}
		catch
		{
			acpi = null;
			acpiAt = 8.988465674311579E+307;
		}
		return acpi;
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern nint CreateFile(string name, uint access, uint share, nint security, uint creation, uint flags, nint template);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool DeviceIoControl(nint device, uint code, nint inBuffer, int inSize, byte[] outBuffer, int outSize, out int returned, nint overlapped);

	[DllImport("kernel32.dll")]
	private static extern bool CloseHandle(nint handle);

	private Dictionary<int, string> DiskLetters()
	{
		if (clock.Elapsed.TotalSeconds - lettersAt < 30.0)
		{
			return letters;
		}
		lettersAt = clock.Elapsed.TotalSeconds;
		Dictionary<int, List<string>> dictionary = new Dictionary<int, List<string>>();
		DriveInfo[] drives = DriveInfo.GetDrives();
		foreach (DriveInfo driveInfo in drives)
		{
			try
			{
				DriveType driveType = driveInfo.DriveType;
				if ((uint)(driveType - 2) > 1u)
				{
					continue;
				}
				string text = driveInfo.Name.TrimEnd('\\');
				nint num = CreateFile("\\\\.\\" + text, 0u, 3u, IntPtr.Zero, 3u, 0u, IntPtr.Zero);
				if (num == new IntPtr(-1))
				{
					continue;
				}
				try
				{
					byte[] array = new byte[256];
					if (!DeviceIoControl(num, 5636096u, IntPtr.Zero, 0, array, array.Length, out var _, IntPtr.Zero))
					{
						continue;
					}
					int num2 = BitConverter.ToInt32(array, 0);
					for (int j = 0; j < num2 && 8 + j * 24 + 4 <= array.Length; j++)
					{
						int key = BitConverter.ToInt32(array, 8 + j * 24);
						if (!dictionary.TryGetValue(key, out var value))
						{
							value = (dictionary[key] = new List<string>());
						}
						if (!value.Contains(text))
						{
							value.Add(text);
						}
					}
				}
				finally
				{
					CloseHandle(num);
				}
			}
			catch
			{
			}
		}
		letters = dictionary.ToDictionary((KeyValuePair<int, List<string>> kv) => kv.Key, (KeyValuePair<int, List<string>> kv) => string.Join(" ", kv.Value.OrderBy((string x) => x)));
		return letters;
	}

	private static int? DiskNumber(string id)
	{
		return SensorPicker.DiskNumber(id);
	}

	[DllImport("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)]
	private static extern bool DeviceIoControlIn(nint device, uint code, byte[] inBuffer, int inSize, byte[] outBuffer, int outSize, out int returned, nint overlapped);

	private List<SensorMerge.PhysicalDisk> PhysicalDisks()
	{
		if (!OperatingSystem.IsWindows() || clock.Elapsed.TotalSeconds - physicalAt < 60.0)
		{
			return physical;
		}
		physicalAt = clock.Elapsed.TotalSeconds;
		List<SensorMerge.PhysicalDisk> list = new List<SensorMerge.PhysicalDisk>();
		for (int i = 0; i < 32; i++)
		{
			nint num = CreateFile("\\\\.\\PhysicalDrive" + i, 0u, 3u, IntPtr.Zero, 3u, 0u, IntPtr.Zero);
			if (num == new IntPtr(-1))
			{
				continue;
			}
			try
			{
				byte[] array = new byte[12];
				byte[] buffer = new byte[1024];
				if (DeviceIoControlIn(num, 2954240u, array, array.Length, buffer, buffer.Length, out var got, IntPtr.Zero) && got >= 32)
				{
					list.Add(new SensorMerge.PhysicalDisk(i, (Text(12) + " " + Text(16)).Trim(), Text(24)));
				}
				string Text(int offsetAt)
				{
					int num2 = BitConverter.ToInt32(buffer, offsetAt);
					if (num2 <= 0 || num2 >= got)
					{
						return "";
					}
					int num3 = Array.IndexOf(buffer, (byte)0, num2);
					return Encoding.ASCII.GetString(buffer, num2, ((num3 < 0) ? got : num3) - num2).Trim();
				}
			}
			catch
			{
			}
			finally
			{
				CloseHandle(num);
			}
		}
		physical = list;
		return physical;
	}

	private (double?, double?)? DiskRates(int disk)
	{
		if (!OperatingSystem.IsWindows())
		{
			return null;
		}
		nint num = CreateFile("\\\\.\\PhysicalDrive" + disk, 0u, 3u, IntPtr.Zero, 3u, 0u, IntPtr.Zero);
		if (num == new IntPtr(-1))
		{
			return null;
		}
		try
		{
			byte[] array = new byte[88];
			if (!DeviceIoControl(num, 458784u, IntPtr.Zero, 0, array, array.Length, out var _, IntPtr.Zero))
			{
				return null;
			}
			long num2 = BitConverter.ToInt64(array, 0);
			long num3 = BitConverter.ToInt64(array, 8);
			double totalSeconds = clock.Elapsed.TotalSeconds;
			(long, long, double) value;
			bool num4 = diskCounters.TryGetValue(disk, out value);
			diskCounters[disk] = (num2, num3, totalSeconds);
			if (!num4 || totalSeconds <= value.Item3)
			{
				return (null, null);
			}
			return ((double)Math.Max(0L, num2 - value.Item1) / (totalSeconds - value.Item3), (double)Math.Max(0L, num3 - value.Item2) / (totalSeconds - value.Item3));
		}
		finally
		{
			CloseHandle(num);
		}
	}

	[DllImport("kernel32.dll")]
	private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);

	private double? SystemLoad()
	{
		if (!GetSystemTimes(out var idle, out var kernel, out var user))
		{
			return null;
		}
		ulong num = idle - lastIdle;
		ulong num2 = kernel - lastKernel;
		ulong num3 = user - lastUser;
		bool num4 = lastKernel == 0;
		lastIdle = idle;
		lastKernel = kernel;
		lastUser = user;
		ulong num5 = num2 + num3;
		if (!num4 && num5 != 0L)
		{
			return Math.Clamp(100.0 * (double)(num5 - num) / (double)num5, 0.0, 100.0);
		}
		return null;
	}

	private static SensorNode Node(OpenHardwareMonitor.Hardware.IHardware h)
	{
		return new SensorNode(h.HardwareType.ToString(), h.Name, "ohm:" + h.Identifier, h.Sensors.Select(delegate(OpenHardwareMonitor.Hardware.ISensor s)
		{
			string type = s.SensorType.ToString();
			string name = s.Name;
			float? value = s.Value;
			double? value2;
			if (value.HasValue)
			{
				float valueOrDefault = value.GetValueOrDefault();
				if (float.IsFinite(valueOrDefault))
				{
					value2 = valueOrDefault;
					goto IL_004c;
				}
			}
			value2 = null;
			goto IL_004c;
			IL_004c:
			return new Lumen.Core.SensorValue(type, name, value2, s.Index, s.Identifier.ToString());
		}).ToList(), (h.SubHardware ?? Array.Empty<OpenHardwareMonitor.Hardware.IHardware>()).Select(Node).ToList());
	}

	private List<SensorNode> ReadOhm()
	{
		List<SensorNode> list = new List<SensorNode>();
		foreach (OpenHardwareMonitor.Hardware.IHardware item in ohm.Hardware)
		{
			try
			{
				item.Update();
				list.Add(Node(item));
			}
			catch
			{
			}
		}
		return list;
	}

	public string Report()
	{
		StringBuilder b = new StringBuilder();
		StringBuilder stringBuilder = b;
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(9, 2, stringBuilder);
		handler.AppendLiteral("Lumen ");
		handler.AppendFormatted(typeof(WindowsSource).Assembly.GetName().Version?.ToString(3));
		handler.AppendLiteral(" · ");
		handler.AppendFormatted(DateTime.Now, "yyyy-MM-dd HH:mm:ss");
		stringBuilder2.AppendLine(ref handler);
		stringBuilder = b;
		StringBuilder stringBuilder3 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(51, 3, stringBuilder);
		handler.AppendLiteral("Windows: ");
		handler.AppendFormatted(Environment.OSVersion.VersionString);
		handler.AppendLiteral(" · administrator: ");
		handler.AppendFormatted(admin);
		handler.AppendLiteral(" · LibreHardwareMonitor ");
		handler.AppendFormatted(typeof(Computer).Assembly.GetName().Version);
		stringBuilder3.AppendLine(ref handler);
		b.AppendLine();
		b.AppendLine("=== LibreHardwareMonitor ===");
		foreach (LibreHardwareMonitor.Hardware.IHardware item in computer.Hardware)
		{
			Dump(item, "");
		}
		b.AppendLine();
		b.AppendLine("=== Open Hardware Monitor (driverless: vendor GPU APIs, SMART) ===");
		if (ohmError != null)
		{
			b.AppendLine("Not available: " + ohmError);
		}
		if (ohm != null)
		{
			foreach (string diagnostic in ohm.Diagnostics)
			{
				b.AppendLine("Note: " + diagnostic);
			}
			foreach (OpenHardwareMonitor.Hardware.IHardware item2 in ohm.Hardware)
			{
				stringBuilder = b;
				StringBuilder stringBuilder4 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(7, 3, stringBuilder);
				handler.AppendLiteral("[");
				handler.AppendFormatted(item2.HardwareType);
				handler.AppendLiteral("] ");
				handler.AppendFormatted(item2.Name);
				handler.AppendLiteral("  (");
				handler.AppendFormatted(item2.Identifier);
				handler.AppendLiteral(")");
				stringBuilder4.AppendLine(ref handler);
				OpenHardwareMonitor.Hardware.ISensor[] sensors = item2.Sensors;
				foreach (OpenHardwareMonitor.Hardware.ISensor sensor in sensors)
				{
					stringBuilder = b;
					StringBuilder stringBuilder5 = stringBuilder;
					handler = new StringBuilder.AppendInterpolatedStringHandler(6, 3, stringBuilder);
					handler.AppendLiteral("    ");
					handler.AppendFormatted(sensor.SensorType, -12);
					handler.AppendLiteral(" ");
					handler.AppendFormatted<string>(sensor.Name, -34);
					handler.AppendLiteral(" ");
					float? value = sensor.Value;
					handler.AppendFormatted(value.HasValue ? value.GetValueOrDefault().ToString("0.###", CultureInfo.InvariantCulture) : "—");
					stringBuilder5.AppendLine(ref handler);
				}
			}
		}
		b.AppendLine();
		stringBuilder = b;
		StringBuilder stringBuilder6 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(35, 1, stringBuilder);
		handler.AppendLiteral("=== External source (setting: ");
		handler.AppendFormatted(sourceChoice());
		handler.AppendLiteral(") ===");
		stringBuilder6.AppendLine(ref handler);
		if (sourceChoice() == SensorSource.Builtin)
		{
			b.AppendLine("Not used.");
		}
		else if (externalName == null)
		{
			b.AppendLine("Not reachable – AIDA64/HWiNFO not running or shared memory switched off; built-in readings are shown.");
		}
		else
		{
			b.AppendLine("Reading from " + externalName + (Paused ? " – LibreHardwareMonitor and Open Hardware Monitor are paused" : ""));
			string lastText = external.LastText;
			if (lastText != null)
			{
				b.AppendLine(Regex.Replace(lastText, "(</(sys|temp|fan|duty|volt|curr|pwr)>)", "$1" + Environment.NewLine));
			}
		}
		b.AppendLine();
		b.AppendLine("=== Combined ===");
		if (mergeLog.Count == 0)
		{
			b.AppendLine("Nothing needed from Open Hardware Monitor or an external source.");
		}
		foreach (string item3 in mergeLog)
		{
			b.AppendLine(item3);
		}
		return b.ToString();
		void Dump(LibreHardwareMonitor.Hardware.IHardware x, string indent)
		{
			StringBuilder stringBuilder7 = b;
			StringBuilder stringBuilder8 = stringBuilder7;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(7, 4, stringBuilder7);
			handler2.AppendFormatted(indent);
			handler2.AppendLiteral("[");
			handler2.AppendFormatted(x.HardwareType);
			handler2.AppendLiteral("] ");
			handler2.AppendFormatted(x.Name);
			handler2.AppendLiteral("  (");
			handler2.AppendFormatted(x.Identifier);
			handler2.AppendLiteral(")");
			stringBuilder8.AppendLine(ref handler2);
			LibreHardwareMonitor.Hardware.ISensor[] sensors2 = x.Sensors;
			foreach (LibreHardwareMonitor.Hardware.ISensor sensor2 in sensors2)
			{
				stringBuilder7 = b;
				StringBuilder stringBuilder9 = stringBuilder7;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(6, 4, stringBuilder7);
				handler2.AppendFormatted(indent);
				handler2.AppendLiteral("    ");
				handler2.AppendFormatted(sensor2.SensorType, -12);
				handler2.AppendLiteral(" ");
				handler2.AppendFormatted<string>(sensor2.Name, -34);
				handler2.AppendLiteral(" ");
				float? value2 = sensor2.Value;
				handler2.AppendFormatted(value2.HasValue ? value2.GetValueOrDefault().ToString("0.###", CultureInfo.InvariantCulture) : "—");
				stringBuilder9.AppendLine(ref handler2);
			}
			LibreHardwareMonitor.Hardware.IHardware[] subHardware = x.SubHardware;
			for (int j = 0; j < subHardware.Length; j++)
			{
				Dump(subHardware[j], indent + "  ");
			}
		}
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

	private static MemoryInfo? ReadMemory()
	{
		MemoryStatus status = new MemoryStatus
		{
			Length = (uint)Marshal.SizeOf<MemoryStatus>()
		};
		if (!GlobalMemoryStatusEx(ref status))
		{
			return null;
		}
		return new MemoryInfo((double)(status.TotalPhys - status.AvailPhys) / 1073741824.0, (double)status.TotalPhys / 1073741824.0);
	}

	private NetworkInfo? ReadNetwork()
	{
		NetworkInterface networkInterface;
		try
		{
			List<NetworkInterface> source = NetworkInterface.GetAllNetworkInterfaces().Where(delegate(NetworkInterface n)
			{
				bool flag = n.OperationalStatus == OperationalStatus.Up;
				if (flag)
				{
					NetworkInterfaceType networkInterfaceType = n.NetworkInterfaceType;
					bool flag2 = ((networkInterfaceType == NetworkInterfaceType.Loopback || networkInterfaceType == NetworkInterfaceType.Tunnel) ? true : false);
					flag = !flag2;
				}
				return flag;
			}).ToList();
			SensorPicker.Nic pick = SensorPicker.Network(source.Select(delegate(NetworkInterface n)
			{
				bool hasGateway = false;
				long bytesReceived = 0L;
				try
				{
					hasGateway = n.GetIPProperties().GatewayAddresses.Any((GatewayIPAddressInformation g) => !g.Address.Equals(IPAddress.Any) && !g.Address.Equals(IPAddress.IPv6Any));
				}
				catch
				{
				}
				try
				{
					bytesReceived = n.GetIPStatistics().BytesReceived;
				}
				catch
				{
				}
				return new SensorPicker.Nic(n.Id, n.Description, hasGateway, bytesReceived);
			}).ToList(), nicId);
			networkInterface = (((object)pick == null) ? null : source.First((NetworkInterface n) => n.Id == pick.Id));
		}
		catch
		{
			return null;
		}
		if (networkInterface == null)
		{
			return null;
		}
		IPInterfaceStatistics iPStatistics = networkInterface.GetIPStatistics();
		double totalSeconds = clock.Elapsed.TotalSeconds;
		double? downloadBytesPerSecond = null;
		double? uploadBytesPerSecond = null;
		if (networkInterface.Id == nicId && totalSeconds > lastTime)
		{
			downloadBytesPerSecond = (double)Math.Max(0L, iPStatistics.BytesReceived - lastRx) / (totalSeconds - lastTime);
			uploadBytesPerSecond = (double)Math.Max(0L, iPStatistics.BytesSent - lastTx) / (totalSeconds - lastTime);
		}
		nicId = networkInterface.Id;
		lastRx = iPStatistics.BytesReceived;
		lastTx = iPStatistics.BytesSent;
		lastTime = totalSeconds;
		string address = networkInterface.GetIPProperties().UnicastAddresses.FirstOrDefault((UnicastIPAddressInformation a) => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString();
		return new NetworkInfo(networkInterface.Description, downloadBytesPerSecond, uploadBytesPerSecond, (networkInterface.Speed > 0) ? new double?(networkInterface.Speed) : ((double?)null), address);
	}

	public void Dispose()
	{
		try
		{
			computer.Close();
		}
		catch
		{
		}
		try
		{
			ohm?.Dispose();
		}
		catch
		{
		}
	}
}
