using System.Windows;
using Circuids.Pulse.Extensions;
using Circuids.Pulse.WPF.ConformanceHost.Conformance;
using Microsoft.Extensions.DependencyInjection;

namespace Circuids.Pulse.WPF.ConformanceHost;

public partial class App : Application
{
	private ServiceProvider? _provider;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		var services = new ServiceCollection();
		services.AddSingleton<WpfHostProbe>();
		services.AddTransient<MainWindow>();
		services.AddPulse(p =>
		{
			p.AssignedPlatform = "WPF";
			p.DefaultTestTimeout = TimeSpan.FromSeconds(10);
			p.AddSuite<WpfRuntimeBoundarySuite>();
			p.AddSuite<WpfApplicationBoundarySuite>();
			p.AddSuite<WpfDispatcherBoundarySuite>();
			p.AddSuite<WpfFileStorageBoundarySuite>();
		});

		_provider = services.BuildServiceProvider();
		MainWindow = _provider.GetRequiredService<MainWindow>();
		MainWindow.Show();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		_provider?.Dispose();
		base.OnExit(e);
	}
}

