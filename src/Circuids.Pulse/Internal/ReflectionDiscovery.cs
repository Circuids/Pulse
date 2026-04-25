using System.Reflection;

namespace Circuids.Pulse.Internal;

/// <summary>One discovered, ready-to-run test node (a single case or a single matrix row).</summary>
internal sealed class DiscoveredTest
{
    public DiscoveredTest(
        string suiteName,
        string testName,
        MethodInfo method,
        object?[] arguments,
        string? skipReason,
        bool acceptsCancellationToken,
        int timeoutMs)
    {
        SuiteName = suiteName;
        TestName = testName;
        Method = method;
        Arguments = arguments;
        SkipReason = skipReason;
        AcceptsCancellationToken = acceptsCancellationToken;
        TimeoutMs = timeoutMs;
    }

    public string SuiteName { get; }
    public string TestName { get; }
    public MethodInfo Method { get; }
    public object?[] Arguments { get; }
    public string? SkipReason { get; }

    /// <summary>
    /// True when the method's last parameter is <see cref="CancellationToken"/>; the framework
    /// appends the per-test linked token at invocation time.
    /// </summary>
    public bool AcceptsCancellationToken { get; }

    /// <summary>
    /// Per-test timeout in milliseconds (<c>0</c> = inherit <see cref="PulseBuilder.DefaultTestTimeout"/>).
    /// </summary>
    public int TimeoutMs { get; }
}

/// <summary>
/// Reflects over a registered suite type and produces an ordered list of <see cref="DiscoveredTest"/>.
/// Discovery is fully synchronous and scoped to the suite type's declared methods (including those
/// inherited from abstract conformance bases). No assembly scanning.
/// </summary>
internal static class ReflectionDiscovery
{
    private const BindingFlags MethodFlags =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

    public static IReadOnlyList<DiscoveredTest> Discover(Type suiteType)
    {
        if (suiteType is null) throw new ArgumentNullException(nameof(suiteType));

        var suiteName = suiteType.FullName ?? suiteType.Name;
        var results = new List<DiscoveredTest>();

        foreach (var method in suiteType.GetMethods(MethodFlags))
        {
            if (method.IsSpecialName) continue;
            if (method.DeclaringType == typeof(object)) continue;

            var caseAttr = method.GetCustomAttribute<PulseCaseAttribute>(inherit: true);
            var matrixAttr = method.GetCustomAttribute<PulseMatrixAttribute>(inherit: true);

            if (caseAttr is null && matrixAttr is null) continue;

            if (caseAttr is not null && matrixAttr is not null)
            {
                throw new InvalidOperationException(
                    $"[Pulse] Method '{suiteName}.{method.Name}' is tagged with both [PulseCase] and [PulseMatrix]. " +
                    "These attributes are mutually exclusive.");
            }

            ValidateReturnType(suiteName, method);

            var parameters = method.GetParameters();
            var acceptsCt = parameters.Length > 0
                && parameters[^1].ParameterType == typeof(CancellationToken);
            var declaredArity = acceptsCt ? parameters.Length - 1 : parameters.Length;

            if (caseAttr is not null)
            {
                if (declaredArity != 0)
                {
                    throw new InvalidOperationException(
                        $"[Pulse] [PulseCase] method '{suiteName}.{method.Name}' must take zero parameters " +
                        "(an optional trailing CancellationToken is allowed). " +
                        "Use [PulseMatrix] + [PulseRow(...)] for parameterized tests.");
                }

                results.Add(new DiscoveredTest(
                    suiteName: suiteName,
                    testName: caseAttr.DisplayName ?? method.Name,
                    method: method,
                    arguments: Array.Empty<object?>(),
                    skipReason: caseAttr.Skip,
                    acceptsCancellationToken: acceptsCt,
                    timeoutMs: caseAttr.TimeoutMs));
                continue;
            }

            var rowAttrs = method.GetCustomAttributes<PulseRowAttribute>(inherit: true).ToArray();
            if (rowAttrs.Length == 0)
            {
                throw new InvalidOperationException(
                    $"[Pulse] [PulseMatrix] method '{suiteName}.{method.Name}' has no [PulseRow] entries. " +
                    "Add at least one [PulseRow(...)] attribute or convert the method to [PulseCase].");
            }

            var displayRoot = matrixAttr!.DisplayName ?? method.Name;

            foreach (var row in rowAttrs)
            {
                if (row.Arguments.Length != declaredArity)
                {
                    throw new InvalidOperationException(
                        $"[Pulse] [PulseRow] on '{suiteName}.{method.Name}' supplies {row.Arguments.Length} " +
                        $"argument(s) but the method expects {declaredArity}.");
                }

                var rowName = $"{displayRoot}({FormatArguments(row.Arguments)})";
                var skip = matrixAttr.Skip ?? row.Skip;

                results.Add(new DiscoveredTest(
                    suiteName: suiteName,
                    testName: rowName,
                    method: method,
                    arguments: row.Arguments,
                    skipReason: skip,
                    acceptsCancellationToken: acceptsCt,
                    timeoutMs: matrixAttr.TimeoutMs));
            }
        }

        return results;
    }

    private static void ValidateReturnType(string suiteName, MethodInfo method)
    {
        var rt = method.ReturnType;
        if (rt == typeof(void) || rt == typeof(Task) || rt == typeof(ValueTask))
            return;

        throw new InvalidOperationException(
            $"[Pulse] '{suiteName}.{method.Name}' must return void, Task, or ValueTask. Found: {rt.FullName}.");
    }

    private static string FormatArguments(object?[] arguments)
    {
        if (arguments.Length == 0) return string.Empty;
        return string.Join(", ", arguments.Select(FormatOne));

        static string FormatOne(object? value) => value switch
        {
            null => "null",
            string s => $"\"{s}\"",
            char c => $"'{c}'",
            bool b => b ? "true" : "false",
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? value.GetType().Name,
        };
    }
}
