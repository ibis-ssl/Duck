using Duck.Testing.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Duck.Testing.RefereeDriver.Hosting;

/// <summary>明示的に選択するHTTPホストの境界。既存の受信・制御ライブラリから依存を分離する。</summary>
internal static class RefereeDriverHost
{
    /// <summary>明示有効化時だけ内部39110を選び、コンテナ未指定時はloopbackへ限定する。</summary>
    internal static void ConfigureHttp(WebApplicationBuilder builder)
    {
        if (!bool.TryParse(builder.Configuration["RefereeDriver:Enabled"], out var enabled) || !enabled)
            return;
        var container = bool.TryParse(builder.Configuration["Testing:ContainerMode"], out var selected) && selected;
        builder.WebHost.UseUrls(container ? "http://0.0.0.0:39110" : "http://127.0.0.1:39110");
    }

    /// <summary>明示有効化時だけ現在の自身診断とAPI照合を射影し、取得や受信の時刻を更新しない。</summary>
    internal static void MapHealth(WebApplication app)
    {
        if (!bool.TryParse(app.Configuration["RefereeDriver:Enabled"], out var enabled) || !enabled)
            return;
        app.MapGet("/health/referee", (RefereeApiMatcher matcher, ServiceReceiveDiagnostics diagnostics) =>
        {
            var match = matcher.GetCurrent();
            var diagnostic = match.Udp?.Diagnostic ?? diagnostics.GetSnapshots(RefereeReceiveService.InputId)[0];
            // 識別子は入力された帰属情報であり、この応答は実DCP所有の証明ではない。
            return Results.Json(new
            {
                isReady = match.IsReady,
                reason = match.Reason,
                diagnostic,
                api = match.Api,
            }, statusCode: match.IsReady ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
        });
    }
}
