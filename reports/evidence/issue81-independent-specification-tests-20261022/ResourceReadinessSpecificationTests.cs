using Duck.Testing.Diagnostics;

namespace Issue81.IndependentSpecificationTests;

/// <summary>I81-01の対象識別、サービス復号、新しさ、失敗による準備解除を検証する。</summary>
public sealed class ResourceReadinessSpecificationTests
{
    private const string Input = "independent-vision";
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(5);

    /// <summary>設計が要求する実行・資源・完全コンテナIDを持つ独立fixtureを作る。</summary>
    private static ResourceDiagnosticIdentity Identity(long generation = 1) =>
        new("spec-run", "duck", new string('a', 64), generation);

    /// <summary>公開宣言だけを使って観測証跡を作る。</summary>
    private static ResourceDiagnosticSnapshot Evidence(
        ResourceDiagnosticIdentity identity, long sequence, long count,
        DiagnosticEvidenceKind kind = DiagnosticEvidenceKind.ServiceDecode,
        DiagnosticDecodeStatus status = DiagnosticDecodeStatus.Succeeded,
        TimeSpan? age = null, string input = Input) =>
        new(identity, input, sequence, kind, status, count, age ?? TimeSpan.Zero);

    /// <summary>新しい入力を10件復号した準備証跡を与える。これは一般状態の最小件数の規定ではない。</summary>
    private static void GiveFreshProgress(ResourceReadinessState state, ResourceDiagnosticIdentity identity)
    {
        state.Observe(Evidence(identity, 1, 0));
        for (int i = 1; i <= 10; i++)
            state.Observe(Evidence(identity, i + 1, i));
    }

    /// <summary>R01: 開始のみで受信・復号証跡がない状態を準備完了にしない。</summary>
    [Fact]
    public void R01_NoEvidenceIsNotReady()
    {
        var state = new ResourceReadinessState(Identity(), Input, Window, new IndependentClock());
        Assert.False(state.GetReadiness().IsReady);
    }

    /// <summary>R02: 同じ対象の新しいサービス復号証跡から準備完了を判断できる。</summary>
    [Fact]
    public void R02_FreshServiceProgressIsReady()
    {
        var identity = Identity();
        var state = new ResourceReadinessState(identity, Input, Window, new IndependentClock());
        GiveFreshProgress(state, identity);
        Assert.True(state.GetReadiness().IsReady);
    }

    /// <summary>R03: 別実行・別資源・別コンテナ・旧世代の証跡では対象の準備を証明できない。</summary>
    [Theory]
    [InlineData("run")]
    [InlineData("resource")]
    [InlineData("container")]
    [InlineData("generation")]
    public void R03_DifferentIdentityCannotEstablishReadiness(string changed)
    {
        var expected = Identity(2);
        var actual = new ResourceDiagnosticIdentity(
            changed == "run" ? "other-run" : expected.RunId,
            changed == "resource" ? "other-resource" : expected.ResourceId,
            changed == "container" ? new string('b', 64) : expected.ContainerId,
            changed == "generation" ? 1 : expected.Generation);
        var state = new ResourceReadinessState(expected, Input, Window, new IndependentClock());
        GiveFreshProgress(state, actual);
        Assert.False(state.GetReadiness().IsReady);
    }

    /// <summary>R04: 必要な入力以外の復号件数では当該入力の準備を証明できない。</summary>
    [Fact]
    public void R04_DifferentInputCannotEstablishReadiness()
    {
        var identity = Identity();
        var state = new ResourceReadinessState(identity, Input, Window, new IndependentClock());
        for (int i = 0; i <= 10; i++)
            state.Observe(Evidence(identity, i + 1, i, input: "other-input"));
        Assert.False(state.GetReadiness().IsReady);
    }

    /// <summary>R05: 独立観測器と種別不明の証跡をサービス自身の復号の代用にしない。</summary>
    [Theory]
    [InlineData(DiagnosticEvidenceKind.AuxiliaryObservation)]
    [InlineData(DiagnosticEvidenceKind.Unknown)]
    public void R05_NonServiceEvidenceCannotEstablishReadiness(DiagnosticEvidenceKind kind)
    {
        var identity = Identity();
        var state = new ResourceReadinessState(identity, Input, Window, new IndependentClock());
        for (int i = 0; i <= 10; i++)
            state.Observe(Evidence(identity, i + 1, i, kind));
        Assert.False(state.GetReadiness().IsReady);
    }

    /// <summary>R06: 失敗または成功未確認の復号は準備完了の証跡にできない。</summary>
    [Theory]
    [InlineData(DiagnosticDecodeStatus.Failed)]
    [InlineData(DiagnosticDecodeStatus.Unknown)]
    public void R06_UnsuccessfulDecodeIsNotReady(DiagnosticDecodeStatus status)
    {
        var identity = Identity();
        var state = new ResourceReadinessState(identity, Input, Window, new IndependentClock());
        for (int i = 0; i <= 10; i++)
            state.Observe(Evidence(identity, i + 1, i, status: status));
        Assert.False(state.GetReadiness().IsReady);
    }

    /// <summary>R07: 準備完了後の復号失敗で準備を解除する。</summary>
    [Fact]
    public void R07_DecodeFailureRevokesReadiness()
    {
        var identity = Identity();
        var state = new ResourceReadinessState(identity, Input, Window, new IndependentClock());
        GiveFreshProgress(state, identity);
        Assert.True(state.GetReadiness().IsReady);
        state.Observe(Evidence(identity, 12, 10, status: DiagnosticDecodeStatus.Failed));
        Assert.False(state.GetReadiness().IsReady);
    }

    /// <summary>R08: 設定した有限窓より十分古い証跡を準備完了に使わない。</summary>
    [Fact]
    public void R08_AlreadyExpiredEvidenceIsNotReady()
    {
        var identity = Identity();
        var state = new ResourceReadinessState(identity, Input, Window, new IndependentClock());
        for (int i = 0; i <= 10; i++)
            state.Observe(Evidence(identity, i + 1, i, age: Window + TimeSpan.FromSeconds(1)));
        Assert.False(state.GetReadiness().IsReady);
    }

    /// <summary>R09: 新規観測がなく期限を十分越えた場合は準備を解除する。</summary>
    [Fact]
    public void R09_ReadinessExpiresWithoutNewDecode()
    {
        var identity = Identity();
        var clock = new IndependentClock();
        var state = new ResourceReadinessState(identity, Input, Window, clock);
        GiveFreshProgress(state, identity);
        Assert.True(state.GetReadiness().IsReady);
        clock.Advance(Window + TimeSpan.FromSeconds(1));
        Assert.False(state.GetReadiness().IsReady);
    }

    /// <summary>R10: 新しさが不明または時間として無効な証跡から準備を推定しない。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R10_UnprovenAgeIsNotReady(bool negative)
    {
        var identity = Identity();
        var state = new ResourceReadinessState(identity, Input, Window, new IndependentClock());
        for (int i = 0; i <= 10; i++)
            state.Observe(new(identity, Input, i + 1,
                DiagnosticEvidenceKind.ServiceDecode, DiagnosticDecodeStatus.Succeeded,
                i, negative ? TimeSpan.FromSeconds(-1) : null));
        Assert.False(state.GetReadiness().IsReady);
    }

    /// <summary>R11: 対象の再作成時に旧証跡を解除し、新対象自身の証跡で準備を回復する。</summary>
    [Fact]
    public void R11_NewGenerationRequiresItsOwnEvidence()
    {
        var previous = Identity();
        var next = new ResourceDiagnosticIdentity(previous.RunId, previous.ResourceId, new string('b', 64), 2);
        var state = new ResourceReadinessState(previous, Input, Window, new IndependentClock());
        GiveFreshProgress(state, previous);
        Assert.True(state.GetReadiness().IsReady);
        state.AdvanceGeneration(next);
        Assert.False(state.GetReadiness().IsReady);
        state.Observe(Evidence(previous, 100, 100));
        Assert.False(state.GetReadiness().IsReady);
        GiveFreshProgress(state, next);
        Assert.True(state.GetReadiness().IsReady);
    }
    /// <summary>R12: 負値またはゼロの成功件数だけでは新しい検出の復号を証明できない。</summary>
    [Theory]
    [InlineData(-1L)]
    [InlineData(0L)]
    public void R12_NoSuccessfulDecodeCannotEstablishReadiness(long count)
    {
        var identity = Identity();
        var state = new ResourceReadinessState(identity, Input, Window, new IndependentClock());
        for (int i = 1; i <= 11; i++)
            state.Observe(Evidence(identity, i, count));
        Assert.False(state.GetReadiness().IsReady);
    }
}
