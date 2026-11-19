namespace Tracker.Performance;

/// <summary>
/// 昇順に並べた標本から求める最近傍順位法の分位点。標本がなければ各値は null になる。
/// </summary>
/// <param name="P50">50 パーセンタイル。</param>
/// <param name="P95">95 パーセンタイル。</param>
/// <param name="P99">99 パーセンタイル。</param>
/// <param name="Max">最大値。</param>
internal sealed record Quantiles(double? P50, double? P95, double? P99, double? Max)
{
    /// <summary>
    /// 標本を昇順に並べ、切り上げた順位を使って分位点を返す。呼び出し側は順序を保存できない。
    /// </summary>
    internal static Quantiles Calculate(List<double> samples)
    {
        samples.Sort();
        return new Quantiles(At(0.50), At(0.95), At(0.99), At(1.00));

        double? At(double fraction) =>
            samples.Count == 0 ? null : samples[(int)Math.Ceiling(fraction * samples.Count) - 1];
    }
}
