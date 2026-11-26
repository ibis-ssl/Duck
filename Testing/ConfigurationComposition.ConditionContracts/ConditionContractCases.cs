namespace ConfigurationComposition.ConditionContracts;

public static class ConditionContractCases
{
    private const string Comparison = "cameraCount >= 4 && mode == \"comparison\"";

    public static int Run(IConditionEvaluator evaluator, TextWriter? output = null)
    {
        output ??= Console.Out;
        var cases = new (string Name, Action Test)[]
        {
            ("CC01 比較と論理の4組合せ", () =>
            {
                foreach (var (count, mode, expected) in new[]
                {
                    (3L, "comparison", ConditionOutcome.False), (3L, "other", ConditionOutcome.False),
                    (4L, "comparison", ConditionOutcome.True), (4L, "other", ConditionOutcome.False)
                })
                    Expect(evaluator.Evaluate(Input(Comparison, count, mode)), expected);
            }),
            ("CC02 評価結果が偽なら取り込み先に触れない", () =>
            {
                var opens = 0;
                void OpenIfSelected(ConditionResult result)
                {
                    if (result.Outcome == ConditionOutcome.True) opens++;
                }
                var excluded = evaluator.Evaluate(Input(Comparison, count: 3));
                Expect(excluded, ConditionOutcome.False);
                OpenIfSelected(excluded);
                Equal(0, opens);
                var selected = evaluator.Evaluate(Input(Comparison));
                Expect(selected, ConditionOutcome.True);
                OpenIfSelected(selected);
                Equal(1, opens);
                var failed = evaluator.Evaluate(Input("unknown == 4"));
                Expect(failed, ConditionOutcome.UnknownVariable);
                OpenIfSelected(failed);
                Equal(1, opens);
            }),
            ("CC03 真偽・有限double・否定", () =>
            {
                Expect(evaluator.Evaluate(Input("enabled && !(ratio > 0.5) && cameraCount != 3")), ConditionOutcome.True);
                Expect(evaluator.Evaluate(Input("enabled && ratio < 0.5")), ConditionOutcome.False);
            }),
            ("CC04 符号付き64ビット整数と大小境界", () =>
            {
                Expect(evaluator.Evaluate(Input(
                    "cameraCount >= -9223372036854775808 && cameraCount <= 9223372036854775807 && ratio > -0.5 && ratio < 1.0")),
                    ConditionOutcome.True);
                Expect(evaluator.Evaluate(Input("cameraCount == -9223372036854775808", count: long.MinValue)), ConditionOutcome.True);
                Expect(evaluator.Evaluate(Input("cameraCount == 9223372036854775807", count: long.MaxValue)), ConditionOutcome.True);
            }),
            ("CC05 構文誤りと範囲外の整数を拒否", () =>
            {
                Expect(evaluator.Evaluate(Input("cameraCount >=")), ConditionOutcome.SyntaxError);
                Expect(evaluator.Evaluate(Input("cameraCount == 9223372036854775808")), ConditionOutcome.SyntaxError);
            }),
            ("CC06 未宣言変数を未評価分岐でも拒否", () =>
            {
                Expect(evaluator.Evaluate(Input("unknown == 4")), ConditionOutcome.UnknownVariable);
                Expect(evaluator.Evaluate(Input("true || unknown == 4")), ConditionOutcome.UnknownVariable);
                Expect(evaluator.Evaluate(Input("CameraCount == 4")), ConditionOutcome.UnknownVariable);
            }),
            ("CC07 型違いと非真偽結果を拒否", () =>
            {
                foreach (var expression in new[] { "mode == 4", "mode >= 4", "cameraCount", "cameraCount == 4.0" })
                    Expect(evaluator.Evaluate(Input(expression)), ConditionOutcome.TypeError);
            }),
            ("CC08 許可外の式を未評価分岐も含めて拒否", () =>
            {
                foreach (var expression in new[]
                {
                    "mode.startsWith(\"c\")", "true || size(mode) > 0", "mode.length == 3",
                    "[1,2].all(x,true)", "true || cameraCount + 1 > 0", "mode[0] == \"c\"",
                    "true ? true : false", "1u == 1u", "null == null"
                })
                {
                    var result = evaluator.Evaluate(Input(expression));
                    // 無効にした構文をパーサーで拒否する候補も許容する。
                    Require(result.Outcome is ConditionOutcome.UnsupportedExpression or ConditionOutcome.SyntaxError,
                        "許可外の式が式エラーとして拒否されなかった");
                    Equal(0, result.ExecutedSteps);
                }
            }),
            ("CC09 宣言と異なる束縛・欠落・余分な入力を拒否", () =>
            {
                var input = Input(Comparison);
                foreach (var args in new Dictionary<string, object?>[]
                {
                    Copy(input, "cameraCount", 4), Copy(input, "cameraCount", "4"),
                    Copy(input, "ratio", double.NaN), Copy(input, "ratio", double.PositiveInfinity),
                    Copy(input, "mode", null), Copy(input, "extra", true),
                    input.Arguments.Where(pair => pair.Key != "mode").ToDictionary()
                })
                    Expect(evaluator.Evaluate(input with { Arguments = args }), ConditionOutcome.InvalidBinding);
            }),
            ("CC10 文字列比較はUnicodeコードポイント順・等値・大小文字区別", () =>
            {
                Expect(evaluator.Evaluate(Input("mode > \"\uE000\"", mode: "😀")), ConditionOutcome.True);
                Expect(evaluator.Evaluate(Input("mode < \"😀\"", mode: "\uE000")), ConditionOutcome.True);
                Expect(evaluator.Evaluate(Input("mode == \"日本語\"", mode: "日本語")), ConditionOutcome.True);
                Expect(evaluator.Evaluate(Input("mode == \"comparison\"", mode: "Comparison")), ConditionOutcome.False);
                Expect(evaluator.Evaluate(Input("mode == \"é\"", mode: "e\u0301")), ConditionOutcome.False);
            }),
            ("CC11 式長のUTF-8境界", () =>
            {
                Expect(evaluator.Evaluate(Input(new string(' ', 4092) + "true")), ConditionOutcome.True);
                Expect(evaluator.Evaluate(Input(new string(' ', 4093) + "true")), ConditionOutcome.PreparationLimitExceeded);
                var limits = new ConditionLimits(ExpressionBytes: 4);
                Expect(evaluator.Evaluate(Input("true") with { Limits = limits }), ConditionOutcome.True);
                Expect(evaluator.Evaluate(Input("true ") with { Limits = limits }), ConditionOutcome.PreparationLimitExceeded);
                const string unicode = "mode == \"あ\"";
                var bytes = System.Text.Encoding.UTF8.GetByteCount(unicode);
                Expect(evaluator.Evaluate(Input(unicode, mode: "あ") with { Limits = new(ExpressionBytes: bytes) }), ConditionOutcome.True);
                Expect(evaluator.Evaluate(Input(unicode, mode: "あ") with { Limits = new(ExpressionBytes: bytes - 1) }), ConditionOutcome.PreparationLimitExceeded);
            }),
            ("CC12 構文木ノード数の境界", () =>
            {
                Expect(evaluator.Evaluate(Input("enabled && true") with { Limits = new(AstNodes: 3) }), ConditionOutcome.True);
                Expect(evaluator.Evaluate(Input("enabled && true") with { Limits = new(AstNodes: 2) }), ConditionOutcome.PreparationLimitExceeded);
                // 深さと独立に、64ノードは許可し、均衡した65ノードを拒否する。
                Expect(evaluator.Evaluate(Input("!(" + Balanced(32) + ")")), ConditionOutcome.False);
                Expect(evaluator.Evaluate(Input(Balanced(33))), ConditionOutcome.PreparationLimitExceeded);
            }),
            ("CC13 構文木深さの境界", () =>
            {
                Expect(evaluator.Evaluate(Input("enabled && true") with { Limits = new(AstDepth: 2) }), ConditionOutcome.True);
                Expect(evaluator.Evaluate(Input("enabled && true") with { Limits = new(AstDepth: 1) }), ConditionOutcome.PreparationLimitExceeded);
                Expect(evaluator.Evaluate(Input(string.Join(" && ", Enumerable.Repeat("enabled", 16)))), ConditionOutcome.True);
                Expect(evaluator.Evaluate(Input(string.Join(" && ", Enumerable.Repeat("enabled", 17)))), ConditionOutcome.PreparationLimitExceeded);
            }),
            ("CC14 文字列値と文字列定数の長さ・不正Unicode", () =>
            {
                const string expr = "mode == \"あ\"";
                Expect(evaluator.Evaluate(Input("true", mode: new string('x', 256))), ConditionOutcome.True);
                Expect(evaluator.Evaluate(Input("true", mode: new string('x', 257))), ConditionOutcome.PreparationLimitExceeded);
                Expect(evaluator.Evaluate(Input(expr, mode: "あ") with { Limits = new(StringBytes: 3) }), ConditionOutcome.True);
                Expect(evaluator.Evaluate(Input("true", mode: "あ") with { Limits = new(StringBytes: 2) }), ConditionOutcome.PreparationLimitExceeded);
                Expect(evaluator.Evaluate(Input(expr, mode: "x") with { Limits = new(StringBytes: 2) }), ConditionOutcome.PreparationLimitExceeded);
                Expect(evaluator.Evaluate(Input("true", mode: new string((char)0xd800, 1))), ConditionOutcome.InvalidBinding);
            }),
            ("CC15 実行予算の境界と実行されなかったノード", () =>
            {
                var zero = evaluator.Evaluate(Input("true") with { Limits = new(EvaluationSteps: 0) });
                Expect(zero, ConditionOutcome.EvaluationLimitExceeded);
                Equal(0, zero.ExecutedSteps);
                var exact = evaluator.Evaluate(Input(Comparison) with { Limits = new(EvaluationSteps: 7) });
                Expect(exact, ConditionOutcome.True);
                Equal(7, exact.ExecutedSteps);
                var exceeded = evaluator.Evaluate(Input(Comparison) with { Limits = new(EvaluationSteps: 6) });
                Expect(exceeded, ConditionOutcome.EvaluationLimitExceeded);
                Equal(6, exceeded.ExecutedSteps);
                var shortcut = evaluator.Evaluate(Input("false && cameraCount >= 4") with { Limits = new(EvaluationSteps: 2) });
                Expect(shortcut, ConditionOutcome.False);
                Equal(2, shortcut.ExecutedSteps);
            }),
            ("CC16 予算超過を論理演算で吸収しない", () =>
            {
                foreach (var expression in new[] { "(cameraCount >= 4) || true", "(cameraCount >= 4) && false" })
                {
                    var result = evaluator.Evaluate(Input(expression) with { Limits = new(EvaluationSteps: 2) });
                    Expect(result, ConditionOutcome.EvaluationLimitExceeded);
                    Equal(2, result.ExecutedSteps);
                }
            }),
            ("CC17 実行前取消は0ステップ", () =>
            {
                using var cts = new CancellationTokenSource();
                cts.Cancel();
                var result = evaluator.Evaluate(Input(Comparison), cts.Token);
                Expect(result, ConditionOutcome.Cancelled);
                Equal(0, result.ExecutedSteps);
            }),
            ("CC18 実行中取消を論理演算で吸収しない", () =>
            {
                foreach (var expression in new[] { "(cameraCount >= 4) || true", "(cameraCount >= 4) && false" })
                {
                    using var cts = new CancellationTokenSource();
                    var observed = new List<int>();
                    var result = evaluator.Evaluate(Input(expression), cts.Token, new(StepEntered: step =>
                    {
                        observed.Add(step);
                        if (step == 2) cts.Cancel();
                    }));
                    Require(cts.IsCancellationRequested, "実行中の取消が発生しなかった");
                    Expect(result, ConditionOutcome.Cancelled);
                    Equal(2, result.ExecutedSteps);
                    Require(observed.SequenceEqual(new[] { 1, 2 }), "取消後に次のステップが実行された");
                }
            }),
            ("CC19 通常の式エラーを失敗として返す", () =>
            {
                var hooks = new ConditionTestHooks(FaultedVariable: "cameraCount");
                Expect(evaluator.Evaluate(Input("cameraCount >= 4"), hooks: hooks), ConditionOutcome.EvaluationError);
                Expect(evaluator.Evaluate(Input("(cameraCount >= 4) && true"), hooks: hooks), ConditionOutcome.EvaluationError);
            }),
            ("CC20 通常の式エラーはCELの論理吸収に従う", () =>
            {
                var hooks = new ConditionTestHooks(FaultedVariable: "cameraCount");
                foreach (var expression in new[] { "(cameraCount >= 4) || true", "true || (cameraCount >= 4)" })
                    Expect(evaluator.Evaluate(Input(expression), hooks: hooks), ConditionOutcome.True);
                foreach (var expression in new[] { "(cameraCount >= 4) && false", "false && (cameraCount >= 4)" })
                    Expect(evaluator.Evaluate(Input(expression), hooks: hooks), ConditionOutcome.False);
            }),
            ("CC21 次の評価は前回の取消・予算状態を引き継がない", () =>
            {
                Expect(evaluator.Evaluate(Input(Comparison) with { Limits = new(EvaluationSteps: 1) }), ConditionOutcome.EvaluationLimitExceeded);
                using var cts = new CancellationTokenSource();
                cts.Cancel();
                Expect(evaluator.Evaluate(Input(Comparison), cts.Token), ConditionOutcome.Cancelled);
                var next = evaluator.Evaluate(Input(Comparison) with { Limits = new(EvaluationSteps: 7) });
                Expect(next, ConditionOutcome.True);
                Equal(7, next.ExecutedSteps);
            })
        };

        var failures = 0;
        foreach (var (name, test) in cases)
        {
            try
            {
                test();
                output.WriteLine($"PASS {name}");
            }
            catch (Exception e)
            {
                failures++;
                // 候補から渡された例外メッセージや入力原文を出力しない。
                var detail = e is ContractAssertion ? e.Message : e.GetType().Name;
                output.WriteLine($"FAIL {name}: {detail}");
            }
        }
        output.WriteLine($"{cases.Length - failures}/{cases.Length} passed (condition contracts; test adapter only)");
        return failures == 0 ? 0 : 1;
    }

    private static ConditionInput Input(string expression, long count = 4, string mode = "comparison") =>
        new(expression,
            new Dictionary<string, ScalarKind>(StringComparer.Ordinal)
            {
                ["cameraCount"] = ScalarKind.Int64, ["mode"] = ScalarKind.String,
                ["enabled"] = ScalarKind.Bool, ["ratio"] = ScalarKind.Double
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["cameraCount"] = count, ["mode"] = mode, ["enabled"] = true, ["ratio"] = 0.5d
            },
            new ConditionLimits());

    private static Dictionary<string, object?> Copy(ConditionInput input, string name, object? value) =>
        new(input.Arguments, StringComparer.Ordinal) { [name] = value };

    private static string Balanced(int leaves) =>
        leaves == 1 ? "enabled" : $"({Balanced(leaves / 2)} && {Balanced(leaves - leaves / 2)})";

    private static void Expect(ConditionResult result, ConditionOutcome expected)
    {
        Equal(expected, result.Outcome);
        if (expected is ConditionOutcome.SyntaxError or ConditionOutcome.UnknownVariable or ConditionOutcome.TypeError
            or ConditionOutcome.UnsupportedExpression or ConditionOutcome.InvalidBinding or ConditionOutcome.PreparationLimitExceeded)
            Equal(0, result.ExecutedSteps);
    }
    private static void Equal<T>(T expected, T actual) =>
        Require(EqualityComparer<T>.Default.Equals(expected, actual), $"期待値={expected}, 実際={actual}");
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new ContractAssertion(message);
    }
    private sealed class ContractAssertion(string message) : Exception(message);
}
