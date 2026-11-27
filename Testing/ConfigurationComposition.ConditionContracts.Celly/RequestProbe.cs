using ConfigurationComposition.ConditionContracts;

namespace ConfigurationComposition.ConditionContracts.CellyAdapter;

// 試験専用の要求境界。設定合成や起動処理の製品実装ではない。
internal static class RequestProbe
{
    private sealed record Result(ConditionOutcome Outcome, int Steps,
        IReadOnlyList<bool>? Decisions, IReadOnlyList<ConditionResult> Observed);

    // 5 と 6 は境界確認用の値であり、製品の評価上限ではない。
    private static Result EvaluateRequest(IConditionEvaluator evaluator, int budget,
        CancellationToken token = default, ConditionTestHooks? secondHooks = null)
    {
        var pending = new List<bool>();
        var observed = new List<ConditionResult>();
        var steps = 0;
        foreach (var (expression, hooks) in new (string, ConditionTestHooks?)[]
        {
            ("count >= 4", null), ("count == 4", secondHooks)
        })
        {
            var result = evaluator.Evaluate(new ConditionInput(expression,
                new Dictionary<string, ScalarKind> { ["count"] = ScalarKind.Int64 },
                new Dictionary<string, object?> { ["count"] = 4L },
                new ConditionLimits(EvaluationSteps: budget - steps)), token, hooks);
            observed.Add(result);
            steps += result.ExecutedSteps;
            if (result.Outcome is not (ConditionOutcome.True or ConditionOutcome.False))
                return new(result.Outcome, steps, null, observed);
            pending.Add(result.Outcome == ConditionOutcome.True);
        }

        return token.IsCancellationRequested
            ? new(ConditionOutcome.Cancelled, steps, null, observed)
            : new(ConditionOutcome.True, steps, pending.ToArray(), observed);
    }

    public static int Run(IConditionEvaluator evaluator)
    {
        var failed = 0;
        void Check(string name, Result result, ConditionOutcome second, int secondSteps,
            bool adopted)
        {
            var pass = result.Observed.Count == 2
                && result.Observed[0] == new ConditionResult(ConditionOutcome.True, 3)
                && result.Observed[1] == new ConditionResult(second, secondSteps)
                && result.Steps == 3 + secondSteps && result.Outcome == second
                && (adopted ? result.Decisions?.SequenceEqual(new[] { true, true }) == true
                    : result.Decisions is null);
            Console.WriteLine($"{(pass ? "PASS" : "FAIL")} {name}: {result.Outcome}, total={result.Steps}, " +
                $"first={result.Observed.FirstOrDefault()}, second={result.Observed.LastOrDefault()}, " +
                $"published={result.Decisions?.Count.ToString() ?? "none"}");
            if (!pass) failed++;
        }

        Check("RP01 cumulative 5", EvaluateRequest(evaluator, 5),
            ConditionOutcome.EvaluationLimitExceeded, 2, false);
        Check("RP02 cumulative 6", EvaluateRequest(evaluator, 6),
            ConditionOutcome.True, 3, true);
        Check("RP03 later error", EvaluateRequest(evaluator, 6,
                secondHooks: new(FaultedVariable: "count")),
            ConditionOutcome.EvaluationError, 3, false);
        using var cts = new CancellationTokenSource();
        Check("RP04 later cancellation", EvaluateRequest(evaluator, 6, cts.Token,
                new(StepEntered: step => { if (step == 2) cts.Cancel(); })),
            ConditionOutcome.Cancelled, 2, false);
        Console.WriteLine($"{4 - failed}/4 passed (request probe only; no product composition or startup)");
        return failed == 0 ? 0 : 1;
    }
}
