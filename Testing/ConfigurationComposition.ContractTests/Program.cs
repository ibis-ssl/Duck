using System.Xml.Linq;

var cases = new (string Name, Action Run)[]
{
    ("複数・入れ子 include と値の出典", Contracts.NestedIncludesPreserveOrigins),
    ("子ごとの型付き引数", Contracts.ChildArgumentsAreTypedAndIndependent),
    ("条件あり・なし include", Contracts.IncludeConditionsSelectChildren),
    ("構造の未知キー", Contracts.UnknownStructuralKeyIsRejected),
    ("循環 include", Contracts.CycleIsRejected),
    ("明示上書きなしの衝突", Contracts.ConflictingValuesAreRejected),
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
        Equal("shared", result.Instances["common"].Origin);
        Equal("left", result.Instances["alpha"].Origin);
        Equal("right", result.Instances["beta"].Origin);
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

    public static void ConflictingValuesAreRejected()
    {
        var files = new Dictionary<string, Document>
        {
            ["root"] = new([new("first", EmptyArgs), new("second", EmptyArgs)]),
            ["first"] = OneInstance("duck", ("Port", 10020L)),
            ["second"] = OneInstance("duck", ("Port", 10021L))
        };
        Throws<CompositionException>(() => ReferenceComposer.Compose(files, "root"));
    }

    public static void InvalidPlanDoesNotActivate()
    {
        var files = new Dictionary<string, Document>
        {
            ["root"] = new([new("a", EmptyArgs), new("b", EmptyArgs)]),
            ["a"] = OneInstance("duplicate", ("Port", 10020L)),
            ["b"] = OneInstance("duplicate", ("Port", 10021L))
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

    private static IReadOnlyDictionary<string, Type> Parameters(params (string Name, Type Type)[] declarations) =>
        declarations.ToDictionary(pair => pair.Name, pair => pair.Type, StringComparer.Ordinal);
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
sealed record Document(
    IReadOnlyList<Include> Includes,
    IReadOnlyList<Instance>? Instances = null,
    IReadOnlyDictionary<string, Type>? Parameters = null,
    IReadOnlyList<string>? UnknownKeys = null);
sealed record ResolvedInstance(string Kind, IReadOnlyDictionary<string, object> Settings, string Origin);
sealed record ResolvedPlan(IReadOnlyDictionary<string, ResolvedInstance> Instances);
sealed record Value(object? Data, string? ParameterName)
{
    public static Value Literal(object value) => new(value, null);
    public static Value FromParameter(string name) => new(null, name);
}
sealed class CompositionException(string message) : Exception(message);

static class ReferenceComposer
{
    private static readonly HashSet<string> KnownKeys = ["version", "parameters", "includes", "instances", "overrides"];

    public static ResolvedPlan Compose(IReadOnlyDictionary<string, Document> files, string entry)
    {
        var result = new Dictionary<string, ResolvedInstance>(StringComparer.Ordinal);
        Visit(entry, new HashSet<string>(StringComparer.Ordinal), files, result, new Dictionary<string, object>());
        return new(result);
    }

    private static void Visit(string path, HashSet<string> active, IReadOnlyDictionary<string, Document> files,
        Dictionary<string, ResolvedInstance> result, IReadOnlyDictionary<string, object> arguments)
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
            Visit(include.File, active, files, result, childArguments);
        }

        foreach (var instance in document.Instances ?? [])
        {
            var expandedName = ResolveValue(instance.Name, document.Parameters, arguments) as string
                ?? throw new CompositionException("instance name must resolve to string");
            var settings = ResolveSettings(instance.Settings, document.Parameters, arguments);
            var resolved = new ResolvedInstance(instance.Kind, settings, path);
            if (result.TryGetValue(expandedName, out var existing))
            {
                if (existing.Kind != resolved.Kind || !EqualMaps(existing.Settings, resolved.Settings))
                    throw new CompositionException($"conflicting instance {expandedName}");
                continue;
            }
            result.Add(expandedName, resolved);
        }
        active.Remove(path);
    }

    private static IReadOnlyDictionary<string, object> BindArguments(IReadOnlyDictionary<string, Type>? declarations,
        IReadOnlyDictionary<string, object> supplied)
    {
        var declared = declarations ?? new Dictionary<string, Type>();
        if (supplied.Keys.Any(key => !declared.ContainsKey(key))) throw new CompositionException("unknown child argument");
        if (supplied.Any(pair => pair.Value.GetType() != declared[pair.Key])) throw new CompositionException("child argument type mismatch");
        return supplied;
    }

    private static IReadOnlyDictionary<string, object> ResolveSettings(IReadOnlyDictionary<string, Value> settings,
        IReadOnlyDictionary<string, Type>? declarations, IReadOnlyDictionary<string, object> activeArguments)
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

    private static object? ResolveValue(Value value, IReadOnlyDictionary<string, Type>? declarations,
        IReadOnlyDictionary<string, object> arguments)
    {
        if (value.ParameterName is null) return value.Data;
        if (declarations?.TryGetValue(value.ParameterName, out var type) != true ||
            !arguments.TryGetValue(value.ParameterName, out var argument) || argument.GetType() != type)
            throw new CompositionException($"invalid or unbound parameter {value.ParameterName}");
        return argument;
    }

    private static bool EqualMaps(IReadOnlyDictionary<string, object> left, IReadOnlyDictionary<string, object> right) =>
        left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out var value) && Equals(pair.Value, value) && pair.Value.GetType() == value.GetType());
}
