using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace Lumen.App;

public sealed class LumenApp : Application
{
	public override void Initialize()
	{
		base.Styles.Add(new FluentTheme());
		base.Name = "Lumen";
	}

	public override void OnFrameworkInitializationCompleted()
	{
		Dispatcher.UIThread.UnhandledException += delegate(object _, DispatcherUnhandledExceptionEventArgs e)
		{
			CrashLog.Write(e.Exception, "ui");
			e.Handled = true;
		};
		if (base.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime classicDesktopStyleApplicationLifetime)
		{
			new Controller(classicDesktopStyleApplicationLifetime, classicDesktopStyleApplicationLifetime.Args?.Contains("--demo") ?? false).Start();
		}
		base.OnFrameworkInitializationCompleted();
	}
}
