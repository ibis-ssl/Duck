using Microsoft.AspNetCore.Builder;

namespace Duck.Testing.RefereeDriver.Hosting;

/// <summary>明示的に選択するHTTPホストの境界。既存の受信・制御ライブラリから依存を分離する。</summary>
internal static class RefereeDriverHost
{
    /// <summary>有効化とコンテナ指定に基づく待受選択。先行試験用の未実装宣言。</summary>
    internal static void ConfigureHttp(WebApplicationBuilder builder)
    {
    }

    /// <summary>自身診断とAPI照合を返す経路の登録。先行試験用の未実装宣言。</summary>
    internal static void MapHealth(WebApplication app)
    {
    }
}
