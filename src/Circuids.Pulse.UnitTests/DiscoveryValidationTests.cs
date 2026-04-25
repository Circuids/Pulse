namespace Circuids.Pulse.UnitTests;

public sealed class DiscoveryValidationTests
{
    private static Task<TestRunReport> RunAsync<TSuite>() where TSuite : class
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddPulse(p => p.AddSuite<TSuite>());
        var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<ITestExecutor>().RunAsync();
    }

    [Fact]
    public async Task Method_with_both_PulseCase_and_PulseMatrix_is_rejected()
    {
        var report = await RunAsync<BothAttributesSuite>();
        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Failed, r.Outcome);
        Assert.Equal("(discovery)", r.TestName);
        Assert.Contains("mutually exclusive", r.Message);
    }

    [Fact]
    public async Task PulseCase_with_parameters_is_rejected()
    {
        var report = await RunAsync<CaseWithParameters>();
        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Failed, r.Outcome);
        Assert.Contains("zero parameters", r.Message);
    }

    [Fact]
    public async Task PulseMatrix_without_rows_is_rejected()
    {
        var report = await RunAsync<MatrixWithoutRows>();
        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Failed, r.Outcome);
        Assert.Contains("no [PulseRow]", r.Message);
    }

    [Fact]
    public async Task PulseRow_with_wrong_argument_count_is_rejected()
    {
        var report = await RunAsync<RowArgCountMismatch>();
        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Failed, r.Outcome);
        Assert.Contains("argument(s)", r.Message);
    }

    [Fact]
    public async Task Method_returning_int_is_rejected()
    {
        var report = await RunAsync<NonVoidReturn>();
        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Failed, r.Outcome);
        Assert.Contains("must return void, Task, or ValueTask", r.Message);
    }

    public sealed class BothAttributesSuite
    {
        [PulseCase, PulseMatrix]
        [PulseRow(1)]
        public void Both(int x) { }
    }

    public sealed class CaseWithParameters
    {
        [PulseCase] public void Has_args(int x) { }
    }

    public sealed class MatrixWithoutRows
    {
        [PulseMatrix] public void No_rows(int x) { }
    }

    public sealed class RowArgCountMismatch
    {
        [PulseMatrix]
        [PulseRow(1, 2, 3)]
        public void One_arg_method(int x) { }
    }

    public sealed class NonVoidReturn
    {
        [PulseCase] public int Returns_int() => 42;
    }
}
