namespace Duck.Testing.Diagnostics;

public enum DiagnosticEvidenceKind
{
    Unknown,
    ServiceDecode,
    AuxiliaryObservation,
}

public enum DiagnosticDecodeStatus
{
    Unknown,
    Succeeded,
    Failed,
}

/// <summary>
/// 一つの資源の起動世代と入力に対応する、メモリ内の正規化済み観測値。
/// 外部サービスの応答データや通信形式は表さない。
/// </summary>
/// <param name="Identity">観測に対応する識別子。期待する所有者の識別子と照合する。</param>
/// <param name="InputId">呼出側の入力キー。同じ資源の他の入力件数と区別する。</param>
/// <param name="ObservationSequence">ローカルの収集側が取得開始前に割り当てる、正の単調増加番号。</param>
/// <param name="EvidenceKind">サービス自身の正常な復号で増えた件数かどうか。</param>
/// <param name="DecodeStatus">この観測が表す最新の復号結果。</param>
/// <param name="SuccessfullyDecodedCount">この識別子と入力における、非負の累積件数。</param>
/// <param name="DecodeAgeUpperBound">
/// Observe 呼出時点で、最後の正常な復号から経過した時間の根拠ある上限。取得の遅延も含める。
/// 経過時間を確定できない場合は null とする。状態の取得時刻、未検証の外部時刻、
/// パケット記録の時刻を、この値の代用にしてはならない。
/// </param>
public sealed record ResourceDiagnosticSnapshot(
    ResourceDiagnosticIdentity Identity,
    string InputId,
    long ObservationSequence,
    DiagnosticEvidenceKind EvidenceKind,
    DiagnosticDecodeStatus DecodeStatus,
    long SuccessfullyDecodedCount,
    TimeSpan? DecodeAgeUpperBound);
