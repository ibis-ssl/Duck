# Sub-agent実行レポート

## タスク

- 目的: Issue #10 設計書で、説明なしでは意味が分かりにくい英語概念語を監査し、設計意図を変えずに本文の日本語化と用語説明追加を行う。
- タスク種別: 設計書監査および設計書修正。

## sub-agentを使う理由

- 理由: ユーザー指定の gpt-5.5 high 相当の独立した設計書監査として、Issue #10 の用語観点だけを既存実装作業から切り離して確認するため。

## 対象範囲

- 対象: `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md` を主対象に、`Tracker/Tracker.Server/Design/tasks-status.md` と `Tracker/Tracker.Server/Design/phases-status.md` も検索対象として確認した。

## 対象外

- 対象外: 製品コード、テストコード、PR 本文、`Tracker/Tracker.Server/appsettings.json`、進捗状態の変更、Issue #10 の時間同期方針変更。

## 実行コマンド

- 実行コマンド: `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-doc-maintainer/SKILL.md`、`sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-executor/SKILL.md`、`sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/codex-delegation-executor/SKILL.md`、`sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`、`sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`、`sed -n '1,240p' reports/issue-10-design-terminology-audit-20260514100412.md`、`sed -n '1,240p' Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`、`sed -n '241,520p' Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`、`sed -n '1,220p' Tracker/Tracker.Server/Design/tasks-status.md`、`sed -n '1,220p' Tracker/Tracker.Server/Design/phases-status.md`、`rg -n "alignment record|selected tick|selected replay timeline tick|metadata|sidecar|fallback|hold|tick|comparison|Field source frame" Tracker/Tracker.Server/Design`、`git diff --check`。
- 検証結果: `git diff --check` は出力なし。指定語句検索では、主対象設計書に残る英語概念語は用語欄で説明済み、またはコード識別子・UI ラベル・固定ログ値に紐づく表現として残した。進捗管理ファイル側の一致は過去の進捗記録と完了済みタスク説明であり、今回の進捗状態を変えない方針に従い本文修正対象から外した。

## 対象ファイル

- 変更または確認したファイル: 変更は `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md` と本レポート。確認のみは `Tracker/Tracker.Server/Design/tasks-status.md`、`Tracker/Tracker.Server/Design/phases-status.md`。

## 指摘事項

- 指摘要約または「指摘なし」: `alignment record`、`selected tick`、`selected replay timeline tick`、`metadata`、`sidecar`、`fallback`、`hold`、`comparison`、`Field source frame` は説明なしでは意味が分かりにくい概念語として扱い、主対象設計書の用語欄へ説明を追加した。本文中の `source` は表示元、`selected tick` は選択 tick、`fallback` は代替使用、`hold` は保持のように、日本語中心の表現へ置き換えた。`UI render tick`、`ReplayTimelineIndex`、`CandidateMissing`、`NoCandidateSnapshot`、`Raw Aggregate`、`Raw Camera`、`Tracked`、`3rd party tracker` などはコード識別子・UI ラベル・固定概念として残し、用語欄で意味を補った。

## 結果

- 結果: Issue #10 の設計意図は維持した。selected replay timeline tick を固定し、同じ表示元の latest-before snapshot を使い、future / later snapshot へ fallback しない方針は変更していない。進捗状態は変更していない。

## リスク

- 未解決のリスクまたは後続対応: 進捗管理ファイルには過去の進捗記録として英語概念語が残っている。今回の指示では進捗状態変更を避ける必要があるため未修正としたが、将来、進捗管理文言全体を日本語化する専用作業があれば別途整理できる。
