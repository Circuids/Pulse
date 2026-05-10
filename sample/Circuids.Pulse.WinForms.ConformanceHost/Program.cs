using Circuids.Pulse.Extensions;
using Circuids.Pulse.WinForms.ConformanceHost.Conformance;
using Microsoft.Extensions.DependencyInjection;

namespace Circuids.Pulse.WinForms.ConformanceHost;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var services = new ServiceCollection();
        services.AddSingleton<WinFormsHostProbe>();
        services.AddTransient<Form1>();
        services.AddPulse(p =>
        {
            p.AssignedPlatform = "WinForms";
            p.DefaultTestTimeout = TimeSpan.FromSeconds(10);
            p.AddSuite<WinFormsRuntimeBoundarySuite>();
            p.AddSuite<WinFormsApplicationBoundarySuite>();
            p.AddSuite<WinFormsUiThreadBoundarySuite>();
            p.AddSuite<WinFormsFileStorageBoundarySuite>();
        });

        using var provider = services.BuildServiceProvider();
        Application.Run(provider.GetRequiredService<Form1>());
    }
}