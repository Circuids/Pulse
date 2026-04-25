using System.Text.Json.Serialization;

namespace Circuids.Pulse;

/// <summary>
/// <see cref="System.Text.Json.Serialization.JsonSerializerContext"/> for the Pulse report shape.
/// This is the only stability contract Pulse exposes to consumer UIs and CI scrapers.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(TestRunReport))]
[JsonSerializable(typeof(TestResult))]
[JsonSerializable(typeof(RuntimeEnvironment))]
public partial class PulseJsonContext : JsonSerializerContext
{
}
