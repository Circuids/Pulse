namespace Circuids.Pulse.WinForms.ConformanceHost;

public sealed class WinFormsHostProbe
{
    public Control? MainControl { get; set; }

    public int UiThreadId { get; set; }
}