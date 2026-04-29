namespace Circuids.Pulse;

/// <summary>
/// Executes registered Pulse test suites inside the consumer host application.
/// </summary>
public interface ITestExecutor
{
    /// <summary>Runs every registered suite sequentially and returns the aggregate report.</summary>
    Task<TestRunReport> RunAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs only the suite whose suite name (the registered type's <see cref="Type.FullName"/>)
    /// matches <paramref name="suiteName"/> exactly.
    /// </summary>
    Task<TestRunReport> RunAsync(string suiteName, CancellationToken cancellationToken = default);
}
