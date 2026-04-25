using Circuids.Pulse.Extensions;
using Circuids.Pulse.Maui.Sample.Conformance;
using Microsoft.Extensions.Logging;

namespace Circuids.Pulse.Maui.Sample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Pages resolved through DI so MainPage receives ITestExecutor via its constructor.
        builder.Services.AddTransient<MainPage>();

        // Register Pulse with the conformance suites this binary should run when the user
        // taps "Run conformance" in MainPage.
        builder.Services.AddPulse(p =>
        {
            p.AssignedPlatform = "MAUI";
            p.DefaultTestTimeout = TimeSpan.FromSeconds(10);
            p.AddSuite<MauiHostSuite>();
            p.AddSuite<DispatcherSuite>();
            p.AddSuite<PreferencesSuite>();
            p.AddSuite<ViewportMatrixSuite>();
            p.AddSuite<LifetimeAndTimeoutSuite>();
        });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
