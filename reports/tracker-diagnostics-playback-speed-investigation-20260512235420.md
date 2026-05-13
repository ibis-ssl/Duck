# Tracker diagnostics playback responsiveness investigation

## 目的

- `/diagnostics` の scrub 追従と playback が遅い原因を整理する。
- 結論は `sidecar reread per scrub/tick` と `playback interval too conservative` の二本立てでまとめる。

## 背景

- ユーザー報告: `/diagnostics` の再生がすごく遅い。
- 追加情報: scrub 追従だけでなく playback も遅い。
- 対象 session: `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Server/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260512T145134883Z-cf4b51408ed44743b2350520eb9eec6e`

## 調査結果

- 原因1: `sidecar reread per scrub/tick`。
  - `OnTimelineScrubbed` -> `SelectEntryByIndex` -> `SyncComparisonState` で、scrub 1回ごとに `TrackerDiagnosticsComparisonUiState.Load` が走る。
  - playback tick でも `RunPlaybackAsync` の callback が `SelectEntryByIndex(nextIndex)` を呼ぶため、scrub と同じ経路に入る。
  - `TrackerDiagnosticsComparisonViewStateReader.Load` は毎回 `TrackerSnapshotReplayReader.ReadSession(metadataPath)` を呼び、tracker snapshot sidecar を全件読み直す。
  - `TrackerSnapshotReplayReader.ReadSession` は metadata read、`tracker-packet-snapshots.jsonl` 全行 JSON parse、payload restore check、timestamp sort、diagnostics summary build を行う。
  - 指定 session の `tracker-packet-snapshots.jsonl` は 140,833,300 bytes / 29,043 lines。diagnostics log は 2,527,153 bytes / 1,045 lines。軽量な `jq` 抽出だけでも sidecar 全 JSON parse は約 1.26 sec。
  - render snapshot は log 選択時に index/cache され、scrub ごとは dictionary lookup なので主犯ではない。
- 原因2: `playback interval too conservative`。
  - `DiagnosticsPlaybackState.GetInterval` は Play では実 timestamp delta をそのまま使う。
  - Fast Forward は `FastForwardStep = 5` で5 entry先へ進み、interval は timestamp delta の 1/4、最小 30ms。
  - 指定 session の diagnostics timeline は 1,045 entries / 約 38.0 sec。現設計では Play は約 38.0 sec、Fast Forward でも約 9.5 sec 相当で、調査用途にはまだ遅い。
- 実行コマンド:
  - `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs | sed -n '136,180p'`
  - `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs | sed -n '360,446p'`
  - `nl -ba Tracker/Tracker.Server/Components/Pages/DiagnosticsPlaybackState.cs | sed -n '1,240p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs | sed -n '1,220p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs | sed -n '1,360p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerSnapshotReplayReader.cs | sed -n '1,220p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogReader.cs | sed -n '1,220p'`
  - `find .../ssl-vision-packets-20260512T145134883Z-cf4b51408ed44743b2350520eb9eec6e -maxdepth 1 -type f -printf '%s\t%p\n' | sort -nr`
  - `wc -l .../tracker-packet-snapshots.jsonl .../*.tracker-diagnostics.log`
  - `gzip -cd .../*.render-snapshots.jsonl.gz | wc -l`
  - `gzip -cd .../*.jsonl.gz | wc -l`
  - `/usr/bin/time -f 'elapsed=%e user=%U sys=%S maxrss_kb=%M' jq -c '{role:.SourceRole,label:.SourceLabel,frame:.TrackedFrameNumber,ts:.TrackedFrameTimestampNs,summary:.SemanticSummary,hasPayload:(.PayloadBase64 != null and .PayloadBase64 != "")}' .../tracker-packet-snapshots.jsonl > /dev/null`

## 推奨対応

- 1 taskでまとめる案を優先する。候補: `TRACKER-056`「diagnostics playback / scrub responsiveness を改善する」。
- 理由: scrub も playback tick も同じ `SelectEntryByIndex` -> `SyncComparisonState` を通るため、sidecar cache は両方に効く。cache だけでは Fast Forward 約9.5 sec 問題が残るため、speed control も同じ体感改善 task に含めるのが妥当。
- 実装内容:
  - comparison UI 用の lightweight sidecar index/cache を追加する。cache key は metadata / sidecar path、mtime、file size。
  - cache は full replay session ではなく、source role / label、tracked frame number、timestamp、raw payload restored、ball count、robot count など UI comparison に必要な projection に限定する。
  - scrub/tick 時は sidecar reread をせず、in-memory lookup で `SelectedEntryComparison` だけ再計算する。
  - nearest timestamp は全件 `OrderBy` ではなく、timestamp sort 済み配列への binary search で近傍だけ比較する。
  - Play の既定 1x は real timestamp delta 契約として維持する。
  - 調査用に `1x / 2x / 4x / 8x / 16x` などの playback speed selector を追加するか、Fast Forward を明示的に高速化する。ユーザーが速度を予測できるため selector 案を優先。
- 触る候補ファイル:
  - `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
  - 必要なら新規 `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonIndex.cs` または cache helper
  - `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs`
  - `Tracker/Tracker.Server/Components/Pages/DiagnosticsPlaybackState.cs`
  - `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
  - `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
  - `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`
  - `Tracker/Tracker.Tests/DiagnosticsPlaybackStateTests.cs`
- focused test 候補:
  - 同一 diagnostics log で selected entry を連続変更しても sidecar reader が1回しか呼ばれないこと。
  - sidecar の mtime / length が変わったら cache を破棄して読み直すこと。
  - missing / empty / corrupt / not-created / metadata-missing の既存 status contract が維持されること。
  - Play 既定が 1x real timestamp delta を維持すること。
  - selected speed が 4x / 8x / 16x の場合に Play interval が delta / speed になること。ただし 16ms などの下限は維持すること。
  - playback tick で entry selection が連続更新されても comparison cache が再利用されること。
  - focused command: `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" NUGET_HTTP_CACHE_PATH="$PWD/.codex-nuget-http-cache" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~TrackerDiagnosticsComparisonViewStateTests|FullyQualifiedName~DiagnosticsPlaybackStateTests" -m:1 /nr:false -p:NuGetAudit=false`

## リスク

- full payload 付き replay session をそのまま cache するとメモリ使用量が大きくなる。UI 用 lightweight projection に限定する必要がある。
- file 更新検知を path だけにすると CaptureOn 継続中や同名 file 更新時に stale cache になる。mtime / length を key に含める。
- `TrackerSnapshotReplayReader` は CLI / replay 用 contract でも使われるため、UI 追従改善では既存 replay reader の挙動を壊さず、comparison view-state reader 側に cache / index を足すのが安全。
- Play の既定速度を変更すると「通常再生は log timestamp delta に合わせる」既存契約を壊す。高速化は明示 speed selector または Fast Forward 側に閉じる。
- どうしても分割するなら `TRACKER-056` sidecar cache、`TRACKER-057` playback speed control が自然。ただし今回の体感問題は同じ `/diagnostics` timeline 操作なので、早期解消優先なら1 taskで扱える。
- 調査のみのため dotnet test は未実行。
