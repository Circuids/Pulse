using System.Globalization;
using System.Text;

namespace Circuids.Pulse;

/// <summary>
/// Produces Pulse's canonical human-readable representation of a completed <see cref="TestRunReport"/>.
/// This is a pure transformation — not a pipeline, sink, or console abstraction. Consumers decide
/// where the output goes (<c>Console.WriteLine</c>, <c>ILogger</c>, a file, CI). The formatter has
/// zero knowledge of Console, Terminal, File System, Logging, or DI. No ANSI codes.
/// </summary>
public static class TestRunReportFormatter
{
    private const string Separator = "────────────────────────────────────────────────";

    /// <summary>
    /// Formats <paramref name="report"/> into Pulse's canonical plain-text representation.
    /// </summary>
    /// <param name="report">The completed test run report. Must not be <see langword="null"/>.</param>
    /// <returns>A deterministic, human-readable string suitable for console, CI logs, bug reports,
    /// and any other text-based consumer.</returns>
    public static string Format(TestRunReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb = new StringBuilder();

        AppendHeader(sb, report);
        AppendRuntime(sb, report.RuntimeEnvironment);
        AppendSummary(sb, report);
        AppendResults(sb, report);

        return sb.ToString();
    }

    private static void AppendHeader(StringBuilder sb, TestRunReport report)
    {
        sb.AppendLine(Separator);
        sb.AppendLine("Pulse Test Run Report");
        sb.AppendLine(Separator);
        sb.AppendLine();
        sb.Append("Status".PadRight(10));
        sb.Append(": ");
        sb.AppendLine(report.Success ? "Passed" : "Failed");
        sb.Append("Duration".PadRight(10));
        sb.Append(": ");
        sb.AppendLine(FormatDuration(report.Duration));
        sb.Append("Platform".PadRight(10));
        sb.Append(": ");
        sb.AppendLine(report.AssignedPlatform);
        sb.AppendLine();
    }

    private static void AppendRuntime(StringBuilder sb, RuntimeEnvironment rt)
    {
        sb.AppendLine("Runtime");
        AppendProperty(sb, "  .NET", rt.FrameworkDescription);
        AppendProperty(sb, "  OS", rt.OSDescription);
        AppendProperty(sb, "  Runtime", rt.RuntimeIdentifier);
        AppendProperty(sb, "  Processor", $"{rt.ProcessArchitecture} ({rt.ProcessorCount} cores)");
        AppendProperty(sb, "  Machine", rt.MachineName);
        sb.AppendLine();
    }

    private static void AppendSummary(StringBuilder sb, TestRunReport report)
    {
        sb.AppendLine("Summary");
        AppendProperty(sb, "  Passed", report.Passed.ToString(CultureInfo.InvariantCulture));
        AppendProperty(sb, "  Failed", report.Failed.ToString(CultureInfo.InvariantCulture));
        AppendProperty(sb, "  Skipped", report.Skipped.ToString(CultureInfo.InvariantCulture));
        AppendProperty(sb, "  Total", report.Total.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine();
    }

    private static void AppendResults(StringBuilder sb, TestRunReport report)
    {
        if (report.Results.Count == 0)
        {
            sb.AppendLine(Separator);
            sb.AppendLine("Results");
            sb.AppendLine(Separator);
            sb.AppendLine();
            sb.AppendLine("No tests were executed.");
            sb.AppendLine();
            return;
        }

        var suites = report.Results
            .GroupBy(r => r.SuiteName)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

        var passedSuites = suites
            .Where(g => !g.Any(r => r.Outcome == TestOutcome.Failed) &&
                        !g.All(r => r.Outcome == TestOutcome.Skipped))
            .ToList();
        var skippedSuites = suites
            .Where(g => g.All(r => r.Outcome == TestOutcome.Skipped))
            .ToList();
        var failedSuites = suites
            .Where(g => g.Any(r => r.Outcome == TestOutcome.Failed))
            .ToList();

        sb.AppendLine(Separator);
        sb.AppendLine("Results");
        sb.AppendLine(Separator);
        sb.AppendLine();

        AppendSuiteGroup(sb, passedSuites, "✓");
        AppendSuiteGroup(sb, skippedSuites, "○");
        AppendSuiteGroup(sb, failedSuites, "✗");
    }

    private static void AppendSuiteGroup(StringBuilder sb, List<IGrouping<string, TestResult>> suites, string icon)
    {
        foreach (var suite in suites)
        {
            var tests = suite.OrderBy(t => t.TestName, StringComparer.Ordinal).ToList();
            var suiteDuration = suite.Aggregate(TimeSpan.Zero, (acc, r) => acc + r.Duration);
            var testCount = tests.Count;

            if (suite.All(r => r.Outcome == TestOutcome.Skipped))
            {
                sb.Append(icon);
                sb.Append(' ');
                sb.Append(suite.Key);
                sb.AppendLine("  Skipped");
                sb.AppendLine();
                continue;
            }

            sb.Append(icon);
            sb.Append(' ');
            sb.Append(suite.Key);
            sb.Append(" (");
            sb.Append(testCount.ToString(CultureInfo.InvariantCulture));
            sb.Append(testCount == 1 ? " test, " : " tests, ");
            sb.Append(FormatDuration(suiteDuration));
            sb.AppendLine(")");

            foreach (var test in tests)
            {
                AppendTestResult(sb, test);
            }

            sb.AppendLine();
        }
    }

    private static void AppendTestResult(StringBuilder sb, TestResult test)
    {
        var icon = test.Outcome switch
        {
            TestOutcome.Passed => "✓",
            TestOutcome.Skipped => "○",
            TestOutcome.Failed => "✗",
            _ => "?"
        };

        if (test.Outcome == TestOutcome.Skipped)
        {
            sb.Append("  ");
            sb.Append(icon);
            sb.Append(' ');
            sb.Append(test.TestName);
            if (!string.IsNullOrWhiteSpace(test.Message))
            {
                sb.Append(" – ");
                sb.AppendLine(test.Message);
            }
            else
            {
                sb.AppendLine(" – Skipped");
            }

            return;
        }

        sb.Append("  ");
        sb.Append(icon);
        sb.Append(' ');
        sb.Append(test.TestName);
        sb.Append(" (");
        sb.Append(FormatDuration(test.Duration));
        sb.AppendLine(")");

        if (test.Outcome == TestOutcome.Failed)
        {
            if (!string.IsNullOrWhiteSpace(test.Message))
            {
                sb.Append("    Error: ");
                sb.AppendLine(test.Message);
            }

            if (!string.IsNullOrWhiteSpace(test.StackTrace))
            {
                sb.AppendLine("    Stack Trace:");
                AppendIndentedLines(sb, test.StackTrace, "      ");
            }
        }
    }

    private static void AppendProperty(StringBuilder sb, string label, string value)
    {
        sb.Append(label.PadRight(12));
        sb.Append(": ");
        sb.AppendLine(value);
    }

    private static void AppendIndentedLines(StringBuilder sb, string text, string indent)
    {
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                sb.Append(indent);
                sb.Append(text, start, i - start);
                sb.AppendLine();
                start = i + 1;
            }
            else if (text[i] == '\r')
            {
                var len = i - start;
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                sb.Append(indent);
                sb.Append(text, start, len);
                sb.AppendLine();
                start = i + 1;
            }
        }

        if (start < text.Length)
        {
            sb.Append(indent);
            sb.Append(text, start, text.Length - start);
            sb.AppendLine();
        }
    }

    internal static string FormatDuration(TimeSpan duration)
    {
        if (duration == TimeSpan.Zero)
        {
            return "-";
        }

        if (duration.TotalMilliseconds < 1)
        {
            return "< 1 ms";
        }

        if (duration.TotalSeconds < 1)
        {
            return $"{duration.TotalMilliseconds:F0} ms";
        }

        if (duration.TotalMinutes < 1)
        {
            return $"{duration.TotalSeconds:F2} s";
        }

        if (duration.TotalHours < 1)
        {
            var minutes = (int)duration.TotalMinutes;
            var seconds = duration.Seconds + (duration.Milliseconds / 1000.0);
            return $"{minutes}m {seconds:F2} s";
        }

        var hours = (int)duration.TotalHours;
        var remainingMinutes = duration.Minutes;
        var remainingSeconds = duration.Seconds + (duration.Milliseconds / 1000.0);
        return $"{hours}h {remainingMinutes}m {remainingSeconds:F2} s";
    }
}
