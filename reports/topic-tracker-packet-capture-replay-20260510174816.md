# Tracker packet capture and replay report

## 目的

VisionReceiver が受信した SSL-Vision UDP datagram を、問題再現時に再利用できるよう gzip 圧縮した JSONL として保存する。
保存したデータは自動テストから読み戻し、TrackerEngine に再投入できる形式にする。

## 実装内容

- `VisionReceiver:PacketCapture` 設定を追加した。
  - `Enabled`
  - `DirectoryPath`
  - `FilePrefix`
  - `FlushEachPacket`
- `VisionReceiverService` の受信直後、protobuf decode 前の raw datagram bytes を保存するようにした。
  - decode 失敗の可能性がある packet も保存対象にできる。
- 保存形式は `*.jsonl.gz` とした。
  - 1 行 1 packet。
  - `schemaVersion`, `receivedAt`, `remoteEndpoint`, `payloadBase64` を保存する。
- `VisionPacketCaptureFile.ReadRecords(path)` で gzip JSONL を読み戻し、`VisionPacketCaptureRecord.ParsePacket()` で `SSL_WrapperPacket` に復元できるようにした。

## 自動テスト

- `VisionPacketCaptureTests.Capture_WhenEnabled_WritesCompressedReplayRecords`
  - capture が有効なときに gzip JSONL が作成されること。
  - 保存 payload が元の `SSL_WrapperPacket` bytes と一致すること。
  - 読み戻した payload を protobuf として parse できること。
- `VisionPacketCaptureTests.ReadRecords_CanReplayCapturedPacketsThroughTrackerEngine`
  - 保存済み capture を読み戻して TrackerEngine に再投入すること。
  - geometry packet と detection packet の順に再生し、field geometry と ball position が committed frame に反映されること。
- `VisionPacketCaptureTests.Capture_WhenDisabled_DoesNotCreateCaptureFile`
  - capture 無効時にファイルを作らないこと。
- `VisionReceiverConfigurationResolverTests.AppsettingsJson_ExposesPacketCaptureDefaults`
  - `appsettings.json` の既定値が bind 可能であること。

## 確認結果

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~VisionPacketCaptureTests|FullyQualifiedName~VisionReceiverConfigurationResolverTests"
```

結果: Passed 9 / Failed 0

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore
```

結果: Build succeeded, Warning 0, Error 0

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore
```

結果: Passed 112 / Failed 0
