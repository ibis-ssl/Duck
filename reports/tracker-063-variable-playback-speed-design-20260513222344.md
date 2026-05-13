# Sub-agent実行レポート

## タスク

- 目的: TRACKER-063 の playback start 速度選択維持と可変早送り倍率の最小設計を具体化する。
- タスク種別: design

## sub-agentを使う理由

- 理由: ユーザー指定により設計は gpt-5.5 high sub-agent が担当する。親は manager として tracking と gate を管理する。

## 対象範囲

- 対象: `/diagnostics` playback controls の速度選択 contract、再生開始時の speed/mode 遷移、64x 超の可変早送り倍率、timer interval / normalization による実効速度 cap。

## 対象外

- 対象外: saved alignment v2、scrub、Field source、comparison、overlay、tracker capture schema の変更。既存の exact comparison / 任意 tick 比較経路を壊す変更。

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-executor/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `git status --short`
  - `rg -n "TRACKER-059|TRACKER-060|TRACKER-063|FastForward|DiagnosticsPlaybackState|saved alignment" /home/ibis/.codex/memories/MEMORY.md`
  - `rg -n "Playback|再生|FastForward|早送り|speed|Speed|倍率|DiagnosticsPlaybackState|TRACKER-063|TRACKER-060|TRACKER-059" Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md Tracker/Tracker.Server/README.md Tracker/Tracker.Core/Design/tasks-status.md Tracker/Tracker.Core/Design/phases-status.md reports/tracker-063-variable-playback-speed-design-20260513222344.md`
  - `sed -n '1,260p' Tracker/Tracker.Server/Components/Pages/DiagnosticsPlaybackState.cs`
  - `sed -n '1,620p' Tracker/Tracker.Tests/DiagnosticsPlaybackStateTests.cs`
  - `rg -n "diagnostics-playback|PlaybackSpeed|StartPlayback|FastForward|StopPlayback|OnPlaybackSpeed|selectedPlayback|fastForwardSpeed|speedMultiplier" Tracker/Tracker.Server/Components/Pages/Diagnostics.razor Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
  - `sed -n '120,155p' Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
  - `sed -n '210,225p' Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md && sed -n '274,284p' Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
  - `sed -n '88,98p' Tracker/Tracker.Server/README.md`
  - `git diff --check -- Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md Tracker/Tracker.Server/README.md reports/tracker-063-variable-playback-speed-design-20260513222344.md`

## 対象ファイル

- 変更または確認したファイル:
  - 変更: `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
  - 変更: `Tracker/Tracker.Server/README.md`
  - 変更: `reports/tracker-063-variable-playback-speed-design-20260513222344.md`
  - 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsPlaybackState.cs`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
  - 確認: `Tracker/Tracker.Tests/DiagnosticsPlaybackStateTests.cs`
  - 確認のみ: `Tracker/Tracker.Core/Design/tasks-status.md`
  - 確認のみ: `Tracker/Tracker.Core/Design/phases-status.md`

## 指摘事項

- 指摘要約または「指摘なし」:
  - 現実装では `StartPlaybackAsync(DiagnosticsPlaybackMode.Play)` が `selectedPlaybackSpeedLabel = DiagnosticsPlaybackState.NormalPlaybackSpeedLabel` を実行するため、fast speed 選択中に Play button を押すと選択 speed が `等倍速` へ戻る。
  - 現実装の `NormalizeSpeedMultiplier` は `FastForwardSpeedMultipliers` の固定 list membership だけを許可し、未登録値を `DefaultFastForwardSpeedMultiplier` へ戻すため、64x 超の可変倍率を state contract で保持できない。
  - 現実装の `FastForwardMinimumInterval = 30ms` は `GetInterval` の fast path で hard floor として使われるため、代表的な 1.6s delta では `64x` 以上が同じ 30ms interval に潰れ、64x 超の実効速度が上がらない。
  - `GetNextIndex` は FastForward でも 1 replay timeline tick ずつ進めており、TRACKER-059 の非間引き contract 自体は設計上維持できる。64x 超対応は tick skip ではなく interval 計算と normalization の修正で扱う。

## 結果

- 結果:
  - `TRACKER-063` の設計として、Play / Fast Forward / Stop の従来 transport button 配置を維持しつつ、Play button を「現在選択中 speed で再生開始」と定義した。選択中 speed が `等倍速` の場合だけ `DiagnosticsPlaybackMode.Play` を開始し、fast multiplier 選択中は Play button 押下でも `DiagnosticsPlaybackMode.FastForward` とその multiplier で開始する。
  - 速度選択 UI は巨大な action button 群へ戻さず、scrubber 行の compact control として `等倍速` tab と可変 `早送り倍率` control を並べる方針にした。`4x` / `16x` / `64x` は固定 choice list ではなく preset shortcut とし、表示 label は `等倍速` または `${multiplier}x` とする。
  - fast multiplier は固定 `[4, 16, 64]` membership で normalize せず、初期目安 `2x` 以上 `1024x` 以下の範囲 clamp で扱う。既定 fast multiplier は `16x` を維持する。
  - FastForward interval は tick 非間引きを維持したまま `max(FastForwardTimerFloor, normalizedTimelineDelta / fastMultiplier)` とし、現行の `30ms` hard floor を実効速度 cap として残さない。代表的な 1.6s delta で `128x` / `256x` が `64x` より短い interval になり、64x 超の実効速度が上がることを acceptance に追加した。
  - saved alignment v2、timeline scrubber、Field source selector、`Tracker Comparison` panel、`Tracker.CaptureReplay` の任意 tick 比較経路は設計変更対象外として維持する方針を明記した。

## リスク

- 未解決のリスクまたは後続対応:
  - 非常に密な replay timeline では小さい timer floor によってそれ以上の高速化が頭打ちになる可能性がある。これは busy loop を避けるため許容するが、64x 超が全く効かない状態には戻さない regression test が必要。
  - Blazor Server / browser / OS timer の実測 granularity により、1ms floor が常に 1ms 実行になるとは限らない。実装では設計上の interval 計算 contract と、代表的 delta での実効短縮を focused test で固定する必要がある。
  - UI の可変倍率入力範囲と操作部品の最終形は実装時に compact 性を確認する必要がある。ただし大型 redesign、exact comparison / Field / overlay / capture schema の変更は不要。
