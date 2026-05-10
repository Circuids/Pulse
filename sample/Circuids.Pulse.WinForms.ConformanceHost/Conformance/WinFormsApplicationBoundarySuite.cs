namespace Circuids.Pulse.WinForms.ConformanceHost.Conformance;

public sealed class WinFormsApplicationBoundarySuite
{
    private readonly WinFormsHostProbe _probe;

    public WinFormsApplicationBoundarySuite(WinFormsHostProbe probe)
    {
        _probe = probe;
    }

    [PulseCase]
    public void Application_message_loop_is_running()
    {
        PulseAssert.True(Application.MessageLoop, "Pulse must run inside the active WinForms message loop.");
    }

    [PulseCase]
    public void Main_form_is_registered_in_open_forms()
    {
        var control = _probe.MainControl ?? throw new InvalidOperationException("Main control is not registered.");
        PulseAssert.Contains((Form)control, Application.OpenForms.Cast<Form>(), "The host form must be part of Application.OpenForms.");
    }

    [PulseCase]
    public void Application_identity_is_available()
    {
        PulseAssert.False(string.IsNullOrWhiteSpace(Application.ProductName), "WinForms must expose an application product name.");
    }

    [PulseCase]
    public void Executable_path_points_to_existing_file()
    {
        PulseAssert.True(File.Exists(Application.ExecutablePath), $"Executable path must exist: {Application.ExecutablePath}");
    }
}