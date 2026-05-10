using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Circuids.Pulse;

namespace Circuids.Pulse.WPF.ConformanceHost;

public partial class MainWindow : Window
{
    private readonly ITestExecutor _executor;
    private readonly Button _runButton = new();
    private readonly TextBlock _summaryText = new();
    private readonly DataGrid _resultsGrid = new();
    private readonly TextBox _jsonTextBox = new();

    public MainWindow(ITestExecutor executor, WpfHostProbe probe)
    {
        _executor = executor;
        InitializeComponent();
        probe.MainWindow = this;
        probe.Dispatcher = Dispatcher;
        probe.UiThreadId = Environment.CurrentManagedThreadId;
        ConfigureLayout();
    }

    private void ConfigureLayout()
    {
        var panel = new DockPanel { LastChildFill = true };

        _runButton.Content = "Run conformance";
        _runButton.Height = 40;
        _runButton.Click += RunButton_Click;
        DockPanel.SetDock(_runButton, Dock.Top);

        _summaryText.Height = 34;
        _summaryText.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(_summaryText, Dock.Top);

        _jsonTextBox.Height = 180;
        _jsonTextBox.IsReadOnly = true;
        _jsonTextBox.AcceptsReturn = true;
        _jsonTextBox.AcceptsTab = true;
        _jsonTextBox.TextWrapping = TextWrapping.NoWrap;
        _jsonTextBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _jsonTextBox.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        DockPanel.SetDock(_jsonTextBox, Dock.Bottom);

        _resultsGrid.IsReadOnly = true;
        _resultsGrid.AutoGenerateColumns = true;

        panel.Children.Add(_runButton);
        panel.Children.Add(_summaryText);
        panel.Children.Add(_jsonTextBox);
        panel.Children.Add(_resultsGrid);
        Content = panel;
    }

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        _runButton.IsEnabled = false;
        _runButton.Content = "Running...";

        try
        {
            var report = await _executor.RunAsync();
            _summaryText.Text = $"{(report.Success ? "PASS" : "FAIL")} - {report.Total} total, {report.Passed} passed, {report.Failed} failed, {report.Skipped} skipped";
            _jsonTextBox.Text = JsonSerializer.Serialize(report, PulseJsonContext.Default.TestRunReport);
            _resultsGrid.ItemsSource = report.Results.Select(ResultRow.From).ToList();
        }
        finally
        {
            _runButton.IsEnabled = true;
            _runButton.Content = "Re-run";
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