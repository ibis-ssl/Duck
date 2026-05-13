# TRACKER-055 playback / scrub performance implementation

## タスク

- ID: TRACKER-055
- Title: diagnostics playback / scrubber の低速問題を解消する

## 実装担当

- sub-agent: gpt-5.5 high implementation sub-agent
- model: gpt-5.5 high

## 対象範囲

- `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
- `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs`
- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
- `Tracker/Tracker.Server/Components/Pages/DiagnosticsPlaybackState.cs`
- `Tracker/Tracker.Tests/DiagnosticsPlaybackStateTests.cs`
- `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`
- 必要な design / README / tracking 更新

## 対象外

- Field 左右 source 切替と `Tracker Comparison` 折り畳みは `TRACKER-056`
- Field 重ね合わせ表示は `TRACKER-057`
- DB 化や snapshot sidecar format 変更

## 実行内容

- 必須 Skill と設計参照を確認した。
  - `/home/ibis/AI/CodexSkill/skills/implementation-executor/SKILL.md`
  - `/home/ibis/AI/CodexSkill/skills/tdd-executor/SKILL.md`
  - `/home/ibis/AI/CodexSkill/skills/design-executor/SKILL.md`
  - `/home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- TDD red:
  - `TrackerDiagnosticsComparisonViewStateTests` に同一 file state の連続 `Load` で sidecar index build が 1 回だけになる test と、sidecar file state 変更で rebuild する test を追加した。
  - `DiagnosticsPlaybackStateTests` に Fast Forward `4x` / `16x` / `64x`、Play 1x 維持、speed 変更前 tick の stale guard を固定する test を追加した。
  - 実装前 focused test は compile failure。
    - `DiagnosticsPlaybackState.GetNextIndex` / `GetInterval` / `ShouldApplyTick` に speed overload がない。
    - `TrackerDiagnosticsComparisonViewStateReader` に test 用 sidecar reader seam がない。
- Green 実装:
  - `TrackerDiagnosticsComparisonViewStateReader` に diagnostics log / metadata / sidecar の full path、存在有無、last write time UTC ticks、length を key にした lightweight index cache を追加した。
  - index は raw payload 文字列や full replay session を保持せず、source role / label、tracked frame number、tracked timestamp、raw payload restored flag、ball count、robot count の projection だけを保持する。
  - selected entry 変更時は cached index から source options と nearest timestamp comparison を作る。All filter は non-own があれば non-own を優先する既存互換を維持した。
  - nearest lookup は timestamp sorted bucket の binary search で前後 candidate だけを比較する。
  - `SemanticSummary` がある通常 record では全 record の protobuf parse を避け、古い / 手書き record で summary がない場合だけ payload fallback を行う。
  - cache は singleton reader 内で thread-safe に扱い、LRU で 2 index まで保持する。
  - `DiagnosticsPlaybackState` に Fast Forward speed multiplier `4x` / `16x` / `64x`、step 計算、interval 計算、speed-aware stale tick guard を追加した。Play は speed selector に関係なく実 timestamp delta のまま。
  - `/diagnostics` の Fast Forward button 近くに speed selector を追加し、Fast Forward 中の speed 変更では playback を新 speed で再開始する。
  - design doc、README、tracking を実装内容に合わせて最小更新した。
- 実データ evidence:
  - 指定 session: `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Server/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260512T145134883Z-cf4b51408ed44743b2350520eb9eec6e`
  - `tracker-packet-snapshots.jsonl`: 140,833,300 bytes / 29,043 lines
  - `*.tracker-diagnostics.log`: 2,527,153 bytes / 1,045 lines

## 検証

- Red:
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" NUGET_HTTP_CACHE_PATH="$PWD/.codex-nuget-http-cache" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~TrackerDiagnosticsComparisonViewStateTests|FullyQualifiedName~DiagnosticsPlaybackStateTests" -m:1 /nr:false -p:NuGetAudit=false`
  - 結果: failed as expected。新規 API / constructor 未実装による compile failure。
- Green:
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" NUGET_HTTP_CACHE_PATH="$PWD/.codex-nuget-http-cache" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~TrackerDiagnosticsComparisonViewStateTests|FullyQualifiedName~DiagnosticsPlaybackStateTests" -m:1 /nr:false -p:NuGetAudit=false`
  - 結果: Passed。31 passed、0 failed。
  - 既知 warning: `Tracker.CaptureReplay` restore/audit path が `/home/ibis/.local/share/NuGet/http-cache/.../vuln_index.dat-new` の read-only warning を出したが、test 実行自体は成功した。
- `git diff --check`
  - 結果: passed。

## 結果

- `/diagnostics` の scrub / playback tick は、同一 diagnostics log / metadata / sidecar file state では tracker snapshot sidecar JSONL を再読込せず、cached lightweight index から comparison を更新する。
- sidecar length または mtime が変わると cache key が変わり、次回 load で index を再構築する。
- 通常 Play は既存どおり diagnostics entry の実 timestamp delta を使う。
- Fast Forward は compact selector で `4x` / `16x` / `64x` を選べる。既定は `16x`。
- Stop、末尾到達時の先頭戻り、mode / cancellation / speed の stale tick guard は維持した。
- commit / push / PR 操作は行っていない。

## リスク

- 初回 log 選択時の index build は sidecar size に比例する。今回の実装は tick / scrub の再読込解消を優先し、background preload / cancellation は入れていない。
- cache は lightweight projection のみで bounded LRU だが、同時に複数巨大 session を頻繁に切り替える場合は初回 build が再発する。
- `SemanticSummary` がない古い record では fallback のため payload parse が発生する。通常 writer が作る record では summary がある前提。
- NuGet audit warning が sandbox の read-only home cache を参照して出ている。focused test は pass しているが、必要なら環境側 cache path の追加固定が別途必要。

## Review follow-up

- 対象 review: `reports/tracker-055-review-20260513003935.md`
- 対象 finding: `ComparisonSnapshotIndex.FindNearest` が target より小さい同一 timestamp 群の末尾 record を選び、旧実装の `TrackedFrameTimestampNs`, `ReceivedAt` 昇順先頭 tie-break と異なる問題。
- Red:
  - `TrackerDiagnosticsComparisonViewStateTests.Load_WhenNearestTimestampHasDuplicates_UsesEarliestReceivedAtCandidate` を追加した。
  - 修正前は `Expected: 9502 / Actual: 9501` で失敗し、同一 timestamp 群の末尾 record を選ぶ regression を再現した。
- Green:
  - `FindNearest` で previous candidate を比較する際、`insertionIndex - 1` を直接使わず、その timestamp 範囲の先頭 index を lower-bound で取得してから比較するよう修正した。
  - 同距離の場合に小さい timestamp を優先する既存 tie-break は維持した。
- 検証:
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "TrackerDiagnosticsComparisonViewStateTests" -m:1 /nr:false`
    - 結果: Passed。13 passed、0 failed。
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "TrackerDiagnosticsComparisonViewStateTests|DiagnosticsPlaybackStateTests" -m:1 /nr:false`
    - 結果: Passed。32 passed、0 failed。
  - `git diff --check`
    - 結果: passed。
- 追加リスク:
  - 今回は blocking finding のみを修正した。TRACKER-056以降のUI改善、cache構造の再設計、review report自体の書き換えは行っていない。
