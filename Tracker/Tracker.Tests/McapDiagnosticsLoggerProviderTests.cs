using Microsoft.Extensions.Logging;
using Tracker.Diagnostics.Mcap;

namespace Tracker.Tests;

public sealed class McapDiagnosticsLoggerProviderTests
{
    /// <summary>
    /// 何を確認しているか: 書き込み失敗を完了済み MCAP として公開せず、所有する未完了ファイルを削除すること。
    /// </summary>
    [Fact]
    public void LogWriteFailure_DeletesPartialFileAndRejectsCompletion()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"duck-mcap-failure-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var partialPath = Path.Combine(directory, "run.mcap.partial");
        var completedPath = Path.Combine(directory, "run.mcap");
        var originalExitCode = Environment.ExitCode;

        try
        {
            using var file = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            var failingStream = new FailingWriteStream(file);
            var provider = new McapDiagnosticsLoggerProvider(partialPath, completedPath, failingStream);
            var logger = provider.CreateLogger("Duck.Tests.Diagnostics");
            failingStream.FailWrites = true;

            logger.LogWarning("故障注入の診断記録");

            Assert.Throws<InvalidOperationException>(() => provider.Complete());
            Assert.False(File.Exists(partialPath));
            Assert.False(File.Exists(completedPath));
            provider.Dispose();
        }
        finally
        {
            Environment.ExitCode = originalExitCode;
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// 何を確認しているか: ホスト停止が異常なら writer を閉じても MCAP を完成名で公開しないこと。
    /// </summary>
    [Fact]
    public void Complete_WhenHostFailed_DeletesOwnedPartialWithoutPublishingCompletedFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"duck-mcap-host-failure-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var partialPath = Path.Combine(directory, "run.mcap.partial");
        var completedPath = Path.Combine(directory, "run.mcap");

        try
        {
            var output = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            using var provider = new McapDiagnosticsLoggerProvider(partialPath, completedPath, output);
            provider.CreateLogger("Duck.Tests.Diagnostics").LogInformation("異常停止前の記録");

            provider.Complete(hostSucceeded: false);

            Assert.False(File.Exists(partialPath));
            Assert.False(File.Exists(completedPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class FailingWriteStream(Stream inner) : Stream
    {
        public bool FailWrites { get; set; }

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }

        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count)
        {
            ThrowIfRequested();
            inner.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ThrowIfRequested();
            inner.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            ThrowIfRequested();
            inner.WriteByte(value);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private void ThrowIfRequested()
        {
            if (FailWrites)
            {
                throw new IOException("故障注入: 診断ファイルへの書き込みを拒否しました。");
            }
        }
    }
}
