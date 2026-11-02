using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using Lumen.Core;

namespace Lumen.App;

public class Platform
{
	private const string Task = "Lumen Sidebar";

	public string SettingsPath { get; init; } = Settings.DefaultPath();

	public virtual bool IsAdministrator
	{
		get
		{
			if (!OperatingSystem.IsWindows())
			{
				return false;
			}
			using WindowsIdentity ntIdentity = WindowsIdentity.GetCurrent();
			return new WindowsPrincipal(ntIdentity).IsInRole(WindowsBuiltInRole.Administrator);
		}
	}

	public static string ExePath => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Lumen.exe");

	public virtual bool AutostartEnabled
	{
		get
		{
			if (OperatingSystem.IsWindows())
			{
				return RunSchtasks("/Query /TN \"Lumen Sidebar\"", elevate: false) == 0;
			}
			return false;
		}
	}

	public virtual void RestartElevated()
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}
		try
		{
			Process.Start(new ProcessStartInfo(ExePath)
			{
				UseShellExecute = true,
				Verb = "runas",
				Arguments = "--elevated"
			});
			Environment.Exit(0);
		}
		catch (Win32Exception)
		{
		}
	}

	public virtual bool SetAutostart(bool on)
	{
		if (!OperatingSystem.IsWindows())
		{
			return false;
		}
		return RunSchtasks(on ? $"/Create /F /TN \"{"Lumen Sidebar"}\" /SC ONLOGON /RL HIGHEST /DELAY 0000:10 /TR \"\\\"{ExePath}\\\"\"" : "/Delete /F /TN \"Lumen Sidebar\"", !IsAdministrator) == 0;
	}

	private static int RunSchtasks(string args, bool elevate)
	{
		try
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo("schtasks.exe", args)
			{
				CreateNoWindow = true,
				UseShellExecute = elevate,
				WindowStyle = ProcessWindowStyle.Hidden
			};
			if (elevate)
			{
				processStartInfo.Verb = "runas";
			}
			using Process process = Process.Start(processStartInfo);
			if (process == null)
			{
				return -1;
			}
			process.WaitForExit(15000);
			return process.ExitCode;
		}
		catch
		{
			return -1;
		}
	}
}
