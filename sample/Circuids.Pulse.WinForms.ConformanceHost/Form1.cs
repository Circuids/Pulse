using System.Text.Json;
using Circuids.Pulse;

namespace Circuids.Pulse.WinForms.ConformanceHost;

public partial class Form1 : Form
{
    private readonly ITestExecutor _executor;
    private readonly Button _runButton = new();
    private readonly Label _summaryLabel = new();
    private readonly DataGridView _resultsGrid = new();
    private readonly TextBox _jsonTextBox = new();

    public Form1(ITestExecutor executor, WinFormsHostProbe probe)
    {
        _executor = executor;
        InitializeComponent();
        probe.MainControl = this;
        probe.UiThreadId = Environment.CurrentManagedThreadId;
        ConfigureLayout();
    }

    private void ConfigureLayout()
    {
        Text = "Pulse WinForms Conformance";
        Width = 1100;
        Height = 760;

        _runButton.Text = "Run conformance";
        _runButton.Dock = DockStyle.Top;
        _runButton.Height = 40;
        _runButton.Click += RunButton_Click;

        _summaryLabel.Dock = DockStyle.Top;
        _summaryLabel.Height = 34;
        _summaryLabel.TextAlign = ContentAlignment.MiddleLeft;

        _resultsGrid.Dock = DockStyle.Fill;
        _resultsGrid.ReadOnly = true;
        _resultsGrid.AllowUserToAddRows = false;
        _resultsGrid.AllowUserToDeleteRows = false;
        _resultsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        _jsonTextBox.Dock = DockStyle.Bottom;
        _jsonTextBox.Height = 180;
        _jsonTextBox.Multiline = true;
        _jsonTextBox.ScrollBars = ScrollBars.Both;
        _jsonTextBox.WordWrap = false;

        Controls.Add(_resultsGrid);
        Controls.Add(_jsonTextBox);
        Controls.Add(_summaryLabel);
        Controls.Add(_runButton);
    }

    private async void RunButton_Click(object? sender, EventArgs e)
    {
        _runButton.Enabled = false;
        _runButton.Text = "Running...";

        try
        {
            var report = await _executor.RunAsync();
            _summaryLabel.Text = $"{(report.Success ? "PASS" : "FAIL")} - {report.Total} total, {report.Passed} passed, {report.Failed} failed, {report.Skipped} skipped";
            _jsonTextBox.Text = JsonSerializer.Serialize(report, PulseJsonContext.Default.TestRunReport);
            _resultsGrid.DataSource = report.Results.Select(ResultRow.From).ToList();
        }
        finally
        {
            _runButton.Enabled = true;
            _runButton.Text = "Re-run";
        }
    }

    private sealed record ResultRow(string Outcome, string Suite, string Test, string Duration, string Message)
    {
        public static ResultRow From(TestResult result) => new(
            result.Outcome.ToString(),
            ShortSuite(result.SuiteName),
            result.TestName,
            $"{result.Duration.TotalMilliseconds:0} ms",
            result.Message ?? string.Empty);

        private static string ShortSuite(string suiteName)
        {
            var index = suiteName.LastIndexOf('.');
            return index >= 0 ? suiteName[(index + 1)..] : suiteName;
        }
    }
}
