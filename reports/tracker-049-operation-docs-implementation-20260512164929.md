# Sub-agent実行レポート

## タスク

`TRACKER-049` として CaptureOn 比較ログの運用ドキュメントと確認手順を整える。

## sub-agentを使う理由

ドキュメント実装・必要な調査・検証は sub-agent に委譲し、親エージェントは report を確認して裁定するため。

## 対象範囲

- `Tracker:Receive:Enabled`
- multicast endpoint
- CaptureOn session folder
- snapshot sidecar
- `Tracker.CaptureReplay` / diagnostics 確認方法
- `--settings` と CaptureOn metadata の使い分け
- manual evidence 手順
- 必要な README または運用メモ

## 対象外

- runtime code の変更
- test code の変更。ただし docs 変更だけでは不十分と判断した場合は report に理由を記録する
- `TRACKER-050` の PR ready 化
- commit / push / PR 操作
- 追加 `TRACKER` の即時作成

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/codex-delegation-executor/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/implementation-executor/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/tdd-executor/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-doc-maintainer/SKILL.md`
- `sed -n '1,260p' reports/tracker-049-operation-docs-implementation-20260512164929.md`
- `sed -n '1,260p' Tracker/Tracker.Core/Design/tasks-status.md`
- `sed -n '1,220p' Tracker/Tracker.Core/Design/phases-status.md`
- `rg -n "CaptureOn|CaptureReplay|snapshot|sidecar|Tracker:Receive|Receive:Enabled|multicast|diagnostics|manual evidence|metadata|--settings|SSL_VISION|Vision" README* Tracker reports -g '*.md'`
- `sed -n '1,320p' Tracker/Tracker.Server/README.md`
- `sed -n '88,135p' Tracker/Tracker.Server/Vision/VisionPacketCaptureFile.cs`
- `sed -n '130,160p' Tracker/Tracker.Server/Vision/VisionPacketCaptureSession.cs`
- `sed -n '1,125p' Tracker/Tracker.Server/Program.cs`
- `sed -n '220,270p' Tracker/Tracker.CaptureReplay/ReplayOptions.cs`
- `sed -n '114,255p' Tracker/Tracker.Server/README.md`
- `sed -n '255,345p' Tracker/Tracker.Server/README.md`
- `rg -n "Tracker:Receive:Enabled|multicast endpoint|CaptureOn session folder|tracker-packet-snapshots\\.jsonl|TrackerSnapshotSidecarPath|TrackerSnapshotLog|TrackerSnapshotSources|trackerSnapshot|trackerComparison|--settings|manual evidence|/diagnostics|rawPayloadRestored|nearest-timestamp" Tracker/Tracker.Server/README.md`
- `git diff -- Tracker/Tracker.Server/README.md`
- `git diff --check`
- dotnet build / test は未実行。今回は Markdown-only の運用手順追加で runtime code / test code を変更しておらず、期待検証も README の `sed` / `rg` 確認と `git diff --check` で足りるため。

## 対象ファイル

- 変更: `Tracker/Tracker.Server/README.md`
- 変更: `reports/tracker-049-operation-docs-implementation-20260512164929.md`
- 参照: `Tracker/Tracker.Core/Design/tasks-status.md`
- 参照: `Tracker/Tracker.Core/Design/phases-status.md`
- 参照: `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- 参照: `Tracker/Tracker.Server/appsettings.json`
- 参照: `Tracker/Tracker.Server/Program.cs`
- 参照: `Tracker/Tracker.Server/Vision/VisionPacketCaptureFile.cs`
- 参照: `Tracker/Tracker.Server/Vision/VisionPacketCaptureSession.cs`
- 参照: `Tracker/Tracker.CaptureReplay/ReplayOptions.cs`
- 参照: `Tracker/Tracker.CaptureReplay/Program.cs`

## 指摘事項

- blocking findings なし。
- runtime code / test code の不足で TRACKER を即時追加すべき事項は見つけていない。
- `Tracker:Receive:Enabled`、active profile の multicast endpoint、CaptureOn session folder、snapshot sidecar、metadata、`Tracker.CaptureReplay`、diagnostics、manual evidence は README 上で辿れる状態にした。
- `--settings` は tracker settings 解決にも使われるため、CaptureOn 比較ログの normal path では metadata を渡すこと、手元の `appsettings.json` や手書き metadata は意図的な再評価・互換用途として扱うことを明記した。

## 結果

- `Tracker/Tracker.Server/README.md` に CaptureOn 比較ログの運用手順を追記した。
- `VisionReceiver:PacketCapture` の成果物説明を CaptureOn session folder 前提へ更新し、`tracker-packet-snapshots.jsonl`、`TrackerSnapshotSidecarPath`、`TrackerSnapshotLog`、`TrackerSnapshotSources` を明記した。
- `CaptureOn 比較ログの manual evidence` セクションを追加し、`Tracker:Receive:Enabled`、multicast endpoint、session folder、metadata / sidecar 確認、CaptureReplay 出力、diagnostics 確認、report 証跡項目を手順化した。
- `Tracker:Receive` セクションを追加し、既定無効、active profile の publish multicast address / port を監視すること、`InterfaceAddress` の使いどころを説明した。
- `sed` で追加セクションを確認し、`rg` で exit criteria の主要語が README に存在することを確認した。
- `git diff --check` は成功した。

## リスク

- 実機または live multicast 環境での manual evidence 取得はこの worker の範囲外で、今回の検証は文書・既存実装名との整合確認に限定した。
- dotnet build / test は未実行。Markdown-only 変更のため runtime regression は発生しない想定だが、最終 review gate では README 文言と実装の読み合わせを別途実施する必要がある。
