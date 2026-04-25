// Pulse recognizes Xunit.SkipException by FullName (proposal §6 — friction-free xUnit migration).
// This stub has that exact FullName so the by-name detection can be validated without taking a
// hard dependency on a specific xUnit version that defines SkipException.
namespace Xunit
{
    public sealed class SkipException : System.Exception
    {
        public SkipException(string message) : base(message) { }
    }
}
