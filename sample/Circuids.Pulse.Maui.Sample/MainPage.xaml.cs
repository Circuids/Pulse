namespace Circuids.Pulse.Maui.Sample;

public partial class MainPage : ContentPage
{
    private readonly ITestExecutor _executor;

    public MainPage(ITestExecutor executor)
    {
        InitializeComponent();
        _executor = executor;
    }

    private async void OnRunClicked(object? sender, EventArgs e)
    {
        RunButton.IsEnabled = false;
        RunButton.Text = "Running…";
        SummaryLabel.Text = string.Empty;
        EnvironmentLabel.Text = string.Empty;
        ResultsView.ItemsSource = null;

        try
        {
            var report = await _executor.RunAsync();

            SummaryLabel.Text =
                $"{(report.Success ? "PASS" : "FAIL")}  ·  " +
                $"{report.Total} total · {report.Passed} passed · " +
                $"{report.Failed} failed · {report.Skipped} skipped";

            EnvironmentLabel.Text =
                $"Platform: {report.AssignedPlatform}\n" +
                $"Framework: {report.RuntimeEnvironment.FrameworkDescription}\n" +
                $"RID: {report.RuntimeEnvironment.RuntimeIdentifier}\n" +
                $"OS: {report.RuntimeEnvironment.OSDescription}";

            ResultsView.ItemsSource = report.Results.Select(r => new ResultRow(r)).ToList();
        }
        finally
        {
            RunButton.IsEnabled = true;
            RunButton.Text = "Re-run";
        }
    }

    private sealed record ResultRow(string Outcome, string TestName, string DurationText, string Message, bool HasMessage)
    {
        public ResultRow(TestResult r) : this(
            Outcome: r.Outcome.ToString().ToUpperInvariant(),
            TestName: $"{ShortSuite(r.SuiteName)} · {r.TestName}",
            DurationText: $"{r.Duration.TotalMilliseconds:0} ms",
            Message: r.Message ?? string.Empty,
            HasMessage: !string.IsNullOrEmpty(r.Message))
        { }

        private static string ShortSuite(string fullName)
        {
            var idx = fullName.LastIndexOf('.');
            return idx >= 0 ? fullName[(idx + 1)..] : fullName;
        }
    }
}
