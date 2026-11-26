using System.Text;
using Celly;
using Celly.Ast;
using Celly.Checking;
using Celly.Common;
using Celly.Providers;
using Celly.Stdlib;
using Celly.Types;
using Celly.Values;

namespace ConfigurationComposition.ConditionContracts.CellyAdapter;

// 試験専用の候補。製品コンポーザの実装や公開APIではない。
internal sealed class CellyConditionEvaluator : IConditionEvaluator
{
    private static readonly FunctionRegistry Standard = StandardFunctions.CreateRegistry();
    // 利用者の式全体を Celly の Program/Eval に渡さない。
    // この2式だけは固定の型検査済み論理演算として、通常のCELエラー吸収に使う。
    private static readonly CelProgram And = PrepareLogical("_left && _right");
    private static readonly CelProgram Or = PrepareLogical("_left || _right");

    public ConditionResult Evaluate(ConditionInput input, CancellationToken cancellationToken = default,
        ConditionTestHooks? hooks = null)
    {
        var guard = new EvaluationGuard(input.Limits.EvaluationSteps, cancellationToken, hooks);
        try
        {
            guard.CheckCancellation();
            var limits = input.Limits;
            if (limits.ExpressionBytes < 0 || limits.AstNodes < 1 || limits.AstDepth < 1
                || limits.StringBytes < 0 || limits.EvaluationSteps < 0)
                throw new Rejected(ConditionOutcome.PreparationLimitExceeded);
            ValidateText(input.Expression, limits.ExpressionBytes, ConditionOutcome.SyntaxError);
            var declarations = new Dictionary<string, ScalarKind>(input.Declarations, StringComparer.Ordinal);
            var bindings = new Dictionary<string, object?>(input.Arguments, StringComparer.Ordinal);
            ValidateBindings(declarations, bindings, limits.StringBytes, guard);
            var env = CelEnv.Create(new CelEnvSettings
            {
                DisableMacros = true,
                EnableOptionalSyntax = false,
                Declarations = declarations.Select(pair => new VariableDecl(pair.Key, ToCelType(pair.Value))).ToArray()
            });
            var parsed = env.Parse(input.Expression);
            guard.CheckCancellation();
            if (parsed.Ast is null) throw new Rejected(ConditionOutcome.SyntaxError);

            var nodeCount = 0;
            ValidateShape(parsed.Ast.Expr, declarations, limits, guard, 1, ref nodeCount);
            var checkedResult = env.Check(parsed.Ast);
            guard.CheckCancellation();
            if (checkedResult.HasErrors || checkedResult.TypeOf(parsed.Ast.Expr) != CelType.Bool)
                throw new Rejected(ConditionOutcome.TypeError);
            ValidateTypes(parsed.Ast.Expr, checkedResult, guard);

            var value = Visit(parsed.Ast.Expr, bindings, guard, hooks);
            guard.CheckCancellation();
            return new ConditionResult(value switch
            {
                BoolValue { Value: true } => ConditionOutcome.True,
                BoolValue { Value: false } => ConditionOutcome.False,
                _ => ConditionOutcome.EvaluationError
            }, guard.Steps);
        }
        catch (Rejected error)
        {
            return new(error.Outcome, guard.Steps);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(ConditionOutcome.Cancelled, guard.Steps);
        }
    }

    private static CelProgram PrepareLogical(string expression)
    {
        var env = CelEnv.Create(new CelEnvSettings
        {
            DisableMacros = true,
            EnableOptionalSyntax = false,
            Declarations = [new VariableDecl("_left", CelType.Bool), new VariableDecl("_right", CelType.Bool)]
        });
        var ast = env.Parse(expression).Ast ?? throw new InvalidOperationException("固定の論理式の解析に失敗");
        var check = env.Check(ast);
        if (check.HasErrors || check.TypeOf(ast.Expr) != CelType.Bool)
            throw new InvalidOperationException("固定の論理式の型検査に失敗");
        return env.Program(ast);
    }

    private static CelType ToCelType(ScalarKind kind) => kind switch
    {
        ScalarKind.Bool => CelType.Bool,
        ScalarKind.Int64 => CelType.Int,
        ScalarKind.Double => CelType.Double,
        ScalarKind.String => CelType.String,
        _ => throw new Rejected(ConditionOutcome.InvalidBinding)
    };

    private static void ValidateText(string value, int maxBytes, ConditionOutcome invalid)
    {
        if (value.Length > maxBytes) throw new Rejected(ConditionOutcome.PreparationLimitExceeded);
        try
        {
            if (new UTF8Encoding(false, true).GetByteCount(value) > maxBytes)
                throw new Rejected(ConditionOutcome.PreparationLimitExceeded);
        }
        catch (EncoderFallbackException)
        {
            throw new Rejected(invalid);
        }
    }

    private static void ValidateBindings(Dictionary<string, ScalarKind> declarations, Dictionary<string, object?> bindings,
        int maxStringBytes, EvaluationGuard guard)
    {
        if (bindings.Count != declarations.Count) throw new Rejected(ConditionOutcome.InvalidBinding);
        foreach (var (name, kind) in declarations)
        {
            guard.CheckCancellation();
            if (!bindings.TryGetValue(name, out var value)) throw new Rejected(ConditionOutcome.InvalidBinding);
            if (kind == ScalarKind.Bool && value is bool || kind == ScalarKind.Int64 && value is long
                || kind == ScalarKind.Double && value is double d && double.IsFinite(d))
                continue;
            if (kind == ScalarKind.String && value is string text)
            {
                ValidateText(text, maxStringBytes, ConditionOutcome.InvalidBinding);
                continue;
            }
            throw new Rejected(ConditionOutcome.InvalidBinding);
        }
    }

    private static bool IsComparison(string function) => function is Operators.Equals or Operators.NotEquals
        or Operators.Less or Operators.LessEquals or Operators.Greater or Operators.GreaterEquals;

    private static bool IsAllowedCall(CallExpr call) =>
        call.Target is null &&
        (IsComparison(call.Function) || call.Function is Operators.LogicalAnd or Operators.LogicalOr
            ? call.Args.Count == 2
            : call.Function == Operators.LogicalNot && call.Args.Count == 1);

    private static void ValidateShape(Expr node, Dictionary<string, ScalarKind> declarations,
        ConditionLimits limits, EvaluationGuard guard, int depth, ref int count)
    {
        guard.CheckCancellation();
        if (++count > limits.AstNodes || depth > limits.AstDepth)
            throw new Rejected(ConditionOutcome.PreparationLimitExceeded);
        switch (node)
        {
            case IdentExpr ident:
                if (!declarations.ContainsKey(ident.Name)) throw new Rejected(ConditionOutcome.UnknownVariable);
                return;
            case ConstExpr { Value.Kind: ConstantKind.Bool or ConstantKind.Int }:
                return;
            case ConstExpr { Value.Kind: ConstantKind.Double } constant when double.IsFinite(constant.Value.DoubleValue):
                return;
            case ConstExpr { Value.Kind: ConstantKind.String } constant:
                ValidateText(constant.Value.StringValue, limits.StringBytes, ConditionOutcome.SyntaxError);
                return;
            case CallExpr call when IsAllowedCall(call):
                foreach (var argument in call.Args)
                    ValidateShape(argument, declarations, limits, guard, depth + 1, ref count);
                return;
            default:
                throw new Rejected(ConditionOutcome.UnsupportedExpression);
        }
    }

    private static void ValidateTypes(Expr node, CheckResult check, EvaluationGuard guard)
    {
        guard.CheckCancellation();
        var type = check.TypeOf(node);
        if (type != CelType.Bool && type != CelType.Int && type != CelType.Double && type != CelType.String)
            throw new Rejected(ConditionOutcome.TypeError);
        if (node is not CallExpr call) return;
        // 初版の型付き単一値には暗黙の数値変換や異種等値を含めない。
        if (IsComparison(call.Function) && check.TypeOf(call.Args[0]) != check.TypeOf(call.Args[1]))
            throw new Rejected(ConditionOutcome.TypeError);
        foreach (var argument in call.Args)
            ValidateTypes(argument, check, guard);
    }

    private static CelValue Visit(Expr node, Dictionary<string, object?> bindings, EvaluationGuard guard, ConditionTestHooks? hooks)
    {
        guard.Enter();
        CelValue value = node switch
        {
            // 試験に限り、妥当な束縛を読んだ時の通常式エラーを注入する。
            IdentExpr ident when ident.Name == hooks?.FaultedVariable => new ErrorValue("test evaluation error"),
            IdentExpr ident => NativeTypeAdapter.Instance.NativeToValue(bindings[ident.Name]),
            ConstExpr constant => constant.Value.Kind switch
            {
                ConstantKind.Bool => BoolValue.Of(constant.Value.BoolValue),
                ConstantKind.Int => IntValue.Of(constant.Value.IntValue),
                ConstantKind.Double => DoubleValue.Of(constant.Value.DoubleValue),
                ConstantKind.String => StringValue.Of(constant.Value.StringValue),
                _ => throw new Rejected(ConditionOutcome.UnsupportedExpression)
            },
            CallExpr call => EvaluateCall(call, bindings, guard, hooks),
            _ => throw new Rejected(ConditionOutcome.UnsupportedExpression)
        };
        guard.CheckCancellation();
        return value;
    }

    private static CelValue EvaluateCall(CallExpr call, Dictionary<string, object?> bindings,
        EvaluationGuard guard, ConditionTestHooks? hooks)
    {
        var left = Visit(call.Args[0], bindings, guard, hooks);
        if (call.Function is Operators.LogicalAnd or Operators.LogicalOr)
        {
            // 決定済みの左辺だけで結果が決まるときは、右辺を実行しない。
            if (left is BoolValue b && (call.Function == Operators.LogicalAnd ? !b.Value : b.Value))
                return left;
            var right = Visit(call.Args[1], bindings, guard, hooks);
            guard.CheckCancellation();
            return (call.Function == Operators.LogicalAnd ? And : Or).Eval(
                new Dictionary<string, object?> { ["_left"] = left, ["_right"] = right });
        }

        CelValue[] args = call.Args.Count == 1 ? [left] : [left, Visit(call.Args[1], bindings, guard, hooks)];
        guard.CheckCancellation();
        // 通常の式エラーはCEL値として運ぶ。取消・予算超過はここへ値として来ない。
        return args.FirstOrDefault(arg => arg is ErrorValue)
            ?? (Standard.Find(call.Function) ?? throw new Rejected(ConditionOutcome.UnsupportedExpression))(args);
    }

    private sealed class Rejected(ConditionOutcome outcome) : Exception
    {
        public ConditionOutcome Outcome { get; } = outcome;
    }

    private sealed class EvaluationGuard(int maxSteps, CancellationToken token, ConditionTestHooks? hooks)
    {
        public int Steps { get; private set; }
        public void CheckCancellation() => token.ThrowIfCancellationRequested();

        public void Enter()
        {
            CheckCancellation();
            if (Steps >= maxSteps) throw new Rejected(ConditionOutcome.EvaluationLimitExceeded);
            Steps++;
            hooks?.StepEntered?.Invoke(Steps);
            CheckCancellation();
        }
    }
}
