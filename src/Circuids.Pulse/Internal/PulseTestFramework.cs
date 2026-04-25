using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.TestHost;

namespace Circuids.Pulse.Internal;

/// <summary>
/// Pulse's <see cref="ITestFramework"/> implementation. Hosted inside the consumer host app via
/// <c>Microsoft.Testing.Platform.Builder.TestApplication.CreateBuilderAsync</c>; receives discovery
/// and execution requests from MTP and publishes <see cref="TestNodeUpdateMessage"/>s onto MTP's
/// message bus.
/// </summary>
/// <remarks>
/// Per the v1 design (proposal §4.3 / §4.4), this type is a real MTP <c>ITestFramework</c> — Pulse
/// reuses MTP's request/response pump, message bus, and cancellation plumbing rather than
/// reinventing them. Side-effect: every executed test is also captured into the shared
/// <see cref="PulseRunContext"/> so the consumer-facing <see cref="ITestExecutor"/> can return a
/// strongly-typed <see cref="TestRunReport"/>.
/// </remarks>
internal sealed class PulseTestFramework : ITestFramework, IDataProducer
{
    private readonly PulseRunContext _runContext;

    public PulseTestFramework(PulseRunContext runContext)
    {
        _runContext = runContext;
    }

    public string Uid => "Circuids.Pulse";
    public string Version => "0.1.0";
    public string DisplayName => "Circuids.Pulse";
    public string Description => "Circuids.Pulse runtime test runner.";

    public Type[] DataTypesProduced { get; } = new[] { typeof(TestNodeUpdateMessage) };

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) =>
        Task.FromResult(new CreateTestSessionResult { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) =>
        Task.FromResult(new CloseTestSessionResult { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        try
        {
            switch (context.Request)
            {
                case DiscoverTestExecutionRequest discover:
                    await DiscoverAllAsync(discover.Session.SessionUid, context).ConfigureAwait(false);
                    break;
                case RunTestExecutionRequest run:
                    await RunAllAsync(run.Session.SessionUid, context).ConfigureAwait(false);
                    break;
            }
        }
        finally
        {
            context.Complete();
        }
    }

    private async Task DiscoverAllAsync(SessionUid sessionUid, ExecuteRequestContext context)
    {
        foreach (var registration in _runContext.Builder.Suites)
        {
            if (context.CancellationToken.IsCancellationRequested) return;

            var suiteName = SuiteName(registration.SuiteType);
            if (!MatchesFilter(suiteName)) continue;

            IReadOnlyList<DiscoveredTest> tests;
            try
            {
                tests = ReflectionDiscovery.Discover(registration.SuiteType);
            }
            catch
            {
                // Discovery errors are surfaced during run; for pure discovery we just skip.
                continue;
            }

            foreach (var test in tests)
            {
                if (context.CancellationToken.IsCancellationRequested) return;

                var node = new TestNode
                {
                    Uid = new TestNodeUid($"{test.SuiteName}::{test.TestName}"),
                    DisplayName = test.TestName,
                    Properties = new PropertyBag(DiscoveredTestNodeStateProperty.CachedInstance),
                };

                await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(sessionUid, node))
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task RunAllAsync(SessionUid sessionUid, ExecuteRequestContext context)
    {
        foreach (var registration in _runContext.Builder.Suites)
        {
            if (context.CancellationToken.IsCancellationRequested) return;
            _runContext.OuterCancellation.ThrowIfCancellationRequested();

            var suiteName = SuiteName(registration.SuiteType);
            if (!MatchesFilter(suiteName)) continue;

            object suiteInstance;
            try
            {
                suiteInstance = registration.Factory is not null
                    ? registration.Factory(_runContext.HostServices)
                    : ActivatorUtilities.CreateInstance(_runContext.HostServices, registration.SuiteType);
            }
            catch (Exception ex)
            {
                await ReportFailureAsync(
                    context, sessionUid, suiteName,
                    testName: "(suite construction)",
                    message: $"Failed to construct suite: {ex.Message}",
                    stack: ex.StackTrace,
                    elapsed: TimeSpan.Zero,
                    exception: ex).ConfigureAwait(false);
                continue;
            }

            IReadOnlyList<DiscoveredTest> tests;
            try
            {
                tests = ReflectionDiscovery.Discover(registration.SuiteType);
            }
            catch (Exception ex)
            {
                await ReportFailureAsync(
                    context, sessionUid, suiteName,
                    testName: "(discovery)",
                    message: ex.Message,
                    stack: ex.StackTrace,
                    elapsed: TimeSpan.Zero,
                    exception: ex).ConfigureAwait(false);
                continue;
            }

            foreach (var test in tests)
            {
                if (context.CancellationToken.IsCancellationRequested) return;
                _runContext.OuterCancellation.ThrowIfCancellationRequested();

                await RunOneAsync(context, sessionUid, suiteInstance, test).ConfigureAwait(false);
            }
        }
    }

    private async Task RunOneAsync(
        ExecuteRequestContext context,
        SessionUid sessionUid,
        object suiteInstance,
        DiscoveredTest test)
    {
        var uid = new TestNodeUid($"{test.SuiteName}::{test.TestName}");

        if (test.SkipReason is not null)
        {
            await ReportSkippedAsync(context, sessionUid, uid, test, test.SkipReason, TimeSpan.Zero)
                .ConfigureAwait(false);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var returned = test.Method.Invoke(suiteInstance, test.Arguments);
            await AwaitIfNeeded(returned).ConfigureAwait(false);

            stopwatch.Stop();
            await ReportPassedAsync(context, sessionUid, uid, test, stopwatch.Elapsed)
                .ConfigureAwait(false);
        }
        catch (Exception raw)
        {
            stopwatch.Stop();
            var ex = raw is TargetInvocationException tie && tie.InnerException is not null
                ? tie.InnerException
                : raw;

            if (IsSkipException(ex))
            {
                await ReportSkippedAsync(context, sessionUid, uid, test, ex.Message, stopwatch.Elapsed)
                    .ConfigureAwait(false);
                return;
            }

            await ReportFailureAsync(
                context, sessionUid,
                suiteName: test.SuiteName,
                testName: test.TestName,
                message: ex.Message,
                stack: ex.StackTrace,
                elapsed: stopwatch.Elapsed,
                exception: ex).ConfigureAwait(false);
        }
    }

    private async Task ReportPassedAsync(
        ExecuteRequestContext context,
        SessionUid sessionUid,
        TestNodeUid uid,
        DiscoveredTest test,
        TimeSpan elapsed)
    {
        var node = new TestNode
        {
            Uid = uid,
            DisplayName = test.TestName,
            Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance),
        };
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(sessionUid, node))
            .ConfigureAwait(false);

        _runContext.Add(new TestResult
        {
            SuiteName = test.SuiteName,
            TestName = test.TestName,
            Outcome = TestOutcome.Passed,
            Duration = elapsed,
        });
    }

    private async Task ReportSkippedAsync(
        ExecuteRequestContext context,
        SessionUid sessionUid,
        TestNodeUid uid,
        DiscoveredTest test,
        string reason,
        TimeSpan elapsed)
    {
        var node = new TestNode
        {
            Uid = uid,
            DisplayName = test.TestName,
            Properties = new PropertyBag(new SkippedTestNodeStateProperty(reason)),
        };
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(sessionUid, node))
            .ConfigureAwait(false);

        _runContext.Add(new TestResult
        {
            SuiteName = test.SuiteName,
            TestName = test.TestName,
            Outcome = TestOutcome.Skipped,
            Message = reason,
            Duration = elapsed,
        });
    }

    private async Task ReportFailureAsync(
        ExecuteRequestContext context,
        SessionUid sessionUid,
        string suiteName,
        string testName,
        string message,
        string? stack,
        TimeSpan elapsed,
        Exception? exception)
    {
        var stateProp = exception is not null
            ? new FailedTestNodeStateProperty(exception, message)
            : new FailedTestNodeStateProperty(message);

        var node = new TestNode
        {
            Uid = new TestNodeUid($"{suiteName}::{testName}"),
            DisplayName = testName,
            Properties = new PropertyBag(stateProp),
        };

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(sessionUid, node))
            .ConfigureAwait(false);

        _runContext.Add(new TestResult
        {
            SuiteName = suiteName,
            TestName = testName,
            Outcome = TestOutcome.Failed,
            Message = message,
            StackTrace = stack,
            Duration = elapsed,
        });
    }

    private bool MatchesFilter(string suiteName) =>
        _runContext.SuiteFilter is null
        || string.Equals(suiteName, _runContext.SuiteFilter, StringComparison.Ordinal);

    private static string SuiteName(Type t) => t.FullName ?? t.Name;

    private static Task AwaitIfNeeded(object? returned) => returned switch
    {
        null => Task.CompletedTask,
        Task t => t,
        ValueTask vt => vt.AsTask(),
        _ => Task.CompletedTask,
    };

    private static bool IsSkipException(Exception ex)
    {
        if (ex is PulseSkipException) return true;
        var typeName = ex.GetType().FullName;
        return typeName is "Xunit.SkipException" or "Xunit.Sdk.SkipException";
    }
}
