namespace Circuids.Pulse;

/// <summary>
/// Optional per-suite lifetime hooks. A suite type implementing this interface receives one
/// <see cref="InitializeAsync"/> call before its first test and one <see cref="DisposeAsync"/>
/// call after its last test, regardless of pass / fail / skip outcomes.
/// </summary>
/// <remarks>
/// Suite instances that implement <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/>
/// are also disposed after the suite finishes; <see cref="DisposeAsync"/> on this interface
/// runs first.
/// </remarks>
public interface IPulseLifetime
{
    /// <summary>Runs once before the suite's first test executes.</summary>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>Runs once after the suite's last test completes.</summary>
    Task DisposeAsync(CancellationToken cancellationToken);
}
