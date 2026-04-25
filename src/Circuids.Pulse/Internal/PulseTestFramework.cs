using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.TestHost;

namespace Circuids.Pulse.Internal;

/// <summary>
/// MTP <see cref="ITestFramework"/> adapter. Hosted in-process per <see cref="ITestExecutor.RunAsync(System.Threading.CancellationToken)"/>
/// invocation; drives discovery and execution and dual-writes results to MTP's message bus and
/// the shared <see cref="PulseRunContext"/> so the consumer receives a strongly-typed
/// <see cref="TestRunReport"/>.
/// </summary>
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

    public Type[] DataTypesProduced { get; } = [typeof(TestNodeUpdateMessage)];

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

            try
            {
                await InvokeLifetimeAsync(suiteInstance, initialize: true, context, sessionUid, suiteName)
                    .ConfigureAwait(false);

                foreach (var test in tests)
                {
                    if (context.CancellationToken.IsCancellationRequested) return;
                    _runContext.OuterCancellation.ThrowIfCancellationRequested();

                    await RunOneAsync(context, sessionUid, suiteInstance, test).ConfigureAwait(false);
                }
            }
            finally
            {
                await InvokeLifetimeAsync(suiteInstance, initialize: false, context, sessionUid, suiteName)
                    .ConfigureAwait(false);
                await DisposeSuiteAsync(suiteInstance, context, sessionUid, suiteName)
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task InvokeLifetimeAsync(
        object suiteInstance,
        bool initialize,
        ExecuteRequestContext context,
        SessionUid sessionUid,
        string suiteName)
    {
        if (suiteInstance is not IPulseLifetime lifetime) return;

        var ct = _runContext.OuterCancellation;
        var label = initialize ? "(suite InitializeAsync)" : "(suite DisposeAsync)";
        try
        {
            if (initialize) await lifetime.InitializeAsync(ct).ConfigureAwait(false);
            else await lifetime.DisposeAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await ReportFailureAsync(
                context, sessionUid, suiteName,
                testName: label,
                message: ex.Message,
                stack: ex.StackTrace,
                elapsed: TimeSpan.Zero,
                exception: ex).ConfigureAwait(false);
        }
    }

    private async Task DisposeSuiteAsync(
        object suiteInstance,
        ExecuteRequestContext context,
        SessionUid sessionUid,
        string suiteName)
    {
        try
        {
            switch (suiteInstance)
            {
                case IAsyncDisposable ad:
                    await ad.DisposeAsync().ConfigureAwait(false);
                    break;
                case IDisposable d:
                    d.Dispose();
                    break;
            }
        }
        catch (Exception ex)
        {
            await ReportFailureAsync(
                context, sessionUid, suiteName,
                testName: "(suite Dispose)",
                message: ex.Message,
                stack: ex.StackTrace,
                elapsed: TimeSpan.Zero,
                exception: ex).ConfigureAwait(false);
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

        var timeout = ResolveTimeout(test);
        using var perTestCts = CancellationTokenSource.CreateLinkedTokenSource(_runContext.OuterCancellation);
        if (timeout is { } t) perTestCts.CancelAfter(t);

        var arguments = test.AcceptsCancellationToken
            ? Append(test.Arguments, perTestCts.Token)
            : test.Arguments;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var returned = test.Method.Invoke(suiteInstance, arguments);
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

            // Per-test timeout fired (linked CTS), but the outer run isn't cancelled → timeout failure.
            if (ex is OperationCanceledException
                && perTestCts.IsCancellationRequested
                && !_runContext.OuterCancellation.IsCancellationRequested
                && timeout is { } tm)
            {
                await ReportFailureAsync(
                    context, sessionUid,
                    suiteName: test.SuiteName,
                    testName: test.TestName,
                    message: $"Test exceeded timeout of {tm.TotalMilliseconds:F0}ms",
                    stack: ex.StackTrace,
                    elapsed: stopwatch.Elapsed,
                    exception: ex).ConfigureAwait(false);
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

    private TimeSpan? ResolveTimeout(DiscoveredTest test)
    {
        if (test.TimeoutMs > 0) return TimeSpan.FromMilliseconds(test.TimeoutMs);
        return _runContext.Builder.DefaultTestTimeout;
    }

    private static object?[] Append(object?[] source, object? value)
    {
        var copy = new object?[source.Length + 1];
        Array.Copy(source, copy, source.Length);
        copy[^1] = value;
        return copy;
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
