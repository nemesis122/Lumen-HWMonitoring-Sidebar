using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Lumen.Core;

public static class SystemReader
{
	private static string? os;

	private static string? build;

	public static IReadOnlyList<VolumeInfo> Volumes()
	{
		List<VolumeInfo> list = new List<VolumeInfo>();
		DriveInfo[] drives = DriveInfo.GetDrives();
		foreach (DriveInfo driveInfo in drives)
		{
			try
			{
				bool flag = !driveInfo.IsReady;
				if (!flag)
				{
					DriveType driveType = driveInfo.DriveType;
					bool flag2 = (uint)(driveType - 2) <= 1u;
					flag = !flag2;
				}
				if (!flag && driveInfo.TotalSize > 0)
				{
					list.Add(new VolumeInfo(driveInfo.Name.TrimEnd('\\', '/'), driveInfo.VolumeLabel, (double)driveInfo.TotalSize / 1073741824.0, (double)driveInfo.AvailableFreeSpace / 1073741824.0));
				}
			}
			catch
			{
			}
		}
		return list.Take(8).ToList();
	}

	public static SystemInfo Read()
	{
		if (os == null)
		{
			(os, build) = Describe();
		}
		TimeSpan timeSpan = TimeSpan.FromMilliseconds(Environment.TickCount64);
		int num = 0;
		int num2 = 0;
		Process[] processes = Process.GetProcesses();
		foreach (Process process in processes)
		{
			using (process)
			{
				num++;
				try
				{
					num2 += process.Threads.Count;
				}
				catch
				{
				}
			}
		}
		return new SystemInfo(os, build, timeSpan, DateTime.Now - timeSpan, num, num2);
	}

	private static (string, string) Describe()
	{
		if (!OperatingSystem.IsWindows())
		{
			return (RuntimeInformation.OSDescription, Environment.OSVersion.Version.ToString());
		}
		try
		{
			using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion");
			string text = (registryKey?.GetValue("ProductName") as string) ?? "Windows";
			int result;
			int num = (int.TryParse(registryKey?.GetValue("CurrentBuildNumber") as string, out result) ? result : Environment.OSVersion.Version.Build);
			if (num >= 22000)
			{
				text = text.Replace("Windows 10", "Windows 11");
			}
			string text2 = (registryKey?.GetValue("DisplayVersion") as string) ?? "";
			string text3 = ((registryKey?.GetValue("UBR") is int num2) ? ("." + num2) : "");
			return (text, ((text2.Length > 0) ? (text2 + " · ") : "") + num + text3);
		}
		catch
		{
			return ("Windows", Environment.OSVersion.Version.ToString());
		}
	}
}
