using System.Globalization;
using System.Net;
using Aspire.Hosting.ApplicationModel;

namespace Duck.Testing.AppHost;

/// <summary>Compose のポート割り当て結果を Aspire のエンドポイントモデルへ反映します。</summary>
internal static class ComposeEndpointProjection
{
    /// <summary>Compose が割り当てたホストアドレスとポートを Aspire のエンドポイントへ反映します。</summary>
    /// <param name="endpoint">割り当て情報を設定する Aspire エンドポイントです。</param>
    /// <param name="address">Compose が返したホスト IP アドレスです。</param>
    /// <param name="port">ホスト側に割り当てられたポートです。</param>
    /// <exception cref="ArgumentNullException"><paramref name="endpoint"/> が <see langword="null"/> です。</exception>
    /// <exception cref="ArgumentException"><paramref name="address"/> が空白です。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="address"/> が IP アドレスでないか、<paramref name="port"/> がポート範囲外です。</exception>
    /// <exception cref="InvalidOperationException">エンドポイントにコンテナー側の対象ポートが設定されていません。</exception>
    public static void SetAllocatedPort(EndpointAnnotation endpoint, string address, int port)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        if (!IPAddress.TryParse(address, out _) || port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "Compose must return a valid IP address and host port.");
        }

        var targetPort = endpoint.TargetPort ?? throw new InvalidOperationException(
            $"Endpoint '{endpoint.Name}' must declare its container target port before Compose allocation.");
        endpoint.AllocatedEndpoint = new AllocatedEndpoint(
            endpoint,
            address,
            port,
            targetPort.ToString(CultureInfo.InvariantCulture));
    }
}
