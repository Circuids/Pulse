using System.Globalization;
using System.Text;

namespace Circuids.Pulse.UnitTests;

public sealed class TestRunReportFormatterTests
{
    private static TestRunReport CreateReport(
        string assignedPlatform = "TestPlatform",
        params TestResult[] results) =>
        new()
        {
            AssignedPlatform = assignedPlatform,
            RuntimeEnvironment = new RuntimeEnvironment
            {
                FrameworkDescription = ".NET 10.0.2",
                RuntimeIdentifier = "win-x64",
                OSDescription = "Microsoft Windows 10.0.26100",
                OSArchitecture = "X64",
                ProcessArchitecture = "X64",
                IsBrowser = false,
                IsWasm = false,
                MachineName = "TESTMACHINE",
                ProcessorCount = 16,
            },
            Timestamp = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero),
            Results = results,
            Duration = results.Length > 0
                ? results.Aggregate(TimeSpan.Zero, (acc, r) => acc + r.Duration)
                : TimeSpan.Zero,
        };

    private static TestResult PassedResult(string suite, string test, int durationMs = 10) =>
        new()
        {
            SuiteName = suite,
            TestName = test,
            Outcome = TestOutcome.Passed,
            Message = null,
            StackTrace = null,
            Duration = TimeSpan.FromMilliseconds(durationMs),
        };

    private static TestResult FailedResult(string suite, string test, string message, string? stackTrace = null, int durationMs = 45) =>
        new()
        {
            SuiteName = suite,
            TestName = test,
            Outcome = TestOutcome.Failed,
            Message = message,
            StackTrace = stackTrace ?? $"   at {suite}.{test}() in /src/{suite}.cs:line 12",
            Duration = TimeSpan.FromMilliseconds(durationMs),
        };

    private static TestResult SkippedResult(string suite, string test, string? reason = null) =>
        new()
        {
            SuiteName = suite,
            TestName = test,
            Outcome = TestOutcome.Skipped,
            Message = reason,
            StackTrace = null,
            Duration = TimeSpan.Zero,
        };

    [Fact]
    public void Format_SingleSuite_AllPassed_ProducesConciseOutput()
    {
        var report = CreateReport("Browser",
            PassedResult("AuthSpec", "Login_Succeeds", 12),
            PassedResult("AuthSpec", "Logout_ClearsSession", 8));

        var output = TestRunReportFormatter.Format(report);

        Assert.Contains("Status".PadRight(10) + ": Passed", output);
        Assert.Contains("✓ AuthSpec (2 tests, 20 ms)", output);
        Assert.Contains("  ✓ Login_Succeeds (12 ms)", output);
        Assert.Contains("  ✓ Logout_ClearsSession (8 ms)", output);
        Assert.DoesNotContain("Error:", output);
        Assert.DoesNotContain("Stack Trace:", output);
    }

    [Fact]
    public void Format_MultipleSuites_AllPassed_OrdersAlphabetically()
    {
        var report = CreateReport("Mobile",
            PassedResult("ZebraSpec", "ZTest", 5),
            PassedResult("AlphaSpec", "ATest", 3));

        var output = TestRunReportFormatter.Format(report);

        var alphaPos = output.IndexOf("✓ AlphaSpec", StringComparison.Ordinal);
        var zebraPos = output.IndexOf("✓ ZebraSpec", StringComparison.Ordinal);
        Assert.True(alphaPos < zebraPos, "Suites should be ordered alphabetically");
    }

    [Fact]
    public void Format_AllPassed_SummaryReflectsCounts()
    {
        var report = CreateReport("Server",
            PassedResult("Suite1", "TestA"),
            PassedResult("Suite1", "TestB"),
            PassedResult("Suite2", "TestC"));

        var output = TestRunReportFormatter.Format(report);

        Assert.Contains("  Passed".PadRight(12) + ": 3", output);
        Assert.Contains("  Failed".PadRight(12) + ": 0", output);
        Assert.Contains("  Skipped".PadRight(12) + ": 0", output);
        Assert.Contains("  Total".PadRight(12) + ": 3", output);
    }

    [Fact]
    public void Format_MixedOutcomes_GroupsPassedBeforeSkippedBeforeFailed()
    {
        var report = CreateReport("Test",
            SkippedResult("SkippedSuite", "SkipMe"),
            PassedResult("PassingSuite", "RegularTest"),
            FailedResult("FailingSuite", "BrokenTest", "Boom"));

        var output = TestRunReportFormatter.Format(report);

        var passedPos = output.IndexOf("✓ PassingSuite", StringComparison.Ordinal);
        var skippedPos = output.IndexOf("○ SkippedSuite", StringComparison.Ordinal);
        var failedPos = output.IndexOf("✗ FailingSuite", StringComparison.Ordinal);

        Assert.True(passedPos < skippedPos, "Passed suites before skipped");
        Assert.True(skippedPos < failedPos, "Skipped suites before failed");
    }

    [Fact]
    public void Format_SuiteWithMixedPassAndFail_ShowsAsFailedWithTestDetails()
    {
        var report = CreateReport("Test",
            PassedResult("MixedSuite", "GoodTest", 10),
            FailedResult("MixedSuite", "BadTest", "Assertion failed"));

        var output = TestRunReportFormatter.Format(report);

        Assert.Contains("✗ MixedSuite (2 tests,", output);
        Assert.Contains("  ✓ GoodTest (10 ms)", output);
        Assert.Contains("  ✗ BadTest (45 ms)", output);
    }

    [Fact]
    public void Format_AllSkippedSuite_ShowsConciseSkippedLine()
    {
        var report = CreateReport("Test",
            SkippedResult("DeadSuite", "OldTest", "Not yet implemented"));

        var output = TestRunReportFormatter.Format(report);

        Assert.Contains("○ DeadSuite  Skipped", output);
        Assert.DoesNotContain("OldTest", output);
    }

    [Fact]
    public void Format_IndividualSkippedTest_ShowsSkipReason()
    {
        var report = CreateReport("Test",
            PassedResult("SuiteX", "ActiveTest", 5),
            SkippedResult("SuiteX", "PendingTest", "Not ready"));

        var output = TestRunReportFormatter.Format(report);

        Assert.Contains("  ○ PendingTest – Not ready", output);
    }

    [Fact]
    public void Format_SkippedTestWithoutReason_ShowsDefaultSkipLabel()
    {
        var report = CreateReport("Test",
            SkippedResult("SuiteX", "NoReasonTest"));

        var output = TestRunReportFormatter.Format(report);

        Assert.Contains("○ SuiteX  Skipped", output);
    }

    [Fact]
    public void Format_FailedTest_IncludesMessageAndStackTrace()
    {
        var report = CreateReport("Test",
            FailedResult("BrokenSuite", "ThrowsUp",
                "PulseAssert.Equal failed: Values differ\n  Expected: 42\n  Actual: 0",
                "   at BrokenSuite.ThrowsUp() in /src/BrokenSuite.cs:line 42\n   at Pulse.TestExecutor.InvokeTest()"));

        var output = TestRunReportFormatter.Format(report);

        Assert.Contains("    Error: PulseAssert.Equal failed: Values differ", output);
        Assert.Contains("  Expected: 42", output);
        Assert.Contains("  Actual: 0", output);
        Assert.Contains("    Stack Trace:", output);
        Assert.Contains("      at BrokenSuite.ThrowsUp()", output);
        Assert.Contains("      at Pulse.TestExecutor.InvokeTest()", output);
    }

    [Fact]
    public void Format_FailedTest_WithoutStackTrace_OmitsStackTraceSection()
    {
        var report = CreateReport("Test",
            FailedResult("BrokenSuite", "ThrowsUp", "Something went wrong", stackTrace: string.Empty));

        var output = TestRunReportFormatter.Format(report);

        Assert.Contains("    Error: Something went wrong", output);
        Assert.DoesNotContain("Stack Trace:", output);
    }

    [Fact]
    public void Format_FailedTest_WithoutMessage_OmitsErrorLine()
    {
        var report = CreateReport("Test",
            FailedResult("BrokenSuite", "MysteryFail", message: null!));

        var output = TestRunReportFormatter.Format(report);

        Assert.DoesNotContain("Error:", output);
        Assert.Contains("✗ BrokenSuite", output);
    }

    [Fact]
    public void Format_EmptyReport_ProducesValidOutput()
    {
        var report = CreateReport("Empty");

        var output = TestRunReportFormatter.Format(report);

        Assert.Contains("Status".PadRight(10) + ": Passed", output);
        Assert.Contains("No tests were executed.", output);
        Assert.Contains("  Total".PadRight(12) + ": 0", output);
    }

    [Fact]
    public void Format_EmptyReport_DoesNotThrow()
    {
        var report = CreateReport("Empty");
        var exception = Record.Exception(() => TestRunReportFormatter.Format(report));
        Assert.Null(exception);
    }

    [Fact]
    public void Format_NullReport_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TestRunReportFormatter.Format(null!));
    }

    [Fact]
    public void Format_IncludesRuntimeEnvironmentDetails()
    {
        var report = CreateReport("Browser");

        var output = TestRunReportFormatter.Format(report);

        Assert.Contains(".NET", output);
        Assert.Contains(".NET 10.0.2", output);
        Assert.Contains("win-x64", output);
        Assert.Contains("TESTMACHINE", output);
        Assert.Contains("X64 (16 cores)", output);
    }

    [Fact]
    public void Format_SameReport_ProducesIdenticalOutput()
    {
        var report = CreateReport("Test",
            PassedResult("SuiteA", "Test1", 5),
            FailedResult("SuiteB", "Test2", "Fail", "trace"),
            SkippedResult("SuiteC", "Test3", "Skip"));

        var output1 = TestRunReportFormatter.Format(report);
        var output2 = TestRunReportFormatter.Format(report);

        Assert.Equal(output1, output2);
    }

    [Fact]
    public void Format_LargeReport_RemainsReadable()
    {
        var results = new List<TestResult>();
        for (var s = 0; s < 50; s++)
        {
            var suiteName = $"Suite{s:D3}";
            for (var t = 0; t < 10; t++)
            {
                results.Add(PassedResult(suiteName, $"Test{t:D3}", 1));
            }
        }

        var report = CreateReport("Large", results.ToArray());
        var output = TestRunReportFormatter.Format(report);

        Assert.Contains("  Total".PadRight(12) + ": 500", output);
        Assert.Contains("  Passed".PadRight(12) + ": 500", output);
        Assert.Contains("✓ Suite049 (10 tests, 10 ms)", output);
        Assert.Contains("✓ Suite000 (10 tests, 10 ms)", output);
    }

    [Fact]
    public void Format_LargeReportWithFailures_ExpandsFailures()
    {
        var results = new List<TestResult>();
        for (var s = 0; s < 20; s++)
        {
            var suiteName = $"Suite{s:D2}";
            results.Add(PassedResult(suiteName, "PassingTest", 1));
            results.Add(PassedResult(suiteName, "AnotherPass", 1));
        }

        results.Add(FailedResult("FailingSuite", "CriticalBreak", "Kaboom!", "   at FailingSuite.CriticalBreak()"));

        var report = CreateReport("Mixed", results.ToArray());
        var output = TestRunReportFormatter.Format(report);

        Assert.Contains("✗ FailingSuite", output);
        Assert.Contains("Kaboom!", output);
        Assert.Contains("  Total".PadRight(12) + ": 41", output);
    }

    [Fact]
    public void Format_DurationZero_ShowsDashForSkippedTest()
    {
        var report = CreateReport("Test",
            SkippedResult("Suite", "SkippedTest"));
        var output = TestRunReportFormatter.Format(report);
        Assert.Contains("○ Suite  Skipped", output);
    }

    [Fact]
    public void Format_DurationMilliseconds_ShowsMs()
    {
        var report = CreateReport("Test",
            PassedResult("Suite", "Test", 450));
        var output = TestRunReportFormatter.Format(report);
        Assert.Contains("(1 test, 450 ms)", output);
        Assert.Contains("(450 ms)", output);
    }

    [Fact]
    public void Format_DurationSeconds_ShowsSecondsWithTwoDecimals()
    {
        var report = CreateReport("Test",
            PassedResult("Suite", "Test", 2140));
        var output = TestRunReportFormatter.Format(report);
        Assert.Contains("(2.14 s)", output);
    }

    [Fact]
    public void Format_Duration_MinutesAndSeconds()
    {
        var report = CreateReport("Test",
            PassedResult("Suite", "Test", (int)TimeSpan.FromSeconds(125.5).TotalMilliseconds));
        var output = TestRunReportFormatter.Format(report);
        Assert.Contains("2m 5.50 s", output);
    }

    [Fact]
    public void Format_Duration_HoursMinutesSeconds()
    {
        var report = CreateReport("Test",
            PassedResult("Suite", "Test", (int)TimeSpan.FromSeconds(3661).TotalMilliseconds));
        var output = TestRunReportFormatter.Format(report);
        Assert.Contains("1h 1m 1.00 s", output);
    }

    [Fact]
    public void Format_SuiteWithLongName_DoesNotTruncate()
    {
        var longName = "ThisIsAVeryLongSuiteNameThatShouldNotBeTruncated_" + new string('x', 80);
        var report = CreateReport("Test",
            PassedResult(longName, "Test1", 5));

        var output = TestRunReportFormatter.Format(report);

        Assert.Contains(longName, output);
    }

    [Fact]
    public void Format_StackTraceWithCrLf_FormatsCorrectly()
    {
        var report = CreateReport("Test",
            FailedResult("Suite", "Test", "Error",
                "   at Suite.Test() in /src/Suite.cs:line 10\r\n   at Runner.Invoke() in /src/Runner.cs:line 20"));

        var output = TestRunReportFormatter.Format(report);

        Assert.Contains("      at Suite.Test()", output);
        Assert.Contains("      at Runner.Invoke()", output);
    }

    [Fact]
    public void Format_FailedTest_DoesNotLeakSuccessIconForFailures()
    {
        var report = CreateReport("Test",
            FailedResult("BrokenSuite", "Test1", "Failed"),
            PassedResult("PassingSuite", "Test2", 5));

        var output = TestRunReportFormatter.Format(report);

        Assert.Contains("✗ BrokenSuite", output);
        Assert.Contains("✓ PassingSuite", output);
    }

    [Fact]
    public void Format_SuiteWithOnlySkippedAndPassed_ShowsIndividualTests()
    {
        var report = CreateReport("Test",
            PassedResult("MixedSuite", "ActiveTest", 10),
            SkippedResult("MixedSuite", "PendingTest", "WIP"));

        var output = TestRunReportFormatter.Format(report);

        Assert.Contains("✓ MixedSuite (2 tests, 10 ms)", output);
        Assert.Contains("  ✓ ActiveTest (10 ms)", output);
        Assert.Contains("  ○ PendingTest – WIP", output);
    }
}
