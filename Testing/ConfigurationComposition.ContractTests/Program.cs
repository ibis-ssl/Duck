using System.Xml.Linq;

var cases = new (string Name, Action Run)[]
{
    ("複数・入れ子 include と値の出典", Contracts.NestedIncludesPreserveOrigins),
    ("子ごとの型付き引数", Contracts.ChildArgumentsAreTypedAndIndependent),
    ("明示引数が既定値に優先", Contracts.ExplicitArgumentsOverrideDefaults),
    ("条件あり・なし include", Contracts.IncludeConditionsSelectChildren),
    ("構造の未知キー", Contracts.UnknownStructuralKeyIsRejected),
    ("循環 include", Contracts.CycleIsRejected),
    ("後の同型値を採用し出典・順序を保持", Contracts.LaterTypedValueWinsAndKeepsHistory),
    ("型違いを拒否", Contracts.TypeMismatchIsRejected),
    ("失敗時に起動しない", Contracts.InvalidPlanDoesNotActivate),
    ("試験プロジェクトに Aspire 依存がない", Contracts.ProjectHasNoAspirePackageReference),
};

var failures = 0;
foreach (var (name, run) in cases)
{
    try
    {
        run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception error)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {error.Message}");
    }
}

Console.WriteLine($"{cases.Length - failures}/{cases.Length} passed (test-only reference; no product adapter)");
return failures == 0 ? 0 : 1;

static class Contracts
{
    public static void NestedIncludesPreserveOrigins()
    {
        var files = new Dictionary<string, Document>
        {
            ["root"] = new([new("left", EmptyArgs), new("right", EmptyArgs)]),
            ["left"] = new([new("shared", EmptyArgs)], [Named("alpha", "duck.runtime", Map(("Port", 10020L)))]),
            ["right"] = new([new("shared", EmptyArgs)], [Named("beta", "duck.runtime", Map(("Port", 10021L)))]),
            ["shared"] = new([], [Named("common", "duck.development.base", Map(("Lifetime", "session")))])
        };

        var result = ReferenceComposer.Compose(files, "root");
        Equal(3, result.Instances.Count);
        Equal("shared", result.Instances["common"].Origins["Lifetime"]);
        Equal("left", result.Instances["alpha"].Origins["Port"]);
        Equal("right", result.Instances["beta"].Origins["Port"]);
    }

    public static void ChildArgumentsAreTypedAndIndependent()
    {
        var files = new Dictionary<string, Document>
        {
            ["root"] = new([
                new("runtime", Args(("instance", "duck-main"), ("outputPort", 11010L))),
                new("runtime", Args(("instance", "duck-candidate"), ("outputPort", 11011L)))
            ]),
            ["runtime"] = new([], [new(
                Parameter("instance"), "duck.runtime", Map(("Port", Parameter("outputPort"))))],
                Parameters(("instance", typeof(string)), ("outputPort", typeof(long))))
        };

        var result = ReferenceComposer.Compose(files, "root");
        Equal("duck.runtime", result.Instances["duck-main"].Kind);
        Equal(11010L, result.Instances["duck-main"].Settings["Port"]);
        Equal(11011L, result.Instances["duck-candidate"].Settings["Port"]);
        Equal(typeof(long), result.Instances["duck-main"].Settings["Port"].GetType());
        var invalid = new Dictionary<string, Document>
        {
            ["root"] = new([new("runtime", Args(("instance", "duck-main"), ("outputPort", "11010")))]),
            ["runtime"] = files["runtime"]
        };
        Throws<CompositionException>(() => ReferenceComposer.Compose(invalid, "root"));
    }

    public static void ExplicitArgumentsOverrideDefaults()
    {
        var files = new Dictionary<string, Document>
        {
            ["root"] = new([new("runtime", Args(("instance", "duck"), ("outputPort", 11010L)))]),
            ["runtime"] = new([], [new(
                Parameter("instance"), "duck.runtime", Map(("Port", Parameter("outputPort"))))],
                ParametersWithDefaults(("instance", typeof(string), null), ("outputPort", typeof(long), 10020L)))
        };
        var result = ReferenceComposer.Compose(files, "root");
        Equal(11010L, result.Instances["duck"].Settings["Port"]);
    }

    public static void IncludeConditionsSelectChildren()
    {
        var files = new Dictionary<string, Document>
        {
            ["root"] = new([
                new("always", EmptyArgs),
                new("selected", EmptyArgs, When: true),
                new("skipped", EmptyArgs, When: false)
            ]),
            ["always"] = OneInstance("base"),
            ["selected"] = OneInstance("comparison"),
            ["skipped"] = OneInstance("never")
        };

        var result = ReferenceComposer.Compose(files, "root");
        True(result.Instances.ContainsKey("base"));
        True(result.Instances.ContainsKey("comparison"));
        True(!result.Instances.ContainsKey("never"));
    }

    public static void UnknownStructuralKeyIsRejected()
    {
        var files = new Dictionary<string, Document>
        {
            ["root"] = new([], UnknownKeys: ["mystery"])
        };
        Throws<CompositionException>(() => ReferenceComposer.Compose(files, "root"));
    }

    public static void CycleIsRejected()
    {
        var files = new Dictionary<string, Document>
        {
            ["a"] = new([new("b", EmptyArgs)]),
            ["b"] = new([new("a", EmptyArgs)])
        };
        Throws<CompositionException>(() => ReferenceComposer.Compose(files, "a"));
    }

    // 記述順で後の同型値が採用され、採用元と適用履歴が結果に残る契約を確認する。参照モデルの結果は製品動作の証拠ではない。
    public static void LaterTypedValueWinsAndKeepsHistory()
    {
        var files = new Dictionary<string, Document>
        {
            ["root"] = new([new("first", EmptyArgs), new("second", EmptyArgs)]),
            ["first"] = OneInstance("duck", ("Port", 10020L)),
            ["second"] = OneInstance("duck", ("Port", 10021L))
        };
        var result = ReferenceComposer.Compose(files, "root");
        Equal(10021L, result.Instances["duck"].Settings["Port"]);
        var trace = result.Instances["duck"].Origins["Port"];
        Equal("second", trace);
        Equal("first,second", string.Join(',', result.Instances["duck"].ApplicationOrder));
    }

    public static void TypeMismatchIsRejected()
    {
        var files = new Dictionary<string, Document>
        {
            ["root"] = new([new("first", EmptyArgs), new("second", EmptyArgs)]),
            ["first"] = OneInstance("duck", ("Port", 10020L)),
            ["second"] = OneInstance("duck", ("Port", 10020.0))
        };
        Throws<CompositionException>(() => ReferenceComposer.Compose(files, "root"));
    }

    public static void InvalidPlanDoesNotActivate()
    {
        var files = new Dictionary<string, Document>
        {
            ["root"] = new([new("a", EmptyArgs), new("b", EmptyArgs)]),
            ["a"] = OneInstance("duplicate", ("Port", 10020L)),
            ["b"] = new([], [Named("duplicate", "duck.other", Map(("Port", 10021L)))])
        };
        var activationCount = 0;
        try
        {
            var resolved = ReferenceComposer.Compose(files, "root");
            Activate(resolved, () => activationCount++);
        }
        catch (CompositionException)
        {
            // 全体の解決が成功した場合だけ起動処理へ進む契約を確認する。
        }
        Equal(0, activationCount);
    }

    public static void ProjectHasNoAspirePackageReference()
    {
        var projectPath = Path.Combine(AppContext.BaseDirectory, "ConfigurationComposition.ContractTests.csproj");
        if (!File.Exists(projectPath))
            projectPath = Path.Combine(Environment.CurrentDirectory, "Testing", "ConfigurationComposition.ContractTests", "ConfigurationComposition.ContractTests.csproj");
        var project = XDocument.Load(projectPath);
        True(!project.Descendants().Any(element =>
            element.Name.LocalName == "PackageReference" &&
            ((string?)element.Attribute("Include"))?.StartsWith("Aspire.", StringComparison.OrdinalIgnoreCase) == true));
        True(!project.Descendants().Any(element =>
            element.Name.LocalName == "ProjectReference" &&
            ((string?)element.Attribute("Include"))?.Contains("Aspire", StringComparison.OrdinalIgnoreCase) == true));
    }

    private static void Activate(ResolvedPlan plan, Action start)
    {
        _ = plan;
        start();
    }

    private static Document OneInstance(string name, params (string Key, object Value)[] settings) =>
        new([], [Named(name, "duck.runtime", Map(settings))]);

    private static Instance Named(string name, string kind, IReadOnlyDictionary<string, Value> settings) =>
        new(Value.Literal(name), kind, settings);

    private static IReadOnlyDictionary<string, Value> Map(params (string Key, object Value)[] values) =>
        values.ToDictionary(pair => pair.Key, pair => pair.Value is Value value ? value : Value.Literal(pair.Value));

    private static IReadOnlyDictionary<string, object> Args(params (string Key, object Value)[] values) =>
        values.ToDictionary(pair => pair.Key, pair => pair.Value);

    private static IReadOnlyDictionary<string, ParameterSpec> Parameters(params (string Name, Type Type)[] declarations) =>
        declarations.ToDictionary(pair => pair.Name, pair => new ParameterSpec(pair.Type), StringComparer.Ordinal);
    private static IReadOnlyDictionary<string, ParameterSpec> ParametersWithDefaults(params (string Name, Type Type, object? DefaultValue)[] declarations) =>
        declarations.ToDictionary(pair => pair.Name, pair => new ParameterSpec(pair.Type, pair.DefaultValue), StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, object> EmptyArgs = Args();
    private static Value Parameter(string name) => Value.FromParameter(name);

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"expected {expected}, got {actual}");
    }

    private static void True(bool value)
    {
        if (!value) throw new InvalidOperationException("condition was false");
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"expected {typeof(T).Name}");
    }
}

sealed record Include(string File, IReadOnlyDictionary<string, object> Args, bool? When = null);
sealed record Instance(Value Name, string Kind, IReadOnlyDictionary<string, Value> Settings);
sealed record ParameterSpec(Type Type, object? DefaultValue = null);
sealed record Document(
    IReadOnlyList<Include> Includes,
    IReadOnlyList<Instance>? Instances = null,
    IReadOnlyDictionary<string, ParameterSpec>? Parameters = null,
    IReadOnlyList<string>? UnknownKeys = null);
sealed record ResolvedInstance(string Kind, IReadOnlyDictionary<string, object> Settings, IReadOnlyDictionary<string, string> Origins, IReadOnlyList<string> ApplicationOrder);
sealed record ResolvedPlan(IReadOnlyDictionary<string, ResolvedInstance> Instances);
sealed record Value(object? Data, string? ParameterName)
{
    public static Value Literal(object value) => new(value, null);
    public static Value FromParameter(string name) => new(null, name);
}
sealed class CompositionException(string message) : Exception(message);

static class ReferenceComposer
{
    private static readonly HashSet<string> KnownKeys = ["version", "parameters", "includes", "instances"];

    public static ResolvedPlan Compose(IReadOnlyDictionary<string, Document> files, string entry)
    {
        var result = new Dictionary<string, ResolvedInstance>(StringComparer.Ordinal);
        Visit(entry, new HashSet<string>(StringComparer.Ordinal), files, result, new Dictionary<string, object>(), new List<string>());
        return new(result);
    }

    private static void Visit(string path, HashSet<string> active, IReadOnlyDictionary<string, Document> files,
        Dictionary<string, ResolvedInstance> result, IReadOnlyDictionary<string, object> arguments, List<string> applicationOrder)
    {
        if (!active.Add(path)) throw new CompositionException($"include cycle at {path}");
        if (!files.TryGetValue(path, out var document)) throw new CompositionException($"missing document {path}");
        if (document.UnknownKeys?.Any(key => !KnownKeys.Contains(key)) == true)
            throw new CompositionException("unknown structural key");

        foreach (var include in document.Includes)
        {
            if (include.When == false) continue;
            if (!files.TryGetValue(include.File, out var child)) throw new CompositionException($"missing document {include.File}");
            var childArguments = BindArguments(child.Parameters, include.Args);
            Visit(include.File, active, files, result, childArguments, applicationOrder);
        }

        foreach (var instance in document.Instances ?? [])
        {
            var expandedName = ResolveValue(instance.Name, document.Parameters, arguments) as string
                ?? throw new CompositionException("instance name must resolve to string");
            var settings = ResolveSettings(instance.Settings, document.Parameters, arguments);
            applicationOrder.Add(path);
            if (result.TryGetValue(expandedName, out var existing))
            {
                if (existing.Kind != instance.Kind) throw new CompositionException($"conflicting kind {expandedName}");
                var merged = new Dictionary<string, object>(existing.Settings, StringComparer.Ordinal);
                var origins = new Dictionary<string, string>(existing.Origins, StringComparer.Ordinal);
                foreach (var (key, value) in settings)
                {
                    if (merged.TryGetValue(key, out var oldValue) && oldValue.GetType() != value.GetType())
                        throw new CompositionException($"type mismatch at {expandedName}/{key}");
                    merged[key] = value;
                    origins[key] = path;
                }
                result[expandedName] = new(instance.Kind, merged, origins, existing.ApplicationOrder.Append(path).ToArray());
                continue;
            }
            result.Add(expandedName, new(instance.Kind, settings, settings.Keys.ToDictionary(key => key, _ => path), [path]));
        }
        active.Remove(path);
    }

    private static IReadOnlyDictionary<string, object> BindArguments(IReadOnlyDictionary<string, ParameterSpec>? declarations,
        IReadOnlyDictionary<string, object> supplied)
    {
        var declared = declarations ?? new Dictionary<string, ParameterSpec>();
        if (supplied.Keys.Any(key => !declared.ContainsKey(key))) throw new CompositionException("unknown child argument");
        var resolved = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (name, spec) in declared)
        {
            if (supplied.TryGetValue(name, out var value))
            {
                if (value.GetType() != spec.Type) throw new CompositionException("child argument type mismatch");
                resolved.Add(name, value);
            }
            else if (spec.DefaultValue is not null) resolved.Add(name, spec.DefaultValue);
            else throw new CompositionException("missing required child argument");
        }
        return resolved;
    }

    private static IReadOnlyDictionary<string, object> ResolveSettings(IReadOnlyDictionary<string, Value> settings,
        IReadOnlyDictionary<string, ParameterSpec>? declarations, IReadOnlyDictionary<string, object> activeArguments)
    {
        var resolved = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (key, value) in settings)
        {
            if (value.ParameterName is null) resolved.Add(key, value.Data!);
            else
            {
                resolved.Add(key, ResolveValue(value, declarations, activeArguments)!);
            }
        }
        return resolved;
    }

    private static object? ResolveValue(Value value, IReadOnlyDictionary<string, ParameterSpec>? declarations,
        IReadOnlyDictionary<string, object> arguments)
    {
        if (value.ParameterName is null) return value.Data;
        if (declarations is null || !declarations.TryGetValue(value.ParameterName, out var type) ||
            !arguments.TryGetValue(value.ParameterName, out var argument) || argument.GetType() != type.Type)
            throw new CompositionException($"invalid or unbound parameter {value.ParameterName}");
        return argument;
    }

}
