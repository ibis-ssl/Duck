namespace ConfigurationComposition.ConditionContracts;

// 候補ライブラリの型、構文木、例外、診断メッセージを試験の期待値へ持ち込まない。
public enum ScalarKind { Bool, Int64, Double, String }

public enum ConditionOutcome
{
    True,
    False,
    SyntaxError,
    UnknownVariable,
    TypeError,
    UnsupportedExpression,
    InvalidBinding,
    EvaluationError,
    PreparationLimitExceeded,
    EvaluationLimitExceeded,
    Cancelled
}

// 調査用の暫定値。製品の最終値・実行時間の保証を定めるものではない。
public sealed record ConditionLimits(
    int ExpressionBytes = 4096,
    int AstNodes = 64,
    int AstDepth = 16,
    int StringBytes = 256,
    int EvaluationSteps = 64);

public sealed record ConditionInput(
    string Expression,
    IReadOnlyDictionary<string, ScalarKind> Declarations,
    IReadOnlyDictionary<string, object?> Arguments,
    ConditionLimits Limits);

// 1ステップは実行する単一値、変数参照、演算子の各1件。
// 開始前の取消や上限で開始できなかったステップは数えず、短絡した部分も数えない。
public sealed record ConditionResult(ConditionOutcome Outcome, int ExecutedSteps);

public interface IConditionEvaluator
{
    ConditionResult Evaluate(ConditionInput input, CancellationToken cancellationToken = default,
        ConditionTestHooks? hooks = null);
}

// 試験専用。実行開始後の取消、通常の式エラーを時刻や外部資源に依存せず再現する。
// FaultedVariable は宣言・束縛済みの変数を読み取る瞬間だけ、候補の通常式エラー値にする。
// 変数未宣言や型違いはこの仕組みで偽装しない。製品の入力APIに含めない。
public sealed record ConditionTestHooks(Action<int>? StepEntered = null, string? FaultedVariable = null);
