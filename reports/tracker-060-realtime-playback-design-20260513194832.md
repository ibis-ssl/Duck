# Sub-agent実行レポート

## タスク

- 目的: TRACKER-060 の設計を具体化し、等倍速 `Play` を30fps相当の表示更新で実時間1xへ追従させる。ただし TRACKER-059 の saved alignment v2 / scrub / Field source / comparison による確実な比較経路は壊さない。
- タスク種別: design

## sub-agentを使う理由

- 理由: ユーザー指示により、設計は gpt-5.5 high sub-agent に任せる。

## 対象範囲

- 対象:
- `TRACKER-059` の saved alignment v2 / unified replay timeline を前提にした、等倍速 `Play` の realtime playback 設計。
- `Play` の30fps相当表示更新、wall-clock と selected tick `ReceivedAt` からの target capture-time 計算、latest-before replay timeline tick selection。
- 高頻度 tracker tick を保存・比較データとして保持しつつ、等倍速表示だけが中間 tick を表示スキップしうることの設計明文化。
- `Fast Forward` と Play 専用 realtime stepping の責務分離。
- TDD acceptance と README / tracking の設計記述更新。

## 対象外

- 対象外:
- C# production code / test code の実装。
- saved alignment schema の破壊的変更。
- 既存 scrub / Field source / comparison / `Tracker.CaptureReplay` の選択・比較能力低下。
- alignment v1 救済。
- browser manual evidence。
- 既存 dirty `Tracker/Tracker.Server/appsettings.json` の変更・revert・stage。

## 実行コマンド

- 実行コマンド:
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-executor/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,240p' reports/tracker-060-realtime-playback-design-20260513194832.md`
- `sed -n '1,260p' Tracker/Tracker.Core/Design/tasks-status.md`
- `sed -n '1,220p' Tracker/Tracker.Core/Design/phases-status.md`
- `sed -n '1,240p' reports/tracker-059-fastest-timeline-design-20260513175146.md`
- `rg -n "TRACKER-060|TRACKER-059|Play|Fast Forward|scrub|replay timeline|unified replay timeline|alignment|ReceivedAt|Field source|comparison|30fps|fps|wall-clock|wall clock" Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
- `rg -n "TRACKER-060|TRACKER-059|Play|Fast Forward|scrub|replay timeline|unified replay timeline|alignment|ReceivedAt|Field source|comparison|30fps|fps|wall-clock|wall clock" Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `rg -n "TRACKER-060|TRACKER-059|Play|Fast Forward|scrub|replay timeline|unified replay timeline|alignment|ReceivedAt|Field source|comparison|30fps|fps|wall-clock|wall clock" Tracker/Tracker.Server/README.md`
- `git status --short`
- `nl -ba Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md | sed -n '118,150p'`
- `nl -ba Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md | sed -n '196,220p'`
- `nl -ba Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md | sed -n '248,284p'`
- `nl -ba Tracker/Tracker.Core/Design/tracker-architecture-plan.md | sed -n '136,154p'`
- `nl -ba Tracker/Tracker.Core/Design/tracker-architecture-plan.md | sed -n '558,568p'`
- `nl -ba Tracker/Tracker.Server/README.md | sed -n '78,98p'`
- `nl -ba Tracker/Tracker.Server/README.md | sed -n '224,233p'`
- `git diff -- Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
- `git diff -- Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `git diff -- Tracker/Tracker.Server/README.md Tracker/Tracker.Core/Design/tasks-status.md Tracker/Tracker.Core/Design/phases-status.md`
- `git diff --check`

## 対象ファイル

- 変更または確認したファイル:
- 変更: `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
- 変更: `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- 変更: `Tracker/Tracker.Server/README.md`
- 変更: `Tracker/Tracker.Core/Design/tasks-status.md`
- 変更: `Tracker/Tracker.Core/Design/phases-status.md`
- 変更: `reports/tracker-060-realtime-playback-design-20260513194832.md`
- 確認: `reports/tracker-059-fastest-timeline-design-20260513175146.md`
- 確認: `Tracker/Tracker.Server/appsettings.json` は既存 dirty diff があることだけ確認し、変更していない。

## 指摘事項

- 指摘要約または「指摘なし」:
- 指摘なし。
- 設計上の注意点として、`Play` の30fps相当表示更新は保存済み replay timeline tick を減らす処理ではなく、表示更新時に選ぶ selected tick の進め方だけを変える。
- saved alignment v2 / scrub / Field source / comparison / `Tracker.CaptureReplay` は、任意 replay timeline tick を選択して比較できる経路として維持する必要がある。

## 結果

- 結果:
- `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md` に、等倍速 `Play` の realtime stepping を具体化した。
- 設計方針: Play 開始時に開始 wall-clock と selected replay timeline tick の `ReceivedAt` を保持し、約33.3msごとの30fps相当表示更新で `targetReceivedAt = startTick.ReceivedAt + (currentWallClock - startWallClock)` を計算する。UI は `ReceivedAt <= targetReceivedAt` の latest replay timeline tick へ直接追従する。
- 高頻度 tracker tick は saved alignment v2 / unified replay timeline / tracker packet snapshot / comparison data として保持し、等倍速 Play の表示だけが中間 tick をスキップしうると明記した。
- `Fast Forward` は Play 専用 realtime stepping から分離し、TRACKER-059 の調査用挙動である tick 間引きなし / capture-time delta multiplier を維持する設計にした。
- TDD acceptance に、200Hz tick / 30fps表示更新で開始から1秒後に約30個目の逐次 tick ではなく wall-clock 1秒相当 tick へ進むこと、Play が表示しなかった中間 tick でも scrub / Field source / comparison が任意 tick 比較を維持すること、Fast Forward が既存調査用挙動を維持することを追加した。
- `Tracker/Tracker.Core/Design/tracker-architecture-plan.md` に、replay timeline の architecture contract と diagnostics viewer contract として同じ方針を追記した。
- `Tracker/Tracker.Server/README.md` に、ユーザー向け挙動として等倍速 Play の30fps相当表示更新、中間 tick 表示スキップの可能性、report に残す確認項目を追記した。
- `tasks-status.md` / `phases-status.md` は、設計済み状態、設計 report path、Play/Fast Forward 分離、200Hz tick / 30fps acceptance を反映した。実装・review は未実施のまま保持した。
- `git diff --check` は pass。

## リスク

- 未解決のリスクまたは後続対応:
- 実装時に timer drift、browser scheduling delay、Blazor render latency があるため、Play の判定は「timer 回数」ではなく wall-clock から target capture-time を都度計算する必要がある。
- 30fps相当 interval は約33.3msを目標にするが、実 timer の粒度は環境依存。TDD では clock abstraction または deterministic time provider で確認する必要がある。
- 200Hz など高頻度 timeline では Play 表示上の selected tick が大きく飛ぶため、UI 上の selected time / index 表示と scrubber 位置が追従していることを実装時に確認する必要がある。
- `Tracker/Tracker.Server/appsettings.json` の既存 dirty diff はユーザー実行用ローカル設定の可能性があるため変更していない。
