using Tracker.DebugHost.Tracking;

namespace Issue81.IndependentSpecificationTests;

/// <summary>承認比較設計136-150行を公開resolverだけで具体化する追加仕様試験。</summary>
public sealed class ComparisonResolverAdditionalSpecificationTests
{
    /// <summary>C01: 三つの正式source nameがそれぞれ指定されたlogical roleへ解決される。</summary>
    [Theory]
    [InlineData("ibis", "Duck")]
    [InlineData("TIGERs", "TIGERs")]
    [InlineData("ER-FORCE", "ER-Force")]
    public void C01_ExactNamesResolveToDeclaredRoles(string name, string expected)
    {
        var result = TrackerComparisonSourceResolver.Resolve(name, "unrelated-label", "uuid-a", "192.0.2.1:11010", "external");
        Assert.Equal(expected, result.LogicalRole);
    }

    /// <summary>C02: 大文字小文字・余白・未定義名・空値を既知roleへ推測で割り当てない。</summary>
    [Theory]
    [InlineData("IBIS")]
    [InlineData("tigers")]
    [InlineData("ER-Force")]
    [InlineData(" ibis")]
    [InlineData("ibis ")]
    [InlineData("debug-host-observer")]
    [InlineData("")]
    [InlineData(null)]
    public void C02_NonExactNamesDoNotResolve(string? name)
    {
        var result = TrackerComparisonSourceResolver.Resolve(name, "unrelated-label", "uuid-a", "192.0.2.1:11010", "external");
        Assert.Null(result.LogicalRole);
    }

    /// <summary>C03: 表示labelに既知名があってもsource nameが未定義ならlogical roleを作らない。</summary>
    [Theory]
    [InlineData("ibis")]
    [InlineData("TIGERs")]
    [InlineData("ER-FORCE")]
    public void C03_LabelDoesNotSubstituteForSourceName(string label)
    {
        var result = TrackerComparisonSourceResolver.Resolve("undefined-source", label, "uuid-a", "192.0.2.1:11010", "external");
        Assert.Null(result.LogicalRole);
    }

    /// <summary>C04: 既存SourceRoleと比較logical roleを独立した概念として扱う。</summary>
    [Theory]
    [InlineData("own")]
    [InlineData("external")]
    [InlineData("unknown")]
    public void C04_ExistingSourceRoleDoesNotChangeLogicalRole(string sourceRole)
    {
        var sources = new[] { (Name: "ibis", Role: "Duck"), (Name: "TIGERs", Role: "TIGERs"), (Name: "ER-FORCE", Role: "ER-Force") };
        foreach (var source in sources)
        {
            var result = TrackerComparisonSourceResolver.Resolve(source.Name, "label", "uuid-a", "192.0.2.1:11010", sourceRole);
            Assert.Equal(source.Role, result.LogicalRole);
        }
    }

    /// <summary>C05: 同じUUIDが複数endpointから届いても一つのcomparison identityへ解決される。</summary>
    [Theory]
    [InlineData("ibis")]
    [InlineData("TIGERs")]
    [InlineData("ER-FORCE")]
    public void C05_UuidHasPriorityOverRemoteEndpoint(string name)
    {
        var first = TrackerComparisonSourceResolver.Resolve(name, "label-a", "uuid-a", "192.0.2.1:11010", "external");
        var second = TrackerComparisonSourceResolver.Resolve(name, "label-b", "uuid-a", "192.0.2.2:22020", "external");
        Assert.False(string.IsNullOrEmpty(first.IdentityKey));
        Assert.Equal(first.IdentityKey, second.IdentityKey);
    }

    /// <summary>C06: 異なるUUIDを同じendpointや同じsource nameで一つに潰さない。</summary>
    [Fact]
    public void C06_DistinctUuidsRemainDistinct()
    {
        var first = TrackerComparisonSourceResolver.Resolve("ibis", "label", "uuid-a", "192.0.2.1:11010", "external");
        var second = TrackerComparisonSourceResolver.Resolve("ibis", "label", "uuid-b", "192.0.2.1:11010", "external");
        Assert.NotEqual(first.IdentityKey, second.IdentityKey);
    }

    /// <summary>C07: 同UUIDに別role名が現れてもendpointや名称で別identityへ分裂させない。</summary>
    [Fact]
    public void C07_SameUuidAcrossRoleNamesRemainsSameIdentity()
    {
        var duck = TrackerComparisonSourceResolver.Resolve("ibis", "label-a", "shared-uuid", "192.0.2.1:11010", "external");
        var tigers = TrackerComparisonSourceResolver.Resolve("TIGERs", "label-b", "shared-uuid", "192.0.2.2:11010", "external");
        Assert.Equal("Duck", duck.LogicalRole);
        Assert.Equal("TIGERs", tigers.LogicalRole);
        Assert.Equal(duck.IdentityKey, tigers.IdentityKey);
    }

    /// <summary>C08: UUIDがない場合は同じsource nameとendpointから同じ代用identityを得る。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void C08_MissingUuidUsesStableNameAndEndpointIdentity(string? uuid)
    {
        var first = TrackerComparisonSourceResolver.Resolve("ibis", "label-a", uuid, "192.0.2.1:11010", "external");
        var second = TrackerComparisonSourceResolver.Resolve("ibis", "label-b", uuid, "192.0.2.1:11010", "external");
        Assert.False(string.IsNullOrEmpty(first.IdentityKey));
        Assert.Equal(first.IdentityKey, second.IdentityKey);
    }

    /// <summary>C09: UUIDがない場合は異なるendpointの証跡を一つのsourceへ混ぜない。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void C09_FallbackIdentitySeparatesEndpoints(string? uuid)
    {
        var first = TrackerComparisonSourceResolver.Resolve("ibis", "label", uuid, "192.0.2.1:11010", "external");
        var second = TrackerComparisonSourceResolver.Resolve("ibis", "label", uuid, "192.0.2.2:11010", "external");
        Assert.NotEqual(first.IdentityKey, second.IdentityKey);
    }

    /// <summary>C10: UUIDがない場合は同じendpointでも異なるsource nameを区別する。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void C10_FallbackIdentitySeparatesSourceNames(string? uuid)
    {
        var first = TrackerComparisonSourceResolver.Resolve("ibis", "label", uuid, "192.0.2.1:11010", "external");
        var second = TrackerComparisonSourceResolver.Resolve("TIGERs", "label", uuid, "192.0.2.1:11010", "external");
        Assert.NotEqual(first.IdentityKey, second.IdentityKey);
    }

    /// <summary>C11: UUIDがある場合は既存SourceRoleをidentityの根拠へ混ぜない。</summary>
    [Fact]
    public void C11_UuidIdentityIsIndependentOfExistingSourceRole()
    {
        var first = TrackerComparisonSourceResolver.Resolve("ibis", "label", "uuid-a", "192.0.2.1:11010", "own");
        var second = TrackerComparisonSourceResolver.Resolve("ibis", "label", "uuid-a", "192.0.2.1:11010", "external");
        Assert.Equal(first.IdentityKey, second.IdentityKey);
    }
}
