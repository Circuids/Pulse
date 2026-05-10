using Microsoft.Extensions.DependencyInjection;

namespace Circuids.Pulse.Maui.Sample;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}