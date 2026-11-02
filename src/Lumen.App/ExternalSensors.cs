using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using Lumen.Core;

namespace Lumen.App;

public sealed class ExternalSensors
{
	public const string AidaMap = "AIDA64_SensorValues";

	public const string HwinfoMap = "Global\\HWiNFO_SENS_SM2";

	private readonly Stopwatch clock = Stopwatch.StartNew();

	private readonly Dictionary<SensorSource, double> missedAt = new Dictionary<SensorSource, double>();

	public static volatile string Status = "";

	public SensorSource? Active { get; private set; }

	public string? LastText { get; private set; }

	public (string Name, List<SensorNode> Nodes)? Read(SensorSource wanted)
	{
		Active = null;
		if (wanted == SensorSource.Builtin || !OperatingSystem.IsWindows())
		{
			Status = "";
			return null;
		}
		SensorSource[] array = ((wanted != SensorSource.Auto) ? new SensorSource[1] { wanted } : new SensorSource[2]
		{
			SensorSource.Aida64,
			SensorSource.HwInfo
		});
		for (int i = 0; i < array.Length; i++)
		{
			SensorSource sensorSource = array[i];
			if (missedAt.TryGetValue(sensorSource, out var value) && clock.Elapsed.TotalSeconds - value < 5.0)
			{
				continue;
			}
			try
			{
				List<SensorNode> list = ((sensorSource == SensorSource.Aida64) ? ReadAida() : ReadHwinfo());
				if (list.Count > 0)
				{
					Active = sensorSource;
					Status = "connected:" + sensorSource;
					return ((sensorSource == SensorSource.Aida64) ? "AIDA64" : "HWiNFO", list);
				}
			}
			catch
			{
			}
			missedAt[sensorSource] = clock.Elapsed.TotalSeconds;
		}
		Status = "missing:" + wanted;
		return null;
	}

	private List<SensorNode> ReadAida()
	{
		using MemoryMappedFile memoryMappedFile = MemoryMappedFile.OpenExisting("AIDA64_SensorValues", MemoryMappedFileRights.Read);
		using MemoryMappedViewStream memoryMappedViewStream = memoryMappedFile.CreateViewStream(0L, 0L, MemoryMappedFileAccess.Read);
		List<byte> list = new List<byte>(65536);
		int num;
		while ((num = memoryMappedViewStream.ReadByte()) > 0)
		{
			list.Add((byte)num);
		}
		string text = (LastText = Encoding.Latin1.GetString(list.ToArray()));
		return External.AidaNodes(External.ParseAida(text));
	}

	private List<SensorNode> ReadHwinfo()
	{
		using MemoryMappedFile memoryMappedFile = MemoryMappedFile.OpenExisting("Global\\HWiNFO_SENS_SM2", MemoryMappedFileRights.Read);
		using MemoryMappedViewAccessor memoryMappedViewAccessor = memoryMappedFile.CreateViewAccessor(0L, 0L, MemoryMappedFileAccess.Read);
		byte[] array = new byte[44];
		memoryMappedViewAccessor.ReadArray(0L, array, 0, array.Length);
		int? num = External.HwinfoSize(array);
		if (num.HasValue)
		{
			int valueOrDefault = num.GetValueOrDefault();
			if (valueOrDefault <= memoryMappedViewAccessor.Capacity)
			{
				byte[] array2 = new byte[valueOrDefault];
				memoryMappedViewAccessor.ReadArray(0L, array2, 0, valueOrDefault);
				List<HwinfoReading> list = External.ParseHwinfo(array2);
				LastText = string.Join("\n", list.Select((HwinfoReading r) => $"{r.Sensor} | {r.Label} = {r.Value:0.###} {r.Unit}"));
				return External.HwinfoNodes(list);
			}
		}
		return new List<SensorNode>();
	}
}
