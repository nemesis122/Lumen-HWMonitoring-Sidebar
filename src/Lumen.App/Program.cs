using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;

namespace Lumen.App;

public static class Program
{
	[STAThread]
	public static int Main(string[] args)
	{
		bool createdNew;
		using Mutex mutex = new Mutex(initiallyOwned: true, "Local\\Lumen.Sidebar", out createdNew);
		if (!createdNew && args.Contains("--restarted"))
		{
			try
			{
				createdNew = mutex.WaitOne(TimeSpan.FromSeconds(20.0));
			}
			catch (AbandonedMutexException)
			{
				createdNew = true;
			}
		}
		if (!createdNew && !args.Contains("--elevated"))
		{
			return 0;
		}
		Diagnostics.Initialize();
		AppDomain.CurrentDomain.UnhandledException += delegate(object _, UnhandledExceptionEventArgs e)
		{
			CrashLog.Write(e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString() ?? "Unknown fatal error"), "fatal");
		};
		TaskScheduler.UnobservedTaskException += delegate(object? _, UnobservedTaskExceptionEventArgs e)
		{
			CrashLog.Write(e.Exception, "task");
			e.SetObserved();
		};
		return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
	}

	public static AppBuilder BuildAvaloniaApp()
	{
		return AppBuilder.Configure<LumenApp>().UsePlatformDetect().WithInterFont();
	}
}

