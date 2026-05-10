using System.IO;
using System.Reflection;
using System.Windows;

namespace Circuids.Pulse.WPF.ConformanceHost.Conformance;

public sealed class WpfApplicationBoundarySuite
{
    private readonly WpfHostProbe _probe;

    public WpfApplicationBoundarySuite(WpfHostProbe probe)
    {
        _probe = probe;
    }

    [PulseCase]
    public void Application_current_is_available()
    {
        PulseAssert.NotNull(Application.Current, "Pulse must run inside a live WPF Application.");
    }

    [PulseCase]
    public void Main_window_is_registered()
    {
        PulseAssert.Same(_probe.MainWindow, Application.Current.MainWindow, "The DI-created main window must be the WPF Application.MainWindow.");
    }

    [PulseCase]
    public void Application_dispatcher_is_available()
    {
        PulseAssert.Same(_probe.Dispatcher, Application.Current.Dispatcher, "Pulse must use the same dispatcher as the WPF application.");
    }

    [PulseCase]
    public void Entry_assembly_location_exists()
    {
        var location = Assembly.GetEntryAssembly()?.Location;
        PulseAssert.False(string.IsNullOrWhiteSpace(location), "The WPF host must expose an entry assembly location.");
        PulseAssert.True(File.Exists(location), $"Entry assembly path must exist: {location}");
    }
}