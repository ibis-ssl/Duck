using Duck.Testing.Diagnostics;

namespace Issue81.IndependentSpecificationTests;

/// <summary>I81-02のサービス自身の復号件数と時刻を独立した入力で検証する。</summary>
public sealed class ServiceReceiveSpecificationTests
{
    private const string Input = "independent-service-input";

    /// <summary>実行・資源・完全コンテナIDが既知の診断対象を作る。</summary>
    private static ResourceDiagnosticIdentity Identity() => new("spec-run", "debug-host", new string('c', 64), 1);

    /// <summary>サービス公開APIに成功復号を一回ずつ記録する。</summary>
    private static void RecordTen(ServiceReceiveDiagnostics diagnostics, ServiceReceiveDiagnostics.ReceiveEpoch epoch)
    {
        for (int i = 0; i < 10; i++)
            diagnostics.RecordSuccess(diagnostics.BeginReceive(epoch));
    }

    /// <summary>S01: 復号前の診断に準備完了の証拠はない。</summary>
    [Fact]
    public void S01_ReceiveStartAloneIsNotReady()
    {
        var diagnostics = new ServiceReceiveDiagnostics(Identity(), new IndependentClock());
        var epoch = diagnostics.BeginEpoch(Input);
        diagnostics.BeginReceive(epoch);
        Assert.All(diagnostics.GetSnapshots(Input), snapshot => Assert.False(snapshot.IsReady));
    }

    /// <summary>S02: 成功復号10件の件数・対象ID・時刻を診断へ結び付ける。</summary>
    [Fact]
    public void S02_SuccessfulDecodeExposesBoundEvidence()
    {
        var identity = Identity();
        var diagnostics = new ServiceReceiveDiagnostics(identity, new IndependentClock());
        var epoch = diagnostics.BeginEpoch(Input);
        RecordTen(diagnostics, epoch);
        var snapshot = Assert.Single(diagnostics.GetSnapshots(Input));
        Assert.Equal(identity, snapshot.Identity);
        Assert.Equal(Input, snapshot.InputId);
        Assert.Equal(10L, snapshot.SuccessfullyDecodedCount);
        Assert.Equal(0L, snapshot.DecodeFailureCount);
        Assert.Equal(DiagnosticDecodeStatus.Succeeded, snapshot.DecodeStatus);
        Assert.NotNull(snapshot.LastReceivedAt);
        Assert.NotNull(snapshot.DecodeAgeUpperBound);
        Assert.True(snapshot.IsReady);
    }

    /// <summary>S03: 復号失敗だけでは成功復号件数を増やさず、準備完了にしない。</summary>
    [Fact]
    public void S03_FailedDecodeCannotCountAsSuccess()
    {
        var diagnostics = new ServiceReceiveDiagnostics(Identity(), new IndependentClock());
        var epoch = diagnostics.BeginEpoch(Input);
        for (int i = 0; i < 10; i++)
            diagnostics.RecordFailure(diagnostics.BeginReceive(epoch));
        var snapshots = diagnostics.GetSnapshots(Input);
        Assert.NotEmpty(snapshots);
        Assert.All(snapshots, snapshot =>
        {
            Assert.Equal(0L, snapshot.SuccessfullyDecodedCount);
            Assert.False(snapshot.IsReady);
        });
        Assert.Equal(10L, snapshots.Sum(snapshot => snapshot.DecodeFailureCount));
    }

    /// <summary>S04: 準備完了後の復号失敗を公開診断へ反映し、準備を解除する。</summary>
    [Fact]
    public void S04_FailureRevokesPriorReadyEvidence()
    {
        var diagnostics = new ServiceReceiveDiagnostics(Identity(), new IndependentClock());
        var epoch = diagnostics.BeginEpoch(Input);
        RecordTen(diagnostics, epoch);
        Assert.True(Assert.Single(diagnostics.GetSnapshots(Input)).IsReady);
        diagnostics.RecordFailure(diagnostics.BeginReceive(epoch));
        var snapshot = Assert.Single(diagnostics.GetSnapshots(Input));
        Assert.Equal(10L, snapshot.SuccessfullyDecodedCount);
        Assert.Equal(1L, snapshot.DecodeFailureCount);
        Assert.Equal(DiagnosticDecodeStatus.Failed, snapshot.DecodeStatus);
        Assert.False(snapshot.IsReady);
    }

    /// <summary>S05: 有限な鮮度窓を越えて受信が止まった場合は診断上の準備を解除する。</summary>
    [Fact]
    public void S05_DiagnosticsExpireWithoutNewDecode()
    {
        var clock = new IndependentClock();
        var diagnostics = new ServiceReceiveDiagnostics(Identity(), clock);
        var epoch = diagnostics.BeginEpoch(Input);
        RecordTen(diagnostics, epoch);
        Assert.True(Assert.Single(diagnostics.GetSnapshots(Input)).IsReady);
        Assert.True(ServiceReceiveDiagnostics.FreshnessWindow > TimeSpan.Zero);
        clock.Advance(ServiceReceiveDiagnostics.FreshnessWindow + TimeSpan.FromSeconds(1));
        Assert.False(Assert.Single(diagnostics.GetSnapshots(Input)).IsReady);
    }

    /// <summary>S06: 対象IDを持たない診断は復号件数だけで資源の準備を証明できない。</summary>
    [Fact]
    public void S06_UnboundDiagnosticsCannotEstablishResourceReadiness()
    {
        var diagnostics = new ServiceReceiveDiagnostics(null, new IndependentClock());
        var epoch = diagnostics.BeginEpoch(Input);
        RecordTen(diagnostics, epoch);
        Assert.All(diagnostics.GetSnapshots(Input), snapshot => Assert.False(snapshot.IsReady));
    }

    /// <summary>S07: 三つの追跡送信元について件数・名称・UUID・接続元の対応を保持する。</summary>
    [Fact]
    public void S07_SourceEvidenceRemainsSeparatelyIdentifiable()
    {
        var diagnostics = new ServiceReceiveDiagnostics(Identity(), new IndependentClock());
        var epoch = diagnostics.BeginEpoch(Input);
        var sources = new[]
        {
            (Key: "source-a", Name: "ibis", Uuid: "uuid-a", Endpoint: "192.0.2.1:11010"),
            (Key: "source-b", Name: "TIGERs", Uuid: "uuid-b", Endpoint: "192.0.2.2:11010"),
            (Key: "source-c", Name: "ER-FORCE", Uuid: "uuid-c", Endpoint: "192.0.2.3:11010"),
        };
        foreach (var source in sources)
            for (int i = 0; i < 10; i++)
                diagnostics.RecordSuccess(diagnostics.BeginReceive(epoch),
                    source.Key, source.Name, source.Uuid, source.Endpoint);
        var snapshots = diagnostics.GetSnapshots(Input);
        foreach (var source in sources)
        {
            var snapshot = Assert.Single(snapshots.Where(item => item.SourceKey == source.Key));
            Assert.Equal(source.Name, snapshot.SourceName);
            Assert.Equal(source.Uuid, snapshot.SourceUuid);
            Assert.Equal(source.Endpoint, snapshot.RemoteEndpoint);
            Assert.Equal(10L, snapshot.SuccessfullyDecodedCount);
        }
    }
    /// <summary>S08: 古い受信を遅れて復号しても、受信時刻を現在へ更新して準備証拠にしない。</summary>
    [Fact]
    public void S08_DelayedDecodeDoesNotMakeOldReceptionFresh()
    {
        var clock = new IndependentClock();
        var diagnostics = new ServiceReceiveDiagnostics(Identity(), clock);
        var epoch = diagnostics.BeginEpoch(Input);
        var receivedAt = clock.GetUtcNow();
        var attempts = Enumerable.Range(0, 10).Select(_ => diagnostics.BeginReceive(epoch)).ToArray();
        clock.Advance(ServiceReceiveDiagnostics.FreshnessWindow + TimeSpan.FromSeconds(1));
        foreach (var attempt in attempts) diagnostics.RecordSuccess(attempt);
        var snapshot = Assert.Single(diagnostics.GetSnapshots(Input));
        Assert.Equal(receivedAt, snapshot.LastReceivedAt);
        Assert.False(snapshot.IsReady);
    }

    /// <summary>S09: 一送信元の更新を別送信元の鮮度証拠に使わない。</summary>
    [Fact]
    public void S09_OneSourceCannotRefreshAnotherSource()
    {
        var clock = new IndependentClock();
        var diagnostics = new ServiceReceiveDiagnostics(Identity(), clock);
        var epoch = diagnostics.BeginEpoch(Input);
        foreach (var key in new[] { "source-a", "source-b" })
            for (int i = 0; i < 10; i++)
                diagnostics.RecordSuccess(diagnostics.BeginReceive(epoch), key);
        clock.Advance(ServiceReceiveDiagnostics.FreshnessWindow + TimeSpan.FromSeconds(1));
        for (int i = 0; i < 10; i++)
            diagnostics.RecordSuccess(diagnostics.BeginReceive(epoch), "source-a");
        var snapshots = diagnostics.GetSnapshots(Input);
        Assert.True(Assert.Single(snapshots.Where(item => item.SourceKey == "source-a")).IsReady);
        Assert.False(Assert.Single(snapshots.Where(item => item.SourceKey == "source-b")).IsReady);
    }
}
