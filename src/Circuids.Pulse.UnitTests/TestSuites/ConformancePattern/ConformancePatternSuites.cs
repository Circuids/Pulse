namespace Circuids.Pulse.UnitTests.TestSuites.ConformancePattern;

internal abstract class ConformanceBase
{
    protected static int CounterValue;

    public virtual void Spec_test()
    {
        CounterValue++;
    }
}

internal sealed class DerivedSuite : ConformanceBase
{
    public static int Counter
    {
        get => CounterValue;
        set => CounterValue = value;
    }

    [PulseCase]
    public override void Spec_test() => base.Spec_test();
}

internal abstract class AttributedConformanceBase
{
    [PulseCase]
    public virtual void Inherited_spec() { }
}

internal sealed class DerivedInheritsAttrSuite : AttributedConformanceBase
{
    public override void Inherited_spec() => base.Inherited_spec();
}

internal sealed class StaticMethodSuite
{
    [PulseCase]
    public static void Static_one() => throw new InvalidOperationException("must not run");

    [PulseCase]
    public void Instance_one() { }
}

internal sealed class PrivateMethodSuite
{
    [PulseCase]
    private void Private_one() => throw new InvalidOperationException("must not run");

    [PulseCase]
    public void Public_one() { }
}

internal sealed class MixedAnnotationSuite
{
    [PulseCase] public void Tagged_one() { }

    public void Untagged_one() => throw new InvalidOperationException("must not run");
}

internal sealed class MixedShapesSuite
{
    [PulseCase]
    public void Plain_case() { }

    [PulseMatrix]
    [PulseRow(1)]
    [PulseRow(2)]
    public void Matrix_row(int n) => Assert.True(n > 0);
}
