namespace Circuids.Pulse.UnitTests;

/// <summary>
/// Validates the load-bearing pattern: an abstract conformance base defines the
/// behavior; concrete subclasses opt in by overriding and re-attributing with [PulseCase]. This
/// is how Bridge will share specs across Blazor/MAUI/etc.
/// </summary>
public sealed class ConformancePatternTests
{
    private static async Task<TestRunReport> RunAsync(Action<PulseBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddPulse(configure);
        await using var sp = services.BuildServiceProvider();
        return await sp.GetRequiredService<ITestExecutor>().RunAsync();
    }

    [Fact]
    public async Task Override_with_PulseCase_runs_base_body()
    {
        DerivedSuite.Counter = 0;
        var report = await RunAsync(p => p.AddSuite<DerivedSuite>());

        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Passed, r.Outcome);
        Assert.Equal(1, DerivedSuite.Counter);
        // Suite name is the concrete type, not the abstract base.
        Assert.Equal(typeof(DerivedSuite).FullName, r.SuiteName);
    }

    [Fact]
    public async Task Attribute_inherited_from_base_when_inherit_true()
    {
        var report = await RunAsync(p => p.AddSuite<DerivedInheritsAttrSuite>());
        // Body is on base, attribute is on base, override does nothing — inheritance picks it up.
        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Passed, r.Outcome);
    }

    [Fact]
    public async Task Static_methods_are_ignored_even_with_PulseCase()
    {
        var report = await RunAsync(p => p.AddSuite<StaticMethodSuite>());
        // Static method tagged with [PulseCase] is intentionally NOT discovered (BindingFlags.Instance).
        // The instance method is the only one picked up.
        var r = Assert.Single(report.Results);
        Assert.Equal(nameof(StaticMethodSuite.Instance_one), r.TestName);
    }

    [Fact]
    public async Task Private_methods_are_ignored()
    {
        var report = await RunAsync(p => p.AddSuite<PrivateMethodSuite>());
        var r = Assert.Single(report.Results);
        Assert.Equal(nameof(PrivateMethodSuite.Public_one), r.TestName);
    }

    [Fact]
    public async Task Methods_without_attributes_are_ignored()
    {
        var report = await RunAsync(p => p.AddSuite<MixedAnnotationSuite>());
        // Only Tagged_one is reported; Untagged_one is invisible.
        var r = Assert.Single(report.Results);
        Assert.Equal(nameof(MixedAnnotationSuite.Tagged_one), r.TestName);
    }

    [Fact]
    public async Task Case_and_matrix_in_same_suite_both_run()
    {
        var report = await RunAsync(p => p.AddSuite<MixedShapesSuite>());

        // 1 case + 2 matrix rows = 3 results.
        Assert.Equal(3, report.Total);
        Assert.Contains(report.Results, r => r.TestName == nameof(MixedShapesSuite.Plain_case));
        Assert.Contains(report.Results, r => r.TestName.StartsWith("Matrix_row(", StringComparison.Ordinal));
    }

    public abstract class ConformanceBase
    {
        protected static int _counter;

        public virtual void Spec_test()
        {
            _counter++;
        }
    }

    public sealed class DerivedSuite : ConformanceBase
    {
        public static int Counter
        {
            get => _counter;
            set => _counter = value;
        }

        [PulseCase]
        public override void Spec_test() => base.Spec_test();
    }

    public abstract class AttributedConformanceBase
    {
        [PulseCase]
        public virtual void Inherited_spec() { }
    }

    public sealed class DerivedInheritsAttrSuite : AttributedConformanceBase
    {
        // Override exists but no attribute applied here; inherit:true on the lookup picks up base's.
        public override void Inherited_spec() => base.Inherited_spec();
    }

    public sealed class StaticMethodSuite
    {
        // Should NOT be discovered (BindingFlags.Instance excludes static).
        [PulseCase]
        public static void Static_one() => throw new InvalidOperationException("must not run");

        [PulseCase]
        public void Instance_one() { }
    }

    public sealed class PrivateMethodSuite
    {
        // Discovery uses BindingFlags.Public — non-public methods are skipped even if attributed.
        [PulseCase]
        private void Private_one() => throw new InvalidOperationException("must not run");

        [PulseCase]
        public void Public_one() { }
    }

    public sealed class MixedAnnotationSuite
    {
        [PulseCase] public void Tagged_one() { }
        public void Untagged_one() => throw new InvalidOperationException("must not run");
    }

    public sealed class MixedShapesSuite
    {
        [PulseCase]
        public void Plain_case() { }

        [PulseMatrix]
        [PulseRow(1)]
        [PulseRow(2)]
        public void Matrix_row(int n) => Assert.True(n > 0);
    }
}
