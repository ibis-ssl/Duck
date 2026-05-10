using System.Globalization;

/// <summary>
/// Capture replay の --expect / --detail-filter で使う整数 metric 条件。
/// </summary>
internal sealed record Condition(string Metric, ComparisonOperator Operator, int Expected)
{
    /// <summary>
    /// 実測 metric がこの条件を満たすか評価する。
    /// </summary>
    public bool Evaluate(int actual)
    {
        return Operator switch
        {
            ComparisonOperator.GreaterThanOrEqual => actual >= Expected,
            ComparisonOperator.LessThanOrEqual => actual <= Expected,
            ComparisonOperator.Equal => actual == Expected,
            ComparisonOperator.NotEqual => actual != Expected,
            ComparisonOperator.GreaterThan => actual > Expected,
            ComparisonOperator.LessThan => actual < Expected,
            _ => throw new InvalidOperationException($"Unsupported operator '{Operator}'."),
        };
    }

    public override string ToString()
    {
        return $"{Metric}{Operator.ToSymbol()}{Expected.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// CLI 文字列を metric 名の許可リストに照らして条件へ変換する。
    /// </summary>
    public static bool TryParse(
        string text,
        IReadOnlySet<string> allowedMetrics,
        out Condition condition,
        out string? error)
    {
        condition = new Condition("", ComparisonOperator.Equal, 0);
        error = null;

        foreach (var candidate in ComparisonOperatorExtensions.ParseOrder)
        {
            var symbol = candidate.ToSymbol();
            var index = text.IndexOf(symbol, StringComparison.Ordinal);
            if (index <= 0)
            {
                continue;
            }

            var metric = text[..index].Trim();
            var expectedText = text[(index + symbol.Length)..].Trim();
            if (!allowedMetrics.Contains(metric))
            {
                error = $"Unsupported metric '{metric}'.";
                return false;
            }

            if (!int.TryParse(expectedText, NumberStyles.None, CultureInfo.InvariantCulture, out var expected))
            {
                error = $"Condition '{text}' has an invalid integer value.";
                return false;
            }

            condition = new Condition(metric, candidate, expected);
            return true;
        }

        error = $"Condition '{text}' must contain one of: >=, <=, ==, !=, >, <.";
        return false;
    }
}

/// <summary>
/// Capture replay 条件式で使える比較演算子。
/// </summary>
internal enum ComparisonOperator
{
    GreaterThanOrEqual,
    LessThanOrEqual,
    Equal,
    NotEqual,
    GreaterThan,
    LessThan,
}

/// <summary>
/// 比較演算子の parse 順序と CLI 表示記号を提供する。
/// </summary>
internal static class ComparisonOperatorExtensions
{
    /// <summary>
    /// >= と > などの前方一致を壊さないための parse 順序。
    /// </summary>
    public static readonly IReadOnlyList<ComparisonOperator> ParseOrder =
    [
        ComparisonOperator.GreaterThanOrEqual,
        ComparisonOperator.LessThanOrEqual,
        ComparisonOperator.Equal,
        ComparisonOperator.NotEqual,
        ComparisonOperator.GreaterThan,
        ComparisonOperator.LessThan,
    ];

    /// <summary>
    /// CLI 条件式で使う比較演算子の記号へ変換する。
    /// </summary>
    public static string ToSymbol(this ComparisonOperator comparisonOperator)
    {
        return comparisonOperator switch
        {
            ComparisonOperator.GreaterThanOrEqual => ">=",
            ComparisonOperator.LessThanOrEqual => "<=",
            ComparisonOperator.Equal => "==",
            ComparisonOperator.NotEqual => "!=",
            ComparisonOperator.GreaterThan => ">",
            ComparisonOperator.LessThan => "<",
            _ => throw new InvalidOperationException($"Unsupported operator '{comparisonOperator}'."),
        };
    }
}
