using System.Windows.Threading;

namespace Circuids.Pulse.WPF.ConformanceHost;

public sealed class WpfHostProbe
{
    public MainWindow? MainWindow { get; set; }

    public Dispatcher? Dispatcher { get; set; }

    public int UiThreadId { get; set; }
}