using System.Runtime.InteropServices;

namespace Circuids.Pulse.TestSupport.Runtime;

public abstract class RuntimeHostSpec
{
    protected abstract bool ExpectedIsBrowser { get; }

    protected virtual Architecture? ExpectedProcessArchitecture => null;

    protected virtual Architecture? DisallowedProcessArchitecture => null;

    protected abstract string ExpectedRuntimeIdentifierSubstring { get; }

    protected void Browser_flag_matches_host_expectation_core()
    {
        Equal(ExpectedIsBrowser, OperatingSystem.IsBrowser(), "OperatingSystem.IsBrowser() did not match the host expectation.");
    }

    protected void Process_architecture_matches_host_expectation_core()
    {
        var actual = RuntimeInformation.ProcessArchitecture;

        if (ExpectedProcessArchitecture is { } expected)
        {
            Equal(expected, actual, "Process architecture did not match the host expectation.");
        }

        if (DisallowedProcessArchitecture is { } disallowed && actual == disallowed)
        {
            throw new InvalidOperationException($"Process architecture must not be '{disallowed}'.");
        }
    }

    protected void Runtime_identifier_contains_expected_host_substring_core()
    {
        var runtimeIdentifier = RuntimeInformation.RuntimeIdentifier;

        if (!runtimeIdentifier.Contains(ExpectedRuntimeIdentifierSubstring, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Runtime identifier '{runtimeIdentifier}' did not contain '{ExpectedRuntimeIdentifierSubstring}'.");
        }
    }

    protected void Framework_description_starts_with_dotnet_core()
    {
        var frameworkDescription = RuntimeInformation.FrameworkDescription;

        if (!frameworkDescription.StartsWith(".NET", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Framework description '{frameworkDescription}' did not start with '.NET'.");
        }
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected '{expected}', got '{actual}'.");
        }
    }
}