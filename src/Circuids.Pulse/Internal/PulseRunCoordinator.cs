using System.Threading;

namespace Circuids.Pulse.Internal;

/// <summary>Coordinates the single active Pulse run allowed for one host service provider.</summary>
internal sealed class PulseRunCoordinator
{
    private const string AlreadyRunningMessage =
        "Pulse is already running for this application.\n\n" +
        "Pulse does not support concurrent runs because it executes inside a shared application runtime.\n\n" +
        "Wait for the current run to complete before starting another.";

    private int _isRunning;

    public RunLease Enter()
    {
        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
        {
            throw new InvalidOperationException(AlreadyRunningMessage);
        }

        return new RunLease(this);
    }

    private void Exit() => Volatile.Write(ref _isRunning, 0);

    internal readonly struct RunLease : IDisposable
    {
        private readonly PulseRunCoordinator? _coordinator;

        public RunLease(PulseRunCoordinator coordinator)
        {
            _coordinator = coordinator;
        }

        public void Dispose() => _coordinator?.Exit();
    }
}
