using Circuids.Pulse.Blazor.WebAssembly.Sample;
using Circuids.Pulse.Blazor.WebAssembly.Sample.Conformance;
using Circuids.Pulse.Extensions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register Pulse with the conformance suites this binary should run when /conformance is hit.
builder.Services.AddPulse(p =>
{
    p.AssignedPlatform = "Blazor.WebAssembly";
    p.DefaultTestTimeout = TimeSpan.FromSeconds(10);
    p.AddSuite<BrowserRuntimeBoundarySuite>();
    p.AddSuite<HttpHostBoundarySuite>();
    p.AddSuite<BrowserInteropBoundarySuite>();
    p.AddSuite<BrowserStorageBoundarySuite>();
});

await builder.Build().RunAsync();
