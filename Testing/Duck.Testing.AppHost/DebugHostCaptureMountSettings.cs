using Aspire.Hosting.ApplicationModel;

namespace Duck.Testing.AppHost;

/// <summary>
/// DebugHost の一実行分の記録先または既存再生元と、コンテナ内の割当先を表します。
/// </summary>
public sealed class DebugHostCaptureMountSettings
{
    private DebugHostCaptureMountSettings(string hostDirectory, string containerDirectory, bool isReadOnly)
    {
        HostDirectory = hostDirectory;
        ContainerDirectory = containerDirectory;
        IsReadOnly = isReadOnly;
    }

    /// <summary>ホスト側で bind mount する絶対ディレクトリを取得します。</summary>
    public string HostDirectory { get; }

    /// <summary>コンテナ内で記録または再生に使う絶対ディレクトリを取得します。</summary>
    public string ContainerDirectory { get; }

    /// <summary>再生元として読み取り専用で割り当てるかどうかを取得します。</summary>
    public bool IsReadOnly { get; }

    /// <summary>
    /// 実行ID専用の新規Windows保存先を作成し、capture用の割当設定を返します。
    /// </summary>
    /// <param name="localAppDataDirectory">現在の利用者の LocalApplicationData の絶対パス。</param>
    /// <param name="runId">保存先の単一パス要素として使う実行ID。</param>
    /// <returns>`/data/captures` へ読み書き可能で割り当てる設定。</returns>
    /// <exception cref="ArgumentException">基準パスまたは実行IDが不正な場合。</exception>
    /// <exception cref="InvalidOperationException">実行IDの保存先が既に存在する場合。</exception>
    public static DebugHostCaptureMountSettings CreateCapture(string localAppDataDirectory, string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localAppDataDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        if (!Path.IsPathFullyQualified(localAppDataDirectory))
        {
            throw new ArgumentException("LocalApplicationData must be an absolute path.", nameof(localAppDataDirectory));
        }

        if (runId is "." or ".." || runId.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')))
        {
            throw new ArgumentException("RunId must be a single path segment using ASCII letters, digits, hyphen, or underscore.", nameof(runId));
        }

        var localAppDataRoot = Path.GetFullPath(localAppDataDirectory);
        var runDirectory = Path.Combine(localAppDataRoot, "Duck", "Aspire", runId);
        if (Directory.Exists(runDirectory) || File.Exists(runDirectory))
        {
            throw new InvalidOperationException("The capture directory for this RunId already exists and will not be reused.");
        }

        var captureDirectory = Path.Combine(runDirectory, "captures");
        Directory.CreateDirectory(captureDirectory);
        return new DebugHostCaptureMountSettings(captureDirectory, "/data/captures", isReadOnly: false);
    }

    /// <summary>
    /// 既存の絶対再生元だけを読み取り専用で割り当てる設定を返します。保存元は作成・変更しません。
    /// </summary>
    /// <param name="replayDirectoryPath">利用者が選択した既存の絶対ディレクトリ。</param>
    /// <returns>`/data/replay` へ読み取り専用で割り当てる設定。</returns>
    /// <exception cref="ArgumentException">パスが絶対パスでない場合。</exception>
    /// <exception cref="DirectoryNotFoundException">指定されたディレクトリが存在しない場合。</exception>
    public static DebugHostCaptureMountSettings CreateReplay(string replayDirectoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replayDirectoryPath);
        if (!Path.IsPathFullyQualified(replayDirectoryPath))
        {
            throw new ArgumentException("ReplayDirectoryPath must be an absolute path.", nameof(replayDirectoryPath));
        }

        var replayDirectory = Path.GetFullPath(replayDirectoryPath);
        if (!Directory.Exists(replayDirectory))
        {
            throw new DirectoryNotFoundException("The replay directory does not exist.");
        }

        return new DebugHostCaptureMountSettings(replayDirectory, "/data/replay", isReadOnly: true);
    }

    /// <summary>
    /// 保存先を資源へ割り当て、DebugHost の記録ディレクトリと初期記録状態を設定します。
    /// </summary>
    /// <param name="resource">割り当て先のDebugHostコンテナ資源。</param>
    /// <returns>bind mountと設定値を追加した資源。</returns>
    public IResourceBuilder<ContainerResource> ApplyTo(IResourceBuilder<ContainerResource> resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return resource
            .WithBindMount(HostDirectory, ContainerDirectory, IsReadOnly)
            .WithEnvironment("VisionReceiver__PacketCapture__DirectoryPath", ContainerDirectory)
            .WithEnvironment("VisionReceiver__PacketCapture__Enabled", "false");
    }
}
