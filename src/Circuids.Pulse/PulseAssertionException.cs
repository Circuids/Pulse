namespace Circuids.Pulse;

/// <summary>
/// Thrown by <see cref="PulseAssert"/> when an assertion fails. The Pulse executor maps any
/// thrown exception inside a test body to <see cref="TestOutcome.Failed"/>, surfacing
/// <see cref="Exception.Message"/> and <see cref="Exception.StackTrace"/> on the resulting
/// <see cref="TestResult"/>.
/// </summary>
public sealed class PulseAssertionException : Exception
{
    /// <summary>Initializes a new <see cref="PulseAssertionException"/> with the specified message.</summary>
    public PulseAssertionException(string message) : base(message) { }
}
