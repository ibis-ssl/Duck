using Tracker.DebugHost.Vision;

namespace Tracker.Tests;

public class VisionFieldProjectionTests
{
    /// <summary>
    /// 何を確認しているか: field 原点が viewport 中央へ射影されること。
    /// </summary>
    [Fact]
    public void Project_WithOrigin_ReturnsViewportCenter()
    {
        var projection = VisionFieldProjection.FromGeometry(null);

        var point = projection.Project(0, 0);

        Assert.Equal(projection.ViewBoxWidth / 2, point.X, precision: 3);
        Assert.Equal(projection.ViewBoxHeight / 2, point.Y, precision: 3);
    }

    /// <summary>
    /// 何を確認しているか: geometry の field 端点が viewport 内に収まること。
    /// </summary>
    [Fact]
    public void Project_WithGeometryFieldEdges_KeepsEndpointsInsideViewport()
    {
        var geometry = new SSL_GeometryData
        {
            Field = new SSL_GeometryFieldSize
            {
                FieldLength = 12000,
                FieldWidth = 9000,
            },
        };
        var projection = VisionFieldProjection.FromGeometry(geometry);

        var left = projection.Project(-6000, 0);
        var right = projection.Project(6000, 0);
        var top = projection.Project(0, 4500);
        var bottom = projection.Project(0, -4500);

        Assert.InRange(left.X, 0, projection.ViewBoxWidth);
        Assert.InRange(right.X, 0, projection.ViewBoxWidth);
        Assert.InRange(top.Y, 0, projection.ViewBoxHeight);
        Assert.InRange(bottom.Y, 0, projection.ViewBoxHeight);
    }

    /// <summary>
    /// 何を確認しているか: geometry が未取得の場合に既定 field size で projection を作ること。
    /// </summary>
    [Fact]
    public void FromGeometry_WhenGeometryIsMissing_UsesDefaultFieldSize()
    {
        var projection = VisionFieldProjection.FromGeometry(null);

        Assert.Equal(VisionFieldProjection.DefaultFieldLength, projection.FieldLength);
        Assert.Equal(VisionFieldProjection.DefaultFieldWidth, projection.FieldWidth);
    }
}
