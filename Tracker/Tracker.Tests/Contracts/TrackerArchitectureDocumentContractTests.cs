namespace Tracker.Tests;

/// <summary>
/// Tracker の正本アーキテクチャ文書が、再構成後も実装判断を拘束する設計契約を保持していることを検証する。
/// </summary>
public class TrackerArchitectureDocumentContractTests
{
    /// <summary>
    /// 何を確認しているか: 参照実装から採用する考え方と採用しない構造が明示され、詳細設計へ辿れることを確認する。
    /// </summary>
    [Fact]
    public void ArchitectureDocument_StatesReferenceImplementationPolicyAndDetailedDesignLink()
    {
        var document = ReadArchitectureDocument();

        Assert.Contains("[Tracker Core engine 詳細設計](tracker-core-engine-detail-design.md)", document);
        Assert.Contains("### 2.2 参照実装の採否", document);
        Assert.Contains("採用する", document);
        Assert.Contains("採用しない", document);
    }

    /// <summary>
    /// 何を確認しているか: Tracker が直接扱う SSL-Vision proto 入力の境界が型名で明示されていることを確認する。
    /// </summary>
    [Fact]
    public void ArchitectureDocument_ListsCanonicalVisionInputProtoTypes()
    {
        var document = ReadArchitectureDocument();

        Assert.Contains("`SSL_WrapperPacket`", document);
        Assert.Contains("`SSL_DetectionFrame`", document);
        Assert.Contains("`SSL_DetectionBall`", document);
        Assert.Contains("`SSL_DetectionRobot`", document);
        Assert.Contains("`SSL_GeometryData`", document);
        Assert.Contains("`SSL_GeometryFieldSize`", document);
    }

    /// <summary>
    /// 何を確認しているか: profile 切替完了前に受信元設定を先行変更しない責務境界が文書に残っていることを確認する。
    /// </summary>
    [Fact]
    public void ArchitectureDocument_PreservesReceiverProfileSwitchBoundary()
    {
        var document = ReadArchitectureDocument();

        Assert.Contains("受信元設定は `ProfileSwitched` 後の observer 側で切り替える", document);
        Assert.Contains("切替完了前の old state 出力と new state 表示を混在させない", document);
    }

    /// <summary>
    /// 何を確認しているか: Kalman filter の設定値が covariance 更新と対応付けに使われる契約が詳細設計への委譲だけで失われていないことを確認する。
    /// </summary>
    [Fact]
    public void ArchitectureDocument_PreservesKalmanCovarianceContract()
    {
        var document = ReadArchitectureDocument();

        Assert.Contains("`ProcessNoise`", document);
        Assert.Contains("`MeasurementNoise`", document);
        Assert.Contains("covariance", document);
        Assert.Contains("`Gate`", document);
    }

    private static string ReadArchitectureDocument()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "Tracker",
                "Design",
                "Core",
                "tracker-architecture-plan.md");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            current = current.Parent;
        }

        throw new FileNotFoundException(
            "Tracker/Design/Core/tracker-architecture-plan.md was not found from the test output directory.");
    }
}
